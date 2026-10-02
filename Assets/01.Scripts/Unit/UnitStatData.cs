using UnityEngine;
using OZGL2.Synergy;

/// <summary>
/// 유닛 1종의 기초 스탯을 담는 ScriptableObject.
/// 코드 재빌드 없이 인스펙터에서 밸런싱할 수 있게 데이터를 분리해뒀다
/// (추후 성민 파트의 "게임데이터 설계/로드"에서 스프레드시트 임포트로 대체/연동 가능).
/// </summary>
[CreateAssetMenu(fileName = "New Unit Stat", menuName = "MajokDefense/Unit Stat Data")]
public class UnitStatData : ScriptableObject
{
    [Header("기본 정보")]
    public string unitId;          // 코드/데이터에서 참조할 고유 키 (예: "Hero_Warrior", "Army_MeleeDPS")
    public string displayName;     // 화면 표시용 이름
    public UnitSide side;

    [Header("직업 (시너지·특성 배율 판정용 — 성민 파트 연계)")]
    public SynergyJob job;

    [Header("스탯 (밸런스시트 03.전투공식 기준)")]
    public int maxHealth = 100;
    public float attackPower = 10f;
    public float attackSpeed = 1f;   // 초당 공격 횟수

    [Range(0f, 0.8f)]
    public float defensePercent = 0f;  // 받는 피해 = 공격력 x (1 - min(방어%, 0.8)). 상한 80% 확정(항상 최소 20% 관통)

    // ⚠ 칸(그리드 단위) → 월드 유닛 환산값이 아직 미확정 (밸런스시트 "사거리 1칸" 셀 #ERROR, 준기와 협의 필요).
    // 지금은 시트에 적힌 칸 수를 그대로 월드 유닛으로 취급 — 협의 끝나면 일괄 보정 예정.
    public float attackRange = 1f;
    public float rangeBonusPerStar = 0f; // 성급 상승 시 사거리 증가량(성급-1 배). 힐러·궁수 전용, 그 외 0
    public float moveSpeed = 2f;       // 유닛/초. 마왕군은 배치형이라 보통 미사용, 용사만 실사용
    public float healAmount = 0f;      // 힐러 전용 1회 힐량 (힐/초 = healAmount x attackSpeed)

    // SPUM의 ATTACK 상태엔 클립이 여러 개(인덱스순) 등록될 수 있다. 유닛 스폰 후 Play 모드에서
    // 해당 SPUM_Prefabs 컴포넌트의 Attack List를 보고, 원거리/마법 전용 클립이 있으면 그 인덱스를
    // 여기 넣는다. 리스트 범위를 벗어나면 자동으로 0번으로 보정됨.
    public int attackAnimationIndex = 0;

    [Header("원거리 공격 (비워두면 근접/즉시 판정, 채우면 발사체 발사)")]
    public GameObject projectilePrefab;  // 관통 없음 · 단일 대상 유도. 용사/마왕군 공용 프리팹(Arrow, Fireball 등) 할당
    public float projectileSpeed = 8f;   // 유닛/초
    // 프리팹 스프라이트/애니메이션이 기본으로 바라보는 방향 (Arrow=위, Fireball=오른쪽처럼 프리팹마다 다름).
    // Projectile이 이 값을 기준으로 실제 진행 방향을 바라보게 회전시킨다.
    public Vector2 projectileDefaultFacing = Vector2.up;

    [Header("스플래시 (0이면 단일 대상. 근접·발사체 공격 양쪽에 적용)")]
    public float splashRadius = 0f;      // 주 타겟 위치 기준 이 반경 안의 다른 적도 피해

    [Range(0f, 1f)]
    public float splashSecondaryDamagePercent = 1f; // 주 타겟 외 간접 피격 대상에게 적용할 피해 비율(1=전액, 0.5=절반)
    public int splashMaxTargets = 0;     // 간접 피격 최대 인원수(주 타겟 제외). 0 이하면 무제한

