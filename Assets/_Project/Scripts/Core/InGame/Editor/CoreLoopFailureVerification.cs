using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Stage;
using OZGL2.Stage.Prototype;
using UnityEditor;
using UnityEngine;

namespace OZGL2.InGame.Editor
{
    public static class CoreLoopFailureVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/InGame/Verify Core Failures (Empty Play Scene)")]
        public static async void Run()
        {
            Result = "Running";
            bool previousBackground = Application.runInBackground;
            try
            {
                Check(Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<InGamePrototypeBootstrap>() == null, "Use empty Play scene");
                Application.runInBackground = true;
                await VerifyBootstrap();
                await VerifyRetryLocks();
                await VerifyFactory();
                await VerifySavedResult(eBattleResult.VICTORY);
                await VerifySavedResult(eBattleResult.DEFEAT);
                await VerifyTimeouts();
                Result = "PASS: UI failure isolation, initial-error retry, cleanup/lobby retry locks, assembly rollback, invalid configuration, confirmed win/defeat disk persistence without rewards, prepare cancellation and cleanup timeout quarantine.";
            }
            catch (Exception error) { Result = "FAIL: " + error; }
            finally { Application.runInBackground = previousBackground; }
        }

        private static async Task VerifyBootstrap()
        {
            var root = new GameObject("BootstrapFailureCheck"); root.SetActive(false);
            var bootstrap = root.AddComponent<InGamePrototypeBootstrap>();
            int deliveries = 0;
            bootstrap.Changed += () => throw new InvalidOperationException("Injected UI failure");
            bootstrap.Changed += () => deliveries++;
            try
            {
                bootstrap.StartPrototype();
                await bootstrap.Completion;
                Check(bootstrap.Stage == null && bootstrap.Error != null && bootstrap.CanRetry, "Retry available before Stage exists");
                Check(deliveries == 1 && bootstrap.NotificationErrorCount == 1 && bootstrap.LastNotificationError != null, "Later UI subscriber still called");
                bootstrap.StartPrototype(); await bootstrap.Completion;
                Check(deliveries == 2 && bootstrap.CanRetry, "Repeat initialization failure is recoverable");
            }
            finally { UnityEngine.Object.Destroy(root); }
        }

