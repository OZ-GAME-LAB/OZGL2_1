using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 목표 횟수의 실제 경계만 단일 메시로 그린다. 채움과 진행도 집계는 카드와 런타임 상태가 맡는다.
    [AddComponentMenu("UI/Achievement Progress Dividers")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIAchievementProgressDividers : MaskableGraphic
    {
        private const int MAX_RENDERED_DIVIDERS = 4096;

        [SerializeField, Min(0), Tooltip("미리보기 목표 횟수입니다. 실행 중에는 연결된 업적 카드가 설정하며 0이면 구분선을 숨깁니다.")]
        private int _target = 1;
        [SerializeField, Min(1), Tooltip("한 칸에 해당하는 횟수입니다. 마지막 칸은 남은 횟수만큼 짧게 표시합니다.")]
        private int _progressPerSegment = 1;
        [SerializeField, Min(0f), Tooltip("구분선 두께입니다. 인접 칸이 좁으면 겹치지 않도록 자동으로 줄어듭니다. 색상은 Graphic의 Color로 설정합니다.")]
        private float _lineWidth = 3f;
        [SerializeField, Tooltip("선택 연결: 한 개짜리 눈금 스프라이트입니다. 비어 있으면 단색 구분선을 표시합니다. Full Rect 스프라이트를 사용합니다.")]
        private Sprite _tickSprite;
        [SerializeField, Range(0f, 1f), Tooltip("진행 막대 높이 대비 눈금 높이입니다. 1은 기존 전체 높이 구분선입니다.")]
        private float _heightRatio = 1f;
        [SerializeField, Min(0f), Tooltip("진행 막대 아래쪽에서 눈금을 띄우는 거리입니다.")]
        private float _bottomInset;
        [SerializeField, Tooltip("완료 상태의 눈금 추가 틴트입니다. 밝은 완료 Fill 위에서 눈금이 보이도록 조절합니다.")]
        private Color _completedTint = Color.white;
        [SerializeField, Range(1, MAX_RENDERED_DIVIDERS), Tooltip("한 메시의 구분선 렌더 예산입니다. 초과 시 실제 경계 중 고르게 선택해 표시하며 논리 칸 수와 진행 비율은 바꾸지 않습니다. 최대 4096개로 UI 정점 한도를 보호합니다.")]
        private int _renderDividerLimit = MAX_RENDERED_DIVIDERS;

        private bool _isCompleted;

        public Sprite TickSprite => _tickSprite;
        public override Texture mainTexture => _tickSprite != null ? _tickSprite.texture : base.mainTexture;

        public int Target => Mathf.Max(0, _target);
        public int ProgressPerSegment => Mathf.Max(1, _progressPerSegment);
        public int SegmentCount => Target > 0 ? 1 + (Target - 1) / ProgressPerSegment : 0;
        public int DividerCount => Mathf.Max(0, SegmentCount - 1);
        public int RenderedDividerCount => Mathf.Min(DividerCount, Mathf.Clamp(_renderDividerLimit, 1, MAX_RENDERED_DIVIDERS));

        public void Configure(int target, int progressPerSegment)
        {
            target = Mathf.Max(1, target);
            progressPerSegment = Mathf.Max(1, progressPerSegment);
            raycastTarget = false;
            if (_target == target && _progressPerSegment == progressPerSegment) return;
            _target = target;
            _progressPerSegment = progressPerSegment;
            SetVerticesDirty();
        }

        public void Clear()
        {
            if (_target == 0) return;
            _target = 0;
            SetVerticesDirty();
        }

        public void SetCompleted(bool isCompleted)
        {
            if (_isCompleted == isCompleted) return;
            _isCompleted = isCompleted;
            SetVerticesDirty();
        }

        // 인덱스는 0부터 시작한다. 목표를 정확히 나누지 못해도 경계는 실제 누적 횟수 비율을 유지한다.
        public float GetDividerRatio(int dividerIndex)
        {
            if (dividerIndex < 0 || dividerIndex >= DividerCount) return 0f;
            return (float)(((long)dividerIndex + 1) * ProgressPerSegment / (double)Target);
        }

        protected override void OnEnable()
        {
            raycastTarget = false;
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int renderedCount = RenderedDividerCount;
            Rect rect = GetPixelAdjustedRect();
            if (renderedCount == 0 || rect.width <= 0f || rect.height <= 0f || _lineWidth <= 0f ||
                float.IsNaN(_lineWidth) || float.IsInfinity(_lineWidth)) return;

            float bottom = rect.yMin + Mathf.Clamp(_bottomInset, 0f, rect.height);
            float top = Mathf.Min(rect.yMax, bottom + rect.height * Mathf.Clamp01(_heightRatio));
            if (top <= bottom) return;
            Vector4 uv = _tickSprite != null
                ? UnityEngine.Sprites.DataUtility.GetOuterUV(_tickSprite)
                : new Vector4(0f, 0f, 1f, 1f);
            Color32 lineColor = color * (_isCompleted ? _completedTint : Color.white);
            for (int index = 0; index < renderedCount; index++)
            {
                // 지나치게 큰 목표에서도 반복 횟수와 정점을 제한한다. 가상 등분선 대신 실제 경계를 선택한다.
                int dividerIndex = renderedCount == 1
                    ? (DividerCount - 1) / 2
                    : (int)((long)index * (DividerCount - 1) / (renderedCount - 1));
                long boundaryProgress = ((long)dividerIndex + 1) * ProgressPerSegment;
                double ratio = boundaryProgress / (double)Target;
                double nextSegmentProgress = System.Math.Min(ProgressPerSegment, Target - boundaryProgress);
                // 마지막 나머지 칸까지 고려해 양쪽 칸이 선에 가려지지 않도록 두께를 제한한다.
                float halfWidth = (float)System.Math.Min(_lineWidth * 0.5, rect.width * nextSegmentProgress / Target * 0.25);
                float x = (float)(rect.xMin + rect.width * ratio);
                int vertexIndex = vh.currentVertCount;
                vh.AddVert(new Vector3(x - halfWidth, bottom), lineColor, new Vector2(uv.x, uv.y));
                vh.AddVert(new Vector3(x - halfWidth, top), lineColor, new Vector2(uv.x, uv.w));
                vh.AddVert(new Vector3(x + halfWidth, top), lineColor, new Vector2(uv.z, uv.w));
                vh.AddVert(new Vector3(x + halfWidth, bottom), lineColor, new Vector2(uv.z, uv.y));
                vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
                vh.AddTriangle(vertexIndex, vertexIndex + 2, vertexIndex + 3);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            _target = Mathf.Max(0, _target);
            _progressPerSegment = Mathf.Max(1, _progressPerSegment);
            _lineWidth = Mathf.Max(0f, _lineWidth);
            _heightRatio = Mathf.Clamp01(_heightRatio);
            _bottomInset = Mathf.Max(0f, _bottomInset);
            _renderDividerLimit = Mathf.Clamp(_renderDividerLimit, 1, MAX_RENDERED_DIVIDERS);
            raycastTarget = false;
            base.OnValidate();
            SetAllDirty();
        }
#endif
    }
}
