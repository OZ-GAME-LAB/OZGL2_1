using UnityEngine;

namespace OZGL2.Skill
{
    /// <summary>
    /// 스킬 1개의 런타임 상태 — 봉인 해제(SP) · 장착 · 쿨다운.
    /// 강화 레벨은 없다(해금 = 완성). 수치 보정은 특성 트리가 SkillModifiers 로 건다.
    /// </summary>
    public class SkillRuntime
    {
        public SkillData Data { get; }
        public bool IsUnlocked { get; private set; }
        public bool IsEquipped { get; private set; }

        private readonly SkillModifiers _mods;
        private float _cooldownEndTime;

        public SkillRuntime(SkillData data, SkillModifiers mods = null, bool unlocked = false)
        {
            Data = data;
            _mods = mods ?? SkillModifiers.None;
            IsUnlocked = unlocked;
        }

        /// <summary>봉인 해제에 드는 SP. 티어1=1 / 티어2=2 / 티어3=3 / 궁극=5.</summary>
        public int UnlockCost => Data.category == SkillCategory.Ultimate ? 5 : Mathf.Clamp(Data.tier, 1, 4);

        public float EffectivePower => Data.skillPower * _mods.PowerMult;
        public float EffectiveCooldown => Data.cooldown * _mods.CooldownMult;
        public float EffectiveRadius => Data.radius * _mods.RadiusMult;
        public float EffectiveBuffDuration => Data.duration * _mods.BuffDurationMult;
        public int EffectiveReviveCount => Mathf.Max(0, Data.reviveCount + _mods.ReviveBonus);

        public bool IsReady(float now) => IsUnlocked && IsEquipped && now >= _cooldownEndTime;
        public float RemainingCooldown(float now) => Mathf.Max(0f, _cooldownEndTime - now);
        public void PutOnCooldown(float now) => _cooldownEndTime = now + EffectiveCooldown;

        /// <summary>디버그 — 즉시 재사용 가능 상태로.</summary>
        public void ResetCooldown() => _cooldownEndTime = 0f;

        public void SetUnlocked(bool value)
        {
            IsUnlocked = value;
            if (!value) IsEquipped = false;
        }

        public void SetEquipped(bool value) => IsEquipped = value;
    }
}
