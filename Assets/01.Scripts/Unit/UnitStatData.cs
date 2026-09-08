using UnityEngine;

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

    [Header("스탯 (밸런스시트 03.전투공식 기준)")]
    public int maxHealth = 100;
    public float attackPower = 10f;
    public float attackSpeed = 1f;   // 초당 공격 횟수

    [Range(0f, 0.8f)]
    public float defensePercent = 0f;  // 받는 피해 = 공격력 x (1 - min(방어%, 0.8)). 상한 80% 확정(항상 최소 20% 관통)

    // ⚠ 칸(그리드 단위) → 월드 유닛 환산값이 아직 미확정 (밸런스시트 "사거리 1칸" 셀 #ERROR, 준기와 협의 필요).
    // 지금은 시트에 적힌 칸 수를 그대로 월드 유닛으로 취급 — 협의 끝나면 일괄 보정 예정.
    public float attackRange = 1f;
    public float moveSpeed = 2f;       // 유닛/초. 마왕군은 배치형이라 보통 미사용, 용사만 실사용
    public float healAmount = 0f;      // 힐러 전용 1회 힐량 (힐/초 = healAmount x attackSpeed)

    [Header("참고 데이터 (다른 파트 연계용, 세진 파트에서는 미사용)")]
    public int cost = 1;                // 마왕군 코스트 (배치/뽑기 비용 — 김건·준기 파트 연계)
    public int killExpReward = 0;       // 용사 처치 시 지급 경험치 (성민 파트 연계)

    [Header("합성 (5.2절 — Day3에서 사용)")]
    [Range(1, 3)]
    public int starLevel = 1;
    // 성급별 배율: 1성 x1.0 / 2성 x2.1 / 3성 x4.5 (기획서 5.2절 확정 수치)
    public static float GetStarMultiplier(int star)
    {
        switch (star)
        {
            case 1: return 1.0f;
            case 2: return 2.1f;
            case 3: return 4.5f;
            default: return 1.0f;
        }
    }
}
