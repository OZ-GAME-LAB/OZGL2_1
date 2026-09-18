using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마왕군 배치판 스텁. 실제 그리드 시스템(김건·준기 파트) 붙기 전까지 격자 생성 + 드래그앤드랍 +
/// 합성 트리거 + 디버그 스폰을 담당한다.
/// 교체 시 가이드: 합성 판정(FusionRules)과 칸 자체(UnitSlot)는 그대로 두고, 이 클래스의
/// "격자를 어떻게 만들고 좌표를 어디에 두는지"와 "입력을 어떻게 받는지"만 실제 그리드 쪽 코드로 바꾸면 된다.
/// </summary>
public class SlotGrid : MonoBehaviour
{
    [Header("격자 설정 (기획서 5.1절 — 시작 그리드 4x3)")]
    public int columns = 4;
    public int rows = 3;
    public float cellSize = 1.5f;
    public Vector3 origin = Vector3.zero;

    [Header("타일 비주얼 (SPUM Ultimate Resource Bundle > Res/Maps/BG/Tile01/Tileset01/TP_Tile01_00 스프라이트)")]
    public Sprite tileSprite;

    [Header("디버그 스폰 (Day3 임시 — 실제 뽑기 UI 붙기 전까지, 빈 칸 우클릭하면 1성으로 스폰)")]
    public UnitBase debugUnitPrefab;

    private UnitSlot[,] slots;
    private UnitSlot dragSourceSlot;
    private UnitBase draggingUnit;
    private UnitSlot hoveredSlot;

    private void Start()
    {
        BuildGrid();
    }

