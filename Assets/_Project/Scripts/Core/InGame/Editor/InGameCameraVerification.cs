using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Stage;
using OZGL2.Stage.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.InGame.Editor
{
    public static class InGameCameraVerification
    {
        public static string Result { get; private set; } = "Not run";
        public static GameObject Root { get; private set; }
        private static InGameGridSession _session;
        private static CancellationTokenSource _cancel;
        private static Task _run;
        private static StageRunHost _host;
        private static bool _background;
        [MenuItem("OZGL2/InGame/Verify World Preparation (Empty Play Scene)")]
        public static async void Run()
        {
            Result = "Running"; _background = Application.runInBackground; Application.runInBackground = true;
            try
            {
                await Verify(); await CleanupAsync(); await Task.Delay(100);
                Application.runInBackground = true;
                await Verify(true);
                Result = "PASS: initial framing validation, battle resize failure cancels host, preparation recovery/retry, world placement, transition gate, cancellation and viewport framing.";
            }
            catch (Exception error) { Result = "FAIL: " + error; }
            finally { await CleanupAsync(); }
        }
        private static async Task Verify(bool injectResizeFailure = false)
        {
            Check(Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<InGamePrototypeBootstrap>() == null, "Use empty Play scene");
            var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
            Root = new GameObject("CameraVerification"); Root.SetActive(false);
            var bootstrap = Root.AddComponent<InGamePrototypeBootstrap>(); bootstrap.enabled = false;
            Set(bootstrap, "_config", config);
            var cameraGo = new GameObject("TestCamera"); cameraGo.transform.SetParent(Root.transform);
            var camera = cameraGo.AddComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.035f, 0.055f, 0.09f);
            var transition = Root.AddComponent<InGameCameraTransition>();
            Set(transition, "_camera", camera); Set(transition, "_config", AssetDatabase.LoadAssetAtPath<InGameCameraConfigSO>(InGameCameraSetup.CONFIG_PATH));
            var ui = new GameObject("PreparationUI"); ui.transform.SetParent(Root.transform);
            ui.AddComponent<UIDocument>().panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/Data/Grid/Prototype/GridPanelSettings.asset");
            var runner = ui.AddComponent<GridPrototypeRunner>();
            var presentation = Root.AddComponent<InGamePhasePresentation>();
            Set(presentation, "_bootstrap", bootstrap); Set(presentation, "_runner", runner); Set(presentation, "_camera", transition);
            Set(bootstrap, "_phasePresentation", presentation);
            float originalFar = camera.farClipPlane;
            camera.farClipPlane = 2;
            bool rejected = false;
            try { presentation.ValidateSetup(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && bootstrap.Stage == null, "Invalid first framing rejected before stage creation");
            camera.farClipPlane = originalFar;
            presentation.ValidateSetup();
            var board = Root.AddComponent<InGameGridPresentation>();
            Set(board, "_bootstrap", bootstrap);
            Set(board, "_theme", AssetDatabase.LoadAssetAtPath<OZGL2.Grid.UI.GridBoardThemeSO>(GridConnectivitySetup.THEME_PATH));
            _session = new InGameGridSession(config.Catalog.CreateDefinition(), new DummyRewardLedger());
            Set(bootstrap, "_session", _session);
            var dummy = new ManualStageServices();
            var rewards = new StageGridRewards(_session, config.CreateRewardSource(), dummy);
            var prep = new StageGridPreparation(_session, config.Catalog.CreateInitialUnit(), config.Catalog.CreateInitialBlock(), config.InitialAnchor);
            var stage = new StageManager(dummy, rewards, prep, _session, new MemoryStageProgressStore(), new Lobby());
            typeof(InGamePrototypeBootstrap).GetProperty("Stage").SetValue(bootstrap, stage);
            Action notify = () => typeof(InGamePrototypeBootstrap).GetMethod("Notify", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bootstrap, null);
            _session.Changed += notify;
            stage.StateChanged += state => notify();
            Root.SetActive(true);
            _cancel = new CancellationTokenSource();
            _host = new GameObject("CameraVerificationHost").AddComponent<StageRunHost>();
            Set(bootstrap, "_host", _host);
            _cancel.Token.Register(_host.CancelRun);
            Check(_host.StartRun(stage, config.Stage), "Test host starts");
            _run = _host.CurrentRun;
            await Until(() => dummy.PendingRequest == eDummyRequest.BATTLE);
            if (injectResizeFailure)
            {
                camera.farClipPlane = 2;
                Set(presentation, "_resolution", Vector2Int.zero);
                typeof(InGamePhasePresentation).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presentation, null);
                await Until(() => _run.IsCompleted);
                Check(!stage.IsRunning && bootstrap.Error != null && bootstrap.Error.Contains("Camera presentation failed"),
                    "Battle resize failure stops run and preserves reason");
                Check(!presentation.CanInteract && !presentation.CanRetryCamera, "Combat failure cannot resume as preparation");
                return;
            }
            var grid = _session.Session.Grid;
            Check(!presentation.IsTransitioning && !presentation.CanInteract, "Round one directly in battle framing");
            Check(Root.GetComponentsInChildren<UnitBase>(true).Length == 0, "No live units instantiated by preview");
            var point = config.GridWorldOrigin + new Vector3(config.InitialAnchor.x, config.InitialAnchor.y, 0) * config.CellWorldSize;
            Check(presentation.Surface.ScreenToCell(camera.WorldToScreenPoint(point)) == config.InitialAnchor, "Battle camera picking");
            var battlePosition = camera.transform.position;
            dummy.CompleteBattle(dummy.RequestId, eBattleResult.VICTORY);
            await Until(() => rewards.Pending != null);
            rewards.TryChooseUnit(rewards.Pending.RequestId, 0);
            await Until(() => grid.Phase == eGridPhase.PREPARATION);
            Check(!presentation.RequestBattle(false), "Preparation zoom locks start");
            await Until(() => presentation.CanInteract);
            Check(camera.transform.position.z > battlePosition.z, "Preparation is closer");
            Check(presentation.Surface.ScreenToCell(camera.WorldToScreenPoint(point)) == config.InitialAnchor, "Preparation camera picking");
            Check(Root.GetComponentsInChildren<UnitBase>(true).Length == 0 && Root.GetComponentsInChildren<SpriteRenderer>().Length > 0, "Passive sprites only");
            Check(Root.GetComponentsInChildren<TextMesh>().Single().text == "★1", "Preparation displays placed unit star");
            System.IO.Directory.CreateDirectory("Temp/CameraVerification");
            ScreenCapture.CaptureScreenshot("Temp/CameraVerification/preparation.png");
            await Task.Delay(150);
            var panel = runner.GetComponent<UIDocument>().rootVisualElement;
            var pos = presentation.Surface.CellToPanel(config.InitialAnchor);
            using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = pos, button = 0 }))
            { evt.target = presentation.Surface.Element; presentation.Surface.Element.SendEvent(evt); }
            Check(grid.DragKind == eGridDragKind.UNIT, "World pointer selects unit first");
            using (var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
            { evt.target = panel; panel.SendEvent(evt); }
            Check(!grid.HasSelection, "World drag cancellation");
            await Task.Delay(100);
            var block = grid.Blocks.Single(b => !b.IsPlaced);
            var destination = config.InitialAnchor + Vector2Int.right;
            var blockCard = panel.Q<VisualElement>("block-card-" + block.InstanceId);
            Down(blockCard, blockCard.worldBound.center);
            Move(panel, presentation.Surface.CellToPanel(destination));
            Check(grid.GetPreviewFailure() == ePlacementFailure.NONE, "World block ghost valid");
            Up(panel, presentation.Surface.CellToPanel(destination)); await Task.Delay(100);
            Check(grid.FindBlock(block.InstanceId).IsPlaced, "Card-to-world block placement");
            var stored = grid.Units.Single(u => !u.IsPlaced);
            var card = panel.Q<VisualElement>("card-" + stored.InstanceId);
            Down(card, card.worldBound.center); Up(panel, presentation.Surface.CellToPanel(destination)); await Task.Delay(100);
            Check(grid.FindUnit(stored.InstanceId).IsPlaced, "Card-to-world unit placement");
            Down(presentation.Surface.Element, presentation.Surface.CellToPanel(destination));
            Up(panel, panel.Q<VisualElement>("storage-tray").worldBound.center); await Task.Delay(100);
            Check(!grid.FindUnit(stored.InstanceId).IsPlaced && grid.FindBlock(block.InstanceId).IsPlaced, "World-to-tray returns unit only");
            Check(Root.GetComponentsInChildren<TextMesh>().Length == grid.PlacedCount, "Returned unit leaves no world star label");
            Set(bootstrap, "_combatConnection", new InGameCombatConnection(new[] { presentation }, () => null));
            Check(bootstrap.HasCombatParticipants && !bootstrap.HasExternalCombatParticipants, "Camera does not imply external combat connection");
            Set(bootstrap, "_combatConnection", null);
            float farClip = camera.farClipPlane;
            camera.farClipPlane = 2;
            Check(bootstrap.TryBeginBattle(), "Failed camera request accepted for processing");
            await Until(() => !presentation.IsTransitioning);
            Check(presentation.Error != null && presentation.Error.Contains("recovery failed") && !presentation.CanInteract,
                "Both transition and rollback failures are contained");
            Check(grid.Phase == eGridPhase.PREPARATION && presentation.CanExitAfterCameraError, "Failure cannot start battle and permits safe exit");
            camera.farClipPlane = farClip;
            Check(presentation.RetryCamera() && !presentation.RetryCamera(), "Camera retry rejects duplicates");
            await Until(() => presentation.CanInteract);
            Check(presentation.Error == null && !presentation.CanRetryCamera, "Successful recovery clears error and unlocks input");
            Check(bootstrap.TryBeginBattle() && !bootstrap.TryBeginBattle(), "Single accepted start");
            Check(grid.Phase == eGridPhase.PREPARATION && _session.Session.Deployment != null, "Battle not committed before zoom ends");
            await Until(() => dummy.PendingRequest == eDummyRequest.BATTLE);
            Check(grid.Phase == eGridPhase.BATTLE && Vector3.Distance(camera.transform.position, battlePosition) < 0.01f, "Commit after framing");
            presentation.SetCombatEnabled(true);
            Check(Root.GetComponentsInChildren<TextMesh>().Length == 0, "Combat hides all preparation stars");
            ui.SetActive(false);
            ScreenCapture.CaptureScreenshot("Temp/CameraVerification/battle.png");
            await Task.Delay(100); ui.SetActive(true);
            dummy.CompleteBattle(dummy.RequestId, eBattleResult.VICTORY);
            await Until(() => rewards.Pending != null);
            rewards.TryChooseUnit(rewards.Pending.RequestId, 0);
            await Until(() => presentation.CanInteract);
            Check(bootstrap.TryBeginBattle(), "Start next transition");
            presentation.CancelPresentation(); _cancel.Cancel();
            await Task.Delay(750);
            Check(dummy.PendingRequest != eDummyRequest.BATTLE && !transition.IsMoving, "Cancel cannot start stale battle");
            var pending = transition.FrameAsync(new Bounds(Vector3.zero, new Vector3(8, 6, 0)), true);
            transition.Cancel();
            Check(pending.IsCanceled, "Camera cancellation completes task");
            foreach (float aspect in new[] { 16f / 9f, 4f / 3f, 21f / 9f })
            {
                camera.aspect = aspect;
                var bounds = new Bounds(Vector3.zero, new Vector3(8, 6, 0));
                await transition.FrameAsync(bounds, true, true);
                var safe = transition.Config.GetViewport(true);
                foreach (var corner in new[] { bounds.min, bounds.max, new Vector3(-4, 3, 0), new Vector3(4, -3, 0) })
                { var v = camera.WorldToViewportPoint(corner); Check(safe.Contains(new Vector2(v.x, v.y)), "Maximum board inside safe viewport"); }
            }
        }
        private static void Set(UnityEngine.Object target, string field, object value)
        { target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value); }
        private static async Task Until(Func<bool> condition)
        {
            float end = Time.realtimeSinceStartup + 8;
            while (!condition()) { if (Time.realtimeSinceStartup > end) throw new TimeoutException("Verification wait"); await Task.Delay(30); }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Down(VisualElement target, Vector2 point)
        { using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = point, button = 0 })) { evt.target = target; target.SendEvent(evt); } }
        private static void Move(VisualElement target, Vector2 point)
        { using (var evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, mousePosition = point, button = 0 })) { evt.target = target; target.SendEvent(evt); } }
        private static void Up(VisualElement target, Vector2 point)
        { using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 })) { evt.target = target; target.SendEvent(evt); } }
        private static async Task CleanupAsync()
        {
            _cancel?.Cancel();
            if (_run != null) { try { await _run; } catch (OperationCanceledException) { } }
            if (_host != null) { await _host.ShutdownAsync(); UnityEngine.Object.Destroy(_host.gameObject); _host = null; }
            _session?.Dispose(); _session = null;
            if (Root != null) UnityEngine.Object.Destroy(Root);
            _cancel?.Dispose(); _cancel = null; _run = null;
            Application.runInBackground = _background;
        }
        private sealed class Lobby : IStageLobby
        { public Task ReturnAsync(StageRunResult result, CancellationToken token) => Task.CompletedTask; }
    }
}
