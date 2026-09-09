using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Stage.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.Stage.Editor
{
    public static class StageEnhancementVerification
    {
        public static string LastResult { get; private set; } = "Not run";
        public static string LastPlayResult { get; private set; } = "Not run";
        public static long WarmPoolAllocatedBytes { get; private set; }
        [MenuItem("OZGL2/Stage/Verify Progress And Persistence")]
        public static async void StartChecks()
        {
            LastResult = "Running";
            try
            {
                await VerifyAsync();
                LastResult = "PASS: round rules, snapshots, disk round-trip/replacement, defeat EXP, save failure gate, receipt replay, retry, original flow.";
            }
            catch (Exception exception) { LastResult = "FAIL: " + exception; }
        }
        public static async Task VerifyAsync()
        {
            Require(RoundCompletionEvaluator.Evaluate(new BattleProgress(false, 0, 1)) == eRoundOutcome.ONGOING, "No early victory");
            Require(RoundCompletionEvaluator.Evaluate(new BattleProgress(true, 0, 1)) == eRoundOutcome.VICTORY, "Victory");
            Require(RoundCompletionEvaluator.Evaluate(new BattleProgress(false, 1, 0)) == eRoundOutcome.DEFEAT, "Defeat");
            Require(RoundCompletionEvaluator.Evaluate(new BattleProgress(true, 0, 0)) == eRoundOutcome.SIMULTANEOUS, "Unresolved simultaneous");
            var source = new TestSource(3);
            var progress = new StageRunProgress(source.CreateSnapshot());
            progress.BeginRound(1);
            progress.RecordRound(1, new RoundResult(eBattleResult.VICTORY, 12));
            var first = progress.CreateSnapshot();
            bool isRejected = false;
            try { progress.RecordRound(1, new RoundResult(eBattleResult.VICTORY, 12)); }
            catch (InvalidOperationException) { isRejected = true; }
            Require(isRejected, "Duplicate round rejected");
            progress.MarkRewardApplied(eRewardKind.GENERAL);
            Require(first.Rounds[0].GeneralReward == eRewardStatus.PENDING, "Snapshot must not mutate");

            string directory = Path.GetFullPath(Path.Combine("Library", "StageVerification", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            var disk = new FileStageProgressStore(directory);
            await disk.SaveAsync(first, CancellationToken.None);
            await disk.SaveAsync(progress.CreateSnapshot(), CancellationToken.None);
            var loaded = disk.Load(first.RunId);
            Require(loaded.ClearedRoundCount == 1 && loaded.EarnedExperience == 12 &&
                loaded.Rounds[0].GeneralReward == eRewardStatus.APPLIED, "Disk replacement round-trip");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                try { await disk.SaveAsync(first, cancelled.Token); throw new Exception("Cancelled write accepted"); }
                catch (OperationCanceledException) { }
            }
            Require(disk.Load(first.RunId).Rounds[0].GeneralReward == eRewardStatus.APPLIED, "Cancelled write preserves prior file");
            Require(Directory.GetFiles(directory, "*.tmp").Length == 0, "No temporary file leak");

            string ledgerPath = Path.Combine(directory, "receipts.json");
            var ledger = new DummyRewardLedger(ledgerPath);
            string key = new RewardRequest(first.RunId, 1, eRewardKind.GENERAL).RequestId;
            Require(await ledger.ApplyAsync(key, CancellationToken.None), "First receipt");
            Require(!await ledger.ApplyAsync(key, CancellationToken.None), "Duplicate receipt");
            var reloaded = new DummyRewardLedger(ledgerPath);
            Require(!await reloaded.ApplyAsync(key, CancellationToken.None), "Receipt survives reload");

            var services = new TestServices();
            var history = new RecordingStore();
            var flow = new StageManager(services, services, services, services, history, services);
            await flow.RunAsync(source, CancellationToken.None);
            Require(flow.State == eStageState.FAILED && flow.ClearedRoundCount == 1, "Defeat round count");
            Require(services.Final.EarnedExperience == 19 && services.Final.CurrentRoundNumber == 2, "Defeat EXP included");
            Require(history.Snapshots.Exists(s => s.ClearedRoundCount == 1 && s.Rounds[0].GeneralReward == eRewardStatus.PENDING), "Save clear before reward");
            Require(history.Snapshots.Exists(s => s.ClearedRoundCount == 1 && s.Rounds[0].GeneralReward == eRewardStatus.APPLIED), "Save applied reward");
            Require(history.Snapshots[history.Snapshots.Count - 1].IsSettled, "Settlement completion saved");
            string oldRunId = flow.Progress.RunId;
            await flow.RunAsync(source, CancellationToken.None);
            Require(flow.Progress.RunId != oldRunId && flow.Progress.EarnedExperience == 19, "Retry fresh run");

            services = new TestServices();
            var failure = new FailOnClearStore();
            flow = new StageManager(services, services, services, services, failure, services);
            try { await flow.RunAsync(source, CancellationToken.None); throw new Exception("Save failure swallowed"); }
            catch (IOException) { }
            Require(flow.State == eStageState.ERROR && services.Rewards == 0 && services.Battles == 1 && services.Final == null,
                "Save failure must block rewards, next battle and settlement");
            Require(flow.PersistenceError is IOException, "Secondary persistence failure surfaced");
            await StageFlowVerification.VerifyAsync();
        }

        [MenuItem("OZGL2/Stage/Verify Pool And Scene Lifetime (Play)")]
        public static async void StartPlayChecks()
        {
            LastPlayResult = "Running";
            try
            {
                await VerifyPlayAsync();
                LastPlayResult = "PASS: pool reuse/stale/duplicate return, battle EXP, spawn gate, defender revival, cancellation cleanup, scene unload lifetime.";
            }
            catch (Exception exception) { LastPlayResult = "FAIL: " + exception; }
        }
        private static async Task VerifyPlayAsync()
        {
            Require(Application.isPlaying, "Play mode required");
            var root = new GameObject("Stage verification pool");
            var template = new GameObject("Stage verification hero");
            template.SetActive(false);
            var prefab = template.AddComponent<PooledHero>();
            template.AddComponent<PoolResetProbe>();
            HeroPool pool = null;
            try
            {
                pool = new HeroPool(new[] { new HeroPoolEntry("test_hero", prefab, 2, 2, 7) }, root.transform);
                int deaths = 0;
                Action<HeroLease> handler = lease => deaths++;
                Action<HeroLease> returnHandler = lease => pool.Return(lease);
                var first = pool.Rent("test_hero", Vector3.zero, handler, returnHandler);
                first.Hero.gameObject.SetActive(true);
                Require(first.Hero.TryReportDeath(first.LeaseId), "First death");
                Require(!first.Hero.TryReportDeath(first.LeaseId) && deaths == 1, "Duplicate death rejected");
                var second = pool.Rent("test_hero", Vector3.one, handler, returnHandler);
                Require(second.Hero == first.Hero && second.LeaseId != first.LeaseId, "Physical object reused");
                Require(!second.Hero.TryReportDeath(first.LeaseId) && !pool.Return(first), "Stale lease rejected");
                Require(pool.Return(second) && !pool.Return(second) && deaths == 1, "Cleanup is not death");
                var probe = second.Hero.GetComponent<PoolResetProbe>();
                Require(probe.CurrentLease == 0 && probe.SpawnCount == 2 && probe.ReturnCount == 2, "Reuse reset hooks");
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int index = 0; index < 1000; index++) pool.Return(pool.Rent("test_hero", Vector3.zero, handler, returnHandler));
                WarmPoolAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(pool.CreatedCount == 2, "Warm pool performs no additional creation");

                var defenders = new DummyDefenders(2);
                var battle = new PooledStageBattle(pool, defenders, Vector3.zero);
                var round = new RoundDefinition("round_test", new[] { new HeroSpawnDefinition("test_hero", 2, 0) }, false,
                    "reward_test", new AugmentTierWeights(1, 0, 0));
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var run = battle.RunRoundAsync(round, timeout.Token);
                    Require(battle.DefeatOneHero() && battle.DefeatOneHero(), "Kill both heroes");
                    var result = await run;
                    Require(result.Outcome == eBattleResult.VICTORY && result.EarnedExperience == 14 && pool.ActiveCount == 0, "Pooled victory and EXP");
                    defenders.DefeatOne();
                    run = battle.RunRoundAsync(round, timeout.Token);
                    Require(defenders.AliveCount == 2, "Defenders revived each round");
                    defenders.DefeatOne(); defenders.DefeatOne();
                    result = await run;
                    Require(result.Outcome == eBattleResult.DEFEAT && result.EarnedExperience == 0 && pool.ActiveCount == 0,
                        "Defeat cleanup grants no EXP");
                }
                using (var cancellation = new CancellationTokenSource())
                {
                    var delayed = new RoundDefinition("round_delayed", new[] { new HeroSpawnDefinition("test_hero", 2, 1) }, false,
                        "reward_test", new AugmentTierWeights(1, 0, 0));
                    var run = battle.RunRoundAsync(delayed, cancellation.Token);
                    battle.DefeatOneHero();
                    await Task.Yield();
                    Require(!run.IsCompleted && !battle.IsSpawningComplete, "Scheduled hero blocks early clear");
                    cancellation.Cancel();
                    try { await run; throw new Exception("Cancellation ignored"); }
                    catch (OperationCanceledException) { }
                    Require(pool.ActiveCount == 0, "Cancel returns all leases");
                }
            }
            finally { pool?.Dispose(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(template); }

            var scene = SceneManager.CreateScene("stage_lifetime_" + Guid.NewGuid().ToString("N"));
            var view = new GameObject("Temporary preparation view");
            SceneManager.MoveGameObjectToScene(view, scene);
            var host = new GameObject("Stage verification host").AddComponent<StageRunHost>();
            var manual = new ManualStageServices();
            var manager = new StageManager(manual, manual, manual, manual, new MemoryStageProgressStore(), manual);
            try
            {
                host.StartRun(manager, new TestSource(2));
                view.SetActive(false);
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (!unload.isDone) await Task.Yield();
                Require(host != null && manager.IsRunning && manager.State == eStageState.PREPARATION, "Host survives scene unload");
                host.CancelRun();
                await host.CurrentRun;
                Require(manager.State == eStageState.CANCELLED, "Host cancellation");
            }
            finally { UnityEngine.Object.Destroy(host.gameObject); }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private sealed class TestSource : IStageDataSource
        {
            private readonly int _count;
            internal TestSource(int count) { _count = count; }
            public StageDefinition CreateSnapshot()
            {
                var rounds = new List<RoundDefinition>();
                for (int number = 1; number <= _count; number++)
                    rounds.Add(new RoundDefinition("round_" + number, new[] { new HeroSpawnDefinition("test_hero", 1, 0) }, false,
                        "reward_test", new AugmentTierWeights(1, 0, 0)));
                return new StageDefinition("stage_test", rounds);
            }
        }
        private sealed class RecordingStore : IStageProgressStore
        {
            internal readonly List<StageRunResult> Snapshots = new List<StageRunResult>();
            public Task SaveAsync(StageRunResult snapshot, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Snapshots.Add(snapshot); return Task.CompletedTask; }
        }
        private sealed class FailOnClearStore : IStageProgressStore
        {
            public Task SaveAsync(StageRunResult snapshot, CancellationToken token)
                => snapshot.ClearedRoundCount > 0 ? Task.FromException(new IOException("Expected save failure")) : Task.CompletedTask;
        }
        private sealed class TestServices : IStageBattle, IStagePreparation, IStageRewards, IStageSession, IStageLobby
        {
            public Task ReturnAsync(StageRunResult result, CancellationToken token) => Task.CompletedTask;
            internal int Battles;
            internal int Rewards;
            internal StageRunResult Final;
            public Task BeginAsync(string id, CancellationToken token) { Battles = 0; Rewards = 0; Final = null; return Task.CompletedTask; }
            public Task PrepareAsync(bool canSkip, CancellationToken token) => Task.CompletedTask;
            public Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken token)
            { Battles++; return Task.FromResult(new RoundResult(Battles == 1 ? eBattleResult.VICTORY : eBattleResult.DEFEAT, Battles == 1 ? 12 : 7)); }
            public Task SelectGeneralRewardAsync(RewardRequest request, string id, CancellationToken token) { Rewards++; return Task.CompletedTask; }
            public Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken token) => Task.CompletedTask;
            public Task SettleAsync(StageRunResult result, CancellationToken token) { Final = result; return Task.CompletedTask; }
        }
    }
}

