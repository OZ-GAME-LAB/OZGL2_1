using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UI
{
    /// <summary>게임 데이터와 분리된 설명창 외형과 기준 크기만 관리합니다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIAdaptiveTooltipPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform _contentRoot;
        [SerializeField] private Image _frame;
        [SerializeField] private RectTransform _headerOrnament;
        [SerializeField, Min(160f)] private float _baseWidth = 600f;
        [SerializeField] private Vector4 _padding = new Vector4(32f, 44f, 32f, 28f);
        [SerializeField] private Vector2 _ornamentSize = new Vector2(120f, 60f);
        [SerializeField, Min(0.1f)] private float _framePixelsPerUnitMultiplier = 4f;
        [SerializeField] private TMP_FontAsset _titleFont;
        [SerializeField] private TMP_FontAsset _bodyFont;

        public RectTransform RectTransform => transform as RectTransform;
        public RectTransform ContentRoot => _contentRoot;
        public TMP_FontAsset TitleFont => _titleFont;
        public TMP_FontAsset BodyFont => _bodyFont;

        public float GetWidth(float screenScale, float screenWidth)
        {
            float scale = Mathf.Max(0.01f, screenScale);
            float available = Mathf.Max(1f, screenWidth - 16f * scale);
            return Mathf.Min(_baseWidth * scale, available);
        }

        // 왼쪽/위/오른쪽/아래 여백이며 ContentRoot의 좌표계는 루트와 같습니다.
        public Vector4 GetPadding(float screenScale) => _padding * Mathf.Max(0.01f, screenScale);

        public void ApplySize(float width, float height, float screenScale)
        {
            float scale = Mathf.Max(0.01f, screenScale);
            RectTransform.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
            if (_frame != null)
                _frame.pixelsPerUnitMultiplier = _framePixelsPerUnitMultiplier / scale;
            if (_headerOrnament != null)
                _headerOrnament.sizeDelta = _ornamentSize * scale;
        }
    }
}
