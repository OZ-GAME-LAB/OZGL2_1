using System;
using System.IO;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 로비 MenuPopup과 SettingsPopup의 시각 계층을 독립 전투 메뉴 Prefab으로 추출한다.
/// 로비 원본 Prefab은 읽기만 하며 Scene/Prefab YAML은 직접 수정하지 않는다.
/// </summary>
public static class BattleMenuPopupIntegrationBuilder
{
    public const string SOURCE_PREFAB =
        "Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab";
    public const string TARGET_ROOT = "Assets/06.UI/BattleMutedPreview/Menu_v1";
    public const string TARGET_PREFAB = TARGET_ROOT + "/Prefabs/Popup_BattleMenu.prefab";
    public const string OLD_TARGET_ROOT = "Assets/06.UI/BattleMutedPreview/Settings_v1";
    public const string BATTLE_SCENE = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    public const string POPUP_ROOT_PATH = "Canvas_Popups";
    public const string ORIGINAL_POPUP_PATH = "Canvas_Popups/Popup_Settings";
    public const string SETTINGS_ONLY_POPUP_NAME = "Popup_Settings_Lobby";
    public const string NEW_POPUP_NAME = "Popup_Menu_Lobby";
    public const string SETTINGS_BUTTON_PATH = "UI_BattleScreens/Canvas_GetReady/SettingsButton";
    public const string POPUP_CONTROLLER_PATH = "UI_Root";

