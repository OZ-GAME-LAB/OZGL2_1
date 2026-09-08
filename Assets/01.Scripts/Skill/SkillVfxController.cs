using System.Collections;
using UnityEngine;

namespace OZGL2.Skill
{
    /// <summary>
    /// SkillManager.Casted 를 구독해 스킬 VFX 프리팹을 재생한다.
    /// SkillData 에 프리팹이 지정돼 있으면 그것을, 없으면 아무것도 안 함(SkillBarUI 의 코드 기본 연출이 대신).
    ///
    /// SkillManager 만 참조 — 씬 구조에 독립적. 실제 씬에서도 Bind 한 번이면 그대로 동작.
    /// </summary>
    public class SkillVfxController : MonoBehaviour
    {
        [Tooltip("발사체가 조준점까지 날아가는 시간(초)")]
        [SerializeField] private float _projectileTravelTime = 0.22f;

        [Tooltip("프리팹에 파티클이 없을 때 자동 파괴까지 대기(초)")]
        [SerializeField] private float _fallbackLifetime = 2f;

        private SkillManager _manager;
        private Transform _casterAnchor;

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

        private void OnCasted(SkillCastEvent e)
        {
            bool projectileFirst = e.Data.projectileVfx != null && e.Data.castMode == SkillCastMode.Targeted;

            if (projectileFirst)
            {
                StartCoroutine(PlayWithProjectile(e));
            }
            else
            {
                PlayImpacts(e);
            }
        }

        private IEnumerator PlayWithProjectile(SkillCastEvent e)
        {
            Vector3 from = _casterAnchor != null ? _casterAnchor.position : Vector3.zero;
            Vector3 to = e.CastPoint;

            var proj = Instantiate(e.Data.projectileVfx, from, LookRotation(to - from), transform);
            float t = 0f;
            while (t < _projectileTravelTime)
            {
                t += Time.deltaTime;
                proj.transform.position = Vector3.Lerp(from, to, t / _projectileTravelTime);
                yield return null;
            }

            Destroy(proj, 0.1f);
            PlayImpacts(e);
        }

        private void PlayImpacts(SkillCastEvent e)
        {
            if (e.Data.castVfx != null)
            {
                var fx = Instantiate(e.Data.castVfx, e.CastPoint, Quaternion.identity, transform);
                if (e.Data.scaleCastVfxToRadius && e.Data.radius > 0f)
                {
                    fx.transform.localScale = Vector3.one * e.Data.radius;
                }

                AutoDestroy(fx);
            }

            if (e.Data.perTargetVfx != null && e.HitPoints != null)
            {
                foreach (var point in e.HitPoints)
                {
                    AutoDestroy(Instantiate(e.Data.perTargetVfx, point, Quaternion.identity, transform));
                }
            }
        }

        private void AutoDestroy(GameObject go)
        {
            var ps = go.GetComponentInChildren<ParticleSystem>();
            float life = ps != null ? ps.main.duration + ps.main.startLifetime.constantMax : _fallbackLifetime;
            Destroy(go, Mathf.Max(life, 0.5f));
        }

        private static Quaternion LookRotation(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f)
            {
                return Quaternion.identity;
            }

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angle);
        }
    }
}
