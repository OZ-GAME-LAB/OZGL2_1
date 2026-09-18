using UnityEngine;

namespace OZGL2.Sandbox
{
    /// <summary>
    /// 개인 루프 테스트 화면(로비·스테이지 선택·인게임) 전체가 같이 쓰는 다크 테마 GUIStyle 모음.
    /// 기본 회색 IMGUI 대신 어두운 패널 + 금색 강조 버튼으로 "디버그 창"이 아니라 게임 화면처럼 보이게 함.
    /// </summary>
    public static class SandboxGameUI
    {
        private static GUIStyle _panel, _title, _subtitle, _body, _primaryButton, _secondaryButton, _cardOpen, _cardLocked;
        private static GUIStyle _diamondLabel, _diamondSub, _categoryHeader;

        public static GUIStyle Panel => _panel ??= BuildPanel(new Color(0.08f, 0.09f, 0.14f, 0.97f), 20, 18);
        public static GUIStyle Title => _title ??= BuildLabel(24, FontStyle.Bold, new Color(0.95f, 0.85f, 0.55f));
        public static GUIStyle Subtitle => _subtitle ??= BuildLabel(15, FontStyle.Normal, new Color(0.8f, 0.82f, 0.9f));
        public static GUIStyle Body => _body ??= BuildLabel(13, FontStyle.Normal, new Color(0.75f, 0.77f, 0.85f));
        public static GUIStyle PrimaryButton => _primaryButton ??= BuildButton(new Color(0.72f, 0.52f, 0.14f), Color.white, 16, 46);
        public static GUIStyle SecondaryButton => _secondaryButton ??= BuildButton(new Color(0.22f, 0.25f, 0.33f), new Color(0.9f, 0.9f, 0.95f), 14, 34);
        public static GUIStyle CardOpen => _cardOpen ??= BuildPanel(new Color(0.15f, 0.18f, 0.26f, 1f), 8, 8);
        public static GUIStyle CardLocked => _cardLocked ??= BuildPanel(new Color(0.08f, 0.08f, 0.1f, 1f), 8, 8);
        public static GUIStyle DiamondLabel => _diamondLabel ??= CenterLabel(13, FontStyle.Bold, Color.white);
        public static GUIStyle DiamondSub => _diamondSub ??= CenterLabel(11, FontStyle.Normal, new Color(0.85f, 0.85f, 0.9f));
        public static GUIStyle CategoryHeader => _categoryHeader ??= BuildButtonLikeHeader();

        private static GUIStyle CenterLabel(int size, FontStyle style, Color color)
        {
            var s = BuildLabel(size, style, color);
            s.alignment = TextAnchor.UpperCenter;
            return s;
        }

        private static GUIStyle BuildButtonLikeHeader()
        {
            var s = new GUIStyle(GUI.skin.box);
            s.normal.background = SolidTex(new Color(0.55f, 0.22f, 0.2f));
            s.normal.textColor = Color.white;
            s.fontSize = 13;
            s.fontStyle = FontStyle.Bold;
            s.alignment = TextAnchor.MiddleCenter;
            s.padding = new RectOffset(4, 4, 6, 6);
            return s;
        }

        /// <summary>다이아몬드(45도 회전 사각형) 노드를 그리고, 잠겨있지 않고 클릭됐으면 true. 라벨은 아래에 별도로 그려짐.</summary>
        public static bool DrawDiamondNode(Rect rect, Color fillColor, bool locked, string title, string subtitle)
        {
            var prevMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, rect.center);
            GUI.DrawTexture(rect, SolidTex(locked ? new Color(0.14f, 0.09f, 0.09f) : fillColor));
            GUI.matrix = prevMatrix;

            bool clicked = !locked && GUI.Button(rect, GUIContent.none, GUIStyle.none);

            if (locked) GUI.Label(rect, "잠김", DiamondSub);

            float labelW = rect.width + 50f;
            var titleRect = new Rect(rect.center.x - labelW / 2f, rect.yMax + 2f, labelW, 18f);
            GUI.Label(titleRect, title, DiamondLabel);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var subRect = new Rect(rect.center.x - labelW / 2f, rect.yMax + 18f, labelW, 16f);
                GUI.Label(subRect, subtitle, DiamondSub);
            }
            return clicked;
        }

        /// <summary>두 점을 잇는 얇은 직선 — 트리 노드 사이 연결선용.</summary>
        public static void DrawLine(Vector2 a, Vector2 b, Color color, float thickness = 2f)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.01f) return;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            var prevMatrix = GUI.matrix;
            var rect = new Rect(a.x, a.y - thickness / 2f, length, thickness);
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(rect, SolidTex(color));
            GUI.matrix = prevMatrix;
        }

        private static GUIStyle BuildPanel(Color bg, int padH, int padV)
        {
            var s = new GUIStyle(GUI.skin.box);
            s.normal.background = SolidTex(bg);
            s.padding = new RectOffset(padH, padH, padV, padV);
            return s;
        }

        private static GUIStyle BuildLabel(int size, FontStyle style, Color color)
        {
            var s = new GUIStyle(GUI.skin.label);
            s.fontSize = size;
            s.fontStyle = style;
            s.normal.textColor = color;
            s.wordWrap = true;
            return s;
        }

        private static GUIStyle BuildButton(Color bg, Color text, int fontSize, int height)
        {
            var s = new GUIStyle(GUI.skin.button);
            s.normal.background = SolidTex(bg);
            s.hover.background = SolidTex(Lighten(bg, 1.15f));
            s.active.background = SolidTex(Lighten(bg, 0.8f));
            s.normal.textColor = text;
            s.hover.textColor = text;
            s.active.textColor = text;
            s.fontSize = fontSize;
            s.fontStyle = FontStyle.Bold;
            s.fixedHeight = height;
            s.padding = new RectOffset(10, 10, 6, 6);
            return s;
        }

        private static Color Lighten(Color c, float f) => new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a);

        private static Texture2D SolidTex(Color c)
        {
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { c, c, c, c });
            tex.Apply();
            return tex;
        }
    }
}
