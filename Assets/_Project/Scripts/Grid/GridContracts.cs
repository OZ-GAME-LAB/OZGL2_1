using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Grid
{
    public enum eGridPhase { PREPARATION, BATTLE, REWARD, WAITING, ENDED }
    public enum eGridDragKind { NONE, BLOCK, UNIT, EXPANSION }
    public enum ePlacementFailure { NONE, NOT_PREPARING, NO_SELECTION, OUTSIDE_BOUNDS, NO_FLOOR, OCCUPIED, FLOOR_EXISTS, DISCONNECTED, NO_BLOCK, WRONG_BLOCK, BLOCK_OCCUPIED }

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
        public string BlockId { get; }
        public bool IsPlaced => BlockId != null;
        public UnitPlacement(string instanceId, UnitDefinition definition, string blockId = null)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Unit instance ID required.");
            InstanceId = instanceId; Definition = definition ?? throw new ArgumentNullException(nameof(definition)); BlockId = blockId;
        }
        public UnitPlacement WithBlock(string blockId) => new UnitPlacement(InstanceId, Definition, blockId);
    }
    public sealed class UnitDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string RequiredBlockId { get; }
        public UnitDefinition(string id, string displayName, string requiredBlockId)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(requiredBlockId)) throw new ArgumentException("Unit and required block IDs required.");
            Id = id; DisplayName = displayName; RequiredBlockId = requiredBlockId;
        }
    }
}
