using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Skill
{
    /// <summary>
    /// 지속 장판. 수명 동안 틱마다 반경 내 대상에 효과를 적용한다.
    ///  - 고정: 감속 늪·저주 낙인
    ///  - 이동: 화염 회오리 (마왕 → 지정점 방향으로 이동하며 적을 휘감아 일정 거리까지 끌고 감 + 도트딜)
    /// </summary>
    public class SkillZone : MonoBehaviour
    {
        private ITargetProvider _enemies;
        private IAllyProvider _allies;
        private ZoneEffect _effect;
        private ZoneTarget _target;
        private float _radius;
        private float _magnitude;
        private float _tick;
        private float _endTime;
        private float _nextTickTime;
        private float _dotDamage;
        private float _dotTick;
        private float _nextDotTime;

        private bool _moving;
        private Vector3 _moveDir;
        private float _moveSpeed;
        private float _pullForce;

        private readonly List<IDamageable> _enemyBuffer = new List<IDamageable>();
        private readonly List<IHealable> _allyBuffer = new List<IHealable>();

        public void Init(ITargetProvider enemies, IAllyProvider allies, SkillData d, float radiusOverride = -1f, float durationOverride = -1f)
        {
            _enemies = enemies;
            _allies = allies;
            _effect = d.zoneEffect;
            _target = d.zoneTarget;
            _radius = radiusOverride > 0f ? radiusOverride : d.radius;
            _magnitude = d.zoneMagnitude;
            _tick = Mathf.Max(0.05f, d.zoneTick);
            _dotDamage = d.onHitDotDamage;   // 장판 안에 있는 동안 틱마다 들어가는 도트(독 늪 등)
            _dotTick = Mathf.Max(0.1f, d.onHitDotTick);
            _endTime = Time.time + (durationOverride > 0f ? durationOverride : d.duration);
        }

        /// <summary>화염 회오리처럼 이동 + 끌어당기는 장판.</summary>
        public void SetMoving(Vector3 direction, float speed, float pullForce)
        {
            _moving = true;
            _moveDir = direction.normalized;
            _moveSpeed = speed;
            _pullForce = pullForce;
        }

        private void Update()
        {
            if (Time.time >= _endTime)
            {
                Destroy(gameObject);
                return;
            }

            if (_moving)
            {
                transform.position += _moveDir * (_moveSpeed * Time.deltaTime);
                if (IsOffScreen(transform.position, _radius)) { Destroy(gameObject); return; } // 화면 밖으로 완전히 나가면 즉시 사라짐
                ShoveEnemies();
            }

            if (Time.time < _nextTickTime)
            {
                return;
            }

            _nextTickTime = Time.time + _tick;
            ApplyTick();
        }

        /// <summary>장판이 카메라 화면 밖으로 완전히 벗어났는지(반경만큼 여유). 카메라를 못 쓰면(비직교 등) false.</summary>
        private static bool IsOffScreen(Vector3 pos, float margin)
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return false;
            float h = cam.orthographicSize + margin;
            float w = cam.orthographicSize * cam.aspect + margin;
            Vector3 c = cam.transform.position;
            return Mathf.Abs(pos.x - c.x) > w || Mathf.Abs(pos.y - c.y) > h;
        }

        // 회오리가 한 대상을 끌고 갈 수 있는 총 거리 = pullForce × 0.5 (화염 회오리: force 8 → 4칸). 이 거리를 채우면 풀어 준다.
        private const float CarryDistanceFactor = 0.5f;
        // 끌려가는 속도는 회오리 속도의 95% — 회오리 안에 머물며 휘감겨 가고, 앞으로 튕겨 나가지 않는다.
        private const float CarrySpeedFactor = 0.95f;
        private readonly Dictionary<IDamageable, float> _carried = new Dictionary<IDamageable, float>();

        /// <summary>
        /// 회오리 안의 적을 진행 방향으로 "휘감아 끌고 간다". 예전에는 회오리 속도의 6배로 밀쳐서(force 8 → 초당 48) 용사가
        /// 회오리를 앞질러 화면 끝까지 날아갔다. 이제는 회오리와 비슷한 속도로, 한 대상당 정해진 거리까지만 끌고 가고,
        /// 중심 쪽으로 살짝 당겨 회오리 안에 머물게 한다(그동안 도트 피해를 계속 받는다).
        /// </summary>
        private void ShoveEnemies()
        {
            if (_enemies == null || _pullForce <= 0f) return;
            float maxCarry = _pullForce * CarryDistanceFactor;
            _enemyBuffer.Clear();
            _enemies.QueryInRadius(transform.position, _radius, _enemyBuffer);
            foreach (var e in _enemyBuffer)
            {
                if (e.IsDead) continue;
                _carried.TryGetValue(e, out float used);
                if (used >= maxCarry) continue; // 충분히 끌고 왔으면 놓아 준다

                Vector3 toCenter = transform.position - e.Position;
                toCenter.z = 0f;
                float inward = Mathf.Clamp01(toCenter.magnitude / Mathf.Max(0.01f, _radius)); // 바깥쪽일수록 중심으로 당김
                Vector3 dir = (_moveDir + toCenter.normalized * (0.6f * inward)).normalized;
                float step = Mathf.Min(_moveSpeed * CarrySpeedFactor * Time.deltaTime, maxCarry - used);
                (e as IStatusReceiver)?.ApplyKnockback(dir, step);
                _carried[e] = used + step;
            }
        }

        private void ApplyTick()
        {
            if (_target == ZoneTarget.Allies)
            {
                if (_allies == null) return;
                _allyBuffer.Clear();
                _allies.QueryAlliesInRadius(transform.position, _radius, _allyBuffer);
                foreach (var a in _allyBuffer)
                {
                    if (_effect == ZoneEffect.Heal)
                    {
                        a.Heal(_magnitude);
                    }
                }

                return;
            }

            if (_enemies == null) return;
            _enemyBuffer.Clear();
            _enemies.QueryInRadius(transform.position, _radius, _enemyBuffer);
            bool dotNow = _dotDamage > 0f && Time.time >= _nextDotTime;
            if (dotNow) _nextDotTime = Time.time + _dotTick;
            foreach (var e in _enemyBuffer)
            {
                if (dotNow) e.TakeDamage(_dotDamage);
                var status = e as IStatusReceiver;
                switch (_effect)
                {
                    case ZoneEffect.Stun:
                        status?.ApplyStun(_tick + 0.05f);
                        break;
                    case ZoneEffect.Slow:
                        status?.ApplySlow(_magnitude, _tick + 0.05f);
                        break;
                    case ZoneEffect.Vulnerable:
                        status?.ApplyVulnerable(_magnitude, _tick + 0.05f);
                        break;
                    case ZoneEffect.DamageOverTime:
                        if (_magnitude > 0f) e.TakeDamage(_magnitude); // 0이면 피해 없는 장판(이동·휘감기만 확인할 때)
                        break;
                }
            }
        }
    }
}
