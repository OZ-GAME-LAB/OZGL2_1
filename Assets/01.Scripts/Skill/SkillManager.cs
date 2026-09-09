using System;
using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Skill
{
    /// <summary>스킬 발동 요청. SkillExecutor 가 이걸 받아 시간 있는 효과를 실행한다.</summary>
    public readonly struct SkillCastRequest
    {
        public readonly SkillRuntime Skill;
        public readonly Vector3 CastPoint;

        public SkillCastRequest(SkillRuntime skill, Vector3 castPoint)
        {
            Skill = skill;
            CastPoint = castPoint;
        }
    }

    /// <summary>
    /// 마왕의 스킬 보유·쿨다운·발동 판정. 순수 C# (MonoBehaviour 아님).
    /// 데미지·연출은 여기서 안 한다 — CastRequested 이벤트만 쏘고, SkillExecutor(MonoBehaviour)가
    /// 투사체 이동·시전 지연·연쇄 딜레이 같은 타이밍을 처리한다.
    ///
    /// 입력은 이 클래스가 받지 않는다. 호출자(스킬 바 UI)가 TryCastInstant / TryCastTargeted 를 호출.
    /// </summary>
    public class SkillManager
    {
        private readonly List<SkillRuntime> _skills = new List<SkillRuntime>();
        private readonly ITargetProvider _targets;

        /// <summary>스킬 발동이 확정될 때마다 발생. SkillExecutor 가 구독해 효과·연출을 실행.</summary>
        public event Action<SkillCastRequest> CastRequested;

        public SkillManager(ITargetProvider targets)
        {
            _targets = targets;
        }

        public IReadOnlyList<SkillRuntime> Skills => _skills;

        /// <summary>조준 프리뷰용. UI 가 별도로 ITargetProvider 를 들지 않게 한다.</summary>
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

        public bool TryCastInstant(SkillRuntime skill)
        {
            float now = Time.time;
            if (skill == null || skill.Data.castMode != SkillCastMode.Instant || !skill.IsReady(now))
            {
                return false;
            }

            Raise(skill, ResolveInstantPoint());
            skill.PutOnCooldown(now);
            return true;
        }

        public bool TryCastTargeted(SkillRuntime skill, Vector3 worldPoint)
        {
            float now = Time.time;
            if (skill == null || skill.Data.castMode != SkillCastMode.Targeted || !skill.IsReady(now))
            {
                return false;
            }

            Raise(skill, worldPoint);
            skill.PutOnCooldown(now);
            return true;
        }

        private void Raise(SkillRuntime skill, Vector3 point)
        {
            CastRequested?.Invoke(new SkillCastRequest(skill, point));
        }

        // 즉시형 기준점: 가장 가까운 적. 맵 전체(운석·시간정지)는 radius 를 크게 잡으면 전부 걸린다.
        private Vector3 ResolveInstantPoint()
        {
            var nearest = _targets.Nearest(Vector3.zero);
            return nearest != null ? nearest.Position : Vector3.zero;
        }
    }
}
