using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>기록 뒤의 하단 그라데이션을 채운다. 외곽 곡선은 부모 Mask가 자른다.</summary>
    [AddComponentMenu("UI/Lobby Record Card Shade")]
    public sealed class UIRecordCardShade : MaskableGraphic
    {
        [SerializeField, Min(1f)] private float _fadeHeight = 240f;

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            float fadeBottom = Mathf.Max(rect.yMin, rect.yMax - Mathf.Max(1f, _fadeHeight));
            AddBand(helper, rect, fadeBottom, rect.yMax);
            // 페이드 길이와 면의 높이를 분리해 곡선 직전에서 음영이 끊기지 않게 한다.
            if (fadeBottom > rect.yMin) AddBand(helper, rect, rect.yMin, fadeBottom);
        }

        private void AddBand(VertexHelper helper, Rect rect, float bottom, float top)
        {
            int index = helper.currentVertCount;
            AddVertex(helper, new Vector2(rect.xMin, bottom), rect);
            AddVertex(helper, new Vector2(rect.xMin, top), rect);
            AddVertex(helper, new Vector2(rect.xMax, top), rect);
            AddVertex(helper, new Vector2(rect.xMax, bottom), rect);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index + 2, index + 3, index);
        }

        private void AddVertex(VertexHelper helper, Vector2 point, Rect rect)
        {
            Color shade = color;
            shade.a *= Mathf.Clamp01((rect.yMax - point.y) / Mathf.Max(1f, _fadeHeight));
            helper.AddVert(point, shade, Vector2.zero);
        }
    }
}
