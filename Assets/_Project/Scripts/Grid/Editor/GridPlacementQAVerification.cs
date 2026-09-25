using System;
using System.Collections.Generic;
using System.Linq;
using OZGL2.Grid.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.Editor
{
    public static class GridPlacementQAVerification
    {
        public static string Result { get; private set; } = "Not run";
        public static readonly FootprintDefinition SINGLE = new FootprintDefinition("single", "Single", new[] { Vector2Int.zero });
        public static readonly FootprintDefinition EXPANSION = new FootprintDefinition("expand", "Expand", new[] { Vector2Int.zero, Vector2Int.up });
        public static readonly FootprintDefinition CORNER = new FootprintDefinition("corner_qa", "Corner", new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.up * 2, new Vector2Int(1, 0) });
        [MenuItem("OZGL2/Grid/Verify Placement QA")]
        public static void Run()
        {
            try
            {
                VerifyTransforms(); VerifyExpansion(); VerifyMirroredUnits(); VerifyMirroredBlocks();
                Result = "PASS: screen reflection/rotation, expansion identity/atomic move/occupied/bridge/bounds/storage/mandatory gates, mirrored placement/fusion/tray/snapshot, occupied and invalid overlays";
            }
            catch (Exception error) { Result = "FAIL: " + error; }
        }
        public static GridRunSession CreateSession()
        {
            var session = new GridRunSession(Guid.NewGuid().ToString("N"), new GridDefinition(new Vector2Int(4, 3), new Vector2Int(8, 5), EXPANSION),
                (a, b) => a.Definition.Id == b.Definition.Id && a.StarLevel == b.StarLevel && a.StarLevel < 3);
            Check(session.TryAllowPreparation(session.RunId, 1, false), "Preparation");
            return session;
        }
        public static void PlaceBlock(GridManager grid, string id, FootprintDefinition shape, Vector2Int anchor)
        {
            grid.AddBlock(id, shape.Id, shape); Check(grid.BeginBlockDrag(id), "Block drag");
            grid.MovePreview(anchor); Check(grid.CommitPreview(), "Place block " + id);
        }
        private static void SeedBattleUnit(GridManager grid)
        {
            var anchor = grid.Definition.InitialOrigin;
            PlaceBlock(grid, "base", SINGLE, anchor);
            grid.AddUnit("base_unit", new UnitDefinition("base_unit", "Base", SINGLE));
            grid.BeginUnitDrag("base_unit"); grid.MovePreview(anchor); Check(grid.CommitPreview(), "Base unit");
        }
        public static void RequestExpansion(GridRunSession session)
        {
            int round = session.NextRound;
            Check(session.TryBeginBattle(session.RunId, round), "Start before expansion");
            Check(session.TryFinishBattle(session.RunId, round, "reward_" + round), "Finish before expansion");
            Check(session.TryChooseExpansion(session.RunId, session.PendingRewardId), "Choose expansion");
            Check(session.TryAllowPreparation(session.RunId, session.NextRound, true), "Next preparation");
        }
        public static ExpansionPlacement AddExpansion(GridRunSession session, Vector2Int anchor)
        {
            RequestExpansion(session);
            Check(session.Grid.BeginExpansionDrag(), "Required expansion drag");
            session.Grid.MovePreview(anchor); Check(session.Grid.CommitPreview(), "Required expansion commit");
            return session.Grid.Expansions.Last();
        }
        private static void VerifyTransforms()
        {
            using (var session = CreateSession())
            {
                var grid = session.Grid; grid.AddBlock("shape", CORNER.Id, CORNER);
                foreach (int rotation in Enumerable.Range(0, 4))
                    foreach (bool mirrored in new[] { false, true })
                    {
                        grid.BeginBlockDrag("shape"); grid.MovePreview(new Vector2Int(4, 1));
                        if (mirrored) grid.MirrorPreview();
                        for (int i = 0; i < rotation; i++) grid.RotatePreview();
                        var before = grid.GetPreviewCells(); var anchor = grid.PreviewAnchor;
                        grid.MirrorPreview();
                        Check(new HashSet<Vector2Int>(grid.GetPreviewCells()).SetEquals(before.Select(c => new Vector2Int(2 * anchor.x - c.x, c.y))), "F reflects current screen X");
                        grid.MirrorPreview(); Check(grid.GetPreviewCells().SequenceEqual(before), "F twice restores");
                        for (int i = 0; i < 4; i++) grid.RotatePreview();
                        Check(grid.GetPreviewCells().SequenceEqual(before), "R four times restores"); grid.CancelDrag();
                    }
            }
        }
        private static void VerifyExpansion()
        {
            using (var session = CreateSession())
            {
                var grid = session.Grid; SeedBattleUnit(grid);
                var initial = new HashSet<Vector2Int>(grid.FloorCells);
                var bridge = AddExpansion(session, new Vector2Int(1, 0));
                var outer = AddExpansion(session, new Vector2Int(0, 0));
                Check(grid.GetExpansionAt(grid.Definition.InitialOrigin) == null && grid.Expansions.Count == 2, "Initial floor immutable and separate");
                Check(!grid.BeginExpansionDrag("missing"), "Unknown expansion");
                int notifications = 0; grid.LayoutChanged += () => notifications++;
                var before = new HashSet<Vector2Int>(grid.FloorCells);
                grid.BeginExpansionDrag(bridge.InstanceId); grid.MovePreview(new Vector2Int(6, 0));
                Check(!grid.CommitPreview() && grid.LastDropFailure == ePlacementFailure.DISCONNECTED, "Bridge cannot isolate outer floor");
                Check(before.SetEquals(grid.FloorCells) && notifications == 0, "Failed move atomic");
                grid.CancelDrag();
                grid.BeginExpansionDrag(outer.InstanceId); grid.MovePreview(new Vector2Int(6, 0));
                Check(!grid.CanBeginBattle && !grid.CanSkipPreparation, "Drag gates battle");
                Check(grid.CommitPreview() && notifications == 1 && grid.FloorCells.Count == 16 && initial.IsSubsetOf(grid.FloorCells), "Move preserves initial area and count");
                Check(grid.FindExpansion(outer.InstanceId).Anchor == new Vector2Int(6, 0), "Identity preserved");
                grid.BeginExpansionDrag(outer.InstanceId);
                Check(!grid.DropToTray() && grid.LastDropFailure == ePlacementFailure.CANNOT_STORE_EXPANSION && grid.StoredCount == 0, "Expansion cannot be stored");
                grid.MovePreview(grid.Definition.InitialOrigin); Check(grid.GetPreviewFailure() == ePlacementFailure.FLOOR_EXISTS, "Cannot overlap initial area");
                grid.MovePreview(new Vector2Int(7, 4)); Check(grid.GetPreviewFailure() == ePlacementFailure.OUTSIDE_BOUNDS, "Cannot leave bounds");
                grid.CancelDrag(); Check(grid.FindExpansion(outer.InstanceId).Anchor == new Vector2Int(6, 0), "Cancel retains pose");
                // 기존 확장 재배치로 새로 받은 확장 보상의 배치 의무가 사라지면 안 된다.
                RequestExpansion(session);
                grid.BeginExpansionDrag(outer.InstanceId); grid.MovePreview(new Vector2Int(6, 1));
                Check(grid.CommitPreview() && grid.RequiresExpansionPlacement && !grid.CanSkipPreparation, "Relocation preserves pending reward");
                grid.BeginExpansionDrag(); grid.MovePreview(new Vector2Int(7, 1)); Check(grid.CommitPreview(), "Pending reward still placeable");
                PlaceBlock(grid, "left", SINGLE, new Vector2Int(1, 0));
                Check(!grid.BeginExpansionDrag(bridge.InstanceId) && grid.LastDropFailure == ePlacementFailure.EXPANSION_OCCUPIED, "Occupied expansion cannot move");
                Check(grid.GetExpansionAt(new Vector2Int(1, 1)).InstanceId == bridge.InstanceId, "Both cells identify bundle");
                Check(grid.GetStoredItems().All(i => i.Kind != eGridDragKind.EXPANSION), "No expansion inventory entry");
                Check(session.TryBeginBattle(session.RunId, session.NextRound), "Battle after expansion");
                Check(!grid.BeginExpansionDrag(outer.InstanceId), "Battle input locked");
                Check(new HashSet<Vector2Int>(session.Deployment.FloorCells).SetEquals(grid.FloorCells), "Moved floor captured for combat");
            }
        }
        private static void VerifyMirroredUnits()
        {
            using (var session = CreateSession())
            {
                var grid = session.Grid;
                var cells = new List<Vector2Int>();
                for (int y = 0; y < 3; y++) for (int x = 0; x < 4; x++) cells.Add(new Vector2Int(x, y));
                var support = new FootprintDefinition("support", "Support", cells);
                PlaceBlock(grid, "support", support, grid.Definition.InitialOrigin);
                var definition = new UnitDefinition("test", "Test", SINGLE, SINGLE.Id, SINGLE, CORNER);
                foreach (var id in new[] { "target", "a", "b", "c" }) grid.AddUnit(id, definition);
                grid.BeginUnitDrag("target"); grid.MovePreview(new Vector2Int(4, 0)); grid.MirrorPreview(); Check(grid.CommitPreview(), "Mirrored target");
                Check(grid.TryFuseUnits("a", "target") && grid.TryFuseUnits("b", "c") && grid.TryFuseUnits("c", "target"), "Fuse to three stars");
                var unit = grid.FindUnit("target");
                Check(unit.StarLevel == 3 && unit.IsMirrored && unit.Anchor == new Vector2Int(4, 0), "Fusion preserves target transform");
                Check(grid.GetOccupiedCells().Count == 4 && grid.GetInvalidCells().Count == 0, "Star footprint occupies all four cells");
                var board = new GridBoardView(grid); board.Render();
                Check(board.Element.Query<UnityEngine.UIElements.VisualElement>("occupied-cell").ToList().Count == 4, "Occupied overlay count");
                grid.BeginUnitDrag("target");
                Check(grid.GetOccupiedCells(grid.SelectedId).Count == 0, "Dragged unit excluded from occupancy display");
                grid.MovePreview(new Vector2Int(7, 4)); grid.MirrorPreview(); Check(!grid.CommitPreview(), "Invalid mirrored placement rejected");
                grid.CancelDrag(); Check(grid.FindUnit("target").IsMirrored, "Cancel preserves mirror");
                grid.BeginUnitDrag("target"); Check(grid.DropToTray() && grid.FindUnit("target").IsMirrored, "Tray preserves mirror");
                grid.BeginUnitDrag("target"); grid.MovePreview(new Vector2Int(4, 0)); Check(grid.CommitPreview(), "Place again");
                Check(session.TryBeginBattle(session.RunId, session.NextRound), "Start mirrored deployment");
                var deployed = session.Deployment.Units.Single();
                Check(deployed.IsMirrored && deployed.StarLevel == 3 && deployed.ShapeId == CORNER.Id && deployed.Cells.SequenceEqual(unit.GetCells()), "Snapshot preserves star/mirror/cells");
                board.Render(); Check(board.Element.Query<UnityEngine.UIElements.VisualElement>("occupied-cell").ToList().Count == 0, "No occupancy overlay in combat");
            }
        }
        private static void VerifyMirroredBlocks()
        {
            using (var session = CreateSession())
            {
                var grid = session.Grid;
                grid.AddBlock("corner", CORNER.Id, CORNER); grid.BeginBlockDrag("corner");
                grid.MovePreview(new Vector2Int(4, 0)); grid.MirrorPreview(); Check(grid.CommitPreview(), "Mirrored block placement");
                Check(grid.GetBlockAt(new Vector2Int(3, 0))?.InstanceId == "corner" && grid.GetBlockAt(new Vector2Int(5, 0)) == null, "Mirrored support lookup");
                grid.AddUnit("u", new UnitDefinition("u", "Unit", SINGLE)); grid.BeginUnitDrag("u");
                grid.MovePreview(new Vector2Int(3, 0)); Check(grid.CommitPreview(), "Unit on mirrored support");
                grid.BeginBlockDrag("corner"); Check(grid.DropToTray(), "Return occupied mirrored block");
                Check(!grid.FindUnit("u").IsPlaced && grid.FindBlock("corner").IsMirrored, "Occupant returned and mirror kept");
            }
        }
        public static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
