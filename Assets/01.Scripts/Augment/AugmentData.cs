using UnityEngine;

namespace OZGL2.Augment
{
    /// <summary>
    /// 증강 효과 타입. 특성과 달리 "런 한정" — AugmentRun 이 매 런 초기화, 저장 안 함.
    /// Instant* 는 픽 순간 1회 실행하고 끝(누적 배율에 안 들어감).
    /// </summary>
    public enum AugmentEffect
    {
        SkillDamage, SkillCooldown, SkillRadius, SkillBuffDuration, SkillCapstone,
        MonDefense, MonHp, MonAttack, MonAttackSpeed,
        HeroMoveSpeed, HeroDefense, HeroAttack, HeroVulnerable,
        XpGain, Grid,
        InstantSp, InstantHealMonsters, InstantXp, InstantResetCooldowns,
        // 특이 매커니즘 (롤 아레나 스타일 — 판을 바꾸는 픽)
        CritChance,        // 스킬 시전 시 확률로 피해 1.5배
        EchoRecast,        // 스킬 시전 시 확률로 쿨탐 없이 즉시 한 번 더 발동
        CooldownOnKill,    // 용사 처치 시 모든 스킬 쿨탐 N초 감소
        ExplodeOnDeath,    // 용사가 죽으면 그 자리에서 주변 용사에게 소규모 폭발 피해
        OnHitSlow,         // AreaDamage 계열 스킬에 맞은 용사 이동속도 즉시 감소(2초)
        MonsterReviveChance, // 몬스터가 죽으면 확률로 즉시 부활
        MonsterShield,     // 모든 몬스터가 첫 피격 1회를 무효화하는 보호막을 두르고 시작
        XpPerAliveMonster, // 용사 처치 XP에 "생존 몬스터 수" 만큼 보너스 추가
        PredatorInstinct,  // 콤보: 몬스터 공격력 + 공격속도 동시 상승
        FrostLance,        // 콤보: 용사 이동속도 + 방어력 동시 감소
    }

    /// <summary>
    /// 증강 카드 1종. 무료, 라운드 마일스톤마다 3장 제시 → 1장 선택.
    /// **전부 유니크** — 한 번 고르면 이번 런에서 다시 안 뜸(스택 없음). 특성과 달리 계정에 저장 안 함(런 종료 시 소멸).
    /// </summary>
    [CreateAssetMenu(menuName = "OZGL2/Augment Data", fileName = "SO_Augment_")]
    public class AugmentData : ScriptableObject
    {
        public string augmentId;
        public string displayName;
        [TextArea] public string description;
        [Range(1, 3)] public int tier = 1;
        [Tooltip("같은 티어 안에서 상대 가중치 — 클수록 더 자주 나옴")]
        public float weight = 10f;

        public AugmentEffect effect;
        [Tooltip("이 카드의 고정 효과값 (한 번만 적용, 스택 없음)")]
        public float value = 0.06f;
        [Tooltip("이번 런에서 최대 몇 번 뽑을 수 있는지. 기본 1 = 완전 유니크")]
        public int maxStack = 1;

        [Tooltip("픽 순간 1회 실행 — 누적 배율에 반영 안 됨 (즉시 회복·즉시 SP·즉시 XP 등)")]
        public bool isInstant = false;

        public string connStatus = "실제";

        /// <summary>티어 표시명 — 1=실버 2=골드 3=플래티넘.</summary>
        public static string TierName(int tier) => tier switch
        {
            1 => "실버",
            2 => "골드",
            3 => "플래티넘",
            _ => $"T{tier}",
        };
    }
}
