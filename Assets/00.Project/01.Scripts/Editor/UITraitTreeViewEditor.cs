using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.Editor.UIFlow
{
    [CustomEditor(typeof(UITraitTreeView))]
    public sealed class UITraitTreeViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Traits에서 노드별 ID·이름·설명·단계별 효과 문구를 등록합니다. " +
                "미리보기 클릭은 실제 포인트/저장을 변경하지 않습니다. " +
                "아래 버튼은 임시 단계를 Initial Rank로 초기화하며, 편집 모드에서는 Undo를 지원합니다.", MessageType.Info);
            if (GUILayout.Button("임시 특성 데이터 적용 / 단계 초기화")) ApplyPreview((UITraitTreeView)target);
        }

        public static void ApplyPreview(UITraitTreeView view)
        {
            if (view == null) return;
            Text[] texts = view.GetComponentsInChildren<Text>(true);
            if (!Application.isPlaying) Undo.RecordObjects(texts, "Apply trait preview data");
            view.ApplyPreviewData();
            if (Application.isPlaying) return;
            foreach (Text text in texts)
                if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            if (view.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }
    }
}
