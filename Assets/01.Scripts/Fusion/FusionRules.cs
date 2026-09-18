using UnityEngine;

/// <summary>
/// 두 슬롯이 합성 가능한지 판정하고 실행하는 순수 규칙.
/// UnitSlot 인터페이스에만 의존해서, 그리드 구현이 스텁(SlotGrid) → 실제 그리드로 바뀌어도
/// 이 클래스는 그대로 재사용할 수 있게 분리해뒀다.
/// 규칙(5.2절): 같은 종류(unitId) + 같은 성급 2기 → 상위 성급 1기. 최대 3성.
/// </summary>
public static class FusionRules
{
    public const int MaxStar = 3;

    public static bool CanFuse(UnitSlot a, UnitSlot b)
    {
        if (a == null || b == null || a == b)
        {
            return false;
        }

        UnitBase unitA = a.occupant;
        UnitBase unitB = b.occupant;
        if (unitA == null || unitB == null)
        {
            return false;
        }

        if (unitA.statData == null || unitB.statData == null)
        {
            return false;
        }

        if (unitA.statData.unitId != unitB.statData.unitId)
        {
            return false;
        }

        if (unitA.statData.starLevel != unitB.statData.starLevel)
        {
            return false;
        }

        return unitA.statData.starLevel < MaxStar;
    }

    /// <summary>
    /// keepSlot의 유닛을 성급업 시키고 removeSlot의 유닛은 제거한다(칸 반환).
    /// 실행 전 CanFuse()로 먼저 확인할 것.
    /// </summary>
    public static void Fuse(UnitSlot keepSlot, UnitSlot removeSlot)
    {
        UnitBase keepUnit = keepSlot.occupant;
        UnitBase removeUnit = removeSlot.occupant;

        int newStar = keepUnit.statData.starLevel + 1;

        removeSlot.Clear();
        Object.Destroy(removeUnit.gameObject);

        keepUnit.ApplyStarLevel(newStar);
    }
}
