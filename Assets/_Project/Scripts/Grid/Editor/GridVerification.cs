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
            try
            {
                VerifyCells(); VerifyStorage(); VerifyRunSession(); VerifyBattleSnapshotCommit(); VerifyExpansion();
                LastResult = "PASS: cross-block unit cells, shared capacity, atomic discard/reward/return, stale/cancel/reentrancy, immutable deployment, preparation and 14 expansions.";
            }
            catch (Exception exception) { LastResult = "FAIL: " + exception; }
            finally { foreach (var session in _sessions.Values) session.Dispose(); }
        }
        private static GridPrototypeCatalogSO Catalog => AssetDatabase.LoadAssetAtPath<GridPrototypeCatalogSO>(GridPrototypeSetup.DATA_PATH + "/GridPrototypeCatalog.asset");
        private static FootprintDefinition Single => Catalog.CreateBlocks()[0];
        private static UnitDefinition Basic => Catalog.CreateUnits()[0];
        private static FootprintDefinition Horizontal => new FootprintDefinition("test_horizontal", "Horizontal", new[] { Vector2Int.zero, Vector2Int.right });
        private static void VerifyCells()
        {
            Require(Catalog.CreateInitialUnit().Footprint.Cells.Count == 1 && Catalog.CreateDefinition().StorageCapacity == 10, "Initial SO and limit");
            foreach (var shape in Catalog.CreateBlocks())
            {
                Require(shape.GetCells(Vector2Int.zero, 0).SequenceEqual(shape.GetCells(Vector2Int.zero, 4)), "Rotation cycle");
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    var test = CreatePreparedGrid(); test.AddBlock("shape", shape.Id, shape);
                    bool placed = false;
                    for (int y = 0; y < 3 && !placed; y++) for (int x = 2; x < 6 && !placed; x++)
                    {
                        test.BeginBlockDrag("shape"); for (int i = 0; i < rotation; i++) test.RotatePreview();
                        test.MovePreview(new Vector2Int(x, y)); placed = test.CommitPreview(); if (!placed) test.CancelDrag();
                    }
                    Require(placed, "Every shape/orientation fits initial floor");
                    test.AddUnit("unit", new UnitDefinition("test_unit", "Unit", shape));
                    var block = test.FindBlock("shape"); test.BeginUnitDrag("unit");
                    for (int i = 0; i < rotation; i++) test.RotatePreview();
                    test.MovePreview(block.Anchor); Require(test.CommitPreview(), "Unit rotation follows own cells");
                }
            }
            var grid = CreatePreparedGrid();
            grid.AddBlock("wide", Horizontal.Id, Horizontal); Place(grid, "wide", new Vector2Int(2, 0));
            grid.AddUnit("u1", Basic); grid.AddUnit("u2", Basic);
            PlaceUnit(grid, "u1", new Vector2Int(2, 0)); PlaceUnit(grid, "u2", new Vector2Int(3, 0));
            Require(grid.GetUnitsOnBlock("wide").Count == 2 && grid.StoredCount == 0, "Two singles on one block");
            grid.BeginUnitDrag("u1"); grid.MovePreview(new Vector2Int(3, 0));
            Require(grid.GetPreviewFailure() == ePlacementFailure.OCCUPIED && !grid.CommitPreview(), "Other unit overlap denied");
            grid.CancelDrag(); Require(grid.GetUnitAt(new Vector2Int(2, 0)).InstanceId == "u1", "Cancel preserves cells");
            grid.BeginBlockDrag("wide"); for (int i = 0; i < 4; i++) grid.RotatePreview();
            Require(grid.CommitPreview() && grid.PlacedCount == 2, "No-op preserves multiple occupants");
            grid.BeginBlockDrag("wide"); grid.MovePreview(new Vector2Int(-1, 0));
            Require(!grid.CommitPreview(), "Invalid block move"); grid.CancelDrag(); Require(grid.PlacedCount == 2, "Invalid drop preserves occupants");
            grid.BeginBlockDrag("wide"); grid.MovePreview(new Vector2Int(2, 1));
            Require(grid.CommitPreview() && grid.PlacedCount == 0 && grid.StoredCount == 2, "Actual move returns all occupants");
            var bridge = CreatePreparedGrid();
            bridge.AddBlock("a", Single.Id, Single); bridge.AddBlock("b", Single.Id, Single);
            Place(bridge, "a", new Vector2Int(2, 0)); Place(bridge, "b", new Vector2Int(3, 0));
            bridge.AddUnit("large", new UnitDefinition("large", "Large", Horizontal)); PlaceUnit(bridge, "large", new Vector2Int(2, 0));
            Require(bridge.GetUnitAt(new Vector2Int(3, 0)).InstanceId == "large", "Unit spans separate blocks");
            var snap = new GridDeploymentSnapshot(bridge);
            Require(snap.Units[0].Cells.Count == 2 && snap.Units[0].Anchor == new Vector2Int(2, 0), "Unit-based deployment");
            bridge.BeginUnitDrag("large"); bridge.RotatePreview(); Require(bridge.GetPreviewFailure() == ePlacementFailure.OUTSIDE_BOUNDS, "Missing rotated support"); bridge.CancelDrag();
            bridge.BeginBlockDrag("a"); Require(bridge.DropToTray() && !bridge.FindUnit("large").IsPlaced && bridge.FindBlock("b").IsPlaced, "Return spanning unit whole");
            Require(snap.Units[0].Cells.Count == 2, "Snapshot retained after return");
        }
        private static void Fill(GridManager grid, int count)
        { for (int i = 0; i < count; i++) grid.AddBlock("filler_" + i, Single.Id, Single); }
        private static void VerifyStorage()
        {
            var grid = CreatePreparedGrid(); var session = _sessions[grid];
            grid.AddBlock("wide", Horizontal.Id, Horizontal); Place(grid, "wide", new Vector2Int(2, 0));
            grid.AddUnit("a", Basic); grid.AddUnit("b", Basic); PlaceUnit(grid, "a", new Vector2Int(2, 0)); PlaceUnit(grid, "b", new Vector2Int(3, 0));
            Fill(grid, 10);
            bool denied = false; try { grid.AddUnit("over", Basic); } catch (InvalidOperationException) { denied = true; }
            Require(denied && grid.StoredCount == 10, "Direct grant cannot bypass capacity");
            grid.BeginBlockDrag("wide"); grid.MovePreview(new Vector2Int(2, 1));
            Require(!grid.CommitPreview() && grid.PendingStorage.RequiredDiscardCount == 2 && grid.FindBlock("wide").Anchor == new Vector2Int(2, 0), "Full storage delays block move and keeps source");
            Require(session.TryCancelStorage(session.RunId, grid.PendingStorage.RequestId) && grid.PlacedCount == 2, "Movement cancellation keeps occupants");
            grid.BeginBlockDrag("wide"); Require(!grid.DropToTray(), "Return waits for space");
            var request = grid.PendingStorage;
            Require(request.RequiredDiscardCount == 3 && request.Incoming.Count == 3 && grid.PlacedCount == 2 && grid.FindBlock("wide").IsPlaced, "Whole original preserved");
            Require(!grid.BeginUnitDrag("a") && !session.CanBeginBattle, "Pending blocks inputs");
            Require(!session.TryConfirmStorage("stale", request.RequestId, request.DiscardCandidates.Take(3).ToArray()), "Foreign run denied");
            Require(!session.TryConfirmStorage(session.RunId, request.RequestId, new[] { request.DiscardCandidates[0], request.DiscardCandidates[0], request.DiscardCandidates[1] }), "Duplicate selections denied");
            Require(!session.TryConfirmStorage(session.RunId, request.RequestId, new[] { request.Incoming[0], request.DiscardCandidates[0], request.DiscardCandidates[1] }), "Incoming cannot be discarded");
            Require(session.TryCancelStorage(session.RunId, request.RequestId) && grid.PlacedCount == 2 && grid.StoredCount == 10, "Cancel restores nothing because nothing changed");
            grid.BeginBlockDrag("wide"); grid.DropToTray(); var next = grid.PendingStorage;
            Require(!session.TryConfirmStorage(session.RunId, request.RequestId, next.DiscardCandidates.Take(3).ToArray()), "Old confirmation rejected");
            Require(session.TryConfirmStorage(session.RunId, next.RequestId, next.DiscardCandidates.Take(3).ToArray()), "Atomic return and discard");
            Require(grid.StoredCount == 10 && grid.PlacedCount == 0 && !grid.FindBlock("wide").IsPlaced, "Result within limit");
            Require(!session.TryConfirmStorage(session.RunId, next.RequestId, next.DiscardCandidates.Take(3).ToArray()), "Duplicate confirm rejected");
            var rewards = CreatePreparedGrid(); var owner = _sessions[rewards];
            rewards.AddBlock("board", Single.Id, Single); rewards.AddUnit("hero", Basic); Place(rewards, "board", new Vector2Int(2, 0)); PlaceUnit(rewards, "hero", new Vector2Int(2, 0));
            Fill(rewards, 9); Require(Start(rewards) && Finish(rewards), "Reward setup");
            string rewardId = owner.PendingRewardId;
            Require(!owner.TryChooseUnit(owner.RunId, rewardId, Basic, Single) && rewards.PendingStorage.RequiredDiscardCount == 1, "9 plus 2 waits");
            Require(owner.PendingRewardId == rewardId && rewards.Units.Count == 1 && rewards.StoredCount == 9, "No partial reward");
            Require(!owner.TryAllowPreparation(owner.RunId, owner.NextRound, true) && !owner.TryChooseExpansion(owner.RunId, rewardId), "Pending reward cannot be bypassed");
            var pending = rewards.PendingStorage;
            Require(owner.TryCancelStorage(owner.RunId, pending.RequestId) && owner.PendingRewardId == rewardId, "Cancelled reward remains available");
            owner.TryChooseUnit(owner.RunId, rewardId, Basic, Single); pending = rewards.PendingStorage;
            bool reentered = false;
            rewards.Changed += () => { if (owner.PendingRewardId == null) reentered = owner.TryAllowPreparation(owner.RunId, owner.NextRound, true); };
            Require(owner.TryConfirmStorage(owner.RunId, pending.RequestId, pending.DiscardCandidates.Take(1).ToArray()), "Reward resolve");
            Require(!reentered && owner.PendingRewardId == null && rewards.StoredCount == 10 && rewards.Units.Count == 2, "Completion before observer, reentrancy blocked");
            Require(owner.TryAllowPreparation(owner.RunId, owner.NextRound, true), "Explicit preparation after reward");
            rewards.BeginUnitDrag("hero"); rewards.DropToTray();
            pending = rewards.PendingStorage; Require(pending != null, "Unit return waits at full capacity");
            owner.Dispose(); Require(rewards.PendingStorage == null && !owner.TryConfirmStorage(owner.RunId, pending.RequestId, pending.DiscardCandidates.Take(1).ToArray()), "Shutdown cancels pending transaction");
        }
        private static void VerifyExpansion()
        {
            var grid = CreatePreparedGrid(); grid.AddBlock("b", Single.Id, Single); grid.AddUnit("u", Basic);
            Place(grid, "b", new Vector2Int(2, 0)); PlaceUnit(grid, "u", new Vector2Int(2, 0));
            for (int i = 0; i < 14; i++)
            {
                Require(Start(grid) && Finish(grid) && ChooseExpansion(grid), "Expansion round");
                Require(!grid.CanBeginBattle && !grid.CanSkipPreparation, "Required expansion gating");
                bool placed = false;
                for (int y = 0; y < 5 && !placed; y++) for (int x = 0; x < 8 && !placed; x++) for (int r = 0; r < 4 && !placed; r++)
                {
                    grid.BeginExpansionDrag(); for (int t = 0; t < r; t++) grid.RotatePreview(); grid.MovePreview(new Vector2Int(x, y));
                    placed = grid.CommitPreview(); if (!placed) grid.CancelDrag();
                }
                Require(placed && grid.StoredCount == 0, "Expansion outside storage");
            }
            Require(grid.FloorCells.Count == 40 && !grid.CanExpand, "Maximum grid");
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
        private static void VerifyBattleSnapshotCommit()
        {
            using (var session = new GridRunSession("snapshot_commit", Catalog.CreateDefinition()))
            {
                var grid = session.Grid;
                var shape = Catalog.CreateInitialBlock();
                grid.AddBlock("b", shape.Id, shape);
                grid.AddUnit("u", Catalog.CreateInitialUnit());
                Require(session.TryAllowPreparation(session.RunId, 1, false), "Snapshot preparation");
                Place(grid, "b", new Vector2Int(2, 0));
                int attempts = 0;
                bool rejectedWithoutMutation = true;
                GridDeploymentSnapshot observed = null;
                grid.Changed += () =>
                {
                    if (grid.CanBeginBattle)
                    {
                        attempts++;
                        var previous = session.Deployment;
                        bool accepted = session.TryBeginBattle(session.RunId, session.NextRound);
                        rejectedWithoutMutation &= !accepted && ReferenceEquals(previous, session.Deployment) &&
                            grid.Phase == eGridPhase.PREPARATION;
                    }
                    if (grid.Phase == eGridPhase.BATTLE) observed = session.Deployment;
                };
                PlaceUnit(grid, "u", new Vector2Int(2, 0));
                Require(attempts > 0 && rejectedWithoutMutation && session.Deployment == null,
                    "Rejected observer start preserves null snapshot");
                Require(session.TryBeginBattle(session.RunId, 1), "Snapshot first battle");
                var first = session.Deployment;
                Require(first != null && ReferenceEquals(observed, first) && first.Units[0].Anchor == new Vector2Int(2, 0),
                    "Start observer sees committed first snapshot");
                Require(session.TryFinishBattle(session.RunId, 1, "snapshot_reward") &&
                    session.TryChooseUnit(session.RunId, "snapshot_reward", Catalog.CreateInitialUnit(), shape) &&
                    session.TryAllowPreparation(session.RunId, 2, true), "Snapshot next preparation");
                Place(grid, "b", new Vector2Int(3, 0));
                int previousAttempts = attempts;
                PlaceUnit(grid, "u", new Vector2Int(3, 0));
                Require(attempts > previousAttempts && rejectedWithoutMutation && ReferenceEquals(session.Deployment, first),
                    "Rejected observer start preserves previous round snapshot");
                Require(session.TryBeginBattle(session.RunId, 2, true), "Snapshot next battle with skip");
                Require(!ReferenceEquals(session.Deployment, first) && ReferenceEquals(observed, session.Deployment) &&
                    session.Deployment.Units[0].Anchor == new Vector2Int(3, 0) && first.Units[0].Anchor == new Vector2Int(2, 0),
                    "Start observer sees updated snapshot while previous snapshot stays immutable");
            }
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

