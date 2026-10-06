using System;
using System.Collections.Generic;
using OZGL2.Contracts;
using OZGL2.Skill;
using OZGL2.Synergy;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 마왕이 스킬을 쓰는 순간을 마왕에게서 뿜어져 나오게 연출한다. 팀의 스킬 실행기(SkillExecutor)가 만드는 효과·피해 판정은 그대로 두고,
    /// 시전 이벤트(SkillManager.CastRequested)에 맞춰 마왕 쪽에 얹기만 한다.
    /// - 발밑에 마법진 고리가 퍼지고, 가슴에서 빛이 터지며, 마왕이 한 번 크게 부푼다.
    /// - 마왕에서 시전 지점까지 빛줄기가 뻗고, 불꽃 알갱이가 그 길을 따라 날아간다.
    /// 그림은 전부 코드로 만든 부드러운 원·고리·줄이라 별도 이미지가 필요 없다.
    /// </summary>
    public sealed class InGameKingCastFx : MonoBehaviour
    {
        [SerializeField] private Color _core = new Color(1f, 0.80f, 0.38f, 1f);
        [SerializeField] private Color _edge = new Color(0.86f, 0.32f, 1f, 1f);
        [SerializeField, Range(0.5f, 3f), Tooltip("연출 전체의 크기·밝기 배율")] private float _intensity = 1.3f;
        [SerializeField, Range(0f, 0.4f), Tooltip("시전 순간 마왕이 부푸는 정도")] private float _punch = 0.16f;
        [SerializeField] private int _sortingOrder = 1015;

        private sealed class Fx
        {
            public Transform tf;
            public SpriteRenderer sr;
            public float age, life;
            public Action<Fx, float> tick; // (효과, 진행도 0~1)
        }

        private readonly List<Fx> _active = new List<Fx>();
        private readonly Stack<Fx> _pool = new Stack<Fx>();
        private RealSynergySync _sync;
        private SkillManager _hooked;
        private Transform _king;
        private Vector3 _kingBaseScale = Vector3.one;
        private float _punchT = 1f;
        private float _punchAmount;
        private Sprite _glow, _ring, _dot, _line, _solid;
        private float _shakeT, _shakeLen, _shakeAmp;

        private void OnDisable()
        {
            Unhook();
            RestoreKing();
            foreach (var fx in _active) if (fx.tf != null) Destroy(fx.tf.gameObject);
            _active.Clear();
            while (_pool.Count > 0) { var fx = _pool.Pop(); if (fx.tf != null) Destroy(fx.tf.gameObject); }
        }

        private void Unhook()
        {
            if (_hooked != null) _hooked.CastRequested -= OnCast;
            _hooked = null;
        }

        private void Update()
        {
            if (_sync == null) _sync = FindFirstObjectByType<RealSynergySync>();
            var manager = _sync != null ? _sync.SkillManager : null;
            if (manager != _hooked)
            {
                Unhook();
                if (manager != null) { manager.CastRequested += OnCast; _hooked = manager; }
            }

            float dt = Time.unscaledDeltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var fx = _active[i];
                fx.age += dt;
                float k = Mathf.Clamp01(fx.age / fx.life);
                fx.tick(fx, k);
                if (k >= 1f) { Release(fx); _active.RemoveAt(i); }
            }
            TickKingPunch(dt);
            TickShake(dt);
        }

        // ───────────── 궁극기: 화면 전체를 쓰는 연출

        /// <summary>궁극기마다 색을 달리한다(유성우=불, 절대영도=얼음, 심판=금빛 하양).</summary>
        private void UltimateColors(SkillData data, out Color core, out Color edge)
        {
            string key = (data.name + data.skillId).ToLowerInvariant();
            if (key.Contains("absolute") || key.Contains("zero")) { core = new Color(0.75f, 0.95f, 1f); edge = new Color(0.25f, 0.55f, 1f); }
            else if (key.Contains("judg")) { core = new Color(1f, 0.96f, 0.7f); edge = new Color(1f, 0.75f, 0.2f); }
            else { core = new Color(1f, 0.72f, 0.28f); edge = new Color(1f, 0.25f, 0.08f); }
        }

        private void PlayUltimate(Vector3 feet, Vector3 chest, Color core, Color edge, float s)
        {
            // 마왕 위로 솟는 빛기둥
            Spawn(_glow, chest + Vector3.up * 6f, 1.25f, (fx, k) =>
            {
                float width = Mathf.Lerp(0.5f, 2.6f, Mathf.Sin(k * Mathf.PI)) * s;
                fx.tf.localScale = new Vector3(width, 22f, 1f);
                fx.sr.color = Alpha(Color.Lerp(Color.white, core, k), Mathf.Sin(Mathf.Clamp01(k * 1.05f) * Mathf.PI) * 0.85f);
            });
            // 맵 끝까지 퍼지는 충격파 고리 세 겹
            for (int i = 0; i < 3; i++)
            {
                float delay = i * 0.2f;
                Spawn(_ring, feet, 1.5f + delay, (fx, k) =>
                {
                    float t = Mathf.Clamp01((fx.age - delay) / 1.5f);
                    float e = 1f - (1f - t) * (1f - t) * (1f - t);
                    fx.tf.localScale = Vector3.one * Mathf.Lerp(1f, 34f, e);
                    fx.sr.color = Alpha(Color.Lerp(core, edge, t), (fx.age < delay ? 0f : 1f - t) * 0.85f);
                });
            }
            // 화면 전체 번쩍임
            Spawn(_solid, chest, 0.9f, (fx, k) =>
            {
                fx.tf.localScale = Vector3.one * 300f;
                fx.sr.color = Alpha(Color.Lerp(Color.white, core, k * 1.4f), Mathf.Pow(1f - k, 2f) * 0.6f);
                fx.sr.sortingOrder = _sortingOrder + 20;
            });
            // 마왕 둘레에서 한동안 이어서 피어오르는 불꽃 알갱이
            for (int i = 0; i < 46; i++)
            {
                float delay = UnityEngine.Random.value * 1.6f;
                float angle = UnityEngine.Random.value * 6.2832f;
                float radius = UnityEngine.Random.Range(0.6f, 2.8f);
                float rise = UnityEngine.Random.Range(2.5f, 6.5f);
                float size = UnityEngine.Random.Range(0.18f, 0.42f) * s;
                Color tint = Color.Lerp(core, edge, UnityEngine.Random.value);
                Spawn(_dot, feet, 1.1f + delay, (fx, k) =>
                {
                    float t = Mathf.Clamp01((fx.age - delay) / 1.1f);
                    fx.tf.position = feet + new Vector3(Mathf.Cos(angle) * radius * (1f - 0.5f * t), Mathf.Sin(angle) * radius * 0.35f + rise * t, 0f);
                    fx.tf.localScale = Vector3.one * size * (1f - 0.7f * t);
                    fx.sr.color = Alpha(tint, fx.age < delay ? 0f : (1f - t));
                });
            }
            _shakeT = 0f; _shakeLen = 1.1f; _shakeAmp = 0.22f;
        }

        private void TickShake(float dt)
        {
            if (_shakeT >= _shakeLen) { if (InGameCameraControl.Shake != Vector2.zero) InGameCameraControl.Shake = Vector2.zero; return; }
            _shakeT += dt;
            float k = 1f - Mathf.Clamp01(_shakeT / _shakeLen);
            InGameCameraControl.Shake = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * _shakeAmp * k * k;
        }

        // ───────────── 시전

        private void OnCast(SkillCastRequest request)
        {
            EnsureSprites();
            if (_king == null) { var go = GameObject.Find("DemonKing"); if (go != null) { _king = go.transform; _kingBaseScale = _king.localScale; } }
            Vector3 feet = _king != null ? _king.position : (UnitRegistry.KingWorldPosition ?? Vector3.zero);
            Vector3 chest = feet + Vector3.up * 0.75f * (_king != null ? _king.localScale.y : 1f);
            var data = request.Skill != null ? request.Skill.Data : null;
            bool ultimate = data != null && data.category == SkillCategory.Ultimate;
            Color core = _core, edge = _edge;
            if (ultimate) UltimateColors(data, out core, out edge);
            float s = _intensity * (ultimate ? 1.8f : 1f);
            if (ultimate) PlayUltimate(feet, chest, core, edge, s);

            // 발밑 마법진: 납작한 고리가 두 번 퍼진다
            for (int i = 0; i < 2; i++)
            {
                float delay = i * 0.12f;
                Spawn(_ring, feet + Vector3.down * 0.15f, 0.55f + delay, (fx, k) =>
                {
                    float t = Mathf.Clamp01((fx.age - delay) / 0.55f);
                    float e = 1f - (1f - t) * (1f - t);
                    fx.tf.localScale = new Vector3(Mathf.Lerp(0.8f, 3.4f, e) * s, Mathf.Lerp(0.8f, 3.4f, e) * s * 0.45f, 1f);
                    fx.sr.color = Alpha(Color.Lerp(_core, _edge, t), (fx.age < delay ? 0f : 1f - t) * 0.95f);
                });
            }
            // 가슴에서 터지는 빛
            Spawn(_glow, chest, 0.42f, (fx, k) =>
            {
                float e = 1f - (1f - k) * (1f - k);
                fx.tf.localScale = Vector3.one * Mathf.Lerp(0.6f, 3.6f, e) * s;
                fx.sr.color = Alpha(Color.Lerp(Color.white, _core, k), (1f - k) * 0.95f);
            });
            Spawn(_glow, chest, 0.7f, (fx, k) =>
            {
                fx.tf.localScale = Vector3.one * Mathf.Lerp(1.4f, 5.2f, k) * s;
                fx.sr.color = Alpha(_edge, (1f - k) * (1f - k) * 0.55f);
            });
            _punchT = 0f;
            _punchAmount = ultimate ? _punch * 2.2f : _punch;

            // 시전 지점까지 빛줄기와 알갱이
            Vector3 target = request.CastPoint;
            target.z = chest.z;
            Vector3 delta = target - chest;
            float length = delta.magnitude;
            if (length < 0.6f) return;
            Vector3 dir = delta / length;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            Spawn(_line, chest, 0.34f, (fx, k) =>
            {
                float thick = Mathf.Lerp(0.55f, 0.04f, k) * s;
                fx.tf.localScale = new Vector3(length, thick, 1f);
                fx.sr.color = Alpha(Color.Lerp(Color.white, _core, k), (1f - k) * 0.9f);
            }).tf.rotation = Quaternion.Euler(0f, 0f, angle);
            Spawn(_line, chest, 0.5f, (fx, k) =>
            {
                float thick = Mathf.Lerp(1.1f, 0.2f, k) * s;
                fx.tf.localScale = new Vector3(length, thick, 1f);
                fx.sr.color = Alpha(_edge, (1f - k) * 0.35f);
            }).tf.rotation = Quaternion.Euler(0f, 0f, angle);

            int sparks = Mathf.Clamp(Mathf.RoundToInt(length * 1.6f * s), 10, 40);
            Vector3 side = new Vector3(-dir.y, dir.x, 0f);
            for (int i = 0; i < sparks; i++)
            {
                float t0 = UnityEngine.Random.value;
                float speed = UnityEngine.Random.Range(0.8f, 1.5f);
                float jitter = UnityEngine.Random.Range(-0.45f, 0.45f);
                float size = UnityEngine.Random.Range(0.14f, 0.3f) * s;
                float life = Mathf.Lerp(0.3f, 0.55f, UnityEngine.Random.value);
                Color tint = Color.Lerp(_core, _edge, UnityEngine.Random.value * 0.7f);
                Spawn(_dot, chest, life, (fx, k) =>
                {
                    float along = Mathf.Clamp01(t0 * 0.35f + k * speed);
                    fx.tf.position = chest + dir * (length * along) + side * (jitter * (1f - k));
                    fx.tf.localScale = Vector3.one * size * (1f - 0.6f * k);
                    fx.sr.color = Alpha(tint, 1f - k);
                });
            }
        }

        private void TickKingPunch(float dt)
        {
            if (_king == null) return;
            if (_punchT >= 1f) { if (_king.localScale != _kingBaseScale && _punchT >= 1f) RestoreKing(); return; }
            _punchT = Mathf.Min(1f, _punchT + dt / 0.38f);
            float bump = Mathf.Sin(_punchT * Mathf.PI) * _punchAmount;
            _king.localScale = _kingBaseScale * (1f + bump);
            if (_punchT >= 1f) RestoreKing();
        }

        private void RestoreKing()
        {
            if (_king != null) _king.localScale = _kingBaseScale;
        }

        // ───────────── 효과 객체

        private static Color Alpha(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }

        private Fx Spawn(Sprite sprite, Vector3 position, float life, Action<Fx, float> tick)
        {
            Fx fx;
            if (_pool.Count > 0) fx = _pool.Pop();
            else
            {
                var go = new GameObject("KingCastFx");
                go.transform.SetParent(transform, false);
                fx = new Fx { tf = go.transform, sr = go.AddComponent<SpriteRenderer>() };
            }
            fx.tf.gameObject.SetActive(true);
            fx.tf.position = position;
            fx.tf.rotation = Quaternion.identity;
            fx.tf.localScale = Vector3.one;
            fx.sr.sprite = sprite;
            fx.sr.sortingOrder = _sortingOrder;
            fx.age = 0f; fx.life = life; fx.tick = tick;
            _active.Add(fx);
            return fx;
        }

        private void Release(Fx fx)
        {
            fx.tf.gameObject.SetActive(false);
            _pool.Push(fx);
        }

        // ───────────── 코드로 만든 그림

        private void EnsureSprites()
        {
            if (_glow != null) return;
            _solid = MakeSprite(4, 4, (x, y, w, h) => 1f, 0.5f);
            _glow = MakeSprite(64, 64, (x, y, w, h) => { float d = Radial(x, y, w, h); return Mathf.Pow(1f - Mathf.Clamp01(d), 2.2f); }, 0.5f);
            _ring = MakeSprite(64, 64, (x, y, w, h) => { float d = Radial(x, y, w, h); return Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.1f) * (d < 1f ? 1f : 0f); }, 0.5f);
            _dot = MakeSprite(32, 32, (x, y, w, h) => { float d = Radial(x, y, w, h); return Mathf.Pow(1f - Mathf.Clamp01(d), 1.3f); }, 0.5f);
            // 줄: 왼쪽 끝이 가장 밝고 위아래로 부드럽게 퍼진다(가로 1칸, 피벗은 왼쪽 가운데)
            _line = MakeSprite(32, 16, (x, y, w, h) =>
            {
                float v = 1f - Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                float u = 1f - 0.35f * (x / (float)w);
                return Mathf.Pow(Mathf.Clamp01(v), 1.4f) * u;
            }, 0f);
        }

        private static float Radial(int x, int y, int w, int h)
        {
            float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static Sprite MakeSprite(int w, int h, Func<int, int, int, int, float> alpha, float pivotX)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y, w, h)) * 255f));
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(pivotX, 0.5f), Mathf.Max(w, h) == w ? w : h * 2f);
        }
    }
}
