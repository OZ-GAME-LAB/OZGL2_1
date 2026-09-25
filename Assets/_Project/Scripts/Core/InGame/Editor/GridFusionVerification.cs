using System;
using System.Linq;
using OZGL2.Grid;
using OZGL2.Grid.UI;
using UnityEditor;
using UnityEngine;

namespace OZGL2.InGame.Editor
{
    public static class GridFusionVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/InGame/Verify Grid Fusion Rules")]
        public static void Run()
        {
            try { Verify(); Result = "PASS: fusion atomicity, star footprints, invalid placement battle gate and recovery, tray capacity, snapshot, reentrancy, floor preservation."; }
            catch (Exception exception) { Result = "FAIL: " + exception; }
        }
        private static void Verify()
        {
            var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
            Require(config.Catalog.CreateUnits().All(u => u.Footprint.Cells.Count == 1), "Single-cell runtime roster");
            using (var session = new GridRunSession("fusion_check", config.Catalog.CreateDefinition(), GridFusionPolicy.CanFuse))
            {
                var grid = session.Grid;
                var basic = config.Catalog.CreateInitialUnit();
                var single = config.Catalog.CreateInitialBlock();
                grid.AddBlock("floor", "floor", CreateTestFloor(grid.Definition.InitialSize));
                for (int i = 0; i < 4; i++) grid.AddUnit("unit" + i, basic);
                Require(!grid.TryFuseUnits("unit0", "unit1"), "Waiting rejects fusion");
                session.TryAllowPreparation(session.RunId, 1, false);
                var origin = grid.Definition.InitialOrigin;
                grid.BeginBlockDrag("floor"); grid.MovePreview(origin); Require(grid.CommitPreview(), "Place floor");
                Place(grid, "unit0", origin); Place(grid, "unit1", origin + Vector2Int.right);
                int layoutEvents = 0;
                Action observer = () => { layoutEvents++; Require(!grid.TryFuseUnits("unit2", "unit3"), "No observer reentry"); };
                grid.LayoutChanged += observer;
                grid.BeginUnitDrag("unit0"); grid.MovePreview(origin + Vector2Int.right);
                Require(grid.CanFusePreview && grid.CommitPreview(), "Drop-to-fuse on board");
                grid.LayoutChanged -= observer;
                Require(layoutEvents == 1 && grid.LastObserverError == null, "Exactly one committed notification");
                Require(grid.FindUnit("unit0") == null && grid.FindUnit("unit1").StarLevel == 2, "Consume and upgrade");
                Require(grid.GetUnitAt(origin) == null && grid.GetBlockAt(origin) != null && grid.FloorCells.Count == 12, "Only unit occupancy freed");
                var before = new GridDeploymentSnapshot(grid);
                Require(grid.TryFuseUnits("unit2", "unit3") && grid.StoredCount == 1, "Tray fusion frees capacity");
                Require(grid.TryFuseUnits("unit3", "unit1") && grid.FindUnit("unit1").StarLevel == 3, "Tray-to-board fusion");
                Require(grid.GetInvalidCells().Count > 0 && !grid.CanBeginBattle && !grid.CanSkipPreparation,
                    "Expanded star footprint outside floor blocks combat without rejecting fusion");
                Require(!session.TryBeginBattle(session.RunId, 1), "Invalid placement cannot bypass battle gate");
                Require(before.Units.Single().StarLevel == 2, "Earlier snapshot is immutable");
                Require(!FusionRules.CanFuse(basic.Id, 3, basic.Id, 3), "Maximum three stars");
                Require(!grid.TryFuseUnits("unit1", "unit1"), "Cannot fuse self");
                grid.AddUnit("other", new UnitDefinition("other", "Other", single));
                Require(!grid.TryFuseUnits("other", "unit1"), "Reject different kind/star");
                var validAnchor = origin + Vector2Int.one;
                Place(grid, "unit1", validAnchor);
                Require(grid.GetInvalidCells().Count == 0, "Reposition clears invalid footprint");
                grid.BeginUnitDrag("unit1"); grid.RotatePreview();
                Require(grid.GetPreviewCells().SequenceEqual(basic.GetFootprint(3).GetCells(validAnchor, 1)), "Rotated ghost uses three-star footprint");
                grid.CancelDrag();
                Require(grid.FindUnit("unit1").StarLevel == 3, "Move preserves star");
                grid.BeginUnitDrag("unit1"); Require(grid.DropToTray(), "Return to storage");
                Require(grid.FindUnit("unit1").StarLevel == 3, "Storage preserves star");
                Place(grid, "unit1", validAnchor);
                Require(session.TryBeginBattle(session.RunId, 1), "Capture deployment");
                Require(session.Deployment.Units.Single().StarLevel == 3, "Battle receives star");
                Require(session.Deployment.Units.Single().ShapeId == basic.GetFootprint(3).Id, "Battle receives star footprint");
                Require(!grid.TryFuseUnits("other", "unit1"), "Combat rejects fusion");
            }
            using (var session = new GridRunSession("full_tray", config.Catalog.CreateDefinition(), GridFusionPolicy.CanFuse))
            {
                var grid = session.Grid;
                for (int i = 0; i < grid.Definition.StorageCapacity; i++) grid.AddUnit("u" + i, config.Catalog.CreateInitialUnit());
                session.TryAllowPreparation(session.RunId, 1, false);
                Require(grid.TryFuseUnits("u0", "u1") && grid.StoredCount == 9 && !grid.HasPendingStorage, "Full tray can fuse without discard");
            }
        }
        public static FootprintDefinition CreateTestFloor(Vector2Int size)
        {
            var cells = new System.Collections.Generic.List<Vector2Int>();
            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++) cells.Add(new Vector2Int(x, y));
            return new FootprintDefinition("verification_floor", "Verification floor", cells);
        }
        public static void Place(GridManager grid, string id, Vector2Int cell)
        { Require(grid.BeginUnitDrag(id), "Select " + id); grid.MovePreview(cell); Require(grid.CommitPreview(), "Place " + id); }
        public static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
