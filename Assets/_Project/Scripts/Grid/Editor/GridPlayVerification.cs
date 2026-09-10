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
            var card = root.Q<VisualElement>("block-card-block_corner_three");
            Require(card != null && card.worldBound.width > 0, "Card layout ready");
            Down(card, card.worldBound.center); await Frame();
            Require(manager.SelectedId == "block_corner_three", "Pointer down begins card selection");
            Move(root, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            Require(manager.GetPreviewFailure() == ePlacementFailure.NONE && manager.PlacedCount == 0, "Green ghost without mutation");
            var ghost = runner.Board.GhostLayer.Q<VisualElement>("ghost-cell");
            Require(ghost != null && Near(ghost.resolvedStyle.backgroundColor, GridBoardView.VALID_COLOR), "Green translucent view");
            Up(root, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            Require(manager.FindBlock("block_corner_three").IsPlaced && root.Q<VisualElement>("block-card-block_corner_three") == null, "Card becomes placed unit");
            Require(!manager.CanBeginBattle, "Block without unit cannot start");
            card = root.Q<VisualElement>("card-unit_dummy_unit_single"); Down(card, card.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(4, 2))); await Frame();
            Require(manager.GetPreviewFailure() == ePlacementFailure.WRONG_BLOCK, "Basic rejects corner block");
            ghost = runner.Board.GhostLayer.Q<VisualElement>("ghost-cell");
            Require(Near(ghost.resolvedStyle.backgroundColor, GridBoardView.INVALID_COLOR), "Unit mismatch ghost red");
            Key(root, KeyCode.Escape); await Frame();
            card = root.Q<VisualElement>("card-unit_dummy_unit_corner_three"); Down(card, card.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(5, 2))); await Frame();
            Require(manager.GetPreviewFailure() == ePlacementFailure.NONE, "Mage accepts non-anchor corner cell");
            ghost = runner.Board.GhostLayer.Q<VisualElement>("ghost-cell");
            Require(Near(ghost.resolvedStyle.backgroundColor, GridBoardView.VALID_COLOR), "Matching unit ghost green");
            Up(root, runner.Board.CellToPanel(new Vector2Int(5, 2))); await Frame();
            Require(manager.GetUnitOnBlock("block_corner_three") != null && manager.CanBeginBattle, "Separate unit deployment");

            Down(runner.Board.Element, runner.Board.CellToPanel(new Vector2Int(4, 2)), true); await Frame();
            Require(root.Q<VisualElement>("card-unit_dummy_unit_corner_three") != null, "Occupant card temporarily shown");
            Key(root, KeyCode.R); await Frame();
            Require(manager.PreviewRotation == 1, "R rotates footprint");
            Move(root, runner.Board.CellToPanel(new Vector2Int(-1, 0))); await Frame();
            ghost = runner.Board.GhostLayer.Q<VisualElement>("ghost-cell");
            Require(ghost != null && Near(ghost.resolvedStyle.backgroundColor, GridBoardView.INVALID_COLOR), "Red translucent view");
            Up(root, runner.Board.CellToPanel(new Vector2Int(-1, 0))); await Frame();
            Require(manager.FindBlock("block_corner_three").Anchor == new Vector2Int(4, 2) && manager.FindBlock("block_corner_three").Rotation == 0,
                "Invalid UI drop restores original transform");
            Require(manager.GetUnitOnBlock("block_corner_three") != null, "Invalid block drop restores occupant");
            Down(runner.Board.Element, runner.Board.CellToPanel(new Vector2Int(4, 2)), true); await Frame();
            Up(root, root.Q<VisualElement>("block-tray").worldBound.center); await Frame();
            Require(manager.PlacedCount == 0 && root.Q<VisualElement>("block-card-block_corner_three") != null && manager.FloorCells.Count == 12, "Return to card tray retains floor");

            card = root.Q<VisualElement>("block-card-block_single"); Down(card, card.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(2, 0))); Up(root, runner.Board.CellToPanel(new Vector2Int(2, 0))); await Frame();
            card = root.Q<VisualElement>("card-unit_dummy_unit_single"); Down(card, card.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(2, 0))); Up(root, runner.Board.CellToPanel(new Vector2Int(2, 0))); await Frame();
            Down(runner.Board.Element, runner.Board.CellToPanel(new Vector2Int(2, 0))); await Frame();
            Require(manager.DragKind == eGridDragKind.UNIT, "Unit marker selects unit");
            Up(root, root.Q<VisualElement>("unit-tray").worldBound.center); await Frame();
            Require(manager.PlacedCount == 0 && manager.FindBlock("block_single").IsPlaced, "Unit tray drop preserves block");
            card = root.Q<VisualElement>("card-unit_dummy_unit_single"); Down(card, card.worldBound.center); await Frame();
            Move(root, runner.Board.CellToPanel(new Vector2Int(2, 0))); Up(root, runner.Board.CellToPanel(new Vector2Int(2, 0))); await Frame();
            Require(runner.Session.TryBeginBattle(runner.Session.RunId, runner.Session.NextRound), "Start with placed unit");
            card = root.Q<VisualElement>("block-card-block_line_two"); Down(card, card.worldBound.center); await Frame();
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
        private static async Task Frame() { await Task.Yield(); await Task.Yield(); }
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

