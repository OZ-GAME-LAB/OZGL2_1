using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace OZGL2.Grid
{
    /// <summary>기준 칸을 원점으로 둔 불변 점유 모양. SO와 실행 상태를 분리한다.</summary>
    public sealed class FootprintDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public ReadOnlyCollection<Vector2Int> Cells { get; }
        public FootprintDefinition(string id, string displayName, IEnumerable<Vector2Int> cells)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Footprint ID required.");
            var copy = new List<Vector2Int>(cells ?? throw new ArgumentNullException(nameof(cells)));
            var unique = new HashSet<Vector2Int>(copy);
            if (copy.Count == 0 || unique.Count != copy.Count || !unique.Contains(Vector2Int.zero))
                throw new ArgumentException("Footprint needs unique cells including its anchor (0,0).");
            var visited = new HashSet<Vector2Int> { Vector2Int.zero };
            var queue = new Queue<Vector2Int>(); queue.Enqueue(Vector2Int.zero);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var direction in GridPlacementRules.Directions)
                    if (unique.Contains(current + direction) && visited.Add(current + direction)) queue.Enqueue(current + direction);
            }
            if (visited.Count != copy.Count) throw new ArgumentException("Footprint must be edge-connected.");
            Id = id; DisplayName = displayName; Cells = copy.AsReadOnly();
        }
        public Vector2Int[] GetCells(Vector2Int anchor, int rotation)
        {
            var result = new Vector2Int[Cells.Count];
            int turns = ((rotation % 4) + 4) % 4;
            for (int i = 0; i < Cells.Count; i++)
            {
                var cell = Cells[i];
                for (int turn = 0; turn < turns; turn++) cell = new Vector2Int(cell.y, -cell.x);
                result[i] = anchor + cell;
            }
            return result;
        }
    }
}
