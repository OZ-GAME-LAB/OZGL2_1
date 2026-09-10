using UnityEngine;

namespace OZGL2.Progression
{
    public enum TraitBranch { Monster, Hero, Skill, Economy }

    public enum TraitLine { A, B, C, Capstone }

    /// <summary>특성 노드 40개. 이름은 갈래_라인_순번. 캡스톤은 갈래_Cap.</summary>
    public enum TraitId
    {
        Mon_A1, Mon_A2, Mon_A3, Mon_B1, Mon_B2, Mon_B3, Mon_C1, Mon_C2, Mon_C3, Mon_Cap,
        Hero_A1, Hero_A2, Hero_A3, Hero_B1, Hero_B2, Hero_B3, Hero_C1, Hero_C2, Hero_C3, Hero_Cap,
        Sk_A1, Sk_A2, Sk_A3, Sk_B1, Sk_B2, Sk_B3, Sk_C1, Sk_C2, Sk_C3, Sk_Cap,
        Eco_A1, Eco_A2, Eco_A3, Eco_B1, Eco_B2, Eco_B3, Eco_C1, Eco_C2, Eco_C3, Eco_Cap,
    }

    /// <summary>
    /// 특성 효과 타입. TraitTree.ApplyEffect 가 이걸 보고 TraitModifiers 필드를 누적한다.
    /// 캡스톤(Cap*)은 한 번에 여러 필드를 건드린다.
    /// </summary>
    public enum TraitEffect
    {
        // 몬스터
        MonAttack, MonAttackSpeed, MonRange, MonHp, MonDefense, MonExecute,
        // 용사(약화 — 값은 양수로 넣고 ApplyEffect 에서 감소 적용)
        HeroAttack, HeroAttackSpeed, HeroRange, HeroDefense, HeroMoveSpeed, HeroHp, HeroVulnerable,
        // 스킬
        SkillDamage, SkillCooldown, SkillRadius, SkillUltCooldown, SkillArmorPen, SkillBuffDuration, SkillSlot,
        // 재화·성장
        XpGain, XpNeed, KillXp, LevelSurge, MilestoneLp, Grid, DeployCost,
        // 캡스톤
        CapMonster, CapHero, CapSkill, CapEconomy,
    }

    [CreateAssetMenu(menuName = "OZGL2/Trait Data", fileName = "SO_Trait_")]
    public class TraitData : ScriptableObject
    {
        [Header("식별")]
        public TraitId id;
        public string displayName;
        [TextArea] public string description;
        public TraitBranch branch;
        public TraitLine line;

        [Header("효과")]
        public TraitEffect effect;
        [Tooltip("랭크당 값. 배율은 0.05 = +5%, flat 은 그대로.")]
        public float valuePerRank = 0.02f;
        [Range(1, 5)] public int maxRank = 3;
        [Tooltip("랭크 0→1, 1→2, … 비용. 길이 = maxRank")]
        public int[] rankCosts = { 1, 2, 3 };

        [Header("전제 (전부 만렙이어야 해금 — 라인 루트는 비움, 캡스톤은 3개)")]
        public TraitId[] parents;

        [Tooltip("샌드박스 패널 뱃지용: 실제 / 샌드박스 / 스텁")]
        public string connStatus = "실제";

        /// <summary>currentRank → currentRank+1 로 올리는 비용.</summary>
        public int CostForRank(int currentRank)
            => (rankCosts != null && currentRank >= 0 && currentRank < rankCosts.Length) ? rankCosts[currentRank] : 999;
    }
}
