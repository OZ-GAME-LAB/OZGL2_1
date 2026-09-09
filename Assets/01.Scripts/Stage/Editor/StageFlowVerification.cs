using System;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Stage.Prototype;
using UnityEditor;

namespace OZGL2.Stage.Editor
{
    /// <summary>Unity 메인 스레드에서 실행하는 더미 계약 통합 검사입니다.</summary>
    public static class StageFlowVerification
    {
        public static string LastResult { get; private set; } = "Not run";

        public static async void StartSceneChecks()
        {
            LastResult = "Running scene checks";
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                Require(UnityEngine.Application.isPlaying && scene.name == "JOB_KIMGUN", "Play in JOB_KIMGUN first");
                StagePrototypeRunner runner = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var candidate = root.GetComponent<StagePrototypeRunner>();
                    if (candidate != null) runner = candidate;
                }
                Require(runner != null, "Missing prototype runner");
                runner.StartNormalStage();
                var initialRun = runner.CurrentRun;
                runner.StartHardStage();
                Require(ReferenceEquals(initialRun, runner.CurrentRun), "Runner duplicate start");
                await DriveAsync(runner.Manager, runner.Services, runner.CurrentRun, 0);
                Require(runner.Manager.State == eStageState.CLEARED && runner.Manager.TotalRounds == 30 && runner.Error == null, "Normal scene run");
                runner.StartHardStage();
                await DriveAsync(runner.Manager, runner.Services, runner.CurrentRun, 0);
                Require(runner.Manager.State == eStageState.CLEARED && runner.Manager.TotalRounds == 50 && runner.Error == null, "Hard scene run");
                runner.StartNormalStage();
                await DriveAsync(runner.Manager, runner.Services, runner.CurrentRun, 3);
                Require(runner.Manager.State == eStageState.FAILED && runner.Manager.ClearedRoundCount == 2, "Scene defeat");
                runner.StartNormalStage();
                runner.CancelStage();
                await runner.CurrentRun;
                Require(runner.Manager.State == eStageState.CANCELLED && runner.Error == null, "Scene cancellation");
                LastResult = "PASS: JOB_KIMGUN Play mode, both assigned SOs, 30R/50R full runs, duplicate start, defeat and cancellation.";
            }
            catch (Exception exception) { LastResult = "FAIL: " + exception; }
        }

        [MenuItem("OZGL2/Stage/Verify Dummy Flow")]
        public static async void StartChecks()
        {
            LastResult = "Running";
            try
            {
                await VerifyAsync();
                LastResult = "PASS: 30R/50R, reward order, final settlement, skip, defeat/retry, duplicate start/result, cancellation, adapter error.";
            }
            catch (Exception exception) { LastResult = "FAIL: " + exception; }
        }

        public static async Task VerifyAsync()
        {
            foreach (string name in new[] { "DummyStageNormal", "DummyStageHard" })
            {
                var data = LoadData(name);
                int count = data.CreateSnapshot().Rounds.Count;
                var services = new ManualStageServices();
                var manager = new StageManager(services, services, services, services, new MemoryStageProgressStore(), services);
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    var run = manager.RunAsync(data, timeout.Token);
                    bool isRejected = false;
                    try { await manager.RunAsync(data, timeout.Token); }
                    catch (InvalidOperationException) { isRejected = true; }
                    Require(isRejected, "Duplicate start must be rejected");
                    await DriveAsync(manager, services, run, 0);
                    Require(manager.State == eStageState.CLEARED && manager.ClearedRoundCount == count, "Full clear");
                    Require(services.GeneralRewardCount == count - 1, "No final general reward");
                    Require(services.AugmentCount == count / 10 - 1, "No final augment");
                    Require(services.SkippedPreparationCount == count - 1, "Every later preparation may be skipped");
                    Require(services.SettlementCount == 1 && services.IsLastStageCleared, "Single clear settlement");
                    Require(services.LobbyReturnCount == 1 && manager.Progress.IsSettled, "Lobby follows saved settlement");
                }
            }

            var normal = LoadData("DummyStageNormal");
            var manual = new ManualStageServices();
            var flow = new StageManager(manual, manual, manual, manual, new MemoryStageProgressStore(), manual);
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                await DriveAsync(flow, manual, flow.RunAsync(normal, timeout.Token), 11);
                Require(flow.State == eStageState.FAILED && flow.ClearedRoundCount == 10, "Defeat count");
                Require(manual.GeneralRewardCount == 10 && manual.AugmentCount == 1, "No reward for defeated round");
                Require(manual.SettlementCount == 1 && !manual.IsLastStageCleared, "Defeat settlement");

                var retry = flow.RunAsync(normal, timeout.Token);
                Require(flow.CurrentRoundNumber == 1 && flow.ClearedRoundCount == 0, "Retry resets progress");
                Require(manual.GeneralRewardCount == 0 && manual.AugmentCount == 0, "Retry resets dummy rewards");
                timeout.Cancel();
                try { await retry; throw new Exception("Cancellation not propagated"); }
                catch (OperationCanceledException) { }
                Require(flow.State == eStageState.CANCELLED && !flow.IsRunning, "Cancellation releases run");
                Require(manual.PendingRequest == eDummyRequest.NONE && manual.SettlementCount == 0, "Cancel clears pending without settlement");
            }

            // 이미 끝난 어댑터도 취소 이후 승리로 진행할 수 없어야 한다.
            using (var cancellation = new CancellationTokenSource())
            {
                var cancellingBattle = new CancellingBattle(cancellation);
                var services = new ImmediateServices();
                var manager = new StageManager(cancellingBattle, services, services, services, new MemoryStageProgressStore(), services);
                try { await manager.RunAsync(normal, cancellation.Token); throw new Exception("Cancellation ignored"); }
                catch (OperationCanceledException) { }
                Require(manager.ClearedRoundCount == 0 && services.FinishCount == 0, "No work after cancellation");
            }

            var faultServices = new ImmediateServices();
            var faultManager = new StageManager(new FailingBattle(), faultServices, faultServices, faultServices, new MemoryStageProgressStore(), faultServices);
            try { await faultManager.RunAsync(normal, CancellationToken.None); throw new Exception("Adapter error swallowed"); }
            catch (InvalidOperationException) { }
            Require(faultManager.State == eStageState.ERROR && !faultManager.IsRunning && faultServices.FinishCount == 0,
                "Adapter error must not become defeat or success");
        }

        public static async Task DriveAsync(StageManager manager, ManualStageServices services, Task run, int defeatRound)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            long lastRequestId = -1;
            while (!run.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Dummy flow stalled at " + manager.State + " round " + manager.CurrentRoundNumber);
                long requestId = services.RequestId;
                if (requestId != lastRequestId && services.PendingRequest != eDummyRequest.NONE)
                {
                    lastRequestId = requestId;
                    // 디스크 저장을 포함한 전체 실행 시간 대신, 외부 요청이 멈춘 시간을 검사한다.
                    deadline = DateTime.UtcNow.AddSeconds(20);
                    switch (services.PendingRequest)
                    {
                        case eDummyRequest.PREPARATION:
                            Require(manager.State == eStageState.PREPARATION, "Preparation state");
                            if (manager.CurrentRoundNumber == 1)
                                Require(!services.CompletePreparation(requestId, true), "First preparation cannot skip");
                            Require(services.CompletePreparation(requestId, manager.CurrentRoundNumber > 1), "Preparation completion");
                            break;
                        case eDummyRequest.BATTLE:
                            Require(manager.State == eStageState.COMBAT, "Combat state");
                            Require(!services.CompleteBattle(requestId - 1, eBattleResult.VICTORY), "Stale result rejected");
                            Require(services.CompleteBattle(requestId, manager.CurrentRoundNumber == defeatRound
                                ? eBattleResult.DEFEAT : eBattleResult.VICTORY), "Battle completion");
                            Require(!services.CompleteBattle(requestId, eBattleResult.DEFEAT), "Duplicate result rejected");
                            break;
                        case eDummyRequest.GENERAL_REWARD:
                            Require(manager.State == eStageState.GENERAL_REWARD, "General reward state");
                            Require(!services.CompletePreparation(requestId, true), "Skip cannot bypass reward");
                            Require(manager.CurrentRoundNumber < manager.TotalRounds, "No final selection");
                            services.CompleteSelection(requestId);
                            break;
                        case eDummyRequest.AUGMENT:
                            Require(manager.State == eStageState.AUGMENT, "Augment state");
                            Require(services.GeneralRewardCount == manager.CurrentRoundNumber, "General reward before augment");
                            Require(manager.CurrentRoundNumber % 10 == 0 && manager.CurrentRoundNumber < manager.TotalRounds, "Boss augment only");
                            services.CompleteSelection(requestId);
                            break;
                        case eDummyRequest.SETTLEMENT:
                            Require(manager.State == eStageState.SETTLING, "Wait for settlement before terminal state");
                            services.CompleteSettlement(requestId);
                            break;
                        case eDummyRequest.LOBBY:
                            Require(manager.State == eStageState.RETURNING_TO_LOBBY && manager.Progress.IsSettled, "Lobby state after settlement save");
                            Require(services.SettlementCount == 1, "No lobby before settlement");
                            services.CompleteLobbyReturn(requestId);
                            break;
                    }
                }
                await Task.Yield();
            }
            await run;
        }

        private static StageDataSO LoadData(string name)
        {
            var data = AssetDatabase.LoadAssetAtPath<StageDataSO>("Assets/03.ScriptableObjects/Stage/Dummy/" + name + ".asset");
            if (data == null) throw new InvalidOperationException("Missing dummy asset: " + name);
            return data;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private sealed class CancellingBattle : IStageBattle
        {
            private readonly CancellationTokenSource _cancellation;
            internal CancellingBattle(CancellationTokenSource cancellation) { _cancellation = cancellation; }
            public Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken cancellationToken)
            {
                _cancellation.Cancel();
                return Task.FromResult(new RoundResult(eBattleResult.VICTORY, 0));
            }
        }

        private sealed class FailingBattle : IStageBattle
        {
            public Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken cancellationToken)
                => Task.FromException<RoundResult>(new InvalidOperationException("Expected test failure"));
        }

        private sealed class ImmediateServices : IStageRewards, IStagePreparation, IStageSession, IStageLobby
        {
            public Task ReturnAsync(StageRunResult result, CancellationToken token) => Task.CompletedTask;
            public int FinishCount { get; private set; }
            public Task BeginAsync(string stageId, CancellationToken token) => Task.CompletedTask;
            public Task PrepareAsync(bool canSkip, CancellationToken token) => Task.CompletedTask;
            public Task SelectGeneralRewardAsync(RewardRequest request, string rewardId, CancellationToken token) => Task.CompletedTask;
            public Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken token) => Task.CompletedTask;
            public Task SettleAsync(StageRunResult result, CancellationToken token)
            {
                FinishCount++;
                return Task.CompletedTask;
            }
        }
    }
}