    [Header("CC 저항 (0=그대로 적용, 1=완전 면역 — 보스 등 특수 유닛용)")]
    [Range(0f, 1f)]
    public float ccResistance = 0f;    // 둔화 강도·기절 지속시간을 이 비율만큼 줄여서 적용

    [Header("단일 대상 직업 기믹 (궁수·도적 — 성급 2 이상에서만 발동)")]
    public bool targetLowestHealthEnemy = false; // 최근접 대신 사거리 안 최저 체력 적 우선(마무리 특화)
    public int comboStunAttackInterval = 0;      // N번째 공격마다 대상 기절(0=비활성). 도적 예: 3
    public float comboStunDuration = 1f;         // 위 스턴 지속시간
    [Range(0f, 3f)]
    public float executeDamageBonusPerMissingHealth = 0f; // 대상이 잃은 체력 비율만큼 추가 피해 배율(0=비활성, 0.5=최대 +50%)

    [Header("화상(도트) — 기본공격 적중 시 대상에게 지속 피해 추가 (0=비활성)")]
    public float burnDamagePerSecond = 0f;
    public float burnDuration = 0f;

    [Header("회복 오라 — 공격 여부와 무관하게 주기적으로 자신 포함 주변 아군 회복 (0=비활성)")]
    public float auraHealAmount = 0f;
    public float auraHealInterval = 0f;
    public float auraHealRadius = 0f;

    [Header("소환(교황류) — 주기적으로 아군(용사) 증원 소환, 소환수는 풀링 없이 직접 스폰 (0=비활성)")]
    public GameObject summonPrefab;        // 소환할 유닛 프리팹(UnitBase 포함된 용사 프리팹)
    public float summonInterval = 0f;      // 소환 주기(초)
    public int summonCountPerWave = 0;     // 1회 소환 시 마리 수
    public int summonMaxActive = 0;        // 이 보스가 살려둔 소환수 동시 생존 상한
    public float summonSpawnRadius = 1.5f; // 소환 위치가 자신 중심으로 퍼지는 반경
    [Range(1f, 3f)]
    public float summonBuffAttackMultiplier = 1f; // 소환 시 주변 아군(용사)에게 거는 공격력 배율(1=비활성)
    public float summonBuffDuration = 0f;
    public float summonBuffRadius = 0f;

    [Header("페이즈 전환(최종보스류) — 체력 비율 이하로 떨어지면 1회 발동 (0=비활성)")]
    [Range(0f, 1f)]
    public float phaseTransitionHealthRatio = 0f; // 예: 0.5 = 체력 50% 이하에서 발동
    public float phaseTransitionAttackMultiplier = 1f; // 발동 시 자신 공격력 배율
    public float phaseTransitionBuffDuration = 0f;
    public float phaseTransitionStunDuration = 0f; // 발동 시 주변 적(마왕군) 전체 스턴
    public float phaseTransitionRadius = 0f;

    [Header("참고 데이터 (다른 파트 연계용, 세진 파트에서는 미사용)")]
    public int cost = 1;                // 마왕군 코스트 (배치/뽑기 비용 — 김건·준기 파트 연계)
    public int killExpReward = 0;       // 용사 처치 시 지급 경험치 (성민 파트 연계)

    [Header("합성 (5.2절 — Day3에서 사용)")]
    [Range(1, 3)]
    public int starLevel = 1;
    // 성급별 배율: 1성 x1.0 / 2성 x1.7 / 3성 x3.2 — 밸런스 테스트에서 2성(x2.1)이 너무 세다는 피드백으로 하향(2026-09-29, 성민).
    // 예전 기획서 5.2절 값은 2.1 / 4.5. 밸런스 시트 04.성급배율과 같이 맞춰 둘 것.
    public static float GetStarMultiplier(int star)
    {
        switch (star)
        {
            case 1: return 1.0f;
            case 2: return 1.7f;
            case 3: return 3.2f;
            default: return 1.0f;
        }
    }
}
