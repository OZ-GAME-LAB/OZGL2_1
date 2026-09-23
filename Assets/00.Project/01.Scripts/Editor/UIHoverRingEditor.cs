using OZGL2.UIFlow;
using UnityEditor;

namespace OZGL2.Editor.UIFlow
{
    // Graphic의 기본 Inspector에 숨겨질 수 있는 링 전용 설정도 함께 노출한다.
    [CustomEditor(typeof(UIHoverRing))]
    public sealed class UIHoverRingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Remaining Fraction이 1이면 전체 링, 0이면 빈 상태입니다. " +
                "재생 중에는 호버 컨트롤러가 이 값을 갱신합니다. Thickness는 링 두께입니다.", MessageType.Info);
        }
    }
}
