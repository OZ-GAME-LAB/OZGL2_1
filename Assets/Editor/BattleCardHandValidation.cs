using System;
using System.Collections.Generic;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Prefab Stage나 실제 Scene을 열지 않고 에셋의 직렬화 연결만 검사한다.
public static class BattleCardHandValidation
{
    private const string PREFAB_DIRECTORY = "Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs";
    private const string HAND_PREFAB_PATH = PREFAB_DIRECTORY + "/BattleCardHand.prefab";
    private const string UNIT_PREFAB_PATH = PREFAB_DIRECTORY + "/BattleCard_Unit.prefab";
    private const string LAND_SLOT_PREFAB_PATH = PREFAB_DIRECTORY + "/BattleCard_LandSlot.prefab";
    private const string RELIC_PREFAB_PATH = PREFAB_DIRECTORY + "/BattleCard_Relic.prefab";
    private const string UNIT_CATALOG_PATH = "Assets/06.UI/LobbyMutedPreview/Heraldry_Codex_v1/UnitCatalog.asset";

    [MenuItem("Tools/OZGL/UI/Battle/Validate Card Hand Prefab")]
    public static void ValidateMenu()
    {
        try
        {
            Debug.Log(Validate());
        }
        catch (Exception exception)
        {
            Debug.LogError("전투 카드 손패 Prefab 검증 실패\n" + exception);
            throw;
        }
    }

    public static string Validate()
    {
        GameObject root = RequireAsset<GameObject>(HAND_PREFAB_PATH);
        Need(EditorUtility.IsPersistent(root), "손패 대상이 Project 에셋이 아닙니다.");

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            Need(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                "Missing Script: " + GetPath(child, root.transform));
        }

        RectTransform rootRect = NeedComponent<RectTransform>(root, "Root RectTransform");
        Canvas canvas = NeedComponent<Canvas>(root, "Root Canvas");
        CanvasScaler scaler = NeedComponent<CanvasScaler>(root, "Root CanvasScaler");
        NeedComponent<GraphicRaycaster>(root, "Root GraphicRaycaster");
        CanvasGroup canvasGroup = NeedComponent<CanvasGroup>(root, "Root CanvasGroup");
        NeedComponent<BattleHandEventSystemInstaller>(root, "EventSystem installer");
        UIGridStorageHandAdapter adapter = NeedComponent<UIGridStorageHandAdapter>(root, "Grid storage adapter");
        UIBattleCardHandView handView = NeedComponent<UIBattleCardHandView>(root, "Hand view");
        ScrollRect scrollRect = NeedComponent<ScrollRect>(root, "Hand ScrollRect");

        Need(rootRect != null, "Root RectTransform");
        Need(canvas.renderMode == RenderMode.ScreenSpaceOverlay, "Canvas는 Screen Space Overlay여야 합니다.");
        Need(canvas.sortingOrder == 10, "Canvas sortingOrder는 10이어야 합니다.");
        Need(!canvas.overrideSorting, "별도 overrideSorting Canvas를 사용하면 안 됩니다.");
        Need(root.GetComponentsInChildren<Canvas>(true).Length == 1, "손패 내부에 추가 Canvas가 있으면 안 됩니다.");
        Need(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize, "CanvasScaler mode");
        Need(Approximately(scaler.referenceResolution.x, 1920f) && Approximately(scaler.referenceResolution.y, 1080f),
            "CanvasScaler referenceResolution");
        Need(Approximately(scaler.matchWidthOrHeight, 0.5f), "CanvasScaler matchWidthOrHeight");
        Need(canvasGroup.interactable && canvasGroup.blocksRaycasts, "CanvasGroup 입력 설정");

        RectTransform handPanel = FindRect(root.transform, "HandPanel");
        Need(Approximately(handPanel.sizeDelta.x, 1080f) && Approximately(handPanel.sizeDelta.y, 540f),
            "HandPanel 크기는 1080x540이어야 합니다.");
        Need(handPanel.anchorMin == new Vector2(0.5f, 0f) && handPanel.anchorMax == new Vector2(0.5f, 0f),
            "HandPanel은 화면 중앙 하단 anchor여야 합니다.");
        Need(handPanel.pivot == new Vector2(0.5f, 0f), "HandPanel pivot은 중앙 하단이어야 합니다.");
        Need(handPanel.anchoredPosition == Vector2.zero, "HandPanel은 중앙 하단 원점에 배치되어야 합니다.");

