using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Grid.UI
{
    /// <summary>전투 스크립트를 생성하지 않고 스프라이트만 복사한 준비용 표시.</summary>
    public sealed class GridWorldPreparationView : IDisposable
    {
        private readonly GridManager _grid;
        private readonly GridWorldMapping _mapping;
        private readonly Func<string, int, GameObject> _prefab;
        private readonly GameObject _root;
        private readonly Dictionary<string, GameObject> _units = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, int> _stars = new Dictionary<string, int>();
        private readonly List<SpriteRenderer> _ghost = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _frontier = new List<SpriteRenderer>();
        private readonly Sprite _solid;
        private readonly float _cellSize;
        private GameObject _dragActor;
        private string _dragId;
        private int _dragStar;
        public bool IsVisible { get; private set; }
        public GridWorldPreparationView(GridManager grid, GridWorldMapping mapping, float cellSize, Func<string, GameObject> prefab, Transform parent)
            : this(grid, mapping, cellSize, (id, star) => prefab(id), parent) { }
        public GridWorldPreparationView(GridManager grid, GridWorldMapping mapping, float cellSize, Func<string, int, GameObject> prefab, Transform parent)
        {
            _grid = grid; _mapping = mapping; _cellSize = cellSize; _prefab = prefab;
            _root = new GameObject("PreparationVisuals"); _root.transform.SetParent(parent, false);
            _solid = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
            var king = Square("KingMarker", _root.transform, -1, new Color(0.6f, 0.4f, 0.12f));
            king.transform.position = mapping.GetWorldPosition(grid.Definition.KingAnchor);
            king.transform.localScale = Vector3.one * cellSize * 0.6f;
            grid.Changed += Refresh;
            SetVisible(false);
        }
        public void SetVisible(bool visible)
        { IsVisible = visible; _root.SetActive(visible); if (visible) Refresh(); }
        public void Refresh()
        {
            if (!IsVisible) return;
            var retained = new HashSet<string>();
            foreach (var unit in _grid.Units)
            {
                if (!unit.IsPlaced) continue;
                retained.Add(unit.InstanceId);
                GameObject actor;
                if (!_units.TryGetValue(unit.InstanceId, out actor) || _stars[unit.InstanceId] != unit.StarLevel)
                {
                    if (actor != null) { actor.SetActive(false); UnityEngine.Object.Destroy(actor); }
                    actor = CreateActor(unit.Definition.Id, unit.StarLevel, "Preview_" + unit.InstanceId);
                    _units[unit.InstanceId] = actor; _stars[unit.InstanceId] = unit.StarLevel;
                }
                actor.transform.position = _mapping.GetWorldPosition(unit.Anchor);
                actor.GetComponentInChildren<TextMesh>(true).text = "★" + unit.StarLevel;
                actor.SetActive(!(_grid.DragKind == eGridDragKind.UNIT && _grid.SelectedId == unit.InstanceId));
            }
            foreach (var id in new List<string>(_units.Keys))
                if (!retained.Contains(id)) { _units[id].SetActive(false); UnityEngine.Object.Destroy(_units[id]); _units.Remove(id); _stars.Remove(id); }
            var cells = _grid.HasSelection ? _grid.GetPreviewCells() : Array.Empty<Vector2Int>();
            RenderSquares(_ghost, cells, _grid.GetPreviewFailure() == ePlacementFailure.NONE ? GridBoardView.VALID_COLOR : GridBoardView.INVALID_COLOR, 200, 0.94f);
            var frontier = _grid.RequiresExpansionPlacement ? _grid.GetExpansionFrontier() : Array.Empty<Vector2Int>();
            RenderSquares(_frontier, frontier, new Color(0.4f, 0.8f, 0.6f, 0.22f), -90, 0.86f);
            string selected = _grid.DragKind == eGridDragKind.UNIT ? _grid.SelectedId : null;
            int selectedStar = selected == null ? 0 : _grid.FindUnit(selected).StarLevel;
            if (_dragId != selected || _dragStar != selectedStar)
            {
                if (_dragActor != null) { _dragActor.SetActive(false); UnityEngine.Object.Destroy(_dragActor); }
                _dragId = selected;
                _dragStar = selectedStar;
                _dragActor = selected == null ? null : CreateActor(_grid.FindUnit(selected).Definition.Id, selectedStar, "DraggedUnit");
                if (_dragActor != null)
                    foreach (var renderer in _dragActor.GetComponentsInChildren<SpriteRenderer>())
                    { renderer.color *= new Color(1, 1, 1, 0.65f); renderer.sortingOrder += 250; }
            }
            if (_dragActor != null)
            {
                _dragActor.transform.position = _mapping.GetWorldPosition(_grid.PreviewAnchor);
                _dragActor.GetComponentInChildren<TextMesh>(true).text = "★" + _grid.FindUnit(selected).StarLevel;
            }
        }
        private GameObject CreateActor(string contentId, int starLevel, string name)
        {
            var actor = new GameObject(name); actor.transform.SetParent(_root.transform, false);
            var source = _prefab(contentId, starLevel);
            int count = 0;
            if (source != null)
            {
                actor.transform.localScale = source.transform.localScale;
                foreach (var original in source.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (original.sprite == null || !original.enabled || !IsActiveVisual(original.transform, source.transform)) continue;
                    var child = new GameObject(original.name); child.transform.SetParent(actor.transform, false);
                    child.transform.localPosition = source.transform.InverseTransformPoint(original.transform.position);
                    child.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * original.transform.rotation;
                    var scale = original.transform.lossyScale; var basis = source.transform.lossyScale;
                    child.transform.localScale = new Vector3(scale.x / basis.x, scale.y / basis.y, scale.z / basis.z);
                    var renderer = child.AddComponent<SpriteRenderer>();
                    renderer.sprite = original.sprite; renderer.color = original.color;
                    renderer.flipX = original.flipX; renderer.flipY = original.flipY;
                    renderer.sortingLayerID = original.sortingLayerID; renderer.sortingOrder = original.sortingOrder;
                    count++;
                }
            }
            if (count == 0)
            {
                actor.transform.localScale = Vector3.one;
                var marker = Square("UnitMarker", actor.transform, 1, new Color(0.9f, 0.7f, 0.3f));
                marker.transform.localScale = Vector3.one * _cellSize * 0.45f;
            }
            var labelObject = new GameObject("PreparationStarLevel");
            labelObject.transform.SetParent(actor.transform, false);
            // 프리팹 배율과 관계없이 읽을 수 있는 크기로 표시한다. 전투 프리팹에는 추가하지 않는다.
            var scaleForLabel = actor.transform.lossyScale;
            labelObject.transform.localScale = new Vector3(1 / scaleForLabel.x, 1 / scaleForLabel.y, 1 / scaleForLabel.z);
            float top = actor.transform.position.y + _cellSize * 0.5f;
            foreach (var sprite in actor.GetComponentsInChildren<SpriteRenderer>()) top = Mathf.Max(top, sprite.bounds.max.y);
            labelObject.transform.position = new Vector3(actor.transform.position.x, top + _cellSize * 0.08f, actor.transform.position.z - _cellSize * 0.05f);
            var label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = _cellSize * 0.06f;
            label.color = new Color(1f, 0.85f, 0.3f);
            labelObject.GetComponent<MeshRenderer>().sortingOrder = 500;
            return actor;
        }
        private static bool IsActiveVisual(Transform child, Transform root)
        {
            for (var current = child; current != null && current != root; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }
        private SpriteRenderer Square(string name, Transform parent, int order, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = _solid;
            renderer.sortingOrder = order; renderer.color = color; return renderer;
        }
        private void RenderSquares(List<SpriteRenderer> renderers, IEnumerable<Vector2Int> cells, Color color, int order, float scale)
        {
            int index = 0;
            foreach (var cell in cells)
            {
                if (index == renderers.Count) renderers.Add(Square("PlacementOverlay", _root.transform, order, color));
                var renderer = renderers[index++]; renderer.gameObject.SetActive(true);
                renderer.color = color; renderer.transform.position = _mapping.GetWorldPosition(cell);
                renderer.transform.localScale = Vector3.one * _cellSize * scale;
            }
            for (; index < renderers.Count; index++) renderers[index].gameObject.SetActive(false);
        }
        public void Dispose()
        {
            _grid.Changed -= Refresh;
            _root.SetActive(false); UnityEngine.Object.Destroy(_root); UnityEngine.Object.Destroy(_solid);
        }
    }
}
