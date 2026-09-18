using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    [DisallowMultipleComponent]
    public sealed class UITraitTooltipController : MonoBehaviour
    {
        [SerializeField] private RectTransform _tooltipLayer;
        [SerializeField] private UITraitTooltipView _tooltipView;
        [SerializeField] private UIHoverRing _hoverRing;
        [SerializeField] private ScrollRect _treeScroll;
        [SerializeField] private Canvas _canvas;
        [Min(0.05f)] [SerializeField] private float _hoverDelay = 0.5f;
        [SerializeField] private Vector2 _pointerOffset = new Vector2(20f, 20f);
        [Min(0f)] [SerializeField] private float _screenPadding = 12f;

        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
        private UITraitNodeView _hoveredNode;
        private PointerEventData _pointer;
        private RectTransform _tooltipRect;
        private float _hoverStartedAt;
        private bool _isSuppressedUntilExit;
        public bool IsShowing => _tooltipView != null && _tooltipView.gameObject.activeSelf;

        public void BeginHover(UITraitNodeView node, PointerEventData pointer)
        {
            if (!isActiveAndEnabled || node == null || pointer == null || !node.CanReceiveInput ||
                string.IsNullOrWhiteSpace(node.Data.TraitId)) return;
            // 같은 노드의 이벤트 객체가 바뀌어도 현재 방문의 클릭 차단은 유지한다.
            if (_hoveredNode == node)
            {
                _pointer = pointer;
                return;
            }
            CancelHover();
            _hoveredNode = node;
            _pointer = pointer;
            ResetDelay();
        }

        public void EndHover(UITraitNodeView node)
        {
            if (_hoveredNode == node) CancelHover();
        }

        public void SuppressHoverUntilExit(UITraitNodeView node)
        {
            if (!isActiveAndEnabled || node == null || _hoveredNode != node) return;
            _isSuppressedUntilExit = true;
            HideVisuals();
        }

        public void RefreshTrait(UITraitNodeView node)
        {
            if (!_isSuppressedUntilExit && _hoveredNode == node && IsShowing) _tooltipView.ShowTrait(node.Data);
        }

        public void CancelHover()
        {
            _hoveredNode = null;
            _pointer = null;
            _isSuppressedUntilExit = false;
            HideVisuals();
        }

        private void OnEnable()
        {
            _tooltipRect = _tooltipView != null ? _tooltipView.transform as RectTransform : null;
            if (_treeScroll != null) _treeScroll.onValueChanged.AddListener(HandleScroll);
            CancelHover();
        }

        private void OnDisable()
        {
            if (_treeScroll != null) _treeScroll.onValueChanged.RemoveListener(HandleScroll);
            CancelHover();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelHover();
        }

        private void LateUpdate()
        {
            if (_hoveredNode == null) return;
            if (_tooltipLayer == null || _tooltipRect == null || _hoverRing == null ||
                _pointer == null || !_hoveredNode.CanReceiveInput || !IsPointerOverNode())
            {
                CancelHover();
                return;
            }
            // 이탈 여부는 계속 확인하되, 클릭한 방문에서는 타이머와 표시를 재시작하지 않는다.
            if (_isSuppressedUntilExit) return;
            // Input System은 같은 이벤트 객체를 좌/우/중간 버튼에 재사용하므로
            // LateUpdate에서는 실제 왼쪽 버튼/휠 상태도 함께 확인한다.
            Mouse mouse = Mouse.current;
            bool hasMouseInput = mouse != null && (mouse.leftButton.isPressed || mouse.scroll.ReadValue().sqrMagnitude > 0f);
            if (_pointer.dragging || _pointer.pointerPress != null || hasMouseInput)
            {
                ResetDelay();
                HideVisuals();
                return;
            }

            float progress = Mathf.Clamp01((Time.unscaledTime - _hoverStartedAt) / Mathf.Max(0.05f, _hoverDelay));
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (progress < 1f)
            {
                _hoverRing.gameObject.SetActive(true);
                _hoverRing.SetRemaining(1f - progress);
                Vector2 nodeScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, _hoveredNode.transform.position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_tooltipLayer, nodeScreen, eventCamera, out Vector2 nodeLocal))
                    _hoverRing.rectTransform.localPosition = new Vector3(nodeLocal.x, nodeLocal.y, 0f);
                return;
            }

            _hoverRing.SetRemaining(0f);
            _hoverRing.gameObject.SetActive(false);
            if (!IsShowing)
            {
                _tooltipView.ShowTrait(_hoveredNode.Data);
                _tooltipView.gameObject.SetActive(true);
            }
            PlaceTooltip(eventCamera);
        }

        private bool IsPointerOverNode()
        {
            if (EventSystem.current == null) return false;
            _raycastResults.Clear();
            EventSystem.current.RaycastAll(_pointer, _raycastResults);
            if (_raycastResults.Count == 0) return false;
            // 다른 팝업 또는 스크롤 마스크에 가려진 노드에는 표시하지 않는다.
            Transform hit = _raycastResults[0].gameObject.transform;
            return hit == _hoveredNode.transform || hit.IsChildOf(_hoveredNode.transform);
        }

        private void PlaceTooltip(Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_tooltipLayer, _pointer.position, eventCamera, out Vector2 point)) return;
            // Prefab의 우상단 pivot(1, 1)을 포인터 왼쪽 아래에 둔다.
            Vector2 position = point - new Vector2(Mathf.Abs(_pointerOffset.x), Mathf.Abs(_pointerOffset.y));
            Rect bounds = _tooltipLayer.rect;
            Vector2 size = _tooltipRect.rect.size;
            float minX = bounds.xMin + _screenPadding + size.x * _tooltipRect.pivot.x;
            float maxX = bounds.xMax - _screenPadding - size.x * (1f - _tooltipRect.pivot.x);
            float minY = bounds.yMin + _screenPadding + size.y * _tooltipRect.pivot.y;
            float maxY = bounds.yMax - _screenPadding - size.y * (1f - _tooltipRect.pivot.y);
            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : bounds.center.x;
            position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : bounds.center.y;
            _tooltipRect.localPosition = new Vector3(position.x, position.y, 0f);
        }

        private void HandleScroll(Vector2 position)
        {
            if (_hoveredNode == null || _isSuppressedUntilExit) return;
            ResetDelay();
            HideVisuals();
        }

        private void ResetDelay()
        {
            _hoverStartedAt = Time.unscaledTime;
            HideVisuals();
            if (_hoverRing != null) _hoverRing.SetRemaining(1f);
        }

        private void HideVisuals()
        {
            if (_tooltipView != null) _tooltipView.gameObject.SetActive(false);
            if (_hoverRing != null) _hoverRing.gameObject.SetActive(false);
        }
    }
}
