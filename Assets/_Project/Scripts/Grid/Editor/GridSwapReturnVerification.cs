using System;
using System.Linq;
using System.Threading.Tasks;
using OZGL2.Grid.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.Editor
{
    public static class GridSwapReturnVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/Grid/Verify Swap and Right Click (Empty Play Scene)")]
        public static async void Run()
        {
            Result = "Running";
            GameObject root = null;
            GridPrototypeRunner runner = null;
            var background = Application.runInBackground;
            using (var session = GridPlacementQAVerification.CreateSession())
            try
            {
                Check(Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<OZGL2.InGame.InGamePrototypeBootstrap>() == null, "Use empty Play scene");
                Application.runInBackground = true;
                var grid = session.Grid;
                var origin = grid.Definition.InitialOrigin;
                var support = new FootprintDefinition("support", "Support", Enumerable.Range(0, 12).Select(i => new Vector2Int(i % 4, i / 4)));
                GridPlacementQAVerification.PlaceBlock(grid, "floor", support, origin);
                var single = GridPlacementQAVerification.SINGLE;
                Place(grid, "a", new UnitDefinition("a", "A", single), origin);
                Place(grid, "b", new UnitDefinition("b", "B", single), origin + Vector2Int.right);
                int changes = 0; grid.LayoutChanged += () => changes++;
                grid.BeginUnitDrag("a"); grid.MovePreview(origin + Vector2Int.right);
                Check(grid.CanSwapPreview && grid.GetSwapReturnCells().Single() == origin, "Two destination preview");
                grid.CancelDrag(); Check(changes == 0 && grid.FindUnit("a").Anchor == origin, "Cancel unchanged");

                root = new GameObject("SwapReturnUI"); root.SetActive(false);
                root.AddComponent<UIDocument>().panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/Data/Grid/Prototype/GridPanelSettings.asset");
                runner = root.AddComponent<GridPrototypeRunner>(); runner.Bind(session); root.SetActive(true);
                await Task.Delay(150);
                var panel = root.GetComponent<UIDocument>().rootVisualElement;
                var board = runner.Board;
                Pointer(board.Element, board.CellToPanel(origin), true, 0);
                Pointer(panel, board.CellToPanel(origin + Vector2Int.right), false, 0);
                Check(grid.FindUnit("a").Anchor == origin + Vector2Int.right && grid.FindUnit("b").Anchor == origin && changes == 1, "Pointer swap commits atomically");
                Check(grid.StoredCount == 0 && grid.Blocks.Single().IsPlaced, "Swap retains storage and platform");
                Pointer(board.Element, board.CellToPanel(origin), true, 1);
                Check(!grid.FindUnit("b").IsPlaced && !grid.HasSelection && grid.Blocks.Single().IsPlaced, "Right-click returns only unit");
                Check(grid.BeginUnitDrag("b"), "Tray cannot swap"); grid.MovePreview(grid.FindUnit("a").Anchor);
                Check(!grid.CanSwapPreview && !grid.CommitPreview(), "Stored source rejected"); grid.CancelDrag();
                grid.BeginUnitDrag("b"); grid.MovePreview(origin); Check(grid.CommitPreview(), "Redeploy");
                grid.BeginUnitDrag("a"); Pointer(board.Element, board.CellToPanel(origin), true, 1);
                Check(grid.FindUnit("b").IsPlaced && grid.SelectedId == "a", "Right-click blocked during drag"); grid.CancelDrag();

                var vertical = new FootprintDefinition("vertical", "Vertical", new[] { Vector2Int.zero, Vector2Int.up });
                var horizontal = new FootprintDefinition("horizontal", "Horizontal", new[] { Vector2Int.zero, Vector2Int.right });
                Place(grid, "v", new UnitDefinition("v", "V", vertical), origin + new Vector2Int(2,0));
                Place(grid, "h", new UnitDefinition("h", "H", horizontal), origin + new Vector2Int(0,2));
                grid.BeginUnitDrag("v"); grid.RotatePreview(); grid.MovePreview(grid.FindUnit("h").Anchor);
                Check(!grid.CanSwapPreview && !grid.CommitPreview(), "Equal count but different original shape rejected"); grid.CancelDrag();
                // Non-anchor cell must also resolve the same placed unit.
                Pointer(board.Element, board.CellToPanel(origin + new Vector2Int(2,1)), true, 1);
                Check(!grid.FindUnit("v").IsPlaced, "Right-click occupied non-anchor cell");
                for (int i = grid.StoredCount; i < grid.Definition.StorageCapacity; i++) grid.AddUnit("stored_" + i, new UnitDefinition("stored", "Stored", single));
                Pointer(board.Element, board.CellToPanel(grid.FindUnit("a").Anchor), true, 1);
                Check(grid.HasPendingStorage && grid.FindUnit("a").IsPlaced && grid.StoredCount == 10, "Full storage waits without changing original");
                var pending = grid.PendingStorage;
                Check(session.TryCancelStorage(session.RunId, pending.RequestId) && grid.FindUnit("a").IsPlaced, "Discard cancel restores original");
                grid.TryReturnUnitToTray("a"); pending = grid.PendingStorage;
                Check(session.TryConfirmStorage(session.RunId, pending.RequestId, pending.DiscardCandidates.Take(1).ToArray()), "Discard confirms return");
                Check(!grid.FindUnit("a").IsPlaced && grid.StoredCount == 10, "Capacity retained");
                Check(session.TryBeginBattle(session.RunId, 1), "Battle begins");
                Pointer(board.Element, board.CellToPanel(grid.FindUnit("b").Anchor), true, 1);
                Check(grid.FindUnit("b").IsPlaced && !grid.HasSelection, "Battle blocks right-click");
                Check(grid.LastObserverError == null, "No observer errors");
                VerifyFusionPriority();
                VerifyStarredSwap();
                Result = "PASS: pointer swap/cancel/atomic notification, tray and shape rejection, right-click/non-anchor/drag lock, full-storage cancel/confirm, battle lock and fusion priority";
            }
            catch (Exception error) { Result = "FAIL: " + error; }
            finally { runner?.Unbind(); if (root != null) UnityEngine.Object.Destroy(root); Application.runInBackground = background; }
        }
        private static void VerifyFusionPriority()
        {
            using (var session = GridPlacementQAVerification.CreateSession())
            {
                var g = session.Grid; var p = g.Definition.InitialOrigin; var single = GridPlacementQAVerification.SINGLE;
                GridPlacementQAVerification.PlaceBlock(g,"floor",new FootprintDefinition("two","Two",new[]{Vector2Int.zero,Vector2Int.right}),p);
                var definition = new UnitDefinition("same", "Same", single);
                Place(g,"a",definition,p); Place(g,"b",definition,p+Vector2Int.right);
                g.BeginUnitDrag("a"); g.MovePreview(p+Vector2Int.right);
                Check(g.CanFusePreview && !g.CanSwapPreview && g.CommitPreview() && g.Units.Count == 1 && g.FindUnit("b").StarLevel == 2,"Fusion before swap");
            }
        }
        private static void VerifyStarredSwap()
        {
            using (var session = GridPlacementQAVerification.CreateSession())
            {
                var g = session.Grid; var p = g.Definition.InitialOrigin;
                var single = GridPlacementQAVerification.SINGLE;
                var vertical = new FootprintDefinition("star_two", "Two", new[]{Vector2Int.zero,Vector2Int.up});
                GridPlacementQAVerification.PlaceBlock(g,"support",new FootprintDefinition("support","Support",Enumerable.Range(0,12).Select(i=>new Vector2Int(i%4,i/4))),p);
                foreach(var id in new[]{"a","b"})
                {
                    var definition = new UnitDefinition(id,id,single,null,vertical);
                    Place(g,id,definition,p+(id=="a"?Vector2Int.zero:Vector2Int.right*2));
                    g.AddUnit(id+"_fuse",definition); Check(g.TryFuseUnits(id+"_fuse",id),"Create star two");
                    var anchor=g.FindUnit(id).Anchor;
                    g.BeginUnitDrag(id); g.MirrorPreview(); g.MovePreview(anchor); Check(g.CommitPreview(),"Mirror star two");
                }
                g.BeginUnitDrag("a");g.MovePreview(g.FindUnit("b").Anchor);
                Check(g.CanSwapPreview && g.CommitPreview(),"Matching star footprints swap");
                Check(g.FindUnit("a").StarLevel==2 && g.FindUnit("a").IsMirrored && g.FindUnit("b").IsMirrored,"Preserve stars and mirror");
                Check(session.TryBeginBattle(session.RunId,1),"Capture swapped deployment");
                Check(session.Deployment.Units.Single(u=>u.InstanceId=="a").Anchor==p+Vector2Int.right*2,"Swapped position reaches combat snapshot");
            }
        }
        private static void Place(GridManager grid, string id, UnitDefinition definition, Vector2Int cell)
        { grid.AddUnit(id,definition); grid.BeginUnitDrag(id); grid.MovePreview(cell); Check(grid.CommitPreview(),"Place " + id); }
        private static void Pointer(VisualElement target, Vector2 point, bool down, int button)
        {
            var input = new Event { type = down ? EventType.MouseDown : EventType.MouseUp, mousePosition = point, button = button };
            if (down) { using (var evt = PointerDownEvent.GetPooled(input)) { evt.target = target; target.SendEvent(evt); } }
            else { using (var evt = PointerUpEvent.GetPooled(input)) { evt.target = target; target.SendEvent(evt); } }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
