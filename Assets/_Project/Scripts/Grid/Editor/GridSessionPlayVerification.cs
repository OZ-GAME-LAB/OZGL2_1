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
            try { await Verify(); LastResult = "PASS: bounded common storage, discard UI, reward/return pending reload, cancellation, required expansion, deployment and binding lifecycle."; }
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
                                bool accepted = view.PrototypeFlow.TryChooseUnit(round % 2);
                if (!accepted)
                {
                    var pending = grid.PendingStorage; Require(pending != null, "Capacity request exists");
                    var chosen = new System.Collections.Generic.List<GridStoredItem>();
                    foreach (var item in pending.DiscardCandidates) { if (chosen.Count == pending.RequiredDiscardCount) break; chosen.Add(item); }
                    Require(view.PrototypeFlow.TryConfirmStorage(pending.RequestId, chosen), "Resolve capacity");
                }
                Require(grid.StoredCount <= 10 && grid.Units.Count >= 1, "Bounded reward storage");
                Require(!rewards.TryChoose(session, 0), "Duplicate selection rejected");
            }
            await Frames();
            var tray = view.GetComponent<UIDocument>().rootVisualElement.Q<ScrollView>("storage-tray");
            Require(tray.horizontalScroller.highValue > 0, "Reward overflow scrollable");
            var last = tray.contentContainer[tray.contentContainer.childCount - 1];
            tray.ScrollTo(last); await Frames();
            Require(last.worldBound.Overlaps(tray.contentViewport.worldBound), "Last reward card reachable");
            Require(session.TryBeginBattle(session.RunId, session.NextRound) && view.PrototypeFlow.TryFinishBattle(), "Full reward round");
            Require(!view.PrototypeFlow.TryChooseUnit(0), "Full reward requires discard");
            var pendingBeforeReload = grid.PendingStorage;
            string requestId = pendingBeforeReload.RequestId;
            string scenePath = SceneManager.GetActiveScene().path;
            var reload = SceneManager.LoadSceneAsync(scenePath);
            while (!reload.isDone) await Task.Yield();
            await Frames(); view = GetView();
            Require(ReferenceEquals(view.Session, session) && grid.PendingStorage.RequestId == requestId && grid.StoredCount == 10, "Pending state survives reload");
            var rootUi = view.GetComponent<UIDocument>().rootVisualElement;
            Require(rootUi.Q<VisualElement>("storage-resolution").resolvedStyle.display == DisplayStyle.Flex, "Pending UI restored");
            for (int i = 0; i < pendingBeforeReload.RequiredDiscardCount; i++)
            {
                var item = pendingBeforeReload.DiscardCandidates[i];
                rootUi.Q<Toggle>("discard-" + item.Kind + "-" + item.InstanceId).value = true;
            }
            var confirm = rootUi.Q<Button>("confirm-storage"); Require(confirm.enabledSelf, "Discard count enables confirmation");
            using (var click = NavigationSubmitEvent.GetPooled()) { click.target = confirm; confirm.SendEvent(click); }
            await Frames();
            Require(grid.PendingStorage == null && grid.Phase == eGridPhase.PREPARATION && grid.StoredCount == 10, "UI confirms reward and enters preparation");
            Require(session.TryBeginBattle(session.RunId, session.NextRound) && view.PrototypeFlow.TryFinishBattle() && view.PrototypeFlow.TryChooseExpansion(), "Mandatory reward pending");
            var path = SceneManager.GetActiveScene().path;
            var operation = SceneManager.LoadSceneAsync(path);
            while (!operation.isDone) await Task.Yield();
            await Frames();
            view = GetView();
            Require(ReferenceEquals(view.Session, session) && grid.StoredCount == 10 && grid.RequiresExpansionPlacement, "Screen recreation preserves run and rewards");
            Require(!session.TryBeginBattle(session.RunId, session.NextRound, true), "Reload cannot bypass required expansion");
            grid.BeginExpansionDrag(); grid.MovePreview(new Vector2Int(1, 0)); Require(grid.CommitPreview(), "Expansion after reload");
            Require(session.TryBeginBattle(session.RunId, session.NextRound) && session.Deployment.Units.Count == 1 && session.Deployment.FloorCells.Count == 14, "Next combat receives committed layout");
            Require(view.PrototypeFlow.TryFinishBattle() && !view.PrototypeFlow.TryChooseUnit(0), "Another full reward");
            var fullRequest = grid.PendingStorage;
            var discardItems = new System.Collections.Generic.List<GridStoredItem>();
            for (int i = 0; i < fullRequest.RequiredDiscardCount; i++) discardItems.Add(fullRequest.DiscardCandidates[i]);
            Require(view.PrototypeFlow.TryConfirmStorage(fullRequest.RequestId, discardItems), "Return test preparation");
            var deployed = grid.GetUnitAt(new Vector2Int(2, 0));
            Require(grid.BeginUnitDrag(deployed.InstanceId) && !grid.DropToTray(), "Full tray delays unit return");
            string returnRequestId = grid.PendingStorage.RequestId;
            var returnReload = SceneManager.LoadSceneAsync(scenePath);
            while (!returnReload.isDone) await Task.Yield();
            await Frames(); view = GetView();
            Require(grid.PendingStorage.RequestId == returnRequestId && grid.FindUnit(deployed.InstanceId).IsPlaced && grid.StoredCount == 10, "Return pending survives scene reload without early removal");
            rootUi = view.GetComponent<UIDocument>().rootVisualElement;
            var cancel = rootUi.Q<Button>("cancel-storage");
            using (var click = NavigationSubmitEvent.GetPooled()) { click.target = cancel; cancel.SendEvent(click); }
            await Frames();
            Require(grid.PendingStorage == null && grid.FindUnit(deployed.InstanceId).IsPlaced && grid.CanBeginBattle, "UI cancels return and restores interaction");
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

