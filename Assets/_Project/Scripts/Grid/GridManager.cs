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
        private readonly List<ExpansionPlacement> _expansions = new List<ExpansionPlacement>();
        private readonly ReadOnlyCollection<ExpansionPlacement> _expansionView;
        private readonly List<BlockPlacement> _blocks = new List<BlockPlacement>();
        private readonly ReadOnlyCollection<BlockPlacement> _blockView;
        private readonly List<UnitPlacement> _units = new List<UnitPlacement>();
        private readonly ReadOnlyCollection<UnitPlacement> _unitView;
        private ReadOnlyCollection<Vector2Int> _floorView;
        private string _selectedId;
        public eGridDragKind DragKind { get; private set; }
        private Vector2Int _previewAnchor;
        private int _previewRotation;
        private bool _previewIsMirrored;
        private bool _canSkipPreparation;
        private bool _isNotifying;
        private readonly Func<UnitPlacement, UnitPlacement, bool> _canFuse;
        public GridStorageRequest PendingStorage { get; private set; }
        public bool HasPendingStorage => PendingStorage != null;
        public int StoredCount => CountStored(_blocks, _units);
        public GridDefinition Definition { get; }
        public eGridPhase Phase { get; private set; } = eGridPhase.WAITING;
        public event Action Changed;
        /// <summary>기물 추가 또는 실제 배치/해제/확장 확정 후 통지. 미리보기에는 발생하지 않는다.</summary>
        public event Action LayoutChanged;
        public Exception LastObserverError { get; private set; }
        public ePlacementFailure LastDropFailure { get; private set; }
        public IReadOnlyList<BlockPlacement> Blocks => _blockView;
        public IReadOnlyList<UnitPlacement> Units => _unitView;
        public IReadOnlyCollection<Vector2Int> FloorCells => _floorView;
        public IReadOnlyList<ExpansionPlacement> Expansions => _expansionView;
        public string HoveredExpansionId { get; private set; }
        public bool RequiresExpansionPlacement { get; private set; }
        public bool HasSelection => DragKind != eGridDragKind.NONE;
        public bool IsExpansionDrag => DragKind == eGridDragKind.EXPANSION;
        public string SelectedId => _selectedId;
        public Vector2Int PreviewAnchor => _previewAnchor;
        public int PreviewRotation => _previewRotation;
        public bool PreviewIsMirrored => _previewIsMirrored;
        public bool CanBeginBattle => Phase == eGridPhase.PREPARATION && !RequiresExpansionPlacement && !HasSelection &&
            !HasPendingStorage && PlacedCount > 0 && GetInvalidCells().Count == 0;
        public bool CanSkipPreparation => _canSkipPreparation && CanBeginBattle;
        public int PlacedCount { get { int count = 0; foreach (var unit in _units) if (unit.IsPlaced) count++; return count; } }
        public GridManager(GridDefinition definition, Func<UnitPlacement, UnitPlacement, bool> canFuse = null)
        {
            _canFuse = canFuse;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _blockView = _blocks.AsReadOnly();
            _unitView = _units.AsReadOnly();
            _expansionView = _expansions.AsReadOnly();
            for (int y = 0; y < definition.InitialSize.y; y++)
                for (int x = 0; x < definition.InitialSize.x; x++) _floor.Add(definition.InitialOrigin + new Vector2Int(x, y));
            UpdateFloorView();
        }
        public void AddBlock(string instanceId, string contentId, FootprintDefinition footprint)
        {
            if (Phase == eGridPhase.BATTLE || Phase == eGridPhase.ENDED || HasPendingStorage || HasSelection || _isNotifying) throw new InvalidOperationException("Cannot grant content during battle or after shutdown.");
            if (StoredCount >= Definition.StorageCapacity) throw new InvalidOperationException("Storage full.");
            if (FindBlock(instanceId) != null) throw new ArgumentException("Duplicate unit instance ID.");
            _blocks.Add(new BlockPlacement(instanceId, contentId, footprint)); Notify(true);
        }
        public BlockPlacement FindBlock(string id) => _blocks.Find(unit => unit.InstanceId == id);
        public void AddUnit(string instanceId, UnitDefinition definition)
        {
            if (Phase == eGridPhase.BATTLE || Phase == eGridPhase.ENDED || HasPendingStorage || HasSelection || _isNotifying) throw new InvalidOperationException("Cannot grant content during battle or after shutdown.");
            if (StoredCount >= Definition.StorageCapacity) throw new InvalidOperationException("Storage full.");
            if (FindUnit(instanceId) != null) throw new ArgumentException("Duplicate unit instance ID.");
            _units.Add(new UnitPlacement(instanceId, definition)); Notify(true);
        }
        public UnitPlacement FindUnit(string id) => _units.Find(unit => unit.InstanceId == id);
        public bool CanFuseUnits(string sourceId, string targetId)
        {
            if (Phase != eGridPhase.PREPARATION || HasPendingStorage || sourceId == targetId ||
                (HasSelection && (DragKind != eGridDragKind.UNIT || SelectedId != sourceId))) return false;
            var source = FindUnit(sourceId);
            var target = FindUnit(targetId);
            // 성급이 오르며 발판이 커져서(예: 1x1 → 4칸) 지금 자리에 다 못 들어가도 합성 자체는 막지 않는다.
            // 대신 자리가 부족한 채로 남아있는 동안은 GetInvalidCells()가 그 칸들을 표시하고
            // CanBeginBattle이 false가 되어 정리하기 전까진 웨이브를 시작할 수 없게 한다.
            return source != null && target != null && _canFuse != null && _canFuse(source, target);
        }
        /// <summary>대상 위치와 발판을 보존하고 소모된 유닛의 점유만 반환한다.</summary>
        public bool TryFuseUnits(string sourceId, string targetId)
        {
            if (_isNotifying || !CanFuseUnits(sourceId, targetId)) return false;
            var target = FindUnit(targetId);
            var units = new List<UnitPlacement>(_units);
            units.RemoveAll(unit => unit.InstanceId == sourceId);
            int index = units.FindIndex(unit => unit.InstanceId == targetId);
            units[index] = target.WithStarLevel(target.StarLevel + 1);
            ClearSelection();
            ApplyState(new List<BlockPlacement>(_blocks), units, null);
            return true;
        }
        /// <summary>
        /// 합성으로 발판이 커져서(성급 상승) 지금 자리에 다 못 들어가는 유닛들이 차지하려는 칸 전체.
        /// 비어있지 않으면 화면에서 빨갛게 표시하고, CanBeginBattle이 false가 되어 웨이브 시작을 막는다.
        /// </summary>
        public IReadOnlyCollection<Vector2Int> GetInvalidCells(string excludedUnitId = null)
        {
            var result = new HashSet<Vector2Int>();
            foreach (var unit in _units)
            {
                if (!unit.IsPlaced || unit.InstanceId == excludedUnitId) continue;
                if (GridPlacementRules.ValidateUnit(Definition, _blocks, _units, unit.InstanceId, unit.GetCells()) != ePlacementFailure.NONE)
                    result.UnionWith(unit.GetCells());
            }
            return result;
        }
        public IReadOnlyCollection<Vector2Int> GetOccupiedCells(string excludedUnitId = null)
        {
            var result = new HashSet<Vector2Int>();
            foreach (var unit in _units)
                if (unit.IsPlaced && unit.InstanceId != excludedUnitId) result.UnionWith(unit.GetCells());
            return result;
        }
        public bool CanFusePreview => DragKind == eGridDragKind.UNIT &&
            CanFuseUnits(SelectedId, GetUnitAt(_previewAnchor)?.InstanceId);
        public UnitPlacement GetUnitAt(Vector2Int cell)
        {
            foreach (var unit in _units)
                if (unit.IsPlaced && Array.IndexOf(unit.GetCells(), cell) >= 0) return unit;
            return null;
        }
        public IReadOnlyList<UnitPlacement> GetUnitsOnBlock(string blockId)
        {
            var result = new List<UnitPlacement>();
            var block = FindBlock(blockId);
            if (block == null || !block.IsPlaced) return result.AsReadOnly();
            var cells = new HashSet<Vector2Int>(block.GetCells());
            foreach (var unit in _units)
                if (unit.IsPlaced && cells.Overlaps(unit.GetCells())) result.Add(unit);
            return result.AsReadOnly();
        }
        public BlockPlacement GetBlockAt(Vector2Int cell)
        {
            foreach (var unit in _blocks)
                if (unit.IsPlaced)
                    foreach (var occupied in unit.GetCells()) if (occupied == cell) return unit;
            return null;
        }
        public bool HasFloor(Vector2Int cell) => _floor.Contains(cell);
        public ExpansionPlacement FindExpansion(string id) => _expansions.Find(item => item.InstanceId == id);
        public ExpansionPlacement GetExpansionAt(Vector2Int cell)
        {
            foreach (var expansion in _expansions)
                if (Array.IndexOf(expansion.GetCells(), cell) >= 0) return expansion;
            return null;
        }
        public void SetHoveredCell(Vector2Int? cell)
        {
            if (_isNotifying) return;
            string id = Phase == eGridPhase.PREPARATION && !HasSelection && !HasPendingStorage && cell.HasValue &&
                GetUnitAt(cell.Value) == null && GetBlockAt(cell.Value) == null ? GetExpansionAt(cell.Value)?.InstanceId : null;
            if (HoveredExpansionId == id) return;
            HoveredExpansionId = id; Notify();
        }
        public ePlacementFailure GetExpansionMoveFailure(string id)
        {
            var expansion = FindExpansion(id);
            if (expansion == null) return ePlacementFailure.NO_SELECTION;
            foreach (var cell in expansion.GetCells())
                if (GetBlockAt(cell) != null || GetUnitAt(cell) != null) return ePlacementFailure.EXPANSION_OCCUPIED;
            return ePlacementFailure.NONE;
        }
        private HashSet<Vector2Int> GetRemainingFloor()
        {
            var result = new HashSet<Vector2Int>(_floor);
            var moving = IsExpansionDrag ? FindExpansion(_selectedId) : null;
            if (moving != null) result.ExceptWith(moving.GetCells());
            return result;
        }
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
            var remaining = GetRemainingFloor();
            bool moving = IsExpansionDrag && _selectedId != null;
            for (int y = 0; y < Definition.MaximumSize.y; y++)
                for (int x = 0; x < Definition.MaximumSize.x; x++)
                    for (int rotation = 0; rotation < 4; rotation++)
                    {
                        var cells = Definition.Expansion.GetCells(new Vector2Int(x, y), rotation);
                        var failure = moving ? GridPlacementRules.ValidateExpansionMove(Definition, remaining, cells) :
                            GridPlacementRules.ValidateExpansion(Definition, remaining, cells);
                        if (failure == ePlacementFailure.NONE)
                            foreach (var cell in cells) result.Add(cell);
                    }
            return new List<Vector2Int>(result).AsReadOnly();
        }
        internal bool TryAllowPreparation(bool canSkip)
        {
            if (_isNotifying || HasPendingStorage || (Phase != eGridPhase.REWARD && Phase != eGridPhase.WAITING)) return false;
            _canSkipPreparation = canSkip;
            Phase = eGridPhase.PREPARATION; Notify(); return true;
        }
        internal bool TryAcceptExpansionReward()
        {
            if (_isNotifying || HasPendingStorage || Phase != eGridPhase.REWARD || !CanExpand) return false;
            RequiresExpansionPlacement = true; Notify(); return true;
        }
        internal bool TryAcceptUnitReward(UnitPlacement unit, BlockPlacement block, Action onCommitted)
        {
            if (_isNotifying || HasPendingStorage || Phase != eGridPhase.REWARD || unit.IsPlaced || block.IsPlaced ||
                FindUnit(unit.InstanceId) != null || FindBlock(block.InstanceId) != null ||
                unit.Definition.RewardBlockId != block.Footprint.Id) return false;
            var units = new List<UnitPlacement>(_units) { unit };
            var blocks = new List<BlockPlacement>(_blocks) { block };
            return ApplyOrRequest(blocks, units, eGridStorageOperation.REWARD, onCommitted);
        }
        internal bool TryBeginBattle(Action captureDeployment)
        {
            if (_isNotifying || !CanBeginBattle) return false;
            // 거절된 요청은 확정본을 변경하지 않고, 시작 관찰자는 새 확정본을 읽어야 한다.
            captureDeployment();
            Phase = eGridPhase.BATTLE; Notify(); return true;
        }
        internal bool TryFinishBattle()
        {
            if (_isNotifying || Phase != eGridPhase.BATTLE) return false;
            Phase = eGridPhase.REWARD; Notify(); return true;
        }
        internal void EndRun()
        {
            if (Phase == eGridPhase.ENDED) return;
            Phase = eGridPhase.ENDED; PendingStorage = null; ClearSelection(); Notify();
        }
        public bool BeginBlockDrag(string id)
        {
            var unit = FindBlock(id);
            if (Phase != eGridPhase.PREPARATION || HasSelection || HasPendingStorage || _isNotifying || unit == null) return false;
            _selectedId = id; DragKind = eGridDragKind.BLOCK; _previewAnchor = unit.Anchor; _previewRotation = unit.Rotation;
            _previewIsMirrored = unit.IsMirrored; HoveredExpansionId = null;
            LastDropFailure = ePlacementFailure.NONE;
            Notify(); return true;
        }
        public bool BeginUnitDrag(string id)
        {
            var unit = FindUnit(id);
            if (Phase != eGridPhase.PREPARATION || HasSelection || HasPendingStorage || _isNotifying || unit == null) return false;
            _selectedId = id; DragKind = eGridDragKind.UNIT; _previewRotation = unit.Rotation;
            _previewIsMirrored = unit.IsMirrored; HoveredExpansionId = null;
            LastDropFailure = ePlacementFailure.NONE;
            _previewAnchor = unit.IsPlaced ? unit.Anchor : new Vector2Int(-1, -1);
            Notify(); return true;
        }
        public bool BeginExpansionDrag()
        {
            if (Phase != eGridPhase.PREPARATION || !RequiresExpansionPlacement || HasSelection || HasPendingStorage || _isNotifying) return false;
            DragKind = eGridDragKind.EXPANSION; _selectedId = null; _previewRotation = 0; _previewAnchor = new Vector2Int(-1, -1);
            _previewIsMirrored = false; HoveredExpansionId = null;
            LastDropFailure = ePlacementFailure.NONE;
            Notify(); return true;
        }
        public bool BeginExpansionDrag(string id)
        {
            if (Phase != eGridPhase.PREPARATION || HasSelection || HasPendingStorage || _isNotifying) return false;
            LastDropFailure = GetExpansionMoveFailure(id);
            if (LastDropFailure != ePlacementFailure.NONE) { Notify(); return false; }
            var expansion = FindExpansion(id);
            DragKind = eGridDragKind.EXPANSION; _selectedId = id;
            _previewAnchor = expansion.Anchor; _previewRotation = expansion.Rotation;
            _previewIsMirrored = false; HoveredExpansionId = null;
            Notify(); return true;
        }
        public void MovePreview(Vector2Int anchor) { if (!HasSelection || _isNotifying) return; _previewAnchor = anchor; Notify(); }
        public void RotatePreview() { if (!HasSelection || _isNotifying) return; _previewRotation = (_previewRotation + 1) % 4; Notify(); }
        public void MirrorPreview()
        {
            if (!HasSelection || IsExpansionDrag || _isNotifying) return;
            _previewRotation = (4 - _previewRotation) % 4;
            _previewIsMirrored = !_previewIsMirrored;
            Notify();
        }
        public Vector2Int[] GetPreviewCells()
        {
            if (!HasSelection) return Array.Empty<Vector2Int>();
            var draggedUnit = DragKind == eGridDragKind.UNIT ? FindUnit(_selectedId) : null;
            var shape = draggedUnit != null ? draggedUnit.Definition.GetFootprint(draggedUnit.StarLevel) :
                IsExpansionDrag ? Definition.Expansion : FindBlock(_selectedId).Footprint;
            return shape.GetCells(_previewAnchor, _previewRotation, _previewIsMirrored);
        }
        public ePlacementFailure GetPreviewFailure()
        {
            if (Phase != eGridPhase.PREPARATION) return ePlacementFailure.NOT_PREPARING;
            if (HasPendingStorage) return ePlacementFailure.STORAGE_PENDING;
            if (!HasSelection) return ePlacementFailure.NO_SELECTION;
            if (DragKind == eGridDragKind.UNIT)
            {
                if (CanFusePreview) return ePlacementFailure.NONE;
                return GridPlacementRules.ValidateUnit(Definition, _blocks, _units, _selectedId, GetPreviewCells());
            }
            if (!IsExpansionDrag) return GridPlacementRules.ValidateBlock(Definition, _floor, _blocks, _selectedId, GetPreviewCells());
            if (_selectedId == null) return GridPlacementRules.ValidateExpansion(Definition, _floor, GetPreviewCells());
            var failure = GetExpansionMoveFailure(_selectedId);
            return failure != ePlacementFailure.NONE ? failure :
                GridPlacementRules.ValidateExpansionMove(Definition, GetRemainingFloor(), GetPreviewCells());
        }
        public bool CommitPreview()
        {
            if (_isNotifying) return false;
            LastDropFailure = GetPreviewFailure();
            if (LastDropFailure != ePlacementFailure.NONE) return false;
            if (CanFusePreview) return TryFuseUnits(SelectedId, GetUnitAt(_previewAnchor).InstanceId);
            if (IsExpansionDrag)
            {
                var moving = FindExpansion(_selectedId);
                if (moving != null)
                {
                    _floor.ExceptWith(moving.GetCells());
                    _expansions[_expansions.IndexOf(moving)] = moving.WithPlacement(_previewAnchor, _previewRotation);
                }
                else
                {
                    _expansions.Add(new ExpansionPlacement(Guid.NewGuid().ToString("N"), Definition.Expansion, _previewAnchor, _previewRotation));
                    RequiresExpansionPlacement = false;
                }
                foreach (var cell in GetPreviewCells()) _floor.Add(cell);
                UpdateFloorView();
                ClearSelection(); Notify(true); return true;
            }
            var blocks = new List<BlockPlacement>(_blocks);
            var units = new List<UnitPlacement>(_units);
            if (DragKind == eGridDragKind.UNIT)
            {
                int index = units.FindIndex(unit => unit.InstanceId == _selectedId);
                units[index] = units[index].WithPlacement(true, _previewAnchor, _previewRotation, _previewIsMirrored);
            }
            else
            {
                int index = blocks.FindIndex(block => block.InstanceId == _selectedId);
                var block = blocks[index];
                if (block.IsPlaced && new HashSet<Vector2Int>(block.GetCells()).SetEquals(GetPreviewCells()))
                {
                    // 점유가 같아도 반전·회전 상태는 다음 드래그를 위해 보존한다. 유닛 반환과 레이아웃 알림은 불필요하다.
                    _blocks[index] = block.WithPlacement(true, _previewAnchor, _previewRotation, _previewIsMirrored);
                    ClearSelection(); Notify(); return true;
                }
                ReturnUnitsFromBlock(units, _selectedId);
                blocks[index] = block.WithPlacement(true, _previewAnchor, _previewRotation, _previewIsMirrored);
            }
            return ApplyOrRequest(blocks, units, eGridStorageOperation.LAYOUT, null);
        }
        public bool DropToTray()
        {
            if (_isNotifying || HasPendingStorage || Phase != eGridPhase.PREPARATION || !HasSelection) return false;
            LastDropFailure = GetTrayDropFailure();
            if (LastDropFailure != ePlacementFailure.NONE) return false;
            var blocks = new List<BlockPlacement>(_blocks);
            var units = new List<UnitPlacement>(_units);
            if (DragKind == eGridDragKind.UNIT)
            {
                int index = units.FindIndex(unit => unit.InstanceId == _selectedId);
                units[index] = units[index].WithPlacement(false, units[index].Anchor, _previewRotation, _previewIsMirrored);
            }
            else
            {
                int index = blocks.FindIndex(block => block.InstanceId == _selectedId);
                ReturnUnitsFromBlock(units, _selectedId);
                blocks[index] = blocks[index].WithPlacement(false, blocks[index].Anchor, _previewRotation, _previewIsMirrored);
            }
            return ApplyOrRequest(blocks, units, eGridStorageOperation.LAYOUT, null);
        }
        public ePlacementFailure GetTrayDropFailure()
        {
            if (Phase != eGridPhase.PREPARATION) return ePlacementFailure.NOT_PREPARING;
            if (HasPendingStorage) return ePlacementFailure.STORAGE_PENDING;
            if (IsExpansionDrag) return ePlacementFailure.CANNOT_STORE_EXPANSION;
            if (_selectedId == null) return ePlacementFailure.NO_SELECTION;
            return DragKind == eGridDragKind.BLOCK ?
                GridPlacementRules.ValidateBlockRemoval(_blocks, _selectedId) : ePlacementFailure.NONE;
        }
        public void CancelDrag() { if (!HasSelection || _isNotifying) return; ClearSelection(); Notify(); }
        private void ReturnUnitsFromBlock(List<UnitPlacement> units, string blockId)
        {
            foreach (var unit in GetUnitsOnBlock(blockId))
            {
                int index = units.FindIndex(candidate => candidate.InstanceId == unit.InstanceId);
                units[index] = unit.WithPlacement(false, unit.Anchor, unit.Rotation);
            }
        }
        private static int CountStored(List<BlockPlacement> blocks, List<UnitPlacement> units)
            => blocks.FindAll(block => !block.IsPlaced).Count + units.FindAll(unit => !unit.IsPlaced).Count;
        public IReadOnlyList<GridStoredItem> GetStoredItems() => StoredItems(_blocks, _units).AsReadOnly();
        private static List<GridStoredItem> StoredItems(List<BlockPlacement> blocks, List<UnitPlacement> units)
        {
            var items = new List<GridStoredItem>();
            foreach (var block in blocks) if (!block.IsPlaced) items.Add(new GridStoredItem(eGridDragKind.BLOCK, block.InstanceId, block.Footprint.DisplayName));
            foreach (var unit in units) if (!unit.IsPlaced) items.Add(new GridStoredItem(eGridDragKind.UNIT, unit.InstanceId, unit.Definition.DisplayName));
            return items;
        }
        // 실행 후보를 먼저 계산하여 폐기 취소·화면 재진입에도 원본을 보존한다.
        private bool ApplyOrRequest(List<BlockPlacement> blocks, List<UnitPlacement> units, eGridStorageOperation operation, Action onCommitted)
        {
            int required = CountStored(blocks, units) - Definition.StorageCapacity;
            ClearSelection();
            if (required > 0)
            {
                var before = new HashSet<GridStoredItem>(StoredItems(_blocks, _units));
                var incoming = new List<GridStoredItem>();
                var candidates = new List<GridStoredItem>();
                foreach (var item in StoredItems(blocks, units))
                    if (before.Contains(item)) candidates.Add(item); else incoming.Add(item);
                PendingStorage = new GridStorageRequest(operation, required, incoming, candidates, blocks, units, onCommitted);
                Notify(); return false;
            }
            ApplyState(blocks, units, onCommitted); return true;
        }
        internal bool TryConfirmStorage(string requestId, IReadOnlyCollection<GridStoredItem> discard)
        {
            var request = PendingStorage;
            if (_isNotifying || request == null || request.RequestId != requestId || discard == null || Phase == eGridPhase.ENDED) return false;
            var selected = new HashSet<GridStoredItem>(discard);
            if (selected.Count != discard.Count || selected.Count != request.RequiredDiscardCount) return false;
            var allowed = new HashSet<GridStoredItem>(request.DiscardCandidates);
            if (!selected.IsSubsetOf(allowed)) return false;
            var blocks = request.Blocks.FindAll(block => !selected.Contains(new GridStoredItem(eGridDragKind.BLOCK, block.InstanceId)));
            var units = request.Units.FindAll(unit => !selected.Contains(new GridStoredItem(eGridDragKind.UNIT, unit.InstanceId)));
            if (CountStored(blocks, units) > Definition.StorageCapacity) return false;
            PendingStorage = null;
            ApplyState(blocks, units, request.OnCommitted); return true;
        }
        internal bool TryCancelStorage(string requestId)
        {
            if (_isNotifying || PendingStorage == null || PendingStorage.RequestId != requestId) return false;
            PendingStorage = null; Notify(); return true;
        }
        private void ApplyState(List<BlockPlacement> blocks, List<UnitPlacement> units, Action onCommitted)
        {
            _blocks.Clear(); _blocks.AddRange(blocks); _units.Clear(); _units.AddRange(units);
            onCommitted?.Invoke(); Notify(true);
        }
        private void ClearSelection() { _selectedId = null; DragKind = eGridDragKind.NONE; _previewIsMirrored = false; HoveredExpansionId = null; }
        private void UpdateFloorView() => _floorView = new List<Vector2Int>(_floor).AsReadOnly();
        private void Notify(bool hasLayoutChanged = false)
        {
            bool previous = _isNotifying; _isNotifying = true;
            try { NotifyObservers(Changed); if (hasLayoutChanged) NotifyObservers(LayoutChanged); }
            finally { _isNotifying = previous; }
        }
        private void NotifyObservers(Action observers)
        {
            if (observers == null) return;
            foreach (Action observer in observers.GetInvocationList())
                try { observer(); } catch (Exception exception) { LastObserverError = exception; }
        }
    }
}
