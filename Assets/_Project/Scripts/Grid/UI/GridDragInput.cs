using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.UI
{
    public sealed class GridDragInput : IDisposable
    {
        private readonly GridManager _manager;
        private readonly IGridBoardSurface _board;
        private readonly Func<bool> _canInteract;
        private readonly VisualElement _root;
        private readonly VisualElement _tray;
        private int _pointerId = -1;
        private Vector2Int _grabOffset;
        private Vector2 _pointerPosition;
        public GridDragInput(GridManager manager, IGridBoardSurface board, VisualElement root, VisualElement tray, Func<bool> canInteract = null)
        {
            _canInteract = canInteract ?? (() => true);
            _manager = manager; _board = board; _root = root; _tray = tray;
            root.focusable = true;
            board.Element.RegisterCallback<PointerDownEvent>(OnBoardDown);
            board.Element.RegisterCallback<PointerLeaveEvent>(OnBoardLeave);
            root.RegisterCallback<PointerMoveEvent>(OnMove);
            root.RegisterCallback<PointerUpEvent>(OnUp);
            root.RegisterCallback<PointerCancelEvent>(OnCancel);
            root.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            root.RegisterCallback<KeyDownEvent>(OnKey);
            root.RegisterCallback<FocusOutEvent>(OnFocusOut);
        }
        public void BeginCard(string id, PointerDownEvent evt)
        {
            if (!_canInteract() || evt.button != 0 || !_manager.BeginUnitDrag(id)) return;
            _grabOffset = Vector2Int.zero; Capture(evt); Update(evt.position);
        }
        public void BeginBlockCard(string id, PointerDownEvent evt)
        {
            if (!_canInteract() || evt.button != 0 || !_manager.BeginBlockDrag(id)) return;
            _grabOffset = Vector2Int.zero; Capture(evt); Update(evt.position);
        }
        public void BeginExpansion(PointerDownEvent evt)
        {
            if (!_canInteract() || evt.button != 0 || !_manager.BeginExpansionDrag()) return;
            _grabOffset = Vector2Int.zero; Capture(evt); Update(evt.position);
        }
        private void OnBoardDown(PointerDownEvent evt)
        {
            if (!_canInteract() || evt.button != 0) return;
            var cell = _board.PanelToCell(evt.position);
            var block = _manager.GetBlockAt(cell);
            var unit = _manager.GetUnitAt(cell);
            if (unit != null)
            {
                if (!_manager.BeginUnitDrag(unit.InstanceId)) return;
                _grabOffset = cell - unit.Anchor;
            }
            else if (block != null)
            {
                if (!_manager.BeginBlockDrag(block.InstanceId)) return;
                _grabOffset = cell - block.Anchor;
            }
            else
            {
                var expansion = _manager.GetExpansionAt(cell);
                if (expansion == null || !_manager.BeginExpansionDrag(expansion.InstanceId)) return;
                _grabOffset = cell - expansion.Anchor;
            }
            Capture(evt); Update(evt.position);
        }
        private void Capture(PointerDownEvent evt)
        {
            _pointerId = evt.pointerId; _root.CapturePointer(_pointerId); _root.Focus(); evt.StopPropagation();
        }
        private void Update(Vector2 point)
        { _pointerPosition = point; _manager.MovePreview(_board.PanelToCell(point) - _grabOffset); }
        private void OnMove(PointerMoveEvent evt)
        {
            if (!_canInteract()) { Cancel(); return; }
            if (_pointerId == evt.pointerId && _manager.HasSelection) Update(evt.position);
            else if (!_manager.HasSelection)
                _manager.SetHoveredCell(_board.Element.worldBound.Contains(evt.position) ? _board.PanelToCell(evt.position) : (Vector2Int?)null);
        }
        private void OnBoardLeave(PointerLeaveEvent evt) => _manager.SetHoveredCell(null);
        private void OnUp(PointerUpEvent evt)
        {
            if (_pointerId != evt.pointerId || evt.button != 0) return;
            if (!_canInteract()) { Cancel(); return; }
            string targetId = null;
            // MovePreview가 화면을 갱신하기 전에 현재 카드의 레이아웃으로 드롭 대상을 확정한다.
            if (_tray.worldBound.Contains(evt.position))
                foreach (var card in _tray.Children())
                    if (card.userData is string id && card.worldBound.Contains(evt.position)) { targetId = id; break; }
            Update(evt.position);
            if (_manager.DragKind == eGridDragKind.UNIT && _tray.worldBound.Contains(evt.position))
            {
                if (targetId != null && targetId != _manager.SelectedId)
                {
                    if (!_manager.TryFuseUnits(_manager.SelectedId, targetId)) _manager.CancelDrag();
                }
                else if (!_manager.DropToTray()) _manager.CancelDrag();
            }
            else if (_tray.worldBound.Contains(evt.position))
            { if (!_manager.DropToTray()) _manager.CancelDrag(); }
            else if (!_manager.CommitPreview()) _manager.CancelDrag();
            Release(); evt.StopPropagation();
        }
        private void OnKey(KeyDownEvent evt)
        {
            if (!_canInteract() || !_manager.HasSelection) return;
            if (evt.keyCode == KeyCode.R)
            {
                _manager.RotatePreview();
                _grabOffset = new Vector2Int(_grabOffset.y, -_grabOffset.x);
                Update(_pointerPosition);
            }
            else if (evt.keyCode == KeyCode.F && !_manager.IsExpansionDrag)
            {
                _manager.MirrorPreview();
                _grabOffset = new Vector2Int(-_grabOffset.x, _grabOffset.y);
                Update(_pointerPosition);
            }
            else if (evt.keyCode == KeyCode.Escape) Cancel();
            else return;
            evt.StopPropagation();
        }
        private void OnCancel(PointerCancelEvent evt) { if (evt.pointerId == _pointerId) Cancel(); }
        private void OnCaptureOut(PointerCaptureOutEvent evt) { if (evt.pointerId == _pointerId) Cancel(); }
        private void OnFocusOut(FocusOutEvent evt) { if (evt.target == _root && _manager.HasSelection) Cancel(); }
        public void Cancel() { _manager.CancelDrag(); _manager.SetHoveredCell(null); Release(); }
        private void Release()
        {
            int pointer = _pointerId; _pointerId = -1;
            if (pointer >= 0 && _root.HasPointerCapture(pointer)) _root.ReleasePointer(pointer);
        }
        public void Dispose()
        {
            Cancel();
            _board.Element.UnregisterCallback<PointerDownEvent>(OnBoardDown);
            _board.Element.UnregisterCallback<PointerLeaveEvent>(OnBoardLeave);
            _root.UnregisterCallback<PointerMoveEvent>(OnMove); _root.UnregisterCallback<PointerUpEvent>(OnUp);
            _root.UnregisterCallback<PointerCancelEvent>(OnCancel); _root.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            _root.UnregisterCallback<KeyDownEvent>(OnKey); _root.UnregisterCallback<FocusOutEvent>(OnFocusOut);
        }
    }
}
