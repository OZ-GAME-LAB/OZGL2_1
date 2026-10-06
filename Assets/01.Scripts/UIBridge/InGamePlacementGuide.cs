using System.Collections.Generic;
using OZGL2.Grid;
using OZGL2.InGame;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 배치 화면의 안내 두 가지.
    /// 1) 유닛이 차지한 영역: 놓인 유닛마다 자기 칸(2성·3성은 여러 칸)을 색 판과 굵은 테두리로 묶어 보여 준다.
    ///    1성은 흰 테두리, 2성은 푸른색, 3성은 금색이라 "이 유닛이 어디까지 차지하나"가 한눈에 보인다.
    /// 2) 놓을 수 없는 이유: 유닛·발판·확장을 끌고 있는데 지금 자리가 안 되면 커서 옆에 이유를 알려 준다
    ///    (바닥 밖, 발판이 없음, 이미 놓은 발판과 이어지지 않음 등). 이유가 안 보여서 "왜 안 되는지" 알 수 없던 것을 없앤다.
    /// </summary>
    public sealed class InGamePlacementGuide : MonoBehaviour
    {
        [SerializeField] private int _fillOrder = -73;
        [SerializeField] private int _borderOrder = -72;
        [SerializeField, Range(0.03f, 0.2f), Tooltip("영역 테두리 굵기(칸 단위)")] private float _borderWidth = 0.09f;

        // 마왕성 UI의 붉은 테두리·금 장식·뼈색에 맞춘 색: 1성 뼈색, 2성 진홍, 3성 금빛
        private static readonly Color Star1 = new Color(0.92f, 0.84f, 0.68f, 0.85f);
        private static readonly Color Star2 = new Color(0.86f, 0.22f, 0.26f, 1f);
        private static readonly Color Star3 = new Color(0.98f, 0.76f, 0.25f, 1f);
        private static readonly Color Shadow = new Color(0.05f, 0.02f, 0.03f, 0.62f);

        private InGamePrototypeBootstrap _bootstrap;
        private Camera _camera;
        private Sprite _solid;
        private Transform _root;
        private readonly List<SpriteRenderer> _fills = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _borders = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _shadows = new List<SpriteRenderer>();
        private float _next;

        private Canvas _canvas;
        private RectTransform _toast;
        private TMP_Text _toastText;
        private Image _toastBg;

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void LateUpdate()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            var session = _bootstrap != null ? _bootstrap.GridSession : null;
            var grid = session != null ? session.Grid : null;
            var config = _bootstrap != null ? _bootstrap.Config : null;
            if (grid == null || config == null) { HideAll(); return; }

            bool preparing = grid.Phase == eGridPhase.PREPARATION;
            if (preparing)
            {
                if (Time.unscaledTime >= _next) { _next = Time.unscaledTime + 0.08f; RefreshAreas(grid, config); }
            }
            else HideAreas();

            RefreshToast(grid, config, preparing);
        }

        // ───────────── 유닛이 차지한 영역

        private void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("UnitAreas").transform;
            _root.SetParent(transform, false);
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white); tex.Apply();
            _solid = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private SpriteRenderer Get(List<SpriteRenderer> pool, int index, int order)
        {
            if (index == pool.Count)
            {
                var go = new GameObject("Area");
                go.transform.SetParent(_root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _solid; sr.sortingOrder = order;
                pool.Add(sr);
            }
            var r = pool[index];
            r.gameObject.SetActive(true);
            return r;
        }

        private void RefreshAreas(GridManager grid, InGamePrototypeConfigSO config)
        {
            EnsureRoot();
            float cell = config.CellWorldSize;
            Vector3 origin = config.GridWorldOrigin;
            string dragged = grid.DragKind == eGridDragKind.UNIT ? grid.SelectedId : null;
            int fill = 0, border = 0, shadow = 0;

            foreach (var unit in grid.Units)
            {
                if (!unit.IsPlaced || unit.InstanceId == dragged) continue;
                var cells = unit.GetCells();
                var set = new HashSet<Vector2Int>(cells);
                Color color = unit.StarLevel >= 3 ? Star3 : unit.StarLevel == 2 ? Star2 : Star1;
                float fillAlpha = unit.StarLevel >= 3 ? 0.28f : unit.StarLevel == 2 ? 0.30f : 0.08f;

                foreach (var c in cells)
                {
                    if (fillAlpha > 0f)
                    {
                        var f = Get(_fills, fill++, _fillOrder);
                        f.transform.position = origin + new Vector3(c.x, c.y, 0f) * cell;
                        f.transform.localScale = new Vector3(cell * 0.96f, cell * 0.96f, 1f);
                        f.color = new Color(color.r, color.g, color.b, fillAlpha);
                    }
                    // 바깥쪽 변에만 테두리를 그려 여러 칸이 하나의 덩어리로 보이게 한다
                    for (int side = 0; side < 4; side++)
                    {
                        var dir = side == 0 ? Vector2Int.left : side == 1 ? Vector2Int.right : side == 2 ? Vector2Int.up : Vector2Int.down;
                        if (set.Contains(c + dir)) continue;
                        bool vertical = side < 2;
                        // 어두운 바깥 그림자 줄을 먼저 깔아 풀색 바닥 위에서도 테두리가 또렷하게 한다
                        float sw = _borderWidth * 1.9f * cell;
                        var sh = Get(_shadows, shadow++, _borderOrder - 1);
                        Vector2 sc = new Vector2(c.x, c.y) + new Vector2(dir.x, dir.y) * (0.5f - _borderWidth * 0.95f * 0.5f);
                        sh.transform.position = origin + new Vector3(sc.x, sc.y, 0f) * cell;
                        sh.transform.localScale = vertical ? new Vector3(sw, cell, 1f) : new Vector3(cell, sw, 1f);
                        sh.color = Shadow;
                        var b = Get(_borders, border++, _borderOrder);
                        float w = _borderWidth * cell;
                        Vector2 center = new Vector2(c.x, c.y) + new Vector2(dir.x, dir.y) * (0.5f - _borderWidth * 0.5f);
                        b.transform.position = origin + new Vector3(center.x, center.y, 0f) * cell;
                        b.transform.localScale = vertical ? new Vector3(w, cell, 1f) : new Vector3(cell, w, 1f);
                        b.color = color;
                    }
                }
            }
            for (int i = fill; i < _fills.Count; i++) _fills[i].gameObject.SetActive(false);
            for (int i = border; i < _borders.Count; i++) _borders[i].gameObject.SetActive(false);
            for (int i = shadow; i < _shadows.Count; i++) _shadows[i].gameObject.SetActive(false);
        }

        private void HideAreas()
        {
            foreach (var r in _fills) if (r != null && r.gameObject.activeSelf) r.gameObject.SetActive(false);
            foreach (var r in _borders) if (r != null && r.gameObject.activeSelf) r.gameObject.SetActive(false);
            foreach (var r in _shadows) if (r != null && r.gameObject.activeSelf) r.gameObject.SetActive(false);
        }

        private void HideAll()
        {
            HideAreas();
            if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
        }

        // ───────────── 놓을 수 없는 이유

        private static string Reason(ePlacementFailure failure, eGridDragKind kind)
        {
            switch (failure)
            {
                case ePlacementFailure.OUTSIDE_BOUNDS: return "배치 영역 밖이에요";
                case ePlacementFailure.NO_FLOOR: return kind == eGridDragKind.UNIT ? "바닥(갈색 칸) 위에만 놓을 수 있어요" : "바닥이 있는 칸에만 놓을 수 있어요";
                case ePlacementFailure.NO_BLOCK: return "발판이 있는 칸에만 올릴 수 있어요\n먼저 발판을 놓아 주세요";
                case ePlacementFailure.OCCUPIED: return "이미 다른 유닛·발판이 있어요";
                case ePlacementFailure.DISCONNECTED: return "이미 놓은 발판과 이어서 놓아야 해요";
                case ePlacementFailure.FLOOR_EXISTS: return "이미 바닥이 있는 칸이에요\n초록 칸에 놓아 주세요";
                case ePlacementFailure.EXPANSION_OCCUPIED: return "확장 영역과 겹쳐요";
                case ePlacementFailure.CANNOT_STORE_EXPANSION: return "확장 영역은 보관할 수 없어요";
                case ePlacementFailure.STORAGE_PENDING: return "보관함 정리가 먼저예요";
                default: return null;
            }
        }

        private void RefreshToast(GridManager grid, InGamePrototypeConfigSO config, bool preparing)
        {
            string text = null;
            Vector2 pointer = default;
            if (preparing && grid.HasSelection && Mouse.current != null)
            {
                var failure = grid.GetPreviewFailure();
                pointer = Mouse.current.position.ReadValue();
                if (failure != ePlacementFailure.NONE && PointerOverBoard(grid, config, pointer))
                    text = Reason(failure, grid.DragKind);
            }
            if (text == null)
            {
                if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
                return;
            }
            EnsureToast();
            _canvas.enabled = true;
            _toastText.text = text;
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            _toastText.ForceMeshUpdate();
            var size = _toastText.GetPreferredValues(text);
            _toast.sizeDelta = new Vector2(size.x + 36f * s, size.y + 18f * s);
            // 커서 오른쪽 위에 띄우되 화면 밖으로 나가지 않게 한다
            float x = Mathf.Clamp(pointer.x + 24f * s, _toast.sizeDelta.x * 0.5f + 8f, Screen.width - _toast.sizeDelta.x * 0.5f - 8f);
            float y = Mathf.Clamp(pointer.y + 54f * s, _toast.sizeDelta.y * 0.5f + 8f, Screen.height - _toast.sizeDelta.y * 0.5f - 8f);
            _toast.anchoredPosition = new Vector2(x, y);
        }

        private bool PointerOverBoard(GridManager grid, InGamePrototypeConfigSO config, Vector2 pointer)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;
            float cell = config.CellWorldSize;
            var size = grid.Definition.MaximumSize;
            Vector3 origin = config.GridWorldOrigin;
            Vector3 a = _camera.WorldToScreenPoint(origin + new Vector3(-0.5f, -0.5f, 0f) * cell);
            Vector3 b = _camera.WorldToScreenPoint(origin + new Vector3(size.x - 0.5f, size.y - 0.5f, 0f) * cell);
            var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return rect.Contains(pointer);
        }

        private void EnsureToast()
        {
            if (_canvas != null) return;
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            var go = new GameObject("PlacementToast", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40;
            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(go.transform, false);
            _toast = (RectTransform)boxGo.transform;
            _toast.anchorMin = _toast.anchorMax = Vector2.zero;
            _toast.pivot = new Vector2(0.5f, 0.5f);
            _toastBg = boxGo.GetComponent<Image>();
            _toastBg.color = new Color(0.12f, 0.05f, 0.06f, 0.94f);
            _toastBg.raycastTarget = false;
            var outline = boxGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.45f, 0.4f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(boxGo.transform, false);
            _toastText = textGo.AddComponent<TextMeshProUGUI>();
            if (UiFontOverride.Current != null) _toastText.font = UiFontOverride.Current;
            _toastText.fontSize = 26f * s;
            _toastText.color = new Color(1f, 0.9f, 0.86f);
            _toastText.alignment = TextAlignmentOptions.Center;
            _toastText.raycastTarget = false;
            _toastText.textWrappingMode = TextWrappingModes.NoWrap;
            var tr = _toastText.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            _canvas.enabled = false;
        }
    }
}
