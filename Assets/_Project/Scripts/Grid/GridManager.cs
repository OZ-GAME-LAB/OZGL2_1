using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace OZGL2.Grid
{
    /// <summary>Unity 화면/전투/지급 구현에 의존하지 않는 준비 단계 배치 조정자.</summary>
    public sealed class GridManager : IGridReadModel
    {
        private readonly HashSet<Vector2Int> _floor = new HashSet<Vector2Int>();
        private readonly List<BlockPlacement> _blocks = new List<BlockPlacement>();
        private readonly ReadOnlyCollection<BlockPlacement> _blockView;
        private readonly List<UnitPlacement> _units = new List<UnitPlacement>();
        private readonly ReadOnlyCollection<UnitPlacement> _unitView;
        private ReadOnlyCollection<Vector2Int> _floorView;
        private string _selectedId;
        public eGridDragKind DragKind { get; private set; }
        private Vector2Int _previewAnchor;
        private int _previewRotation;
        private bool _canSkipPreparation;
        public GridDefinition Definition { get; }
        public eGridPhase Phase { get; private set; } = eGridPhase.WAITING;
        public event Action Changed;
        /// <summary>기물 추가 또는 실제 배치/해제/확장 확정 후 통지. 미리보기에는 발생하지 않는다.</summary>
        public event Action LayoutChanged;
        public Exception LastObserverError { get; private set; }
        public IReadOnlyList<BlockPlacement> Blocks => _blockView;
        public IReadOnlyList<UnitPlacement> Units => _unitView;
        public IReadOnlyCollection<Vector2Int> FloorCells => _floorView;
        public bool RequiresExpansionPlacement { get; private set; }
        public bool HasSelection => DragKind != eGridDragKind.NONE;
        public bool IsExpansionDrag => DragKind == eGridDragKind.EXPANSION;
        public string SelectedId => _selectedId;
        public Vector2Int PreviewAnchor => _previewAnchor;
        public int PreviewRotation => _previewRotation;
        public bool CanBeginBattle => Phase == eGridPhase.PREPARATION && !RequiresExpansionPlacement && !HasSelection && PlacedCount > 0;
        public bool CanSkipPreparation => _canSkipPreparation && CanBeginBattle;
        public int PlacedCount { get { int count = 0; foreach (var unit in _units) if (unit.IsPlaced) count++; return count; } }
        public GridManager(GridDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _blockView = _blocks.AsReadOnly();
            _unitView = _units.AsReadOnly();
            for (int y = 0; y < definition.InitialSize.y; y++)
                for (int x = 0; x < definition.InitialSize.x; x++) _floor.Add(definition.InitialOrigin + new Vector2Int(x, y));
            UpdateFloorView();
        }
        public void AddBlock(string instanceId, string contentId, FootprintDefinition footprint)
        {
            if (Phase == eGridPhase.BATTLE || Phase == eGridPhase.ENDED) throw new InvalidOperationException("Cannot grant content during battle or after shutdown.");
            if (FindBlock(instanceId) != null) throw new ArgumentException("Duplicate unit instance ID.");
            _blocks.Add(new BlockPlacement(instanceId, contentId, footprint)); Notify(true);
        }
        public BlockPlacement FindBlock(string id) => _blocks.Find(unit => unit.InstanceId == id);
        public void AddUnit(string instanceId, UnitDefinition definition)
        {
            if (Phase == eGridPhase.BATTLE || Phase == eGridPhase.ENDED) throw new InvalidOperationException("Cannot grant content during battle or after shutdown.");
            if (FindUnit(instanceId) != null) throw new ArgumentException("Duplicate unit instance ID.");
            _units.Add(new UnitPlacement(instanceId, definition)); Notify(true);
        }
        public UnitPlacement FindUnit(string id) => _units.Find(unit => unit.InstanceId == id);
        public UnitPlacement GetUnitOnBlock(string blockId) => _units.Find(unit => unit.IsPlaced && unit.BlockId == blockId);
        public BlockPlacement PreviewTargetBlock => DragKind == eGridDragKind.UNIT ? GetBlockAt(_previewAnchor) : null;
        public bool IsUnitTemporarilyReturned(UnitPlacement unit) => unit.IsPlaced && DragKind == eGridDragKind.BLOCK && unit.BlockId == _selectedId;
        public BlockPlacement GetBlockAt(Vector2Int cell)
        {
            foreach (var unit in _blocks)
                if (unit.IsPlaced)
                    foreach (var occupied in unit.Footprint.GetCells(unit.Anchor, unit.Rotation)) if (occupied == cell) return unit;
            return null;
        }
        public bool HasFloor(Vector2Int cell) => _floor.Contains(cell);
        public bool CanExpand
        {
            get
            {
                for (int y = 0; y < Definition.MaximumSize.y; y++)
                    for (int x = 0; x < Definition.MaximumSize.x; x++)
                        for (int rotation = 0; rotation < 4; rotation++)
                            if (GridPlacementRules.ValidateExpansion(Definition, _floor,
                                Definition.Expansion.GetCells(new Vector2Int(x, y), rotation)) == ePlacementFailure.NONE) return true;
                return false;
            }
        }
        public IReadOnlyCollection<Vector2Int> GetExpansionFrontier()
        {
            var result = new HashSet<Vector2Int>();
            for (int y = 0; y < Definition.MaximumSize.y; y++)
                for (int x = 0; x < Definition.MaximumSize.x; x++)
                    for (int rotation = 0; rotation < 4; rotation++)
                    {
                        var cells = Definition.Expansion.GetCells(new Vector2Int(x, y), rotation);
                        if (GridPlacementRules.ValidateExpansion(Definition, _floor, cells) == ePlacementFailure.NONE)
                            foreach (var cell in cells) result.Add(cell);
                    }
            return new List<Vector2Int>(result).AsReadOnly();
        }
        internal bool TryAllowPreparation(bool canSkip)
        {
            if (Phase != eGridPhase.REWARD && Phase != eGridPhase.WAITING) return false;
            _canSkipPreparation = canSkip;
            Phase = eGridPhase.PREPARATION; Notify(); return true;
        }
        internal bool TryAcceptExpansionReward()
        {
            if (Phase != eGridPhase.REWARD || !CanExpand) return false;
            RequiresExpansionPlacement = true; Notify(); return true;
        }
        internal bool TryAcceptUnitReward(UnitPlacement unit, BlockPlacement block)
        {
            if (Phase != eGridPhase.REWARD || unit.IsPlaced || block.IsPlaced ||
                FindUnit(unit.InstanceId) != null || FindBlock(block.InstanceId) != null ||
                unit.Definition.RequiredBlockId != block.Footprint.Id) return false;
            // 지급만 확정한다. 증강·저장이 끝났는지는 외부 진행 소유자가 판단한다.
            _units.Add(unit); _blocks.Add(block);
            RequiresExpansionPlacement = false;
            Notify(true); return true;
        }
        internal bool TryBeginBattle()
        {
            if (!CanBeginBattle) return false;
            Phase = eGridPhase.BATTLE; Notify(); return true;
        }
        internal bool TryFinishBattle()
        {
            if (Phase != eGridPhase.BATTLE) return false;
            Phase = eGridPhase.REWARD; Notify(); return true;
        }
        internal void EndRun()
        {
            if (Phase == eGridPhase.ENDED) return;
            Phase = eGridPhase.ENDED; ClearSelection(); Notify();
        }
        public bool BeginBlockDrag(string id)
        {
            var unit = FindBlock(id);
            if (Phase != eGridPhase.PREPARATION || HasSelection || unit == null) return false;
            _selectedId = id; DragKind = eGridDragKind.BLOCK; _previewAnchor = unit.Anchor; _previewRotation = unit.Rotation;
            Notify(); return true;
        }
        public bool BeginUnitDrag(string id)
        {
            var unit = FindUnit(id);
            if (Phase != eGridPhase.PREPARATION || HasSelection || unit == null) return false;
            _selectedId = id; DragKind = eGridDragKind.UNIT; _previewRotation = 0;
            _previewAnchor = unit.IsPlaced ? FindBlock(unit.BlockId).Anchor : new Vector2Int(-1, -1);
            Notify(); return true;
        }
        public bool BeginExpansionDrag()
        {
            if (Phase != eGridPhase.PREPARATION || !RequiresExpansionPlacement || HasSelection) return false;
            DragKind = eGridDragKind.EXPANSION; _selectedId = null; _previewRotation = 0; _previewAnchor = new Vector2Int(-1, -1);
            Notify(); return true;
        }
        public void MovePreview(Vector2Int anchor) { if (!HasSelection) return; _previewAnchor = anchor; Notify(); }
        public void RotatePreview() { if (!HasSelection || DragKind == eGridDragKind.UNIT) return; _previewRotation = (_previewRotation + 1) % 4; Notify(); }
        public Vector2Int[] GetPreviewCells()
        {
            if (!HasSelection) return Array.Empty<Vector2Int>();
            if (DragKind == eGridDragKind.UNIT)
            {
                var target = PreviewTargetBlock;
                return target == null ? new[] { _previewAnchor } : target.Footprint.GetCells(target.Anchor, target.Rotation);
            }
            return (IsExpansionDrag ? Definition.Expansion : FindBlock(_selectedId).Footprint).GetCells(_previewAnchor, _previewRotation);
        }
        public ePlacementFailure GetPreviewFailure()
        {
            if (Phase != eGridPhase.PREPARATION) return ePlacementFailure.NOT_PREPARING;
            if (!HasSelection) return ePlacementFailure.NO_SELECTION;
            if (DragKind == eGridDragKind.UNIT)
                return GridPlacementRules.ValidateUnit(FindUnit(_selectedId), PreviewTargetBlock, _units);
            return IsExpansionDrag ? GridPlacementRules.ValidateExpansion(Definition, _floor, GetPreviewCells()) :
                GridPlacementRules.ValidateBlock(Definition, _floor, _blocks, _selectedId, GetPreviewCells());
        }
        public bool CommitPreview()
        {
            if (GetPreviewFailure() != ePlacementFailure.NONE) return false;
            if (IsExpansionDrag)
            {
                foreach (var cell in GetPreviewCells()) _floor.Add(cell);
                UpdateFloorView(); RequiresExpansionPlacement = false;
            }
            else if (DragKind == eGridDragKind.UNIT)
            {
                int index = _units.FindIndex(unit => unit.InstanceId == _selectedId);
                _units[index] = _units[index].WithBlock(PreviewTargetBlock.InstanceId);
            }
            else
            {
                int index = _blocks.FindIndex(unit => unit.InstanceId == _selectedId);
                var block = _blocks[index];
                if (block.IsPlaced && block.Anchor == _previewAnchor && block.Rotation == _previewRotation)
                {
                    ClearSelection(); Notify(); return true;
                }
                ReturnUnitFromBlock(_selectedId);
                _blocks[index] = _blocks[index].WithPlacement(true, _previewAnchor, _previewRotation);
            }
            ClearSelection(); Notify(true); return true;
        }
        public bool DropToTray()
        {
            if (Phase != eGridPhase.PREPARATION || _selectedId == null) return false;
            if (DragKind == eGridDragKind.UNIT)
            {
                int index = _units.FindIndex(unit => unit.InstanceId == _selectedId);
                _units[index] = _units[index].WithBlock(null);
            }
            else
            {
                int index = _blocks.FindIndex(unit => unit.InstanceId == _selectedId);
                ReturnUnitFromBlock(_selectedId);
                _blocks[index] = _blocks[index].WithPlacement(false, _blocks[index].Anchor, _blocks[index].Rotation);
            }
            ClearSelection(); Notify(true); return true;
        }
        public void CancelDrag() { if (!HasSelection) return; ClearSelection(); Notify(); }
        private void ReturnUnitFromBlock(string blockId)
        {
            int index = _units.FindIndex(unit => unit.IsPlaced && unit.BlockId == blockId);
            if (index >= 0) _units[index] = _units[index].WithBlock(null);
        }
        private void ClearSelection() { _selectedId = null; DragKind = eGridDragKind.NONE; }
        private void UpdateFloorView() => _floorView = new List<Vector2Int>(_floor).AsReadOnly();
        private void Notify(bool hasLayoutChanged = false)
        {
            NotifyObservers(Changed);
            if (hasLayoutChanged) NotifyObservers(LayoutChanged);
        }
        private void NotifyObservers(Action observers)
        {
            if (observers == null) return;
            foreach (Action observer in observers.GetInvocationList())
                try { observer(); } catch (Exception exception) { LastObserverError = exception; }
        }
    }
}
