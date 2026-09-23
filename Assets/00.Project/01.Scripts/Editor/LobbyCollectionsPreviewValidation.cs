using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 실제 화면의 연결과 Play 세션 동작만 검증한다. 스킬 편집은 복제 화면으로 격리한다.
public static class LobbyCollectionsPreviewValidation
{
    private const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private const string ROOT = "Assets/06.UI/LobbyMutedPreview/Collections_v1";
    private const string HOST_PREFAB = "Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab";

    [MenuItem("Tools/OZGL2/Lobby/Validate Collections Preview")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!Application.isPlaying || scene.path != SCENE)
            throw new InvalidOperationException("UI_Lobby_MutedPreview Play Mode에서 모든 Overlay를 닫고 실행하세요.");
        var host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UILobbyOverlayView>(true)).Single();
        var controller = host.GetComponent<UIPopupController>();
        var state = host.GetComponent<UILobbyCollectionState>();
        var codex = host.GetComponentsInChildren<UIUnitCodexView>(true).Single();
        var achievements = host.GetComponentsInChildren<UIAchievementView>(true).Single();
        var skill = host.GetComponentsInChildren<UISkillLoadoutPreview>(true).Single();
        if (host.IsOpen || controller == null || controller.OpenCount != 0 || state == null)
            throw new InvalidOperationException("공유 Controller/State 연결을 확인하고 모든 Overlay를 닫으세요.");

        var units = Read<UIUnitCatalogSO>(codex, "_catalog");
        var goals = achievements.Catalog;
        var skills = Read<UISkillPreviewCatalogSO>(skill, "_catalog");
        if (units == null || goals == null || skills == null) throw new InvalidOperationException("카탈로그 연결이 필요합니다.");
        string unitJson = EditorJsonUtility.ToJson(units);
        string goalJson = EditorJsonUtility.ToJson(goals);
        string skillJson = EditorJsonUtility.ToJson(skills);
        string stateJson = EditorJsonUtility.ToJson(state);
        string savedSkills = string.Join(",", skill.GetEquippedIds());
        string prefs = PlayerPrefs.GetString("OZGL2.Skill.Equip", string.Empty);
        int skillPoints = PlayerPrefs.GetInt("OZGL2.Skill.SP", 0);
        int initialFaction = codex.FactionIndex;
        GameObject selection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var checks = new List<string>();
        Action<bool, string> check = (ok, reason) =>
        {
            if (!ok) throw new InvalidOperationException("Collections 검사 실패 (" + checks.Count + "개 통과): " + reason);
            checks.Add(reason);
        };

        try
        {
            state.ResetPreviewState();
            ValidateStructure(host, state, codex, achievements, skill, check);
            ValidateCodex(host, controller, state, codex, units, check);
            ValidateAchievements(host, controller, state, achievements, goals, check);
            ValidateSkillClone(host, controller, skill, skills, check);
            check(EditorJsonUtility.ToJson(units) == unitJson, "유닛 원본 카탈로그 불변");
            check(EditorJsonUtility.ToJson(goals) == goalJson, "업적 원본 카탈로그 불변");
            check(EditorJsonUtility.ToJson(skills) == skillJson, "스킬 원본 카탈로그 불변");
            check(EditorJsonUtility.ToJson(state) == stateJson, "Inspector 초기 해금/진행 설정 불변");
            check(string.Join(",", skill.GetEquippedIds()) == savedSkills, "원본 스킬 화면 장착 상태 불변");
            check(PlayerPrefs.GetString("OZGL2.Skill.Equip", string.Empty) == prefs && PlayerPrefs.GetInt("OZGL2.Skill.SP", 0) == skillPoints, "실제 PlayerPrefs 장착/SP 불변");
            check(!host.IsOpen && controller.OpenCount == 0, "검사 후 Overlay 스택 비어 있음");
        }
        finally
        {
            while (controller != null && controller.OpenCount > 0) controller.CloseConfirmedPopup();
            Write(codex, "_catalog", units);
            Write(achievements, "_catalog", goals);
            state.ResetPreviewState();
            codex.ShowFaction(initialFaction);
            achievements.Refresh();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selection != null && selection.activeInHierarchy ? selection : null);
        }
        return checks.Count + " collections checks passed\n" + string.Join("\n", checks);
    }

    // 모든 Overlay를 닫고 Game View가 실제로 렌더된 다음 호출한다. 프레임 대기는 호출자가 맡는다.
    public static string ValidateLobbyPointer()
    {
        UILobbyOverlayView host = RequirePointerContext();
        var controller = host.GetComponent<UIPopupController>();
        if (controller == null || host.IsOpen || controller.OpenCount != 0)
            throw new InvalidOperationException("포인터 검사 전 모든 Overlay를 닫고 Game View를 1프레임 이상 렌더하세요.");
        var lobby = GameObject.Find("Canvas_Lobby");
        if (lobby == null) throw new InvalidOperationException("Canvas_Lobby를 찾을 수 없습니다.");
        var group = lobby.GetComponent<CanvasGroup>();
        var button = lobby.GetComponentsInChildren<Button>(true).Single(b => b.name == "ReservedButton");
        var achievements = host.GetComponentsInChildren<UIAchievementView>(true).Single();
        var panel = achievements.GetComponent<UIPopupPanel>();
        var checks = new List<string>();
        Action<bool, string> check = CreatePointerCheck(checks);
        GameObject selection = EventSystem.current.currentSelectedGameObject;
        try
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                ClickThroughRaycast(button, check, "업적 로비 버튼 " + (attempt + 1));
                check(achievements.gameObject.activeInHierarchy && controller.IsTopPopup(panel) && host.IsOpen, "업적 포인터 클릭 " + (attempt + 1) + " 실제 화면 열기");
                check(group != null && !group.interactable, "업적 열린 동안 로비 입력 차단 " + (attempt + 1));
                // 새 업적 화면은 아직 렌더 전일 수 있어 Back 좌표 검사는 여기서 강행하지 않는다.
                host.CloseTop();
                check(!host.IsOpen && controller.OpenCount == 0 && !achievements.gameObject.activeSelf, "업적 공용 뒤로 닫기 " + (attempt + 1));
                check(group.interactable && button.IsInteractable(), "닫은 뒤 업적 버튼 재입력 가능 " + (attempt + 1));
            }
        }
        finally
        {
            while (controller.OpenCount > 0) controller.CloseConfirmedPopup();
            EventSystem.current.SetSelectedGameObject(selection != null && selection.activeInHierarchy ? selection : null);
        }
        return checks.Count + " lobby pointer checks passed\n" + string.Join("\n", checks);
    }

    // 도감을 연 뒤 Game View가 실제로 렌더된 상태에서 호출한다. 검사 후 열린 도감과 원래 진영을 유지한다.
    public static string ValidateCodexPointer()
    {
        UILobbyOverlayView host = RequirePointerContext();
        var controller = host.GetComponent<UIPopupController>();
        var view = host.GetComponentsInChildren<UIUnitCodexView>(true).Single();
        var panel = view.GetComponent<UIPopupPanel>();
        if (controller == null || !view.gameObject.activeInHierarchy || !controller.IsTopPopup(panel) || controller.OpenCount != 1)
            throw new InvalidOperationException("도감만 연 뒤 Game View를 1프레임 이상 렌더하고 포인터 검사를 실행하세요.");
        var tabs = Read<UISkillArtButton[]>(view, "_factionButtons");
        if (tabs == null || tabs.Length != 2 || tabs.Any(b => b == null))
            throw new InvalidOperationException("도감 진영 탭 2개 연결이 필요합니다.");
        int originalFaction = view.FactionIndex;
        GameObject selection = EventSystem.current.currentSelectedGameObject;
        var checks = new List<string>();
        Action<bool, string> check = CreatePointerCheck(checks);
        try
        {
            for (int faction = 0; faction < 2; faction++)
            {
                Transform emblem = view.transform.Find("Faction_" + faction + "/Emblem");
                if (emblem == null) throw new InvalidOperationException("진영 마름모 오브젝트 누락: " + faction);
                var emblemButton = emblem.GetComponent<Button>();
                view.ShowFaction(1 - faction);
                ClickThroughRaycast(emblemButton, check, "진영 " + faction + " 마름모");
                check(view.FactionIndex == faction && tabs[faction].IsChosen && !tabs[1 - faction].IsChosen, "마름모 " + faction + " 클릭은 진영/탭 선택 동시 갱신");
                check(view.VisibleCount == 6, "마름모 " + faction + " 클릭 후 카드 6개 표시");

                view.ShowFaction(1 - faction);
                ClickThroughRaycast(tabs[faction], check, "진영 " + faction + " 텍스트 탭");
                check(view.FactionIndex == faction && tabs[faction].IsChosen && !tabs[1 - faction].IsChosen, "기존 텍스트 탭 " + faction + " 클릭 전환 유지");
                check(view.VisibleCount == 6, "텍스트 탭 " + faction + " 클릭 후 카드 6개 표시");
            }
            var lobby = GameObject.Find("Canvas_Lobby");
            var behind = lobby.GetComponentsInChildren<Button>(true).Single(b => b.name == "ReservedButton");
            RaycastResult hit = GetTopHit(behind, out _);
            GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            check(hit.gameObject.transform.IsChildOf(view.transform), "업적 버튼 좌표는 열린 도감 그래픽이 먼저 수신");
            check(clickHandler != behind.gameObject && !behind.IsInteractable(), "도감 뒤 업적 버튼 포인터 입력 차단");
            check(controller.IsTopPopup(panel) && controller.OpenCount == 1, "포인터 검사 중 팝업 중복 생성 없음");
        }
        finally
        {
            view.ShowFaction(originalFaction);
            EventSystem.current.SetSelectedGameObject(selection != null && selection.activeInHierarchy ? selection : null);
        }
        return checks.Count + " codex pointer checks passed\n" + string.Join("\n", checks);
    }

    private static void ValidateStructure(UILobbyOverlayView host, UILobbyCollectionState state,
        UIUnitCodexView codex, UIAchievementView achievements, UISkillLoadoutPreview skill, Action<bool, string> check)
    {
        check(codex.transform.parent == host.transform && achievements.transform.parent == host.transform, "도감/업적은 공용 루트의 직계 자식");
        check(Read<UILobbyCollectionState>(codex, "_state") == state && Read<UILobbyCollectionState>(achievements, "_state") == state && Read<UILobbyCollectionState>(skill, "_unlockState") == state, "세 화면은 동일 Runtime 상태 참조");
        check(!codex.gameObject.activeSelf && !achievements.gameObject.activeSelf, "도감/업적 초기 비활성");
        var lobby = GameObject.Find("Canvas_Lobby");
        check(lobby != null && lobby.GetComponent<CanvasGroup>() != null, "로비 입력 CanvasGroup 존재");
        var controller = host.GetComponent<UIPopupController>();
        check(Read<Transform>(controller, "_popupRoot") == host.transform, "공용 Controller의 내부 Popup Root");
        check(Read<CanvasGroup>(controller, "_screenGroup") == lobby.GetComponent<CanvasGroup>(), "Controller가 실제 로비 입력 제어");

        foreach (var item in new[] { codex.gameObject, achievements.gameObject })
        {
            check(item.GetComponent<UIPopupPanel>() != null && item.GetComponent<CanvasGroup>() != null, item.name + " 팝업/입력 컴포넌트");
            check(item.GetComponent<GraphicRaycaster>() != null, item.name + " GraphicRaycaster 존재");
            check(item.GetComponentsInChildren<CanvasScaler>(true).Length == 0 && item.GetComponentsInChildren<EventSystem>(true).Length == 0, item.name + " 중복 Scaler/EventSystem 없음");
            var back = item.transform.Find("Back").GetComponent<Button>();
            check(HasCall(back, host, "CloseTop"), item.name + " Back은 부모 CloseTop 연결");
            var scroll = item.GetComponentInChildren<ScrollRect>(true);
            check(scroll != null && scroll.content != null && scroll.viewport != null && scroll.vertical && !scroll.horizontal, item.name + " 세로 스크롤 연결");
            check(scroll.viewport.GetComponent<RectMask2D>() != null, item.name + " 실제 viewport 마스크");
            check(!item.GetComponentsInChildren<Transform>(true).Any(t => t.GetComponents<Component>().Any(c => c == null)), item.name + " Missing Script 없음");
            check(!HasBrokenReferences(item), item.name + " 끊어진 직렬화 참조 없음");
            check(item.GetComponentsInChildren<TMP_Text>(true).All(t => t.font != null), item.name + " TMP 폰트 연결");
        }
        foreach (string buttonName in new[] { "CodexButton", "ReservedButton" })
        {
            var button = lobby.GetComponentsInChildren<Button>(true).Single(b => b.name == buttonName);
            check(button.isActiveAndEnabled && button.interactable && button.IsInteractable(), buttonName + " 실제 클릭 가능한 Button 상태");
            check(button.targetGraphic != null && button.targetGraphic.enabled && button.targetGraphic.raycastTarget, buttonName + " 클릭 그래픽 Raycast 활성");
            check(HasCall(button, host, "OpenOverlayPopup"), buttonName + " 공용 Overlay 진입 연결");
            check(!Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i => button.onClick.GetPersistentTarget(i) is UIPopupController), buttonName + " 구형 팝업 중복 연결 없음");
        }

        for (int faction = 0; faction < 2; faction++)
        {
            Transform emblem = codex.transform.Find("Faction_" + faction + "/Emblem");
            check(emblem != null, "진영 " + faction + " 마름모 존재");
            var image = emblem.GetComponent<Image>();
            var button = emblem.GetComponent<Button>();
            check(image != null && image.enabled && image.raycastTarget, "진영 " + faction + " 마름모 Raycast 활성");
            // 화면은 초기 비활성이므로 isActiveAndEnabled 대신 컴포넌트와 Selectable 설정을 검사한다.
            check(button != null && button.enabled && button.interactable && button.IsInteractable(), "진영 " + faction + " 마름모 Button 입력 설정");
            check(button.targetGraphic == image && HasCall(button, codex, "ShowFaction"), "진영 " + faction + " 마름모 진영 전환 연결");
        }

        var hostAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HOST_PREFAB);
        check(hostAsset != null, "부모 Prefab 존재");
        var assetState = hostAsset.GetComponent<UILobbyCollectionState>();
        check(assetState != null, "부모 Prefab 공유 Runtime 상태 연결");
        foreach (string name in new[] { "Canvas_UnitCodex", "Canvas_Achievements" })
        {
            string sourcePath = ROOT + "/Prefabs/" + name + ".prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var nested = hostAsset.transform.Find(name);
            check(source != null && nested != null, name + " 원본 및 중첩 Prefab 존재");
            check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(nested.gameObject) == sourcePath, name + " 원본 Prefab 연결 유지");
            Object sourceView = name == "Canvas_UnitCodex" ? (Object)source.GetComponent<UIUnitCodexView>() : source.GetComponent<UIAchievementView>();
            Object nestedView = name == "Canvas_UnitCodex" ? (Object)nested.GetComponent<UIUnitCodexView>() : nested.GetComponent<UIAchievementView>();
            check(Read<UILobbyCollectionState>(sourceView, "_state") == null, name + " 원본에 외부 Runtime 참조 없음");
            check(source.transform.Find("Back").GetComponent<Button>().onClick.GetPersistentEventCount() == 0, name + " 원본 Back에 부모 외부 참조 없음");
            check(Read<UILobbyCollectionState>(nestedView, "_state") == assetState, name + " 부모 중첩 override로 Runtime 연결");
        }
    }

    private static void ValidateCodex(UILobbyOverlayView host, UIPopupController controller, UILobbyCollectionState state,
        UIUnitCodexView view, UIUnitCatalogSO catalog, Action<bool, string> check)
    {
        var panel = view.GetComponent<UIPopupPanel>();
        var group = GameObject.Find("Canvas_Lobby").GetComponent<CanvasGroup>();
        var open = GameObject.Find("Canvas_Lobby").GetComponentsInChildren<Button>(true).Single(b => b.name == "CodexButton");
        open.Select(); open.onClick.Invoke();
        check(controller.IsTopPopup(panel) && view.gameObject.activeInHierarchy && host.IsOpen, "로비 도감 버튼은 실제 새 화면 열기");
        check(!group.interactable, "도감 열린 동안 로비 입력 차단");
        check(view.GetComponent<Canvas>().overrideSorting && view.GetComponent<Canvas>().sortingOrder == 210, "도감 정렬 210");
        check(catalog.Entries.Count == 12 && catalog.Entries.Select(e => e.Id).Distinct().Count() == 12, "유닛 12종 고유 ID");
        check(view.CardCount >= 12, "유닛 카드 자동 생성");
        var material = Read<Material>(view, "_silhouetteMaterial");
        check(material != null && material.shader != null && material.shader.name == "OZGL2/UI/Collection Silhouette", "알파 실루엣 공용 Material 연결");
        check(!ShaderUtil.ShaderHasError(material.shader), "실루엣 Shader 컴파일 오류 없음");
        for (int faction = 0; faction < 2; faction++)
        {
            view.ShowFaction(faction);
            check(view.VisibleCount == 6 && view.UnlockedCount == 3, "진영 " + faction + " 6종 중 초기 발견 3종");
            check(Read<UISkillArtButton[]>(view, "_factionButtons")[faction].IsChosen, "진영 " + faction + " 선택 표시");
        }
        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            var entry = catalog.Entries[i];
            view.ShowFaction((int)entry.Faction);
            var card = view.GetCard(i);
            var portrait = Read<Image>(card, "_portrait");
            check(entry.Id.StartsWith("unit.", StringComparison.Ordinal) && entry.Portrait != null, entry.Id + " 기존 유닛 키와 렌더 초상");
            check(card.EntryId == entry.Id && card.gameObject.activeSelf && portrait.sprite == entry.Portrait, entry.Id + " 카드/원본 Sprite 일치");
            check(portrait.enabled && !portrait.raycastTarget, entry.Id + " 초상 표시와 입력 비차단");
            check(card.IsUnlocked == entry.DefaultUnlocked && Read<Image>(card, "_lockIcon").enabled == !entry.DefaultUnlocked, entry.Id + " 해금과 자물쇠 일치");
            check(Read<TMP_Text>(card, "_nameText").text == (entry.DefaultUnlocked ? entry.DisplayName : "미발견"), entry.Id + " 발견 이름 마스킹");
            check(entry.DefaultUnlocked ? portrait.material != material && portrait.color == Color.white : portrait.material == material && portrait.color.r < .5f && portrait.color.g < .5f && portrait.color.b < .5f && portrait.color.a == 1, entry.Id + " 원본 색상 또는 어두운 실루엣");
        }
        int lockedIndex = Enumerable.Range(0, catalog.Entries.Count).First(i => !catalog.Entries[i].DefaultUnlocked);
        var locked = catalog.Entries[lockedIndex];
        view.ShowFaction((int)locked.Faction);
        state.SetUnlocked(locked.Id, true);
        check(view.GetCard(lockedIndex).IsUnlocked && view.UnlockedCount == 4, "Runtime 해금은 즉시 카드/발견 수 갱신");
        check(Read<Image>(view.GetCard(lockedIndex), "_portrait").color == Color.white && !Read<Image>(view.GetCard(lockedIndex), "_lockIcon").enabled, "해금 후 원본 색과 자물쇠 복원");
        host.OpenSkills(); host.OpenMenu();
        check(controller.OpenCount == 1 && controller.IsTopPopup(panel), "도감 위 스킬/메뉴 중복 열기 차단");
        view.transform.Find("Back").GetComponent<Button>().onClick.Invoke();
        check(!host.IsOpen && group.interactable && !view.gameObject.activeSelf, "도감 Back은 입력 복원과 닫기");
        check(EventSystem.current == null || EventSystem.current.currentSelectedGameObject == open.gameObject, "도감 닫은 뒤 원래 버튼 포커스 복원");
        state.SetUnlocked(locked.Id, false);
        host.OpenOverlayPopup(panel);
        check(!view.GetCard(lockedIndex).IsUnlocked && view.UnlockedCount == 3, "비활성 중 재잠금은 재열기 때 반영");

        UIUnitCatalogSO expanded = Object.Instantiate(catalog);
        try
        {
            var so = new SerializedObject(expanded);
            var entries = so.FindProperty("_entries");
            entries.InsertArrayElementAtIndex(entries.arraySize);
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("_id").stringValue = "unit.validation_extra";
            entry.FindPropertyRelative("_displayName").stringValue = "추가 카드 검사";
            entry.FindPropertyRelative("_portrait").objectReferenceValue = catalog.Entries[0].Portrait;
            entry.FindPropertyRelative("_faction").enumValueIndex = 0;
            entry.FindPropertyRelative("_defaultUnlocked").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            Write(view, "_catalog", expanded); view.ShowFaction(0);
            check(view.CardCount >= 13 && view.VisibleCount == 7, "유닛 런타임 카탈로그 확장 시 카드 자동 증가");
            check(view.GetCard(12).EntryId == "unit.validation_extra" && view.GetCard(12).IsUnlocked, "추가 카드도 공통 데이터/해금 규칙 사용");
            Write(view, "_catalog", catalog); view.Refresh();
            check(view.VisibleCount == 6 && !view.GetCard(12).gameObject.activeSelf, "카탈로그 축소 시 여분 pool 카드 숨김");
        }
        finally
        {
            Write(view, "_catalog", catalog); view.Refresh(); Object.DestroyImmediate(expanded);
        }
        host.CloseTop();
        check(!host.IsOpen && controller.OpenCount == 0 && group.interactable, "도감 공용 CloseTop 정상 종료");
        state.ResetPreviewState();
    }

    private static void ValidateAchievements(UILobbyOverlayView host, UIPopupController controller, UILobbyCollectionState state,
        UIAchievementView view, UIAchievementCatalogSO catalog, Action<bool, string> check)
    {
        var panel = view.GetComponent<UIPopupPanel>();
        var open = GameObject.Find("Canvas_Lobby").GetComponentsInChildren<Button>(true).Single(b => b.name == "ReservedButton");
        open.onClick.Invoke();
        check(controller.IsTopPopup(panel) && view.gameObject.activeInHierarchy, "업적 로비 버튼은 새 화면 열기");
        check(view.TotalCount == 9 && view.CardCount >= 9 && view.CompletedCount == 2, "업적 9개 중 초기 달성 2개");
        check(Read<TMP_Text>(view, "_summaryText").text == "달성 2 / 9", "업적 완료 수 표시");
        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            var entry = catalog.Entries[i];
            var card = view.GetCard(i);
            int expected = Mathf.Clamp(state.GetAchievementProgress(entry.Id), 0, entry.Target);
            check(card.EntryId == entry.Id && card.Target == entry.Target && entry.Icon != null, entry.Id + " 업적 고정 정보 연결");
            check(card.CurrentProgress == expected && card.IsCompleted == (expected == entry.Target), entry.Id + " 진행도/달성 계산");
            var fill = Read<Image>(card, "_progressFill");
            check(fill.type == Image.Type.Filled && Mathf.Approximately(fill.fillAmount, (float)expected / entry.Target), entry.Id + " 진행 막대 비율");
            check(Read<TMP_Text>(card, "_progressText").text == expected + " / " + entry.Target, entry.Id + " 진행도 문구");
        }
        var tested = catalog.Entries[2];
        state.SetAchievementProgress(tested.Id, -10);
        check(state.GetAchievementProgress(tested.Id) == 0 && view.GetCard(2).CurrentProgress == 0 && !view.GetCard(2).IsCompleted, "음수 진행도는 Runtime/카드에서 0으로 제한");
        state.SetAchievementProgress(tested.Id, tested.Target + 100);
        check(state.GetAchievementProgress(tested.Id) == tested.Target + 100 && view.GetCard(2).CurrentProgress == tested.Target, "누적 원값을 보존하고 카드만 목표치까지 제한");
        check(view.CompletedCount == 3 && view.GetCard(2).ProgressRatio == 1, "진행도 변경은 완료 수/막대 즉시 갱신");
        check(Read<TMP_Text>(view.GetCard(2), "_completedText").text == "달성", "완료 상태 문구 갱신");
        host.CloseTop(); state.SetAchievementProgress(tested.Id, 1); host.OpenOverlayPopup(panel);
        check(view.CompletedCount == 2 && view.GetCard(2).CurrentProgress == 1, "닫힌 동안 진행도 변경 후 재열기 반영");
        view.transform.Find("Back").GetComponent<Button>().onClick.Invoke();
        check(!host.IsOpen && controller.OpenCount == 0, "업적 Back은 공용 스택 닫기");
        state.ResetPreviewState();
    }

    private static void ValidateSkillClone(UILobbyOverlayView host, UIPopupController controller,
        UISkillLoadoutPreview original, UISkillPreviewCatalogSO catalog, Action<bool, string> check)
    {
        int skillCount = catalog.Entries.Count;
        GameObject clone = Object.Instantiate(original.gameObject, host.transform, false);
        clone.name = "SkillCollectionValidation_Transient";
        clone.SetActive(false);
        var view = clone.GetComponent<UISkillLoadoutPreview>();
        var state = clone.AddComponent<UILobbyCollectionState>();
        Write(view, "_unlockState", state);
        var panel = clone.GetComponent<UIPopupPanel>();
        int saveEvents = 0;
        Action<IReadOnlyList<string>> saved = ids => saveEvents++;
        view.SaveRequested += saved;
        try
        {
            host.OpenOverlayPopup(panel);
            check(controller.IsTopPopup(panel), "스킬 복제 화면도 공용 Controller 사용");
            check(skillCount > 0 && Enumerable.Range(0, skillCount).All(view.IsSkillUnlocked), "스킬 카탈로그 " + skillCount + "개 기본 해금 유지");
            check(view.EquippedCount == 3 && !view.HasChanges, "스킬 초기 장착 3개/저장 상태 유지");
            var icons = Read<Image[]>(view, "_cardIcons");
            var locks = Read<Image[]>(view, "_cardLockIcons");
            check(icons.Length == skillCount && locks.Length == skillCount && locks.All(i => i != null), skillCount + "개 스킬 잠금 아이콘 연결");
            var save = Read<UISkillArtButton>(view, "_saveButton");
            var equip = Read<UISkillArtButton>(view, "_equipButton");
            var style = Read<UISkillCategoryStyleSO>(view, "_categoryStyle");
            var material = Read<Material>(view, "_lockedIconMaterial");
            check(material != null && style != null && Read<Image>(view, "_detailLockIcon") != null, "스킬 잠금/분류/상세 표시 참조");
            view.SavePreview(); check(saveEvents == 0 && !save.interactable, "변경 없는 저장은 이벤트 없이 비활성 유지");

            string lockedId = view.GetEquippedIds()[0];
            int lockedIndex = Enumerable.Range(0, skillCount).First(i => catalog.Entries[i].Id == lockedId);
            view.SelectSkill(lockedIndex); state.SetUnlocked(lockedId, false);
            check(!view.IsSkillUnlocked(lockedIndex) && view.EquippedCount == 2 && !view.GetEquippedIds().Contains(lockedId), "재잠금 즉시 장착/조회에서 제거");
            check(!view.HasChanges && !save.interactable, "재잠금은 committed/draft 함께 정리");
            check(icons[lockedIndex].material == material && icons[lockedIndex].color == new Color(.22f, .22f, .22f, 1) && locks[lockedIndex].enabled, "스킬 회색 실루엣과 자물쇠 표시");
            check(Read<TMP_Text>(view, "_detailName").text == "미발견" && Read<TMP_Text>(view, "_detailDescription").text == "해금 후 정보 확인 가능", "미발견 상세 이름/설명 마스킹");
            check(Read<TMP_Text>(view, "_effectValue").text == "—" && Read<TMP_Text>(view, "_cooldown").text == "—", "미발견 효과/쿨타임 마스킹");
            check(Read<Image>(view, "_detailLockIcon").enabled && !equip.interactable, "상세 자물쇠와 장착 버튼 차단");
            view.EquipSelected(); check(view.EquippedCount == 2 && !view.HasChanges, "잠긴 스킬 직접 장착 호출 차단");
            view.ShowCategory((int)catalog.Entries[lockedIndex].Category + 1);
            check(Read<UISkillArtButton[]>(view, "_cards")[lockedIndex].gameObject.activeSelf, "미해금 스킬도 해당 분류 목록에 유지");
            view.SelectSkill(lockedIndex); check(view.SelectedSkillId == lockedId, "미해금 스킬 상세 선택 허용");
            state.SetUnlocked(lockedId, true);
            check(icons[lockedIndex].material == style.GetIconMaterial(catalog.Entries[lockedIndex].Category) && icons[lockedIndex].color == Color.white && !locks[lockedIndex].enabled, "해금 후 분류 Material/흰색 본체 복원");
            check(Read<TMP_Text>(view, "_detailName").text == catalog.Entries[lockedIndex].DisplayName && equip.interactable, "해금 후 상세 정보와 장착 버튼 복원");
            view.EquipSelected(); check(view.EquippedCount == 3 && view.HasChanges, "재해금 장착은 명시적 편집으로 기록");
            view.SavePreview(); check(saveEvents == 1 && !view.HasChanges && !save.interactable, "저장 이벤트 1회와 변경 상태 해제");

            view.UnequipSelected();
            check(view.HasChanges && view.EquippedCount == 2 && save.interactable, "장착 해제는 미저장 편집 유지");
            host.CloseTop();
            check(view.IsExitConfirmationOpen && controller.OpenCount == 2, "공용 뒤로는 기존 미저장 확인창 열기");
            check(!panel.GetComponent<CanvasGroup>().interactable, "확인창 뒤 스킬 입력 차단");
            view.EquipSelected(); view.SavePreview();
            check(view.EquippedCount == 2 && saveEvents == 1, "확인창 중 직접 편집/저장 차단");
            view.CancelExit(); check(!view.IsExitConfirmationOpen && view.HasChanges && controller.OpenCount == 1, "취소는 미저장 편집 보존");
            host.CloseTop(); view.ConfirmExitWithoutSaving();
            check(!clone.activeSelf && controller.OpenCount == 0, "미저장 폐기는 확인창/스킬 함께 닫기");
            host.OpenOverlayPopup(panel);
            check(view.EquippedCount == 3 && !view.HasChanges && view.GetEquippedIds().Contains(lockedId), "재열기는 마지막 저장 구성 복원");

            view.SelectSkill(lockedIndex); view.UnequipSelected();
            int other = Enumerable.Range(0, skillCount).First(i => catalog.Entries[i].Id != lockedId && view.GetEquippedIds().Contains(catalog.Entries[i].Id));
            state.SetUnlocked(catalog.Entries[other].Id, false);
            check(view.HasChanges && view.EquippedCount == 1, "편집 도중 다른 스킬 재잠금도 기존 편집 유지");
            view.SavePreview();
            check(!view.HasChanges && !view.GetEquippedIds().Contains(catalog.Entries[other].Id), "저장 결과에 잠긴 스킬 제외");
            host.CloseTop(); state.SetUnlocked(lockedId, false); host.OpenOverlayPopup(panel);
            view.SelectSkill(lockedIndex);
            check(!view.IsSkillUnlocked(lockedIndex) && Read<TMP_Text>(view, "_detailName").text == "미발견", "닫힌 동안 잠금 변경은 재열기 때 반영");
            host.CloseTop();
        }
        finally
        {
            view.SaveRequested -= saved;
            while (controller.OpenCount > 0) controller.CloseConfirmedPopup();
            Object.DestroyImmediate(clone);
        }
    }

    private static UILobbyOverlayView RequirePointerContext()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!Application.isPlaying || scene.path != SCENE)
            throw new InvalidOperationException("UI_Lobby_MutedPreview Play Mode에서 포인터 검사를 실행하세요.");
        if (EventSystem.current == null || !EventSystem.current.isActiveAndEnabled || EventSystem.current.currentInputModule == null)
            throw new InvalidOperationException("활성 EventSystem/Input Module이 필요합니다. Game View를 먼저 1프레임 이상 렌더하세요.");
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UILobbyOverlayView>(true)).Single();
    }

    private static Action<bool, string> CreatePointerCheck(List<string> checks)
    {
        return (ok, reason) =>
        {
            if (!ok) throw new InvalidOperationException("포인터 검사 실패 (" + checks.Count + "개 통과): " + reason);
            checks.Add(reason);
        };
    }

    private static void ClickThroughRaycast(Button expected, Action<bool, string> check, string label)
    {
        check(expected != null && expected.isActiveAndEnabled && expected.IsInteractable(), label + " 실제 Button 입력 가능");
        RaycastResult hit = GetTopHit(expected, out PointerEventData pointer);
        GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
        check(handler == expected.gameObject, label + " 최상위 Raycast의 클릭 처리 대상 일치 (hit: " + hit.gameObject.name + ")");
        pointer.pointerCurrentRaycast = hit;
        pointer.pointerPressRaycast = hit;
        pointer.pointerPress = handler;
        pointer.rawPointerPress = hit.gameObject;
        pointer.eligibleForClick = true;
        pointer.clickCount = 1;
        pointer.clickTime = Time.unscaledTime;
        check(ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerDownHandler), label + " pointer down 처리");
        check(ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerUpHandler), label + " pointer up 처리");
        check(ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler), label + " pointer click 처리");
    }

    private static RaycastResult GetTopHit(Button target, out PointerEventData pointer)
    {
        Graphic graphic = target != null ? target.targetGraphic : null;
        if (graphic == null || !graphic.isActiveAndEnabled || graphic.depth < 0)
            throw new InvalidOperationException("렌더된 클릭 그래픽이 없습니다. 해당 화면을 연 뒤 Game View를 1프레임 이상 렌더하세요: " + (target != null ? target.name : "Button 누락"));
        Canvas canvas = graphic.canvas;
        if (canvas == null) throw new InvalidOperationException("클릭 그래픽 Canvas 누락: " + target.name);
        Canvas rootCanvas = canvas.rootCanvas;
        Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector3 center = graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center);
        Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, center);
        pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            pointerId = -1,
            position = position,
            pressPosition = position
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        if (results.Count == 0)
            throw new InvalidOperationException("클릭 위치의 Raycast 결과가 없습니다. Game View 렌더와 GraphicRaycaster를 확인하세요: " + target.name + " / " + position);
        return results[0];
    }

    private static bool HasCall(Button button, Object target, string method)
    {
        return Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i => button.onClick.GetPersistentTarget(i) == target && button.onClick.GetPersistentMethodName(i) == method);
    }

    private static bool HasBrokenReferences(GameObject root)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) return true;
            var so = new SerializedObject(component);
            var property = so.GetIterator();
            while (property.NextVisible(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    return true;
        }
        return false;
    }

    private static T Read<T>(Object target, string field)
    {
        if (target == null) throw new InvalidOperationException("검증 참조 누락: " + field);
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (info == null) throw new InvalidOperationException("검증 필드 누락: " + target.GetType().Name + "." + field);
        return (T)info.GetValue(target);
    }

    private static void Write(Object target, string field, Object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (info == null) throw new InvalidOperationException("검증 필드 누락: " + field);
        info.SetValue(target, value);
    }
}
