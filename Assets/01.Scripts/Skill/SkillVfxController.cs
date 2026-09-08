using System.Collections;
using UnityEngine;

namespace OZGL2.Skill
{
    /// <summary>
    /// SkillManager.Casted 를 구독해 스킬 연출을 재생한다.
    ///  - SkillData 에 VFX 프리팹이 지정돼 있으면 그것을 인스턴스화
    ///  - 없으면 코드로 그린 기본 연출(확산 링·섬광 등) — 프리팹이 깨져도/비어도 스킬이 "안 보이는" 일이 없게 하는 안전망
    ///
    /// SkillManager 만 참조 — 씬 구조에 독립적. 실제 씬에서도 Bind 한 번이면 그대로 동작.
    /// 정식 VFX 는 SO_Skill 에셋에 프리팹만 채우면 자동으로 프리팹 경로를 탄다(코드 변경 0).
    /// </summary>
    public class SkillVfxController : MonoBehaviour
    {
        [SerializeField] private float _projectileTravelTime = 0.22f;
        [SerializeField] private float _fallbackLifetime = 2f;
        [Tooltip("VFX 프리팹의 사운드 재생 여부")]
        [SerializeField] private bool _playSfx = false;

        private SkillManager _manager;
        private Transform _casterAnchor;
        private Sprite _disc;
        private Sprite _ring;

        public void Bind(SkillManager manager, Vector3 casterWorldPosition)
        {
            if (_manager != null)
            {
                _manager.Casted -= OnCasted;
            }

            _manager = manager;
            _manager.Casted += OnCasted;

            var anchor = new GameObject("CasterAnchor");
            anchor.transform.SetParent(transform);
            anchor.transform.position = casterWorldPosition;
            _casterAnchor = anchor.transform;
        }

        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.Casted -= OnCasted;
            }
        }

        // ─────────────────────────────────────────── 라우팅

        private void OnCasted(SkillCastEvent e)
        {
            bool hasProjectile = e.Data.projectileVfx != null && e.Data.castMode == SkillCastMode.Targeted;
            bool needsProjectile = e.Data.effectType == SkillEffectType.AreaDamage
                                   && e.Data.castMode == SkillCastMode.Targeted;

            if (hasProjectile)
            {
                StartCoroutine(PrefabProjectile(e));
            }
            else if (needsProjectile)
            {
                StartCoroutine(CodeProjectile(e));
            }
            else
            {
                PlayImpacts(e);
            }
        }

        private void PlayImpacts(SkillCastEvent e)
        {
            if (e.Data.castVfx != null)
            {
                var fx = Spawn(e.Data.castVfx, e.CastPoint, Quaternion.identity);
                if (e.Data.scaleCastVfxToRadius && e.Data.radius > 0f)
                {
                    fx.transform.localScale = Vector3.one * e.Data.radius;
                }

                AutoDestroy(fx);
            }
            else
            {
                CodeImpact(e);
            }

            if (e.HitPoints != null)
            {
                foreach (var point in e.HitPoints)
                {
                    if (e.Data.perTargetVfx != null)
                    {
                        AutoDestroy(Spawn(e.Data.perTargetVfx, point, Quaternion.identity));
                    }
                    else
                    {
                        CodePerTarget(e, point);
                    }
                }
            }
        }

        // ─────────────────────────────────────────── 프리팹 경로

        private IEnumerator PrefabProjectile(SkillCastEvent e)
        {
            Vector3 from = CasterPos();
            var proj = Spawn(e.Data.projectileVfx, from, LookRotation(e.CastPoint - from));
            yield return LerpMove(proj.transform, from, e.CastPoint, _projectileTravelTime);
            Destroy(proj, 0.1f);
            PlayImpacts(e);
        }

        private GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            var go = Instantiate(prefab, pos, rot, transform);
            if (!_playSfx)
            {
                foreach (var audio in go.GetComponentsInChildren<AudioSource>())
                {
                    audio.enabled = false;
                }
            }

            return go;
        }

        private void AutoDestroy(GameObject go)
        {
            var ps = go.GetComponentInChildren<ParticleSystem>();
            float life = ps != null ? ps.main.duration + ps.main.startLifetime.constantMax : _fallbackLifetime;
            Destroy(go, Mathf.Max(life, 0.5f));
        }

        // ─────────────────────────────────────────── 코드 기본 연출 (안전망)

        private IEnumerator CodeProjectile(SkillCastEvent e)
        {
            Vector3 from = CasterPos();
            var go = MakeSprite(_disc ??= MakeDisc(), new Color(1f, 0.62f, 0.16f), from, 0.4f, order: 22);
            yield return LerpMove(go.transform, from, e.CastPoint, _projectileTravelTime);
            Destroy(go);
            PlayImpacts(e);
        }

        private void CodeImpact(SkillCastEvent e)
        {
            switch (e.Data.effectType)
            {
                case SkillEffectType.AreaDamage:
                    StartCoroutine(ExpandFade(e.CastPoint, e.Data.radius, new Color(1f, 0.5f, 0.15f), 0.35f, ringOnly: false));
                    break;
                case SkillEffectType.AreaStun:
                    StartCoroutine(ExpandFade(e.CastPoint, Mathf.Min(e.Data.radius, 12f), new Color(0.4f, 0.8f, 1f), 0.5f, ringOnly: true));
                    break;
            }
        }

        private void CodePerTarget(SkillCastEvent e, Vector3 point)
        {
            Color c = e.Data.effectType switch
            {
                SkillEffectType.ChainDamage => new Color(1f, 0.95f, 0.4f),
                SkillEffectType.HealAllies => new Color(0.4f, 1f, 0.5f),
                _ => Color.white,
            };

            StartCoroutine(ExpandFade(point, 0.9f, c, 0.28f, ringOnly: false));
        }

        private IEnumerator ExpandFade(Vector3 center, float targetRadius, Color color, float duration, bool ringOnly)
        {
            Sprite sprite = ringOnly ? (_ring ??= MakeRing()) : (_disc ??= MakeDisc());
            var go = MakeSprite(sprite, color, center, 0.1f, order: 21);
            var sr = go.GetComponent<SpriteRenderer>();

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, targetRadius * 2f, k);
                sr.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.85f, 0f, k));
                yield return null;
            }

            Destroy(go);
        }

        // ─────────────────────────────────────────── 유틸

        private Vector3 CasterPos()
        {
            return _casterAnchor != null ? _casterAnchor.position : Vector3.zero;
        }

        private static IEnumerator LerpMove(Transform tr, Vector3 from, Vector3 to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                tr.position = Vector3.Lerp(from, to, t / duration);
                yield return null;
            }
        }

        private GameObject MakeSprite(Sprite sprite, Color color, Vector3 pos, float scale, int order)
        {
            var go = new GameObject("CodeVfx");
            go.transform.SetParent(transform);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        private static Quaternion LookRotation(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        private Sprite MakeDisc()
        {
            return MakeCircle(64, 0f);
        }

        private Sprite MakeRing()
        {
            return MakeCircle(96, 0.82f);
        }

        private static Sprite MakeCircle(int size, float innerFraction)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float outer = size * 0.5f - 1f;
            float inner = outer * innerFraction;
            var center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float a = Mathf.Clamp01(outer - d) * (innerFraction > 0f ? Mathf.Clamp01(d - inner) : 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
