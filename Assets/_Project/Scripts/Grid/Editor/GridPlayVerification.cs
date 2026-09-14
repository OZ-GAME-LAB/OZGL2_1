using System;
using System.Threading.Tasks;
using OZGL2.Grid.Prototype;
using OZGL2.Grid.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OZGL2.Grid.Editor
{
    public static class GridPlayVerification
    {
        public static string LastResult { get; private set; } = "Not run";
        [MenuItem("OZGL2/Grid/Verify Card Input (Play)")]
        public static async void Run()
        {
            LastResult = "Running";
            try { await Verify(); LastResult = "PASS: UI card drag, green/red ghost, rotation, invalid drop rollback, tray return, mandatory expansion, combat input lock."; }
            catch (Exception exception) { LastResult = "FAIL: " + exception; }
        }
        private static async Task Verify()
        {
            Require(Application.isPlaying, "Play mode required");
            GridPrototypeRunner runner = null;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.TryGetComponent<GridPrototypeRunner>(out var candidate)) runner = candidate;
            Require(runner != null, "Prototype runner in active scene");
            var root = runner.GetComponent<UIDocument>().rootVisualElement;
            var manager = runner.Manager;
            Require(manager.PlacedCount == 0 && manager.Phase == eGridPhase.PREPARATION, "Run test on fresh Play session");
            await Frame();
            Require(manager.StoredCount == 2 && root.Q<VisualElement>("storage-tray") != null, "Initial shared tray has 2 items");
            var catalog = AssetDatabase.LoadAssetAtPath<GridPrototypeCatalogSO>(GridPrototypeSetup.DATA_PATH + "/GridPrototypeCatalog.asset");
            var shape = catalog.CreateBlocks()[3];
            manager.AddBlock("corner", shape.Id, shape); manager.AddUnit("mage", catalog.CreateUnits()[3]);
            await Frame();
            var card = root.Q<VisualElement>("block-card-corner");
            Down(card, card.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            var ghost = runner.Board.GhostLayer.Q<VisualElement>("ghost-cell");
            Require(ghost != null && Near(ghost.resolvedStyle.backgroundColor, GridBoardView.VALID_COLOR), "Green block ghost");
            Up(root, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            card = root.Q<VisualElement>("card-unit_dummy_unit_single"); Down(card, card.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            Require(manager.GetPreviewFailure() == ePlacementFailure.NONE, "Single unit on larger different shape");
            Up(root, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            Down(runner.Board.Element, runner.Board.CellToPanel(new Vector2Int(4, 2)), true); await Frame();
            Require(manager.DragKind == eGridDragKind.UNIT, "Unit priority even with Shift");
            Up(root, root.Q<VisualElement>("storage-tray").worldBound.center); await Frame();
            Require(manager.PlacedCount == 0 && manager.FindBlock("corner").IsPlaced, "Unit returns alone to common tray");
            card = root.Q<VisualElement>("card-mage"); Down(card, card.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            Require(manager.GetPreviewCells().Length == 3, "Unit own footprint preview");
            Up(root, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            Down(runner.Board.Element, runner.Board.CellToPanel(new Vector2Int(5, 2))); await Frame();
            Require(manager.DragKind == eGridDragKind.UNIT && manager.SelectedId == "mage", "Non-anchor cell grabs whole unit");
            Key(root, KeyCode.R); await Frame(); Require(manager.PreviewRotation == 1, "Unit rotates");
            Move(root, runner.Board.CellToPanel(new Vector2Int(-1, 0))); await Frame();
            ghost = runner.Board.GhostLayer.Q<VisualElement>("ghost-cell");
            Require(Near(ghost.resolvedStyle.backgroundColor, GridBoardView.INVALID_COLOR), "Invalid unit ghost red");
            Up(root, runner.Board.CellToPanel(new Vector2Int(-1, 0))); await Frame();
            Require(manager.FindUnit("mage").Rotation == 0 && manager.FindUnit("mage").IsPlaced, "Invalid unit drop restores");
            Down(runner.Board.Element, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            Up(root, root.Q<VisualElement>("storage-tray").worldBound.center); await Frame();
            Down(runner.Board.Element, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            Require(manager.DragKind == eGridDragKind.BLOCK, "Empty block selects block");
            Key(root, KeyCode.R); await Frame(); Require(manager.PreviewRotation == 1, "Block rotates");
            Up(root, root.Q<VisualElement>("storage-tray").worldBound.center); await Frame();
            Require(!manager.FindBlock("corner").IsPlaced && manager.StoredCount == 4, "Block returns to same tray");
            card = root.Q<VisualElement>("block-card-block_single"); Down(card, card.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(2, 0))); Up(root, runner.Board.CellToPanel(new Vector2Int(2, 0))); await Frame();
            card = root.Q<VisualElement>("card-unit_dummy_unit_single"); Down(card, card.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(2, 0))); Up(root, runner.Board.CellToPanel(new Vector2Int(2, 0))); await Frame();
            Require(runner.Session.TryBeginBattle(runner.Session.RunId, runner.Session.NextRound), "Start with placed unit");
            card = root.Q<VisualElement>("block-card-corner"); Down(card, card.worldBound.center); await Frame();
            Require(!manager.HasSelection, "Combat blocks UI drag");
            Require(runner.PrototypeFlow.TryFinishBattle() && runner.PrototypeFlow.TryChooseExpansion(), "Simulated reward enters mandatory preparation"); await Frame();
            Require(!manager.CanBeginBattle && !manager.CanSkipPreparation, "Mandatory expansion gates both exits");
            var expansion = root.Q<VisualElement>("expansion-card"); Down(expansion, expansion.worldBound.center); await Frame();
            Key(root, KeyCode.Escape); await Frame();
            Require(!manager.HasSelection && manager.RequiresExpansionPlacement, "Escape preserves required reward");
            Down(expansion, expansion.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(1, 0))); await Frame();
            Require(manager.GetPreviewFailure() == ePlacementFailure.NONE, "Expansion green at shared edge");
            Up(root, runner.Board.CellToPanel(new Vector2Int(1, 0))); await Frame();
            Require(manager.FloorCells.Count == 14 && !manager.RequiresExpansionPlacement && manager.CanSkipPreparation, "Expansion placed before continuing");
            Require(manager.LastObserverError == null, "No hidden UI observer failures");
        }
        private static async Task Frame() { int target = Time.frameCount + 2; var limit = DateTime.UtcNow.AddSeconds(5); while (Time.frameCount < target) { if (DateTime.UtcNow > limit) throw new TimeoutException("Frames stalled"); await Task.Yield(); } }
        private static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
        private static void Down(VisualElement target, Vector2 point, bool shift = false)
        {
            using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = point, button = 0, modifiers = shift ? EventModifiers.Shift : EventModifiers.None }))
            { evt.target = target; target.SendEvent(evt); }
        }
        private static void Move(VisualElement target, Vector2 point)
        {
            using (var evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, mousePosition = point, button = 0 }))
            { evt.target = target; target.SendEvent(evt); }
        }
        private static void Up(VisualElement target, Vector2 point)
        {
            using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 }))
            { evt.target = target; target.SendEvent(evt); }
        }
        private static void Key(VisualElement target, KeyCode key)
        {
            using (var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key }))
            { evt.target = target; target.SendEvent(evt); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}

