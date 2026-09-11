using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.UI
{
    /// <summary>좌표 변환과 표시만 담당한다. 일반 바닥과 마왕 위치를 별도 요소로 그린다.</summary>
    public sealed class GridBoardView
    {
        public const float CELL_SIZE = 64;
        public static readonly Color VALID_COLOR = new Color(0.2f, 0.95f, 0.48f, 0.62f);
        public static readonly Color INVALID_COLOR = new Color(1, 0.18f, 0.25f, 0.62f);
        private readonly GridManager _manager;
        private readonly VisualElement _floorLayer;
        private readonly VisualElement _blocksLayer;
        private readonly VisualElement _ghostLayer;
        public VisualElement Element { get; }
        public VisualElement GhostLayer => _ghostLayer;
        public VisualElement KingElement { get; }
        public GridBoardView(GridManager manager)
        {
            _manager = manager;
            Element = new VisualElement { name = "grid-board" };
            Element.style.width = manager.Definition.MaximumSize.x * CELL_SIZE;
            Element.style.height = manager.Definition.MaximumSize.y * CELL_SIZE;
            Element.style.marginTop = 16; Element.style.marginBottom = CELL_SIZE + 24;
            _floorLayer = Layer("floor"); _blocksLayer = Layer("units"); _ghostLayer = Layer("ghost");
            Element.Add(_floorLayer); Element.Add(_blocksLayer); Element.Add(_ghostLayer);
            KingElement = new Label("KING") { name = "king-grid", pickingMode = PickingMode.Ignore };
            Place(KingElement, manager.Definition.KingAnchor, CELL_SIZE - 12);
            KingElement.style.backgroundColor = new Color(0.40f, 0.29f, 0.12f);
            KingElement.style.color = new Color(1, 0.84f, 0.45f);
            KingElement.style.unityTextAlign = TextAnchor.MiddleCenter;
            KingElement.style.fontSize = 18;
            Element.Add(KingElement);
        }
        private static VisualElement Layer(string name)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute; element.style.left = 0; element.style.top = 0;
            element.style.width = Length.Percent(100); element.style.height = Length.Percent(100);
            return element;
        }
        public Vector2Int PanelToCell(Vector2 position)
        {
            var local = Element.WorldToLocal(position);
            return new Vector2Int(Mathf.FloorToInt(local.x / CELL_SIZE),
                _manager.Definition.MaximumSize.y - 1 - Mathf.FloorToInt(local.y / CELL_SIZE));
        }
        public Vector2 CellToPanel(Vector2Int cell) => Element.LocalToWorld(new Vector2((cell.x + 0.5f) * CELL_SIZE,
            (_manager.Definition.MaximumSize.y - cell.y - 0.5f) * CELL_SIZE));
        public void Render()
        {
            _floorLayer.Clear(); _blocksLayer.Clear(); _ghostLayer.Clear();
            var frontier = _manager.RequiresExpansionPlacement ? new HashSet<Vector2Int>(_manager.GetExpansionFrontier()) : null;
            for (int y = 0; y < _manager.Definition.MaximumSize.y; y++)
                for (int x = 0; x < _manager.Definition.MaximumSize.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var tile = new VisualElement { pickingMode = PickingMode.Ignore };
                    Place(tile, cell, CELL_SIZE - 4);
                    tile.style.backgroundColor = _manager.HasFloor(cell) ? new Color(0.17f, 0.24f, 0.33f) : new Color(0.07f, 0.10f, 0.15f);
                    _floorLayer.Add(tile);
                    if (frontier != null && frontier.Contains(cell)) AddDashes(tile);
                }
            foreach (var block in _manager.Blocks)
            {
                if (!block.IsPlaced || (_manager.DragKind == eGridDragKind.BLOCK && block.InstanceId == _manager.SelectedId)) continue;
                foreach (var cell in block.Footprint.GetCells(block.Anchor, block.Rotation))
                    AddCell(_blocksLayer, cell, new Color(0.27f, 0.48f, 0.72f, 0.85f), "occupied");
            }
            foreach (var unit in _manager.Units)
            {
                if (!unit.IsPlaced || _manager.IsUnitTemporarilyReturned(unit) ||
                    (_manager.DragKind == eGridDragKind.UNIT && unit.InstanceId == _manager.SelectedId)) continue;
                AddActor(_blocksLayer, _manager.FindBlock(unit.BlockId).Anchor, unit.Definition.DisplayName, Color.white);
            }
            if (_manager.HasSelection)
            {
                bool isValid = _manager.GetPreviewFailure() == ePlacementFailure.NONE;
                foreach (var cell in _manager.GetPreviewCells()) AddCell(_ghostLayer, cell, isValid ? VALID_COLOR : INVALID_COLOR, "ghost-cell");
                if (_manager.DragKind == eGridDragKind.UNIT)
                    AddActor(_ghostLayer, _manager.PreviewTargetBlock?.Anchor ?? _manager.PreviewAnchor, _manager.FindUnit(_manager.SelectedId).Definition.DisplayName,
                        new Color(1, 1, 1, 0.8f));
            }
        }
        private void AddCell(VisualElement parent, Vector2Int cell, Color color, string name)
        {
            var tile = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Place(tile, cell, CELL_SIZE - 8); tile.style.backgroundColor = color; parent.Add(tile);
        }
        private void AddActor(VisualElement parent, Vector2Int cell, string label, Color color)
        {
            var actor = new Label("●\n" + label) { name = "unit-marker", pickingMode = PickingMode.Ignore };
            Place(actor, cell, CELL_SIZE - 8); actor.style.color = color; actor.style.fontSize = 13;
            actor.style.unityTextAlign = TextAnchor.MiddleCenter; parent.Add(actor);
        }
        private void Place(VisualElement element, Vector2 cell, float size)
        {
            float inset = (CELL_SIZE - size) / 2;
            element.style.position = Position.Absolute;
            element.style.left = cell.x * CELL_SIZE + inset;
            element.style.top = (_manager.Definition.MaximumSize.y - 1 - cell.y) * CELL_SIZE + inset;
            element.style.width = size; element.style.height = size;
        }
        private static void AddDashes(VisualElement tile)
        {
            for (int side = 0; side < 4; side++)
                for (int i = 0; i < 4; i++)
                {
                    var dash = new VisualElement { pickingMode = PickingMode.Ignore };
                    dash.style.position = Position.Absolute; dash.style.backgroundColor = new Color(0.4f, 0.72f, 0.64f);
                    bool isHorizontal = side < 2;
                    dash.style.width = isHorizontal ? 9 : 2; dash.style.height = isHorizontal ? 2 : 9;
                    dash.style.left = isHorizontal ? 8 + i * 16 : (side == 2 ? 2 : CELL_SIZE - 8);
                    dash.style.top = !isHorizontal ? 8 + i * 16 : (side == 0 ? 2 : CELL_SIZE - 8);
                    tile.Add(dash);
                }
        }
    }
}
