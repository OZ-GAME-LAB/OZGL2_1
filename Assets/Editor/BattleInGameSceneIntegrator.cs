using System;
using System.IO;
using System.Linq;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UI_Battle_MutedPreview의 유효한 UI 루트를 Build InGame 씬 전용 계층으로 옮긴다.
/// 원본 Preview 씬은 읽기 전용으로 열며 저장하지 않고, 전달용 임시 Prefab도 실행 후 삭제한다.
/// </summary>
public static class BattleInGameSceneIntegrator
{
    private const string SOURCE_SCENE_PATH =
        "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    private const string TARGET_SCENE_PATH =
        "Assets/00.Scenes/Builds/InGame.unity";
    private const string TEMP_TRANSFER_PREFAB_PATH =
        "Assets/__BattleInGameUITransfer.prefab";
    private const string LOBBY_SCENE_PATH =
        "Assets/00.Scenes/Builds/Lobby.unity";
    private const string ROOT_NAME = "BattleInGameUI";
    private const string UNDO_NAME = "전투 UI InGame 통합";

    private static readonly string[] SOURCE_ROOT_NAMES =
    {
        "UI_Root",
        "UI_BattleScreens",
        "Canvas_Popups",
        "EventSystem"
    };

    [MenuItem("Tools/OZGL2/Battle/Integrate Preview UI Into Build InGame")]
    public static void Integrate()
    {
        Scene targetScene = SceneManager.GetActiveScene();
        if (targetScene.path != TARGET_SCENE_PATH)
            throw new InvalidOperationException(
                "Build InGame 씬을 연 뒤 실행해야 합니다: " + TARGET_SCENE_PATH);
        if (targetScene.isDirty)
            throw new InvalidOperationException(
                "InGame 씬에 저장되지 않은 변경이 있습니다. 먼저 저장 또는 정리한 뒤 다시 실행하세요.");

        GameObject inGameRoot = targetScene.GetRootGameObjects()
            .FirstOrDefault(root => root.GetComponent<OZGL2.InGame.InGamePrototypeBootstrap>() != null);
        if (inGameRoot == null)
            throw new InvalidOperationException("InGamePrototypeBootstrap 루트를 찾을 수 없습니다.");

        string sourceGuid = AssetDatabase.AssetPathToGUID(SOURCE_SCENE_PATH);
        if (string.IsNullOrEmpty(sourceGuid))
            throw new FileNotFoundException("Preview 씬을 찾을 수 없습니다.", SOURCE_SCENE_PATH);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        try
        {
            GameObject prefabAsset = ExtractTransferPrefab(targetScene);
            if (prefabAsset == null)
                throw new InvalidOperationException("전투 UI 전달용 임시 Prefab 생성에 실패했습니다.");

            GameObject existing = targetScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(item => item.gameObject)
                .FirstOrDefault(item => item.name == ROOT_NAME);
            if (existing != null)
            {
                if (existing.GetComponent<UIInGameBattleBridge>() == null)
                    throw new InvalidOperationException(
                        "동일 이름의 사용자 오브젝트가 있어 자동 교체하지 않았습니다: " + existing.name);
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, targetScene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("통합 전투 UI Prefab 배치에 실패했습니다.");

            Undo.RegisterCreatedObjectUndo(instance, UNDO_NAME);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            // 바깥 전달용 Prefab만 해제한다. 내부의 기존 팀 공용 Prefab 연결은 유지된다.
            PrefabUtility.UnpackPrefabInstance(
                instance,
                PrefabUnpackMode.OutermostRoot,
                InteractionMode.AutomatedAction);

            ConfigureRuntimeConnections(instance, inGameRoot, true);

            ValidateIntegratedInstance(instance);
            EditorSceneManager.MarkSceneDirty(targetScene);
            if (!EditorSceneManager.SaveScene(targetScene))
                throw new IOException("Build InGame 씬 저장에 실패했습니다.");

            Selection.activeGameObject = instance;
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "Build InGame 전투 UI 통합 완료: " +
                "Preview UI 4개 루트를 독립 Scene Root로 배치하고 기존 공용 Prefab 연결을 유지했습니다. " +
                "실제 Grid 손패, 전투 시작, 증강 선택, 승패 결과 연결. " +
                "원본 Preview 씬은 저장하지 않았습니다.",
                instance);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
        finally
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TEMP_TRANSFER_PREFAB_PATH) != null)
                AssetDatabase.DeleteAsset(TEMP_TRANSFER_PREFAB_PATH);
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Integrate Preview UI Into Build InGame", true)]
    private static bool ValidateIntegrateMenu() =>
        !EditorApplication.isPlayingOrWillChangePlaymode &&
        SceneManager.GetActiveScene().path == TARGET_SCENE_PATH;

