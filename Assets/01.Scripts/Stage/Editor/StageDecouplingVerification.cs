using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Stage.Prototype;
using UnityEditor;
using UnityEngine;

namespace OZGL2.Stage.Editor
{
    public static class StageDecouplingVerification
    {
        public static string LastResult { get; private set; } = "Not run";
        public static string LastPlayResult { get; private set; } = "Not run";
        [MenuItem("OZGL2/Stage/Verify Decoupled Flow")]
        public static async void StartChecks()
        {
            LastResult = "Running";
            try
            {
                await VerifyAsync();
                LastResult = "PASS: observer isolation, settle-save-lobby order, independent failure gates, settlement cancellation boundary, existing flow/persistence.";
            }
            catch (Exception exception) { LastResult = "FAIL: " + exception; }
        }
        private static async Task VerifyAsync()
        {
            var fixture = new Fixture();
            var manager = fixture.CreateManager();
            int observed = 0;
            manager.StateChanged += state => throw new InvalidOperationException("Expected UI failure A");
            manager.StateChanged += state => observed++;
            manager.StateChanged += state => throw new InvalidOperationException("Expected UI failure B");
            await manager.RunAsync(fixture, CancellationToken.None);
            Require(manager.State == eStageState.CLEARED && manager.NotificationErrorCount == observed * 2 && observed > 0,
                "Failing UI must not abort stage or other listeners");
            Require(manager.LastNotificationError.Message.Contains("B") && !string.IsNullOrEmpty(manager.LastFailedSubscriber), "Observer diagnostic retained");
            Require(string.Join(",", fixture.Calls) == "save,save,settle,save_settled,lobby", "Settlement order");

            foreach (var target in new[] { eFailureTarget.SETTLEMENT, eFailureTarget.SAVE_SETTLED, eFailureTarget.LOBBY })
            {
                fixture = new Fixture { FailureTarget = target };
                manager = fixture.CreateManager();
                try { await manager.RunAsync(fixture, CancellationToken.None); throw new Exception("Expected failure missing"); }
                catch (IOException) { }
                Require(manager.State == eStageState.ERROR && fixture.SettlementCount == 1, "Single settlement attempt");
                Require(manager.Progress.IsSettled == (target != eFailureTarget.SETTLEMENT), "Accurate completed settlement flag");
                Require(fixture.LobbyCount == (target == eFailureTarget.LOBBY ? 1 : 0), "Failures before lobby block movement");
                if (target == eFailureTarget.SAVE_SETTLED)
                    Require(!fixture.Saved.IsSettled && manager.PersistenceError != null, "Unsaved completion cannot move to lobby");
                if (target == eFailureTarget.LOBBY)
                {
                    Require(fixture.Saved.IsSettled, "Lobby failure preserves durable settlement");
                    fixture.FailureTarget = eFailureTarget.NONE;
                    await fixture.ReturnAsync(manager.Progress, CancellationToken.None);
                    Require(fixture.SettlementCount == 1 && fixture.LobbyCount == 2, "Retrying movement does not call settlement");
                }
            }
            using (var cancellation = new CancellationTokenSource())
            {
                fixture = new Fixture { CancelAfterSettlement = cancellation };
                manager = fixture.CreateManager();
                try { await manager.RunAsync(fixture, cancellation.Token); throw new Exception("Expected cancellation missing"); }
                catch (OperationCanceledException) { }
                Require(manager.State == eStageState.CANCELLED && fixture.Saved.IsSettled && fixture.LobbyCount == 0,
                    "Cancellation after payout must retain settled fact");
            }
            await StageEnhancementVerification.VerifyAsync();
        }

