using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace OZGL2.Stage.Editor
{
    public static class StageBoundaryVerification
    {
        public static string LastResult { get; private set; } = "Not run";
        [MenuItem("OZGL2/Stage/Verify Lifecycle Boundaries (Play)")]
        public static async void StartChecks()
        {
            LastResult = "Running";
            try { await Verify(); LastResult = "PASS: external run/round IDs, reward-augment-save gating, final/defeat/cancel shutdown, lobby self-wait rejected, deferred cleanup and lobby failure."; }
            catch (Exception exception) { LastResult = "FAIL: " + exception; }
        }
        private static async Task Verify()
        {
            Require(Application.isPlaying, "Play mode required");
            var fixture = new Fixture { HoldRewards = true };
            var manager = fixture.CreateManager();
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var run = manager.RunAsync(fixture, timeout.Token);
                await Until(() => fixture.GeneralCalls == 1);
                Require(fixture.Preparations == 1 && manager.State == eStageState.GENERAL_REWARD, "General reward gates next preparation");
                fixture.General.TrySetResult(true);
                await Until(() => fixture.AugmentCalls == 1);
                Require(fixture.Preparations == 1 && manager.State == eStageState.AUGMENT, "Augment gates next preparation");
                fixture.Augment.TrySetResult(true); await Bounded(run);
                Require(manager.State == eStageState.CLEARED && fixture.Preparations == 2 && fixture.EndCalls == 1 && fixture.GeneralCalls == 1,
                    "Final round ends grid once and skips final reward");
            }
            fixture = new Fixture { Defeat = true }; manager = fixture.CreateManager();
            await manager.RunAsync(fixture, CancellationToken.None);
            Require(manager.State == eStageState.FAILED && fixture.EndCalls == 1 && fixture.GeneralCalls == 0, "Defeat has no next preparation or reward");
            fixture = new Fixture { HoldPreparation = true }; manager = fixture.CreateManager();
            using (var cancel = new CancellationTokenSource())
            {
                var run = manager.RunAsync(fixture, cancel.Token); cancel.Cancel();
                try { await Bounded(run); throw new Exception("Cancellation not observed"); }
                catch (OperationCanceledException) { }
                Require(manager.State == eStageState.CANCELLED && fixture.EndCalls == 1 && fixture.LobbyCalls == 0, "Cancellation terminates grid without a lobby reward");
            }
            for (int mode = 0; mode < 3; mode++)
            {
                fixture = new Fixture { LobbyMode = mode };
                var host = new GameObject("Stage boundary host").AddComponent<StageRunHost>();
                fixture.Host = host; manager = fixture.CreateManager();
                var resource = new ResourceProbe(manager); host.OwnResource(resource);
                try
                {
                    Require(host.StartRun(manager, fixture), "Host starts");
                    await Bounded(host.CurrentRun);
                    if (mode == 0) Require(manager.State == eStageState.ERROR && host.Error.Contains("Cannot await"), "Self-await rejected instead of hanging");
                    else Require(manager.State == (mode == 1 ? eStageState.CLEARED : eStageState.ERROR), "Deferred shutdown preserves result or lobby failure");
                    await Bounded(host.ShutdownAsync());
                    Require(resource.Count == 1 && host.IsShutdownComplete, "Cleanup after run exactly once");
                    Require(ReferenceEquals(host.ShutdownAsync(), host.ShutdownAsync()), "Repeated shutdown shares completion");
                }
                finally { UnityEngine.Object.Destroy(host.gameObject); }
            }
        }
        private static async Task Until(Func<bool> condition)
        {
            var limit = DateTime.UtcNow.AddSeconds(5);
            while (!condition()) { if (DateTime.UtcNow > limit) throw new TimeoutException(); await Task.Yield(); }
        }
        private static async Task Bounded(Task task)
        {
            if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))) != task) throw new TimeoutException("Lifecycle wait did not complete");
            await task;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private sealed class ResourceProbe : IDisposable
        {
            private readonly StageManager _manager;
            public int Count { get; private set; }
            public ResourceProbe(StageManager manager) { _manager = manager; }
            public void Dispose() { Require(!_manager.IsRunning, "Do not dispose while run is active"); Count++; }
        }
        private sealed class Fixture : IStageDataSource, IStageBattle, IStagePreparation, IStageSession, IStageRewards, IStageProgressStore, IStageLobby
        {
            public bool HoldRewards, HoldPreparation, Defeat;
            public int LobbyMode = -1;
            public StageRunHost Host;
            public int Preparations, GeneralCalls, AugmentCalls, EndCalls, LobbyCalls;
            public readonly TaskCompletionSource<bool> General = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> Augment = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private StageRunContext _context;
            private StageRunResult _saved;
            public StageManager CreateManager() => new StageManager(this, this, this, this, this, this);
            public StageDefinition CreateSnapshot() => new StageDefinition("stage_boundary", new[] {
                new RoundDefinition("round_one", new[] { new HeroSpawnDefinition("hero", 1, 0) }, true, "reward", new AugmentTierWeights(1, 0, 0)),
                new RoundDefinition("round_two", new[] { new HeroSpawnDefinition("hero", 1, 0) }, false, "reward", new AugmentTierWeights(1, 0, 0)) });
            public Task BeginAsync(StageRunContext context, CancellationToken token) { _context = context; return Task.CompletedTask; }
            public Task PrepareAsync(StagePreparationRequest request, CancellationToken token)
            {
                Require(EndCalls == 0 && request.RunId == _context.RunId && request.RoundNumber == Preparations + 1, "Preparation IDs match stage owner");
                Require(request.CanSkip == (Preparations > 0), "Skip policy supplied by stage");
                if (Preparations > 0) Require(_saved.Rounds[0].GeneralReward == eRewardStatus.APPLIED && _saved.Rounds[0].Augment == eRewardStatus.APPLIED,
                    "Both reward receipts saved before next preparation");
                Preparations++;
                return HoldPreparation ? Task.Delay(Timeout.Infinite, token) : Task.CompletedTask;
            }
            public void EndRun(string runId) { Require(runId == _context.RunId, "End uses original run ID"); EndCalls++; }
            public Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken token)
                => Task.FromResult(new RoundResult(Defeat ? eBattleResult.DEFEAT : eBattleResult.VICTORY, 1));
            public Task SelectGeneralRewardAsync(RewardRequest request, string id, CancellationToken token)
            {
                Require(request.RunId == _context.RunId && request.RoundNumber == 1, "Reward carries original run/round identity");
                GeneralCalls++; return HoldRewards ? General.Task : Task.CompletedTask;
            }
            public Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken token)
            { AugmentCalls++; return HoldRewards ? Augment.Task : Task.CompletedTask; }
            public Task SettleAsync(StageRunResult result, CancellationToken token)
            { Require(EndCalls == 1, "Grid ended before settlement and lobby"); return Task.CompletedTask; }
            public Task SaveAsync(StageRunResult result, CancellationToken token) { _saved = result; return Task.CompletedTask; }
            public async Task ReturnAsync(StageRunResult result, CancellationToken token)
            {
                LobbyCalls++; Require(_saved.IsSettled && EndCalls == 1, "Saved settlement and grid shutdown precede lobby");
                if (LobbyMode == 0) await Host.ShutdownAsync();
                else if (LobbyMode > 0)
                {
                    Host.RequestShutdownAfterRun();
                    if (LobbyMode == 2) throw new InvalidOperationException("Injected lobby failure");
                }
            }
        }
    }
}
