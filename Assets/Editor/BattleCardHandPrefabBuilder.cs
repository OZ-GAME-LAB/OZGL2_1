using System;
using System.IO;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 실제 전투 Scene과 김건 팀의 Grid Prefab을 수정하지 않고 독립 손패 Prefab만 생성한다.
public static class BattleCardHandPrefabBuilder
{
    private const string PREFAB_DIRECTORY = "Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs";
    private const string HAND_PREFAB_PATH = PREFAB_DIRECTORY + "/BattleCardHand.prefab";
    private const string UNIT_PREFAB_PATH = PREFAB_DIRECTORY + "/BattleCard_Unit.prefab";
    private const string LAND_SLOT_PREFAB_PATH = PREFAB_DIRECTORY + "/BattleCard_LandSlot.prefab";
    private const string RELIC_PREFAB_PATH = PREFAB_DIRECTORY + "/BattleCard_Relic.prefab";
    private const string UNIT_CATALOG_PATH = "Assets/06.UI/LobbyMutedPreview/Collections_v1/UnitCatalog.asset";

    private const float HAND_WIDTH = 1080f;
    private const float HAND_HEIGHT = 540f;
    private const float CARD_SCALE = 0.29f;
    private const float COMFORTABLE_SPACING = -60f;
    private const float MINIMUM_REVEAL_WIDTH = 56f;
    private const float FAN_ARC_HEIGHT = 32f;
    private const float MAX_FAN_ANGLE = 8f;
    private const float HOVER_SCALE = 1.44f;
    private const float HOVER_RISE = 60f;
    private const float TRANSITION_DURATION = 0.15f;
    private const float BOTTOM_PADDING = 24f;
    private const int HAND_CANVAS_SORTING_ORDER = 10;

    private static readonly Color BACKGROUND_COLOR = new Color(0f, 0f, 0f, 0f);
    private static readonly Color BORDER_COLOR = new Color32(177, 132, 57, 190);
    private static readonly Color SCROLLBAR_BACKGROUND_COLOR = new Color32(18, 12, 14, 236);
    private static readonly Color SCROLLBAR_HANDLE_COLOR = new Color32(194, 151, 70, 255);

    [MenuItem("Tools/OZGL/UI/Battle/Create Or Update Card Hand Prefab")]
    public static void CreateOrUpdateCardHandPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("컴파일과 임포트가 끝난 Edit Mode에서 실행해 주세요.");

        GameObject unitPrefab = RequireCardPrefab(UNIT_PREFAB_PATH);
        GameObject landSlotPrefab = RequireCardPrefab(LAND_SLOT_PREFAB_PATH);
        GameObject relicPrefab = RequireCardPrefab(RELIC_PREFAB_PATH);
        UIUnitCatalogSO unitCatalog = AssetDatabase.LoadAssetAtPath<UIUnitCatalogSO>(UNIT_CATALOG_PATH);
        if (unitCatalog == null)
            Debug.LogWarning("선택적 유닛 표시 카탈로그를 찾지 못했습니다. 카드 제목·초상화 보강 없이 계속합니다: " + UNIT_CATALOG_PATH);
        bool isOverwrite = AssetDatabase.LoadAssetAtPath<GameObject>(HAND_PREFAB_PATH) != null;

        if (isOverwrite && !EditorUtility.DisplayDialog(
                "손패 Prefab 덮어쓰기",
                "기존 BattleCardHand.prefab을 새 구성으로 덮어씁니다.\n\n" +
                "임시 생성 오브젝트는 Undo에 등록되지만 Prefab 에셋 덮어쓰기는 Unity Undo로 되돌릴 수 없습니다. " +
                "필요하면 Git에서 기존 에셋을 복원해 주세요.",
                "덮어쓰기",
                "취소"))
        {
            return;
        }