    [MenuItem("Tools/OZGL2/Battle/Connect Existing Build InGame UI")]
    public static void ConnectExisting()
    {
        Scene targetScene = SceneManager.GetActiveScene();
        if (targetScene.path != TARGET_SCENE_PATH)
            throw new InvalidOperationException("Build InGame 씬을 연 뒤 실행해야 합니다: " + TARGET_SCENE_PATH);

        GameObject inGameRoot = targetScene.GetRootGameObjects()
            .FirstOrDefault(root => root.GetComponent<OZGL2.InGame.InGamePrototypeBootstrap>() != null);
        GameObject uiRoot = targetScene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == ROOT_NAME);
        if (inGameRoot == null || uiRoot == null)
            throw new InvalidOperationException("InGamePrototype 또는 BattleInGameUI Scene Root를 찾을 수 없습니다.");

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("기존 전투 UI Runtime 연결");
        try
        {
            ConfigureRuntimeConnections(uiRoot, inGameRoot, true);
            ValidateIntegratedInstance(uiRoot);
            EditorSceneManager.MarkSceneDirty(targetScene);
            if (!EditorSceneManager.SaveScene(targetScene))
                throw new IOException("Build InGame 씬 저장에 실패했습니다.");
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = uiRoot;
            Debug.Log("기존 Build InGame UI의 손패·웨이브·스킬·팝업 입력 연결을 갱신했습니다.", uiRoot);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Connect Existing Build InGame UI", true)]
    private static bool ValidateConnectExistingMenu() =>
        !EditorApplication.isPlayingOrWillChangePlaymode &&
        SceneManager.GetActiveScene().path == TARGET_SCENE_PATH;

    private static GameObject ExtractTransferPrefab(Scene targetScene)
    {
        Scene sourceScene = default;
        GameObject wrapper = null;
        try
        {
            sourceScene = EditorSceneManager.OpenScene(
                SOURCE_SCENE_PATH,
                OpenSceneMode.Additive);
            EditorSceneManager.SetActiveScene(sourceScene);

            wrapper = new GameObject(ROOT_NAME);
            SceneManager.MoveGameObjectToScene(wrapper, sourceScene);

            foreach (string rootName in SOURCE_ROOT_NAMES)
            {
                GameObject sourceRoot = sourceScene.GetRootGameObjects()
                    .FirstOrDefault(root => root.name == rootName);
                if (sourceRoot == null)
                    throw new InvalidOperationException(
                        "Preview 씬에 필요한 루트가 없습니다: " + rootName);
                sourceRoot.transform.SetParent(wrapper.transform, false);
            }

            ConfigureForInGame(wrapper);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(TEMP_TRANSFER_PREFAB_PATH) != null)
                AssetDatabase.DeleteAsset(TEMP_TRANSFER_PREFAB_PATH);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, TEMP_TRANSFER_PREFAB_PATH);
            if (prefab == null)
                throw new InvalidOperationException("전투 UI 전달용 임시 Prefab 저장에 실패했습니다.");
            return prefab;
        }
        finally
        {
            EditorSceneManager.SetActiveScene(targetScene);
            if (sourceScene.IsValid() && sourceScene.isLoaded)
                EditorSceneManager.CloseScene(sourceScene, true);
        }
    }

    private static void ConfigureForInGame(GameObject wrapper)
    {
        Transform uiRoot = NeedChild(wrapper.transform, "UI_Root");
        Transform screens = NeedChild(wrapper.transform, "UI_BattleScreens");
        Transform popupRoot = NeedChild(wrapper.transform, "Canvas_Popups");
        Transform preparation = NeedChild(screens, "Canvas_Preparation");
        Transform combat = NeedChild(screens, "Canvas_Combat");

        UIPageGroup pageGroup = NeedComponent<UIPageGroup>(screens);
        UIBattleMutedPreviewView hud = NeedComponent<UIBattleMutedPreviewView>(screens);
        UIPopupController popupController = NeedComponent<UIPopupController>(uiRoot);
        UISceneNavigator navigator = NeedComponent<UISceneNavigator>(uiRoot);

        Button startButton = NeedComponent<Button>(NeedChild(preparation, "StartCombatButton"));
        Transform hand = NeedChild(preparation, "BattleCardHand_Preview");
        UIGridStorageHandAdapter handAdapter = NeedComponent<UIGridStorageHandAdapter>(hand);
        UIBattleCardHandPreviewPresenter previewPresenter =
            hand.GetComponent<UIBattleCardHandPreviewPresenter>();
        handAdapter.enabled = true;
        if (previewPresenter != null) previewPresenter.enabled = false;

        RectTransform skillSlots = NeedComponent<RectTransform>(NeedDescendant(combat, "SkillSlots"));
        UICombatSkillSlotView[] sceneSkillSlots = GetSceneSkillSlots(skillSlots);
        if (sceneSkillSlots.Length == 0)
            throw new InvalidOperationException("Canvas_Combat의 실제 스킬 슬롯을 찾을 수 없습니다.");
        ConfigureSkillLayout(skillSlots, false);
        UIInGameSkillBarController skillController = wrapper.GetComponent<UIInGameSkillBarController>();
        if (skillController == null) skillController = wrapper.AddComponent<UIInGameSkillBarController>();
        skillController.Configure(null, skillSlots, sceneSkillSlots, sceneSkillSlots[0].PreviewCatalog);

        Transform previewTriggers = combat.Find("PreviewTriggers");
        if (previewTriggers != null) previewTriggers.gameObject.SetActive(false);

        Transform augmentRoot = NeedChild(popupRoot, "Canvas_AugmentSelection");
        UIAugmentSelectionView augmentView = NeedComponent<UIAugmentSelectionView>(augmentRoot);
        UIPopupPanel augmentPopup = NeedComponent<UIPopupPanel>(augmentRoot);

        Transform victoryRoot = NeedChild(popupRoot, "Canvas_BattleVictory");
        UIBattleResultView victoryView = NeedComponent<UIBattleResultView>(victoryRoot);
        UIPopupPanel victoryPopup = NeedComponent<UIPopupPanel>(victoryRoot);

        Transform defeatRoot = NeedChild(popupRoot, "Canvas_BattleDefeat");
        UIBattleResultView defeatView = NeedComponent<UIBattleResultView>(defeatRoot);
        UIPopupPanel defeatPopup = NeedComponent<UIPopupPanel>(defeatRoot);

        UIInGameBattleBridge bridge = wrapper.AddComponent<UIInGameBattleBridge>();
        bridge.Configure(
            pageGroup,
            preparation.gameObject,
            combat.gameObject,
            hud,
            startButton,
            popupController,
            augmentView,
            augmentPopup,
            victoryView,
            victoryPopup,
            defeatView,
            defeatPopup,
            navigator,
            LOBBY_SCENE_PATH);

        while (startButton.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(startButton.onClick, 0);
        UnityEventTools.AddPersistentListener(startButton.onClick, bridge.RequestBattle);

        RemoveBrokenPopupListeners(wrapper);

        augmentRoot.gameObject.SetActive(false);
        victoryRoot.gameObject.SetActive(false);
        defeatRoot.gameObject.SetActive(false);

        EditorUtility.SetDirty(wrapper);
        EditorUtility.SetDirty(startButton);
        EditorUtility.SetDirty(handAdapter);
        EditorUtility.SetDirty(skillController);
        if (previewPresenter != null) EditorUtility.SetDirty(previewPresenter);
        EditorUtility.SetDirty(bridge);
    }

    private static void ConfigureRuntimeConnections(
        GameObject uiRoot,
        GameObject inGameRoot,
        bool recordUndo)
    {
        var bootstrap = NeedComponent<OZGL2.InGame.InGamePrototypeBootstrap>(inGameRoot.transform);
        var phasePresentation = NeedComponent<OZGL2.InGame.InGamePhasePresentation>(inGameRoot.transform);
        var augmentProvider = NeedComponent<OZGL2.InGame.InGameAugmentRewards>(inGameRoot.transform);
        var gridRunner = inGameRoot.GetComponentInChildren<OZGL2.Grid.Prototype.GridPrototypeRunner>(true);
        if (gridRunner == null)
            throw new InvalidOperationException("InGamePrototype의 GridPrototypeRunner를 찾을 수 없습니다.");
        UnityEngine.UIElements.UIDocument legacyDocument =
            gridRunner.GetComponent<UnityEngine.UIElements.UIDocument>();
        if (legacyDocument == null)
            throw new InvalidOperationException("GridPrototypeRunner의 UIDocument를 찾을 수 없습니다.");

        UIInGameBattleBridge bridge = NeedComponent<UIInGameBattleBridge>(uiRoot.transform);
        UIGridStorageHandAdapter handAdapter = uiRoot.GetComponentInChildren<UIGridStorageHandAdapter>(true);
        if (handAdapter == null)
            throw new InvalidOperationException("BattleInGameUI의 실제 손패 Adapter를 찾을 수 없습니다.");
        Transform combat = NeedDescendant(uiRoot.transform, "Canvas_Combat");
        RectTransform skillSlots = NeedComponent<RectTransform>(NeedDescendant(combat, "SkillSlots"));
        UICombatSkillSlotView[] sceneSkillSlots = GetSceneSkillSlots(skillSlots);
        if (sceneSkillSlots.Length == 0)
            throw new InvalidOperationException("Canvas_Combat의 실제 스킬 슬롯을 찾을 수 없습니다.");

        UIInGameSkillBarController skillController = uiRoot.GetComponent<UIInGameSkillBarController>();
        if (skillController == null)
            skillController = recordUndo
                ? Undo.AddComponent<UIInGameSkillBarController>(uiRoot)
                : uiRoot.AddComponent<UIInGameSkillBarController>();

        if (recordUndo)
        {
            Undo.RecordObject(bridge, UNDO_NAME);
            Undo.RecordObject(handAdapter, UNDO_NAME);
            Undo.RecordObject(skillController, UNDO_NAME);
            Undo.RecordObject(skillSlots, UNDO_NAME);
        }

        ConfigureSkillLayout(skillSlots, recordUndo);
        bridge.ConfigureRuntimeSources(bootstrap, phasePresentation, augmentProvider);
        handAdapter.ConfigureRuntimeSources(bootstrap, phasePresentation, legacyDocument);
        skillController.Configure(bootstrap, skillSlots, sceneSkillSlots, sceneSkillSlots[0].PreviewCatalog);

        EditorUtility.SetDirty(bridge);
        EditorUtility.SetDirty(handAdapter);
        EditorUtility.SetDirty(skillController);
        EditorUtility.SetDirty(skillSlots);
    }

    private static void ConfigureSkillLayout(RectTransform container, bool recordUndo)
    {
        HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = recordUndo
                ? Undo.AddComponent<HorizontalLayoutGroup>(container.gameObject)
                : container.gameObject.AddComponent<HorizontalLayoutGroup>();
        else if (recordUndo)
            Undo.RecordObject(layout, UNDO_NAME);

        layout.childAlignment = TextAnchor.LowerCenter;
        layout.spacing = 19f;
        layout.padding = new RectOffset(0, 0, 0, 56);
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        EditorUtility.SetDirty(layout);
    }

    private static UICombatSkillSlotView[] GetSceneSkillSlots(RectTransform container)
    {
        return container.Cast<Transform>()
            .Where(child => child != null &&
                !child.name.StartsWith("Legacy_", StringComparison.Ordinal))
            .Select(child => child.GetComponentInChildren<UICombatSkillSlotView>(true))
            .Where(view => view != null)
            .Take(5)
            .ToArray();
    }

    /// <summary>
    /// Preview 씬에서 이미 삭제된 팝업을 가리키는 클릭 연결만 전달 대상에서 제거한다.
    /// 원본 Scene/Prefab은 저장하지 않으며 InGame 씬에 배치되는 인스턴스에만 적용된다.
    /// </summary>
    private static void RemoveBrokenPopupListeners(GameObject wrapper)
    {
        foreach (Button button in wrapper.GetComponentsInChildren<Button>(true))
        {
            SerializedObject serializedButton = new SerializedObject(button);
            SerializedProperty calls = serializedButton.FindProperty(
                "m_OnClick.m_PersistentCalls.m_Calls");
            if (calls == null) continue;

            for (int index = calls.arraySize - 1; index >= 0; index--)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(index);
                SerializedProperty method = call.FindPropertyRelative("m_MethodName");
                SerializedProperty argument = call.FindPropertyRelative(
                    "m_Arguments.m_ObjectArgument");

                if (method == null || method.stringValue != "OpenPopup" ||
                    argument == null || argument.objectReferenceValue != null)
                {
                    continue;
                }

                UnityEventTools.RemovePersistentListener(button.onClick, index);
                EditorUtility.SetDirty(button);
            }
        }
    }

    private static void ValidateIntegratedInstance(GameObject instance)
    {
        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(instance) > 0)
            throw new InvalidOperationException("통합 전투 UI에 Missing Script가 있습니다.");

        UIInGameBattleBridge bridge = instance.GetComponent<UIInGameBattleBridge>();
        if (bridge == null)
            throw new InvalidOperationException("UIInGameBattleBridge가 없습니다.");

        UIGridStorageHandAdapter adapter = instance.GetComponentInChildren<UIGridStorageHandAdapter>(true);
        if (adapter == null || !adapter.enabled || !adapter.IsRuntimeConnected)
            throw new InvalidOperationException("실제 Grid 손패 Adapter의 Runtime 연결이 완료되지 않았습니다.");

        UIInGameSkillBarController skillController = instance.GetComponent<UIInGameSkillBarController>();
        if (skillController == null || !skillController.IsConfigured)
            throw new InvalidOperationException("실제 장착 스킬 HUD 연결이 완료되지 않았습니다.");

        UIBattleCardHandPreviewPresenter presenter =
            instance.GetComponentInChildren<UIBattleCardHandPreviewPresenter>(true);
        if (presenter != null && presenter.enabled)
            throw new InvalidOperationException("미리보기 카드 Presenter가 활성화되어 있습니다.");

        int eventSystemCount = UnityEngine.Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None).Count(system => system.gameObject.scene == instance.scene);
        if (eventSystemCount != 1)
            throw new InvalidOperationException(
                "InGame 씬의 EventSystem은 정확히 1개여야 합니다. 현재: " + eventSystemCount);
    }

    private static Transform NeedChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
            throw new InvalidOperationException(parent.name + " 아래에 " + name + " 오브젝트가 없습니다.");
        return child;
    }

    private static T NeedComponent<T>(Component component) where T : Component
    {
        T value = component.GetComponent<T>();
        if (value == null)
            throw new InvalidOperationException(
                component.name + "에 " + typeof(T).Name + " 컴포넌트가 없습니다.");
        return value;
    }

    private static Transform FindDescendant(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDescendant(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform NeedDescendant(Transform parent, string name)
    {
        Transform found = FindDescendant(parent, name);
        if (found == null)
            throw new InvalidOperationException(parent.name + " 아래에서 " + name + " 오브젝트를 찾을 수 없습니다.");
        return found;
    }
}