        RectTransform viewport = FindRect(handPanel, "Viewport");
        RectTransform content = FindRect(viewport, "Content");
        Scrollbar scrollbar = NeedComponent<Scrollbar>(
            FindRect(handPanel, "HorizontalScrollbar").gameObject,
            "Horizontal Scrollbar");

        Need(scrollRect.horizontal && !scrollRect.vertical, "ScrollRect는 가로 전용이어야 합니다.");
        Need(scrollRect.movementType == ScrollRect.MovementType.Clamped, "ScrollRect movementType");
        Need(scrollRect.viewport == viewport, "ScrollRect viewport 연결");
        Need(scrollRect.content == content, "ScrollRect content 연결");
        Need(scrollRect.horizontalScrollbar == scrollbar, "ScrollRect scrollbar 연결");
        Need(scrollRect.horizontalScrollbarVisibility == ScrollRect.ScrollbarVisibility.AutoHide,
            "Scrollbar overflow 표시 방식");

        Image viewportImage = NeedComponent<Image>(viewport.gameObject, "Viewport Image");
        Need(!viewportImage.raycastTarget, "Viewport 빈 영역은 전장 입력을 막으면 안 됩니다.");
        NeedComponent<RectMask2D>(viewport.gameObject, "Viewport RectMask2D");
        RectTransform background = FindRect(handPanel, "Background");
        Image backgroundImage = NeedComponent<Image>(background.gameObject, "Background Image");
        Need(!backgroundImage.raycastTarget, "Background 빈 영역은 전장 입력을 막으면 안 됩니다.");
        Need(Approximately(backgroundImage.color.a, 0f), "Background는 완전히 투명해야 합니다.");
        RectTransform topBorder = FindRect(background, "TopBorder");
        Need(!topBorder.gameObject.activeSelf, "TopBorder는 비활성 상태여야 합니다.");
        Need(!NeedComponent<Image>(topBorder.gameObject, "TopBorder Image").raycastTarget,
            "장식 Border는 전장 입력을 막으면 안 됩니다.");
        Need(scrollbar.handleRect != null && scrollbar.targetGraphic != null, "Scrollbar Handle 연결");

        SerializedObject viewBindings = new SerializedObject(handView);
        NeedReference(viewBindings, "_scrollRect", scrollRect);
        NeedReference(viewBindings, "_viewport", viewport);
        NeedReference(viewBindings, "_dropZone", background);
        NeedReference(viewBindings, "_content", content);
        NeedReference(viewBindings, "_horizontalScrollbar", scrollbar);

        GameObject unitPrefab = RequireAsset<GameObject>(UNIT_PREFAB_PATH);
        GameObject landSlotPrefab = RequireAsset<GameObject>(LAND_SLOT_PREFAB_PATH);
        GameObject relicPrefab = RequireAsset<GameObject>(RELIC_PREFAB_PATH);
        NeedReference(viewBindings, "_unitCardPrefab", unitPrefab);
        NeedReference(viewBindings, "_landSlotCardPrefab", landSlotPrefab);
        NeedReference(viewBindings, "_relicCardPrefab", relicPrefab);
        ValidateCardPrefab(unitPrefab, "Unit");
        ValidateCardPrefab(landSlotPrefab, "LandSlot");
        ValidateCardPrefab(relicPrefab, "Relic");

        NeedFloat(viewBindings, "_cardScale", 0.29f);
        NeedFloat(viewBindings, "_comfortableSpacing", -60f);
        NeedFloat(viewBindings, "_minimumRevealWidth", 56f);
        NeedFloat(viewBindings, "_fanArcHeight", 32f);
        NeedFloat(viewBindings, "_maxFanAngle", 8f);
        NeedFloat(viewBindings, "_hoverScale", 1.44f);
        NeedFloat(viewBindings, "_hoverRise", 60f);
        NeedFloat(viewBindings, "_transitionDuration", 0.15f);
        NeedFloat(viewBindings, "_bottomPadding", 24f);

