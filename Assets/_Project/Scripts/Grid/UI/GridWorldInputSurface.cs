using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.UI
{
    /// <summary>투명한 UI 입력 영역을 월드 XY 배치판 좌표로 변환한다.</summary>
    public sealed class GridWorldInputSurface : IGridBoardSurface, IDisposable
    {
        private readonly Camera _camera;
        private readonly Vector3 _origin;
        private readonly float _cellSize;
        private readonly Action _render;
        private readonly Action<Vector2Int?> _hover;
        private readonly Func<bool> _canInteract;
        public VisualElement Element { get; } = new VisualElement { name = "world-grid-input" };
        public GridWorldInputSurface(Camera camera, Vector3 origin, float cellSize, Action render,
            Action<Vector2Int?> hover = null, Func<bool> canInteract = null)
        {
            _camera = camera; _origin = origin; _cellSize = cellSize; _render = render; _hover = hover; _canInteract = canInteract;
            Element.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            Element.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            Element.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            Element.RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }
        private void OnPointerMove(PointerMoveEvent evt)
            => _hover?.Invoke((_canInteract == null || _canInteract()) ? PanelToCell(evt.position) : (Vector2Int?)null);
        private void OnPointerLeave(PointerLeaveEvent evt) => _hover?.Invoke(null);
        private void OnPointerCancel(PointerCancelEvent evt) => _hover?.Invoke(null);
        private void OnDetach(DetachFromPanelEvent evt) => _hover?.Invoke(null);
        public Vector2Int ScreenToCell(Vector2 position)
        {
            if (_camera == null || !float.IsFinite(position.x) || !float.IsFinite(position.y)) return new Vector2Int(-1000, -1000);
            var ray = _camera.ScreenPointToRay(position);
            float distance;
            if (!new Plane(Vector3.forward, _origin).Raycast(ray, out distance)) return new Vector2Int(-1000, -1000);
            var point = ray.GetPoint(distance) - _origin;
            return new Vector2Int(Mathf.FloorToInt(point.x / _cellSize + 0.5f), Mathf.FloorToInt(point.y / _cellSize + 0.5f));
        }
        public Vector2Int PanelToCell(Vector2 position)
        {
            var panel = Element.panel;
            if (panel == null) return new Vector2Int(-1000, -1000);
            var size = panel.visualTree.layout.size;
            if (!float.IsFinite(size.x) || !float.IsFinite(size.y) || size.x <= 0 || size.y <= 0) return new Vector2Int(-1000, -1000);
            return ScreenToCell(new Vector2(position.x * Screen.width / size.x, Screen.height - position.y * Screen.height / size.y));
        }
        public Vector2 CellToPanel(Vector2 cell)
        {
            var screen = _camera.WorldToScreenPoint(_origin + new Vector3(cell.x, cell.y, 0) * _cellSize);
            var size = Element.panel.visualTree.layout.size;
            return new Vector2(screen.x * size.x / Screen.width, (Screen.height - screen.y) * size.y / Screen.height);
        }
        public void Render() => _render?.Invoke();
        public void Dispose()
        {
            _hover?.Invoke(null);
            Element.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            Element.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            Element.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            Element.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
        }
    }
}
