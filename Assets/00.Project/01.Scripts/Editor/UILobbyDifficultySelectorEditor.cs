using System.Linq;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(UILobbyDifficultySelector))]
public sealed class UILobbyDifficultySelectorEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        UILobbyDifficultySelector selector = (UILobbyDifficultySelector)target;
        EditorGUILayout.HelpBox("Artwork Set 하나로 마왕성 / 용사군 세트를 교체합니다. 세트 또는 Catalog 내부 값을 편집했다면 아래 버튼으로 표시를 갱신하세요. 실제 전투 난이도와 저장 데이터는 변경하지 않습니다.", MessageType.Info);
        if (!selector.TryValidate(out string reason)) EditorGUILayout.HelpBox(reason, MessageType.Warning);
        if (GUILayout.Button("난이도 표시 갱신")) RefreshWithUndo(selector);
        if (Application.isPlaying)
            EditorGUILayout.LabelField("확정된 선택", selector.SelectedDifficulty + (selector.IsTransitioning ? " (전환 중)" : string.Empty));
    }

    public static void RefreshWithUndo(UILobbyDifficultySelector selector)
    {
        if (selector == null) return;
        if (!Application.isPlaying)
            Undo.RecordObjects(selector.GetComponentsInChildren<Component>(true).Where(item => item != null).Cast<Object>().ToArray(), "Refresh lobby difficulty presentation");
        selector.RefreshPresentation();
        if (Application.isPlaying) return;
        EditorUtility.SetDirty(selector);
        if (selector.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(selector.gameObject.scene);
    }
}
