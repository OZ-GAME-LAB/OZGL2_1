using OZGL2.UIFlow;
using UnityEditor;

namespace OZGL2.Editor.UIFlow
{
    // Unity 기본 Button Inspector에 가려지는 상태 Sprite와 TMP 참조를 노출한다.
    [CustomEditor(typeof(UISkillArtButton))]
    [CanEditMultipleObjects]
    public sealed class UISkillArtButtonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Normal / Hover / Pressed / Chosen / Disabled는 글자 없는 이미지입니다. " +
                "문구는 자식 Label(TMP)에서 수정하세요. 카테고리 선택은 키보드 포커스와 별개로 유지됩니다.", MessageType.Info);
        }
    }
}
