using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 한 개짜리 눈금 스프라이트를 등간격으로 반복한다. 경험치·업적 등 데이터 규칙은 알지 않는다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Progress Tick Graphic")]
    public sealed class UIProgressTickGraphic : MaskableGraphic
    {
        private const int MAX_SEGMENTS = 64;

        [SerializeField, Tooltip("Full Rect 스프라이트를 사용한다. 비어 있으면 눈금을 그리지 않는다.")]
        private Sprite _tickSprite;
        [SerializeField, Range(1, MAX_SEGMENTS), Tooltip("전체 구간 수. 5구간이면 20/40/60/80%에 눈금 4개를 그린다.")]
        private int _segmentCount = 5;
        [SerializeField, Min(0f), Tooltip("눈금 높이. 너비는 스프라이트 비율을 유지하며 좁은 칸에서는 함께 축소한다.")]
        private float _tickHeight = 16f;

        public Sprite TickSprite => _tickSprite;
        public int SegmentCount => Mathf.Clamp(_segmentCount, 1, MAX_SEGMENTS);
        public int TickCount => SegmentCount - 1;
        public override Texture mainTexture => _tickSprite != null ? _tickSprite.texture : base.mainTexture;

        public void SetSegmentCount(int segmentCount)
        {
            segmentCount = Mathf.Clamp(segmentCount, 1, MAX_SEGMENTS);
            if (_segmentCount == segmentCount) return;
            _segmentCount = segmentCount;
            SetVerticesDirty();
        }

        public float GetTickRatio(int index) => index >= 0 && index < TickCount
            ? (index + 1f) / SegmentCount : 0f;

        protected override void OnEnable()
        {
            raycastTarget = false;
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (_tickSprite == null || TickCount == 0 || rect.width <= 0f || rect.height <= 0f ||
                _tickHeight <= 0f || float.IsNaN(_tickHeight) || float.IsInfinity(_tickHeight)) return;

            float aspect = _tickSprite.rect.width / Mathf.Max(1f, _tickSprite.rect.height);
            float height = Mathf.Min(_tickHeight, rect.height);
            float width = Mathf.Min(height * aspect, rect.width / SegmentCount * 0.5f);
            height = width / Mathf.Max(0.001f, aspect);
            Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(_tickSprite);
            float top = rect.yMax;
            for (int index = 0; index < TickCount; index++)
            {
                float center = Mathf.Lerp(rect.xMin, rect.xMax, GetTickRatio(index));
                int start = vh.currentVertCount;
                vh.AddVert(new Vector3(center - width * 0.5f, top - height), color, new Vector2(uv.x, uv.y));
                vh.AddVert(new Vector3(center - width * 0.5f, top), color, new Vector2(uv.x, uv.w));
                vh.AddVert(new Vector3(center + width * 0.5f, top), color, new Vector2(uv.z, uv.w));
                vh.AddVert(new Vector3(center + width * 0.5f, top - height), color, new Vector2(uv.z, uv.y));
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            _segmentCount = SegmentCount;
            _tickHeight = Mathf.Max(0f, _tickHeight);
            raycastTarget = false;
            base.OnValidate();
            SetAllDirty();
        }
#endif
    }
}
