using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// 고정된 입력 영역과 움직이는 카드 비주얼을 분리해 호버 중 포인터 깜빡임을 막는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIBattleCardHandSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private UIBattleCardHandView _owner;
        private RectTransform _slotRoot;
        private RectTransform _hitAreaRoot;
        private RectTransform _visualRoot;
        private Image _hitArea;
        private Canvas _visualCanvas;
        private CanvasGroup _visualCanvasGroup;
        private UIBattlePreparationCardView _cardView;
        private Sprite _defaultArtwork;
        private Sprite _defaultTypeIcon;
        private Vector2 _layoutTarget;
        private float _layoutRotationDegrees;
        private Vector2 _layoutStart;
        private float _layoutElapsed;
        private float _layoutDuration;
        private bool _isLayoutAnimating;
        private Vector2 _visualStartPosition;
        private Vector2 _visualTargetPosition;
        private Vector3 _visualStartScale;
        private Vector3 _visualTargetScale;
        private Quaternion _visualStartRotation;
        private Quaternion _visualTargetRotation;
        private float _visualElapsed;
        private float _visualDuration;
        private bool _isVisualAnimating;
        private Vector2 _baseHitAreaSize;
        private int _hoverVisualSortingOrder;
        private bool _isHovered;
        private bool _isDragging;
        private bool _isInteractable = true;

        public BattleHandCardDisplayData Data { get; private set; }
        public string Id => Data != null ? Data.Id : string.Empty;
        public int LogicalIndex { get; private set; } = -1;
        public bool IsHovered => _isHovered;
        public bool IsDragging => _isDragging;

        internal string ReuseKey { get; private set; } = string.Empty;
        internal eBattleHandCardKind Kind => Data != null ? Data.Kind : eBattleHandCardKind.UNIT;
        internal Sprite DefaultArtwork => _defaultArtwork;
        internal Sprite DefaultTypeIcon => _defaultTypeIcon;

        internal void Initialize(
            UIBattleCardHandView owner,
            RectTransform slotRoot,
            RectTransform hitAreaRoot,
            RectTransform visualRoot,
            Image hitArea,
            Canvas visualCanvas,
            CanvasGroup visualCanvasGroup,
            UIBattlePreparationCardView cardView,
            Vector2 baseHitAreaSize,
            int hoverVisualSortingOrder)
        {
            _owner = owner;
            _slotRoot = slotRoot;
            _hitAreaRoot = hitAreaRoot;
            _visualRoot = visualRoot;
            _hitArea = hitArea;
            _visualCanvas = visualCanvas;
            _visualCanvasGroup = visualCanvasGroup;
            _cardView = cardView;
            _defaultArtwork = cardView != null ? cardView.Artwork : null;
            _defaultTypeIcon = cardView != null ? cardView.TypeIcon : null;
            _baseHitAreaSize = new Vector2(
                Mathf.Max(0f, baseHitAreaSize.x),
                Mathf.Max(0f, baseHitAreaSize.y));
            _hoverVisualSortingOrder = hoverVisualSortingOrder;

            if (_hitArea != null)
            {
                _hitArea.color = new Color(1f, 1f, 1f, 0f);
                _hitArea.raycastTarget = _isInteractable;
            }

            if (_visualCanvasGroup != null)
            {
                _visualCanvasGroup.interactable = false;
                _visualCanvasGroup.blocksRaycasts = false;
            }

            ResetHitArea();
            ResetVisualSorting();
            ResetVisualImmediate();
        }

        internal void Bind(BattleHandCardDisplayData data, string reuseKey, int logicalIndex)
        {
            Data = data;
            ReuseKey = reuseKey ?? string.Empty;
            LogicalIndex = logicalIndex;
        }

        internal UIBattlePreparationCardView GetCardView() => _cardView;

        private void Update()
        {
            if (!Application.isPlaying) return;

            float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            UpdateLayoutAnimation(deltaTime);
            UpdateVisualAnimation(deltaTime);
        }

        internal bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            return _hitAreaRoot != null && RectTransformUtility.RectangleContainsScreenPoint(
                _hitAreaRoot, screenPoint, eventCamera);
        }

        internal bool ContainsVisualScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            return _owner != null &&
                   _owner.IsScreenPointInsideViewport(screenPoint, eventCamera) &&
                   _visualRoot != null && RectTransformUtility.RectangleContainsScreenPoint(
                _visualRoot, screenPoint, eventCamera);
        }

        internal void SetLogicalIndex(int logicalIndex)
        {
            LogicalIndex = logicalIndex;
        }

        internal void SetInteractable(bool interactable)
        {
            _isInteractable = interactable;
            if (_hitArea != null) _hitArea.raycastTarget = interactable;

            if (!interactable)
            {
                _isDragging = false;
                SetHoverState(false, 1f, 0f, 0f, true);
            }
        }

        internal void MoveTo(Vector2 target, float rotationDegrees, float duration, bool immediate)
        {
            _layoutTarget = target;
            _layoutRotationDegrees = rotationDegrees;
            StopLayoutAnimation();
            ApplyBaseVisual(duration, immediate);
            if (_slotRoot == null) return;

            if (immediate || duration <= 0f || !Application.isPlaying || !isActiveAndEnabled)
            {
                _slotRoot.anchoredPosition = target;
                return;
            }

            _layoutStart = _slotRoot.anchoredPosition;
            _layoutElapsed = 0f;
            _layoutDuration = duration;
            _isLayoutAnimating = true;
        }

        internal void SetHoverState(
            bool hovered,
            float hoverScale,
            float hoverRise,
            float duration,
            bool immediate)
        {
            _isHovered = hovered;
            StopVisualAnimation();
            UpdateHitArea(hovered, hoverScale, hoverRise);
            UpdateVisualSorting(hovered);

            if (_visualRoot == null) return;

            Vector2 targetPosition = hovered ? new Vector2(0f, Mathf.Max(0f, hoverRise)) : Vector2.zero;
            Vector3 targetScale = Vector3.one * (hovered ? Mathf.Max(0.01f, hoverScale) : 1f);
            Vector3 targetRotation = hovered
                ? Vector3.zero
                : new Vector3(0f, 0f, _layoutRotationDegrees);
            if (immediate || duration <= 0f || !Application.isPlaying || !isActiveAndEnabled)
            {
                _visualRoot.anchoredPosition = targetPosition;
                _visualRoot.localScale = targetScale;
                _visualRoot.localRotation = Quaternion.Euler(targetRotation);
                return;
            }

            StartVisualAnimation(
                targetPosition,
                targetScale,
                Quaternion.Euler(targetRotation),
                duration);
        }

        internal void ResetState(bool snapToLayoutTarget)
        {
            _isDragging = false;
            _isHovered = false;
            StopAllAnimations();
            ResetVisualImmediate();
            ResetHitArea();
            ResetVisualSorting();
            if (snapToLayoutTarget && _slotRoot != null) _slotRoot.anchoredPosition = _layoutTarget;
        }

        internal void Dispose()
        {
            _owner = null;
            _isInteractable = false;
            StopAllAnimations();
            ResetVisualSorting();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isInteractable || _owner == null) return;
            _owner.HandlePointerEnter(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isInteractable || _owner == null || _isDragging) return;
            _owner.HandlePointerExit(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isInteractable || _owner == null) return;
            _owner.HandlePointerClick(this, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_isInteractable || _owner == null) return;
            _isDragging = true;
            _owner.HandleBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || !_isInteractable || _owner == null) return;
            _owner.HandleDrag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;
            if (_owner == null) return;

            _owner.HandleEndDrag(this, eventData);
            if (_owner == null || !isActiveAndEnabled) return;
            Camera eventCamera = eventData != null ? eventData.pressEventCamera : null;
            Vector2 screenPosition = eventData != null ? eventData.position : Vector2.zero;
            if (ContainsScreenPoint(screenPosition, eventCamera))
                _owner.HandlePointerEnter(this);
            else
                _owner.HandlePointerExit(this);
        }

        private void OnDisable()
        {
            ResetState(true);
        }

        private void OnDestroy()
        {
            StopAllAnimations();
        }

        private void UpdateHitArea(bool hovered, float hoverScale, float hoverRise)
        {
            if (_hitAreaRoot == null) return;
            if (!hovered)
            {
                ResetHitArea();
                return;
            }

            float scale = Mathf.Max(1f, hoverScale);
            float baseBottom = -_baseHitAreaSize.y * 0.5f;
            float hoveredBottom = Mathf.Max(0f, hoverRise) - _baseHitAreaSize.y * scale * 0.5f;
            float hoveredTop = Mathf.Max(0f, hoverRise) + _baseHitAreaSize.y * scale * 0.5f;
            float unionBottom = Mathf.Min(baseBottom, hoveredBottom);
            float unionTop = Mathf.Max(_baseHitAreaSize.y * 0.5f, hoveredTop);

            // 너비는 인접 카드 전환을 방해하지 않도록 기본 슬롯 폭을 유지한다.
            _hitAreaRoot.sizeDelta = new Vector2(_baseHitAreaSize.x, unionTop - unionBottom);
            _hitAreaRoot.anchoredPosition = new Vector2(0f, (unionTop + unionBottom) * 0.5f);
        }

        private void ResetHitArea()
        {
            if (_hitAreaRoot == null) return;
            _hitAreaRoot.sizeDelta = _baseHitAreaSize;
            _hitAreaRoot.anchoredPosition = Vector2.zero;
        }

        private void ResetVisualImmediate()
        {
            if (_visualRoot == null) return;
            _visualRoot.anchoredPosition = Vector2.zero;
            _visualRoot.localScale = Vector3.one;
            _visualRoot.localRotation = Quaternion.Euler(0f, 0f, _layoutRotationDegrees);
        }

        private void UpdateVisualSorting(bool hovered)
        {
            if (_visualCanvas == null) return;
            _visualCanvas.overrideSorting = hovered;
            if (hovered) _visualCanvas.sortingOrder = _hoverVisualSortingOrder;
        }

        private void ResetVisualSorting()
        {
            if (_visualCanvas == null) return;
            _visualCanvas.overrideSorting = false;
        }

        private void ApplyBaseVisual(float duration, bool immediate)
        {
            if (_visualRoot == null || _isHovered || _isDragging) return;

            StopVisualAnimation();
            Vector3 targetRotation = new Vector3(0f, 0f, _layoutRotationDegrees);
            if (immediate || duration <= 0f || !Application.isPlaying || !isActiveAndEnabled)
            {
                _visualRoot.anchoredPosition = Vector2.zero;
                _visualRoot.localScale = Vector3.one;
                _visualRoot.localRotation = Quaternion.Euler(targetRotation);
                return;
            }

            StartVisualAnimation(
                Vector2.zero,
                Vector3.one,
                Quaternion.Euler(targetRotation),
                duration);
        }

        private void StartVisualAnimation(
            Vector2 targetPosition,
            Vector3 targetScale,
            Quaternion targetRotation,
            float duration)
        {
            if (_visualRoot == null) return;

            _visualStartPosition = _visualRoot.anchoredPosition;
            _visualTargetPosition = targetPosition;
            _visualStartScale = _visualRoot.localScale;
            _visualTargetScale = targetScale;
            _visualStartRotation = _visualRoot.localRotation;
            _visualTargetRotation = targetRotation;
            _visualElapsed = 0f;
            _visualDuration = Mathf.Max(0.0001f, duration);
            _isVisualAnimating = true;
        }

        private void UpdateLayoutAnimation(float deltaTime)
        {
            if (!_isLayoutAnimating || _slotRoot == null) return;

            _layoutElapsed += deltaTime;
            float progress = Mathf.Clamp01(_layoutElapsed / Mathf.Max(0.0001f, _layoutDuration));
            float eased = EaseOutCubic(progress);
            _slotRoot.anchoredPosition = Vector2.LerpUnclamped(_layoutStart, _layoutTarget, eased);
            if (progress >= 1f) _isLayoutAnimating = false;
        }

        private void UpdateVisualAnimation(float deltaTime)
        {
            if (!_isVisualAnimating || _visualRoot == null) return;

            _visualElapsed += deltaTime;
            float progress = Mathf.Clamp01(_visualElapsed / Mathf.Max(0.0001f, _visualDuration));
            float eased = EaseOutCubic(progress);
            _visualRoot.anchoredPosition = Vector2.LerpUnclamped(
                _visualStartPosition,
                _visualTargetPosition,
                eased);
            _visualRoot.localScale = Vector3.LerpUnclamped(
                _visualStartScale,
                _visualTargetScale,
                eased);
            _visualRoot.localRotation = Quaternion.SlerpUnclamped(
                _visualStartRotation,
                _visualTargetRotation,
                eased);
            if (progress >= 1f) _isVisualAnimating = false;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private void StopAllAnimations()
        {
            StopLayoutAnimation();
            StopVisualAnimation();
        }

        private void StopLayoutAnimation()
        {
            _isLayoutAnimating = false;
        }

        private void StopVisualAnimation()
        {
            _isVisualAnimating = false;
        }
    }
}
