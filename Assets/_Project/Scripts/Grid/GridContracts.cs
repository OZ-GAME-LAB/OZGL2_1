using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Grid
{
    public enum eGridPhase { PREPARATION, BATTLE, REWARD, WAITING, ENDED }
    public enum eGridDragKind { NONE, BLOCK, UNIT, EXPANSION }
    public enum ePlacementFailure { NONE, NOT_PREPARING, NO_SELECTION, OUTSIDE_BOUNDS, NO_FLOOR, OCCUPIED, FLOOR_EXISTS, DISCONNECTED, NO_BLOCK, STORAGE_PENDING }

    public interface IGridPreparation
    {
        bool CanBeginBattle { get; }
        bool CanSkipPreparation { get; }
        bool RequiresExpansionPlacement { get; }
        bool TryAllowPreparation(string runId, int round, bool canSkip);
        bool TryBeginBattle(string runId, int round, bool skip = false);
        bool TryFinishBattle(string runId, int round, string rewardRequestId);
    }
    public interface IGridReadModel
    {
        event Action Changed;
        event Action LayoutChanged;
        IReadOnlyList<BlockPlacement> Blocks { get; }
        IReadOnlyList<UnitPlacement> Units { get; }
        IReadOnlyCollection<Vector2Int> FloorCells { get; }
        bool CanExpand { get; }
        int StoredCount { get; }
        GridStorageRequest PendingStorage { get; }
    }
    /// <summary>바닥 위에 배치하는 블록. 유닛의 배치 연결과 독립적이다.</summary>
    public sealed class BlockPlacement
    {
        public string InstanceId { get; }
        public string ContentId { get; }
        public FootprintDefinition Footprint { get; }
        public bool IsPlaced { get; }
        public Vector2Int Anchor { get; }
        public int Rotation { get; }
        public BlockPlacement(string instanceId, string contentId, FootprintDefinition footprint,
            bool isPlaced = false, Vector2Int anchor = default, int rotation = 0)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(contentId)) throw new ArgumentException("Unit IDs required.");
            InstanceId = instanceId; ContentId = contentId; Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            IsPlaced = isPlaced; Anchor = anchor; Rotation = ((rotation % 4) + 4) % 4;
        }
        public BlockPlacement WithPlacement(bool isPlaced, Vector2Int anchor, int rotation)
            => new BlockPlacement(InstanceId, ContentId, Footprint, isPlaced, anchor, rotation);
    }
    public sealed class UnitPlacement
    {
        public string InstanceId { get; }
        public UnitDefinition Definition { get; }
        public bool IsPlaced { get; }
        public Vector2Int Anchor { get; }
        public int Rotation { get; }
        public UnitPlacement(string instanceId, UnitDefinition definition, bool isPlaced = false, Vector2Int anchor = default, int rotation = 0)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Unit instance ID required.");
            InstanceId = instanceId; Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            IsPlaced = isPlaced; Anchor = anchor; Rotation = ((rotation % 4) + 4) % 4;
        }
        public UnitPlacement WithPlacement(bool isPlaced, Vector2Int anchor, int rotation)
            => new UnitPlacement(InstanceId, Definition, isPlaced, anchor, rotation);
        public Vector2Int[] GetCells() => Definition.Footprint.GetCells(Anchor, Rotation);
    }
    public sealed class UnitDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public FootprintDefinition Footprint { get; }
        public string RewardBlockId { get; }
        public UnitDefinition(string id, string displayName, FootprintDefinition footprint, string rewardBlockId = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Unit ID required.");
            Id = id; DisplayName = displayName; Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            RewardBlockId = rewardBlockId ?? footprint.Id;
        }
    }
}
