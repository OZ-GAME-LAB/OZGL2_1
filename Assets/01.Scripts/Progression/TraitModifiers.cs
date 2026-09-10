namespace OZGL2.Progression
{
    /// <summary>
    /// 특성 트리 전체 랭크를 합산한 결과. TraitTree.BuildModifiers() 가 만든다.
    /// 배율 필드는 1 이 기본(무보정), flat 필드는 0.
    /// 소비처: 스킬(SkillModifiers) · MawangLevel · SkillManager.EquipCapacity · SandboxUnit(HP·취약).
    /// 나머지(몬스터 공/공속/사거리, 용사 스탯, 그리드 등)는 값만 담기고 게임 스탯수정자 배선 대기.
    /// </summary>
    public struct TraitModifiers
    {
        // ── 스킬 (실제)
        public float SkillPowerMult;
        public float SkillCooldownMult;
        public float SkillRadiusMult;
        public float SkillUltCooldownMult;
        public float SkillBuffDurationMult;
        public int ExtraSkillSlots;
        public float SkillArmorPen;          // 스텁

        // ── 몬스터
        public float MonsterAttackMult;      // 스텁
        public float MonsterAttackSpeedMult; // 스텁
        public float MonsterRangeMult;       // 스텁
        public float MonsterHpMult;          // 샌드박스 실제
        public float MonsterDefenseAdd;      // 스텁
        public float MonsterExecuteBonus;    // 스텁

        // ── 용사 (약화 — 배율은 <1, add 는 음수)
        public float HeroAttackMult;         // 스텁
        public float HeroAttackSpeedMult;    // 스텁
        public float HeroRangeMult;          // 스텁
        public float HeroDefenseAdd;         // 스텁
        public float HeroMoveSpeedMult;      // 스텁
        public float HeroHpMult;             // 샌드박스 실제
        public float HeroIncomingSkillMult;  // 샌드박스 실제 (취약 각인)

        // ── 재화·성장
        public float XpGainMult;
        public float XpNeedMult;
        public int KillXpBonus;
        public int SurgeXpPer5Level;
        public int MilestoneBonusLp;
        public int MilestoneSpBonus;
        public int GridBonus;                // 스텁
        public int DeployCostBonus;          // 스텁

        public static TraitModifiers Neutral => new TraitModifiers
        {
            SkillPowerMult = 1f, SkillCooldownMult = 1f, SkillRadiusMult = 1f,
            SkillUltCooldownMult = 1f, SkillBuffDurationMult = 1f,
            MonsterAttackMult = 1f, MonsterAttackSpeedMult = 1f, MonsterRangeMult = 1f, MonsterHpMult = 1f,
            HeroAttackMult = 1f, HeroAttackSpeedMult = 1f, HeroRangeMult = 1f,
            HeroMoveSpeedMult = 1f, HeroHpMult = 1f, HeroIncomingSkillMult = 1f,
            XpGainMult = 1f, XpNeedMult = 1f,
        };
    }
}
