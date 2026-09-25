using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Stage;
using OZGL2.Stage.Prototype;
using UnityEditor;
using UnityEngine;

namespace OZGL2.InGame.Editor
{
    public static class GeneralRewardVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/InGame/Verify General Rewards")]
        public static async void Run()
        {
            Result = "Running";
            try
            {
                VerifyDraw();
                await VerifyGrant(false, false);
                await VerifyGrant(true, false);
                await VerifyGrant(true, true);
                Result = "PASS: weighted unlock filtering, unique/fallback candidates, no eligible data, third unit, overflow cancellation and atomic grant, stable offers, stale/double/cancelled requests, 1-star bundle";
            }
            catch (Exception exception) { Result = "FAIL: " + exception; }
        }
        private static UnitRewardEntry Entry(string id, float weight, bool unlocked = true)
        {
            var block = new FootprintDefinition("single", "Single", new[] { Vector2Int.zero });
            return new UnitRewardEntry(new UnitDefinition(id, id, block), block, weight, unlocked);
        }
        private static GeneralRewardSource Source(UnitRewardEntry[] entries, int seed = 49)
            => new GeneralRewardSource(entries, new DefaultUnitRewardUnlocks(entries), new System.Random(seed));
        private static void VerifyDraw()
        {
            var entries = new[] { Entry("a", 1), Entry("b", 9), Entry("c", 1), Entry("locked", 100, false), Entry("zero", 0) };
            var source = Source(entries);
            int heavyFirst = 0;
            for (int i = 0; i < 4000; i++)
            {
                var options = source.Draw(false);
                Check(options.Count == 3 && options.Select(option => option.Unit.Id).Distinct().Count() == 3, "Unique candidates");
                Check(options.All(option => option.Unit.Id != "locked" && option.Unit.Id != "zero"), "Unlock and zero weight");
                if (options[0].Unit.Id == "b") heavyFirst++;
            }
            Check(heavyFirst > 3000 && heavyFirst < 3500, "Weighted first draw distribution");
            Check(source.Draw(true)[2].Kind == eGeneralRewardKind.EXPANSION, "Expansion option");
            Check(Source(new[] { Entry("a", 1) }).Draw(false).All(option => option.Unit.Id == "a"), "Single eligible fallback");
            var two = Source(new[] { Entry("a", 1), Entry("b", 1) }).Draw(false);
            Check(two[0].Unit.Id != two[1].Unit.Id && two.Count == 3, "Repeat only after unique pool exhausted");
            bool failed = false;
            try { Source(new[] { Entry("locked", 1, false), Entry("zero", 0) }); }
            catch (InvalidOperationException) { failed = true; }
            Check(failed, "Empty pool must fail");
            failed = false;
            try { Source(new[] { Entry("duplicate", 1), Entry("duplicate", 1) }); }
            catch (ArgumentException) { failed = true; }
            Check(failed, "Duplicate IDs must fail");
        }
        private static async Task VerifyGrant(bool noExpansion, bool cancel)
        {
            var initial = Entry("initial", 1);
            var expansion = new FootprintDefinition("expansion", "Expansion", new[] { Vector2Int.zero, Vector2Int.up });
            var maximum = new Vector2Int(8, 5);
            var definition = new GridDefinition(noExpansion ? maximum : new Vector2Int(4, 3), maximum, expansion);
            using (var session = new InGameGridSession(definition, new DummyRewardLedger()))
            using (var token = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                string runId = Guid.NewGuid().ToString("N");
                await session.BeginAsync(new StageRunContext(runId, "test"), token.Token);
                StageGridPreparation.PlaceInitial(session.Session, initial.Option.Unit, initial.Option.Block, definition.InitialOrigin);
                var grid = session.Session.Grid;
                Check(grid.Phase == eGridPhase.BATTLE, "Initial battle");
                var rewards = new StageGridRewards(session, Source(new[] { Entry("a", 1), Entry("b", 1), Entry("c", 1) }), new ManualStageServices());
                var request = new RewardRequest(runId, 1, eRewardKind.GENERAL);
                var task = rewards.SelectGeneralRewardAsync(request, "test", token.Token);
                try
                {
                    // 전투가 끝난 보상 단계에서 보관함을 채워 두 항목의 동시 지급을 검증한다.
                    for (int i = 0; i < 10; i++) grid.AddUnit("stored_" + i, initial.Option.Unit);
                    Check(rewards.Candidates.Count == 3 && !task.IsCompleted, "Wait for selection");
                    var offered = rewards.Candidates;
                    int index = noExpansion ? 2 : 0;
                    Check(!rewards.TrySelect("stale", index) && !rewards.TrySelect(request.RequestId, 3), "Invalid selection");
                    if (noExpansion) Check(!rewards.TryChooseExpansion(request.RequestId), "Unlisted expansion");
                    if (cancel)
                    {
                        token.Cancel();
                        Check(!rewards.TrySelect(request.RequestId, index), "Cancelled click before continuation");
                        try { await task; } catch (OperationCanceledException) { }
                        Check(rewards.Pending == null && rewards.Candidates.Count == 0 && grid.StoredCount == 10, "Cancel clears offer without grant");
                        return;
                    }
                    Check(!rewards.TrySelect(request.RequestId, index) && grid.HasPendingStorage && !task.IsCompleted, "Wait for space");
                    Check(grid.PendingStorage.RequiredDiscardCount == 2, "Unit and block require two slots");
                    Check(session.Session.TryCancelStorage(runId, grid.PendingStorage.RequestId), "Cancel discard");
                    Check(ReferenceEquals(offered, rewards.Candidates) && grid.StoredCount == 10, "Stable offers after cancel");
                    rewards.TrySelect(request.RequestId, index);
                    var pending = grid.PendingStorage;
                    Check(session.Session.TryConfirmStorage(runId, pending.RequestId, pending.DiscardCandidates.Take(2).ToArray()), "Discard and grant");
                    Check(!rewards.TrySelect(request.RequestId, index), "No double grant before continuation");
                    await task;
                    Check(grid.StoredCount == 10 && grid.Units.Any(unit => unit.Definition.Id == offered[index].Unit.Id && unit.StarLevel == 1), "One-star unit");
                    Check(grid.Blocks.Any(block => !block.IsPlaced && block.ContentId == "single"), "Matching block");
                    await rewards.SelectGeneralRewardAsync(request, "test", token.Token);
                    Check(grid.StoredCount == 10 && rewards.Pending == null, "Same request idempotent");
                }
                finally { token.Cancel(); try { await task; } catch (OperationCanceledException) { } }
            }
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
