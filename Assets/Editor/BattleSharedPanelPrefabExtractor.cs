using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>공통 HUD/적 예고를 중첩 프리팹으로 분리하고 씬의 표시 참조를 보존한다.</summary>
public static class BattleSharedPanelPrefabExtractor
{
    private const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    private const string CANVAS = "Assets/02.Prefabs/UI/UI_Panel/Canvas_GetReady.prefab";
    private const string DESTINATION = "Assets/06.UI/BattleMutedPreview/Prefabs/";
    private const string UNDO = "전투 HUD/적 예고 프리팹 분리";
    private static readonly string[] PANELS = { "BattleHUD", "WavePreview" };
    private static readonly string[] FILES = { "BattleHud.prefab", "WavePreviewPanel.prefab" };

    private sealed class ReferenceBinding
    {
        public MonoBehaviour Owner;
        public string Property;
        public string TargetPath;
        public Type TargetType;
        public int ComponentIndex;
    }

    [MenuItem("Tools/OZGL2/Battle/Extract Shared HUD And Wave Prefabs")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            scene.path != SCENE || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("UI_Battle_MutedPreview의 Edit Mode에서만 실행합니다.");
        if (scene.isDirty)
            throw new InvalidOperationException("미저장 씬 변경을 먼저 보존/저장한 뒤 실행해 주세요.");
        if (FILES.Any(f => File.Exists(DESTINATION + f)))
            throw new InvalidOperationException("대상 프리팹이 이미 존재합니다. 재생성 대신 원본 Prefab을 편집하세요.");

        Transform screens = scene.GetRootGameObjects().Single(g => g.name == "UI_BattleScreens").transform;
        Transform canvas = screens.Find("Canvas_GetReady");
        if (canvas == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(canvas.gameObject) != CANVAS)
            throw new InvalidOperationException("예상한 Canvas_GetReady 프리팹 인스턴스가 아닙니다.");
        foreach (string panel in PANELS)
            if (canvas.Find(panel) == null) throw new InvalidOperationException("대상 누락: " + panel);

        // 씬에만 있는 디자인/추가 오브젝트를 부모 원본에 무조건 적용하지 않는다.
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(CANVAS);
        foreach (var modification in PrefabUtility.GetPropertyModifications(canvas.gameObject) ?? Array.Empty<PropertyModification>())
        {
            Transform target = GetTransform(modification.target);
            if (target != null && IsPanelPath(AnimationUtility.CalculateTransformPath(target, source.transform)))
                throw new InvalidOperationException("패널에 개별 씬 Override가 있습니다. 먼저 해당 편집의 보존 방식을 확인해 주세요.");
        }

        var before = CapturePanelVisuals(canvas);
        var buttons = CaptureAllButtons(scene);
        var bindings = CaptureReferences(scene, canvas);
        if (bindings.Count(b => b.Owner == screens.GetComponent<UIBattleMutedPreviewView>()) != 10)
            throw new InvalidOperationException("HUD View의 필수 표시 참조 10개를 확인하지 못했습니다. 저장 전에 연결을 점검하세요.");
        string backup = "Tools/Art/Backups/BattleSharedPanels_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(SCENE, backup + "/UI_Battle_MutedPreview.unity");
        File.Copy(CANVAS, backup + "/Canvas_GetReady.prefab");

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO);
        foreach (var owner in bindings.Select(b => b.Owner).Distinct()) Undo.RegisterCompleteObjectUndo(owner, UNDO);