        EnsureFolder(PREFAB_DIRECTORY);
        Scene activeScene = SceneManager.GetActiveScene();
        bool activeSceneWasDirty = activeScene.isDirty;
        GameObject[] activeRoots = activeScene.isLoaded ? activeScene.GetRootGameObjects() : Array.Empty<GameObject>();
        Scene previewScene = default;
        GameObject savedPrefab = null;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Battle Card Hand Prefab 생성");

        try
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            if (!previewScene.IsValid())
                throw new InvalidOperationException("손패 Prefab용 격리 PreviewScene을 만들지 못했습니다.");

            BuildResult result = BuildHierarchy(previewScene, unitPrefab, landSlotPrefab, relicPrefab, unitCatalog);
            savedPrefab = PrefabUtility.SaveAsPrefabAsset(result.Root, HAND_PREFAB_PATH, out bool success);
            if (!success || savedPrefab == null)
                throw new IOException("손패 Prefab 저장에 실패했습니다: " + HAND_PREFAB_PATH);

            AssetDatabase.SaveAssetIfDirty(savedPrefab);
        }
        finally
        {
            if (previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(previewScene);

            Undo.CollapseUndoOperations(undoGroup);
            VerifyActiveSceneUnchanged(activeScene, activeSceneWasDirty, activeRoots);
        }

        EditorGUIUtility.PingObject(savedPrefab);
        Debug.Log(
            "전투 카드 손패 Prefab 생성 완료: " + HAND_PREFAB_PATH + "\n" +
            "기존 카드 Prefab은 참조만 재사용했고 Scene, Grid 코어 Prefab, ProjectSettings는 수정하지 않았습니다.\n" +
            "Canvas 정렬 순서는 10이며 Grid UI Toolkit(0)보다 위, 상위 팝업(100)보다 아래입니다.\n" +
            "임시 PreviewScene 계층 생성은 Undo 등록을 사용하지만 Prefab 에셋 생성·덮어쓰기는 Unity Undo 대상이 아닙니다.");
    }

    [MenuItem("Tools/OZGL/UI/Battle/Apply Card Hand Fan Layout")]
    public static void ApplyCardHandFanLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("컴파일과 임포트가 끝난 Edit Mode에서 실행해 주세요.");

        GameObject root = PrefabUtility.LoadPrefabContents(HAND_PREFAB_PATH);
        if (root == null)
            throw new InvalidOperationException("손패 Prefab을 찾을 수 없습니다: " + HAND_PREFAB_PATH);

        try
        {
            RectTransform handPanel = RequireChildRect(root.transform, "HandPanel");
            handPanel.anchorMin = new Vector2(0.5f, 0f);
            handPanel.anchorMax = new Vector2(0.5f, 0f);
            handPanel.pivot = new Vector2(0.5f, 0f);
            handPanel.anchoredPosition = Vector2.zero;
            handPanel.sizeDelta = new Vector2(HAND_WIDTH, HAND_HEIGHT);

            RectTransform background = RequireChildRect(handPanel, "Background");
            Image backgroundImage = background.GetComponent<Image>()
                ?? throw new InvalidOperationException("Background Image가 없습니다.");
            backgroundImage.color = BACKGROUND_COLOR;
            backgroundImage.raycastTarget = false;

            RectTransform topBorder = RequireChildRect(background, "TopBorder");
            topBorder.gameObject.SetActive(false);

            RectTransform viewport = RequireChildRect(handPanel, "Viewport");
            SetFullStretch(viewport, 0f, 0f, 0f, 0f);

            UIBattleCardHandView handView = root.GetComponent<UIBattleCardHandView>()
                ?? throw new InvalidOperationException("UIBattleCardHandView가 없습니다.");
            SerializedObject viewBindings = new SerializedObject(handView);
            SetFloat(viewBindings, "_cardScale", CARD_SCALE);
            SetFloat(viewBindings, "_comfortableSpacing", COMFORTABLE_SPACING);
            SetFloat(viewBindings, "_minimumRevealWidth", MINIMUM_REVEAL_WIDTH);
            SetFloat(viewBindings, "_fanArcHeight", FAN_ARC_HEIGHT);
            SetFloat(viewBindings, "_maxFanAngle", MAX_FAN_ANGLE);
            SetFloat(viewBindings, "_hoverScale", HOVER_SCALE);
            SetFloat(viewBindings, "_hoverRise", HOVER_RISE);
            SetFloat(viewBindings, "_transitionDuration", TRANSITION_DURATION);
            SetFloat(viewBindings, "_bottomPadding", BOTTOM_PADDING);
            viewBindings.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, HAND_PREFAB_PATH, out bool success);
            if (!success || saved == null)
                throw new IOException("손패 Prefab 부채꼴 설정 저장에 실패했습니다: " + HAND_PREFAB_PATH);

            AssetDatabase.SaveAssetIfDirty(saved);
            EditorGUIUtility.PingObject(saved);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log(
            "전투 카드 손패 부채꼴 설정 적용 완료: " + HAND_PREFAB_PATH + "\n" +
            "1080x540 하단 중앙 영역 / 배경·상단 테두리 비표시 / 카드 겹침 60 / 곡률 32 / 회전 ±8도 / 호버 1.44배\n" +
            "Prefab 에셋 저장은 Unity Undo 대상이 아니므로 필요하면 Git에서 복원해 주세요.");
    }

    private static BuildResult BuildHierarchy(
        Scene scene,
        GameObject unitPrefab,
        GameObject landSlotPrefab,
        GameObject relicPrefab,
        UIUnitCatalogSO unitCatalog)
    {
        RectTransform root = CreateRect(scene, null, "BattleCardHand");
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(1920f, 1080f);

        Canvas canvas = AddComponent<Canvas>(root.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.overrideSorting = false;
        canvas.sortingOrder = HAND_CANVAS_SORTING_ORDER;

        CanvasScaler scaler = AddComponent<CanvasScaler>(root.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        GraphicRaycaster raycaster = AddComponent<GraphicRaycaster>(root.gameObject);
        raycaster.ignoreReversedGraphics = true;
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

        CanvasGroup canvasGroup = AddComponent<CanvasGroup>(root.gameObject);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.ignoreParentGroups = false;

        AddComponent<BattleHandEventSystemInstaller>(root.gameObject);
        UIGridStorageHandAdapter adapter = AddComponent<UIGridStorageHandAdapter>(root.gameObject);
        ScrollRect scrollRect = AddComponent<ScrollRect>(root.gameObject);
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 60f;
        scrollRect.elasticity = 0.1f;

        RectTransform handPanel = CreateRect(scene, root, "HandPanel");
        handPanel.anchorMin = new Vector2(0.5f, 0f);
        handPanel.anchorMax = new Vector2(0.5f, 0f);
        handPanel.pivot = new Vector2(0.5f, 0f);
        handPanel.anchoredPosition = Vector2.zero;
        handPanel.sizeDelta = new Vector2(HAND_WIDTH, HAND_HEIGHT);

        RectTransform background = CreateRect(scene, handPanel, "Background");
        SetBottomStretch(background, 0f, 0f, 0f, 350f);
        Image backgroundImage = AddComponent<Image>(background.gameObject);
        backgroundImage.color = BACKGROUND_COLOR;
        backgroundImage.raycastTarget = false;

        RectTransform topBorder = CreateRect(scene, background, "TopBorder");
        topBorder.anchorMin = new Vector2(0f, 1f);
        topBorder.anchorMax = new Vector2(1f, 1f);
        topBorder.pivot = new Vector2(0.5f, 1f);
        topBorder.anchoredPosition = Vector2.zero;
        topBorder.sizeDelta = new Vector2(0f, 2f);
        Image topBorderImage = AddComponent<Image>(topBorder.gameObject);
        topBorderImage.color = BORDER_COLOR;
        topBorderImage.raycastTarget = false;
        topBorder.gameObject.SetActive(false);

        RectTransform viewport = CreateRect(scene, handPanel, "Viewport");
        SetFullStretch(viewport, 0f, 0f, 0f, 0f);
        Image viewportImage = AddComponent<Image>(viewport.gameObject);
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = false;
        RectMask2D viewportMask = AddComponent<RectMask2D>(viewport.gameObject);
        viewportMask.padding = Vector4.zero;
        viewportMask.softness = Vector2Int.zero;

        RectTransform content = CreateRect(scene, viewport, "Content");
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        Scrollbar horizontalScrollbar = BuildScrollbar(scene, handPanel);

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontalScrollbar = horizontalScrollbar;
        scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.horizontalScrollbarSpacing = 4f;

        UIBattleCardHandView handView = AddComponent<UIBattleCardHandView>(root.gameObject);
        SerializedObject viewBindings = new SerializedObject(handView);
        Bind(viewBindings, "_scrollRect", scrollRect);
        Bind(viewBindings, "_viewport", viewport);
        Bind(viewBindings, "_dropZone", background);
        Bind(viewBindings, "_content", content);
        Bind(viewBindings, "_unitCardPrefab", unitPrefab);
        Bind(viewBindings, "_landSlotCardPrefab", landSlotPrefab);
        Bind(viewBindings, "_relicCardPrefab", relicPrefab);
        Bind(viewBindings, "_horizontalScrollbar", horizontalScrollbar);
        SetFloat(viewBindings, "_cardScale", CARD_SCALE);
        SetFloat(viewBindings, "_comfortableSpacing", COMFORTABLE_SPACING);
        SetFloat(viewBindings, "_minimumRevealWidth", MINIMUM_REVEAL_WIDTH);
        SetFloat(viewBindings, "_fanArcHeight", FAN_ARC_HEIGHT);
        SetFloat(viewBindings, "_maxFanAngle", MAX_FAN_ANGLE);
        SetFloat(viewBindings, "_hoverScale", HOVER_SCALE);
        SetFloat(viewBindings, "_hoverRise", HOVER_RISE);
        SetFloat(viewBindings, "_transitionDuration", TRANSITION_DURATION);
        SetFloat(viewBindings, "_bottomPadding", BOTTOM_PADDING);
        viewBindings.ApplyModifiedProperties();

        SerializedObject adapterBindings = new SerializedObject(adapter);
        Bind(adapterBindings, "_handView", handView);
        SerializedProperty unitCatalogProperty = adapterBindings.FindProperty("_unitCatalog");
        if (unitCatalogProperty != null)
            unitCatalogProperty.objectReferenceValue = unitCatalog;
        else
            Debug.LogWarning("UIGridStorageHandAdapter._unitCatalog 필드가 없어 선택적 카탈로그 연결을 건너뜁니다.");
        RequireProperty(adapterBindings, "_hideLegacyStorageTray").boolValue = true;
        adapterBindings.ApplyModifiedProperties();

        return new BuildResult(root.gameObject);
    }

    private static Scrollbar BuildScrollbar(Scene scene, RectTransform parent)
    {
        RectTransform scrollbarRect = CreateRect(scene, parent, "HorizontalScrollbar");
        scrollbarRect.anchorMin = new Vector2(0f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 0f);
        scrollbarRect.pivot = new Vector2(0.5f, 0f);
        scrollbarRect.anchoredPosition = new Vector2(0f, 6f);
        scrollbarRect.sizeDelta = new Vector2(-96f, 14f);

        Image background = AddComponent<Image>(scrollbarRect.gameObject);
        background.color = SCROLLBAR_BACKGROUND_COLOR;
        background.raycastTarget = true;

        Scrollbar scrollbar = AddComponent<Scrollbar>(scrollbarRect.gameObject);
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        scrollbar.numberOfSteps = 0;
        scrollbar.value = 0f;
        scrollbar.size = 0.25f;

        RectTransform slidingArea = CreateRect(scene, scrollbarRect, "SlidingArea");
        SetFullStretch(slidingArea, 2f, 2f, -2f, -2f);

        RectTransform handle = CreateRect(scene, slidingArea, "Handle");
        SetFullStretch(handle, 0f, 0f, 0f, 0f);
        Image handleImage = AddComponent<Image>(handle.gameObject);
        handleImage.color = SCROLLBAR_HANDLE_COLOR;
        handleImage.raycastTarget = true;

        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        return scrollbar;
    }

    private static GameObject RequireCardPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new InvalidOperationException("기존 카드 Prefab을 찾을 수 없습니다: " + path);
        if (prefab.GetComponent<UIBattlePreparationCardView>() == null)
            throw new InvalidOperationException("UIBattlePreparationCardView가 없는 카드 Prefab입니다: " + path);
        return prefab;
    }

    private static RectTransform CreateRect(Scene scene, RectTransform parent, string name)
    {
        GameObject item = EditorUtility.CreateGameObjectWithHideFlags(
            name,
            HideFlags.HideAndDontSave,
            typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(item, scene);
        if (item.scene != scene)
            throw new InvalidOperationException("임시 손패 오브젝트 격리에 실패했습니다: " + name);

        item.hideFlags = HideFlags.None;
        Undo.RegisterCreatedObjectUndo(item, "손패 UI 오브젝트 생성");
        RectTransform rect = item.GetComponent<RectTransform>();
        if (parent != null)
            Undo.SetTransformParent(rect, parent, "손패 UI 부모 연결");
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.localPosition = Vector3.zero;
        return rect;
    }

    private static T AddComponent<T>(GameObject target) where T : Component
    {
        T component = Undo.AddComponent<T>(target);
        if (component == null)
            throw new InvalidOperationException(typeof(T).Name + " 컴포넌트 생성에 실패했습니다: " + target.name);
        return component;
    }

    private static void SetBottomStretch(RectTransform rect, float left, float bottom, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2((left + right) * 0.5f, bottom);
        rect.sizeDelta = new Vector2(-(left - right), height);
    }

    private static void SetFullStretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private static void Bind(SerializedObject bindings, string fieldName, Object reference)
        => RequireProperty(bindings, fieldName).objectReferenceValue = reference;

    private static RectTransform RequireChildRect(Transform parent, string path)
    {
        Transform child = parent != null ? parent.Find(path) : null;
        if (child is RectTransform rect) return rect;
        throw new InvalidOperationException("손패 RectTransform을 찾을 수 없습니다: " + path);
    }

    private static void SetFloat(SerializedObject bindings, string fieldName, float value)
        => RequireProperty(bindings, fieldName).floatValue = value;

    private static SerializedProperty RequireProperty(SerializedObject bindings, string fieldName)
        => bindings.FindProperty(fieldName)
           ?? throw new InvalidOperationException(
               bindings.targetObject.GetType().Name + "에 직렬화 필드가 없습니다: " + fieldName);

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent))
            throw new InvalidOperationException("에셋 폴더 경로가 잘못되었습니다: " + path);

        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
            throw new IOException("에셋 폴더 생성에 실패했습니다: " + path);
    }

    private static void VerifyActiveSceneUnchanged(Scene scene, bool wasDirty, GameObject[] originalRoots)
    {
        if (SceneManager.GetActiveScene() != scene || !scene.IsValid() || scene.isDirty != wasDirty)
            throw new InvalidOperationException("손패 Prefab 생성 중 실제 Scene 상태가 변경되었습니다. 자동 저장하지 않았습니다.");

        GameObject[] currentRoots = scene.isLoaded ? scene.GetRootGameObjects() : Array.Empty<GameObject>();
        if (currentRoots.Length != originalRoots.Length)
            throw new InvalidOperationException("손패 Prefab 생성 중 실제 Scene 루트 구성이 변경되었습니다.");

        for (int index = 0; index < originalRoots.Length; index++)
        {
            if (currentRoots[index] != originalRoots[index])
                throw new InvalidOperationException("손패 Prefab 생성 중 실제 Scene 루트 순서가 변경되었습니다.");
        }
    }

    private sealed class BuildResult
    {
        public readonly GameObject Root;

        public BuildResult(GameObject root)
        {
            Root = root;
        }
    }
}
