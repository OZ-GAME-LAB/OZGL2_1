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

        // ── 실전투에서 눈에 보이는 효과 (지속시간·임계값은 AugmentTuning)
        public float LowHpHealFraction;      // 체력 30% 이하가 되면 1회 최대 체력의 이 비율만큼 회복
        public float IronFormationReduction; // 전투 시작 후 8초간 몬스터가 받는 피해 감소 비율
        public float ChainStrikeStep;        // 몬스터가 공격할 때마다 공격속도 증가량(중첩, 최대 AugmentTuning.ChainStrikeCap)
        public float HuntAttackBonus;        // 용사 처치 시 5초간 몬스터 공격력 증가 비율

        public static AugmentModifiers Neutral => new AugmentModifiers
        {
            SkillPowerMult = 1f, SkillCooldownMult = 1f, SkillRadiusMult = 1f, SkillBuffDurationMult = 1f,
            MonsterAttackMult = 1f, MonsterAttackSpeedMult = 1f, MonsterHpMult = 1f,
            HeroAttackMult = 1f, HeroMoveSpeedMult = 1f, HeroIncomingSkillMult = 1f,
            XpGainMult = 1f,
        };
    }

    /// <summary>실전투 증강의 고정 수치 — 카드 value 와 별개로 코드에 박아두는 임계값·지속시간.</summary>
    public static class AugmentTuning
    {
        public const float LowHpThreshold = 0.30f;   // 위기의 치유 발동 체력 비율
        public const float IronFormationSeconds = 8f; // 철벽 진형 지속(전투 시작 기준)
        public const float ChainStrikeCap = 0.20f;   // 연타 본능 최대 중첩 보너스
        public const float HuntSeconds = 5f;         // 사냥 개시 지속
    }
}
