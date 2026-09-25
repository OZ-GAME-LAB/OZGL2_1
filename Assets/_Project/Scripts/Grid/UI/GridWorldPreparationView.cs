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
        private readonly List<SpriteRenderer> _invalid = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _occupied = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _hover = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _invalidBorders = new List<SpriteRenderer>();
        private readonly Sprite _solid;
        private readonly float _cellSize;
        private GameObject _dragActor;
        private string _dragId;
        private int _dragStar;
        private LineRenderer _rangeLine;
        private Material _rangeMaterial;
        private const int RANGE_SEGMENTS = 48;
        public bool IsVisible { get; private set; }
        /// <summary>(콘텐츠 ID, 성급) → 사거리(월드 단위). Grid가 전투 코드를 직접 참조하지 않도록 외부에서 주입한다. 없으면 사거리 표시 안 함.</summary>
        public Func<string, int, float> RangeProvider { get; set; }
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
            bool preparing = _grid.Phase == eGridPhase.PREPARATION;
            string selected = _grid.DragKind == eGridDragKind.UNIT ? _grid.SelectedId : null;
            var cells = preparing && _grid.HasSelection ? _grid.GetPreviewCells() : Array.Empty<Vector2Int>();
            RenderSquares(_ghost, cells, _grid.GetPreviewFailure() == ePlacementFailure.NONE ? GridBoardView.VALID_COLOR : GridBoardView.INVALID_COLOR, -60, 0.94f);
            var frontier = preparing && (_grid.RequiresExpansionPlacement || _grid.IsExpansionDrag) ? _grid.GetExpansionFrontier() : Array.Empty<Vector2Int>();
            RenderSquares(_frontier, frontier, new Color(0.4f, 0.8f, 0.6f, 0.22f), -90, 0.86f);
            // 합성으로 발판이 커져서 자리가 부족한 유닛이 있으면 그 칸을 빨갛게 표시한다.
            // 합성 자체는 막지 않고(GridManager.CanFuseUnits), 이 칸이 남아있는 동안 CanBeginBattle이
            // false가 되어 정리하기 전까진 웨이브를 시작할 수 없다.
            var invalid = preparing ? _grid.GetInvalidCells(selected) : Array.Empty<Vector2Int>();
            RenderSquares(_occupied, preparing ? _grid.GetOccupiedCells(selected) : Array.Empty<Vector2Int>(), GridBoardView.OCCUPIED_COLOR, -80, 0.94f);
            RenderSquares(_invalid, invalid, GridBoardView.INVALID_PLACEMENT_COLOR, -70, 0.98f);
            RenderInvalidBorders(invalid);
            var hovered = preparing ? _grid.FindExpansion(_grid.HoveredExpansionId) : null;
            RenderSquares(_hover, hovered != null ? hovered.GetCells() : Array.Empty<Vector2Int>(),
                hovered == null || _grid.GetExpansionMoveFailure(hovered.InstanceId) == ePlacementFailure.NONE ? GridBoardView.EXPANSION_HOVER_COLOR : GridBoardView.INVALID_COLOR, -65, 0.94f);
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
            UpdateRangeIndicator(selected, selectedStar);
        }
        /// <summary>유닛을 누르고(드래그) 있는 동안만 드래그 위치 기준으로 사거리 원을 표시한다.</summary>
        private void UpdateRangeIndicator(string selectedId, int starLevel)
        {
            float range = 0f;
            if (_dragActor != null && selectedId != null && RangeProvider != null)
                range = RangeProvider(_grid.FindUnit(selectedId).Definition.Id, starLevel);
            if (range <= 0f)
            {
                if (_rangeLine != null) _rangeLine.gameObject.SetActive(false);
                return;
            }
            if (_rangeLine == null)
            {
                var go = new GameObject("RangeIndicator"); go.transform.SetParent(_root.transform, false);
                _rangeLine = go.AddComponent<LineRenderer>();
                _rangeMaterial = new Material(Shader.Find("Sprites/Default"));
                _rangeLine.material = _rangeMaterial;
                _rangeLine.useWorldSpace = true; _rangeLine.loop = true;
                _rangeLine.positionCount = RANGE_SEGMENTS;
                _rangeLine.startWidth = _rangeLine.endWidth = _cellSize * 0.05f;
                _rangeLine.startColor = _rangeLine.endColor = new Color(1f, 1f, 1f, 0.7f);
                _rangeLine.sortingOrder = 600;
            }
            _rangeLine.gameObject.SetActive(true);
            Vector3 center = _dragActor.transform.position;
            for (int i = 0; i < RANGE_SEGMENTS; i++)
            {
                float angle = (float)i / RANGE_SEGMENTS * Mathf.PI * 2f;
                _rangeLine.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * range, Mathf.Sin(angle) * range, 0f));
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
        private void RenderInvalidBorders(IEnumerable<Vector2Int> cells)
        {
            int index = 0;
            foreach (var cell in cells)
                for (int side = 0; side < 4; side++)
                {
                    if (index == _invalidBorders.Count) _invalidBorders.Add(Square("InvalidPlacementBorder", _root.transform, -69, Color.red));
                    var renderer = _invalidBorders[index++]; renderer.gameObject.SetActive(true);
                    var offset = side == 0 ? Vector2.left : side == 1 ? Vector2.right : side == 2 ? Vector2.up : Vector2.down;
                    renderer.transform.position = _mapping.GetWorldPosition((Vector2)cell + offset * 0.47f);
                    renderer.transform.localScale = new Vector3(side < 2 ? 0.035f : 0.975f, side < 2 ? 0.975f : 0.035f, 1) * _cellSize;
                }
            for (; index < _invalidBorders.Count; index++) _invalidBorders[index].gameObject.SetActive(false);
        }
        public void Dispose()
        {
            _grid.Changed -= Refresh;
            _root.SetActive(false); UnityEngine.Object.Destroy(_root); UnityEngine.Object.Destroy(_solid);
            if (_rangeMaterial != null) UnityEngine.Object.Destroy(_rangeMaterial);
        }
    }
}
