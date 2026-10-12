using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OZGL2.UIBridge;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>시작 라벨의 분리 구성과 진행 데이터를 쓰지 않는 표시 검증을 Editor에서 수행한다.</summary>
public static class LobbyStartButtonLabelTools
{
    private const string BUTTON_PATH = "Canvas_Lobby/StageSelection/CurrentStage/StartButton";
    private const string UNDO_NAME = "Separate Lobby Start Button Labels";
    private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/UI/Lobby Start Button/Apply Separate Labels")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        Need(!EditorApplication.isPlayingOrWillChangePlaymode &&
            scene.path == LobbyInlineRecordBuilder.SCENE_PATH, "Edit Mode의 Builds/Lobby가 필요합니다.");
        GameObject buttonObject = GameObject.Find(BUTTON_PATH);
        Need(buttonObject != null && buttonObject.scene == scene &&
            !PrefabUtility.IsPartOfPrefabInstance(buttonObject), "현재 씬의 독립 StartButton을 확인하세요.");
        var bridges = Object.FindObjectsByType<LobbyDifficultyLock>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Where(c => c.gameObject.scene == scene).ToArray();
        Need(bridges.Length == 1, "LobbyDifficultyLock 연결 대상 한 개를 확인하세요.");
        var legacy = buttonObject.transform.Find("Label");
        Need(legacy != null && buttonObject.transform.Find("UnLockLabel") == null &&
            buttonObject.transform.Find("LockLabel") == null, "기존 Label만 있을 때 한 번 적용하세요.");
        var unlocked = legacy.GetComponent<TMP_Text>();
        Need(unlocked != null && unlocked.text == "전투 준비" && legacy.childCount == 0,
            "기존 전투 준비 TMP Label을 확인하세요.");
        var iconView = buttonObject.GetComponent<UILobbyStartButtonIconView>();
        var iconSelector = iconView == null ? null : new SerializedObject(iconView).FindProperty("_selector");
        var selector = iconSelector == null ? null : iconSelector.objectReferenceValue as UILobbyDifficultySelector;
        Need(selector != null && new SerializedObject(selector).FindProperty("_startButton").objectReferenceValue ==
            buttonObject.GetComponent<Button>(), "난이도 선택기의 시작 버튼 연결을 확인하세요.");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        try
        {
            Undo.RecordObject(legacy.gameObject, UNDO_NAME);
            legacy.name = "UnLockLabel";
            GameObject lockedObject = Object.Instantiate(legacy.gameObject, buttonObject.transform, false);
            Undo.RegisterCreatedObjectUndo(lockedObject, UNDO_NAME);
            lockedObject.name = "LockLabel";
            TMP_Text locked = lockedObject.GetComponent<TMP_Text>();
            // 기존 글자가 쓰던 유효 영역의 중심은 유지하고 잠금 글자의 비대칭 여백만 제거한다.
            RectTransform rect = locked.rectTransform;
            Vector4 margin = unlocked.margin;
            Vector2 size = rect.sizeDelta;
            Vector2 position = rect.anchoredPosition;
            position.x += (margin.x - margin.z) * .5f;
            position.y += (margin.w - margin.y) * .5f;
            size.x -= margin.x + margin.z;
            size.y -= margin.y + margin.w;
            Need(size.x > 0f && size.y > 0f, "잠금 글자의 유효 크기를 확인하세요.");
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            locked.margin = Vector4.zero;
            locked.text = "잠김";
            locked.raycastTarget = false;
            lockedObject.SetActive(false);
            var serialized = new SerializedObject(bridges[0]);
            Undo.RecordObject(bridges[0], UNDO_NAME);
            serialized.FindProperty("_lockLabel").objectReferenceValue = locked;
            serialized.FindProperty("_unLockLabel").objectReferenceValue = unlocked;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bridges[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("StartButton: Label → UnLockLabel 보존, LockLabel 추가 및 TutorialBridge의 두 라벨 연결 완료. 씬 저장은 별도입니다.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }

    [MenuItem("Tools/UI/Lobby Start Button/Validate Separate Labels")]
    public static void ValidateMenu() => Debug.Log(Validate());

    public static string Validate()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 검증하세요.");
        var scene = EditorSceneManager.GetActiveScene();
        GameObject source = GameObject.Find(BUTTON_PATH);
        Need(source != null && source.scene == scene, "현재 Lobby StartButton이 필요합니다.");
        var bridge = Object.FindObjectsByType<LobbyDifficultyLock>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Single(c => c.gameObject.scene == scene);
        var references = new SerializedObject(bridge);
        var sourceLocked = source.transform.Find("LockLabel")?.GetComponent<TMP_Text>();
        var sourceUnlocked = source.transform.Find("UnLockLabel")?.GetComponent<TMP_Text>();
        Need(sourceLocked != null && sourceUnlocked != null && sourceLocked != sourceUnlocked &&
            references.FindProperty("_lockLabel").objectReferenceValue == sourceLocked &&
            references.FindProperty("_unLockLabel").objectReferenceValue == sourceUnlocked,
            "잠금/해금 라벨 직접 연결");
        Need(sourceLocked.text == "잠김" && sourceUnlocked.text == "전투 준비" &&
            !sourceLocked.raycastTarget && !sourceUnlocked.raycastTarget &&
            !sourceLocked.gameObject.activeSelf && sourceUnlocked.gameObject.activeSelf,
            "문구, 입력 비차단 및 기본 표시 상태");
        Need(source.transform.Find("Label") == null, "기존 Label 중복 제거");
        bool wasDirty = scene.isDirty;
        var snapshots = new Dictionary<Component, string>();
        foreach (var c in source.GetComponentsInChildren<Component>(true))
            if (c != null) snapshots.Add(c, EditorJsonUtility.ToJson(c));
        snapshots.Add(bridge, EditorJsonUtility.ToJson(bridge));
        var previewScene = EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        try
        {
            root = new GameObject("StartLabelPreview");
            root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, previewScene);
            var clone = Object.Instantiate(source, root.transform, false);
            var button = clone.GetComponent<Button>();
            var locked = clone.transform.Find("LockLabel").GetComponent<TMP_Text>();
            var unlocked = clone.transform.Find("UnLockLabel").GetComponent<TMP_Text>();
            var tester = root.AddComponent<LobbyDifficultyLock>();
            Set(tester, "_start", button);
            Set(tester, "_lockLabel", locked);
            Set(tester, "_unLockLabel", unlocked);
            string lockedBefore = EditorJsonUtility.ToJson(locked);
            string unlockedBefore = EditorJsonUtility.ToJson(unlocked);
            string lockedRect = EditorJsonUtility.ToJson(locked.rectTransform);
            string unlockedRect = EditorJsonUtility.ToJson(unlocked.rectTransform);
            foreach (bool isLocked in new[] { false, true, true, false, false, true, false })
            {
                Invoke(tester, "ApplyStartButtonState", isLocked);
                Need(locked.gameObject.activeSelf == isLocked && unlocked.gameObject.activeSelf != isLocked &&
                    button.interactable != isLocked, "잠금/해금 배타적 표시와 버튼 상태");
                Need(EditorJsonUtility.ToJson(locked) == lockedBefore &&
                    EditorJsonUtility.ToJson(unlocked) == unlockedBefore &&
                    EditorJsonUtility.ToJson(locked.rectTransform) == lockedRect &&
                    EditorJsonUtility.ToJson(unlocked.rectTransform) == unlockedRect,
                    "전환 시 텍스트·폰트·정렬·여백·위치 보존");
            }
            // 전환 중 LateUpdate는 진행 기록 조회 전에 반환해야 한다. 슬롯은 연결하지 않는다.
            var selector = root.AddComponent<UILobbyDifficultySelector>();
            Set(selector, "_isTransitioning", true);
            Set(tester, "_selector", selector);
            button.interactable = false;
            Invoke(tester, "LateUpdate");
            Need(!button.interactable && !locked.gameObject.activeSelf && unlocked.gameObject.activeSelf,
                "난이도 전환 중 버튼/라벨 보존");
            Set(tester, "_lockLabel", null);
            Set(tester, "_unLockLabel", null);
            Set(tester, "_startLabel", unlocked);
            var legacyAlignment = unlocked.alignment;
            Invoke(tester, "ApplyStartButtonState", true);
            Need(unlocked.text == "잠김" && !button.interactable, "미전환 씬 단일 Label 잠금 호환");
            Invoke(tester, "ApplyStartButtonState", false);
            Need(unlocked.text == "전투 준비" && unlocked.alignment == legacyAlignment &&
                button.interactable, "미전환 씬 단일 Label 해금 복구");
            Set(tester, "_lockLabel", locked);
            Set(tester, "_unLockLabel", null);
            Invoke(tester, "ApplyStartButtonState", true);
            Set(tester, "_start", null);
            Invoke(tester, "ApplyStartButtonState", false);
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
        foreach (var pair in snapshots)
            Need(pair.Key != null && EditorJsonUtility.ToJson(pair.Key) == pair.Value, "원본 UI 직렬화 보존");
        Need(scene.isDirty == wasDirty, "원본 씬 dirty 상태 보존");
        return "PASS: 두 라벨 직접 연결, 기본 표시, 잠금→해금 반복 전환, 문구/정렬/배치 보존, " +
            "전환 중 버튼 비활성 유지, 단일 Label 호환 및 누락 참조 안전 처리. " +
            "임시 Edit Mode 복제본만 사용; 실제 계정/진행 기록/게임 실행 미사용.";
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, PRIVATE_INSTANCE).SetValue(target, value);

    private static void Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, PRIVATE_INSTANCE).Invoke(target, args);

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
