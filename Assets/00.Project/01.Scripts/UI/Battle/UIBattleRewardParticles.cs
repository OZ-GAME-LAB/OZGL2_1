using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 표시 연출의 진행률만 받아 그린다. 경험치 값과 게임플레이 난수에는 관여하지 않는다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Battle Reward Particles")]
    public sealed class UIBattleRewardParticles : MaskableGraphic
    {
        private const int PARTICLE_COUNT = 28;
        private const float EMISSION_RATE = 28f;

        [SerializeField] private TMP_Text _source;
        [SerializeField] private RectTransform _destination;
        [SerializeField, Range(0.05f, 0.85f)] private float _flightDuration = 0.55f;
        [SerializeField, Min(0.5f)] private float _minRadius = 3f;
        [SerializeField, Min(0.5f)] private float _maxRadius = 5f;
        [SerializeField, Min(0f)] private float _fanWidth = 34f;

        private struct Particle
        {
            public Vector2 Start;
            public Vector2 ControlA;
            public Vector2 ControlB;
            public Vector2 End;
            public float Lifetime;
            public float Radius;
            public float Brightness;
        }

        private readonly Particle[] _particles = new Particle[PARTICLE_COUNT];
        private float _duration;
        private float _elapsed;
        private float _emissionInterval;
        private int _emissionCount;
        private bool _isPlaying;
        private int _visibleParticleCount;

        public int VisibleParticleCount => _visibleParticleCount;
        public bool HasTargets => _source != null && _destination != null;

        public void Begin(float duration)
        {
            Clear();
            if (!HasTargets || !isActiveAndEnabled || duration <= 0f ||
                float.IsNaN(duration) || float.IsInfinity(duration)) return;

            _source.ForceMeshUpdate(true);
            GetSourceBounds(out Vector2 sourceCenter, out Vector2 sourceSpan);
            Rect destinationRect = _destination.rect;
            _duration = duration;
            float maxLifetime = Mathf.Min(duration * 0.65f, Mathf.Clamp(_flightDuration, 0.05f, 0.85f));
            float emissionDuration = duration - maxLifetime;
            _emissionCount = Mathf.Max(2, Mathf.CeilToInt(emissionDuration * EMISSION_RATE) + 1);
            _emissionInterval = emissionDuration / (_emissionCount - 1);
            for (int index = 0; index < PARTICLE_COUNT; index++)
            {
                float variation = GetVariation(index, 1);
                float lifetime = maxLifetime * Mathf.Lerp(0.8f, 1f, GetVariation(index, 2));
                if (index == (_emissionCount - 1) % PARTICLE_COUNT) lifetime = maxLifetime;
                Vector2 start = sourceCenter + sourceSpan * Mathf.Lerp(-0.4f, 0.4f, variation);
                Vector2 destinationLocal = new Vector2(
                    Mathf.Lerp(destinationRect.xMin, destinationRect.xMax,
                        Mathf.Lerp(0.18f, 0.92f, GetVariation(index, 3))),
                    Mathf.Lerp(destinationRect.yMin, destinationRect.yMax,
                        Mathf.Lerp(0.2f, 0.8f, GetVariation(index, 4))));
                Vector2 end = rectTransform.InverseTransformPoint(_destination.TransformPoint(destinationLocal));
                Vector2 direction = end - start;
                Vector2 perpendicular = direction.sqrMagnitude > 0.001f
                    ? new Vector2(-direction.y, direction.x).normalized : Vector2.right;
                float fan = Mathf.Lerp(-_fanWidth, _fanWidth, GetVariation(index, 5));
                _particles[index] = new Particle
                {
                    Start = start,
                    ControlA = start + direction * 0.12f + perpendicular * fan,
                    ControlB = end - direction * 0.24f + perpendicular * fan * 0.2f,
                    End = end,
                    Lifetime = lifetime,
                    Radius = Mathf.Lerp(Mathf.Max(0.5f, _minRadius),
                        Mathf.Max(_minRadius, _maxRadius), GetVariation(index, 6)),
                    Brightness = Mathf.Lerp(0.65f, 1f, GetVariation(index, 7))
                };
            }

            _isPlaying = true;
            SetProgress(0f);
        }

        public void SetProgress(float progress)
        {
            if (!_isPlaying) return;
            if (!HasTargets || !isActiveAndEnabled || float.IsNaN(progress) || float.IsInfinity(progress))
            {
                Clear();
                return;
            }

            _elapsed = Mathf.Clamp01(progress) * _duration;
            _visibleParticleCount = 0;
            if (progress >= 1f)
            {
                Clear();
                return;
            }

            for (int index = 0; index < PARTICLE_COUNT; index++)
            {
                float age = GetParticleAge(index);
                if (age > 0f && age < 1f) _visibleParticleCount++;
            }
            SetVerticesDirty();
        }

        public void Clear()
        {
            _isPlaying = false;
            _visibleParticleCount = 0;
            _elapsed = 0f;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            raycastTarget = false;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            Clear();
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_isPlaying || !HasTargets) return;
            for (int index = 0; index < PARTICLE_COUNT; index++)
            {
                Particle particle = _particles[index];
                float age = GetParticleAge(index);
                if (age <= 0f || age >= 1f) continue;

                float alpha = Mathf.Clamp01(age / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.76f, 1f, age))) * particle.Brightness;
                float radius = particle.Radius * Mathf.Lerp(1f, 0.12f,
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, age)));
                Vector2 position = EvaluatePosition(particle, age);
                Color tint = color;
                tint.a *= alpha;

                // 진행 방향 뒤에 짧은 잔광을 남기고 도착 직전에 크기를 줄인다.
                for (int trail = 2; trail >= 1; trail--)
                {
                    float trailAge = Mathf.Max(0f, age - trail * 0.045f);
                    Color trailTint = tint;
                    trailTint.a *= trail == 1 ? 0.3f : 0.12f;
                    UIRadialGlow.AddGlow(vh, EvaluatePosition(particle, trailAge),
                        Vector2.one * radius * (trail == 1 ? 1.4f : 1.1f), trailTint, 10, 3);
                }

                Color glowTint = tint;
                glowTint.a *= 0.5f;
                UIRadialGlow.AddGlow(vh, position, Vector2.one * radius * 2.2f, glowTint, 12, 4);
                Color coreTint = Color.Lerp(tint, Color.white, 0.45f);
                coreTint.a = tint.a;
                UIRadialGlow.AddGlow(vh, position, Vector2.one * radius, coreTint, 10, 3);
            }
        }

        private void GetSourceBounds(out Vector2 center, out Vector2 span)
        {
            TMP_TextInfo textInfo = _source.textInfo;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            int visibleCount = 0;
            for (int index = textInfo.characterCount - 1; index >= 0 && visibleCount < 3; index--)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[index];
                if (!character.isVisible) continue;
                min = Vector2.Min(min, character.bottomLeft);
                max = Vector2.Max(max, character.topRight);
                visibleCount++;
            }

            Vector2 localCenter = visibleCount > 0 ? (min + max) * 0.5f : _source.rectTransform.rect.center;
            float width = visibleCount > 0 ? max.x - min.x : 0f;
            center = rectTransform.InverseTransformPoint(_source.transform.TransformPoint(localCenter));
            span = rectTransform.InverseTransformVector(_source.transform.TransformVector(new Vector3(width, 0f, 0f)));
        }

        private static Vector2 EvaluatePosition(Particle particle, float age)
        {
            float progress = Mathf.Pow(Mathf.Clamp01(age), 1.25f);
            float inverse = 1f - progress;
            return inverse * inverse * inverse * particle.Start +
                3f * inverse * inverse * progress * particle.ControlA +
                3f * inverse * progress * progress * particle.ControlB +
                progress * progress * progress * particle.End;
        }

        private float GetParticleAge(int index)
        {
            // 고정 슬롯을 방출 순서로 재사용하여 긴 연출도 같은 밀도로 이어간다.
            int latestEmission = Mathf.Min(_emissionCount - 1,
                Mathf.FloorToInt(_elapsed / _emissionInterval));
            if (latestEmission < index) return -1f;
            int emission = latestEmission - (latestEmission - index) % PARTICLE_COUNT;
            return (_elapsed - emission * _emissionInterval) / _particles[index].Lifetime;
        }

        private static float GetVariation(int index, int salt)
        {
            // 재생마다 같은 궤적을 만들며 UnityEngine.Random 상태를 소비하지 않는다.
            uint value = unchecked((uint)((index + 1) * 73856093 ^ salt * 19349663));
            value ^= value >> 13;
            value = unchecked(value * 1274126177u);
            return (value & 0x00FFFFFFu) / 16777215f;
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            color = new Color32(255, 203, 77, 255);
            raycastTarget = false;
            base.Reset();
        }

        protected override void OnValidate()
        {
            raycastTarget = false;
            base.OnValidate();
        }
#endif
    }
}
