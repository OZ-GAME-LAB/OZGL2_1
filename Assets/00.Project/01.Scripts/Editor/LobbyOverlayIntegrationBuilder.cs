using System;
using System.Linq;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 화면 아트는 재생성하지 않고, 중첩 프리팹과 열기/닫기 참조만 통합한다.
public static class LobbyOverlayIntegrationBuilder
{
    public const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    public const string OVERLAY_PREFAB = "Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab";
    private const string UNDO_NAME = "Integrate skill settings into lobby overlays";

    [MenuItem("Tools/OZGL2/Lobby/Integrate Skill Settings Into Lobby Overlays")]
    public static void Integrate()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != SCENE || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Prefab Mode를 닫고 UI_Lobby_MutedPreview 편집 모드에서 실행하세요.");
        var hosts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UILobbyOverlayView>(true)).ToArray();
        var skills = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UISkillLoadoutPreview>(true)).ToArray();
        if (hosts.Length != 1 || skills.Length != 1) throw new InvalidOperationException("Overlay와 스킬 화면이 각각 하나여야 합니다.");
        var host = hosts[0];
        var skill = skills[0];
        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(host.gameObject) != OVERLAY_PREFAB ||
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(skill.gameObject) != LobbySkillSettingsPreviewBuilder.PREFAB)
            throw new InvalidOperationException("승인된 Overlay/스킬 프리팹 인스턴스만 처리합니다.");
        var lobby = scene.GetRootGameObjects().Single(r => r.name == "Canvas_Lobby");
        var lobbyGroup = lobby.GetComponent<CanvasGroup>();
        var button = lobby.GetComponentsInChildren<Button>(true).Single(b => b.name == "SkillsButton");
        var back = skill.transform.Find("Back").GetComponent<Button>();
        var panel = skill.GetComponent<UIPopupPanel>();
        var controller = host.GetComponent<UIPopupController>();
        if (lobbyGroup == null || back == null || panel == null) throw new InvalidOperationException("필수 UI 참조가 없습니다.");
        if (skill.gameObject.activeSelf || host.IsOpen) throw new InvalidOperationException("모든 Overlay를 닫고 실행하세요.");
        if (skill.transform.parent == host.transform && controller != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(OVERLAY_PREFAB).transform.Find("Canvas_SkillSettings") != null)
        {
            ApplyOrder(skill.GetComponent<Canvas>(), 210);
            ApplyOrder(skill.transform.Find("ExitConfirmation").GetComponent<Canvas>(), 211);
            Debug.Log("스킬 세팅은 이미 Canvas_LobbyOverlays의 중첩 프리팹입니다. 중복 생성하지 않습니다.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        if (controller == null)
        {
            controller = Undo.AddComponent<UIPopupController>(host.gameObject);
            Assign(controller, "_popupRoot", host.transform);
            // 외부 Scene 참조를 넣기 전에 새 컴포넌트만 Prefab에 적용한다.
            PrefabUtility.ApplyAddedComponent(controller, OVERLAY_PREFAB, InteractionMode.UserAction);
        }

        Undo.SetTransformParent(skill.transform, host.transform, UNDO_NAME);
        Undo.RecordObject(skill.transform, UNDO_NAME);
        var rect = (RectTransform)skill.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition3D = Vector3.zero; rect.sizeDelta = Vector2.zero;
        rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one;
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        SetOrder(skill.GetComponent<Canvas>(), 210);
        SetOrder(skill.transform.Find("ExitConfirmation").GetComponent<Canvas>(), 211);
        Undo.RecordObject(back, UNDO_NAME);
        for (int i = back.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if (back.onClick.GetPersistentTarget(i) is UIPopupController || back.onClick.GetPersistentTarget(i) == host)
                UnityEventTools.RemovePersistentListener(back.onClick, i);
        UnityEventTools.AddPersistentListener(back.onClick, host.CloseTop);
        PrefabUtility.RecordPrefabInstancePropertyModifications(back);
        PrefabUtility.ApplyAddedGameObject(skill.gameObject, OVERLAY_PREFAB, InteractionMode.UserAction);

        Assign(host, "_overlayPopupController", controller);
        Assign(host, "_skillsScreen", panel);
        var hostProperties = new SerializedObject(host);
        PrefabUtility.ApplyPropertyOverride(hostProperties.FindProperty("_overlayPopupController"), OVERLAY_PREFAB, InteractionMode.UserAction);
        PrefabUtility.ApplyPropertyOverride(hostProperties.FindProperty("_skillsScreen"), OVERLAY_PREFAB, InteractionMode.UserAction);
        Assign(controller, "_screenGroup", lobbyGroup); // Scene 인스턴스에만 외부 참조를 연결한다.

        Undo.RecordObject(button, UNDO_NAME);
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if ((button.onClick.GetPersistentTarget(i) is UIPopupController && button.onClick.GetPersistentMethodName(i) == "OpenPopup") ||
                (button.onClick.GetPersistentTarget(i) == host && button.onClick.GetPersistentMethodName(i) == "OpenSkills"))
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, host.OpenSkills);
        EditorUtility.SetDirty(button);
        if (PrefabUtility.IsPartOfPrefabInstance(button)) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undo);
        Debug.Log("통합 완료: Canvas_LobbyOverlays/Canvas_SkillSettings, 내부 Controller/Back, 로비 SkillsButton. 기존 Scene override와 원본 스킬 프리팹 유지. Scene 저장은 검토 후 별도 실행하세요.");
    }

    private static void Assign(Object target, string field, Object value)
    {
        Undo.RecordObject(target, UNDO_NAME);
        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        if (property == null) throw new InvalidOperationException("필드 없음: " + field);
        property.objectReferenceValue = value;
        so.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static void ApplyOrder(Canvas canvas, int order)
    {
        if (!SetOrder(canvas, order)) return;
        var so = new SerializedObject(canvas);
        PrefabUtility.ApplyPropertyOverride(so.FindProperty("m_OverrideSorting"), OVERLAY_PREFAB, InteractionMode.UserAction);
        PrefabUtility.ApplyPropertyOverride(so.FindProperty("m_SortingOrder"), OVERLAY_PREFAB, InteractionMode.UserAction);
    }

    private static bool SetOrder(Canvas canvas, int order)
    {
        if (canvas == null) throw new InvalidOperationException("화면 Canvas가 없습니다.");
        // 비활성 Canvas는 네이티브 setter가 상태를 보존하지 않으므로 직렬화 설정을 변경한다.
        var so = new SerializedObject(canvas);
        var sorting = so.FindProperty("m_OverrideSorting");
        var sortingOrder = so.FindProperty("m_SortingOrder");
        if (sorting.boolValue && sortingOrder.intValue == order) return false;
        Undo.RecordObject(canvas, UNDO_NAME);
        sorting.boolValue = true;
        sortingOrder.intValue = order;
        so.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
        return true;
    }
}
