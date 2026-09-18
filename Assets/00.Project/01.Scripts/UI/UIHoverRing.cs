using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 별도 이미지 없이 Scene에서 크기/색/두께를 편집할 수 있는 도넛 UI 도형이다.
    [AddComponentMenu("UI/Hover Ring")]
    public sealed class UIHoverRing : MaskableGraphic
    {
        [Min(1f)] [SerializeField] private float _thickness = 5f;
        [Range(12, 192)] [SerializeField] private int _segments = 96;
        [Range(0f, 1f)] [SerializeField] private float _remainingFraction = 1f;
        public float RemainingFraction => _remainingFraction;

        public void SetRemaining(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Approximately(_remainingFraction, fraction)) return;
            _remainingFraction = fraction;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_remainingFraction <= 0f) return;
            Rect rect = GetPixelAdjustedRect();
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = Mathf.Max(0f, outer - Mathf.Max(1f, _thickness));
            if (outer <= 0f) return;
            int count = Mathf.Max(1, Mathf.CeilToInt(Mathf.Clamp(_segments, 12, 192) * _remainingFraction));
            // 12시에서 시계방향으로 비워진 각도 뒤에 남은 호만 그린다.
            float start = (1f - _remainingFraction) * Mathf.PI * 2f;
            float sweep = _remainingFraction * Mathf.PI * 2f;
            for (int i = 0; i <= count; i++)
            {
                float angle = start + sweep * i / count;
                Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                vh.AddVert(rect.center + direction * outer, color, Vector2.zero);
                vh.AddVert(rect.center + direction * inner, color, Vector2.zero);
                if (i == 0) continue;
                int index = i * 2;
                vh.AddTriangle(index - 2, index, index - 1);
                vh.AddTriangle(index, index + 1, index - 1);
            }
        }
    }
}
