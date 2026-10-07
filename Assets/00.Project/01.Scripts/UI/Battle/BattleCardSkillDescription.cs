using System.Collections.Generic;
using System.Globalization;

namespace OZGL2.UIFlow
{
    /// <summary>전투 데이터를 변경하지 않고 카드에 표시할 3성 기준의 보유 스킬 설명을 만든다.</summary>
    public static class BattleCardSkillDescription
    {
        public static string Build(UnitStatData stats)
        {
            if (stats == null) return "스킬 정보 없음";

            // 3성에서만 새로 해금된다는 뜻이 아니라, 완성 성급의 실제 동작을 안내한다.
            const string PREFIX = "3성 기준\n";
            if (stats.healAmount > 0f)
                return PREFIX + "사거리 안에서 체력 비율이 낮은\n아군 최대 3명을 동시에 회복";

            var effects = new List<string>();
            if (stats.targetLowestHealthEnemy)
                effects.Add("사거리 내 최저 체력 적 우선 공격");

            if (stats.executeDamageBonusPerMissingHealth > 0f)
                effects.Add("적이 잃은 체력에 비례해\n최대 " + Number(stats.executeDamageBonusPerMissingHealth * 100f) + "% 추가 피해");

            if (stats.comboStunAttackInterval > 0)
                effects.Add("매 " + stats.comboStunAttackInterval.ToString(CultureInfo.InvariantCulture) +
                    "번째 공격마다 " + Number(stats.comboStunDuration) + "초 기절");

            if (stats.splashRadius > 0f)
            {
                string targets = stats.splashMaxTargets > 0
                    ? "최대 " + stats.splashMaxTargets.ToString(CultureInfo.InvariantCulture) + "명"
                    : "모두";
                effects.Add("대상 주변 반경 " + Number(stats.splashRadius) + "의 적\n추가 " + targets +
                    "에게 " + Number(stats.splashSecondaryDamagePercent * 100f) + "% 피해");
            }

            // UnitBase.IsTaunting과 같은 조건이다. 직업명만 보고 도발을 추정하지 않는다.
            if (stats.side == UnitSide.DemonArmy && stats.attackRange <= 1f)
                effects.Add("사거리 안의 용사를 자신에게 유인");

            if (effects.Count == 0) effects.Add("단일 대상에게 기본 공격");
            return PREFIX + string.Join("\n", effects);
        }

        private static string Number(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
