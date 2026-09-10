using System;
using System.Threading.Tasks;
using OZGL2.Grid.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OZGL2.Grid.Editor
{
    public static class GridSessionPlayVerification
    {
        public static string LastResult { get; private set; } = "Not run";
        [MenuItem("OZGL2/Grid/Verify Session And Rewards (Play)")]
        public static async void Run()
        {
            LastResult = "Running";
            try { await Verify(); LastResult = "PASS: two unit choices plus expansion, repeat grants, scroll access, scene reload preserves session and required expansion, next battle snapshot."; }
            catch (Exception exception) { LastResult = "FAIL: " + exception; }
        }
        private static async Task Verify()
        {
            Require(Application.isPlaying, "Fresh Play session required");
            var view = GetView(); var session = view.Session; var grid = session.Grid;
            Require(grid.PlacedCount == 0 && session.CompletedRounds == 0, "Fresh session required");
            grid.BeginBlockDrag("block_single"); grid.MovePreview(new Vector2Int(2, 0)); Require(grid.CommitPreview(), "Initial block");
            grid.BeginUnitDrag("unit_dummy_unit_single"); grid.MovePreview(new Vector2Int(2, 0)); Require(grid.CommitPreview(), "Initial unit");
            var catalog = AssetDatabase.LoadAssetAtPath<GridPrototypeCatalogSO>(GridPrototypeSetup.DATA_PATH + "/GridPrototypeCatalog.asset");
            var rewards = new GridPrototypeRewards(catalog);
            for (int round = 0; round < 8; round++)
            {
                Require(session.TryBeginBattle(session.RunId, session.NextRound) && view.PrototypeFlow.TryFinishBattle(), "Round flow");
                int count = grid.Units.Count;
                Require(view.PrototypeFlow.TryChooseUnit(round % 2) && grid.Units.Count == count + 1 && grid.Blocks.Count == count + 1, "Exactly one unit and one block");
                Require(!rewards.TryChoose(session, 0), "Duplicate selection rejected");
            }
            await Frames();
            var tray = view.GetComponent<UIDocument>().rootVisualElement.Q<ScrollView>("unit-tray");
            Require(tray.horizontalScroller.highValue > 0, "Reward overflow scrollable");
            var last = tray.contentContainer[tray.contentContainer.childCount - 1];
            tray.ScrollTo(last); await Frames();
            Require(last.worldBound.Overlaps(tray.contentViewport.worldBound), "Last reward card reachable");
            Require(session.TryBeginBattle(session.RunId, session.NextRound) && view.PrototypeFlow.TryFinishBattle() && view.PrototypeFlow.TryChooseExpansion(), "Mandatory reward pending");
            var path = SceneManager.GetActiveScene().path;
            var operation = SceneManager.LoadSceneAsync(path);
            while (!operation.isDone) await Task.Yield();
            await Frames();
            view = GetView();
            Require(ReferenceEquals(view.Session, session) && grid.Units.Count == 14 && grid.RequiresExpansionPlacement, "Screen recreation preserves run and rewards");
            Require(!session.TryBeginBattle(session.RunId, session.NextRound, true), "Reload cannot bypass required expansion");
            grid.BeginExpansionDrag(); grid.MovePreview(new Vector2Int(1, 0)); Require(grid.CommitPreview(), "Expansion after reload");
            Require(session.TryBeginBattle(session.RunId, session.NextRound) && session.Deployment.Units.Count == 1 && session.Deployment.FloorCells.Count == 14, "Next combat receives committed layout");
            var isolatedScreen = new GameObject("GridBindingVerification");
            isolatedScreen.SetActive(false);
            var document = isolatedScreen.AddComponent<UIDocument>();
            document.panelSettings = view.GetComponent<UIDocument>().panelSettings;
            var isolatedView = isolatedScreen.AddComponent<GridPrototypeRunner>();
            var externalSession = new GridRunSession("external_binding_test", catalog.CreateDefinition());
            try
            {
                isolatedScreen.SetActive(true); await Frames();
                Require(isolatedView.Session == null && !string.IsNullOrEmpty(isolatedView.BindingError), "Missing injection is reported without auto-bootstrap");
                isolatedView.Bind(externalSession); await Frames();
                Require(ReferenceEquals(isolatedView.Session, externalSession) && isolatedView.BindingError == null && isolatedView.PrototypeFlow == null,
                    "External session binds without dummy catalog or flow");
                bool rejected = false;
                try { isolatedView.Bind(session); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected && ReferenceEquals(isolatedView.Session, externalSession), "Active binding cannot be silently replaced");
                externalSession.Dispose(); await Frames();
                Require(externalSession.Grid.Phase == eGridPhase.ENDED && isolatedView.BindingError == "Grid run ended.", "Shutdown detaches input and presents ended state");
                isolatedScreen.SetActive(false); isolatedScreen.SetActive(true); await Frames();
                Require(isolatedView.BindingError == "Grid run ended.", "Reactivation cannot restart ended run");
            }
            finally { externalSession.Dispose(); UnityEngine.Object.Destroy(isolatedScreen); }
        }
        private static GridPrototypeRunner GetView()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.TryGetComponent<GridPrototypeRunner>(out var runner)) return runner;
            throw new Exception("Prototype view not found");
        }
        private static async Task Frames()
        {
            int target = Time.frameCount + 3;
            while (Time.frameCount < target) await Task.Yield();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}

