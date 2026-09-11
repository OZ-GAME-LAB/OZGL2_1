namespace OZGL2.Augment
{
    /// <summary>
    /// 이번 런에 뽑은 증강 전체를 합산한 결과. TraitModifiers 와 같은 모양이지만
    /// 별도 타입 — 특성(영구)과 증강(런 한정)은 저장 수명이 다르므로 섞지 않는다.
    /// 최종 값은 SkillSandbox.RefreshMods 에서 TraitModifiers 와 곱해서 합친다.
    /// </summary>
    public struct AugmentModifiers
    {
        public float SkillPowerMult;
        public float SkillCooldownMult;
        public float SkillRadiusMult;
        public float SkillBuffDurationMult;

        public float MonsterAttackMult;      // 스텁
        public float MonsterAttackSpeedMult; // 스텁
        public float MonsterHpMult;          // 샌드박스 실제
        public float MonsterDefenseAdd;      // 스텁

        public float HeroAttackMult;         // 스텁
        public float HeroDefenseAdd;         // 스텁
        public float HeroMoveSpeedMult;      // 스텁
        public float HeroIncomingSkillMult;  // 샌드박스 실제

        public float XpGainMult;
        public int GridBonus;                // 스텁

        // ── 특이 매커니즘 (전부 실제 작동)
        public float CritChance;             // 스킬 시전 시 이 확률로 피해 ×1.5
        public float EchoChance;             // 스킬 시전 시 이 확률로 쿨탐 없이 즉시 재시전
        public float CooldownOnKillSeconds;  // 용사 처치 시 전체 스킬 쿨탐 감소(초)
        public float ExplodeOnDeathPower;    // 용사 사망 시 주변 폭발 피해
        public float OnHitSlowAmount;        // AreaDamage 피격 시 이동속도 배율(0.6=−40%), 0=없음
        public float MonsterReviveChance;    // 몬스터 사망 시 즉시 부활 확률
        public bool MonsterShieldActive;     // 몬스터 스폰 시 첫 피격 무효 보호막
        public float XpPerAliveMonster;      // 처치 XP + 생존 몬스터 수 × 이 값

        public static AugmentModifiers Neutral => new AugmentModifiers
        {
            SkillPowerMult = 1f, SkillCooldownMult = 1f, SkillRadiusMult = 1f, SkillBuffDurationMult = 1f,
            MonsterAttackMult = 1f, MonsterAttackSpeedMult = 1f, MonsterHpMult = 1f,
            HeroAttackMult = 1f, HeroMoveSpeedMult = 1f, HeroIncomingSkillMult = 1f,
            XpGainMult = 1f,
        };
    }
}
