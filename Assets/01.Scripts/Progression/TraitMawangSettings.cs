using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 특성 중 마왕 레벨업 규칙에 걸리는 효과(레벨업 필요 XP 감소 · 5의 배수 레벨 보너스 LP · 5레벨마다 XP)를 MawangLevel에 반영한다.
    /// 전투 시스템(RealSynergySync)이 없는 로비에서도 같은 값이 보이도록 로비 쪽에서도 호출한다.
    /// </summary>
    public static class TraitMawangSettings
    {
        public static void Apply(TraitModifiers modifiers, MawangLevel mawang)
        {
            if (mawang == null) return;
            mawang.XpNeedMult = modifiers.XpNeedMult;
            mawang.MilestoneBonusLp = modifiers.MilestoneBonusLp;
            mawang.SurgeXpOnMilestone = modifiers.SurgeXpPer5Level;
        }

        /// <summary>저장된 특성 랭크 기준으로 반영한다(전투 시스템이 아직 없는 로비 진입 직후용).</summary>
        public static void ApplySaved(MawangLevel mawang)
        {
            if (mawang == null) return;
            Apply(new TraitTree(Resources.LoadAll<TraitData>("Traits")).BuildModifiers(), mawang);
        }
    }
}
