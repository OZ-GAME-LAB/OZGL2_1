using System.Collections.Generic;
using OZGL2.Grid.UI;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 프로젝트의 그리드 테마(잔디 타일)를 화면 전체에 깔아 단색 배경을 대신하고, 마왕군 진영과 용사군 진영을 색으로 구분한다.
    /// 그리드가 만든 칸 위치(Surface_x_y)를 기준으로 같은 격자에 맞춰 이어 붙이므로 경계가 어긋나지 않는다.
    /// - 용사군 진영(그리드 위쪽, 용사가 나타나 내려오는 쪽): 밝고 서늘한 푸른빛 잔디 + 파란 깃발
    /// - 마왕군 진영(그리드와 그 아래쪽): 어둡고 붉은 보랏빛이 도는 땅 + 붉은 깃발
    /// - 두 진영 사이는 몇 칸에 걸쳐 서서히 섞인다.
    /// 카메라가 줌 아웃해도 모자라지 않게 시야보다 넓게 깔고, 모자라면 자동으로 넓힌다.
    /// </summary>
    public sealed class InGameTileBackground : MonoBehaviour
    {
        private const int MaxTiles = 6000;
        [SerializeField] private GridBoardThemeSO _theme;
        [SerializeField, Min(1f)] private float _coverMargin = 1.6f;
        [SerializeField, Range(0f, 0.15f)] private float _tileVariation = 0.04f;
        [SerializeField, Range(0f, 0.6f)] private float _edgeDarken = 0.3f;
        [Header("진영 색 (타일 기본색에 곱해진다)")]
        [SerializeField] private Color _heroTint = new Color(0.78f, 1.02f, 1.18f, 1f);
        [SerializeField] private Color _demonTint = new Color(1.05f, 0.72f, 0.82f, 1f);
        [SerializeField, Min(1f)] private float _blendRows = 3f;
        [SerializeField, Min(0)] private int _heroSideRowsAboveGrid = 1;
        [SerializeField] private bool _showFactionFlags = false;

        private Transform _root;
        private Sprite _sprite;
        private Color _tint = Color.white;
        private Vector3 _origin, _right, _up;
        private Quaternion _rotation;
        private bool _hasLattice;
        private int _gridMaxX, _gridMaxY;
        private RectInt _built;
        private bool _hasBuilt;
        private float _nextScan;
        private Sprite _heroFlag, _demonFlag;

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;
            if (_theme == null) { enabled = false; return; }
            if (!_hasLattice && !TryFindLattice()) return;
            EnsureCoverage();
        }

        private bool TryFindLattice()
        {
            var g00 = GameObject.Find("Surface_0_0");
            var g10 = GameObject.Find("Surface_1_0");
            var g01 = GameObject.Find("Surface_0_1");
            if (g00 == null || g10 == null || g01 == null) return false;
            _origin = g00.transform.position;
            _right = g10.transform.position - _origin;
            _up = g01.transform.position - _origin;
            _rotation = g00.transform.rotation;
            _sprite = _theme.GetSprite(eGridCellSurface.UNCLAIMED);
            _tint = _theme.GetTint(eGridCellSurface.UNCLAIMED);
            _hasLattice = _sprite != null && _right.sqrMagnitude > 1e-6f && _up.sqrMagnitude > 1e-6f;
            if (!_hasLattice) return false;

            // 그리드가 차지하는 칸 범위(Surface_x_y 이름에서 읽는다) — 진영 경계 기준
            _gridMaxX = _gridMaxY = 0;
            foreach (var t in g00.transform.parent.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Surface_")) continue;
                var parts = t.name.Split('_');
                if (parts.Length == 3 && int.TryParse(parts[1], out int px) && int.TryParse(parts[2], out int py))
                {
                    _gridMaxX = Mathf.Max(_gridMaxX, px);
                    _gridMaxY = Mathf.Max(_gridMaxY, py);
                }
            }

            var cam = Camera.main;
            if (cam != null) // 타일 사이 틈이 비쳐도 눈에 띄지 않게 배경색을 어두운 잔디 톤으로
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(_tint.r * 0.3f, _tint.g * 0.4f, _tint.b * 0.3f, 1f);
            }
            _heroFlag = CreateFlagSprite(new Color(0.25f, 0.55f, 1f));
            _demonFlag = CreateFlagSprite(new Color(0.85f, 0.15f, 0.25f));
            return true;
        }

        private void EnsureCoverage()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // 카메라가 직교든 원근이든, 화면 네 모서리에서 쏜 광선이 그리드 평면과 만나는 점으로 필요한 칸 범위를 구한다.
            // 줌아웃(전투 전환)에도 모자라지 않게 시야보다 넓게 잡고, 칸이 너무 많아지면 여유를 줄여 본다.
            for (float margin = _coverMargin; margin >= 1f; margin -= 0.15f)
            {
                if (!TryGetCoverage(cam, margin, out int minX, out int minY, out int maxX, out int maxY, out Vector3 center, out float radius)) return;
                if ((maxX - minX + 1) * (maxY - minY + 1) > MaxTiles) continue;
                if (_hasBuilt)
                {
                    if (minX >= _built.xMin && maxX <= _built.xMax && minY >= _built.yMin && maxY <= _built.yMax) return;
                    minX = Mathf.Min(minX, _built.xMin); maxX = Mathf.Max(maxX, _built.xMax);
                    minY = Mathf.Min(minY, _built.yMin); maxY = Mathf.Max(maxY, _built.yMax);
                    if ((maxX - minX + 1) * (maxY - minY + 1) > MaxTiles) return;
                }
                Build(minX, minY, maxX, maxY, center, radius);
                return;
            }
        }

        private bool TryGetCoverage(Camera cam, float margin, out int minX, out int minY, out int maxX, out int maxY,
            out Vector3 center, out float radius)
        {
            minX = minY = int.MaxValue; maxX = maxY = int.MinValue;
            center = _origin; radius = 1f;
            var plane = new Plane(Vector3.Cross(_right, _up).normalized, _origin);
            float lo = 0.5f - 0.5f * margin, hi = 0.5f + 0.5f * margin;
            var points = new Vector3[4];
            int index = 0;
            foreach (float vx in new[] { lo, hi })
                foreach (float vy in new[] { lo, hi })
                {
                    var ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
                    if (!plane.Raycast(ray, out float distance)) return false; // 평면과 만나지 않으면 이번엔 건너뜀
                    points[index++] = ray.GetPoint(distance);
                }
            var middle = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            center = plane.Raycast(middle, out float centerDistance) ? middle.GetPoint(centerDistance) : points[0];
            foreach (var point in points)
            {
                Vector3 relative = point - _origin;
                float u = Vector3.Dot(relative, _right) / _right.sqrMagnitude;
                float v = Vector3.Dot(relative, _up) / _up.sqrMagnitude;
                minX = Mathf.Min(minX, Mathf.FloorToInt(u)); maxX = Mathf.Max(maxX, Mathf.CeilToInt(u));
                minY = Mathf.Min(minY, Mathf.FloorToInt(v)); maxY = Mathf.Max(maxY, Mathf.CeilToInt(v));
                radius = Mathf.Max(radius, Vector3.Distance(point, center));
            }
            return true;
        }

        private void Build(int minX, int minY, int maxX, int maxY, Vector3 viewCenter, float viewRadius)
        {
            if ((maxX - minX + 1) * (maxY - minY + 1) > MaxTiles) return; // 비정상적으로 큰 시야는 무시
            if (_root != null) Destroy(_root.gameObject);
            _root = new GameObject("TileBackground").transform;
            _root.SetParent(transform, false);

            Vector3 scale = new Vector3(_right.magnitude / _sprite.bounds.size.x, _up.magnitude / _sprite.bounds.size.y, 1f);
            float heroStartRow = _gridMaxY + _heroSideRowsAboveGrid + 0.5f; // 이 줄 위부터 용사군 진영이 섞이기 시작
            for (int y = minY; y <= maxY; y++)
            {
                // 0 = 마왕군 진영, 1 = 용사군 진영 (경계에서 부드럽게 전환)
                float hero = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((y - heroStartRow) / _blendRows));
                Color faction = Color.Lerp(_demonTint, _heroTint, hero);
                for (int x = minX; x <= maxX; x++)
                {
                    var tile = new GameObject("Grass_" + x + "_" + y);
                    tile.transform.SetParent(_root, false);
                    Vector3 position = _origin + x * _right + y * _up;
                    tile.transform.SetPositionAndRotation(position, _rotation);
                    tile.transform.localScale = scale;
                    var renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sprite = _sprite;
                    renderer.sortingOrder = -200; // 그리드 바닥(-100)보다 뒤
                    // 칸마다 아주 살짝 밝기를 달리해 단조로운 반복을 줄이고, 시야 가장자리는 어둡게(비네팅)
                    float hash = (((x * 73856093) ^ (y * 19349663)) & 0xFFFF) / 65535f - 0.5f;
                    float variation = 1f + hash * 2f * _tileVariation;
                    float edge = Mathf.Clamp01(Vector2.Distance(position, viewCenter) / (viewRadius * 0.9f));
                    float vignette = 1f - _edgeDarken * edge * edge;
                    var color = _tint * faction * (variation * vignette);
                    color.a = 1f;
                    renderer.color = color;
                }
            }
            _built = new RectInt(minX, minY, maxX - minX, maxY - minY);
            _hasBuilt = true;
            if (_showFactionFlags) PlaceFlags(minX, maxX, minY, maxY, scale);
        }

        /// <summary>진영 표시 깃발 — 용사군은 위쪽에 파란 깃발 줄, 마왕군은 그리드 양옆에 붉은 깃발.</summary>
        private void PlaceFlags(int minX, int maxX, int minY, int maxY, Vector3 tileScale)
        {
            float unit = _up.magnitude; // 한 칸 크기
            float flagScale = unit * 1.6f / (12f / 16f); // 12픽셀 높이 스프라이트(16PPU)가 칸의 약 1.6배가 되게
            var flagParent = new GameObject("FactionFlags").transform;
            flagParent.SetParent(_root, false);

            int heroRow = _gridMaxY + _heroSideRowsAboveGrid + 3; // 용사가 나타나 내려오는 쪽
            if (heroRow <= maxY)
                for (int x = minX + 1; x <= maxX - 1; x += 3)
                    AddFlag(flagParent, _heroFlag, x, heroRow, flagScale);

            // 마왕군: 그리드 좌우 바깥(성벽 쪽)
            int demonRow = Mathf.Max(minY + 1, _gridMaxY / 2);
            AddFlag(flagParent, _demonFlag, -1, demonRow, flagScale);
            AddFlag(flagParent, _demonFlag, _gridMaxX + 1, demonRow, flagScale);
            if (_gridMaxY >= 2)
            {
                AddFlag(flagParent, _demonFlag, -1, demonRow + 2, flagScale);
                AddFlag(flagParent, _demonFlag, _gridMaxX + 1, demonRow + 2, flagScale);
            }
        }

        private void AddFlag(Transform parent, Sprite sprite, int x, int y, float scale)
        {
            if (sprite == null) return;
            var flag = new GameObject("Flag_" + x + "_" + y);
            flag.transform.SetParent(parent, false);
            flag.transform.SetPositionAndRotation(_origin + x * _right + y * _up, _rotation);
            flag.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = flag.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -150; // 배경 위, 그리드(-100) 아래
        }

        /// <summary>작은 도트 깃발(12칸 높이)을 코드로 만든다 — 별도 아트 없이도 진영 구분이 읽히게.</summary>
        private static Sprite CreateFlagSprite(Color flagColor)
        {
            const int w = 10, h = 12;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0, 0, 0, 0);
            var pole = new Color(0.22f, 0.15f, 0.1f, 1f);
            var dark = flagColor * 0.65f; dark.a = 1f;
            var light = Color.Lerp(flagColor, Color.white, 0.35f); light.a = 1f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, clear);
            for (int y = 0; y < h; y++) tex.SetPixel(1, y, pole);   // 깃대
            tex.SetPixel(1, h - 1, light); tex.SetPixel(0, h - 1, pole);
            for (int y = 5; y <= 10; y++)                            // 깃발 천: 오른쪽 위아래 모서리를 깎아 펄럭이는 느낌
                for (int x = 2; x <= 8; x++)
                {
                    bool edgeRow = y == 5 || y == 10, nearEdgeRow = y == 6 || y == 9;
                    if ((edgeRow && x >= 7) || (nearEdgeRow && x >= 8)) continue;
                    tex.SetPixel(x, y, y >= 9 ? light : (y <= 6 ? dark : flagColor));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.15f, 0.04f), 16f);
        }
    }
}
