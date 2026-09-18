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
        private readonly SpriteRenderer[,,] _borders;
        private readonly Sprite _solid;
        private readonly float _cellSize;
        public GridWorldBoardView(GridManager grid, GridBoardThemeSO theme, GridWorldMapping mapping, float cellSize, Transform parent)
        {
            _grid = grid; _theme = theme; _cellSize = cellSize;
            _root = new GameObject("GridWorldBoard");
            _root.transform.SetParent(parent, false);
            _tiles = new SpriteRenderer[grid.Definition.MaximumSize.x, grid.Definition.MaximumSize.y];
            _borders = new SpriteRenderer[_tiles.GetLength(0), _tiles.GetLength(1), 8];
            _solid = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
            var origin = mapping.GetWorldPosition(Vector2.zero);
            var right = mapping.GetWorldPosition(Vector2.right) - origin;
            var up = mapping.GetWorldPosition(Vector2.up) - origin;
            var rotation = Quaternion.LookRotation(Vector3.Cross(right, up), up);
            for (int y = 0; y < _tiles.GetLength(1); y++)
                for (int x = 0; x < _tiles.GetLength(0); x++)
                {
                    var cell = new GameObject("Surface_" + x + "_" + y);
                    cell.transform.SetParent(_root.transform, false);
                    cell.transform.position = mapping.GetWorldPosition(new Vector2(x, y));
                    cell.transform.rotation = rotation;
                    _tiles[x, y] = CreateRenderer("Cell_" + x + "_" + y, cell.transform, -100);
                    for (int edge = 0; edge < 8; edge++)
                    {
                        var renderer = CreateRenderer("Border_" + edge, cell.transform, -99);
                        renderer.sprite = _solid;
                        _borders[x, y, edge] = renderer;
                    }
                }
            grid.LayoutChanged += Refresh;
            Refresh();
        }
        private static SpriteRenderer CreateRenderer(string name, Transform parent, int order)
        {
            var tile = new GameObject(name);
            tile.transform.SetParent(parent, false);
            var renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return renderer;
        }
        private void Refresh()
        {
            for (int y = 0; y < _tiles.GetLength(1); y++)
                for (int x = 0; x < _tiles.GetLength(0); x++)
                {
                    var cell = new Vector2Int(x, y);
                    var surface = GridBoardThemeSO.GetSurface(_grid, cell);
                    var tile = _tiles[x, y];
                    tile.sprite = _theme.GetSprite(surface);
                    tile.color = _theme.GetTint(surface);
                    if (tile.sprite != null)
                        tile.transform.localScale = new Vector3(_cellSize / tile.sprite.bounds.size.x, _cellSize / tile.sprite.bounds.size.y, 1);
                    var borders = GridSurfaceLayout.GetBorders(_grid, cell, _theme.BorderWidth);
                    for (int edge = 0; edge < 8; edge++)
                    {
                        var renderer = _borders[x, y, edge];
                        renderer.enabled = edge < borders.Count;
                        if (!renderer.enabled) continue;
                        var rect = borders[edge];
                        renderer.color = _theme.GetBorderColor(surface);
                        renderer.transform.localPosition = new Vector3((rect.center.x - 0.5f) * _cellSize, (rect.center.y - 0.5f) * _cellSize, 0);
                        renderer.transform.localScale = new Vector3(rect.width * _cellSize, rect.height * _cellSize, 1);
                    }
                }
        }
        public void Dispose()
        {
            _grid.LayoutChanged -= Refresh;
            if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); }
            if (_solid != null) UnityEngine.Object.Destroy(_solid);
        }
    }
}
