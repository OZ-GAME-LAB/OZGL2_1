using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Radial Glow")]
    public sealed class UIRadialGlow : MaskableGraphic
    {
        [SerializeField, Range(12, 96)] private int _segments = 48;
        [SerializeField, Range(2, 16)] private int _rings = 10;

        protected override void OnEnable()
        {
            raycastTarget = false;
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            AddGlow(vh, rect.center, rect.size * 0.5f, color, _segments, _rings);
        }

        // 별도 텍스처 없이 중심에서 가장자리로 투명해지는 UI 메시를 만든다.
        internal static void AddGlow(VertexHelper vh, Vector2 center, Vector2 radius, Color tint,
            int segments = 12, int rings = 4)
        {
            if (radius.x <= 0f || radius.y <= 0f || tint.a <= 0f) return;
            segments = Mathf.Clamp(segments, 4, 96);
            rings = Mathf.Clamp(rings, 1, 16);
            int first = vh.currentVertCount;
            Vector2 uv = new Vector2(0.5f, 0.5f);
            vh.AddVert(center, tint, uv);
            for (int ring = 1; ring <= rings; ring++)
            {
                float ratio = ring / (float)rings;
                Color ringColor = tint;
                ringColor.a *= Mathf.Pow(Mathf.Max(0f, 1f - ratio * ratio), 2.5f);
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment * (Mathf.PI * 2f / segments);
                    Vector2 position = center + new Vector2(Mathf.Cos(angle) * radius.x,
                        Mathf.Sin(angle) * radius.y) * ratio;
                    vh.AddVert(position, ringColor, uv);
                }
            }

            for (int segment = 0; segment < segments; segment++)
                vh.AddTriangle(first, first + 1 + segment, first + 1 + (segment + 1) % segments);

            for (int ring = 1; ring < rings; ring++)
            {
                int inner = first + 1 + (ring - 1) * segments;
                int outer = inner + segments;
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    vh.AddTriangle(inner + segment, outer + segment, outer + next);
                    vh.AddTriangle(inner + segment, outer + next, inner + next);
                }
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            raycastTarget = false;
            base.OnValidate();
        }
#endif
    }
}
