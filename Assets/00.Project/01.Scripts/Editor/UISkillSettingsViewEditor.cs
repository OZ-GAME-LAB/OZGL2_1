using System.Collections.Generic;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.Editor.UIFlow
{
    // 미리보기 적용은 명시적인 Inspector 작업으로만 실행하고 Scene 변경을 Undo로 남긴다.
    [CustomEditor(typeof(UISkillSettingsView))]
    public sealed class UISkillSettingsViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox(
                "Preview Skills의 이름·아이콘·Is Unlocked를 수정한 뒤 아래 버튼으로 표시를 갱신하세요. " +
                "잠금·해금은 미리보기이며 실제 장착이나 저장은 하지 않습니다. " +
                "Play Mode 중 바꾼 임시 값은 종료 시 되돌아갑니다.", MessageType.Info);

            if (!GUILayout.Button("임시 데이터로 표시 갱신")) return;
            ApplyPreview((UISkillSettingsView)target);
        }

        public static void ApplyPreview(UISkillSettingsView view)
        {
            if (view == null) return;
            if (!Application.isPlaying) RecordDisplayUndo(view);

            view.ApplyPreviewData();
            if (Application.isPlaying) return;

            foreach (Transform child in view.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(child)) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                foreach (Component component in child.GetComponents<Component>())
                    if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            EditorUtility.SetDirty(view);
            if (view.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }

        private static void RecordDisplayUndo(UISkillSettingsView view)
        {
            // 설정 데이터의 기존 Undo 기록을 덮어쓰지 않고 표시 대상만 기록한다.
            var objects = new List<Object>();
            foreach (UISkillSlotView slot in view.GetComponentsInChildren<UISkillSlotView>(true))
            {
                foreach (Transform child in slot.GetComponentsInChildren<Transform>(true))
                {
                    objects.Add(child.gameObject);
                    foreach (Graphic graphic in child.GetComponents<Graphic>()) objects.Add(graphic);
                    foreach (Selectable selectable in child.GetComponents<Selectable>()) objects.Add(selectable);
                }
            }
            if (objects.Count > 0) Undo.RecordObjects(objects.ToArray(), "Apply skill preview data");
        }
    }
}