    [MenuItem("Tools/OZGL2/Battle/Integrate Lobby Menu Popup")]
    public static void Integrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("컴파일과 임포트가 끝난 Edit Mode에서 실행해 주세요.");

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != BATTLE_SCENE)
            throw new InvalidOperationException("UI_Battle_MutedPreview Scene을 활성화한 뒤 실행해 주세요.");

        Transform popupRoot = RequireSceneTransform(scene, POPUP_ROOT_PATH);
        Transform settingsButtonTransform = RequireSceneTransform(scene, SETTINGS_BUTTON_PATH);
        Transform controllerTransform = RequireSceneTransform(scene, POPUP_CONTROLLER_PATH);
        Button settingsButton = settingsButtonTransform.GetComponent<Button>();
        UIPopupController popupController = controllerTransform.GetComponent<UIPopupController>();
        if (settingsButton == null) throw new InvalidOperationException("SettingsButton의 Button 컴포넌트가 없습니다.");
        if (popupController == null) throw new InvalidOperationException("UI_Root의 UIPopupController가 없습니다.");

        int openListenerCount = CountOpenPopupListeners(settingsButton, popupController);
        if (openListenerCount != 1)
            throw new InvalidOperationException("SettingsButton의 기존 OpenPopup 연결이 정확히 하나여야 합니다. 현재: " + openListenerCount);

        BuildPopupPrefab();
        GameObject popupAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TARGET_PREFAB);
        if (popupAsset == null) throw new InvalidOperationException("생성한 메뉴 Prefab을 불러오지 못했습니다: " + TARGET_PREFAB);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Integrate lobby menu popup into battle preview");
        try
        {
            DestroySceneObject(scene, ORIGINAL_POPUP_PATH);

            Transform settingsOnlyPopup = popupRoot.Find(SETTINGS_ONLY_POPUP_NAME);
            if (settingsOnlyPopup != null) Undo.DestroyObjectImmediate(settingsOnlyPopup.gameObject);

            Transform currentPopup = popupRoot.Find(NEW_POPUP_NAME);
            if (currentPopup != null) Undo.DestroyObjectImmediate(currentPopup.gameObject);

            GameObject instance = PrefabUtility.InstantiatePrefab(popupAsset, scene) as GameObject;
            if (instance == null) throw new InvalidOperationException("메뉴 Prefab Scene 인스턴스 생성에 실패했습니다.");
            Undo.RegisterCreatedObjectUndo(instance, "Create lobby menu popup");
            Undo.SetTransformParent(instance.transform, popupRoot, "Parent lobby menu popup");
            instance.name = NEW_POPUP_NAME;
            ApplyStretch(instance.transform as RectTransform);
            instance.SetActive(false);

            UIPopupPanel panel = instance.GetComponent<UIPopupPanel>();
            if (panel == null) throw new InvalidOperationException("새 메뉴 Prefab에 UIPopupPanel이 없습니다.");

            Undo.RecordObject(settingsButton, "Connect lobby menu popup button");
            for (int index = settingsButton.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
            {
                if (settingsButton.onClick.GetPersistentTarget(index) == popupController &&
                    settingsButton.onClick.GetPersistentMethodName(index) == nameof(UIPopupController.OpenPopup))
                    UnityEventTools.RemovePersistentListener(settingsButton.onClick, index);
            }
            UnityEventTools.AddObjectPersistentListener<UIPopupPanel>(
                settingsButton.onClick,
                popupController.OpenPopup,
                panel);
            EditorUtility.SetDirty(settingsButton);
            PrefabUtility.RecordPrefabInstancePropertyModifications(settingsButton);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("전투 미리보기 Scene 저장에 실패했습니다: " + BATTLE_SCENE);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }

        if (AssetDatabase.IsValidFolder(OLD_TARGET_ROOT) && !AssetDatabase.DeleteAsset(OLD_TARGET_ROOT))
            throw new IOException("이전 설정 직행 Prefab 폴더를 삭제하지 못했습니다: " + OLD_TARGET_ROOT);

        AssetDatabase.SaveAssets();
        Debug.Log(
            "로비 메뉴 전투 미리보기 통합 완료\n" +
            "- 생성 Prefab: " + TARGET_PREFAB + "\n" +
            "- 시작 화면: MenuPopup, 내부 이동: SettingsPopup\n" +
            "- 삭제: " + POPUP_ROOT_PATH + "/" + SETTINGS_ONLY_POPUP_NAME + " 및 " + OLD_TARGET_ROOT + "\n" +
            "- 연결: " + SETTINGS_BUTTON_PATH + " -> " + POPUP_ROOT_PATH + "/" + NEW_POPUP_NAME + "\n" +
            "- 로비 Scene/Canvas_LobbyOverlays 원본은 변경하지 않았습니다. Prefab Asset 생성·저장은 Undo 대상이 아닙니다.");
    }

    private static void BuildPopupPrefab()
    {
        EnsureFolder(TARGET_ROOT);
        EnsureFolder(TARGET_ROOT + "/Prefabs");

        GameObject sourceRoot = PrefabUtility.LoadPrefabContents(SOURCE_PREFAB);
        GameObject popupRoot = null;
        try
        {
            Transform sourceMenu = sourceRoot.transform.Find("MenuPopup");
            Transform sourceSettings = sourceRoot.transform.Find("SettingsPopup");
            if (sourceMenu == null || sourceSettings == null)
                throw new InvalidOperationException("로비 Prefab에 MenuPopup 또는 SettingsPopup이 없습니다.");

            GameObject menu = Object.Instantiate(sourceMenu.gameObject);
            GameObject settings = Object.Instantiate(sourceSettings.gameObject);
            menu.name = "MenuPopup";
            settings.name = "SettingsPopup";
            menu.hideFlags = HideFlags.None;
            settings.hideFlags = HideFlags.None;

            popupRoot = new GameObject(
                NEW_POPUP_NAME,
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UIPopupPanel),
                typeof(UIBattleMenuPopupView));
            SceneManager.MoveGameObjectToScene(popupRoot, menu.scene);
            menu.transform.SetParent(popupRoot.transform, false);
            settings.transform.SetParent(popupRoot.transform, false);

            ApplyStretch(popupRoot.transform as RectTransform);
            ApplyStretch(menu.transform as RectTransform);
            ApplyStretch(settings.transform as RectTransform);

            CanvasGroup group = popupRoot.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            group.ignoreParentGroups = false;

            UIPopupPanel panel = popupRoot.GetComponent<UIPopupPanel>();
            UIBattleMenuPopupView view = popupRoot.GetComponent<UIBattleMenuPopupView>();

            Button resume = RequireComponent<Button>(menu.transform, "Panel/Resume");
            Button openSettings = RequireComponent<Button>(menu.transform, "Panel/Settings");
            Button quit = RequireComponent<Button>(menu.transform, "Panel/Quit");
            Toggle menuBgm = RequireComponent<Toggle>(menu.transform, "Panel/BGMToggle");
            Toggle menuSfx = RequireComponent<Toggle>(menu.transform, "Panel/SFXToggle");
            TMP_Text menuBgmState = RequireComponent<TMP_Text>(menu.transform, "Panel/BGMToggle/State");
            TMP_Text menuSfxState = RequireComponent<TMP_Text>(menu.transform, "Panel/SFXToggle/State");

            Button back = RequireComponent<Button>(settings.transform, "Panel/Back");
            Toggle settingsBgm = RequireComponent<Toggle>(settings.transform, "Panel/BGMToggle");
            Toggle settingsSfx = RequireComponent<Toggle>(settings.transform, "Panel/SFXToggle");
            TMP_Text settingsBgmState = RequireComponent<TMP_Text>(settings.transform, "Panel/BGMToggle/State");
            TMP_Text settingsSfxState = RequireComponent<TMP_Text>(settings.transform, "Panel/SFXToggle/State");

            ClearPersistentListeners(resume);
            ClearPersistentListeners(openSettings);
            ClearPersistentListeners(quit);
            ClearPersistentListeners(back);
            ClearPersistentListeners(menuBgm);
            ClearPersistentListeners(menuSfx);
            ClearPersistentListeners(settingsBgm);
            ClearPersistentListeners(settingsSfx);

            UnityEventTools.AddPersistentListener(resume.onClick, view.Close);
            UnityEventTools.AddPersistentListener(openSettings.onClick, view.OpenSettings);
            UnityEventTools.AddPersistentListener(quit.onClick, view.RequestQuit);
            UnityEventTools.AddPersistentListener(back.onClick, view.OpenMenu);
            UnityEventTools.AddPersistentListener(menuBgm.onValueChanged, view.SetBgmEnabled);
            UnityEventTools.AddPersistentListener(settingsBgm.onValueChanged, view.SetBgmEnabled);
            UnityEventTools.AddPersistentListener(menuSfx.onValueChanged, view.SetSfxEnabled);
            UnityEventTools.AddPersistentListener(settingsSfx.onValueChanged, view.SetSfxEnabled);
            quit.interactable = false;

            var panelProperties = new SerializedObject(panel);
            panelProperties.FindProperty("_canDismiss").boolValue = true;
            panelProperties.FindProperty("_firstSelected").objectReferenceValue = resume;
            panelProperties.ApplyModifiedPropertiesWithoutUndo();

            var viewProperties = new SerializedObject(view);
            viewProperties.FindProperty("_popupPanel").objectReferenceValue = panel;
            viewProperties.FindProperty("_menuPanel").objectReferenceValue = menu;
            viewProperties.FindProperty("_settingsPanel").objectReferenceValue = settings;
            viewProperties.FindProperty("_menuFirst").objectReferenceValue = resume;
            viewProperties.FindProperty("_settingsFirst").objectReferenceValue = settingsBgm;
            AssignArray(viewProperties.FindProperty("_bgmToggles"), menuBgm, settingsBgm);
            AssignArray(viewProperties.FindProperty("_sfxToggles"), menuSfx, settingsSfx);
            AssignArray(viewProperties.FindProperty("_bgmStateLabels"), menuBgmState, settingsBgmState);
            AssignArray(viewProperties.FindProperty("_sfxStateLabels"), menuSfxState, settingsSfxState);
            viewProperties.FindProperty("_isBgmEnabled").boolValue = true;
            viewProperties.FindProperty("_isSfxEnabled").boolValue = true;
            viewProperties.ApplyModifiedPropertiesWithoutUndo();

            menu.SetActive(true);
            settings.SetActive(false);
            popupRoot.SetActive(false);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(popupRoot, TARGET_PREFAB, out bool success);
            if (!success || saved == null) throw new IOException("전투 메뉴 Prefab 저장에 실패했습니다: " + TARGET_PREFAB);
        }
        finally
        {
            if (popupRoot != null) Object.DestroyImmediate(popupRoot);
            PrefabUtility.UnloadPrefabContents(sourceRoot);
        }
    }

    private static void AssignArray(SerializedProperty property, params Object[] values)
    {
        property.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
    }

    private static int CountOpenPopupListeners(Button button, UIPopupController controller)
    {
        int count = 0;
        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            if (button.onClick.GetPersistentTarget(index) == controller &&
                button.onClick.GetPersistentMethodName(index) == nameof(UIPopupController.OpenPopup))
                count++;
        return count;
    }

    private static void ClearPersistentListeners(Toggle toggle)
    {
        for (int index = toggle.onValueChanged.GetPersistentEventCount() - 1; index >= 0; index--)
            UnityEventTools.RemovePersistentListener(toggle.onValueChanged, index);
    }

    private static void ClearPersistentListeners(Button button)
    {
        for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
            UnityEventTools.RemovePersistentListener(button.onClick, index);
    }

    private static T RequireComponent<T>(Transform root, string path) where T : Component
    {
        Transform target = root.Find(path);
        if (target == null) throw new InvalidOperationException("메뉴 자식이 없습니다: " + path);
        T component = target.GetComponent<T>();
        if (component == null) throw new InvalidOperationException(path + "에 " + typeof(T).Name + "이 없습니다.");
        return component;
    }

    private static void ApplyStretch(RectTransform rect)
    {
        if (rect == null) throw new InvalidOperationException("메뉴 루트 RectTransform이 없습니다.");
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void DestroySceneObject(Scene scene, string path)
    {
        Transform target = FindSceneTransform(scene, path);
        if (target != null) Undo.DestroyObjectImmediate(target.gameObject);
    }

    private static Transform RequireSceneTransform(Scene scene, string path)
        => FindSceneTransform(scene, path) ?? throw new InvalidOperationException("Scene 오브젝트가 없습니다: " + path);

    private static Transform FindSceneTransform(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform current = root.transform;
            for (int index = 1; index < parts.Length && current != null; index++)
                current = current.Find(parts[index]);
            return current;
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("잘못된 에셋 폴더 경로: " + path);
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
            throw new IOException("에셋 폴더 생성에 실패했습니다: " + path);
    }
}
