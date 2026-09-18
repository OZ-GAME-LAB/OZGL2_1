using UnityEngine;

namespace OZGL2.Skill
{
    public enum SkillCategory
    {
        Damage,     // 딜
        Debuff,     // 디버프
        Buff,       // 버프
        Ultimate,   // 궁극기급
    }

    public enum SkillCastMode
    {
        Instant,    // 버튼 클릭 즉시 (맵 전체 / 자동 타겟)
        Targeted,   // 드래그로 위치 지정
    }

    public enum SkillEffectType
    {
        AreaDamage,     // 지정 반경 피해 — 화염구, 운석 (barrageCount 로 다연발)
        ChainDamage,    // 가까운 대상부터 연쇄 — 연쇄 번개
        LineDamage,     // 마왕 → 지정점 직선 관통 — 얼음 가시
        SingleDamage,   // 지정점 최근접 1체 고피해 — 신성 화살
        Knockback,      // 반경 넉백 (+skillPower 피해) — 넉백 파동, 지진
        Stun,           // 반경 즉시 정지 — 시간 정지
        Vacuum,         // 지정점으로 끌어당김 + 피해 — 공허 붕괴
        MovingZone,     // 마왕 → 지정점 방향으로 이동하는 지속 장판 — 화염 회오리
        PersistentZone, // 지정점 고정 지속 장판 (zoneEffect) — 빙결 결계·감속 늪·저주 낙인·축복 오라·함정
        HealAllies,     // 전 아군 체력 % 회복 — 흡혈 의식
        AllyBuff,       // 전 아군 스탯 버프 (일정시간) — 광폭화·강철 피부  ※스탯 수정자 대기, 현재 로그+연출
        Revive,         // 죽은 아군 일부 부활 — 망자 부활
    }

    public enum ZoneEffect
    {
        Stun,           // 빙결 결계
        Slow,           // 감속 늪, 함정
        Vulnerable,     // 저주 낙인 (받는 피해 +)
        DamageOverTime, // 독 안개 류
        Heal,           // 축복의 오라 (아군)
    }

    public enum ZoneTarget
    {
        Enemies,
        Allies,
    }

    public enum BuffStat
    {
        AttackSpeed,    // 광폭화
        Defense,        // 강철 피부
        Attack,
    }

    /// <summary>
    /// 스킬 1종 정의. 밸런스 시트 "11.마왕·스킬" 과 대응. Assets → Create → OZGL2/Skill Data.
    /// 필드가 많지만 effectType 에 따라 쓰는 것만 채우면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "OZGL2/Skill Data", fileName = "SO_Skill_")]
    public class SkillData : ScriptableObject
    {
        [Header("식별")]
        public string skillId;
        public string displayName;
        public SkillCategory category = SkillCategory.Damage;
        [Range(1, 4)] public int tier = 1;

        [Header("발동")]
        public SkillCastMode castMode = SkillCastMode.Instant;
        public float cooldown = 12f;
        public SkillEffectType effectType = SkillEffectType.AreaDamage;

        [Header("공통 수치")]
        [Tooltip("피해량 / 넉백 스킬의 부가 피해")]
        public float skillPower = 100f;
        [Tooltip("효과 반경(칸)")]
        public float radius = 1.5f;
        [Tooltip("지속시간(초) — Zone·Stun·Buff / 회복 비율(0~1) — Heal")]
        public float duration = 2f;

        [Header("타이밍")]
        public float castDelay = 0f;
        public float chainInterval = 0.08f;

        [Header("효과별 세부")]
        [Tooltip("ChainDamage 연쇄 수")]
        public int chainCount = 3;
        [Tooltip("AreaDamage 다연발 수 (유성우). 1 = 단발")]
        public int barrageCount = 1;
        [Tooltip("Knockback / Vacuum 힘")]
        public float force = 6f;
        [Tooltip("LineDamage 길이(칸)")]
        public float lineLength = 6f;
        [Tooltip("MovingZone 이동 속도")]
        public float zoneMoveSpeed = 4f;

        [Header("Zone")]
        public ZoneEffect zoneEffect = ZoneEffect.Slow;
        public ZoneTarget zoneTarget = ZoneTarget.Enemies;
        [Tooltip("Zone 틱 간격(초) — Slow/Vulnerable 갱신, DoT/Heal 적용")]
        public float zoneTick = 0.25f;
        [Tooltip("Slow 배율(0~1) / Vulnerable 배율(>1) / DoT·Heal 틱당 값")]
        public float zoneMagnitude = 0.5f;

        [Header("Buff / Revive")]
        public BuffStat buffStat = BuffStat.AttackSpeed;
        [Tooltip("버프 배율 (1.3 = +30%)")]
        public float buffMultiplier = 1.3f;
        [Tooltip("Revive 부활 수")]
        public int reviveCount = 3;

        [Header("VFX (선택 — 비우면 코드 연출)")]
        public GameObject projectileVfx;
        public GameObject castVfx;
        public GameObject perTargetVfx;
        [Tooltip("Vacuum 붕괴 마무리 등 후속 연출")]
        public GameObject finishVfx;
        public bool scaleCastVfxToRadius = false;
        [Tooltip("VFX 가 uGUI(Canvas) 기반이면 체크 — 월드 Canvas 로 감싸 스폰. Pixel Art RPG VFX = true")]
        public bool vfxIsUi = true;
        [Tooltip("투사체가 마왕이 아니라 하늘(위)에서 떨어짐 — 운석 낙하")]
        public bool fallFromSky = false;

        [Header("궁극기 연출")]
        [Tooltip("반경 안 랜덤 위치에 flourishCount 번 뿌리는 화려함용 프리팹")]
        public GameObject flourishVfx;
        public int flourishCount = 0;
    }
}
