using System.Reflection;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.InGame;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 「배치 영역 확장」 보상을 골랐을 때 끌어다 놓는 조각을 손패와 같은 카드 모양으로 보여 준다.
    /// 지금까지는 옛 UI Toolkit 의 초록색 안내 줄(FLOOR +2 — Drag this required reward…)이 그 자리였다.
    /// - 카드 그림은 손패의 배치 카드(BattleCard_LandSlot)를 그대로 쓰고, 옛 안내 줄은 투명하게 만들어 같은 자리에서 "눌러서 끌기" 입력만 받게 한다.
    /// - 끌고 있는 동안에는 카드를 흐리게 해서 바닥에 올라간 미리보기가 잘 보이게 한다.
    /// </summary>
    public sealed class InGameExpansionCard : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SerializeField, Tooltip("손패의 배치 카드 프리팹(BattleCard_LandSlot)")] private GameObject _cardPrefab;
        [SerializeField, Range(0.2f, 0.6f), Tooltip("화면 높이 대비 카드 높이")] private float _heightRatio = 0.34f;
        [SerializeField, Range(0.1f, 0.6f), Tooltip("카드 가운데의 세로 위치(화면 높이 대비, 아래가 0)")] private float _centerY = 0.30f;

        private GridPrototypeRunner _runner;
        private Label _legacy;
        private Canvas _canvas;
        private RectTransform _card;
        private CanvasGroup _group;
        private bool _filled;
        private string _shownShape;

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void LateUpdate()
        {
            if (_cardPrefab == null) return;
            if (_runner == null) _runner = FindFirstObjectByType<GridPrototypeRunner>(FindObjectsInactive.Include);
            var grid = _runner != null ? _runner.Manager : null;
            if (grid == null) return;

            bool show = grid.Phase == eGridPhase.PREPARATION && grid.RequiresExpansionPlacement;
            if (!show)
            {
                if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
                return;
            }
            EnsureCard(grid);
            _canvas.enabled = true;
            _group.alpha = grid.IsExpansionDrag ? 0.28f : 1f;
            SyncLegacy();
        }

        private void EnsureCard(GridManager grid)
        {
            if (_canvas == null)
            {
                var go = new GameObject("ExpansionCard", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                _canvas = go.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 12;
                var instance = Instantiate(_cardPrefab, go.transform, false);
                instance.name = "ExpansionCardVisual";
                _card = (RectTransform)instance.transform;
                _card.anchorMin = _card.anchorMax = Vector2.zero;
                _card.pivot = new Vector2(0.5f, 0.5f);
                _group = instance.AddComponent<CanvasGroup>();
                _group.blocksRaycasts = false;      // 입력은 아래의 옛 안내 줄(투명)이 받는다
                _group.interactable = false;
                foreach (var graphic in instance.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
                _filled = false;
            }
            Layout();
            var shapeNow = grid.PendingExpansionShape;
            if (_filled && _shownShape == shapeNow.Id) return;
            var view = _card.GetComponent<UIBattlePreparationCardView>();
            if (view == null) return;
            view.SetTitle("배치 영역 확장");
            view.SetAreaDescription("바닥 +" + shapeNow.Cells.Count + "칸", string.Empty); // 자세한 설명은 InGameUiPolish 가 채운다
            var cells = shapeNow.GetCells(Vector2Int.zero, 0, false);
            int minX = int.MaxValue, maxY = int.MinValue;
            foreach (var c in cells) { minX = Mathf.Min(minX, c.x); maxY = Mathf.Max(maxY, c.y); }
            var shape = new Vector2Int[cells.Length];
            for (int i = 0; i < cells.Length; i++) shape[i] = new Vector2Int(cells[i].x - minX, maxY - cells[i].y);
            view.SetFootprint(shape);
            _shownShape = shapeNow.Id;
            _filled = true;
        }

        private void Layout()
        {
            const float designHeight = 1100f;
            float scale = Screen.height * _heightRatio / designHeight;
            _card.localScale = new Vector3(scale, scale, 1f);
            _card.anchoredPosition = new Vector2(Screen.width * 0.5f, Screen.height * _centerY);
        }

        /// <summary>옛 안내 줄을 카드 자리와 같은 크기로 옮기고 투명하게 만든다(눌러서 끌기 입력은 그대로 받는다).</summary>
        private void SyncLegacy()
        {
            if (_legacy == null || _legacy.panel == null)
            {
                _legacy = _runner.GetType().GetField("_expansionCard", Private)?.GetValue(_runner) as Label;
                if (_legacy == null || _legacy.panel == null) return;
            }
            var parent = _legacy.parent;
            if (parent == null) return;
            var corners = new Vector3[4];
            _card.GetWorldCorners(corners);
            // 카드 그림 전체 크기 기준(프리팹 본체 600x1100 중 보이는 카드 부분: 가운데 80% 폭, 90% 높이)
            Vector2 min = corners[0], max = corners[2];
            Vector2 size = max - min;
            Vector2 insetMin = min + new Vector2(size.x * 0.1f, size.y * 0.05f);
            Vector2 insetMax = max - new Vector2(size.x * 0.1f, size.y * 0.05f);
            var panel = _legacy.panel;
            Vector2 tl = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(insetMin.x, insetMax.y));
            Vector2 br = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(insetMax.x, insetMin.y));
            Vector2 ltl = parent.WorldToLocal(tl), lbr = parent.WorldToLocal(br);
            _legacy.style.position = Position.Absolute;
            _legacy.style.left = ltl.x;
            _legacy.style.top = ltl.y;
            _legacy.style.width = lbr.x - ltl.x;
            _legacy.style.height = lbr.y - ltl.y;
            _legacy.style.marginTop = 0;
            _legacy.style.backgroundColor = Color.clear;
            _legacy.style.color = Color.clear;
            _legacy.style.opacity = 0.01f;
        }
    }
}
