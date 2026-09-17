using System;
using System.Linq;
using System.Threading.Tasks;
using OZGL2.Grid.Prototype;
using OZGL2.Grid.UI;
using OZGL2.InGame.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.Editor
{
    public static class GridConnectivityPlayVerification
    {
        public static string Result { get; private set; } = "Not run";
        private static GridRunSession _session;
        private static GameObject _root;
        private static GridWorldBoardView _world;
        private static GridPrototypeRunner _runner;
        private static bool _hasBackgroundOverride;
        private static bool _previousBackground;
        [MenuItem("OZGL2/Grid/Verify Connectivity (Empty Play Scene)")]
        public static async void Run()
        {
            Result = "Running";
            try { await Verify(); Result = "PASS: pointer unit priority, blocked bridge return releases capture, red/green ghosts, valid move, UI/world boundary parity, connected SPUM theme."; }
            catch (Exception error) { Result = "FAIL: " + error; Cleanup(); }
        }
        private static async Task Verify()
        {
            Check(Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<OZGL2.InGame.InGamePrototypeBootstrap>() == null, "Use empty Play scene");
            Cleanup();
            _previousBackground = Application.runInBackground; _hasBackgroundOverride = true; Application.runInBackground = true;
            var expansion = new FootprintDefinition("expansion", "Expansion", new[] { Vector2Int.zero, Vector2Int.up });
            var single = new FootprintDefinition("single", "Single", new[] { Vector2Int.zero });
            _session = new GridRunSession("connectivity_play", new GridDefinition(new Vector2Int(4, 3), new Vector2Int(8, 5), expansion));
            var grid = _session.Grid;
            _session.TryAllowPreparation(_session.RunId, 1, false);
            var origin = grid.Definition.InitialOrigin;
            foreach (var id in new[] { "a", "b", "c" }) grid.AddBlock(id, id, single);
            for (int i = 0; i < 3; i++) GridConnectivityVerification.Place(grid, new[] { "a", "b", "c" }[i], origin + Vector2Int.right * i);
            grid.AddUnit("u", new UnitDefinition("u", "Unit", single));
            grid.BeginUnitDrag("u"); grid.MovePreview(origin + Vector2Int.right); Check(grid.CommitPreview(), "Place unit");
            var theme = AssetDatabase.LoadAssetAtPath<GridBoardThemeSO>(GridConnectivitySetup.THEME_PATH);
            Check(theme != null && Enum.GetValues(typeof(eGridCellSurface)).Cast<eGridCellSurface>().All(s => theme.GetSprite(s) != null), "Three SPUM surface sprites");
            _root = new GameObject("ConnectivityPlayVerification");
            _world = new GridWorldBoardView(grid, theme, new GridWorldMapping(Vector3.zero, Vector3.right, Vector3.up), 1, _root.transform);
            var ui = new GameObject("ConnectivityUI"); ui.transform.SetParent(_root.transform); ui.SetActive(false);
            ui.AddComponent<UIDocument>().panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/Data/Grid/Prototype/GridPanelSettings.asset");
            _runner = ui.AddComponent<GridPrototypeRunner>();
            var data = new SerializedObject(_runner); data.FindProperty("_boardTheme").objectReferenceValue = theme; data.ApplyModifiedPropertiesWithoutUndo();
            _runner.Bind(_session); ui.SetActive(true);
            await Frame(); await Frame();
            var panel = ui.GetComponent<UIDocument>().rootVisualElement;
            var tray = panel.Q<ScrollView>("storage-tray");
            // Runner가 제공하는 실제 보관함 이름을 검사해 테스트가 빈 영역에 드롭하지 않게 한다.
            if (tray == null) tray = panel.Query<ScrollView>().ToList().First();
            Down(_runner.Board.Element, _runner.Board.CellToPanel(origin + Vector2Int.right)); await Frame();
            Check(grid.DragKind == eGridDragKind.UNIT, "Unit selected ahead of supporting platform");
            Up(panel, tray.worldBound.center); await Frame();
            Check(!grid.FindUnit("u").IsPlaced && grid.FindBlock("b").IsPlaced, "Return unit only");
            Down(_runner.Board.Element, _runner.Board.CellToPanel(origin + Vector2Int.right)); await Frame();
            Check(grid.DragKind == eGridDragKind.BLOCK, "Empty platform selected");
            Up(panel, tray.worldBound.center); await Frame();
            Check(grid.FindBlock("b").IsPlaced && !grid.HasSelection && grid.LastDropFailure == ePlacementFailure.DISCONNECTED, "Rejected bridge return releases selection");
            Down(_runner.Board.Element, _runner.Board.CellToPanel(origin + Vector2Int.right * 2)); await Frame();
            Move(panel, _runner.Board.CellToPanel(origin + new Vector2Int(3, 2))); await Frame();
            Check(_runner.Board.GhostLayer.Q("ghost-cell").resolvedStyle.backgroundColor == GridBoardView.INVALID_COLOR, "Disconnected ghost red");
            Move(panel, _runner.Board.CellToPanel(origin + Vector2Int.one)); await Frame();
            Check(_runner.Board.GhostLayer.Q("ghost-cell").resolvedStyle.backgroundColor == GridBoardView.VALID_COLOR, "Connected ghost green");
            Up(panel, _runner.Board.CellToPanel(origin + Vector2Int.one)); await Frame();
            Check(grid.FindBlock("c").Anchor == origin + Vector2Int.one && !grid.HasSelection, "Valid pointer relocation");
            int expected = 0;
            foreach (var cell in grid.FloorCells) expected += GridSurfaceLayout.GetBorders(grid, cell, theme.BorderWidth).Count;
            Check(_root.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.StartsWith("Border_") && r.enabled) == expected, "World border count");
            Check(_runner.Board.Element.Query<VisualElement>("surface-border").ToList().Count == expected, "UI/world border parity");
            var floors = _runner.Board.Element.Q("floor");
            Check(floors.childCount == 40 && floors.Children().All(t => Mathf.Approximately(t.resolvedStyle.width, GridBoardView.CELL_SIZE)), "Forty gapless UI cells");
        }
        public static void Cleanup()
        {
            if (_runner != null) _runner.Unbind();
            _world?.Dispose(); _world = null;
            _session?.Dispose(); _session = null;
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _runner = null;
            if (_hasBackgroundOverride) Application.runInBackground = _previousBackground;
            _hasBackgroundOverride = false;
        }
        private static void Check(bool condition, string message) => GridConnectivityVerification.Check(condition, message);
        private static Task Frame() => Task.Delay(100);
        private static void Down(VisualElement target, Vector2 point)
        { using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = point, button = 0 })) { evt.target = target; target.SendEvent(evt); } }
        private static void Move(VisualElement target, Vector2 point)
        { using (var evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, mousePosition = point, button = 0 })) { evt.target = target; target.SendEvent(evt); } }
        private static void Up(VisualElement target, Vector2 point)
        { using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 })) { evt.target = target; target.SendEvent(evt); } }
    }
}
