using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace OZGL2.Grid
{
    public static class GridPlacementRules
    {
        public static ePlacementFailure ValidateUnit(GridDefinition definition, IEnumerable<BlockPlacement> blocks,
            IEnumerable<UnitPlacement> units, string movingId, Vector2Int[] cells)
        {
            var supported = new HashSet<Vector2Int>();
            foreach (var block in blocks)
                if (block.IsPlaced) supported.UnionWith(block.Footprint.GetCells(block.Anchor, block.Rotation));
            var occupied = new HashSet<Vector2Int>();
            foreach (var unit in units)
                if (unit.IsPlaced && unit.InstanceId != movingId) occupied.UnionWith(unit.GetCells());
            foreach (var cell in cells)
            {
                if (!definition.Contains(cell)) return ePlacementFailure.OUTSIDE_BOUNDS;
                if (!supported.Contains(cell)) return ePlacementFailure.NO_BLOCK;
                if (occupied.Contains(cell)) return ePlacementFailure.OCCUPIED;
            }
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
