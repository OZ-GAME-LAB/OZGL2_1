using OZGL2.Augment;

namespace OZGL2.InGame
{
    /// <summary>실제 전투에서 소비되는 효과만 제공한다. SO의 설명용 connStatus는 실행 계약이 아니다.</summary>
    public static class InGameAugmentAvailability
    {
        public static bool CanOffer(AugmentData data)
        {
            if (data == null) return false;
            if (data.isInstant) return CanOfferInstant(data.effect);
            switch (data.effect)
            {
                case AugmentEffect.SkillDamage:
                case AugmentEffect.SkillCooldown:
                case AugmentEffect.SkillRadius:
                case AugmentEffect.SkillBuffDuration:
                case AugmentEffect.SkillCapstone:
                case AugmentEffect.MonHp:
                case AugmentEffect.MonAttack:
                case AugmentEffect.MonAttackSpeed:
                case AugmentEffect.HeroAttack:
                case AugmentEffect.XpGain:
                case AugmentEffect.CritChance:
                case AugmentEffect.EchoRecast:
                case AugmentEffect.OnHitSlow:
                case AugmentEffect.PredatorInstinct:
                // 아래는 RealSynergySync가 실제 실행 경로(처치 훅·전투 중 감시·CombatModifierHub → UnitBase)에 연결한 효과.
                case AugmentEffect.CooldownOnKill:
                case AugmentEffect.ExplodeOnDeath:
                case AugmentEffect.XpPerAliveMonster:
                case AugmentEffect.MonsterReviveChance:
                case AugmentEffect.MonsterShield:
                case AugmentEffect.HeroVulnerable:
                case AugmentEffect.FrostLance:
                case AugmentEffect.LowHpHeal:
                case AugmentEffect.IronFormation:
                case AugmentEffect.ChainStrikes:
                case AugmentEffect.HuntStart:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>즉시 발동형 — 고르는 순간 RealSynergySync.OnAugmentPicked가 1회 실행하는 효과만 제공한다.</summary>
        private static bool CanOfferInstant(AugmentEffect effect)
        {
            switch (effect)
            {
                case AugmentEffect.InstantSp:
                case AugmentEffect.InstantXp:
                case AugmentEffect.InstantResetCooldowns:
                case AugmentEffect.InstantHealMonsters:
                    return true;
                default:
                    return false;
            }
        }
    }
}
