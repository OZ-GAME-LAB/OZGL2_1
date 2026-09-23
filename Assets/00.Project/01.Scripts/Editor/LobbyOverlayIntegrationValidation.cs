using System;
using System.Collections.Generic;
using System.Linq;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 구조 통합의 회귀 검사. 실제 게임 저장과 에셋은 변경하지 않는다.
public static class LobbyOverlayIntegrationValidation
{
    [MenuItem("Tools/OZGL2/Lobby/Validate Lobby Overlay Integration")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!Application.isPlaying || scene.path != LobbyOverlayIntegrationBuilder.SCENE)
            throw new InvalidOperationException("대상 Scene Play Mode에서 모든 Overlay를 닫고 실행하세요.");
        var hosts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UILobbyOverlayView>(true)).ToArray();
        var skills = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UISkillLoadoutPreview>(true)).ToArray();
        if (hosts.Length != 1 || skills.Length != 1) throw new InvalidOperationException("중복/누락된 화면이 있습니다.");
        var host = hosts[0];
        var skill = skills[0];
        var controller = host.GetComponent<UIPopupController>();
        var legacy = GameObject.Find("UI_Root").GetComponent<UIPopupController>();
        if (host.IsOpen || legacy.OpenCount > 0) throw new InvalidOperationException("검사 전에 화면을 닫으세요.");
        var lobby = GameObject.Find("Canvas_Lobby");
        var group = lobby.GetComponent<CanvasGroup>();
        var open = lobby.GetComponentsInChildren<Button>(true).Single(b => b.name == "SkillsButton");
        var back = skill.transform.Find("Back").GetComponent<Button>();
        var menu = host.transform.Find("MenuPopup").gameObject;
        var settings = host.transform.Find("SettingsPopup").gameObject;
        var traits = host.transform.Find("TraitsScreen").gameObject;
        var checks = new List<string>();
        Action<bool, string> check = (ok, reason) => { if (!ok) throw new InvalidOperationException(reason); checks.Add(reason); };
        string saved = PlayerPrefs.GetString("OZGL2.Skill.Equip", "");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyOverlayIntegrationBuilder.OVERLAY_PREFAB);
        try
        {
            check(skill.transform.parent == host.transform, "스킬은 공용 루트의 직계 자식");
            check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(asset.transform.Find("Canvas_SkillSettings").gameObject) == LobbySkillSettingsPreviewBuilder.PREFAB, "원본 스킬 Prefab 중첩 연결");
            check(!GameObject.Find("Canvas_Popups").GetComponentsInChildren<UISkillLoadoutPreview>(true).Any(), "이전 부모에 중복 스킬 없음");
            check(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).Count() == 1, "EventSystem 한 개 유지");
            check(controller != null && controller != legacy, "Overlay 전용 팝업 Controller");
            var so = new SerializedObject(controller);
            check(so.FindProperty("_popupRoot").objectReferenceValue == host.transform, "Popup Root 내부 연결");
            check(so.FindProperty("_screenGroup").objectReferenceValue == group, "Screen Group Scene 연결");
            var hostSo = new SerializedObject(host);
            check(hostSo.FindProperty("_overlayPopupController").objectReferenceValue == controller, "화면 관리자 Controller 연결");
            check(hostSo.FindProperty("_skillsScreen").objectReferenceValue == skill.GetComponent<UIPopupPanel>(), "화면 관리자 스킬 연결");
            check(open.onClick.GetPersistentTarget(0) == host && open.onClick.GetPersistentMethodName(0) == "OpenSkills", "로비 스킬 버튼 연결");
            check(back.onClick.GetPersistentTarget(0) == host && back.onClick.GetPersistentMethodName(0) == "CloseTop", "스킬 뒤로 내부 연결");
            check(!skill.gameObject.activeSelf && !skill.transform.Find("ExitConfirmation").gameObject.activeSelf, "화면/확인창 초기 비활성");
            check(asset.GetComponentsInChildren<UISkillLoadoutPreview>(true).Length == 1, "부모 Prefab에도 스킬 한 개 저장");
            check(new SerializedObject(asset.GetComponent<UIPopupController>()).FindProperty("_screenGroup").objectReferenceValue == null, "부모 Prefab에 외부 Scene 참조 없음");
            open.Select(); open.onClick.Invoke();
            check(skill.gameObject.activeInHierarchy && host.IsOpen && controller.OpenCount == 1, "로비 버튼에서 스킬 열기");
            check(legacy.OpenCount == 0 && !group.interactable, "기존 스택 불변 / 로비 입력 차단");
            check(skill.GetComponent<Canvas>().sortingOrder == 210 && skill.GetComponent<Canvas>().overrideSorting, "스킬 Canvas 정렬 210");
            host.OpenMenu(); host.OpenTraits(); host.OpenSettings(); host.OpenSkills();
            check(!menu.activeSelf && !settings.activeSelf && !traits.activeSelf && controller.OpenCount == 1, "스킬 위 다른 화면/중복 열기 차단");
            back.onClick.Invoke();
            check(!host.IsOpen && !skill.gameObject.activeSelf && group.interactable, "뒤로와 로비 입력 복원");
            check(EventSystem.current.currentSelectedGameObject == open.gameObject, "로비 이전 선택 복원");
            host.OpenMenu(); host.OpenSkills();
            check(menu.activeSelf && !skill.gameObject.activeSelf, "메뉴에서 스킬 중복 열기 차단");
            host.OpenSettings(); check(settings.activeSelf && !menu.activeSelf, "기존 메뉴-설정 이동");
            host.CloseTop(); check(menu.activeSelf && !settings.activeSelf, "기존 설정-메뉴 복귀");
            host.CloseTop(); check(!host.IsOpen && group.interactable && legacy.enabled, "기존 메뉴 닫기/입력 복원");
            host.OpenTraits(); host.OpenSkills();
            check(traits.activeSelf && !skill.gameObject.activeSelf, "특성 열기와 스킬 중복 차단");
            host.CloseTraits(); check(!host.IsOpen && group.interactable, "기존 특성 닫기 복원");
            var oldPanels = GameObject.Find("Canvas_Popups").GetComponentsInChildren<UIPopupPanel>(true).Where(p => p.CanDismiss).ToArray();
            foreach (var oldPanel in oldPanels)
            {
                legacy.OpenPopup(oldPanel); host.OpenSkills();
                check(legacy.IsTopPopup(oldPanel) && !skill.gameObject.activeSelf, "기존 팝업 유지: " + oldPanel.name);
                legacy.CloseTopPopup();
            }
            host.OpenSkills(); host.enabled = false;
            check(controller.OpenCount == 0 && !skill.gameObject.activeSelf && group.interactable, "관리자 비활성 시 스택/입력 정리");
            host.enabled = true;
            host.OpenSkills(); check(controller.OpenCount == 1 && skill.gameObject.activeSelf, "재활성 후 정상 열기");
            back.onClick.Invoke();
            check(saved == PlayerPrefs.GetString("OZGL2.Skill.Equip", ""), "실제 스킬 저장 불변");
        }
        finally
        {
            host.enabled = true;
            if (controller != null) while (controller.OpenCount > 0) controller.CloseConfirmedPopup();
            while (legacy.OpenCount > 0) legacy.CloseConfirmedPopup();
            host.CloseTraits();
            if (settings.activeSelf) host.CloseTop();
            if (menu.activeSelf) host.CloseTop();
        }
        return checks.Count + " integration checks passed\n" + string.Join("\n", checks);
    }
}