        GameObject contents = PrefabUtility.LoadPrefabContents(CANVAS);
        try
        {
            // 씬과 원본에 누락/추가 자식 차이가 있으면 원본 저장 전에 중단한다.
            AssertSame(before, CapturePanelVisuals(contents.transform), "씬/원본 패널 구조");
            for (int i = 0; i < PANELS.Length; i++)
            {
                GameObject panel = contents.transform.Find(PANELS[i]).gameObject;
                bool success;
                PrefabUtility.SaveAsPrefabAssetAndConnect(panel, DESTINATION + FILES[i], InteractionMode.UserAction, out success);
                if (!success) throw new IOException("중첩 프리팹 생성 실패: " + FILES[i] + ". 백업: " + backup);
            }
            bool saved;
            PrefabUtility.SaveAsPrefabAsset(contents, CANVAS, out saved);
            if (!saved) throw new IOException("부모 Canvas 저장 실패. 백업: " + backup);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        // 자식이 중첩 인스턴스가 되면서 fileID가 바뀔 수 있어 기존 경로/타입으로 재연결한다.
        canvas = screens.Find("Canvas_GetReady");
        foreach (ReferenceBinding binding in bindings)
        {
            Transform target = canvas.Find(binding.TargetPath);
            if (target == null || binding.Owner == null) throw new InvalidOperationException("참조 대상 복구 실패: " + binding.TargetPath);
            Object value = binding.TargetType == typeof(GameObject) ? target.gameObject :
                target.GetComponents(binding.TargetType)[binding.ComponentIndex];
            var serialized = new SerializedObject(binding.Owner);
            SerializedProperty property = serialized.FindProperty(binding.Property);
            if (property == null || value == null) throw new InvalidOperationException("참조 필드 복구 실패: " + binding.Property);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            if (PrefabUtility.IsPartOfPrefabInstance(binding.Owner))
                PrefabUtility.RecordPrefabInstancePropertyModifications(binding.Owner);
        }
        AssertSame(before, CapturePanelVisuals(canvas), "분리 후 배치/아트/텍스트");
        AssertSame(buttons, CaptureAllButtons(scene), "기존 버튼 이벤트");
        for (int i = 0; i < PANELS.Length; i++)
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(canvas.Find(PANELS[i]).gameObject) != DESTINATION + FILES[i])
                throw new InvalidOperationException("중첩 프리팹 연결 확인 실패: " + PANELS[i]);
        screens.GetComponent<UIBattleMutedPreviewView>().RefreshView();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장 실패. 백업: " + backup);
        Debug.Log("HUD/적 예고 중첩 프리팹 분리 완료. 보존 참조 " + bindings.Count +
            "개. 씬 참조 변경 Undo 지원; Prefab 파일은 백업 복구 필요: " + backup);
    }

    private static List<ReferenceBinding> CaptureReferences(Scene scene, Transform canvas)
    {
        var result = new List<ReferenceBinding>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (MonoBehaviour owner in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (owner == null || IsPanelPath(AnimationUtility.CalculateTransformPath(owner.transform, canvas))) continue;
            var serialized = new SerializedObject(owner);
            var property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;
                Object value = property.objectReferenceValue;
                Transform target = GetTransform(value);
                if (target == null || !target.IsChildOf(canvas)) continue;
                string path = AnimationUtility.CalculateTransformPath(target, canvas);
                if (!IsPanelPath(path)) continue;
                result.Add(new ReferenceBinding { Owner = owner, Property = property.propertyPath,
                    TargetPath = path, TargetType = value.GetType(),
                    ComponentIndex = value is Component ? Array.IndexOf(target.GetComponents(value.GetType()), value) : 0 });
            }
        }
        return result;
    }

    private static Transform GetTransform(Object value)
    {
        var component = value as Component;
        if (component != null) return component.transform;
        var gameObject = value as GameObject;
        return gameObject != null ? gameObject.transform : null;
    }

    private static bool IsPanelPath(string path) => PANELS.Any(p => path == p || path.StartsWith(p + "/", StringComparison.Ordinal));

    private static string AssetKey(Object value)
    {
        if (value == null) return "null";
        string guid;
        long id;
        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out id) ? guid + ":" + id : value.name;
    }

    private static List<string> CapturePanelVisuals(Transform canvas)
    {
        var rows = new List<string>();
        foreach (string panel in PANELS)
        foreach (Transform t in canvas.Find(panel).GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(t, canvas);
            var r = (RectTransform)t;
            // 비교는 부모 Canvas 안에서 수행하므로 패널 루트의 형제 순서도 보존한다.
            rows.Add(path + "|" + t.gameObject.activeSelf + "|" + t.gameObject.layer + "|" +
                r.anchorMin.ToString("R") + r.anchorMax.ToString("R") + r.pivot.ToString("R") +
                r.anchoredPosition3D.ToString("R") + r.sizeDelta.ToString("R") +
                r.localScale.ToString("R") + r.localRotation.ToString("R") + "|" +
                t.GetSiblingIndex() + "|" +
                string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().FullName)));
            Image image = t.GetComponent<Image>();
            if (image != null) rows.Add(path + "|Image|" + image.enabled + AssetKey(image.sprite) + AssetKey(image.material) +
                image.color.ToString("R") + image.type + image.preserveAspect + image.raycastTarget + image.fillAmount.ToString("R"));
            Text text = t.GetComponent<Text>();
            if (text != null) rows.Add(path + "|Text|" + text.enabled + text.text + AssetKey(text.font) +
                text.fontSize + text.fontStyle + text.alignment + text.color.ToString("R") +
                text.resizeTextForBestFit + text.resizeTextMinSize + text.resizeTextMaxSize + text.raycastTarget);
        }
        return rows;
    }

    private static List<string> CaptureAllButtons(Scene scene)
    {
        return scene.GetRootGameObjects().SelectMany(root => CaptureButtons(root.transform)).ToList();
    }

    private static List<string> CaptureButtons(Transform canvas)
    {
        var rows = new List<string>();
        foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
        {
            var serialized = new SerializedObject(button);
            SerializedProperty calls = serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            string path = AnimationUtility.CalculateTransformPath(button.transform, canvas);
            rows.Add(path + "|" + button.interactable + "|" + calls.arraySize);
            for (int i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                rows.Add(path + "|" + i + "|" + call.FindPropertyRelative("m_MethodName").stringValue + "|" +
                    call.FindPropertyRelative("m_Target").objectReferenceInstanceIDValue + "|" +
                    call.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceInstanceIDValue);
            }
        }
        return rows;
    }

    private static void AssertSame(List<string> before, List<string> after, string label)
    {
        if (!before.SequenceEqual(after))
            throw new InvalidOperationException(label + " 불일치: " +
                string.Join(" / ", before.Except(after).Take(3)));
    }
}
