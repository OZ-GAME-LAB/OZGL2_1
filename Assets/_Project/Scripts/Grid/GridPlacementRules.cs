using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace OZGL2.Grid
{
    public static class GridPlacementRules
    {
        public static ePlacementFailure ValidateUnit(UnitPlacement moving, BlockPlacement target, IEnumerable<UnitPlacement> units)
        {
            if (target == null || !target.IsPlaced) return ePlacementFailure.NO_BLOCK;
            if (target.Footprint.Id != moving.Definition.RequiredBlockId) return ePlacementFailure.WRONG_BLOCK;
            foreach (var unit in units)
                if (unit.InstanceId != moving.InstanceId && unit.IsPlaced && unit.BlockId == target.InstanceId)
                    return ePlacementFailure.BLOCK_OCCUPIED;
            return ePlacementFailure.NONE;
        }
        public static readonly ReadOnlyCollection<Vector2Int> Directions = new List<Vector2Int>
        { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down }.AsReadOnly();
        public static ePlacementFailure ValidateBlock(GridDefinition definition, HashSet<Vector2Int> floor,
            IEnumerable<BlockPlacement> units, string movingId, Vector2Int[] cells)
        {
            foreach (var cell in cells)
            {
                if (!definition.Contains(cell)) return ePlacementFailure.OUTSIDE_BOUNDS;
                if (!floor.Contains(cell)) return ePlacementFailure.NO_FLOOR;
                foreach (var unit in units)
                    if (unit.IsPlaced && unit.InstanceId != movingId)
                        foreach (var occupied in unit.Footprint.GetCells(unit.Anchor, unit.Rotation))
                            if (occupied == cell) return ePlacementFailure.OCCUPIED;
            }
            return ePlacementFailure.NONE;
        }
        public static ePlacementFailure ValidateExpansion(GridDefinition definition, HashSet<Vector2Int> floor, Vector2Int[] cells)
        {
            bool hasNeighbor = false;
            foreach (var cell in cells)
            {
                if (!definition.Contains(cell)) return ePlacementFailure.OUTSIDE_BOUNDS;
                if (floor.Contains(cell)) return ePlacementFailure.FLOOR_EXISTS;
                foreach (var direction in Directions) if (floor.Contains(cell + direction)) hasNeighbor = true;
            }
            return hasNeighbor ? ePlacementFailure.NONE : ePlacementFailure.DISCONNECTED;
        }
    }
}
