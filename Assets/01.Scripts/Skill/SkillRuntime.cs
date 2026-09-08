using UnityEngine;

namespace OZGL2.Skill
{
    /// <summary>스킬 1개의 런타임 상태. 지금은 쿨다운만, 나중에 트리 강화 레벨·차지 등 확장.</summary>
    public class SkillRuntime
    {
        public SkillData Data { get; }

        private float _cooldownEndTime;

        public SkillRuntime(SkillData data)
        {
            Data = data;
        }

        public bool IsReady(float now)
        {
            return now >= _cooldownEndTime;
        }

        public float RemainingCooldown(float now)
        {
            return Mathf.Max(0f, _cooldownEndTime - now);
        }

        public void PutOnCooldown(float now)
        {
            _cooldownEndTime = now + Data.cooldown;
        }
    }
}