    private void BuildGrid()
    {
        slots = new UnitSlot[columns, rows];

        // origin을 그리드 좌상단이 아니라 "정중앙"으로 취급하도록, 전체 크기의 절반만큼 시작점을 당긴다.
        float halfWidth = (columns - 1) * cellSize * 0.5f;
        float halfHeight = (rows - 1) * cellSize * 0.5f;
        Vector3 startPos = origin + new Vector3(-halfWidth, halfHeight, 0f);

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Vector3 pos = startPos + new Vector3(x * cellSize, -y * cellSize, 0f);

                GameObject slotObj = new GameObject($"Slot_{x}_{y}");
                slotObj.transform.SetParent(transform);
                slotObj.transform.position = pos;
                slotObj.transform.localScale = Vector3.one; // 콜라이더는 스케일 영향 안 받게 1로 고정

                BoxCollider2D col = slotObj.AddComponent<BoxCollider2D>();
                col.size = new Vector2(cellSize * 0.9f, cellSize * 0.9f); // 부모 스케일이 1이라 월드 크기 그대로

                // 스프라이트는 크기가 셀 크기와 다를 수 있어서, 콜라이더와 분리된 자식 오브젝트에서 스케일링한다
                // (슬롯 자체를 스케일링하면 콜라이더 판정 범위까지 같이 늘어나 버려서 분리함).
                GameObject tileObj = new GameObject("Tile");
                tileObj.transform.SetParent(slotObj.transform);
                tileObj.transform.localPosition = Vector3.zero;

                SpriteRenderer tileRenderer = tileObj.AddComponent<SpriteRenderer>();
                tileRenderer.sprite = tileSprite;
                tileRenderer.sortingOrder = -1; // 유닛(기본 0)보다 뒤에 깔리도록
                float spriteWorldSize = tileSprite != null ? tileSprite.bounds.size.x : 1f;
                if (spriteWorldSize > 0f)
                {
                    float scale = cellSize / spriteWorldSize;
                    tileObj.transform.localScale = new Vector3(scale, scale, 1f);
                }

                UnitSlot slot = slotObj.AddComponent<UnitSlot>();
                slot.column = x;
                slot.row = y;
                slot.tileRenderer = tileRenderer;
                slot.ClearHighlight();

                slots[x, y] = slot;
            }
        }
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return; // 마우스 장치 없음 (안전 가드, SPUM 샘플과 동일한 패턴)
        }

        HandleDragInput();
        UpdateHover();

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryDebugSpawn();
        }
    }

    /// <summary>
    /// 마우스 아래 있는 칸을 배치 가능(초록)/불가능(빨강)으로 색칠해서 구분해준다.
    /// 드래그 중이면 "지금 들고 있는 유닛을 여기 놓을 수 있는지" 기준, 아니면 "이 칸이 비어있는지" 기준.
    /// </summary>
    private void UpdateHover()
    {
        UnitSlot slot = RaycastSlot();

        if (slot != hoveredSlot)
        {
            if (hoveredSlot != null)
            {
                hoveredSlot.ClearHighlight();
            }
            hoveredSlot = slot;
        }

        if (hoveredSlot != null)
        {
            hoveredSlot.SetHighlight(CanPlaceAt(hoveredSlot));
        }
    }

    private bool CanPlaceAt(UnitSlot slot)
    {
        if (draggingUnit != null)
        {
            if (slot == dragSourceSlot || slot.IsEmpty)
            {
                return true;
            }
            return FusionRules.CanFuse(slot, dragSourceSlot);
        }

        return slot.IsEmpty;
    }

    private void HandleDragInput()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            UnitSlot slot = RaycastSlot();
            if (slot != null && !slot.IsEmpty)
            {
                dragSourceSlot = slot;
                draggingUnit = slot.occupant;
                draggingUnit.enabled = false; // 드래그 중엔 자체 AI(타겟팅/이동) 잠깐 정지
            }
        }
        else if (Mouse.current.leftButton.isPressed && draggingUnit != null)
        {
            Vector3 mouseWorld = GetMouseWorldPosition();
            Vector3 pos = draggingUnit.transform.position;
            draggingUnit.transform.position = new Vector3(mouseWorld.x, mouseWorld.y, pos.z);
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame && draggingUnit != null)
        {
            UnitSlot dropSlot = RaycastSlot();
            ResolveDrop(dropSlot);
            FinishDrag();
        }
    }

    private void ResolveDrop(UnitSlot dropSlot)
    {
        if (dropSlot == null || dropSlot == dragSourceSlot)
        {
            SnapBack();
            return;
        }

        if (dropSlot.IsEmpty)
        {
            dragSourceSlot.Clear();
            dropSlot.SetOccupant(draggingUnit);
            return;
        }

        // 드롭한 칸에 다른 유닛이 있으면: 합성 가능한지 확인 (드롭 대상 쪽이 남아서 성급업)
        if (FusionRules.CanFuse(dropSlot, dragSourceSlot))
        {
            FusionRules.Fuse(dropSlot, dragSourceSlot); // dragSourceSlot은 Fuse 안에서 Clear + Destroy됨
            return;
        }

        // 종류/성급이 안 맞으면 그냥 원래 자리로
        SnapBack();
    }

    private void SnapBack()
    {
        if (dragSourceSlot != null && draggingUnit != null)
        {
            dragSourceSlot.SetOccupant(draggingUnit);
        }
    }

    private void FinishDrag()
    {
        if (draggingUnit != null)
        {
            draggingUnit.enabled = true;
        }

        draggingUnit = null;
        dragSourceSlot = null;
    }

    private void TryDebugSpawn()
    {
        UnitSlot slot = RaycastSlot();
        if (slot == null || !slot.IsEmpty || debugUnitPrefab == null)
        {
            return;
        }

        UnitBase unit = Instantiate(debugUnitPrefab);
        slot.SetOccupant(unit);
    }

    private UnitSlot RaycastSlot()
    {
        Vector3 worldPos = GetMouseWorldPosition();
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
        return hit.collider != null ? hit.collider.GetComponent<UnitSlot>() : null;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector2 screenPos2D = Mouse.current.position.ReadValue();
        Vector3 screenPos = new Vector3(screenPos2D.x, screenPos2D.y, -Camera.main.transform.position.z);
        return Camera.main.ScreenToWorldPoint(screenPos);
    }
}
