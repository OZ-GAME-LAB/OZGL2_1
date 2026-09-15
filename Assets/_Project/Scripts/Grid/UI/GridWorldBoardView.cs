using System;
using UnityEngine;

namespace OZGL2.Grid.UI
{
    /// <summary>확정된 배치를 월드 바닥으로 표현한다. 입력과 게임 규칙을 소유하지 않는다.</summary>
    public sealed class GridWorldBoardView : IDisposable
    {
        private readonly GridManager _grid;
        private readonly GridBoardThemeSO _theme;
        private readonly GameObject _root;
        private readonly SpriteRenderer[,] _tiles;
        public GridWorldBoardView(GridManager grid, GridBoardThemeSO theme, GridWorldMapping mapping, float cellSize, Transform parent)
        {
            _grid = grid; _theme = theme;
            _root = new GameObject("GridWorldBoard");
            _root.transform.SetParent(parent, false);
            _tiles = new SpriteRenderer[grid.Definition.MaximumSize.x, grid.Definition.MaximumSize.y];
            for (int y = 0; y < _tiles.GetLength(1); y++)
                for (int x = 0; x < _tiles.GetLength(0); x++)
                {
                    var tile = new GameObject("Cell_" + x + "_" + y);
                    tile.transform.SetParent(_root.transform, false);
                    tile.transform.position = mapping.GetWorldPosition(new Vector2(x, y));
                    var renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sortingOrder = -100;
                    _tiles[x, y] = renderer;
                    renderer.sprite = theme.GetSprite(GridBoardThemeSO.GetSurface(grid, new Vector2Int(x, y)));
                    if (renderer.sprite != null)
                        tile.transform.localScale = new Vector3(cellSize / renderer.sprite.bounds.size.x, cellSize / renderer.sprite.bounds.size.y, 1);
                }
            grid.LayoutChanged += Refresh;
            Refresh();
        }
        private void Refresh()
        {
            for (int y = 0; y < _tiles.GetLength(1); y++)
                for (int x = 0; x < _tiles.GetLength(0); x++)
                {
                    var surface = GridBoardThemeSO.GetSurface(_grid, new Vector2Int(x, y));
                    _tiles[x, y].sprite = _theme.GetSprite(surface);
                    _tiles[x, y].color = _theme.GetTint(surface);
                }
        }
        public void Dispose()
        {
            _grid.LayoutChanged -= Refresh;
            if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); }
        }
    }
}
