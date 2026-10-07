using System.Collections.Generic;
using System.Globalization;

namespace OZGL2.UIFlow
{
    /// <summary>현재 성급에서 얻은 특수 효과만 카드의 보유 스킬 설명으로 표시한다.</summary>
    public static class BattleCardSkillDescription
    {
        public static string Build(UnitStatData stats)
        {
            return Build(stats, stats != null ? stats.starLevel : 1);
        }

        public static string Build(UnitStatData stats, int starLevel)
        {
            if (stats == null || starLevel < 2) return "스킬 없음";

            // 성급별 Prefab이 같은 기초 SO를 공유하므로 카드의 현재 성급을 따로 받는다.
            int currentStar = UnityEngine.Mathf.Clamp(starLevel, 1, 3);
            if (stats.healAmount > 0f)
                return "사거리 안에서 체력 비율이 낮은\n아군 최대 " +
                    currentStar.ToString(CultureInfo.InvariantCulture) + "명을 동시에 회복";

            var effects = new List<string>();
            if (stats.executeDamageBonusPerMissingHealth > 0f)
                effects.Add("적이 잃은 체력에 비례해\n최대 " + Number(stats.executeDamageBonusPerMissingHealth * 100f) + "% 추가 피해");

            if (stats.comboStunAttackInterval > 0)
                effects.Add("매 " + stats.comboStunAttackInterval.ToString(CultureInfo.InvariantCulture) +
                    "번째 공격마다 " + Number(stats.comboStunDuration) + "초 기절");

            // 저체력 대상 선택·범위 공격·도발은 기본 전투 방식이므로 보유 스킬에 넣지 않는다.
            return effects.Count == 0 ? "스킬 없음" : string.Join("\n", effects);
        }

        private static string Number(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
