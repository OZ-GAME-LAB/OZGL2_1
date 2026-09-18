using System.Globalization;
using OZGL2.Progression;
using UnityEngine;

namespace OZGL2.UIFlow
{
    // 효과 계산은 기존 모델을 재사용하고, 한국어 표시만 담당한다. 계정/저장 데이터를 소유하지 않는다.
    public static class UITraitEffectTextFormatter
    {
        public static string Format(TraitData definition, int rank)
        {
            if (definition == null) return string.Empty;
            if (rank <= 0) return "적용된 효과 없음";
            var m = TraitTree.PreviewModifiers(definition, rank);
            switch (definition.effect)
            {
                case TraitEffect.MonAttack: return "몬스터 공격력 " + Percent(m.MonsterAttackMult - 1);
                case TraitEffect.MonAttackSpeed: return "몬스터 공격속도 " + Percent(m.MonsterAttackSpeedMult - 1);
                case TraitEffect.MonRange: return "몬스터 사거리 " + Percent(m.MonsterRangeMult - 1);
                case TraitEffect.MonHp: return "몬스터 체력 " + Percent(m.MonsterHpMult - 1);
                case TraitEffect.MonDefense: return "몬스터 방어력 " + Percent(m.MonsterDefenseAdd);
                case TraitEffect.MonExecute: return "체력 30% 이하 용사에게\n주는 피해 " + Percent(m.MonsterExecuteBonus);
                case TraitEffect.HeroAttack: return "용사 공격력 " + Percent(m.HeroAttackMult - 1);
                case TraitEffect.HeroAttackSpeed: return "용사 공격속도 " + Percent(m.HeroAttackSpeedMult - 1);
                case TraitEffect.HeroRange: return "용사 사거리 " + Percent(m.HeroRangeMult - 1);
                case TraitEffect.HeroDefense: return "용사 방어력 " + Percent(m.HeroDefenseAdd);
                case TraitEffect.HeroMoveSpeed: return "용사 이동속도 " + Percent(m.HeroMoveSpeedMult - 1);
                case TraitEffect.HeroHp: return "용사 체력 " + Percent(m.HeroHpMult - 1);
                case TraitEffect.HeroVulnerable: return "용사가 받는 스킬 피해 " + Percent(m.HeroIncomingSkillMult - 1);
                case TraitEffect.SkillDamage: return "스킬 피해 " + Percent(m.SkillPowerMult - 1);
                case TraitEffect.SkillCooldown: return "스킬 쿨타임 " + Percent(m.SkillCooldownMult - 1);
                case TraitEffect.SkillRadius: return "스킬 반경 " + Percent(m.SkillRadiusMult - 1);
                case TraitEffect.SkillUltCooldown: return "궁극기 쿨타임 " + Percent(m.SkillUltCooldownMult - 1);
                case TraitEffect.SkillArmorPen: return "용사 방어력 " + Number(m.SkillArmorPen * 100) + "% 무시";
                case TraitEffect.SkillBuffDuration: return "버프·오라 지속시간 " + Percent(m.SkillBuffDurationMult - 1);
                case TraitEffect.SkillSlot: return "스킬 장착 슬롯 +" + m.ExtraSkillSlots;
                case TraitEffect.XpGain: return "XP 획득 " + Percent(m.XpGainMult - 1);
                case TraitEffect.XpNeed: return "레벨업 필요 XP " + Percent(m.XpNeedMult - 1);
                case TraitEffect.KillXp: return "용사 처치 시 추가 XP +" + m.KillXpBonus;
                case TraitEffect.LevelSurge: return "레벨 5의 배수마다\n즉시 XP +" + m.SurgeXpPer5Level;
                case TraitEffect.MilestoneLp: return "레벨 5의 배수\n보너스 LP +" + m.MilestoneBonusLp;
                case TraitEffect.Grid: return "전투 그리드 +" + m.GridBonus + "칸";
                case TraitEffect.DeployCost: return "시작 배치 코스트 +" + m.DeployCostBonus;
                case TraitEffect.CapMonster:
                    return "몬스터 공격력·체력\n공격속도·사거리 " + Percent(m.MonsterAttackMult - 1);
                case TraitEffect.CapHero:
                    return "용사 공격력·체력·사거리\n공격속도·이동속도 " + Percent(m.HeroAttackMult - 1);
                case TraitEffect.CapSkill:
                    return "스킬 피해 " + Percent(m.SkillPowerMult - 1) + "\n쿨타임 " + Percent(m.SkillCooldownMult - 1);
                case TraitEffect.CapEconomy:
                    return "XP 획득 " + Percent(m.XpGainMult - 1) + "\n레벨 보너스 LP +" + m.MilestoneBonusLp +
                           "\n라운드 보너스 SP +" + m.MilestoneSpBonus;
                default: return "효과 정보 없음";
            }
        }

        private static string Percent(float value)
        {
            float percent = Mathf.Round(value * 10000f) / 100f;
            return (percent > 0 ? "+" : string.Empty) + Number(percent) + "%";
        }

        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
