using System;
using System.Linq;
using OZGL2.Grid.UI;
using UnityEditor;
using UnityEngine;

namespace OZGL2.Grid.Editor
{
    public static class GridConnectivityVerification
    {
        public static string Result { get; private set; } = "Not run";
        private static readonly FootprintDefinition SINGLE = new FootprintDefinition("single", "Single", new[] { Vector2Int.zero });
        private static readonly FootprintDefinition EXPANSION = new FootprintDefinition("expand", "Expand", new[] { Vector2Int.zero, Vector2Int.up });
        [MenuItem("OZGL2/Grid/Verify Connectivity")]
        public static void Run()
        {
            try { Verify(); Result = "PASS: first/adjacent/diagonal, bridge move/return atomicity, no-op rotation, cancellation, occupied end return, capacity transaction, expansion attachment, borders and inner corners."; }
            catch (Exception error) { Result = "FAIL: " + error; }
        }
        private static void Verify()
        {
            using (var session = new GridRunSession("connectivity", new GridDefinition(new Vector2Int(4, 3), new Vector2Int(8, 5), EXPANSION)))
            {
                var grid = session.Grid;
                session.TryAllowPreparation(session.RunId, 1, false);
                foreach (var id in new[] { "a", "b", "c" }) grid.AddBlock(id, id, SINGLE);
                var origin = grid.Definition.InitialOrigin;
                Place(grid, "a", origin);
                grid.BeginBlockDrag("b"); grid.MovePreview(origin + Vector2Int.one);
                Check(grid.GetPreviewFailure() == ePlacementFailure.DISCONNECTED && !grid.CommitPreview(), "Diagonal rejected");
                grid.CancelDrag();
                Place(grid, "b", origin + Vector2Int.right);
                Place(grid, "c", origin + Vector2Int.right * 2);
                grid.AddUnit("u", new UnitDefinition("u", "Unit", SINGLE));
                grid.BeginUnitDrag("u"); grid.MovePreview(origin + Vector2Int.right); Check(grid.CommitPreview(), "Unit on bridge");
                int changed = 0; grid.LayoutChanged += () => changed++;
                grid.BeginBlockDrag("b");
                Check(grid.GetTrayDropFailure() == ePlacementFailure.DISCONNECTED && !grid.DropToTray(), "Bridge cannot return");
                Check(grid.FindUnit("u").IsPlaced && grid.StoredCount == 0 && changed == 0 && !grid.HasPendingStorage, "Rejected return is atomic");
                grid.MovePreview(origin + Vector2Int.one);
                Check(grid.GetPreviewFailure() == ePlacementFailure.DISCONNECTED && !grid.CommitPreview(), "Bridge cannot move");
                grid.CancelDrag();
                grid.BeginBlockDrag("b"); grid.RotatePreview(); Check(grid.CommitPreview(), "Symmetric no-op on bridge");
                Check(grid.FindUnit("u").IsPlaced && changed == 0, "No-op keeps occupants");
                grid.BeginBlockDrag("c"); grid.MovePreview(origin + new Vector2Int(3, 2));
                Check(!grid.CommitPreview(), "Disconnected endpoint move");
                grid.CancelDrag(); Check(grid.FindBlock("c").Anchor == origin + Vector2Int.right * 2, "Cancel restores end");
                Place(grid, "c", origin + Vector2Int.one);
                var borders = GridSurfaceLayout.GetBorders(grid, origin + Vector2Int.right, 0.0625f);
                Check(borders.Count == 3 && borders.Any(r => Mathf.Approximately(r.width, 0.0625f) && Mathf.Approximately(r.height, 0.0625f)), "L inner corner closed");
                grid.BeginBlockDrag("c"); Check(grid.DropToTray(), "End can return");
                Check(GridSurfaceLayout.GetBorders(grid, origin, 0.0625f).Count == 3, "Shared platform edge hidden");
                Check(GridSurfaceLayout.GetBorders(grid, new Vector2Int(0, 4), 0.0625f).Count == 0, "Unclaimed has no platform border");
                grid.BeginUnitDrag("u"); Check(grid.DropToTray(), "Unit independent of platform connectivity");
                grid.BeginUnitDrag("u"); grid.MovePreview(origin); Check(grid.CommitPreview(), "Unit can relocate");
                for (int i = 0; i < 9; i++) grid.AddUnit("stored" + i, new UnitDefinition("stored", "Stored", SINGLE));
                grid.BeginBlockDrag("a"); Check(!grid.DropToTray() && grid.HasPendingStorage, "Occupied end return requests space");
                Check(grid.FindBlock("a").IsPlaced && grid.FindUnit("u").IsPlaced, "Capacity request preserves original state");
                Check(session.TryCancelStorage(session.RunId, grid.PendingStorage.RequestId), "Cancel capacity request");
                Check(grid.FindBlock("a").IsPlaced && grid.FindUnit("u").IsPlaced, "Capacity cancellation atomic");
                grid.BeginBlockDrag("a"); grid.DropToTray();
                var request = grid.PendingStorage;
                Check(session.TryConfirmStorage(session.RunId, request.RequestId, request.DiscardCandidates.Take(request.RequiredDiscardCount).ToArray()), "Confirm capacity request");
                Check(!grid.FindBlock("a").IsPlaced && !grid.FindUnit("u").IsPlaced && grid.StoredCount == 10, "Occupied end returned with unit");
                var floor = new System.Collections.Generic.HashSet<Vector2Int>(grid.FloorCells);
                Check(GridPlacementRules.ValidateExpansion(grid.Definition, floor, EXPANSION.GetCells(new Vector2Int(1, 3), 0)) == ePlacementFailure.DISCONNECTED, "Diagonal expansion rejected");
                Check(GridPlacementRules.ValidateExpansion(grid.Definition, floor, EXPANSION.GetCells(new Vector2Int(1, 0), 0)) == ePlacementFailure.NONE, "Edge expansion accepted");
            }
        }
        public static void Place(GridManager grid, string id, Vector2Int cell)
        {
            Check(grid.BeginBlockDrag(id), "Select " + id);
            grid.MovePreview(cell); Check(grid.CommitPreview(), "Place " + id);
        }
        public static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