        private static async Task VerifyFactory()
        {
            var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
            config.Validate();
            var invalid = UnityEngine.Object.Instantiate(config);
            var catalog = UnityEngine.Object.Instantiate(config.HeroPoolCatalog);
            var root = new GameObject("FactoryFailureCheck");
            try
            {
                var data = new SerializedObject(invalid);
                data.FindProperty("_cellWorldSize").floatValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                try { invalid.Validate(); throw new Exception("Invalid cell size accepted"); }
                catch (InvalidOperationException) { }
                var poolData = new SerializedObject(catalog);
                var entries = poolData.FindProperty("_entries");
                for (int i = 0; i < entries.arraySize; i++) entries.GetArrayElementAtIndex(i).FindPropertyRelative("_initialCapacity").intValue = 1;
                poolData.ApplyModifiedPropertiesWithoutUndo();
                HeroPool pool = null;
                try
                {
                    RealStageBattleFactory.Create(catalog, root.transform, Vector3.zero, () => null,
                        config.DemonArmyCatalog, root.transform, Vector3.zero, 1, out pool,
                        _ => { Check(root.transform.childCount > 0, "Pool was allocated before injected failure"); throw new InvalidOperationException("Injected ownership failure"); });
                    throw new Exception("Expected assembly failure");
                }
                catch (InvalidOperationException) { }
                Check(pool == null, "Failed assembly never publishes pool");
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (root.transform.childCount != 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
                Check(root.transform.childCount == 0, "Allocated pool objects destroyed on rollback");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(invalid);
                UnityEngine.Object.Destroy(catalog);
            }
        }

        private static async Task VerifyRetryLocks()
        {
            var root = new GameObject("RetryLockCheck"); root.SetActive(false);
            var bootstrap = root.AddComponent<InGamePrototypeBootstrap>();
            try
            {
                var connection = new InGameCombatConnection(new[] { new FailedCleanupParticipant() },
                    () => new InGameCombatContext("retry_check", 1, Vector3.zero));
                await connection.BeginRoundAsync(CancellationToken.None);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(InGamePrototypeBootstrap).GetField("_combatConnection", flags).SetValue(bootstrap, connection);
                bootstrap.StartPrototype(); await bootstrap.Completion;
                Check(!bootstrap.CanRetry && bootstrap.RetryBlockedReason.Contains("Cleanup"), "Cleanup failure locks retry after work completes");
                var completion = bootstrap.Completion;
                bootstrap.StartPrototype();
                Check(ReferenceEquals(completion, bootstrap.Completion), "Direct start cannot bypass cleanup lock");
                var release = (Task)typeof(InGamePrototypeBootstrap).GetMethod("ReleaseAsync", flags).Invoke(bootstrap, null);
                await release;
                Check(!bootstrap.CanRetry, "Empty second cleanup does not erase failed cleanup evidence");

                var lobby = root.AddComponent<InGamePrototypeBootstrap>();
                bool observedLock = false;
                lobby.Changed += () => { observedLock = !lobby.CanRetry; lobby.StartPrototype(); };
                typeof(InGamePrototypeBootstrap).GetMethod("BeginLobbyReturn", flags).Invoke(lobby, null);
                Check(observedLock && !lobby.CanRetry && lobby.Completion == null && lobby.RetryBlockedReason.Contains("Lobby"),
                    "Lobby lock is applied before observers and prevents direct restart");
            }
            finally { UnityEngine.Object.Destroy(root); }
        }

        private sealed class FailedCleanupParticipant : IInGameCombatParticipant
        {
            public void SetCombatEnabled(bool isEnabled) { }
            public Task PrepareAsync(InGameCombatContext context, CancellationToken token) => Task.CompletedTask;
            public Task CleanupAsync(InGameCombatContext context) => Task.FromException(new InvalidOperationException("Injected incomplete cleanup"));
        }

        private static async Task VerifySavedResult(eBattleResult outcome)
        {
            var store = new FileStageProgressStore(Path.Combine("Library", "CoreFailureVerification", Guid.NewGuid().ToString("N")));
            var dummy = new ManualStageServices();
            var stage = new StageManager(new FailedCleanupBattle(outcome), dummy, new Preparation(), dummy, store, dummy);
            try { await stage.RunAsync(new Source(), CancellationToken.None); throw new Exception("Expected cleanup failure"); }
            catch (StageBattleCleanupException) { }
            var saved = store.Load(stage.Progress.RunId);
            Check(stage.State == eStageState.ERROR && saved.Rounds.Count == 1 && saved.Rounds[0].Outcome == outcome, "Confirmed outcome saved once");
            Check(saved.ClearedRoundCount == (outcome == eBattleResult.VICTORY ? 1 : 0) && saved.EarnedExperience == 7, "Clear count and experience preserved");
            Check(!saved.IsSettled && dummy.GeneralRewardCount == 0 && dummy.SettlementCount == 0 && dummy.LobbyReturnCount == 0, "No rewards or lobby after cleanup failure");
        }

        private static async Task VerifyTimeouts()
        {
            var delayed = new Participant { Cleanup = new TaskCompletionSource<bool>() };
            var later = new Participant();
            var connection = new InGameCombatConnection(new[] { delayed, later }, () => new InGameCombatContext("timeout", 1, Vector3.zero), TimeSpan.FromMilliseconds(30));
            await connection.BeginRoundAsync(CancellationToken.None);
            var ending = connection.EndRoundAsync();
            Check(later.CleanupCount == 1, "Later cleanup starts despite pending first cleanup");
            await UntilTimeout(connection);
            Check(!ending.IsCompleted && !delayed.IsEnabled, "Timeout does not release active work");
            delayed.Cleanup.SetResult(true);
            try { await ending; throw new Exception("Expected cleanup timeout"); }
            catch (AggregateException) { }
            Check(ReferenceEquals(ending, connection.EndRoundAsync()) && later.CleanupCount == 1, "Late cleanup not repeated");

            var preparing = new Participant { Prepare = new TaskCompletionSource<bool>() };
            connection = new InGameCombatConnection(new[] { preparing }, () => new InGameCombatContext("timeout", 2, Vector3.zero), TimeSpan.FromMilliseconds(30));
            var beginning = connection.BeginRoundAsync(CancellationToken.None);
            await UntilTimeout(connection);
            Check(preparing.Token.IsCancellationRequested && !beginning.IsCompleted && !preparing.IsEnabled, "Cancel timed-out prepare but keep battle blocked");
            preparing.Prepare.SetResult(true);
            try { await beginning; throw new Exception("Expected prepare timeout"); }
            catch (TimeoutException) { }
            await connection.EndRoundAsync();
            Check(preparing.CleanupCount == 1 && !preparing.IsEnabled, "Timed-out prepare never enables combat");
        }
        private static async Task UntilTimeout(InGameCombatConnection connection)
        {
            var limit = DateTime.UtcNow.AddSeconds(5);
            while (!connection.OperationStatus.StartsWith("TIMEOUT"))
            { if (DateTime.UtcNow > limit) throw new Exception("Timeout not reported"); await Task.Delay(10); }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private sealed class FailedCleanupBattle : IStageBattle
        {
            private readonly eBattleResult _outcome;
            public FailedCleanupBattle(eBattleResult outcome) { _outcome = outcome; }
            public Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken token) =>
                Task.FromException<RoundResult>(new StageBattleCleanupException(new RoundResult(_outcome, 7), new[] { new InvalidOperationException("Injected cleanup error") }));
        }
        private sealed class Preparation : IStagePreparation
        {
            public Task PrepareAsync(StagePreparationRequest request, CancellationToken token) => Task.CompletedTask;
            public void EndRun(string runId) { }
        }
        private sealed class Source : IStageDataSource
        {
            public StageDefinition CreateSnapshot() => new StageDefinition("test_stage", new[] {
                new RoundDefinition("round_one", new[] {new HeroSpawnDefinition("hero", 1, 0)}, false, "reward", new AugmentTierWeights(1,0,0)),
                new RoundDefinition("round_two", new[] {new HeroSpawnDefinition("hero", 1, 0)}, false, "reward", new AugmentTierWeights(1,0,0)) });
        }
        private sealed class Participant : IInGameCombatParticipant
        {
            public TaskCompletionSource<bool> Prepare, Cleanup;
            public CancellationToken Token;
            public bool IsEnabled;
            public int CleanupCount;
            public void SetCombatEnabled(bool isEnabled) => IsEnabled = isEnabled;
            public Task PrepareAsync(InGameCombatContext context, CancellationToken token) { Token = token; return Prepare?.Task ?? Task.CompletedTask; }
            public Task CleanupAsync(InGameCombatContext context) { CleanupCount++; return Cleanup?.Task ?? Task.CompletedTask; }
        }
    }
}
