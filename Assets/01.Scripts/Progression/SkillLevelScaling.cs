using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 마왕 레벨에 따른 스킬 피해 배율. 레벨이 1 오를 때마다 스킬 피해가 PerLevel(기본 4%)만큼 늘어난다(레벨 1은 ×1).
    /// 전투에서는 RealSynergySync 가 스킬 피해 배율(특성·증강과 곱)에 이 값을 곱하고, 로비 스킬 세팅 화면은 같은 값을 표시에 쓴다.
    /// 올리는 폭을 바꾸려면 PerLevel 한 줄만 고친다.
    /// </summary>
    public static class SkillLevelScaling
    {
        /// <summary>레벨 1당 늘어나는 비율(0.04 = 4%).</summary>
        public const float PerLevel = 0.04f;

        public static float PowerMult(int level) => 1f + PerLevel * Mathf.Max(0, level - 1);

        /// <summary>지금 마왕 레벨 기준 배율(마왕 정보가 없으면 ×1).</summary>
        public static float CurrentPowerMult()
        {
            var mawang = MawangXpBridge.Mawang;
            return PowerMult(mawang != null ? mawang.Level : 1);
        }
    }
}
