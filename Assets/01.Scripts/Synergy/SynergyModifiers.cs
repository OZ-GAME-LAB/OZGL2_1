namespace OZGL2.Synergy
{
    /// <summary>
    /// 활성화된 시너지 전부를 합산한 결과. SynergyTracker.BuildModifiers() 가 만든다.
    /// 스킬(마왕 개인 파워)과는 별개 — 시너지는 몬스터 군대 쪽 스탯만 건드린다.
    /// </summary>
    public struct SynergyModifiers
    {
        public float WarriorAttackMult;      // 스텁 — 몬스터 전투 시뮬 없음
        public float ShieldHpMult;           // 샌드박스 실제 — 몬스터 체력에 반영
        public float ArcherAttackSpeedMult;  // 스텁
        public float MageAttackMult;         // 스텁
        public float RogueAttackSpeedMult;   // 스텁
        public float HealerHealAmountMult;   // 스텁 — 힐러 유닛 자체가 없음

        // 조합 시너지 (서로 다른 두 직업을 함께 배치했을 때) — 단일 직업 시너지와 별개로 가산
        public float ComboCritChanceBonus;   // 궁수+마법사 — 스킬 치명타 확률 가산 (샌드박스 실제 연동)
        public float ComboSkillCooldownMult; // 힐러+마법사 — 마왕 스킬 쿨타임 배율 (샌드박스 실제 연동)
        public float ComboExecuteBonusMult;  // 전사+도적 — 처치 시 추가 데미지 배율 (스텁)

        public static SynergyModifiers Neutral => new SynergyModifiers
        {
            WarriorAttackMult = 1f,
            ShieldHpMult = 1f,
            ArcherAttackSpeedMult = 1f,
            MageAttackMult = 1f,
            RogueAttackSpeedMult = 1f,
            HealerHealAmountMult = 1f,
            ComboCritChanceBonus = 0f,
            ComboSkillCooldownMult = 1f,
            ComboExecuteBonusMult = 0f,
        };
    }
}
