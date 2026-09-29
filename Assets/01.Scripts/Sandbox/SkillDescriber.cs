using System.Text;
using OZGL2.Skill;

namespace OZGL2.Sandbox
{
    /// <summary>
    /// SkillData 수치에서 스킬 설명문을 자동으로 만든다. SkillData에는 설명 텍스트 필드가 없고, 수치(반경·피해·지속 등)를
    /// 손으로 쓴 설명에 따로 적어두면 밸런스를 고칠 때마다 어긋나기 때문에 — 수치를 그대로 읽어서 문장으로 풀어준다.
    /// 표시하는 피해량은 기본값이며 실제로는 특성·증강 배율이 곱해진다.
    /// </summary>
    public static class SkillDescriber
    {
        public static string CategoryName(SkillCategory c) => c switch
        {
            SkillCategory.Damage => "딜",
            SkillCategory.Debuff => "디버프",
            SkillCategory.Buff => "버프",
            SkillCategory.Ultimate => "궁극기",
            _ => c.ToString(),
        };

        /// <summary>"딜 · 3티어 · 조준 발동 · 재사용 12초"</summary>
        public static string Header(SkillData d)
            => $"{CategoryName(d.category)} · {d.tier}티어 · {(d.castMode == SkillCastMode.Targeted ? "조준 발동" : "즉시 발동")} · 재사용 {d.cooldown:0.#}초";

        public static string Describe(SkillData d)
        {
            var sb = new StringBuilder();
            bool mapWide = d.radius >= 20f;
            string range = mapWide ? "맵 전체" : $"반경 {d.radius:0.#}칸";

            switch (d.effectType)
            {
                case SkillEffectType.AreaDamage:
                    if (d.barrageCount > 1)
                        sb.Append($"{range}에 {d.barrageCount}발을 연달아 떨어뜨려 한 발당 {d.skillPower:0} 피해를 줍니다.")
                          .Append(mapWide ? " 살아있는 용사를 먼저 겨냥합니다." : string.Empty);
                    else if (d.fallFromSky)
                        sb.Append($"지정한 곳 위에서 운석이 떨어져 {range}에 {d.skillPower:0} 피해를 줍니다.")
                          .Append(d.castDelay > 0f ? $" (낙하 예고 {d.castDelay:0.#}초 후 착탄)" : string.Empty);
                    else if (d.castMode == SkillCastMode.Targeted)
                        sb.Append($"지정한 곳으로 날아가 폭발합니다 — {range}에 {d.skillPower:0} 피해.");
                    else
                        sb.Append($"{range}에 {d.skillPower:0} 피해를 줍니다.")
                          .Append(d.castDelay > 0f ? $" (시전 {d.castDelay:0.#}초 후 발동)" : string.Empty);
                    break;

                case SkillEffectType.ChainDamage:
                    sb.Append($"지정한 곳 {range} 안의 적 사이를 최대 {d.chainCount}번 연쇄해 타격마다 {d.skillPower:0} 피해를 줍니다.");
                    break;

                case SkillEffectType.LineDamage:
                    sb.Append($"마왕에서 조준한 방향으로 {d.lineLength:0.#}칸을 날아가며 경로의 모든 적을 관통합니다 — 피해 {d.skillPower:0}, 폭 {d.radius:0.#}칸.");
                    break;

                case SkillEffectType.SingleDamage:
                    sb.Append($"지정한 곳에서 가장 가까운 적 1명에게 {d.skillPower:0}의 큰 피해를 줍니다.");
                    break;

                case SkillEffectType.Knockback:
                    sb.Append($"{range} 안의 적을 밀쳐냅니다(힘 {d.force:0.#})")
                      .Append(d.skillPower > 0f ? $" — 피해 {d.skillPower:0}." : ".");
                    break;

                case SkillEffectType.Stun:
                    sb.Append($"{range} 안의 적을 {d.duration:0.#}초 동안 정지시킵니다.");
                    break;

                case SkillEffectType.Vacuum:
                    sb.Append($"{range} 안의 적을 중심으로 끌어당긴 뒤 {d.skillPower:0} 피해를 줍니다.");
                    break;

                case SkillEffectType.MovingZone:
                    sb.Append($"마왕에서 조준한 방향으로 움직이는 장판을 날립니다 — 반경 {d.radius:0.#}칸, 이동속도 {d.zoneMoveSpeed:0.#}, 화면 밖으로 나가면 사라집니다. ")
                      .Append(ZoneText(d));
                    break;

                case SkillEffectType.PersistentZone:
                    sb.Append($"지정한 곳에 {d.duration:0.#}초 동안 유지되는 장판을 깝니다 — 반경 {d.radius:0.#}칸. ")
                      .Append(ZoneText(d));
                    break;

                case SkillEffectType.HealAllies:
                    sb.Append($"모든 마왕군의 체력을 {d.duration * 100f:0}% 회복합니다.");
                    break;

                case SkillEffectType.AllyBuff:
                    sb.Append($"모든 마왕군의 {BuffStatName(d.buffStat)}을(를) {d.duration:0.#}초 동안 +{(d.buffMultiplier - 1f) * 100f:0}% 올립니다.");
                    break;

                case SkillEffectType.Revive:
                    sb.Append($"쓰러진 마왕군 {d.reviveCount}기를 되살립니다.");
                    break;
            }

            // 피격 부가효과
            if (d.onHitSlowMultiplier > 0f && d.effectType != SkillEffectType.PersistentZone && d.effectType != SkillEffectType.MovingZone)
                sb.Append($" 맞은 적은 {d.onHitSlowSeconds:0.#}초간 이동속도가 {(1f - d.onHitSlowMultiplier) * 100f:0}% 느려집니다.");
            if (d.onHitDotDamage > 0f)
            {
                bool zone = d.effectType == SkillEffectType.PersistentZone || d.effectType == SkillEffectType.MovingZone;
                if (zone) sb.Append($" 장판 안에 있는 동안 {d.onHitDotTick:0.##}초마다 {d.onHitDotDamage:0} 피해를 추가로 줍니다.");
                else sb.Append($" 맞은 적은 {d.onHitDotSeconds:0.#}초 동안 {d.onHitDotTick:0.##}초마다 {d.onHitDotDamage:0} 지속 피해를 입습니다(총 {d.onHitDotDamage * d.onHitDotSeconds / System.Math.Max(d.onHitDotTick, 0.1f):0}).");
            }
            return sb.ToString();
        }

        private static string ZoneText(SkillData d)
        {
            switch (d.zoneEffect)
            {
                case ZoneEffect.Stun: return "장판 위의 적이 정지합니다.";
                case ZoneEffect.Slow: return $"장판 위의 적은 이동속도가 {(1f - d.zoneMagnitude) * 100f:0}% 느려집니다.";
                case ZoneEffect.Vulnerable: return $"장판 위의 적이 받는 피해가 {(d.zoneMagnitude - 1f) * 100f:0}% 늘어납니다.";
                case ZoneEffect.DamageOverTime: return $"장판 위의 적이 {d.zoneTick:0.##}초마다 {d.zoneMagnitude:0} 피해를 입습니다.";
                case ZoneEffect.Heal: return $"장판 위의 마왕군이 {d.zoneTick:0.##}초마다 {d.zoneMagnitude:0} 회복합니다.";
                default: return string.Empty;
            }
        }

        private static string BuffStatName(BuffStat s) => s switch
        {
            BuffStat.AttackSpeed => "공격속도",
            BuffStat.Defense => "방어력",
            BuffStat.Attack => "공격력",
            _ => s.ToString(),
        };
    }
}
