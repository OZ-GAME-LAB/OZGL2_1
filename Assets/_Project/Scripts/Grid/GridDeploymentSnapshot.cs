using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Grid
{
    /// <summary>전투와 시너지가 공유하는 배치 확정본. 전투 중 생사 상태는 포함하지 않는다.</summary>
    public sealed class GridDeploymentSnapshot
    {
        public IReadOnlyList<GridDeployedUnit> Units { get; }
        public IReadOnlyList<Vector2Int> FloorCells { get; }
        public Vector2 KingAnchor { get; }
        public GridDeploymentSnapshot(GridManager grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            var units = new List<GridDeployedUnit>();
            foreach (var unit in grid.Units)
                if (unit.IsPlaced) units.Add(new GridDeployedUnit(unit, grid.FindBlock(unit.BlockId)));
            Units = units.AsReadOnly();
            FloorCells = new List<Vector2Int>(grid.FloorCells).AsReadOnly();
            KingAnchor = grid.Definition.KingAnchor;
        }
    }
    public sealed class GridDeployedUnit
    {
        public string InstanceId { get; }
        public string ContentId { get; }
        public string BlockId { get; }
        public string ShapeId { get; }
        public Vector2Int Anchor { get; }
        public int Rotation { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }
        internal GridDeployedUnit(UnitPlacement unit, BlockPlacement block)
        {
            InstanceId = unit.InstanceId; ContentId = unit.Definition.Id;
            BlockId = block.InstanceId; ShapeId = block.Footprint.Id;
            Anchor = block.Anchor; Rotation = block.Rotation;
            Cells = Array.AsReadOnly(block.Footprint.GetCells(Anchor, Rotation));
        }
    }
    /// <summary>Origin은 셀 (0,0)의 중심. Right/Up은 한 칸 이동량이며 XY/XZ 전투판 모두 지원한다.</summary>
    public sealed class GridWorldMapping
    {
        private readonly Vector3 _origin;
        private readonly Vector3 _right;
        private readonly Vector3 _up;
        public GridWorldMapping(Vector3 origin, Vector3 right, Vector3 up)
        {
            if (!IsFinite(origin) || !IsFinite(right) || !IsFinite(up) || Vector3.Cross(right, up).sqrMagnitude <= Mathf.Epsilon)
                throw new ArgumentException("Grid basis must be finite and non-collinear.");
            _origin = origin; _right = right; _up = up;
        }
        public Vector3 GetWorldPosition(Vector2 cell) => _origin + _right * cell.x + _up * cell.y;
        private static bool IsFinite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
