using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>확장 기록판의 이음선 없는 단색 안쪽 면과 직선 레일 길이만 담당한다.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class UIRecordPanelBackground : MaskableGraphic
    {
        [SerializeField] private Image _leftRail;
        [SerializeField] private Image _rightRail;
        [SerializeField] private float _artScale = 1f;
        [SerializeField] private Color _edgeColor = new Color32(30, 14, 13, 255);
        [SerializeField] private Color _centerColor = new Color32(17, 13, 12, 255);

        public void Configure(Image leftRail, Image rightRail, float scale)
        {
            _leftRail = leftRail;
            _rightRail = rightRail;
            _artScale = Mathf.Max(0.01f, scale);
            raycastTarget = false;
            RefreshRails();
            SetVerticesDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            RefreshRails();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshRails();
        }

        private void RefreshRails()
        {
            float height = Mathf.Max(0f, rectTransform.rect.height - 154f * _artScale);
            SetRail(_leftRail, false, height);
            SetRail(_rightRail, true, height);
        }

        private void SetRail(Image rail, bool isRight, float height)
        {
            if (rail == null) return;
            RectTransform rect = rail.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(isRight ? 1f : 0f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(17f * _artScale, height);
            rect.anchoredPosition = new Vector2((isRight ? -1f : 1f) * 36.5f * _artScale, 86f * _artScale);
            rail.enabled = height > 0.01f;
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            float inset = 38f * _artScale;
            float bottom = rect.yMin + 22f * _artScale;
            float top = rect.yMax - 22f * _artScale;
            float cut = Mathf.Min(48f * _artScale, Mathf.Max(0f, (top - bottom) * 0.5f));
            float left = rect.xMin + inset;
            float right = rect.xMax - inset;
            Vector2[] points = {
                new Vector2(left + cut, bottom), new Vector2(right - cut, bottom),
                new Vector2(right, bottom + cut), new Vector2(right, top - cut),
                new Vector2(right - cut, top), new Vector2(left + cut, top),
                new Vector2(left, top - cut), new Vector2(left, bottom + cut)
            };
            helper.AddVert(new Vector3(rect.center.x, (bottom + top) * 0.5f), _centerColor * color, Vector2.zero);
            foreach (Vector2 point in points) helper.AddVert(point, _edgeColor * color, Vector2.zero);
            for (int i = 0; i < points.Length; i++) helper.AddTriangle(0, i + 1, (i + 1) % points.Length + 1);
        }
    }
}
