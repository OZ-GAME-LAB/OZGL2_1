using System;
using System.Collections.Generic;
using System.Linq;
using OZGL2.Grid.Prototype;
using UnityEditor;
using UnityEngine;

namespace OZGL2.Grid.Editor
{
    public static class GridVerification
    {
        public static string LastResult { get; private set; } = "Not run";
        [MenuItem("OZGL2/Grid/Verify Placement And Preparation")]
        public static void Run()
        {
            _sessions.Clear();
            try { Verify(); VerifyUnitSeparation(); VerifyUnchangedBlockDrop(); VerifyRunSession(); LastResult = "PASS: session rewards/idempotency, immutable deployment, world mapping, separate blocks/units, unchanged drop, rotations, occupancy, preparation gates and 14 expansions."; }
            catch (Exception exception) { LastResult = "FAIL: " + exception; }
        }
        private static GridPrototypeCatalogSO Catalog => AssetDatabase.LoadAssetAtPath<GridPrototypeCatalogSO>(GridPrototypeSetup.DATA_PATH + "/GridPrototypeCatalog.asset");
        private static void Verify()
        {
            var catalog = Catalog; Require(catalog != null, "Catalog assigned");
            var shapes = catalog.CreateBlocks(); Require(shapes.Count == 6, "Six shapes");
            Require(shapes.Select(shape => shape.Cells.Count).SequenceEqual(new[] { 1, 2, 3, 3, 4, 4 }), "Six expected cell counts");
            foreach (var shape in shapes)
            {
                var manager = CreatePreparedGrid(); manager.AddBlock("test", "content", shape);
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    Require(shape.GetCells(Vector2Int.zero, rotation).Distinct().Count() == shape.Cells.Count, "Rotation preserves cells");
                    bool canFit = false;
                    for (int y = 0; y < 3 && !canFit; y++)
                        for (int x = 2; x < 6 && !canFit; x++)
                        {
                            manager.BeginBlockDrag("test");
                            while (manager.PreviewRotation != rotation) manager.RotatePreview();
                            manager.MovePreview(new Vector2Int(x, y));
                            if (manager.CommitPreview()) canFit = true;
                            else manager.CancelDrag();
                        }
                    Require(canFit, "Each shape/orientation fits initial board: " + shape.Id);
                }
                Require(shape.GetCells(Vector2Int.zero, 0).SequenceEqual(shape.GetCells(Vector2Int.zero, 4)), "Four turns restore shape");
            }
            var grid = CreatePreparedGrid();
            Require(grid.FloorCells.Count == 12 && grid.Definition.KingAnchor == new Vector2(3.5f, -1), "Initial floor and independent king");
            Require(!grid.CanBeginBattle && !Start(grid), "King alone cannot start");
            grid.AddBlock("a", "same_type", shapes[3]); grid.AddBlock("b", "same_type", shapes[0]);
            int layoutChanges = 0; grid.LayoutChanged += () => layoutChanges++;
            grid.BeginBlockDrag("a"); grid.MovePreview(new Vector2Int(4, 2));
            Require(grid.GetPreviewFailure() == ePlacementFailure.NONE, "Screenshot corner placement valid");
            Require(grid.PlacedCount == 0 && grid.GetBlockAt(new Vector2Int(4, 2)) == null, "Ghost does not commit");
            Require(layoutChanges == 0, "Preview does not publish committed layout event");
            Require(grid.CommitPreview() && grid.PlacedCount == 0 && !grid.CanBeginBattle, "Empty block is not a deployed unit");
            Require(layoutChanges == 1, "Successful placement publishes one layout event");
            grid.BeginBlockDrag("b"); grid.MovePreview(new Vector2Int(4, 2));
            Require(grid.GetPreviewFailure() == ePlacementFailure.OCCUPIED && !grid.CommitPreview(), "Other unit overlap rejected"); grid.CancelDrag();
            grid.BeginBlockDrag("a"); grid.MovePreview(new Vector2Int(4, 2));
            Require(grid.GetPreviewFailure() == ePlacementFailure.NONE, "Self overlap accepted");
            grid.MovePreview(new Vector2Int(8, 4)); Require(grid.GetPreviewFailure() == ePlacementFailure.OUTSIDE_BOUNDS, "Boundary failure");
            grid.RotatePreview(); grid.CancelDrag();
            Require(grid.FindBlock("a").Anchor == new Vector2Int(4, 2) && grid.FindBlock("a").Rotation == 0, "Failed movement/rotation rollback");
            grid.BeginBlockDrag("b"); grid.MovePreview(new Vector2Int(0, 0));
            Require(grid.GetPreviewFailure() == ePlacementFailure.NO_FLOOR, "Empty max bounds is not floor"); grid.CancelDrag();
            grid.BeginBlockDrag("b"); grid.MovePreview(new Vector2Int(3, -1));
            Require(grid.GetPreviewFailure() == ePlacementFailure.OUTSIDE_BOUNDS, "Cannot place in king area"); grid.CancelDrag();
            grid.BeginBlockDrag("a"); Require(!grid.CanBeginBattle, "Drag prevents transition"); grid.DropToTray();
            Require(grid.FloorCells.Count == 12 && grid.PlacedCount == 0 && !grid.CanBeginBattle, "Tray frees occupancy only");
            Place(grid, "b", new Vector2Int(2, 0));
            grid.AddUnit("basic", new UnitDefinition("basic", "Basic", "single"));
            PlaceUnit(grid, "basic", new Vector2Int(2, 0));
            Require(!grid.CanSkipPreparation && !Start(grid, true), "First preparation cannot be skipped");
            Require(Start(grid), "One regular unit starts battle");
            Require(!grid.BeginBlockDrag("b") && !grid.BeginExpansionDrag() && !ChooseExpansion(grid), "Combat edit lock");
            Require(Finish(grid) && ChooseExpansion(grid), "Reward to required preparation");
            Require(!grid.CanBeginBattle && !Start(grid) && !Start(grid, true), "Mandatory expansion gates start and skip");
            Require(!ChooseExpansion(grid), "Cannot accumulate repeated reward requests");
            grid.BeginExpansionDrag(); grid.MovePreview(new Vector2Int(3, 0));
            Require(grid.GetPreviewFailure() == ePlacementFailure.FLOOR_EXISTS, "Expansion overlap rejected");
            grid.MovePreview(new Vector2Int(0, 3));
            Require(grid.GetPreviewFailure() == ePlacementFailure.DISCONNECTED, "Detached expansion rejected");
            grid.MovePreview(new Vector2Int(1, 3));
            Require(grid.GetPreviewFailure() == ePlacementFailure.DISCONNECTED, "Diagonal is not edge adjacency");
            grid.CancelDrag(); Require(grid.RequiresExpansionPlacement && !grid.CanBeginBattle, "Cancel cannot discard reward");
            var placements = new List<(Vector2Int Anchor, int Rotation)>();
            // Fill adjacent side columns (horizontal dominoes), then the two top rows (vertical dominoes).
            for (int y = 0; y < 3; y++) { placements.Add((new Vector2Int(0, y), 1)); placements.Add((new Vector2Int(6, y), 1)); }
            for (int x = 0; x < 8; x++) placements.Add((new Vector2Int(x, 3), 0));
            for (int i = 0; i < placements.Count; i++)
            {
                if (i > 0) { Require(Start(grid) && Finish(grid) && ChooseExpansion(grid), "Next reward flow"); }
                grid.BeginExpansionDrag(); if (placements[i].Rotation == 1) grid.RotatePreview(); grid.MovePreview(placements[i].Anchor);
                Require(grid.CommitPreview(), "Domino tiling step " + i);
                Require(grid.FloorCells.Count == 12 + (i + 1) * 2 && grid.CanBeginBattle, "Exactly two floor cells per reward");
            }
            Require(!grid.CanExpand && grid.GetExpansionFrontier().Count == 0 && grid.FloorCells.Count == 40, "Full board disables reward");
            Start(grid); Finish(grid);
            Require(!ChooseExpansion(grid) && grid.Phase == eGridPhase.REWARD, "Unavailable reward cannot force deadlock");
            Require(ChooseUnit(grid), "Other reward still proceeds");
            // A single isolated hole cannot fit a domino even though the area is below capacity.
            var floor = new HashSet<Vector2Int>(grid.FloorCells); floor.Remove(new Vector2Int(3, 2));
            bool found = false;
            for (int x = 0; x < 8; x++) for (int y = 0; y < 5; y++) for (int r = 0; r < 4; r++)
                found |= GridPlacementRules.ValidateExpansion(grid.Definition, floor, grid.Definition.Expansion.GetCells(new Vector2Int(x, y), r)) == ePlacementFailure.NONE;
            Require(!found, "Capacity alone does not imply placement availability");
            Require(catalog.CreateDefinition().InitialSize == new Vector2Int(4, 3), "SO source untouched");
        }
        private static void VerifyUnitSeparation()
        {
            var catalog = Catalog; var shapes = catalog.CreateBlocks();
            var grid = CreatePreparedGrid();
            grid.AddBlock("single", "single", shapes[0]); grid.AddBlock("corner", "corner_three", shapes[3]);
            grid.AddBlock("corner_other", "corner_three", shapes[3]);
            grid.AddUnit("basic", new UnitDefinition("basic", "Basic", "single"));
            grid.AddUnit("mage", new UnitDefinition("mage", "Mage", "corner_three"));
            grid.AddUnit("mage_two", new UnitDefinition("mage", "Mage", "corner_three"));
            Place(grid, "single", new Vector2Int(2, 0));
            grid.BeginBlockDrag("corner"); grid.RotatePreview(); grid.MovePreview(new Vector2Int(5, 2));
            Require(grid.CommitPreview(), "Rotated corner block");
            Require(!grid.CanBeginBattle, "Empty blocks cannot start battle");
            grid.BeginUnitDrag("mage"); grid.MovePreview(new Vector2Int(2, 0));
            Require(grid.GetPreviewFailure() == ePlacementFailure.WRONG_BLOCK && !grid.CommitPreview(), "Required shape mismatch");
            grid.MovePreview(new Vector2Int(3, 0)); Require(grid.GetPreviewFailure() == ePlacementFailure.NO_BLOCK, "Floor alone cannot host unit");
            grid.MovePreview(new Vector2Int(4, 2));
            Require(grid.GetPreviewFailure() == ePlacementFailure.NONE && grid.PreviewTargetBlock.InstanceId == "corner", "Any cell of rotated matching block accepts unit");
            grid.RotatePreview(); Require(grid.PreviewRotation == 0 && grid.FindBlock("corner").Rotation == 1, "Unit drag cannot rotate block");
            Require(grid.CommitPreview() && grid.FindUnit("mage").BlockId == "corner" && grid.PlacedCount == 1, "Unit attached separately");
            grid.BeginUnitDrag("mage_two"); grid.MovePreview(new Vector2Int(5, 1));
            Require(grid.GetPreviewFailure() == ePlacementFailure.BLOCK_OCCUPIED && !grid.CommitPreview(), "One unit for whole block"); grid.CancelDrag();
            grid.BeginUnitDrag("mage"); grid.MovePreview(new Vector2Int(4, 2)); Require(grid.GetPreviewFailure() == ePlacementFailure.NONE, "Unit self target valid");
            grid.MovePreview(new Vector2Int(-1, -1)); grid.CancelDrag(); Require(grid.FindUnit("mage").BlockId == "corner", "Unit cancel restores link");
            int updates = 0; grid.LayoutChanged += () => updates++;
            grid.BeginBlockDrag("corner"); grid.RotatePreview(); grid.MovePreview(new Vector2Int(-1, -1));
            Require(grid.IsUnitTemporarilyReturned(grid.FindUnit("mage")) && grid.FindUnit("mage").BlockId == "corner" && updates == 0, "Temporary card only, no committed mutation");
            grid.CancelDrag(); Require(grid.FindUnit("mage").BlockId == "corner" && grid.FindBlock("corner").Rotation == 1 && updates == 0, "Block cancel restores block and unit");
            grid.BeginBlockDrag("corner"); grid.MovePreview(new Vector2Int(4, 2));
            Require(grid.CommitPreview() && !grid.FindUnit("mage").IsPlaced && updates == 1, "Block move returns unit atomically");
            PlaceUnit(grid, "mage", new Vector2Int(4, 2));
            grid.BeginBlockDrag("corner_other"); grid.RotatePreview(); grid.MovePreview(new Vector2Int(3, 1));
            Require(grid.CommitPreview(), "Second matching block");
            PlaceUnit(grid, "mage", new Vector2Int(2, 1));
            Require(grid.FindUnit("mage").BlockId == "corner_other" && grid.GetUnitOnBlock("corner") == null, "Move unit independently to same-shape block");
            grid.BeginBlockDrag("corner_other"); grid.DropToTray();
            Require(!grid.FindBlock("corner_other").IsPlaced && !grid.FindUnit("mage").IsPlaced && grid.FloorCells.Count == 12, "Removing block returns occupant but preserves floor");
            PlaceUnit(grid, "basic", new Vector2Int(2, 0));
            grid.BeginUnitDrag("basic"); grid.DropToTray(); Require(grid.FindBlock("single").IsPlaced && !grid.CanBeginBattle, "Returning unit leaves block");
            PlaceUnit(grid, "basic", new Vector2Int(2, 0)); Start(grid);
            Require(!grid.BeginBlockDrag("single") && !grid.BeginUnitDrag("basic"), "Both layers locked in battle");
            Require(catalog.CreateUnits().Count == 6 && catalog.CreateUnits()[3].RequiredBlockId == "corner_three", "SO requirement mapping");
        }
        private static void VerifyUnchangedBlockDrop()
        {
            var catalog = Catalog;
            var shape = catalog.CreateBlocks()[1];
            var grid = CreatePreparedGrid();
            var anchor = new Vector2Int(3, 1);
            grid.AddBlock("block", shape.Id, shape);
            grid.AddUnit("unit", new UnitDefinition("unit", "Unit", shape.Id));
            Place(grid, "block", anchor);
            PlaceUnit(grid, "unit", anchor);
            int layouts = 0, changes = 0;
            grid.LayoutChanged += () => layouts++;
            grid.Changed += () => changes++;

            Require(grid.BeginBlockDrag("block"), "Begin unchanged drop");
            changes = 0;
            Require(grid.CommitPreview(), "Unchanged drop succeeds");
            Require(grid.FindUnit("unit").BlockId == "block" && grid.CanBeginBattle && !grid.HasSelection,
                "Unchanged drop preserves occupant and restores battle gate");
            Require(layouts == 0 && changes == 1, "Unchanged drop only refreshes presentation");

            grid.BeginBlockDrag("block");
            for (int i = 0; i < 4; i++) grid.RotatePreview();
            Require(grid.CommitPreview() && grid.FindUnit("unit").IsPlaced && layouts == 0,
                "Four rotations restore unchanged placement");

            grid.BeginBlockDrag("block"); grid.MovePreview(new Vector2Int(-1, -1));
            Require(!grid.CommitPreview(), "Invalid drop rejected");
            grid.CancelDrag();
            Require(grid.FindUnit("unit").IsPlaced && grid.FindBlock("block").Anchor == anchor && layouts == 0,
                "Invalid drop and cancellation preserve placement");

            grid.BeginBlockDrag("block"); grid.RotatePreview();
            Require(grid.CommitPreview() && !grid.FindUnit("unit").IsPlaced && layouts == 1,
                "Actual rotation returns occupant and publishes layout change");
            PlaceUnit(grid, "unit", anchor);
            layouts = 0;
            grid.BeginBlockDrag("block"); grid.MovePreview(new Vector2Int(4, 1));
            Require(grid.CommitPreview() && !grid.FindUnit("unit").IsPlaced && layouts == 1,
                "Actual movement returns occupant and publishes layout change");
        }
        private static void VerifyRunSession()
        {
            var catalog = Catalog; var shape = catalog.CreateBlocks()[0]; var unit = catalog.CreateUnits()[0];
            using (var session = new GridRunSession("external_run", catalog.CreateDefinition()))
            {
                var grid = session.Grid;
                grid.AddBlock("b", shape.Id, shape); grid.AddUnit("u", unit);
                Require(!grid.BeginBlockDrag("b") && !session.CanBeginBattle, "Initial preparation waits for permission");
                Require(!session.TryAllowPreparation("old_run", 1, true), "Foreign run permission rejected");
                Require(session.TryAllowPreparation(session.RunId, 1, true), "External preparation permission");
                Place(grid, "b", new Vector2Int(2, 0)); PlaceUnit(grid, "u", new Vector2Int(2, 0));
                IGridPreparation api = session;
                Require(!api.TryBeginBattle(session.RunId, 1, true) && api.TryBeginBattle(session.RunId, 1), "First skip prohibited via unified API");
                var snapshot = session.Deployment;
                Require(snapshot != null && snapshot.Units.Count == 1, "Unified API captures deployment");
                Require(!api.TryFinishBattle(session.RunId, 2, "request_a"), "Wrong round rejected");
                Require(api.TryFinishBattle(session.RunId, 1, "request_a"), "External request ID registered");
                Require(!api.TryFinishBattle(session.RunId, 1, "request_b"), "Duplicate completion rejected");
                Require(!api.TryAllowPreparation(session.RunId, 2, true), "Unselected reward blocks preparation");
                Require(!session.TryChooseUnit("old_run", "request_a", unit, shape) &&
                    !session.TryChooseUnit(session.RunId, "stale", unit, shape), "Foreign and stale rewards rejected");
                Require(!session.TryChooseUnit(session.RunId, "request_a", unit, catalog.CreateBlocks()[1]), "Wrong shape rejected without consuming reward");
                int layouts = 0;
                grid.LayoutChanged += () => layouts++;
                Require(session.TryChooseUnit(session.RunId, "request_a", unit, shape) && layouts == 1 && grid.Units.Count == 2 && grid.Blocks.Count == 2, "Atomic one-unit and block grant");
                Require(grid.Phase == eGridPhase.REWARD && !api.CanBeginBattle && !grid.BeginUnitDrag("u"), "Reward alone never unlocks preparation");
                Require(!session.TryChooseExpansion(session.RunId, "request_a"), "Second reward choice rejected");
                Require(!api.TryAllowPreparation(session.RunId, 1, true) && api.TryAllowPreparation(session.RunId, 2, false), "Only next-round permission accepted");
                Require(!api.CanSkipPreparation && api.CanBeginBattle, "External skip policy honored");
                grid.BeginUnitDrag("u"); grid.DropToTray();
                Require(snapshot.Units.Count == 1 && snapshot.FloorCells.Count == 12 && !api.CanBeginBattle, "Snapshot immutable after rearrangement");
                PlaceUnit(grid, "u", new Vector2Int(2, 0));
                Require(api.TryBeginBattle(session.RunId, 2), "Second battle");
                Require(!api.TryFinishBattle(session.RunId, 2, "request_a"), "Consumed request ID cannot be reused");
                Require(api.TryFinishBattle(session.RunId, 2, "request_b") && session.TryChooseExpansion(session.RunId, "request_b"), "Expansion grant");
                Require(grid.RequiresExpansionPlacement && !grid.BeginExpansionDrag(), "Expansion placement waits for permission");
                Require(api.TryAllowPreparation(session.RunId, 3, true) && !api.CanBeginBattle && !api.CanSkipPreparation, "Mandatory expansion gates exits");
                grid.BeginExpansionDrag(); grid.MovePreview(new Vector2Int(1, 0)); Require(grid.CommitPreview(), "Expansion placement");
                Require(api.CanSkipPreparation && grid.FloorCells.Count == 14, "Permission plus completed expansion opens exits");
                grid.BeginBlockDrag("b"); session.Dispose();
                Require(!grid.HasSelection && grid.Phase == eGridPhase.ENDED && !grid.BeginBlockDrag("b") && !grid.BeginUnitDrag("u") && !grid.BeginExpansionDrag(), "Shutdown cancels and locks all input");
                Require(!grid.CommitPreview() && !grid.DropToTray() && !api.TryBeginBattle(session.RunId, 3) &&
                    !api.TryAllowPreparation(session.RunId, 3, true), "Shutdown blocks mutations and progression");
                bool rejected = false;
                try { grid.AddUnit("late", unit); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Late unit grant blocked");
                rejected = false;
                try { grid.AddBlock("late", shape.Id, shape); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Late block grant blocked");
            }
            using (var fresh = new GridRunSession("new_run", catalog.CreateDefinition()))
            {
                Require(fresh.Grid.FloorCells.Count == 12 && fresh.Grid.Units.Count == 0, "New run independent");
                Require(!fresh.TryAllowPreparation("external_run", 1, true), "Previous run callback cannot unlock new run");
            }
            Require(!typeof(IGridPreparation).IsAssignableFrom(typeof(GridManager)), "Grid no longer exposes progression interface");
            Require(typeof(GridManager).GetMethod("TryBeginBattle") == null && typeof(GridManager).GetMethod("TryFinishBattle") == null, "No public direct progression methods");
            var mapping = new GridWorldMapping(new Vector3(10, 0, 20), Vector3.right * 2, Vector3.forward * 3);
            Require(mapping.GetWorldPosition(new Vector2(2, 1)) == new Vector3(14, 0, 23), "XZ coordinate conversion");
        }
        private static readonly Dictionary<GridManager, GridRunSession> _sessions = new Dictionary<GridManager, GridRunSession>();
        private static GridManager CreatePreparedGrid()
        {
            var session = new GridRunSession(Guid.NewGuid().ToString("N"), Catalog.CreateDefinition());
            _sessions.Add(session.Grid, session);
            Require(session.TryAllowPreparation(session.RunId, 1, false), "Prepare test run");
            return session.Grid;
        }
        private static bool Start(GridManager grid, bool skip = false)
        { var session = _sessions[grid]; return session.TryBeginBattle(session.RunId, session.NextRound, skip); }
        private static bool Finish(GridManager grid)
        { var session = _sessions[grid]; return session.TryFinishBattle(session.RunId, session.NextRound, Guid.NewGuid().ToString("N")); }
        private static bool ChooseExpansion(GridManager grid)
        {
            var session = _sessions[grid];
            return session.TryChooseExpansion(session.RunId, session.PendingRewardId) && session.TryAllowPreparation(session.RunId, session.NextRound, true);
        }
        private static bool ChooseUnit(GridManager grid)
        {
            var session = _sessions[grid];
            return session.TryChooseUnit(session.RunId, session.PendingRewardId, Catalog.CreateUnits()[0], Catalog.CreateBlocks()[0]) &&
                session.TryAllowPreparation(session.RunId, session.NextRound, true);
        }
        private static void PlaceUnit(GridManager grid, string id, Vector2Int cell)
        { Require(grid.BeginUnitDrag(id), "Begin unit placement"); grid.MovePreview(cell); Require(grid.CommitPreview(), "Commit unit placement"); }
        private static void Place(GridManager grid, string id, Vector2Int anchor)
        { Require(grid.BeginBlockDrag(id), "Begin placement"); grid.MovePreview(anchor); Require(grid.CommitPreview(), "Commit placement"); }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}

