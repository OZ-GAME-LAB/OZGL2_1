using OZGL2.Augment;

namespace OZGL2.InGame
{
    /// <summary>실제 전투에서 소비되는 효과만 제공한다. SO의 설명용 connStatus는 실행 계약이 아니다.</summary>
    public static class InGameAugmentAvailability
    {
        public static bool CanOffer(AugmentData data)
        {
            if (data == null || data.isInstant) return false;
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
                    return true;
                default:
                    return false;
            }
        }
    }
}