        float scaledCardHeight = 1100f * ReadFloat(viewBindings, "_cardScale");
        float requiredHeight = ReadFloat(viewBindings, "_bottomPadding") * 2f +
                               ReadFloat(viewBindings, "_hoverRise") +
                               ReadFloat(viewBindings, "_fanArcHeight") +
                               scaledCardHeight * 0.5f * (1f + ReadFloat(viewBindings, "_hoverScale"));
        Need(handPanel.rect.height >= requiredHeight,
            "Hover 카드가 Mask 높이에 잘릴 수 있습니다. 필요 높이=" + requiredHeight);

        ValidateLayoutContracts();
        ValidateViewStateContracts();

        SerializedObject adapterBindings = new SerializedObject(adapter);
        NeedReference(adapterBindings, "_handView", handView);
        Need(RequireProperty(adapterBindings, "_hideLegacyStorageTray").boolValue,
            "기존 UI Toolkit storage-tray 숨김 설정");

        UIUnitCatalogSO unitCatalog = AssetDatabase.LoadAssetAtPath<UIUnitCatalogSO>(UNIT_CATALOG_PATH);
        SerializedProperty unitCatalogProperty = adapterBindings.FindProperty("_unitCatalog");
        string catalogSummary;
        if (unitCatalog == null)
        {
            catalogSummary = "- 선택적 UnitCatalog 에셋 없음: 제목·초상화 보강은 미검증";
            Debug.LogWarning(catalogSummary + " (" + UNIT_CATALOG_PATH + ")");
        }
        else if (unitCatalogProperty == null)
        {
            catalogSummary = "- Adapter에 선택적 _unitCatalog 필드 없음: 카탈로그 연결 건너뜀";
            Debug.LogWarning(catalogSummary);
        }
        else if (unitCatalogProperty.objectReferenceValue != unitCatalog)
        {
            catalogSummary = "- UnitCatalog 연결 누락: 핵심 손패 기능은 가능하지만 제목·초상화 보강 미적용";
            Debug.LogWarning(catalogSummary);
        }
        else
        {
            catalogSummary = "- 선택적 UnitCatalog 제목·초상화 연결 정상";
        }

