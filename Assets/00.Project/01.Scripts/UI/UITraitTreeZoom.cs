using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 이동은 기존 ScrollRect가 담당하고 휠만 줌으로 변환한다. 설명창/고정 HUD는 확대하지 않는다.
    [DisallowMultipleComponent, RequireComponent(typeof(ScrollRect))]
    public sealed class UITraitTreeZoom : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private ScrollRect _scroll;
        [SerializeField, Range(0.1f, 1)] private float _minZoom = 0.25f;
        [SerializeField, Range(1, 3)] private float _maxZoom = 1.6f;
        [SerializeField, Range(0.01f, 0.5f)] private float _wheelStep = 0.12f;
        [SerializeField, Min(0), Tooltip("줌 배율과 관계없이 좌우에 각각 추가할 이동 여유입니다. Canvas 기준 UI 픽셀 단위이며 0이면 기존 범위를 유지합니다.")]
        private float _horizontalDragPadding;
        private readonly Vector3[] _corners = new Vector3[4];
        private float _baseContentWidth;
        private bool _hasCachedContentWidth;

        public float Zoom => _scroll != null && _scroll.content != null ? _scroll.content.localScale.x : 1;
        public float MinZoom => _minZoom;
        public float MaxZoom => _maxZoom;

        private void Awake()
        {
            if (_scroll == null) TryGetComponent(out _scroll);
            // 같은 오브젝트의 ScrollRect에도 휠 이벤트가 전달되므로 기본 휠 이동은 끈다.
            if (_scroll != null) _scroll.scrollSensitivity = 0;
        }

        private void OnEnable()
        {
            UpdateHorizontalDragBounds();
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateHorizontalDragBounds();
        }

        private void OnDisable()
        {
            if (_hasCachedContentWidth && _scroll != null && _scroll.content != null)
                _scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _baseContentWidth);
            _hasCachedContentWidth = false;
        }

        private void UpdateHorizontalDragBounds()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _scroll == null ||
                _scroll.content == null || _scroll.viewport == null ||
                (_horizontalDragPadding <= 0 && !_hasCachedContentWidth)) return;

            RectTransform content = _scroll.content;
            float scale = Mathf.Abs(content.localScale.x);
            if (scale <= Mathf.Epsilon) return;
            if (!_hasCachedContentWidth)
            {
                _baseContentWidth = content.rect.width;
                _hasCachedContentWidth = true;
            }

            // 중앙 고정 앵커의 노드/연결선은 그대로 두고 ScrollRect가 보는 가로 경계만 넓힌다.
            // 작은 배율에서도 좌우 이동이 가능하며, 높이와 세로 제한에는 영향을 주지 않는다.
            float padding = Mathf.Max(0, _horizontalDragPadding);
            float width = padding > 0
                ? (Mathf.Max(_baseContentWidth * scale, _scroll.viewport.rect.width) + padding * 2) / scale
                : _baseContentWidth;
            if (!Mathf.Approximately(content.rect.width, width))
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || _scroll == null || !_scroll.isActiveAndEnabled ||
                _scroll.content == null || _scroll.viewport == null) return;
            float delta = eventData.scrollDelta.y;
            if (Mathf.Approximately(delta, 0)) return;
            float next = Mathf.Clamp(Zoom * Mathf.Exp(Mathf.Clamp(delta, -3, 3) * _wheelStep), _minZoom, _maxZoom);
            if (Mathf.Approximately(next, Zoom)) return;
            RectTransform content = _scroll.content;
            RectTransform viewport = _scroll.viewport;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, eventData.position,
                    eventData.enterEventCamera, out Vector2 localPoint)) return;
            Vector3 before = viewport.InverseTransformPoint(content.TransformPoint(localPoint));
            _scroll.StopMovement();
            content.localScale = new Vector3(next, next, 1);
            UpdateHorizontalDragBounds();
            Vector3 after = viewport.InverseTransformPoint(content.TransformPoint(localPoint));
            content.anchoredPosition += (Vector2)(before - after);
            ClampToViewport();
        }

        private void ClampToViewport()
        {
            RectTransform content = _scroll.content;
            RectTransform viewport = _scroll.viewport;
            content.GetWorldCorners(_corners);
            Vector2 min = viewport.InverseTransformPoint(_corners[0]);
            Vector2 max = viewport.InverseTransformPoint(_corners[2]);
            Rect view = viewport.rect;
            content.anchoredPosition += new Vector2(
                Offset(min.x, max.x, view.xMin, view.xMax),
                Offset(min.y, max.y, view.yMin, view.yMax));
        }

        private static float Offset(float min, float max, float viewMin, float viewMax)
        {
            if (max - min <= viewMax - viewMin) return (viewMin + viewMax - min - max) * 0.5f;
            if (min > viewMin) return viewMin - min;
            if (max < viewMax) return viewMax - max;
            return 0;
        }
    }
}
