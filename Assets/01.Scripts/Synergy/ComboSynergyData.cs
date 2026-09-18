using UnityEngine;

namespace OZGL2.Synergy
{
    /// <summary>조합 시너지 효과 타입. 단일 직업 시너지와 별개로 SynergyModifiers에 가산/적용됨.</summary>
    public enum ComboSynergyEffect { FrontlineAssault, RangedBarrage, SwiftHunters, GuardiansBlessing, SagesBond, ExecutionerCombo }

    /// <summary>
    /// 서로 다른 두 직업을 동시에 일정 수 이상 배치했을 때 발동하는 조합 시너지.
    /// 단일 직업 시너지(SynergyData)와 독립적으로 판정되고 누적 적용됨(둘 다 켜지면 둘 다 발동).
    /// </summary>
    [CreateAssetMenu(menuName = "OZGL2/Combo Synergy Data", fileName = "SO_ComboSynergy_")]
    public class ComboSynergyData : ScriptableObject
    {
        public SynergyJob jobA;
        public SynergyJob jobB;

        [Header("각 직업마다 필요한 최소 배치 수")]
        public int requiredCountEach = 2;

        public string displayName;
        public ComboSynergyEffect effect;
        public float value;

        [TextArea] public string desc;
        public string connStatus = "스텁";
    }
}
