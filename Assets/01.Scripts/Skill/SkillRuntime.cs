using UnityEngine;

namespace OZGL2.Skill
{
    /// <summary>스킬 1개의 런타임 상태 — 쿨다운 + 강화 레벨(스킬 트리).</summary>
    public class SkillRuntime
    {
        /// <summary>강화 레벨당 효과. 밸런스 시트 11.마왕·스킬 트리 기준.</summary>
        public const float PowerPerLevel = 0.15f;   // 피해 +15% / 레벨
        public const float CooldownPerLevel = 0.08f; // 쿨 -8% / 레벨
        public const int MaxUpgradeLevel = 3;

        public SkillData Data { get; }
        public int UpgradeLevel { get; private set; }
        public bool IsUnlocked { get; private set; } = true; // 샌드박스 기본 해금. 실제 게임은 트리에서 해금

        private float _cooldownEndTime;

        public SkillRuntime(SkillData data)
        {
            Data = data;
        }

        public float EffectivePower => Data.skillPower * (1f + PowerPerLevel * UpgradeLevel);
        public float EffectiveCooldown => Data.cooldown * (1f - CooldownPerLevel * UpgradeLevel);

        public bool IsReady(float now) => IsUnlocked && now >= _cooldownEndTime;
        public float RemainingCooldown(float now) => Mathf.Max(0f, _cooldownEndTime - now);
        public void PutOnCooldown(float now) => _cooldownEndTime = now + EffectiveCooldown;

        /// <summary>디버그 — 즉시 재사용 가능 상태로.</summary>
        public void ResetCooldown() => _cooldownEndTime = 0f;

        public void SetUnlocked(bool value) => IsUnlocked = value;

        /// <summary>강화 레벨 +1. 성공 시 true.</summary>
        public bool TryUpgrade()
        {
            if (UpgradeLevel >= MaxUpgradeLevel)
            {
                return false;
            }

            UpgradeLevel++;
            return true;
        }
    }
}
