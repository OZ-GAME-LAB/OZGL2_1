using System.Reflection;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.InGame;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 「배치 영역 확장」 보상을 골랐을 때 끌어다 놓는 조각을 손패와 같은 카드 모양으로 보여 주고, 그 카드를 직접 끌어서 놓게 한다.
    /// 지금까지는 옛 UI Toolkit 의 초록색 안내 줄(FLOOR +2 — Drag this required reward…)이 그 자리였다.
    /// - 카드 그림은 손패의 배치 카드(BattleCard_LandSlot)를 그대로 쓴다.
    /// - 끌기는 이 카드가 uGUI 로 직접 받아서 그리드(BeginExpansionDrag → MovePreview → CommitPreview)를 조작한다.
    ///   (예전에는 투명하게 만든 옛 안내 줄을 같은 자리에 겹쳐 입력만 받게 했는데, 겹친 위치가 어긋나면 카드가 눌리지 않고 확장이 안 됐다.)
    /// - 끌고 있는 동안에는 카드를 흐리게 해서 바닥에 올라간 미리보기가 잘 보이게 하고, R 키로 회전한다. 놓을 수 없는 자리에서 놓으면 카드가 그대로 남는다.
    /// </summary>
    public sealed class InGameExpansionCard : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SerializeField, Tooltip("손패의 배치 카드 프리팹(BattleCard_LandSlot)")] private GameObject _cardPrefab;
        [SerializeField, Range(0.2f, 0.6f), Tooltip("화면 높이 대비 카드 높이")] private float _heightRatio = 0.34f;
        [SerializeField, Range(0.1f, 0.6f), Tooltip("카드 가운데의 세로 위치(화면 높이 대비, 아래가 0)")] private float _centerY = 0.30f;

        private GridPrototypeRunner _runner;
        private InGamePrototypeBootstrap _bootstrap;
        private InGamePhasePresentation _phase;
        private Label _legacy;
        private Canvas _canvas;
        private RectTransform _card;
        private CanvasGroup _group;
        private bool _filled, _dragging;
        private string _shownShape;
        private Camera _camera;

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
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();

            bool show = grid.Phase == eGridPhase.PREPARATION && grid.RequiresExpansionPlacement;
            if (!show)
            {
                _dragging = false;
                if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
                return;
            }
            EnsureCard(grid);
            _canvas.enabled = true;
            _group.alpha = _dragging ? 0.28f : 1f;
            HideLegacy();

            // 끄는 중에는 R 키로 회전, 마우스를 떼는 것이 카드 밖에서 일어나도 놓기가 끝나도록 한다
            if (_dragging)
            {
                if (!grid.IsExpansionDrag) _dragging = false;   // 다른 이유로 끌기가 끝났다
                else
                {
                    var keyboard = Keyboard.current;
                    if (keyboard != null && keyboard.rKey.wasPressedThisFrame) grid.RotatePreview();
                    var mouse = Mouse.current;
                    if (mouse != null) MovePreview(grid, mouse.position.ReadValue());
                    if (mouse != null && mouse.leftButton.wasReleasedThisFrame) Release(grid);
                }
            }
        }

        // ───────────── 카드 만들기

        private void EnsureCard(GridManager grid)
        {
            if (_canvas == null)
            {
                var go = new GameObject("ExpansionCard", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                _canvas = go.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 12;
                go.AddComponent<GraphicRaycaster>();
                var instance = Instantiate(_cardPrefab, go.transform, false);
                instance.name = "ExpansionCardVisual";
                _card = (RectTransform)instance.transform;
                _card.anchorMin = _card.anchorMax = Vector2.zero;
                _card.pivot = new Vector2(0.5f, 0.5f);
                _group = instance.AddComponent<CanvasGroup>();
                foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

                // 카드 전체를 덮는 투명 판이 누르기·끌기를 받는다
                var areaGo = new GameObject("DragArea", typeof(RectTransform), typeof(Image));
                areaGo.transform.SetParent(_card, false);
                var area = (RectTransform)areaGo.transform;
                area.anchorMin = new Vector2(0.1f, 0.05f); area.anchorMax = new Vector2(0.9f, 0.95f);
                area.offsetMin = area.offsetMax = Vector2.zero;
                var image = areaGo.GetComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = true;
                areaGo.AddComponent<Handle>().Owner = this;
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

        /// <summary>옛 안내 줄은 입력도 받지 않게 숨긴다(카드가 대신 입력을 받는다).</summary>
        private void HideLegacy()
        {
            if (_legacy == null || _legacy.panel == null)
            {
                _legacy = _runner.GetType().GetField("_expansionCard", Private)?.GetValue(_runner) as Label;
                if (_legacy == null) return;
            }
            _legacy.pickingMode = PickingMode.Ignore;
            _legacy.style.opacity = 0f;
            _legacy.style.position = Position.Absolute;
            _legacy.style.width = 0f;
            _legacy.style.height = 0f;
            _legacy.style.marginTop = 0f;
        }

        // ───────────── 끌기

        private bool CanInteract()
        {
            if (_phase == null) _phase = FindFirstObjectByType<InGamePhasePresentation>(FindObjectsInactive.Include);
            return _phase == null || _phase.CanInteract;
        }

        private void Begin(Vector2 pointer)
        {
            var grid = _runner != null ? _runner.Manager : null;
            if (grid == null || !CanInteract() || _dragging) return;
            if (!grid.BeginExpansionDrag()) return;
            _dragging = true;
            MovePreview(grid, pointer);
        }

        private void MovePreview(GridManager grid, Vector2 pointer)
        {
            if (_bootstrap == null || _bootstrap.Config == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            var config = _bootstrap.Config;
            Vector3 origin = config.GridWorldOrigin;
            var plane = new Plane(Vector3.back, new Vector3(0f, 0f, origin.z));
            var ray = _camera.ScreenPointToRay(pointer);
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 hit = ray.GetPoint(distance);
            var cell = new Vector2Int(Mathf.RoundToInt((hit.x - origin.x) / config.CellWorldSize), Mathf.RoundToInt((hit.y - origin.y) / config.CellWorldSize));

            // 조각의 가운데가 커서 아래에 오도록 기준 칸을 옮긴다
            var cells = grid.PendingExpansionShape.GetCells(Vector2Int.zero, grid.PreviewRotation, false);
            float sx = 0f, sy = 0f;
            foreach (var c in cells) { sx += c.x; sy += c.y; }
            var center = new Vector2Int(Mathf.RoundToInt(sx / cells.Length), Mathf.RoundToInt(sy / cells.Length));
            grid.MovePreview(cell - center);
        }

        private void Release(GridManager grid)
        {
            _dragging = false;
            if (!grid.IsExpansionDrag) return;
            if (!grid.CommitPreview()) grid.CancelDrag();   // 놓을 수 없는 자리면 취소하고 카드는 그대로 둔다
        }

        private sealed class Handle : MonoBehaviour, IPointerDownHandler
        {
            public InGameExpansionCard Owner;
            public void OnPointerDown(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Left) return;
                Owner.Begin(eventData.position);
            }
        }
    }
}