        [MenuItem("OZGL2/Stage/Verify Delayed Hero Return (Play)")]
        public static async void StartPlayChecks()
        {
            LastPlayResult = "Running";
            try
            {
                await VerifyPlayAsync();
                LastPlayResult = "PASS: delayed death presentation, kill accounting before return, premature/duplicate/stale completion rejection, clear/defeat/cancel reclaim dying heroes.";
            }
            catch (Exception exception) { LastPlayResult = "FAIL: " + exception; }
        }
        private static async Task VerifyPlayAsync()
        {
            Require(Application.isPlaying, "Play mode required");
            var root = new GameObject("Delayed death verification");
            var template = new GameObject("Delayed death template");
            template.SetActive(false);
            var prefab = template.AddComponent<PooledHero>();
            template.AddComponent<DeathPresentationProbe>();
            HeroPool pool = null;
            using (var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                Task<RoundResult> run = null;
                try
                {
                    pool = new HeroPool(new[] { new HeroPoolEntry("test_hero", prefab, 2, 1, 7) }, root.transform);
                    var defenders = new DummyDefenders(1);
                    var battle = new PooledStageBattle(pool, defenders, Vector3.zero);
                    var round = new RoundDefinition("round_test", new[] { new HeroSpawnDefinition("test_hero", 2, 0) }, false,
                        "reward_test", new AugmentTierWeights(1, 0, 0));
                    run = battle.RunRoundAsync(round, lifetime.Token);
                    var heroes = root.GetComponentsInChildren<PooledHero>();
                    Require(heroes.Length == 2, "Two live heroes");
                    var dying = heroes[0];
                    long oldLease = dying.LeaseId;
                    Require(!dying.TryCompleteDeath(oldLease), "Cannot return before death");
                    Require(dying.TryReportDeath(oldLease), "Death accepted");
                    Require(battle.AliveHeroCount == 1 && battle.UnreturnedHeroCount == 2 && battle.EarnedExperience == 7 && pool.ActiveCount == 2,
                        "Count kill independently of presentation");
                    Require(dying.gameObject.activeSelf && dying.GetComponent<DeathPresentationProbe>().IsPlaying, "Presentation retains active actor");
                    Require(!dying.TryReportDeath(oldLease), "Duplicate death rejected");
                    Require(dying.TryCompleteDeath(oldLease) && !dying.TryCompleteDeath(oldLease), "Complete presentation once");
                    Require(pool.ActiveCount == 1 && battle.UnreturnedHeroCount == 1 && battle.EarnedExperience == 7, "Return grants no additional EXP");
                    Require(battle.DefeatOneHero(), "Last hero death");
                    var result = await run;
                    Require(result.Outcome == eBattleResult.VICTORY && result.EarnedExperience == 14 && pool.ActiveCount == 0,
                        "Clear reclaims unfinished presentation without waiting");

                    run = battle.RunRoundAsync(round, lifetime.Token);
                    Require(dying.IsLeased && dying.LeaseId != oldLease && !dying.TryCompleteDeath(oldLease), "Old completion cannot return reused hero");
                    Require(battle.DefeatOneHero(), "Kill one before defeat");
                    defenders.DefeatOne();
                    result = await run;
                    Require(result.Outcome == eBattleResult.DEFEAT && result.EarnedExperience == 7 && pool.ActiveCount == 0,
                        "Defeat reclaims alive and dying without extra EXP");

                    run = battle.RunRoundAsync(round, lifetime.Token);
                    battle.DefeatOneHero();
                    var active = root.GetComponentsInChildren<PooledHero>();
                    long cancelledLease = active[0].LeaseId;
                    lifetime.Cancel();
                    try { await run; throw new Exception("Cancellation ignored"); }
                    catch (OperationCanceledException) { }
                    Require(pool.ActiveCount == 0 && battle.UnreturnedHeroCount == 0 && battle.EarnedExperience == 7, "Cancel reclaims both lists");
                    Require(!active[0].TryCompleteDeath(cancelledLease), "Late completion after cancellation rejected");
                }
                finally
                {
                    lifetime.Cancel();
                    try { if (run != null) await run; }
                    catch (OperationCanceledException) { }
                    finally { pool?.Dispose(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(template); }
                }
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private enum eFailureTarget { NONE, SETTLEMENT, SAVE_SETTLED, LOBBY }
        private sealed class Fixture : IStageDataSource, IStageBattle, IStageRewards, IStagePreparation, IStageSession, IStageProgressStore, IStageLobby
        {
            internal eFailureTarget FailureTarget;
            internal CancellationTokenSource CancelAfterSettlement;
            internal readonly List<string> Calls = new List<string>();
            internal int SettlementCount;
            internal int LobbyCount;
            internal StageRunResult Saved;
            internal StageManager CreateManager() => new StageManager(this, this, this, this, this, this);
            public StageDefinition CreateSnapshot() => new StageDefinition("stage_test", new[] {
                new RoundDefinition("round_test", new[] { new HeroSpawnDefinition("test_hero", 1, 0) }, false,
                    "reward_test", new AugmentTierWeights(1, 0, 0)) });
            public void EndRun(string runId) { }
            public Task BeginAsync(StageRunContext context, CancellationToken token) => Task.CompletedTask;
            public Task PrepareAsync(StagePreparationRequest request, CancellationToken token) => Task.CompletedTask;
            public Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken token) => Task.FromResult(new RoundResult(eBattleResult.VICTORY, 3));
            public Task SelectGeneralRewardAsync(RewardRequest request, string id, CancellationToken token) => throw new Exception("No final reward");
            public Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken token) => throw new Exception("No final augment");
            public Task SettleAsync(StageRunResult result, CancellationToken token)
            {
                Calls.Add("settle"); SettlementCount++;
                if (FailureTarget == eFailureTarget.SETTLEMENT) throw new IOException("Expected settlement failure");
                CancelAfterSettlement?.Cancel();
                return Task.CompletedTask;
            }
            public Task SaveAsync(StageRunResult snapshot, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Calls.Add(snapshot.IsSettled ? "save_settled" : "save");
                if (FailureTarget == eFailureTarget.SAVE_SETTLED && snapshot.IsSettled) throw new IOException("Expected final save failure");
                Saved = snapshot; return Task.CompletedTask;
            }
            public Task ReturnAsync(StageRunResult result, CancellationToken token)
            {
                Require(Saved.IsSettled && result.IsSettled, "Must save settlement before lobby");
                Calls.Add("lobby"); LobbyCount++;
                if (FailureTarget == eFailureTarget.LOBBY) throw new IOException("Expected lobby failure");
                return Task.CompletedTask;
            }
        }
    }
}
