using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Grid
{
    public enum eGridPhase { PREPARATION, BATTLE, REWARD, WAITING, ENDED }
    public enum eGridDragKind { NONE, BLOCK, UNIT, EXPANSION }
    public enum ePlacementFailure { NONE, NOT_PREPARING, NO_SELECTION, OUTSIDE_BOUNDS, NO_FLOOR, OCCUPIED, FLOOR_EXISTS, DISCONNECTED, NO_BLOCK, STORAGE_PENDING, EXPANSION_OCCUPIED, CANNOT_STORE_EXPANSION }

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
        public bool IsMirrored { get; }
        public BlockPlacement(string instanceId, string contentId, FootprintDefinition footprint,
            bool isPlaced = false, Vector2Int anchor = default, int rotation = 0, bool isMirrored = false)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(contentId)) throw new ArgumentException("Unit IDs required.");
            InstanceId = instanceId; ContentId = contentId; Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            IsPlaced = isPlaced; Anchor = anchor; Rotation = ((rotation % 4) + 4) % 4;
            IsMirrored = isMirrored;
        }
        public BlockPlacement WithPlacement(bool isPlaced, Vector2Int anchor, int rotation, bool? isMirrored = null)
            => new BlockPlacement(InstanceId, ContentId, Footprint, isPlaced, anchor, rotation, isMirrored ?? IsMirrored);
        public Vector2Int[] GetCells() => Footprint.GetCells(Anchor, Rotation, IsMirrored);
    }
    public sealed class UnitPlacement
    {
        public int StarLevel { get; }
        public string InstanceId { get; }
        public UnitDefinition Definition { get; }
        public bool IsPlaced { get; }
        public Vector2Int Anchor { get; }
        public int Rotation { get; }
        public bool IsMirrored { get; }
        public UnitPlacement(string instanceId, UnitDefinition definition, bool isPlaced = false, Vector2Int anchor = default, int rotation = 0, int starLevel = 1, bool isMirrored = false)
        {
            if (starLevel < 1) throw new ArgumentOutOfRangeException(nameof(starLevel));
            StarLevel = starLevel;
            if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Unit instance ID required.");
            InstanceId = instanceId; Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            IsPlaced = isPlaced; Anchor = anchor; Rotation = ((rotation % 4) + 4) % 4;
            IsMirrored = isMirrored;
        }
        public UnitPlacement WithPlacement(bool isPlaced, Vector2Int anchor, int rotation, bool? isMirrored = null)
            => new UnitPlacement(InstanceId, Definition, isPlaced, anchor, rotation, StarLevel, isMirrored ?? IsMirrored);
        public UnitPlacement WithStarLevel(int starLevel)
            => new UnitPlacement(InstanceId, Definition, IsPlaced, Anchor, Rotation, starLevel, IsMirrored);
        public Vector2Int[] GetCells() => Definition.GetFootprint(StarLevel).GetCells(Anchor, Rotation, IsMirrored);
    }
    public sealed class UnitDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public FootprintDefinition Footprint { get; }
        public FootprintDefinition FootprintStar2 { get; }
        public FootprintDefinition FootprintStar3 { get; }
        public string RewardBlockId { get; }
        public UnitDefinition(string id, string displayName, FootprintDefinition footprint, string rewardBlockId = null,
            FootprintDefinition footprintStar2 = null, FootprintDefinition footprintStar3 = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Unit ID required.");
            Id = id; DisplayName = displayName; Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            RewardBlockId = rewardBlockId ?? footprint.Id;
            // 성급별 발판이 없으면(콘텐츠 담당자가 아직 안 채운 유닛) 1성 발판을 그대로 쓴다.
            FootprintStar2 = footprintStar2 ?? footprint;
            FootprintStar3 = footprintStar3 ?? footprint;
        }
        /// <summary>성급(1~3)에 맞는 발판을 반환한다. 범위 밖 값은 1성 발판으로 처리한다.</summary>
        public FootprintDefinition GetFootprint(int starLevel)
        {
            switch (starLevel)
            {
                case 2: return FootprintStar2;
                case 3: return FootprintStar3;
                default: return Footprint;
            }
        }
    }
}