        var lines = new List<string>
        {
            "전투 카드 손패 Prefab 검증 완료: " + HAND_PREFAB_PATH,
            "- 독립 Screen Space Overlay Canvas / sortingOrder 10 / 호버 비주얼 Canvas 11 계약 정상",
            "- 1920x1080 CanvasScaler와 중앙 하단 1080x540 손패 영역 연결 정상",
            "- 카드 3종, Grid adapter, EventSystem installer 직렬화 연결 정상",
            catalogSummary,
            "- 투명 배경·비활성 TopBorder·Viewport 비차단, 가로 ScrollRect·RectMask2D·Scrollbar 연결 정상",
            "- 카드 scale 0.29 / 간격 -60 / 노출폭 56 / fan 32·8 / hover 1.44·60·0.15 설정 정상",
            "- 0·1·여러 장·초과 카드 배치, 대칭 부채꼴, 호버 회전 복귀 Edit Mode 계약 검증 정상",
            "- Prefab 에셋만 검사했으며 Scene 배치와 Play Mode 동작은 별도 확인이 필요합니다."
        };
        return string.Join("\n", lines);
    }

    private static void ValidateLayoutContracts()
    {
        const float viewportWidth = 900f;
        const float cardWidth = 174f;
        const float comfortableSpacing = -60f;
        const float minimumRevealWidth = 56f;

        BattleCardHandLayout.Result empty = BattleCardHandLayout.Calculate(
            viewportWidth, cardWidth, 0, comfortableSpacing, minimumRevealWidth);
        Need(empty.Positions.Length == 0 && !empty.RequiresScroll, "0장 배치 계약");

        BattleCardHandLayout.Result single = BattleCardHandLayout.Calculate(
            viewportWidth, cardWidth, 1, comfortableSpacing, minimumRevealWidth);
        Need(single.Positions.Length == 1 && Approximately(single.Positions[0], viewportWidth * 0.5f) &&
             !single.RequiresScroll, "1장 중앙 배치 계약");

        BattleCardHandLayout.Result several = BattleCardHandLayout.Calculate(
            viewportWidth, cardWidth, 4, comfortableSpacing, minimumRevealWidth);
        Need(several.Positions.Length == 4 && !several.RequiresScroll,
            "여러 장 Viewport 내부 배치 계약");
        for (int i = 1; i < several.Positions.Length; i++)
            Need(several.Positions[i] > several.Positions[i - 1], "카드 논리 순서는 좌→우여야 합니다.");
        Need(Approximately(several.Step, cardWidth + comfortableSpacing) && several.Step < cardWidth,
            "기본 손패는 좌→우 겹침 간격으로 배치되어야 합니다.");

        BattleCardHandLayout.Result overflow = BattleCardHandLayout.Calculate(
            320f, cardWidth, 8, comfortableSpacing, minimumRevealWidth);
        Need(overflow.RequiresScroll && overflow.ContentWidth > 320f,
            "최소 노출 폭 초과 시 가로 Scroll 계약");
        for (int i = 1; i < overflow.Positions.Length; i++)
            Need(overflow.Positions[i] - overflow.Positions[i - 1] + 0.01f >= minimumRevealWidth,
                "초과 카드 최소 노출 폭 계약");
    }

    private static void ValidateViewStateContracts()
    {
        GameObject instance = PrefabUtility.LoadPrefabContents(HAND_PREFAB_PATH);
        try
        {
            UIBattleCardHandView view = NeedComponent<UIBattleCardHandView>(instance, "검증용 Hand View");
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(instance.GetComponent<RectTransform>());

            view.Clear();
            Need(view.Slots.Count == 0, "View 0장 상태");

            var items = new List<BattleHandCardDisplayData>();
            for (int i = 0; i < 5; i++)
            {
                items.Add(new BattleHandCardDisplayData(
                    "VALIDATION:" + i,
                    i % 2 == 0 ? eBattleHandCardKind.UNIT : eBattleHandCardKind.LAND_SLOT,
                    "검증 카드 " + i,
                    rankText: i.ToString(),
                    footprint: new[] { Vector2Int.zero }));
            }

            view.SetItems(new[] { items[0] });
            Need(view.Slots.Count == 1 && view.Slots[0].Id == items[0].Id, "View 1장 상태");

            view.SetItems(items);
            Need(view.Slots.Count == items.Count, "View 여러 장 상태");
            for (int i = 0; i < view.Slots.Count; i++)
                Need(view.Slots[i].LogicalIndex == i, "View 논리 순서 유지: " + i);
            ValidateFanVisuals(view, "여러 장 배치");

            UIBattleCardHandSlot first = view.Slots[0];
            UIBattleCardHandSlot second = view.Slots[1];
            Canvas handCanvas = NeedComponent<Canvas>(instance, "검증용 Hand Canvas");
            Canvas firstVisualCanvas = GetVisualCanvas(first);
            Canvas secondVisualCanvas = GetVisualCanvas(second);
            float firstBaseRotation = GetVisualRotation(first);
            float secondBaseRotation = GetVisualRotation(second);
            first.OnPointerEnter(null);
            Need(first.IsHovered, "첫 카드 호버 진입");
            Need(Approximately(GetVisualRotation(first), 0f), "호버 카드는 회전이 0이어야 합니다.");
            Need(first.transform.GetSiblingIndex() == 0,
                "호버 중 slotRoot의 논리 sibling 순서가 바뀌면 안 됩니다.");
            Need(firstVisualCanvas.overrideSorting &&
                 firstVisualCanvas.sortingOrder == handCanvas.sortingOrder + 1,
                "호버 비주얼 Canvas만 손패보다 한 단계 앞에 표시되어야 합니다.");
            first.OnPointerExit(null);
            Need(!first.IsHovered && Approximately(GetVisualRotation(first), firstBaseRotation),
                "호버 해제 시 첫 카드가 기본 회전으로 복귀해야 합니다.");
            Need(!firstVisualCanvas.overrideSorting,
                "호버 해제 후 비주얼 Canvas 정렬 상태가 복귀해야 합니다.");
            first.OnPointerEnter(null);
            second.OnPointerEnter(null);
            Need(!first.IsHovered && second.IsHovered, "한 번에 하나의 카드만 호버");
            Need(Approximately(GetVisualRotation(first), firstBaseRotation),
                "다른 카드 호버 시 이전 카드가 기본 회전으로 복귀해야 합니다.");
            Need(Approximately(GetVisualRotation(second), 0f) && !Approximately(secondBaseRotation, 0f),
                "현재 호버 카드만 회전이 0이어야 합니다.");
            Need(first.transform.GetSiblingIndex() == 0 && second.transform.GetSiblingIndex() == 1,
                "호버 전환 중 slotRoot의 논리 sibling 순서가 유지되어야 합니다.");
            Need(!firstVisualCanvas.overrideSorting && secondVisualCanvas.overrideSorting &&
                 secondVisualCanvas.sortingOrder == handCanvas.sortingOrder + 1,
                "현재 호버 카드의 비주얼 Canvas만 손패 최전면이어야 합니다.");

            view.SetItems(new[] { items[0], items[2], items[3] });
            Need(view.Slots.Count == 3 && !ContainsSlot(view.Slots, items[1].Id),
                "호버 카드 삭제 시 참조 정리");

            view.Slots[0].OnPointerEnter(null);
            view.ResetTransientInteraction();
            for (int i = 0; i < view.Slots.Count; i++)
            {
                Need(!view.Slots[i].IsHovered && !view.Slots[i].IsDragging,
                    "비활성/포커스 상실용 일시 상태 초기화");
                Need(view.Slots[i].transform.GetSiblingIndex() == i,
                    "초기화 후 논리 표시 순서 복귀");
            }
            ValidateFanVisuals(view, "일시 상태 초기화");

            var overflowItems = new List<BattleHandCardDisplayData>();
            for (int i = 0; i < 40; i++)
                overflowItems.Add(new BattleHandCardDisplayData(
                    "OVERFLOW:" + i,
                    eBattleHandCardKind.UNIT,
                    "초과 카드 " + i));
            view.SetItems(overflowItems);
            ScrollRect scrollRect = NeedComponent<ScrollRect>(instance, "검증용 ScrollRect");
            Need(view.Slots.Count == overflowItems.Count && scrollRect.horizontal,
                "초과 카드 생성 및 가로 Scroll 활성화");

            view.Clear();
            Need(view.Slots.Count == 0, "닫기 전 카드 정리");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    private static void ValidateFanVisuals(UIBattleCardHandView view, string context)
    {
        Need(view.Slots.Count >= 3 && view.Slots.Count % 2 == 1,
            context + " 검증에는 3장 이상의 홀수 카드가 필요합니다.");

        int centerIndex = view.Slots.Count / 2;
        int lastIndex = view.Slots.Count - 1;
        RectTransform center = NeedSlotRect(view.Slots[centerIndex], context + " 중앙 카드");
        RectTransform leftEdge = NeedSlotRect(view.Slots[0], context + " 왼쪽 끝 카드");
        RectTransform rightEdge = NeedSlotRect(view.Slots[lastIndex], context + " 오른쪽 끝 카드");

        Need(Approximately(center.anchoredPosition.y - leftEdge.anchoredPosition.y, 32f),
            context + " 중앙 카드 상승 높이는 32여야 합니다.");
        Need(Approximately(leftEdge.anchoredPosition.y, rightEdge.anchoredPosition.y),
            context + " 양 끝 카드 높이가 대칭이어야 합니다.");
        Need(Approximately(GetVisualRotation(view.Slots[0]), 8f),
            context + " 왼쪽 끝 카드 회전은 +8도여야 합니다.");
        Need(Approximately(GetVisualRotation(view.Slots[centerIndex]), 0f),
            context + " 중앙 카드 회전은 0도여야 합니다.");
        Need(Approximately(GetVisualRotation(view.Slots[lastIndex]), -8f),
            context + " 오른쪽 끝 카드 회전은 -8도여야 합니다.");

        for (int i = 0; i < centerIndex; i++)
        {
            int opposite = lastIndex - i;
            RectTransform left = NeedSlotRect(view.Slots[i], context + " 왼쪽 카드 " + i);
            RectTransform right = NeedSlotRect(view.Slots[opposite], context + " 오른쪽 카드 " + opposite);
            Need(Approximately(center.anchoredPosition.x - left.anchoredPosition.x,
                    right.anchoredPosition.x - center.anchoredPosition.x),
                context + " 카드 X 간격이 좌우 대칭이 아닙니다: " + i);
            Need(Approximately(left.anchoredPosition.y, right.anchoredPosition.y),
                context + " 카드 Y 위치가 좌우 대칭이 아닙니다: " + i);
            Need(Approximately(GetVisualRotation(view.Slots[i]), -GetVisualRotation(view.Slots[opposite])),
                context + " 카드 회전이 좌우 대칭이 아닙니다: " + i);
        }
    }

    private static RectTransform NeedSlotRect(UIBattleCardHandSlot slot, string label)
    {
        RectTransform rect = slot != null ? slot.transform as RectTransform : null;
        Need(rect != null, label + " RectTransform이 없습니다.");
        return rect;
    }

    private static float GetVisualRotation(UIBattleCardHandSlot slot)
    {
        Transform visual = slot != null ? slot.transform.Find("VisualRoot") : null;
        Need(visual != null, "카드 슬롯의 VisualRoot를 찾을 수 없습니다.");
        return Mathf.DeltaAngle(0f, visual.localEulerAngles.z);
    }

    private static Canvas GetVisualCanvas(UIBattleCardHandSlot slot)
    {
        Transform visual = slot != null ? slot.transform.Find("VisualRoot") : null;
        Canvas canvas = visual != null ? visual.GetComponent<Canvas>() : null;
        Need(canvas != null, "카드 슬롯의 VisualRoot Canvas를 찾을 수 없습니다.");
        return canvas;
    }

    private static bool ContainsSlot(IReadOnlyList<UIBattleCardHandSlot> slots, string id)
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null && slots[i].Id == id) return true;
        return false;
    }

    private static void ValidateCardPrefab(GameObject prefab, string label)
    {
        Need(prefab.GetComponent<UIBattlePreparationCardView>() != null,
            label + " 카드에 UIBattlePreparationCardView가 없습니다.");
        RectTransform rect = NeedComponent<RectTransform>(prefab, label + " card RectTransform");
        Need(Approximately(rect.sizeDelta.x, 600f) && Approximately(rect.sizeDelta.y, 1100f),
            label + " 카드 원본 크기는 600x1100이어야 합니다.");
    }

    private static T RequireAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException(typeof(T).Name + " 에셋을 찾을 수 없습니다: " + path);
        return asset;
    }

    private static T NeedComponent<T>(GameObject target, string label) where T : Component
    {
        T component = target.GetComponent<T>();
        Need(component != null, label + " 컴포넌트가 없습니다: " + target.name);
        return component;
    }

    private static RectTransform FindRect(Transform parent, string path)
    {
        Transform target = parent.Find(path);
        Need(target != null, "손패 자식을 찾을 수 없습니다: " + path);
        RectTransform rect = target as RectTransform;
        Need(rect != null, "RectTransform이 아닙니다: " + path);
        return rect;
    }

    private static void NeedReference(SerializedObject bindings, string fieldName, UnityEngine.Object expected)
    {
        UnityEngine.Object actual = RequireProperty(bindings, fieldName).objectReferenceValue;
        Need(actual == expected,
            bindings.targetObject.GetType().Name + "." + fieldName + " 연결이 올바르지 않습니다.");
    }

    private static void NeedFloat(SerializedObject bindings, string fieldName, float expected)
    {
        float actual = ReadFloat(bindings, fieldName);
        Need(Approximately(actual, expected),
            bindings.targetObject.GetType().Name + "." + fieldName + " 값이 다릅니다. actual=" + actual);
    }

    private static float ReadFloat(SerializedObject bindings, string fieldName)
        => RequireProperty(bindings, fieldName).floatValue;

    private static SerializedProperty RequireProperty(SerializedObject bindings, string fieldName)
        => bindings.FindProperty(fieldName)
           ?? throw new InvalidOperationException(
               bindings.targetObject.GetType().Name + "에 직렬화 필드가 없습니다: " + fieldName);

    private static string GetPath(Transform target, Transform root)
    {
        string path = target.name;
        while (target != root && target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }

    private static bool Approximately(float left, float right)
        => Mathf.Abs(left - right) <= 0.01f;

    private static void Need(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
