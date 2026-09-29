using System;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleMenuPopupIntegrationValidation
{
    [MenuItem("Tools/OZGL2/Battle/Validate Lobby Menu Popup Integration")]
    public static void ValidateMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        Need(scene.IsValid() && scene.path == BattleMenuPopupIntegrationBuilder.BATTLE_SCENE,
            "UI_Battle_MutedPreview Scene 활성화");

        Transform popupRoot = Find(scene, BattleMenuPopupIntegrationBuilder.POPUP_ROOT_PATH);
        Transform originalPopup = Find(scene, BattleMenuPopupIntegrationBuilder.ORIGINAL_POPUP_PATH);
        Transform settingsOnlyPopup = popupRoot != null
            ? popupRoot.Find(BattleMenuPopupIntegrationBuilder.SETTINGS_ONLY_POPUP_NAME)
            : null;
        Transform menuPopup = popupRoot != null
            ? popupRoot.Find(BattleMenuPopupIntegrationBuilder.NEW_POPUP_NAME)
            : null;
        Transform settingsButtonTransform = Find(scene, BattleMenuPopupIntegrationBuilder.SETTINGS_BUTTON_PATH);
        Transform controllerTransform = Find(scene, BattleMenuPopupIntegrationBuilder.POPUP_CONTROLLER_PATH);

        Need(popupRoot != null, "Canvas_Popups");
        Need(originalPopup == null, "이전 Popup_Settings 제거");
        Need(settingsOnlyPopup == null, "설정 직행 Popup_Settings_Lobby 제거");
        Need(menuPopup != null, "새 Popup_Menu_Lobby");
        Need(!menuPopup.gameObject.activeSelf, "새 메뉴는 시작 시 비활성");
        Need(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(menuPopup.gameObject) ==
             BattleMenuPopupIntegrationBuilder.TARGET_PREFAB,
            "새 메뉴 Prefab 연결");

        CanvasGroup group = menuPopup.GetComponent<CanvasGroup>();
        UIPopupPanel panel = menuPopup.GetComponent<UIPopupPanel>();
        UIBattleMenuPopupView view = menuPopup.GetComponent<UIBattleMenuPopupView>();
        Need(group != null && panel != null && view != null, "메뉴 Runtime 컴포넌트");
        Need(group.interactable && group.blocksRaycasts, "메뉴 CanvasGroup 입력 설정");

        Transform menu = NeedChild(menuPopup, "MenuPopup");
        Transform settings = NeedChild(menuPopup, "SettingsPopup");
        Need(menu.gameObject.activeSelf && !settings.gameObject.activeSelf, "초기 메뉴/설정 활성 상태");

        Button resume = NeedComponent<Button>(menu, "Panel/Resume");
        Button openSettings = NeedComponent<Button>(menu, "Panel/Settings");
        Button quit = NeedComponent<Button>(menu, "Panel/Quit");
        Button back = NeedComponent<Button>(settings, "Panel/Back");
        NeedButtonListener(resume, view, nameof(UIBattleMenuPopupView.Close));
        NeedButtonListener(openSettings, view, nameof(UIBattleMenuPopupView.OpenSettings));
        NeedButtonListener(quit, view, nameof(UIBattleMenuPopupView.RequestQuit));
        Need(!quit.interactable, "게임 종료 미리보기 비활성");
        NeedButtonListener(back, view, nameof(UIBattleMenuPopupView.OpenMenu));

        Toggle menuBgm = NeedComponent<Toggle>(menu, "Panel/BGMToggle");
        Toggle settingsBgm = NeedComponent<Toggle>(settings, "Panel/BGMToggle");
        Toggle menuSfx = NeedComponent<Toggle>(menu, "Panel/SFXToggle");
        Toggle settingsSfx = NeedComponent<Toggle>(settings, "Panel/SFXToggle");
        NeedToggleListener(menuBgm, view, nameof(UIBattleMenuPopupView.SetBgmEnabled));
        NeedToggleListener(settingsBgm, view, nameof(UIBattleMenuPopupView.SetBgmEnabled));
        NeedToggleListener(menuSfx, view, nameof(UIBattleMenuPopupView.SetSfxEnabled));
        NeedToggleListener(settingsSfx, view, nameof(UIBattleMenuPopupView.SetSfxEnabled));

        foreach (TMP_Text text in menuPopup.GetComponentsInChildren<TMP_Text>(true))
            Need(text.font != null, "TMP Font: " + GetPath(menuPopup, text.transform));

        Button settingsButton = settingsButtonTransform != null ? settingsButtonTransform.GetComponent<Button>() : null;
        UIPopupController controller = controllerTransform != null ? controllerTransform.GetComponent<UIPopupController>() : null;
        Need(settingsButton != null && controller != null, "SettingsButton/UIPopupController");

        int openCount = 0;
        bool hasPanelArgument = false;
        for (int index = 0; index < settingsButton.onClick.GetPersistentEventCount(); index++)
        {
            if (settingsButton.onClick.GetPersistentTarget(index) != controller ||
                settingsButton.onClick.GetPersistentMethodName(index) != nameof(UIPopupController.OpenPopup)) continue;
            openCount++;
            var serializedButton = new SerializedObject(settingsButton);
            SerializedProperty calls = serializedButton.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            hasPanelArgument |= calls.GetArrayElementAtIndex(index)
                .FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue == panel;
        }
        Need(openCount == 1, "SettingsButton OpenPopup 연결 수");
        Need(hasPanelArgument, "SettingsButton의 새 메뉴 인자 연결");

        int missingScripts = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        Need(missingScripts == 0, "Missing Script 0개");

        Debug.Log(string.Join("\n", new[]
        {
            "로비 메뉴 전투 미리보기 통합 검증 완료",
            "- 설정 직행 Popup 제거 및 Popup_BattleMenu Prefab 연결 확인",
            "- SettingsButton -> 메뉴 시작 화면 연결 확인",
            "- 메뉴/설정 이동, 돌아가기, 음향 Toggle 동기화 연결 확인",
            "- 게임 종료 비활성 및 TMP Font 확인",
            "- Missing Script 0개"
        }));
    }

    private static void NeedButtonListener(Button button, UnityEngine.Object target, string method)
    {
        Need(button.onClick.GetPersistentEventCount() == 1 &&
             button.onClick.GetPersistentTarget(0) == target &&
             button.onClick.GetPersistentMethodName(0) == method,
            button.name + " -> " + method);
    }

    private static void NeedToggleListener(Toggle toggle, UnityEngine.Object target, string method)
    {
        Need(toggle.onValueChanged.GetPersistentEventCount() == 1 &&
             toggle.onValueChanged.GetPersistentTarget(0) == target &&
             toggle.onValueChanged.GetPersistentMethodName(0) == method,
            toggle.name + " -> " + method);
    }

    private static Transform NeedChild(Transform root, string path)
    {
        Transform target = root.Find(path);
        Need(target != null, "메뉴 자식: " + path);
        return target;
    }

    private static T NeedComponent<T>(Transform root, string path) where T : Component
    {
        Transform target = NeedChild(root, path);
        T component = target.GetComponent<T>();
        Need(component != null, path + "의 " + typeof(T).Name);
        return component;
    }

    private static string GetPath(Transform root, Transform target)
    {
        string path = target.name;
        while (target.parent != null && target.parent != root)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }

    private static Transform Find(Scene scene, string path)
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

    private static void Need(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("[전투 메뉴 검증 실패] " + label);
    }
}
