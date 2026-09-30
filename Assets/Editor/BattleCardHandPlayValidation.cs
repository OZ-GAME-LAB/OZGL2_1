using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 실제 Scene이나 Prefab 에셋을 저장하지 않고 Play Mode에서 손패 런타임 동작을 검증한다.
/// Grid 어댑터는 비활성화하고 표시 전용 외부 목록 API만 사용한다.
/// </summary>
public static class BattleCardHandPlayValidation
{
    private const string HAND_PREFAB_PATH =
        "Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/BattleCardHand.prefab";
    private const float WAIT_TIMEOUT = 2f;
    private const float POSITION_TOLERANCE = 1f;
    private const float SCALE_TOLERANCE = 0.02f;
    private const float ROTATION_TOLERANCE = 0.2f;
    private const float FAN_ARC_HEIGHT = 32f;
    private const float MAX_FAN_ANGLE = 8f;
    private const float HOVER_SCALE = 1.44f;

    private static bool _isRunning;

    public static string LastResult { get; private set; } = "아직 실행하지 않았습니다.";

    [MenuItem("Tools/OZGL/UI/Battle/Validate Card Hand Runtime (Play Mode)")]
    private static async void ValidateRuntimeMenu()
    {
        if (!Application.isPlaying)
        {
            LastResult = "실패: Play Mode에서만 실행할 수 있습니다.";
            Debug.LogError(LastResult);
            return;
        }

        if (_isRunning)
        {
            Debug.LogWarning("전투 카드 손패 Play Mode 검증이 이미 실행 중입니다.");
            return;
        }

        _isRunning = true;
        float previousTimeScale = Time.timeScale;
        bool previousRunInBackground = Application.runInBackground;
        GameObject validationContainer = null;
        GameObject handInstance = null;
        GameObject temporaryEventSystem = null;
        UIBattleCardHandView handView = null;
        Action<UIBattleCardHandSlot, PointerEventData> clickHandler = null;
        Action<UIBattleCardHandSlot, PointerEventData> beginDragHandler = null;
        Action<UIBattleCardHandSlot, PointerEventData> dragHandler = null;
        Action<UIBattleCardHandSlot, PointerEventData> endDragHandler = null;

        try
        {
            // MCP나 비활성 Game View에서도 검증 프레임이 정지하지 않도록 검증 동안만 허용한다.
            Application.runInBackground = true;
            Time.timeScale = 0f;

            GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HAND_PREFAB_PATH);
            Need(handPrefab != null, "손패 Prefab을 찾을 수 없습니다: " + HAND_PREFAB_PATH);

            // 비활성 부모 아래에서 인스턴스를 만든 뒤 실제 Grid 연결 컴포넌트를 먼저 끈다.
            // 이 과정은 현재 Scene과 Project 에셋을 저장하거나 Dirty 처리하지 않는다.
            validationContainer = new GameObject("__BattleCardHandPlayValidation__");
            validationContainer.SetActive(false);
            handInstance = PrefabUtility.InstantiatePrefab(handPrefab, validationContainer.transform) as GameObject;
            Need(handInstance != null, "손패 Prefab 인스턴스 생성에 실패했습니다.");

            UIGridStorageHandAdapter adapter =
                handInstance.GetComponentInChildren<UIGridStorageHandAdapter>(true);
            Need(adapter != null, "UIGridStorageHandAdapter가 없습니다.");
            adapter.enabled = false;

            BattleHandEventSystemInstaller eventSystemInstaller =
                handInstance.GetComponentInChildren<BattleHandEventSystemInstaller>(true);
            if (eventSystemInstaller != null) eventSystemInstaller.enabled = false;

            // 검증 중 실제 마우스 입력이 임시 인스턴스에 섞이지 않도록 Raycaster를 끄고
            // Slot 포인터 콜백을 직접 호출해 호버/드래그 이벤트 분리를 확인한다.
            GraphicRaycaster validationRaycaster =
                handInstance.GetComponentInChildren<GraphicRaycaster>(true);
            Need(validationRaycaster != null, "검증용 GraphicRaycaster가 없습니다.");
            validationRaycaster.enabled = false;

            handView = handInstance.GetComponentInChildren<UIBattleCardHandView>(true);
            Need(handView != null, "UIBattleCardHandView가 없습니다.");

            int clickCount = 0;
            int beginDragCount = 0;
            int dragCount = 0;
            int endDragCount = 0;
            clickHandler = (slot, eventData) => clickCount++;
            beginDragHandler = (slot, eventData) => beginDragCount++;
            dragHandler = (slot, eventData) => dragCount++;
            endDragHandler = (slot, eventData) => endDragCount++;
            handView.CardClicked += clickHandler;
            handView.CardBeginDrag += beginDragHandler;
            handView.CardDragged += dragHandler;
            handView.CardEndDrag += endDragHandler;

            validationContainer.SetActive(true);
            if (EventSystem.current == null)
                temporaryEventSystem = new GameObject(
                    "__BattleCardHandValidationEventSystem__",
                    typeof(EventSystem));
            Need(!adapter.enabled, "검증 중 Grid 어댑터가 활성화되었습니다.");
            Need(!validationRaycaster.enabled, "검증 중 실제 마우스 Raycaster가 활성화되었습니다.");
            await WaitForNextFrameAsync(WAIT_TIMEOUT);
            ForceLayout(handView);

            ValidateEmptyAndSingle(handView);
            ValidateSeveral(handView);
            await ValidateOverflowAsync(handView);
            await ValidateHoverAnimationAsync(handView);
            await ValidateHoverMutationAndResetAsync(handView);

            Need(clickCount == 0, "호버만으로 카드 클릭 이벤트가 호출되었습니다.");
            Need(beginDragCount == 0 && dragCount == 0 && endDragCount == 0,
                "호버만으로 카드 드래그 이벤트가 호출되었습니다.");

            LastResult = string.Join("\n", new[]
            {
                "전투 카드 손패 Play Mode 검증 완료",
                "- Time.timeScale 0에서 unscaled 호버 상승·확대·회전 정렬(1.44 / 60 / 0도 / 0.15) 완료",
                "- 0장·1장·여러 장·40장 외부 SetItems 및 좌우 대칭 부채꼴 배치 정상",
                "- 초과 카드 가로 Scrollbar와 마지막 카드 접근 구조 정상",
                "- 빠른 호버 전환·해제 시 이전 카드 기본 회전 복귀 및 단일 호버 정상",
                "- 슬롯 논리 sibling 고정, 호버 비주얼 전용 Canvas 전면 표시 및 겹침 입력 순서 유지 정상",
                "- Viewport 밖으로 잘린 카드 비주얼의 포인터 포함 판정 제외 정상",
                "- 호버 중 추가·삭제, 호버 카드 삭제, disable/enable 및 위치·크기·회전 초기화 정상",
                "- 호버로 클릭·드래그 이벤트가 호출되지 않음",
                "- UIGridStorageHandAdapter 비활성 상태로 실제 Grid 배치 명령은 호출하지 않음",
                "- 검증 인스턴스는 종료 시 파기하며 Scene/Prefab/SO는 저장하지 않음"
            });
            Debug.Log(LastResult);
        }
        catch (Exception exception)
        {
            LastResult = "전투 카드 손패 Play Mode 검증 실패\n" + exception;
            Debug.LogError(LastResult);
        }
        finally
        {
            if (handView != null)
            {
                if (clickHandler != null) handView.CardClicked -= clickHandler;
                if (beginDragHandler != null) handView.CardBeginDrag -= beginDragHandler;
                if (dragHandler != null) handView.CardDragged -= dragHandler;
                if (endDragHandler != null) handView.CardEndDrag -= endDragHandler;
            }

            Time.timeScale = previousTimeScale;
            Application.runInBackground = previousRunInBackground;
            // 검증 직후 Game View가 비활성화되어도 임시 오브젝트가 다음 프레임까지 남지 않게 즉시 정리한다.
            // Editor 전용 검증 오브젝트만 대상으로 하며 Scene/Prefab 에셋에는 적용하지 않는다.
            if (validationContainer != null) Object.DestroyImmediate(validationContainer);
            else if (handInstance != null) Object.DestroyImmediate(handInstance);
            if (temporaryEventSystem != null) Object.DestroyImmediate(temporaryEventSystem);
            _isRunning = false;
        }
    }

    [MenuItem("Tools/OZGL/UI/Battle/Validate Card Hand Runtime (Play Mode)", true)]
    private static bool CanValidateRuntimeMenu()
        => Application.isPlaying && !_isRunning;

    private static void ValidateEmptyAndSingle(UIBattleCardHandView handView)
    {
        handView.Clear();
        handView.RefreshLayoutImmediate();
        Need(handView.Slots.Count == 0, "0장 상태가 아닙니다.");

        List<BattleHandCardDisplayData> single = CreateItems(1, "SINGLE");
        handView.SetItems(single);
        handView.RefreshLayoutImmediate();
        Need(handView.Slots.Count == 1, "1장 상태가 아닙니다.");
        AssertLogicalOrder(handView, single);
    }

    private static void ValidateSeveral(UIBattleCardHandView handView)
    {
        List<BattleHandCardDisplayData> items = CreateItems(5, "SEVERAL");
        handView.SetItems(items);
        handView.RefreshLayoutImmediate();
        AssertLogicalOrder(handView, items);

        float previousX = float.NegativeInfinity;
        for (int index = 0; index < handView.Slots.Count; index++)
        {
            RectTransform slotRect = handView.Slots[index].transform as RectTransform;
            Need(slotRect != null, "카드 슬롯 RectTransform이 없습니다: " + index);
            Need(slotRect.anchoredPosition.x > previousX,
                "여러 장 카드가 좌→우 순서로 배치되지 않았습니다: " + index);
            Need(slotRect.GetSiblingIndex() == index,
                "기본 표시 순서가 논리 순서와 다릅니다: " + index);
            previousX = slotRect.anchoredPosition.x;
        }

        AssertFanLayout(handView, "여러 장 배치");
    }

    private static async Task ValidateOverflowAsync(UIBattleCardHandView handView)
    {
        List<BattleHandCardDisplayData> items = CreateItems(40, "OVERFLOW");
        handView.SetItems(items);
        handView.RefreshLayoutImmediate();
        ForceLayout(handView);
        AssertLogicalOrder(handView, items);

        ScrollRect scrollRect = handView.GetComponent<ScrollRect>();
        Need(scrollRect != null, "ScrollRect가 없습니다.");
        Need(scrollRect.horizontal, "40장 초과 상태에서 가로 Scroll이 활성화되지 않았습니다.");
        Need(scrollRect.viewport != null && scrollRect.content != null,
            "ScrollRect의 Viewport 또는 Content 연결이 없습니다.");
        Need(scrollRect.content.rect.width > scrollRect.viewport.rect.width,
            "40장 상태에서 Content가 Viewport보다 넓지 않습니다.");
        Need(scrollRect.horizontalScrollbar != null,
            "가로 Scrollbar가 연결되지 않았습니다.");
        Need(scrollRect.horizontalScrollbar.gameObject.activeInHierarchy,
            "40장 상태에서 가로 Scrollbar가 표시되지 않았습니다.");
        Need(scrollRect.horizontalScrollbar.interactable,
            "40장 상태에서 가로 Scrollbar를 조작할 수 없습니다.");

        scrollRect.horizontalNormalizedPosition = 1f;
        ForceLayout(handView);
        await WaitForNextFrameAsync(WAIT_TIMEOUT);
        ForceLayout(handView);

        RectTransform lastSlot = handView.Slots[handView.Slots.Count - 1].transform as RectTransform;
        Need(lastSlot != null, "마지막 카드 슬롯 RectTransform이 없습니다.");
        Bounds lastBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            scrollRect.viewport,
            lastSlot);
        Rect viewportRect = scrollRect.viewport.rect;
        float visibleWidth = Mathf.Max(
            0f,
            Mathf.Min(lastBounds.max.x, viewportRect.xMax) -
            Mathf.Max(lastBounds.min.x, viewportRect.xMin));
        Need(visibleWidth >= 55f,
            "오른쪽 끝으로 스크롤해도 마지막 카드의 최소 노출 폭을 확보하지 못했습니다. visible=" +
            visibleWidth);

        UIBattleCardHandSlot firstSlot = handView.Slots[0];
        RectTransform firstVisual = FindVisualRoot(firstSlot);
        Vector2 firstVisualCenter = RectTransformUtility.WorldToScreenPoint(
            null,
            firstVisual.TransformPoint(firstVisual.rect.center));
        Need(!handView.IsScreenPointInsideViewport(firstVisualCenter, null),
            "Viewport 밖 비주얼 판정을 검증할 첫 카드가 실제로 Viewport 안에 있습니다.");
        Need(!InvokeContainsVisualScreenPoint(firstSlot, firstVisualCenter, null),
            "Viewport 밖으로 잘린 카드 비주얼이 포인터 포함 대상으로 판정되었습니다.");
    }

    private static async Task ValidateHoverAnimationAsync(UIBattleCardHandView handView)
    {
        List<BattleHandCardDisplayData> items = CreateItems(5, "HOVER");
        handView.SetItems(items);
        handView.RefreshLayoutImmediate();
        handView.ResetTransientInteraction();

        UIBattleCardHandSlot first = handView.Slots[0];
        UIBattleCardHandSlot second = handView.Slots[1];
        RectTransform firstVisual = FindVisualRoot(first);
        RectTransform firstHitArea = FindHitArea(first);
        Canvas handCanvas = handView.GetComponent<Canvas>();
        Canvas firstVisualCanvas = firstVisual.GetComponent<Canvas>();
        Need(handCanvas != null, "손패 root Canvas가 없습니다.");
        Need(firstVisualCanvas != null, "카드 VisualRoot에 전면 표시용 Canvas가 없습니다.");
        Need(!firstVisualCanvas.overrideSorting,
            "호버 전 VisualRoot Canvas의 overrideSorting이 활성화되어 있습니다.");
        float firstHitAreaWidth = firstHitArea.rect.width;
        float firstBaseRotation = GetVisualRotation(firstVisual);
        first.OnPointerEnter(null);

        await WaitUntilAsync(
            () => IsHoverVisual(firstVisual),
            WAIT_TIMEOUT,
            "Time.timeScale 0에서 첫 카드의 unscaled 호버 전환이 완료되지 않았습니다.");
        Need(first.IsHovered, "첫 카드의 호버 상태가 설정되지 않았습니다.");
        Need(Approximately(firstVisual.anchoredPosition.y, 60f, POSITION_TOLERANCE),
            "호버 상승 높이가 60 UI 단위가 아닙니다: " + firstVisual.anchoredPosition.y);
        Need(Approximately(firstVisual.localScale.x, HOVER_SCALE, SCALE_TOLERANCE),
            "호버 확대 배율이 1.44가 아닙니다: " + firstVisual.localScale.x);
        Need(Approximately(GetVisualRotation(firstVisual), 0f, ROTATION_TOLERANCE),
            "호버 카드 회전이 0도가 아닙니다: " + GetVisualRotation(firstVisual));
        Need(first.transform.GetSiblingIndex() == 0,
            "호버 중 첫 카드 slotRoot sibling 인덱스가 논리 순서에서 변경되었습니다.");
        Need(firstVisualCanvas.overrideSorting,
            "호버 중 VisualRoot Canvas의 overrideSorting이 활성화되지 않았습니다.");
        Need(firstVisualCanvas.sortingOrder == handCanvas.sortingOrder + 1,
            "호버 VisualRoot가 손패 root Canvas보다 정확히 한 단계 앞에 있지 않습니다.");
        Need(firstVisualCanvas.sortingOrder < 100,
            "호버 VisualRoot가 상위 Popup Canvas 정렬 순서(100) 이상입니다.");
        Need(Approximately(firstHitArea.rect.width, firstHitAreaWidth, POSITION_TOLERANCE),
            "호버 중 HitArea 폭이 늘어나 인접 카드 최소 노출 영역을 침범합니다.");
        first.OnPointerExit(null);
        await WaitUntilAsync(
            () => IsBaseVisual(firstVisual, firstBaseRotation),
            WAIT_TIMEOUT,
            "호버 해제 후 첫 카드가 기본 회전으로 복귀하지 않았습니다.");
        Need(!first.IsHovered, "호버 해제 후 첫 카드의 호버 상태가 남았습니다.");
        Need(first.transform.GetSiblingIndex() == 0,
            "호버 해제 후 첫 카드 slotRoot sibling 인덱스가 논리 순서와 다릅니다.");
        Need(!firstVisualCanvas.overrideSorting,
            "호버 해제 후 VisualRoot Canvas의 overrideSorting이 남았습니다.");

        handView.ResetTransientInteraction();
        first.OnPointerEnter(null);
        await WaitForNextFrameAsync(WAIT_TIMEOUT);
        RectTransform secondVisual = FindVisualRoot(second);
        Canvas secondVisualCanvas = secondVisual.GetComponent<Canvas>();
        second.OnPointerEnter(null);

        await WaitUntilAsync(
            () => IsBaseVisual(firstVisual, firstBaseRotation) && IsHoverVisual(secondVisual),
            WAIT_TIMEOUT,
            "빠른 카드 전환 후 이전/현재 카드 전환이 정상 상태로 완료되지 않았습니다.");
        Need(!first.IsHovered && second.IsHovered,
            "빠른 카드 전환 후 두 번째 카드만 호버 상태가 아닙니다.");
        Need(CountHovered(handView) == 1, "동시에 둘 이상의 카드가 호버 상태입니다.");
        Need(first.transform.GetSiblingIndex() == 0 && second.transform.GetSiblingIndex() == 1,
            "빠른 호버 전환 중 slotRoot sibling 논리 순서가 변경되었습니다.");
        Need(!firstVisualCanvas.overrideSorting && secondVisualCanvas != null &&
             secondVisualCanvas.overrideSorting,
            "빠른 호버 전환 후 현재 카드 VisualRoot만 전면 Canvas 상태가 아닙니다.");
    }

    private static async Task ValidateHoverMutationAndResetAsync(UIBattleCardHandView handView)
    {
        List<BattleHandCardDisplayData> items = CreateItems(5, "MUTATION");
        handView.SetItems(items);
        handView.RefreshLayoutImmediate();

        UIBattleCardHandSlot hovered = handView.Slots[2];
        string hoveredId = hovered.Id;
        hovered.OnPointerEnter(null);
        await WaitForNextFrameAsync(WAIT_TIMEOUT);

        List<BattleHandCardDisplayData> added = new List<BattleHandCardDisplayData>(items)
        {
            CreateItem("MUTATION:5", 5)
        };
        handView.SetItems(added);
        handView.RefreshLayoutImmediate();
        AssertLogicalOrder(handView, added);
        Need(hovered.IsHovered && CountHovered(handView) == 1,
            "호버 중 목록 추가 후 호버 참조가 비정상입니다.");

        List<BattleHandCardDisplayData> removed = new List<BattleHandCardDisplayData>
        {
            added[0],
            added[1],
            added[3],
            added[4],
            added[5]
        };
        handView.SetItems(removed);
        handView.RefreshLayoutImmediate();
        await WaitForNextFrameAsync(WAIT_TIMEOUT);
        AssertLogicalOrder(handView, removed);
        Need(CountHovered(handView) == 0,
            "호버 카드 삭제 후 호버 상태가 남았습니다.");
        Need(!ContainsId(handView, hoveredId),
            "삭제한 호버 카드가 논리 목록에 남았습니다.");

        handView.Slots[0].OnPointerEnter(null);
        handView.ResetTransientInteraction();
        AssertAllTransientStatesReset(handView, "ResetTransientInteraction");

        List<BattleHandCardDisplayData> disableOverflowItems = CreateItems(40, "DISABLE_OVERFLOW");
        handView.SetItems(disableOverflowItems);
        handView.RefreshLayoutImmediate();
        ForceLayout(handView);
        Scrollbar disableScrollbar = handView.GetComponent<ScrollRect>().horizontalScrollbar;
        Need(disableScrollbar != null && disableScrollbar.gameObject.activeInHierarchy &&
             disableScrollbar.interactable,
            "View disable 검증 전 초과 카드 Scrollbar가 활성 상태가 아닙니다.");

        handView.Slots[1].OnPointerEnter(null);
        await WaitForNextFrameAsync(WAIT_TIMEOUT);
        handView.enabled = false;
        AssertAllTransientStatesReset(handView, "View disable");
        AssertSlotRaycastTargets(handView, false, "View disable");
        Need(!disableScrollbar.interactable && !disableScrollbar.gameObject.activeSelf,
            "View disable 후 Scrollbar 입력 또는 표시가 남았습니다.");
        handView.Slots[1].OnPointerEnter(null);
        Need(!handView.Slots[1].IsHovered,
            "비활성 View가 카드 포인터 입력을 다시 받았습니다.");
        handView.SetInteractable(true);
        handView.SetItems(disableOverflowItems);
        handView.RefreshLayoutImmediate();
        AssertSlotRaycastTargets(handView, false, "비활성 View 초과 목록 갱신");
        Need(!disableScrollbar.interactable && !disableScrollbar.gameObject.activeSelf,
            "비활성 View의 초과 목록 갱신 후 Scrollbar 입력 또는 표시가 다시 활성화되었습니다.");

        List<BattleHandCardDisplayData> disabledItems = CreateItems(6, "DISABLED");
        handView.SetItems(disabledItems);
        AssertSlotRaycastTargets(handView, false, "비활성 View 목록 교체");
        handView.enabled = true;
        await WaitForNextFrameAsync(WAIT_TIMEOUT);
        ForceLayout(handView);
        AssertAllTransientStatesReset(handView, "View re-enable");
        AssertSlotRaycastTargets(handView, true, "View re-enable");
        AssertLogicalOrder(handView, disabledItems);
    }

    private static List<BattleHandCardDisplayData> CreateItems(int count, string prefix)
    {
        var items = new List<BattleHandCardDisplayData>(count);
        for (int index = 0; index < count; index++)
            items.Add(CreateItem(prefix + ":" + index, index));
        return items;
    }

    private static BattleHandCardDisplayData CreateItem(string id, int index)
    {
        eBattleHandCardKind kind = index % 2 == 0
            ? eBattleHandCardKind.UNIT
            : eBattleHandCardKind.LAND_SLOT;
        return new BattleHandCardDisplayData(
            id,
            kind,
            "검증 카드 " + index,
            rankText: index.ToString(),
            footprint: new[] { Vector2Int.zero });
    }

    private static void AssertLogicalOrder(
        UIBattleCardHandView handView,
        IReadOnlyList<BattleHandCardDisplayData> expected)
    {
        Need(handView.Slots.Count == expected.Count,
            $"카드 수가 다릅니다. expected={expected.Count}, actual={handView.Slots.Count}");
        for (int index = 0; index < expected.Count; index++)
        {
            UIBattleCardHandSlot slot = handView.Slots[index];
            Need(slot != null, "카드 슬롯이 null입니다: " + index);
            Need(slot.Id == expected[index].Id,
                $"논리 순서가 다릅니다. index={index}, expected={expected[index].Id}, actual={slot.Id}");
            Need(slot.LogicalIndex == index,
                $"LogicalIndex가 다릅니다. index={index}, actual={slot.LogicalIndex}");
            Need(slot.transform.GetSiblingIndex() == index,
                $"slotRoot sibling 순서가 논리 순서와 다릅니다. index={index}, " +
                $"actual={slot.transform.GetSiblingIndex()}");
        }
    }

    private static void AssertAllTransientStatesReset(UIBattleCardHandView handView, string context)
    {
        for (int index = 0; index < handView.Slots.Count; index++)
        {
            UIBattleCardHandSlot slot = handView.Slots[index];
            Need(!slot.IsHovered && !slot.IsDragging,
                context + " 후 임시 상호작용 상태가 남았습니다: " + index);
            float expectedRotation = CalculateBaseRotation(index, handView.Slots.Count);
            Need(IsBaseVisual(FindVisualRoot(slot), expectedRotation),
                context + " 후 카드 비주얼이 기본 위치·크기·회전으로 복귀하지 않았습니다: " + index);
            Need(slot.transform.GetSiblingIndex() == index,
                context + " 후 기본 표시 순서로 복귀하지 않았습니다: " + index);
            Canvas visualCanvas = FindVisualRoot(slot).GetComponent<Canvas>();
            Need(visualCanvas != null && !visualCanvas.overrideSorting,
                context + " 후 VisualRoot Canvas 전면 상태가 남았습니다: " + index);
        }

        if (handView.Slots.Count >= 3 && handView.Slots.Count % 2 == 1)
            AssertFanLayout(handView, context);
    }

    private static void AssertSlotRaycastTargets(
        UIBattleCardHandView handView,
        bool expected,
        string context)
    {
        for (int index = 0; index < handView.Slots.Count; index++)
        {
            Image hitArea = FindHitArea(handView.Slots[index]).GetComponent<Image>();
            Need(hitArea != null && hitArea.raycastTarget == expected,
                context + " 후 카드 HitArea raycastTarget 상태가 올바르지 않습니다: " + index);
        }
    }

    private static int CountHovered(UIBattleCardHandView handView)
    {
        int count = 0;
        for (int index = 0; index < handView.Slots.Count; index++)
            if (handView.Slots[index] != null && handView.Slots[index].IsHovered) count++;
        return count;
    }

    private static bool ContainsId(UIBattleCardHandView handView, string id)
    {
        for (int index = 0; index < handView.Slots.Count; index++)
            if (handView.Slots[index] != null && handView.Slots[index].Id == id) return true;
        return false;
    }

    private static RectTransform FindVisualRoot(UIBattleCardHandSlot slot)
    {
        Transform visual = slot != null ? slot.transform.Find("VisualRoot") : null;
        RectTransform result = visual as RectTransform;
        Need(result != null, "카드 슬롯의 VisualRoot를 찾을 수 없습니다.");
        return result;
    }

    private static RectTransform FindHitArea(UIBattleCardHandSlot slot)
    {
        Transform hitArea = slot != null ? slot.transform.Find("HitArea") : null;
        RectTransform result = hitArea as RectTransform;
        Need(result != null, "카드 슬롯의 HitArea를 찾을 수 없습니다.");
        return result;
    }

    private static bool InvokeContainsVisualScreenPoint(
        UIBattleCardHandSlot slot,
        Vector2 screenPoint,
        Camera eventCamera)
    {
        MethodInfo method = typeof(UIBattleCardHandSlot).GetMethod(
            "ContainsVisualScreenPoint",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Need(method != null, "ContainsVisualScreenPoint 메서드를 찾을 수 없습니다.");
        object result = method.Invoke(slot, new object[] { screenPoint, eventCamera });
        return result is bool contains && contains;
    }

    private static bool IsHoverVisual(RectTransform visual)
        => visual != null &&
           Approximately(visual.anchoredPosition.y, 60f, POSITION_TOLERANCE) &&
           Approximately(visual.localScale.x, HOVER_SCALE, SCALE_TOLERANCE) &&
           Approximately(visual.localScale.y, HOVER_SCALE, SCALE_TOLERANCE) &&
           Approximately(GetVisualRotation(visual), 0f, ROTATION_TOLERANCE);

    private static bool IsBaseVisual(RectTransform visual, float expectedRotation)
        => visual != null &&
           Approximately(visual.anchoredPosition.x, 0f, POSITION_TOLERANCE) &&
           Approximately(visual.anchoredPosition.y, 0f, POSITION_TOLERANCE) &&
           Approximately(visual.localScale.x, 1f, SCALE_TOLERANCE) &&
           Approximately(visual.localScale.y, 1f, SCALE_TOLERANCE) &&
           Approximately(GetVisualRotation(visual), expectedRotation, ROTATION_TOLERANCE);

    private static void AssertFanLayout(UIBattleCardHandView handView, string context)
    {
        Need(handView.Slots.Count >= 3 && handView.Slots.Count % 2 == 1,
            context + " 검증에는 3장 이상의 홀수 카드가 필요합니다.");

        int centerIndex = handView.Slots.Count / 2;
        int lastIndex = handView.Slots.Count - 1;
        RectTransform center = NeedSlotRect(handView.Slots[centerIndex], context + " 중앙 카드");
        RectTransform leftEdge = NeedSlotRect(handView.Slots[0], context + " 왼쪽 끝 카드");
        RectTransform rightEdge = NeedSlotRect(handView.Slots[lastIndex], context + " 오른쪽 끝 카드");

        Need(Approximately(center.anchoredPosition.y - leftEdge.anchoredPosition.y,
                FAN_ARC_HEIGHT, POSITION_TOLERANCE),
            context + " 중앙 카드 상승 높이가 32가 아닙니다.");
        Need(Approximately(leftEdge.anchoredPosition.y, rightEdge.anchoredPosition.y, POSITION_TOLERANCE),
            context + " 양 끝 카드 높이가 대칭이 아닙니다.");
        Need(Approximately(GetVisualRotation(FindVisualRoot(handView.Slots[0])),
                MAX_FAN_ANGLE, ROTATION_TOLERANCE),
            context + " 왼쪽 끝 카드 회전이 +8도가 아닙니다.");
        Need(Approximately(GetVisualRotation(FindVisualRoot(handView.Slots[centerIndex])),
                0f, ROTATION_TOLERANCE),
            context + " 중앙 카드 회전이 0도가 아닙니다.");
        Need(Approximately(GetVisualRotation(FindVisualRoot(handView.Slots[lastIndex])),
                -MAX_FAN_ANGLE, ROTATION_TOLERANCE),
            context + " 오른쪽 끝 카드 회전이 -8도가 아닙니다.");

        for (int index = 0; index < centerIndex; index++)
        {
            int opposite = lastIndex - index;
            RectTransform left = NeedSlotRect(handView.Slots[index], context + " 왼쪽 카드 " + index);
            RectTransform right = NeedSlotRect(handView.Slots[opposite], context + " 오른쪽 카드 " + opposite);
            Need(Approximately(center.anchoredPosition.x - left.anchoredPosition.x,
                    right.anchoredPosition.x - center.anchoredPosition.x, POSITION_TOLERANCE),
                context + " 카드 X 간격이 좌우 대칭이 아닙니다: " + index);
            Need(Approximately(left.anchoredPosition.y, right.anchoredPosition.y, POSITION_TOLERANCE),
                context + " 카드 Y 위치가 좌우 대칭이 아닙니다: " + index);
            Need(Approximately(GetVisualRotation(FindVisualRoot(handView.Slots[index])),
                    -GetVisualRotation(FindVisualRoot(handView.Slots[opposite])), ROTATION_TOLERANCE),
                context + " 카드 회전이 좌우 대칭이 아닙니다: " + index);
        }
    }

    private static RectTransform NeedSlotRect(UIBattleCardHandSlot slot, string label)
    {
        RectTransform rect = slot != null ? slot.transform as RectTransform : null;
        Need(rect != null, label + " RectTransform이 없습니다.");
        return rect;
    }

    private static float CalculateBaseRotation(int index, int count)
    {
        float normalized = count <= 1 ? 0f : Mathf.Lerp(-1f, 1f, index / (float)(count - 1));
        return -normalized * MAX_FAN_ANGLE;
    }

    private static float GetVisualRotation(RectTransform visual)
        => visual != null ? Mathf.DeltaAngle(0f, visual.localEulerAngles.z) : 0f;

    private static void ForceLayout(UIBattleCardHandView handView)
    {
        if (handView == null) return;
        Canvas.ForceUpdateCanvases();
        RectTransform root = handView.transform as RectTransform;
        if (root != null) LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        handView.RefreshLayoutImmediate();
        Canvas.ForceUpdateCanvases();
    }

    private static async Task WaitForNextFrameAsync(float timeoutSeconds)
    {
        int startFrame = Time.frameCount;
        float startTime = Time.unscaledTime;
        while (Time.frameCount <= startFrame)
        {
            Need(Application.isPlaying, "Play Mode가 검증 도중 종료되었습니다.");
            Need(Time.unscaledTime - startTime <= timeoutSeconds,
                "다음 Play Mode frame 대기 시간이 초과되었습니다.");
            await Task.Delay(1);
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        float timeoutSeconds,
        string timeoutMessage)
    {
        float startTime = Time.unscaledTime;
        while (!predicate())
        {
            Need(Application.isPlaying, "Play Mode가 검증 도중 종료되었습니다.");
            Need(Time.unscaledTime - startTime <= timeoutSeconds, timeoutMessage);
            await Task.Delay(1);
        }
    }

    private static bool Approximately(float left, float right, float tolerance)
        => Mathf.Abs(left - right) <= tolerance;

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
