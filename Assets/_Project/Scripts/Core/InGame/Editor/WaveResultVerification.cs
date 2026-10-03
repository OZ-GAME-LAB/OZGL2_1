using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Progression;
using OZGL2.Stage;
using UnityEditor;

namespace OZGL2.InGame.Editor
{
    public static class WaveResultVerification
    {
        public static string Result { get; private set; } = "Not run";
        private static readonly List<string> _checks = new List<string>();
        public static string[] Checks => _checks.ToArray();

        [MenuItem("OZGL2/InGame/Verify Wave Results")]
        public static async void Run()
        {
            Result = "Running";
            _checks.Clear();
            try
            {
                await VerifyProgression();
                await VerifyCancellationAndRetry();
                await VerifyExperience();
                Result = "PASS: " + _checks.Count + " wave result checks";
            }
            catch (Exception error) { Result = "FAIL: " + error; }
        }

        private static async Task VerifyProgression()
        {
            var fixture = new Fixture();
            var confirmation = new WaveResultConfirmation();
            var stage = fixture.CreateManager(confirmation);
            using (var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var run = stage.RunAsync(fixture, lifetime.Token);
                await Until(() => confirmation.Pending != null);
                var first = confirmation.Pending;
                Check(stage.State == eStageState.WAVE_RESULT && fixture.GeneralCalls == 0 && fixture.Battles == 1,
                    "Normal wave waits before reward or next battle");
                Check(first.WaveNumber == 1 && first.IsCleared && first.EarnedExperience == 7 &&
                    fixture.Saved.Rounds[0].GeneralReward == eRewardStatus.PENDING,
                    "Confirmed result is saved before display; wave XP is not cumulative");
                Check(!confirmation.TryConfirm("stale") && confirmation.TryConfirm(first.RequestId) &&
                    !confirmation.TryConfirm(first.RequestId), "Stale and double confirmation are rejected");
                await Until(() => confirmation.Pending?.WaveNumber == 2);
                Check(fixture.GeneralCalls == 1 && fixture.AugmentCalls == 0 && confirmation.Pending.EarnedExperience == 14,
                    "Boss result precedes both rewards and reports only its XP");
                Check(!confirmation.TryConfirm(first.RequestId), "Previous-wave click cannot confirm boss result");
                confirmation.TryConfirm(confirmation.Pending.RequestId);
                await run;
                Check(string.Join(",", fixture.Events) == "battle1,general1,battle2,general2,augment2,battle3,settle,final",
                    "Normal then boss rewards preserve general-before-augment order");
                Check(stage.State == eStageState.CLEARED && confirmation.Pending == null &&
                    fixture.GeneralCalls == 2 && fixture.FinalCalls == 1 && stage.Progress.EarnedExperience == 42,
                    "Final boss bypasses wave popup and rewards, final result once");
            }

            fixture = new Fixture { IsDefeat = true };
            stage = fixture.CreateManager(confirmation);
            await stage.RunAsync(fixture, CancellationToken.None);
            Check(stage.State == eStageState.FAILED && confirmation.Pending == null && fixture.GeneralCalls == 0 && fixture.FinalCalls == 1,
                "Defeat goes directly to existing final result");
        }

        private static async Task VerifyCancellationAndRetry()
        {
            var fixture = new Fixture();
            var confirmation = new WaveResultConfirmation();
            var stage = fixture.CreateManager(confirmation);
            string oldId;
            using (var cancellation = new CancellationTokenSource())
            {
                var run = stage.RunAsync(fixture, cancellation.Token);
                await Until(() => confirmation.Pending != null);
                oldId = confirmation.Pending.RequestId;
                cancellation.Cancel();
                Check(!confirmation.TryConfirm(oldId), "Cancelled confirmation cannot advance rewards");
                try { await run; throw new Exception("Expected cancellation"); }
                catch (OperationCanceledException) { }
                Check(stage.State == eStageState.CANCELLED && !stage.IsRunning && confirmation.Pending == null &&
                    fixture.EndCalls == 1 && fixture.FinalCalls == 0 && fixture.GeneralCalls == 0,
                    "Cancellation releases wait and preparation without reward or final popup");
            }
            fixture = new Fixture();
            stage = fixture.CreateManager(confirmation);
            using (var cancellation = new CancellationTokenSource())
            {
                var run = stage.RunAsync(fixture, cancellation.Token);
                await Until(() => confirmation.Pending != null);
                Check(confirmation.Pending.RequestId != oldId && !confirmation.TryConfirm(oldId), "Retry rejects prior-run confirmation");
                cancellation.Cancel();
                try { await run; } catch (OperationCanceledException) { }
            }

            fixture = new Fixture { HasCleanupFailure = true };
            stage = fixture.CreateManager(confirmation);
            try { await stage.RunAsync(fixture, CancellationToken.None); throw new Exception("Expected cleanup failure"); }
            catch (StageBattleCleanupException) { }
            Check(stage.State == eStageState.ERROR && confirmation.Pending == null && fixture.GeneralCalls == 0 &&
                stage.Progress.Rounds.Count == 1, "Cleanup failure preserves outcome and never opens wave result");
        }

