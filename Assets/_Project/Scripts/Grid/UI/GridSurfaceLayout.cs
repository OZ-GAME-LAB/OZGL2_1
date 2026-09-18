using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Grid.UI
{
    /// <summary>준비 UI와 전투 월드가 공유하는 셀 내부 외곽선. 좌표는 좌하단 기준 0..1이다.</summary>
    public static class GridSurfaceLayout
    {
        public static IReadOnlyList<Rect> GetBorders(GridManager grid, Vector2Int cell, float width)
        {
            var result = new List<Rect>();
            var surface = GridBoardThemeSO.GetSurface(grid, cell);
            if (surface == eGridCellSurface.UNCLAIMED) return result;
            width = Mathf.Clamp(width, 0, 0.25f);
            bool left = Matches(grid, cell + Vector2Int.left, surface);
            bool right = Matches(grid, cell + Vector2Int.right, surface);
            bool up = Matches(grid, cell + Vector2Int.up, surface);
            bool down = Matches(grid, cell + Vector2Int.down, surface);
            if (!left) result.Add(new Rect(0, 0, width, 1));
            if (!right) result.Add(new Rect(1 - width, 0, width, 1));
            if (!down) result.Add(new Rect(0, 0, 1, width));
            if (!up) result.Add(new Rect(0, 1 - width, 1, width));
            // ㄱ자 안쪽 모서리는 변만 검사하면 한 픽셀의 구멍이 남는다.
            if (left && down && !Matches(grid, cell + new Vector2Int(-1, -1), surface)) result.Add(new Rect(0, 0, width, width));
            if (right && down && !Matches(grid, cell + new Vector2Int(1, -1), surface)) result.Add(new Rect(1 - width, 0, width, width));
            if (left && up && !Matches(grid, cell + new Vector2Int(-1, 1), surface)) result.Add(new Rect(0, 1 - width, width, width));
            if (right && up && !Matches(grid, cell + Vector2Int.one, surface)) result.Add(new Rect(1 - width, 1 - width, width, width));
            return result;
        }
        private static bool Matches(GridManager grid, Vector2Int cell, eGridCellSurface surface)
            => grid.Definition.Contains(cell) && (surface == eGridCellSurface.PLATFORM ? grid.GetBlockAt(cell) != null : grid.HasFloor(cell));
    }
}
