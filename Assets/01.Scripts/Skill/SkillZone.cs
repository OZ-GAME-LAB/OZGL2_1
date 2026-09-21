using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Skill
{
    /// <summary>
    /// 지속 장판. 수명 동안 틱마다 반경 내 대상에 효과를 적용한다.
    ///  - 고정: 빙결 결계·감속 늪·저주 낙인
    ///  - 이동: 화염 회오리 (마왕 → 지정점 방향으로 이동하며 끌어당김 + 도트딜)
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

        private void ShoveEnemies()
        {
            if (_enemies == null || _pullForce <= 0f) return;
            _enemyBuffer.Clear();
            _enemies.QueryInRadius(transform.position, _radius, _enemyBuffer);
            foreach (var e in _enemyBuffer)
            {
                // 회오리 진행 방향으로 강하게 밀치고, 살짝 휘감기(옆으로) 섞음
                Vector3 shove = _moveDir + (transform.position - e.Position).normalized * 0.3f;
                (e as IStatusReceiver)?.ApplyKnockback(shove, _pullForce * Time.deltaTime * 6f);
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
            foreach (var e in _enemyBuffer)
            {
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
                        e.TakeDamage(_magnitude);
                        break;
                }
            }
        }
    }
}