        private static async Task VerifyExperience()
        {
            var level = new MawangLevel(false) { XpGainMult = 1.5f };
            var inner = new ExperienceBattle(level);
            var battle = new InGameExperienceBattle(inner, level);
            var round = new Fixture().CreateSnapshot().Rounds[0];
            var result = await battle.RunRoundAsync(round, CancellationToken.None);
            Check(result.EarnedExperience == 90 && level.Level == 2 && level.Xp == 30,
                "Actual multiplied XP survives level-up instead of using remaining-XP delta or pool XP");
            level.AddXp(100);
            result = await battle.RunRoundAsync(round, CancellationToken.None);
            Check(result.EarnedExperience == 90, "Between-wave immediate rewards are excluded from next wave");
            long before = level.TotalEarnedXp;
            var confirmation = new WaveResultConfirmation();
            var wait = confirmation.ShowAsync(new StageWaveResult(Guid.NewGuid().ToString("N"), 1, result), CancellationToken.None);
            confirmation.TryConfirm(confirmation.Pending.RequestId);
            await wait;
            Check(level.TotalEarnedXp == before, "Result confirmation never awards XP again");
            level.ResetForNewRun();
            Check(level.TotalEarnedXp == 0 && level.Level == 1 && level.Xp == 0, "Retry resets earned-XP counter");
            level.SurgeXpOnMilestone = 30;
            level.XpGainMult = 1;
            level.AddXp(1000);
            Check(level.TotalEarnedXp == 1030, "Milestone bonus XP included in actual awards");
            inner.HasCleanupFailure = true;
            try { await battle.RunRoundAsync(round, CancellationToken.None); throw new Exception("Expected cleanup failure"); }
            catch (StageBattleCleanupException error)
            {
                Check(error.ConfirmedResult.EarnedExperience == 60 && error.InnerExceptions.Count == 1,
                    "Cleanup exception retains actual XP and original cause");
            }
        }

        private static async Task Until(Func<bool> condition)
        {
            var end = DateTime.UtcNow.AddSeconds(4);
            while (!condition())
            {
                if (DateTime.UtcNow > end) throw new TimeoutException("Wave result progression timed out.");
                await Task.Yield();
            }
        }
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new Exception(description);
            _checks.Add(description);
        }

        private sealed class ExperienceBattle : IStageBattle
        {
            private readonly MawangLevel _level;
            public bool HasCleanupFailure;
            public ExperienceBattle(MawangLevel level) { _level = level; }
            public Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken token)
            {
                _level.AddXp(40);
                _level.AddXp(20);
                var result = new RoundResult(eBattleResult.VICTORY, 999);
                if (HasCleanupFailure) throw new StageBattleCleanupException(result, new[] { new Exception("cleanup") });
                return Task.FromResult(result);
            }
        }

        private sealed class Fixture : IStageDataSource, IStageBattle, IStagePreparation, IStageSession,
            IStageRewards, IStageProgressStore, IStageLobby
        {
            public bool IsDefeat, HasCleanupFailure;
            public int Battles, GeneralCalls, AugmentCalls, FinalCalls, EndCalls;
            public StageRunResult Saved;
            public readonly List<string> Events = new List<string>();
            public StageManager CreateManager(IStageWaveResults results) => new StageManager(this, this, this, this, this, this, results);
            public StageDefinition CreateSnapshot() => new StageDefinition("wave_result_test", Enumerable.Range(1, 3).Select(i =>
                new RoundDefinition("round_" + i, new[] { new HeroSpawnDefinition("hero", 1, 0) }, i > 1,
                    "reward", new AugmentTierWeights(1, 0, 0))));
            public Task BeginAsync(StageRunContext context, CancellationToken token) => Task.CompletedTask;
            public Task PrepareAsync(StagePreparationRequest request, CancellationToken token) => Task.CompletedTask;
            public void EndRun(string runId) { EndCalls++; }
            public Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken token)
            {
                Events.Add("battle" + ++Battles);
                var result = new RoundResult(IsDefeat ? eBattleResult.DEFEAT : eBattleResult.VICTORY, 7 * Battles);
                if (HasCleanupFailure) throw new StageBattleCleanupException(result, new[] { new Exception("cleanup") });
                return Task.FromResult(result);
            }
            public Task SelectGeneralRewardAsync(RewardRequest request, string rewardId, CancellationToken token)
            { GeneralCalls++; Events.Add("general" + request.RoundNumber); return Task.CompletedTask; }
            public Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken token)
            { AugmentCalls++; Events.Add("augment" + request.RoundNumber); return Task.CompletedTask; }
            public Task SaveAsync(StageRunResult result, CancellationToken token) { Saved = result; return Task.CompletedTask; }
            public Task SettleAsync(StageRunResult result, CancellationToken token) { Events.Add("settle"); return Task.CompletedTask; }
            public Task ReturnAsync(StageRunResult result, CancellationToken token) { FinalCalls++; Events.Add("final"); return Task.CompletedTask; }
        }
    }
}
