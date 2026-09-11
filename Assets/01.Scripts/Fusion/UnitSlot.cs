using UnityEngine;

/// <summary>
/// 마왕군 배치판의 칸 하나. 실제 그리드 시스템(김건·준기 파트)이 붙으면 이 컴포넌트를 그대로
/// 이식하거나, 비슷한 인터페이스(occupant/SetOccupant/Clear)로 교체하면 되도록 최소 기능만 가진다.
/// </summary>
public class UnitSlot : MonoBehaviour
{
    public int column;
    public int row;
    public UnitBase occupant;

    [Header("타일 비주얼 (SlotGrid.BuildGrid에서 자동 연결됨)")]
    public SpriteRenderer tileRenderer;
    public Color defaultColor = Color.white;
    public Color validColor = new Color(0.5f, 1f, 0.5f, 1f);   // 배치 가능 — 연두
    public Color invalidColor = new Color(1f, 0.45f, 0.45f, 1f); // 배치 불가능 — 빨강

    public bool IsEmpty => occupant == null;

    public void SetHighlight(bool canPlace)
    {
        if (tileRenderer == null)
        {
            return;
        }

        tileRenderer.color = canPlace ? validColor : invalidColor;
    }

    public void ClearHighlight()
    {
        if (tileRenderer == null)
        {
            return;
        }

        tileRenderer.color = defaultColor;
    }

    /// <summary>유닛을 이 칸에 배치. 위치를 슬롯 위치로 맞추고 Idle로 정착시킨다.</summary>
    public void SetOccupant(UnitBase unit)
    {
        occupant = unit;
        if (unit == null)
        {
            return;
        }

        unit.transform.position = transform.position;
        unit.hasMoveTarget = false;
        unit.currentTarget = null;
        unit.SetState(UnitState.Idle);
    }

    public void Clear()
    {
        occupant = null;
    }
}
