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

    [Header("스탯")]
    public int maxHealth = 100;
    public float attackPower = 10f;
    public float attackSpeed = 1f;   // 초당 공격 횟수
    public float attackRange = 1f;   // 칸 단위 (그리드 연동 전까지는 월드 유닛으로 취급)
    public float moveSpeed = 2f;     // 유닛/초

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
