using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>배치 규칙을 바꾸지 않고 같은 셀 좌표계로 카메라 전체에 야외 지면을 표시한다.</summary>
    public sealed class OutdoorBattlefieldPresentation : MonoBehaviour
    {
        [SerializeField] private InGamePrototypeConfigSO _gameConfig;
        [SerializeField] private OutdoorBattlefieldConfigSO _appearance;
        [SerializeField] private Camera _camera;
        private readonly Dictionary<Vector2Int, SpriteRenderer> _tiles = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _spares = new Stack<SpriteRenderer>();
        private readonly List<Vector2Int> _outside = new List<Vector2Int>();
        private Transform _root;
        private Vector3 _origin;
        private float _cellSize;
        private Vector2Int _gridSize;
        private RectInt _coverage;
        private bool _hasCoverage;
        private bool _hasWarned;
        private Matrix4x4 _lastView;
        private Matrix4x4 _lastProjection;

        private void OnEnable()
        {
            if (_gameConfig == null || _appearance == null || _camera == null) return;
            _origin = _gameConfig.GridWorldOrigin;
            _cellSize = _gameConfig.CellWorldSize;
            _gridSize = _gameConfig.Catalog.CreateDefinition().MaximumSize;
            _root = new GameObject("OutdoorBattlefieldTiles").transform;
            _root.SetParent(transform, false);
            for (int i = 0; i < _appearance.ApproachTiles.Length; i++)
            {
                var sprite = _appearance.ApproachTiles[i];
                var cell = _appearance.GetApproachCell(i);
                if (sprite == null || cell.y < _gridSize.y) continue;
                // 잔디를 교체하지 않고 연속된 투명 흔적을 덧그려 셀마다 바탕색이 끊기는 것을 막는다.
                var renderer = CreateRenderer("CampApproach_" + cell.x + "_" + cell.y, -195);
                renderer.sprite = sprite;
                renderer.color = new Color(1f, 1f, 1f, _appearance.ApproachOpacity);
                renderer.transform.position = _origin + new Vector3(cell.x, cell.y) * _cellSize;
                renderer.transform.localScale = new Vector3(_cellSize / sprite.bounds.size.x, _cellSize / sprite.bounds.size.y, 1f);
            }
            foreach (var decoration in _appearance.Decorations)
            {
                if (decoration == null || decoration.Sprite == null) continue;
                // 장식이 최대 배치 범위를 덮는 설정 실수를 방지한다. 중앙 하단은 장식 데이터에서 비워 둔다.
                var p = decoration.CellPosition;
                var half = (Vector2)decoration.Sprite.bounds.size * decoration.Scale * 0.5f;
                if (p.x + half.x >= -0.5f && p.x - half.x <= _gridSize.x - 0.5f &&
                    p.y + half.y >= -0.5f && p.y - half.y <= _gridSize.y - 0.5f) continue;
                var renderer = CreateRenderer("BattlefieldDecoration_" + decoration.Sprite.name, -190);
                renderer.sprite = decoration.Sprite;
                renderer.color = new Color(1f, 1f, 1f, decoration.Opacity);
                renderer.transform.position = _origin + new Vector3(p.x, p.y) * _cellSize;
                renderer.transform.localScale = Vector3.one * (_cellSize * decoration.Scale);
            }
            RefreshCoverage();
        }

        // 준비/전투 카메라가 매 프레임 이동하므로 전환 도중에도 화면 모서리를 채워야 한다.
        private void LateUpdate()
        {
            if (_root == null || _camera == null) return;
            if (_hasCoverage && _lastView == _camera.worldToCameraMatrix && _lastProjection == _camera.projectionMatrix) return;
            RefreshCoverage();
        }

        private void RefreshCoverage()
        {
            var plane = new Plane(Vector3.forward, _origin);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int corner = 0; corner < 4; corner++)
            {
                var ray = _camera.ViewportPointToRay(new Vector3(corner & 1, corner >> 1, 0));
                if (!plane.Raycast(ray, out float distance)) return;
                var point = (Vector2)((ray.GetPoint(distance) - _origin) / _cellSize);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            int margin = _appearance.CoverageMargin;
            int xMin = Mathf.FloorToInt(min.x) - margin, yMin = Mathf.FloorToInt(min.y) - margin;
            int xMax = Mathf.CeilToInt(max.x) + margin, yMax = Mathf.CeilToInt(max.y) + margin;
            var next = new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
            _lastView = _camera.worldToCameraMatrix; _lastProjection = _camera.projectionMatrix;
            if ((long)next.width * next.height > _appearance.MaximumTiles)
            {
                if (!_hasWarned) Debug.LogWarning("Outdoor battlefield coverage exceeds its configured tile limit.", this);
                _hasWarned = true;
                return;
            }
            if (_hasCoverage && _coverage == next) return;
            _outside.Clear();
            foreach (var pair in _tiles) if (!next.Contains(pair.Key)) _outside.Add(pair.Key);
            foreach (var cell in _outside)
            {
                var renderer = _tiles[cell]; renderer.gameObject.SetActive(false);
                _spares.Push(renderer); _tiles.Remove(cell);
            }
            foreach (var cell in next.allPositionsWithin)
            {
                if (_tiles.ContainsKey(cell)) continue;
                var renderer = _spares.Count > 0 ? _spares.Pop() : CreateRenderer("Grass", -200);
                renderer.name = "Grass_" + cell.x + "_" + cell.y;
                renderer.sprite = _appearance.GetGrass(cell);
                renderer.transform.position = _origin + new Vector3(cell.x, cell.y) * _cellSize;
                if (renderer.sprite != null) renderer.transform.localScale = new Vector3(_cellSize / renderer.sprite.bounds.size.x, _cellSize / renderer.sprite.bounds.size.y, 1);
                renderer.gameObject.SetActive(true);
                _tiles.Add(cell, renderer);
            }
            _coverage = next; _hasCoverage = true;
        }

        private SpriteRenderer CreateRenderer(string objectName, int order)
        {
            var tile = new GameObject(objectName);
            tile.transform.SetParent(_root, false);
            var renderer = tile.AddComponent<SpriteRenderer>(); renderer.sortingOrder = order;
            return renderer;
        }

        private void OnDisable()
        {
            if (_root != null) { _root.gameObject.SetActive(false); Destroy(_root.gameObject); }
            _root = null; _tiles.Clear(); _spares.Clear(); _outside.Clear(); _hasCoverage = false; _hasWarned = false;
        }
    }
}
