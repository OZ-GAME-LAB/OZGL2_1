using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.UI
{
    public sealed class GridDragInput : IDisposable
    {
        private readonly GridManager _manager;
        private readonly GridBoardView _board;
        private readonly VisualElement _root;
        private readonly VisualElement _tray;
        private readonly VisualElement _blockTray;
        private int _pointerId = -1;
        private Vector2Int _grabOffset;
        public GridDragInput(GridManager manager, GridBoardView board, VisualElement root, VisualElement tray, VisualElement blockTray)
        {
            _manager = manager; _board = board; _root = root; _tray = tray;
            _blockTray = blockTray;
            root.focusable = true;
            board.Element.RegisterCallback<PointerDownEvent>(OnBoardDown);
            root.RegisterCallback<PointerMoveEvent>(OnMove);
            root.RegisterCallback<PointerUpEvent>(OnUp);
            root.RegisterCallback<PointerCancelEvent>(OnCancel);
            root.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            root.RegisterCallback<KeyDownEvent>(OnKey);
            root.RegisterCallback<FocusOutEvent>(OnFocusOut);
        }
        public void BeginCard(string id, PointerDownEvent evt)
        {
            if (evt.button != 0 || !_manager.BeginUnitDrag(id)) return;
            _grabOffset = Vector2Int.zero; Capture(evt); Update(evt.position);
        }
        public void BeginBlockCard(string id, PointerDownEvent evt)
        {
            if (evt.button != 0 || !_manager.BeginBlockDrag(id)) return;
            _grabOffset = Vector2Int.zero; Capture(evt); Update(evt.position);
        }
        public void BeginExpansion(PointerDownEvent evt)
        {
            if (evt.button != 0 || !_manager.BeginExpansionDrag()) return;
            _grabOffset = Vector2Int.zero; Capture(evt); Update(evt.position);
        }
        private void OnBoardDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            var cell = _board.PanelToCell(evt.position);
            var block = _manager.GetBlockAt(cell);
            if (block == null) return;
            var unit = _manager.GetUnitOnBlock(block.InstanceId);
            if (!evt.shiftKey && unit != null && cell == block.Anchor)
            {
                if (!_manager.BeginUnitDrag(unit.InstanceId)) return;
                _grabOffset = Vector2Int.zero;
            }
            else
            {
                if (!_manager.BeginBlockDrag(block.InstanceId)) return;
                _grabOffset = cell - block.Anchor;
            }
            Capture(evt); Update(evt.position);
        }
        private void Capture(PointerDownEvent evt)
        {
            _pointerId = evt.pointerId; _root.CapturePointer(_pointerId); _root.Focus(); evt.StopPropagation();
        }
        private void Update(Vector2 point) => _manager.MovePreview(_board.PanelToCell(point) - _grabOffset);
        private void OnMove(PointerMoveEvent evt) { if (_pointerId == evt.pointerId && _manager.HasSelection) Update(evt.position); }
        private void OnUp(PointerUpEvent evt)
        {
            if (_pointerId != evt.pointerId || evt.button != 0) return;
            Update(evt.position);
            if ((_manager.DragKind == eGridDragKind.UNIT && _tray.worldBound.Contains(evt.position)) ||
                (_manager.DragKind == eGridDragKind.BLOCK && _blockTray.worldBound.Contains(evt.position))) _manager.DropToTray();
            else if (!_manager.CommitPreview()) _manager.CancelDrag();
            Release(); evt.StopPropagation();
        }
        private void OnKey(KeyDownEvent evt)
        {
            if (!_manager.HasSelection) return;
            if (evt.keyCode == KeyCode.R) _manager.RotatePreview();
            else if (evt.keyCode == KeyCode.Escape) Cancel();
            else return;
            evt.StopPropagation();
        }
        private void OnCancel(PointerCancelEvent evt) { if (evt.pointerId == _pointerId) Cancel(); }
        private void OnCaptureOut(PointerCaptureOutEvent evt) { if (evt.pointerId == _pointerId) Cancel(); }
        private void OnFocusOut(FocusOutEvent evt) { if (evt.target == _root && _manager.HasSelection) Cancel(); }
        public void Cancel() { _manager.CancelDrag(); Release(); }
        private void Release()
        {
            int pointer = _pointerId; _pointerId = -1;
            if (pointer >= 0 && _root.HasPointerCapture(pointer)) _root.ReleasePointer(pointer);
        }
        public void Dispose()
        {
            Cancel();
            _board.Element.UnregisterCallback<PointerDownEvent>(OnBoardDown);
            _root.UnregisterCallback<PointerMoveEvent>(OnMove); _root.UnregisterCallback<PointerUpEvent>(OnUp);
            _root.UnregisterCallback<PointerCancelEvent>(OnCancel); _root.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            _root.UnregisterCallback<KeyDownEvent>(OnKey); _root.UnregisterCallback<FocusOutEvent>(OnFocusOut);
        }
    }
}
