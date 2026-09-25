using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OZGL2.Grid.Prototype;
using OZGL2.Grid.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.Editor
{
    public static class GridPlacementQAPlayVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/Grid/Verify Placement QA (Empty Play Scene)")]
        public static async void Run()
        {
            Result = "Running";
            var previousBackground = Application.runInBackground;
            GameObject root = null;
            GridRunSession session = null;
            GridWorldPreparationView world = null;
            GridPrototypeRunner runner = null;
            try
            {
                Check(Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<OZGL2.InGame.InGamePrototypeBootstrap>() == null, "Use empty Play scene");
                Application.runInBackground = true;
                session = GridPlacementQAVerification.CreateSession();
                var grid = session.Grid;
                var supportCells = new List<Vector2Int>();
                for (int y = 0; y < 3; y++) for (int x = 0; x < 4; x++) supportCells.Add(new Vector2Int(x, y));
                GridPlacementQAVerification.PlaceBlock(grid, "support", new FootprintDefinition("support", "Support", supportCells), grid.Definition.InitialOrigin);
                var definition = new UnitDefinition("qa_unit", "QA Unit", GridPlacementQAVerification.SINGLE, null,
                    GridPlacementQAVerification.SINGLE, GridPlacementQAVerification.CORNER);
                foreach (var id in new[] { "target", "a", "b", "c" }) grid.AddUnit(id, definition);
                grid.BeginUnitDrag("target"); grid.MovePreview(new Vector2Int(4, 0)); Check(grid.CommitPreview(), "Place target");
                Check(grid.TryFuseUnits("a", "target") && grid.TryFuseUnits("b", "c") && grid.TryFuseUnits("c", "target"), "Three-star target");
                var expansion = GridPlacementQAVerification.AddExpansion(session, new Vector2Int(1, 0));

                root = new GameObject("PlacementQAPlayVerification");
                world = new GridWorldPreparationView(grid, new GridWorldMapping(Vector3.zero, Vector3.right, Vector3.up), 1,
                    (string id, int star) => null, root.transform);
                world.SetVisible(true);
                var ui = new GameObject("PlacementQAUI"); ui.transform.SetParent(root.transform); ui.SetActive(false);
                ui.AddComponent<UIDocument>().panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/Data/Grid/Prototype/GridPanelSettings.asset");
                runner = ui.AddComponent<GridPrototypeRunner>(); runner.Bind(session); ui.SetActive(true);
                await Frame(); await Frame();
                var panel = ui.GetComponent<UIDocument>().rootVisualElement;
                var tray = panel.Q<ScrollView>("storage-tray");
                Check(runner.Board.Element.Query<VisualElement>("occupied-cell").ToList().Count == 4, "UI shows four occupied cells");
                Check(CountColor(root, GridBoardView.OCCUPIED_COLOR) == 4, "World shows four occupied cells");
                var point = runner.Board.CellToPanel(new Vector2Int(1, 1));
                Move(panel, point); await Frame();
                Check(grid.HoveredExpansionId == expansion.InstanceId && runner.Board.Element.Query<VisualElement>("expansion-hover").ToList().Count == 2, "Hover highlights entire expansion");
                Down(runner.Board.Element, point); await Frame();
                Check(grid.IsExpansionDrag && grid.SelectedId == expansion.InstanceId, "Click empty floor selects expansion");
                Key(panel, KeyCode.R); await Frame();
                Check(grid.PreviewAnchor == new Vector2Int(0, 1) && grid.GetPreviewCells().Contains(new Vector2Int(1, 1)), "Rotation keeps grabbed cell under pointer without mouse movement");
                Up(panel, point); await Frame();
                Check(grid.FindExpansion(expansion.InstanceId).Rotation == 1 && grid.FloorCells.Count == 14, "Expansion pointer commit");
                Down(runner.Board.Element, point); Up(panel, tray.worldBound.center); await Frame();
                Check(!grid.HasSelection && grid.LastDropFailure == ePlacementFailure.CANNOT_STORE_EXPANSION && grid.FloorCells.Count == 14, "Tray drop restores expansion and releases pointer");
                Down(runner.Board.Element, point); Key(panel, KeyCode.Escape); await Frame();
                Check(!grid.HasSelection && grid.FindExpansion(expansion.InstanceId).Rotation == 1, "Esc keeps original expansion");

                point = runner.Board.CellToPanel(new Vector2Int(4, 2));
                Down(runner.Board.Element, point); await Frame();
                Check(grid.DragKind == eGridDragKind.UNIT && grid.SelectedId == "target", "All occupied cells select their unit first");
                Check(CountColor(root, GridBoardView.OCCUPIED_COLOR) == 0, "Dragged unit old footprint hidden");
                Key(panel, KeyCode.F); await Frame();
                Check(grid.PreviewIsMirrored && grid.GetPreviewCells().Contains(new Vector2Int(3, 0)), "F mirrors three-star shape");
                Key(panel, KeyCode.R); await Frame();
                Check(grid.GetPreviewCells().Contains(new Vector2Int(4, 2)), "R keeps grabbed cell");
                Check(grid.GetPreviewFailure() != ePlacementFailure.NONE, "Out-of-floor transformed ghost invalid");
                Key(panel, KeyCode.F); await Frame();
                Check(grid.GetPreviewCells().Contains(new Vector2Int(4, 2)), "F after rotation keeps grabbed cell");
                Key(panel, KeyCode.Escape); await Frame();
                Check(!grid.FindUnit("target").IsMirrored && grid.FindUnit("target").Rotation == 0, "Esc restores orientation");
                Down(runner.Board.Element, point); Key(panel, KeyCode.F); Up(panel, point); await Frame();
                Check(grid.FindUnit("target").IsMirrored, "F committed without additional mouse movement");
                Down(runner.Board.Element, point); Up(panel, tray.worldBound.center); await Frame();
                Check(!grid.FindUnit("target").IsPlaced && grid.FindUnit("target").IsMirrored, "Mirrored tray return");
                var card = panel.Q<VisualElement>("card-target");
                Down(card, card.worldBound.center); Move(panel, runner.Board.CellToPanel(new Vector2Int(4, 0)));
                Up(panel, runner.Board.CellToPanel(new Vector2Int(4, 0))); await Frame();
                Check(grid.FindUnit("target").IsPlaced && grid.FindUnit("target").IsMirrored, "Card redeploy retains reflection");
                Check(session.TryBeginBattle(session.RunId, session.NextRound), "Battle accepts mirrored unit");
                await Frame();
                Check(CountColor(root, GridBoardView.OCCUPIED_COLOR) == 0 && !grid.BeginExpansionDrag(expansion.InstanceId), "Combat hides occupancy and rejects expansion input");
                Check(session.Deployment.Units.Single().IsMirrored && grid.LastObserverError == null, "Combat snapshot and observers");
                Result = "PASS: expansion hover/drag/R/tray rejection/Esc, unit priority, F/R pointer anchor, mirror cancel/commit/tray/redeploy, UI/world occupied cells and combat hiding";
            }
            catch (Exception error) { Result = "FAIL: " + error; }
            finally
            {
                if (runner != null) runner.Unbind();
                world?.Dispose(); session?.Dispose();
                if (root != null) UnityEngine.Object.Destroy(root);
                Application.runInBackground = previousBackground;
            }
        }
        private static int CountColor(GameObject root, Color color) => root.GetComponentsInChildren<SpriteRenderer>().Count(r => r.enabled && r.color == color);
        private static Task Frame() => Task.Delay(100);
        private static void Check(bool condition, string message) => GridPlacementQAVerification.Check(condition, message);
        private static void Down(VisualElement target, Vector2 point)
        { using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = point, button = 0 })) { evt.target = target; target.SendEvent(evt); } }
        private static void Move(VisualElement target, Vector2 point)
        { using (var evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = point, button = 0 })) { evt.target = target; target.SendEvent(evt); } }
        private static void Up(VisualElement target, Vector2 point)
        { using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 })) { evt.target = target; target.SendEvent(evt); } }
        private static void Key(VisualElement target, KeyCode key)
        { using (var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key })) { evt.target = target; target.SendEvent(evt); } }
    }
}
