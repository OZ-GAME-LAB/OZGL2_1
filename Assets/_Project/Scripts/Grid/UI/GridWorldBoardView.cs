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
        private readonly SpriteRenderer[,] _overlays;
        private readonly SpriteRenderer[,,] _borders;
        private readonly Sprite _solid;
        private readonly float _cellSize;
        private eGridPhase _lastPhase;
        public GridWorldBoardView(GridManager grid, GridBoardThemeSO theme, GridWorldMapping mapping, float cellSize, Transform parent)
        {
            _grid = grid; _theme = theme; _cellSize = cellSize;
            _root = new GameObject("GridWorldBoard");
            _root.transform.SetParent(parent, false);
            _tiles = new SpriteRenderer[grid.Definition.MaximumSize.x, grid.Definition.MaximumSize.y];
            if (theme.TerrainTiles != null) _overlays = new SpriteRenderer[_tiles.GetLength(0), _tiles.GetLength(1)];
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
                    if (_overlays != null) _overlays[x, y] = CreateRenderer("Corruption", cell.transform, -99);
                    for (int edge = 0; edge < 8; edge++)
                    {
                        var renderer = CreateRenderer("Border_" + edge, cell.transform, _overlays == null ? -99 : -98);
                        renderer.sprite = _solid;
                        _borders[x, y, edge] = renderer;
                    }
                }
            grid.LayoutChanged += Refresh;
            if (_overlays != null) grid.Changed += RefreshPhase;
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
            _lastPhase = _grid.Phase;
            for (int y = 0; y < _tiles.GetLength(1); y++)
                for (int x = 0; x < _tiles.GetLength(0); x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_overlays != null) { RefreshTerrain(cell); continue; }
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
        private void RefreshPhase()
        {
            if (_lastPhase != _grid.Phase) Refresh();
        }
        private void RefreshTerrain(Vector2Int cell)
        {
            var terrain = _theme.TerrainTiles;
            SetSprite(_tiles[cell.x, cell.y], terrain.GetGround(_grid, cell));
            SetSprite(_overlays[cell.x, cell.y], terrain.GetCorruption(_grid, cell));
            float width = terrain.GuideWidth;
            for (int edge = 0; edge < 8; edge++)
            {
                var guide = _borders[cell.x, cell.y, edge];
                guide.enabled = _grid.Phase == eGridPhase.PREPARATION && edge < 4 && width > 0;
                if (!guide.enabled) continue;
                bool isVertical = edge < 2;
                float offset = ((edge & 1) == 0 ? -1 : 1) * (0.5f - width * 0.5f) * _cellSize;
                guide.transform.localPosition = isVertical ? new Vector3(offset, 0) : new Vector3(0, offset);
                guide.transform.localScale = isVertical ? new Vector3(width * _cellSize, _cellSize, 1) : new Vector3(_cellSize, width * _cellSize, 1);
                var color = terrain.PreparationGuide;
                if (!_grid.HasFloor(cell)) color.a *= 0.5f;
                guide.color = color;
            }
        }
        private void SetSprite(SpriteRenderer renderer, Sprite sprite)
        {
            renderer.sprite = sprite; renderer.enabled = sprite != null;
            if (sprite != null) renderer.transform.localScale = new Vector3(_cellSize / sprite.bounds.size.x, _cellSize / sprite.bounds.size.y, 1);
        }
        public void Dispose()
        {
            _grid.LayoutChanged -= Refresh;
            if (_overlays != null) _grid.Changed -= RefreshPhase;
            if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); }
            if (_solid != null) UnityEngine.Object.Destroy(_solid);
        }
    }
}
