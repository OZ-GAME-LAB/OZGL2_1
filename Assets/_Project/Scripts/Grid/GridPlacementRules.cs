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
            IEnumerable<BlockPlacement> blocks, string movingId, Vector2Int[] cells)
        {
            var remaining = CollectBlockCells(blocks, movingId);
            foreach (var cell in cells)
            {
                if (!definition.Contains(cell)) return ePlacementFailure.OUTSIDE_BOUNDS;
                if (!floor.Contains(cell)) return ePlacementFailure.NO_FLOOR;
                if (remaining.Contains(cell)) return ePlacementFailure.OCCUPIED;
            }
            // 같은 자리에 놓기는 연결 다리를 실제로 회수하는 작업이 아니다.
            foreach (var block in blocks)
                if (block.IsPlaced && block.InstanceId == movingId &&
                    new HashSet<Vector2Int>(block.Footprint.GetCells(block.Anchor, block.Rotation)).SetEquals(cells))
                    return ePlacementFailure.NONE;
            if (!IsConnected(remaining)) return ePlacementFailure.DISCONNECTED;
            remaining.UnionWith(cells);
            return IsConnected(remaining) ? ePlacementFailure.NONE : ePlacementFailure.DISCONNECTED;
        }
        public static ePlacementFailure ValidateBlockRemoval(IEnumerable<BlockPlacement> blocks, string movingId)
            => IsConnected(CollectBlockCells(blocks, movingId)) ? ePlacementFailure.NONE : ePlacementFailure.DISCONNECTED;

        private static HashSet<Vector2Int> CollectBlockCells(IEnumerable<BlockPlacement> blocks, string excludedId)
        {
            var result = new HashSet<Vector2Int>();
            foreach (var block in blocks)
                if (block.IsPlaced && block.InstanceId != excludedId)
                    result.UnionWith(block.Footprint.GetCells(block.Anchor, block.Rotation));
            return result;
        }
        private static bool IsConnected(HashSet<Vector2Int> cells)
        {
            if (cells.Count < 2) return true;
            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            foreach (var cell in cells) { queue.Enqueue(cell); visited.Add(cell); break; }
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var direction in Directions)
                    if (cells.Contains(current + direction) && visited.Add(current + direction)) queue.Enqueue(current + direction);
            }
            return visited.Count == cells.Count;
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
