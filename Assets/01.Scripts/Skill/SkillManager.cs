using System;
using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Skill
{
    /// <summary>스킬이 실제로 발동됐을 때 연출 레이어에 전달되는 정보.</summary>
    public readonly struct SkillCastEvent
    {
        public readonly SkillData Data;
        public readonly Vector3 CastPoint;
        /// <summary>연출을 재생할 지점들(연쇄 번개 = 감전된 대상들, 광역 = 착탄점 1개).</summary>
        public readonly IReadOnlyList<Vector3> HitPoints;

        public SkillCastEvent(SkillData data, Vector3 castPoint, IReadOnlyList<Vector3> hitPoints)
        {
            Data = data;
            CastPoint = castPoint;
            HitPoints = hitPoints;
        }
    }

    /// <summary>
    /// 마왕의 스킬 보유·쿨다운·발동을 관리. 씬에 독립적이다 — ITargetProvider 하나만 있으면
    /// 샌드박스든 실제 인게임이든 동일하게 동작한다. MonoBehaviour 아님(순수 C#).
    ///
    /// 입력은 이 클래스가 받지 않는다. 호출자(샌드박스 IMGUI / 실제 스킬 탭 UI)가
    /// TryCastInstant / TryCastTargeted 를 호출한다.
    /// </summary>
    public class SkillManager
    {
        private readonly List<SkillRuntime> _skills = new List<SkillRuntime>();
        private readonly ITargetProvider _targets;
        private readonly List<IDamageable> _queryBuffer = new List<IDamageable>();
        private readonly List<Vector3> _hitPoints = new List<Vector3>();

        /// <summary>스킬이 발동될 때마다 발생. 연출(SkillVfxController) 이 구독.</summary>
        public event Action<SkillCastEvent> Casted;

        public SkillManager(ITargetProvider targets)
        {
            _targets = targets;
        }

        public IReadOnlyList<SkillRuntime> Skills => _skills;

        /// <summary>조준 프리뷰 등에서 대상 목록이 필요할 때. UI 가 별도로 ITargetProvider 를 들지 않게 한다.</summary>
        public IReadOnlyList<IDamageable> AllTargets => _targets.All;

        public void QueryTargetsInRadius(Vector3 center, float radius, List<IDamageable> results)
        {
            results.Clear();
            _targets.QueryInRadius(center, radius, results);
        }

        public SkillRuntime Unlock(SkillData data)
        {
            var runtime = new SkillRuntime(data);
            _skills.Add(runtime);
            return runtime;
        }

        /// <summary>즉시형 발동. 성공 시 true.</summary>
        public bool TryCastInstant(SkillRuntime skill)
        {
            float now = Time.time;
            if (skill == null || skill.Data.castMode != SkillCastMode.Instant || !skill.IsReady(now))
            {
                return false;
            }

            Execute(skill.Data, ResolveInstantPoint(skill.Data));
            skill.PutOnCooldown(now);
            return true;
        }

        /// <summary>조준형 발동. worldPoint 는 호출자가 지정(마우스 → 지면 레이캐스트 등).</summary>
        public bool TryCastTargeted(SkillRuntime skill, Vector3 worldPoint)
        {
            float now = Time.time;
            if (skill == null || skill.Data.castMode != SkillCastMode.Targeted || !skill.IsReady(now))
            {
                return false;
            }

            Execute(skill.Data, worldPoint);
            skill.PutOnCooldown(now);
            return true;
        }

        // 즉시형은 "맵 전체" 또는 "자동 타겟". 여기선 가장 가까운 적을 기준점으로 잡는다.
        // 맵 전체(운석·시간정지)는 radius 를 크게 잡으면 사실상 전체가 걸린다.
        private Vector3 ResolveInstantPoint(SkillData data)
        {
            var nearest = _targets.Nearest(Vector3.zero);
            return nearest != null ? nearest.Position : Vector3.zero;
        }

        private void Execute(SkillData data, Vector3 point)
        {
            _hitPoints.Clear();

            switch (data.effectType)
            {
                case SkillEffectType.AreaDamage:
                    _queryBuffer.Clear();
                    _targets.QueryInRadius(point, data.radius, _queryBuffer);
                    foreach (var target in _queryBuffer)
                    {
                        target.TakeDamage(data.skillPower);
                    }
                    _hitPoints.Add(point);
                    break;

                case SkillEffectType.ChainDamage:
                    ExecuteChain(point, data);
                    break;

                case SkillEffectType.AreaStun:
                    _queryBuffer.Clear();
                    _targets.QueryInRadius(point, data.radius, _queryBuffer);
                    foreach (var target in _queryBuffer)
                    {
                        (target as IStatusReceiver)?.ApplyStun(data.duration);
                    }
                    _hitPoints.Add(point);
                    break;

                case SkillEffectType.HealAllies:
                    // TODO: 아군(몬스터) provider 붙으면 전 몬스터 체력 data.duration 비율만큼 회복
                    break;
            }

            Casted?.Invoke(new SkillCastEvent(data, point, _hitPoints));
        }

        private void ExecuteChain(Vector3 from, SkillData data)
        {
            var alreadyHit = new HashSet<IDamageable>();
            Vector3 cursor = from;

            for (int i = 0; i < data.chainCount; i++)
            {
                IDamageable next = null;
                float bestSqr = float.MaxValue;

                foreach (var candidate in _targets.All)
                {
                    if (candidate.IsDead || alreadyHit.Contains(candidate))
                    {
                        continue;
                    }

                    float sqr = (candidate.Position - cursor).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        next = candidate;
                    }
                }

                if (next == null)
                {
                    break;
                }

                next.TakeDamage(data.skillPower);
                alreadyHit.Add(next);
                cursor = next.Position;
                _hitPoints.Add(cursor);
            }
        }
    }
}
