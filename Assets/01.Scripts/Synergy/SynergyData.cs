using UnityEngine;

namespace OZGL2.Synergy
{
    /// <summary>직업 카테고리 6종 (기획 확정, 몬스터·용사 공용 — 시너지는 몬스터 쪽만 집계).</summary>
    public enum SynergyJob { Warrior, Shield, Archer, Mage, Rogue, Healer }

    /// <summary>시너지 효과 타입. SynergyTracker.Apply 가 이걸 보고 SynergyModifiers 필드에 반영.</summary>
    public enum SynergyEffect { WarriorAttack, ShieldHp, ArcherAttackSpeed, MageAttack, RogueAttackSpeed, HealerHealAmount }

    /// <summary>
    /// 직업 1종의 시너지 정의. 배치된 해당 직업 수가 3명(1단계)·5명(2단계) 넘으면 발동.
    /// 스킬·특성·증강과 달리 "찍는" 게 아니라 그리드 배치 상태를 그대로 집계한 결과 — 자동 판정.
    /// </summary>
    [CreateAssetMenu(menuName = "OZGL2/Synergy Data", fileName = "SO_Synergy_")]
    public class SynergyData : ScriptableObject
    {
        public SynergyJob job;
        public string displayName;
        public SynergyEffect effect;

        [Header("임계값 (배치 수)")]
        public int tier1Threshold = 3;
        public int tier2Threshold = 5;

        [Header("효과값 (1단계 도달 시 / 2단계 도달 시 — 2단계는 1단계 대체, 누적 아님)")]
        public float tier1Value;
        public float tier2Value;

        [TextArea] public string tier1Desc;
        [TextArea] public string tier2Desc;

        public string connStatus = "스텁";

        public int TierForCount(int count) => count >= tier2Threshold ? 2 : count >= tier1Threshold ? 1 : 0;
    }
}
