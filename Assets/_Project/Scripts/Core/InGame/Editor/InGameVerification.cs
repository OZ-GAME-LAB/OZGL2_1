using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Stage;
using OZGL2.Stage.Prototype;
using UnityEditor;
using UnityEngine;

namespace OZGL2.InGame.Editor
{
    public static class InGameVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/InGame/Verify Integration")]
        public static async void Run()
        {
            Result = "Running";
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
                config.Validate();
                await VerifyRun(config, false);
                await VerifyRun(config, true);
                await VerifyExpansionAndCancellation(config);
                Result = "PASS: complete stage, defeat, initial placement, reward overflow/cancel/discard, stale requests, forced expansion, cancellation, fresh retry";
            }
            catch (Exception exception) { Result = "FAIL: " + exception; }
        }
        private static async Task VerifyRun(InGamePrototypeConfigSO config, bool defeat)
        {
            var session = new InGameGridSession(config.Catalog.CreateDefinition(), new DummyRewardLedger());
            var preparation = new StageGridPreparation(session, config.Catalog.CreateInitialUnit(), config.Catalog.CreateInitialBlock(), config.InitialAnchor);
            var dummy = new ManualStageServices();
            var rewards = new StageGridRewards(session, config.CreateRewardSource(), dummy);
            var store = new MemoryStageProgressStore();
            var lobby = new TestLobby();
            var stage = new StageManager(dummy, rewards, preparation, session, store, lobby);
            using (var token = new CancellationTokenSource(TimeSpan.FromSeconds(40)))
            {
                var task = stage.RunAsync(config.Stage, token.Token);
                try
                {
                    await Until(() => dummy.PendingRequest == eDummyRequest.BATTLE, token.Token);
                    Check(session.Session.Grid.PlacedCount == 1 && session.Session.Grid.StoredCount == 0 && session.Session.Deployment != null, "First battle automatic deployment");
                    Check(!session.Session.CanSkipPreparation, "First preparation must not remain open");
                    int total = stage.TotalRounds;
                    int expectedAugments = config.Stage.CreateSnapshot().Rounds.Take(total - 1).Count(round => round.IsBossRound);
                    for (int round = 1; round <= (defeat ? 1 : total); round++)
                    {
                        await Until(() => dummy.PendingRequest == eDummyRequest.BATTLE, token.Token);
                        Check(stage.CurrentRoundNumber == round, "Round sequence");
                        Check(dummy.CompleteBattle(dummy.RequestId, defeat ? eBattleResult.DEFEAT : eBattleResult.VICTORY), "Complete battle");
                        if (defeat || round == total) break;
                        await Until(() => rewards.Pending != null, token.Token);
                        string id = rewards.Pending.RequestId;
                        Check(!rewards.TryChooseUnit("old_request", 0), "Reject stale reward");
                        rewards.TryChooseUnit(id, 0);
                        if (session.Session.Grid.HasPendingStorage)
                        {
                            Check(stage.CurrentRoundNumber == round && stage.State == eStageState.GENERAL_REWARD, "Do not advance before inventory grant");
                            var pending = session.Session.Grid.PendingStorage;
                            Check(session.Session.TryCancelStorage(session.Session.RunId, pending.RequestId), "Cancel inventory choice");
                            Check(rewards.Pending != null && session.Session.PendingRewardId == id, "Cancelled reward stays selectable");
                            rewards.TryChooseUnit(id, 1);
                            pending = session.Session.Grid.PendingStorage;
                            Check(session.Session.TryConfirmStorage(session.Session.RunId, pending.RequestId,
                                pending.DiscardCandidates.Take(pending.RequiredDiscardCount).ToArray()), "Discard and receive atomically");
                        }
                        await Until(() => dummy.PendingRequest == eDummyRequest.AUGMENT || session.Session.Grid.Phase == eGridPhase.PREPARATION, token.Token);
                        if (dummy.PendingRequest == eDummyRequest.AUGMENT)
                        {
                            Check(dummy.CompleteSelection(dummy.RequestId), "Augment confirmation");
                            await Until(() => session.Session.Grid.Phase == eGridPhase.PREPARATION, token.Token);
                        }
                        Check(session.Session.Grid.StoredCount <= session.Session.Grid.Definition.StorageCapacity, "Storage capacity");
                        Check(session.Session.TryBeginBattle(session.Session.RunId, round + 1, true), "Skip permitted next preparation");
                    }
                    await task;
                    Check(stage.State == (defeat ? eStageState.FAILED : eStageState.CLEARED), "Terminal state");
                    Check(lobby.Count == 1 && store.LastSnapshot.IsSettled && session.Session.IsEnded, "Settlement, lobby and cleanup");
                    Check(dummy.AugmentCount == (defeat ? 0 : expectedAugments), "No terminal-round augmentation");
                    Check(rewards.Pending == null && stage.NotificationErrorCount == 0, "No stale requests or observer failures");
                }
                finally { token.Cancel(); try { await task; } catch (OperationCanceledException) { } session.Dispose(); }
            }
        }
        private static async Task VerifyExpansionAndCancellation(InGamePrototypeConfigSO config)
        {
            var session = new InGameGridSession(config.Catalog.CreateDefinition(), new DummyRewardLedger());
            var preparation = new StageGridPreparation(session, config.Catalog.CreateInitialUnit(), config.Catalog.CreateInitialBlock(), config.InitialAnchor);
            var dummy = new ManualStageServices();
            var rewards = new StageGridRewards(session, config.CreateRewardSource(), dummy);
            var stage = new StageManager(dummy, rewards, preparation, session, new MemoryStageProgressStore(), new TestLobby());
            using (var token = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                var task = stage.RunAsync(config.Stage, token.Token);
                try
                {
                    await Until(() => dummy.PendingRequest == eDummyRequest.BATTLE, token.Token);
                    dummy.CompleteBattle(dummy.RequestId, eBattleResult.VICTORY);
                    await Until(() => rewards.Pending != null, token.Token);
                    Check(rewards.TryChooseExpansion(rewards.Pending.RequestId), "Expansion reward");
                    await Until(() => session.Session.Grid.Phase == eGridPhase.PREPARATION, token.Token);
                    Check(!session.Session.TryBeginBattle(session.Session.RunId, 2, true), "Forced expansion blocks skip");
                    var grid = session.Session.Grid;
                    Check(grid.BeginExpansionDrag(), "Expansion drag");
                    bool placed = false;
                    foreach (var cell in grid.GetExpansionFrontier().ToArray())
                    {
                        for (int turn = 0; turn < 4; turn++)
                        {
                            grid.MovePreview(cell);
                            if (grid.CommitPreview()) { placed = true; break; }
                            grid.RotatePreview();
                        }
                        if (placed) break;
                    }
                    Check(placed && session.Session.TryBeginBattle(session.Session.RunId, 2), "Start after forced expansion");
                    token.Cancel();
                    try { await task; } catch (OperationCanceledException) { }
                    Check(stage.State == eStageState.CANCELLED && session.Session.IsEnded, "Cancellation cleanup");
                    // 같은 구성의 새 실행은 기존 배치나 보관 아이템을 재사용하지 않는다.
                    using (var fresh = new GridRunSession("fresh_retry", config.Catalog.CreateDefinition()))
                    {
                        StageGridPreparation.PlaceInitial(fresh, config.Catalog.CreateInitialUnit(), config.Catalog.CreateInitialBlock(), config.InitialAnchor);
                        Check(fresh.NextRound == 1 && fresh.Grid.StoredCount == 0 && fresh.Grid.PlacedCount == 1, "Fresh retry");
                    }
                }
                finally { token.Cancel(); try { await task; } catch (OperationCanceledException) { } session.Dispose(); }
            }
        }
        private static async Task Until(Func<bool> condition, CancellationToken token)
        { while (!condition()) { token.ThrowIfCancellationRequested(); await Task.Delay(1, token); } }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private sealed class TestLobby : IStageLobby
        {
            public int Count { get; private set; }
            public Task ReturnAsync(StageRunResult result, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Check(result.IsSettled, "Unsettled lobby entry"); Count++; return Task.CompletedTask; }
        }
    }
}
