using System;
using System.Threading;
using System.Threading.Tasks;

namespace OZGL2.Stage.Prototype
{
    public enum eDummyRequest { NONE, PREPARATION, BATTLE, GENERAL_REWARD, AUGMENT, SETTLEMENT, LOBBY }

    /// <summary>실제 시스템 대신 요청을 보관하고, 명시적인 수동 완료 신호를 기다립니다.</summary>
    public sealed class ManualStageServices : IStageBattle, IStageRewards, IStagePreparation, IStageSession, IStageLobby
    {
        private TaskCompletionSource<int> _pending;
        private long _requestId;
        private readonly DummyRewardLedger _ledger;
        public ManualStageServices(DummyRewardLedger ledger = null) { _ledger = ledger ?? new DummyRewardLedger(); }

        public eDummyRequest PendingRequest { get; private set; }
        public long RequestId => _requestId;
        public bool CanSkip { get; private set; }
        public RoundDefinition CurrentRound { get; private set; }
        public string RewardId { get; private set; }
        public AugmentTierWeights AugmentWeights { get; private set; }
        public int GeneralRewardCount { get; private set; }
        public int AugmentCount { get; private set; }
        public int SkippedPreparationCount { get; private set; }
        public int SettlementCount { get; private set; }
        public int LobbyReturnCount { get; private set; }
        public int LastClearedRoundCount { get; private set; }
        public bool IsLastStageCleared { get; private set; }

        public Task BeginAsync(string stageId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GeneralRewardCount = 0;
            AugmentCount = 0;
            SkippedPreparationCount = 0;
            SettlementCount = 0;
            LobbyReturnCount = 0;
            LastClearedRoundCount = 0;
            IsLastStageCleared = false;
            CurrentRound = null;
            RewardId = null;
            AugmentWeights = null;
            return Task.CompletedTask;
        }

        public async Task PrepareAsync(bool canSkip, CancellationToken cancellationToken)
        {
            CanSkip = canSkip;
            int result = await WaitAsync(eDummyRequest.PREPARATION, cancellationToken);
            if (result == 1) SkippedPreparationCount++;
            CanSkip = false;
        }

        public async Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken cancellationToken)
        {
            CurrentRound = round;
            int result = await WaitAsync(eDummyRequest.BATTLE, cancellationToken);
            return new RoundResult(result == 1 ? eBattleResult.VICTORY : eBattleResult.DEFEAT, 0);
        }

        public async Task SelectGeneralRewardAsync(RewardRequest request, string rewardId, CancellationToken cancellationToken)
        {
            if (_ledger.Contains(request.RequestId)) return;
            RewardId = rewardId;
            await WaitAsync(eDummyRequest.GENERAL_REWARD, cancellationToken);
            if (await _ledger.ApplyAsync(request.RequestId, cancellationToken)) GeneralRewardCount++;
        }

        public async Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken cancellationToken)
        {
            if (_ledger.Contains(request.RequestId)) return;
            AugmentWeights = weights;
            await WaitAsync(eDummyRequest.AUGMENT, cancellationToken);
            if (await _ledger.ApplyAsync(request.RequestId, cancellationToken)) AugmentCount++;
        }

        public async Task SettleAsync(StageRunResult result, CancellationToken cancellationToken)
        {
            LastClearedRoundCount = result.ClearedRoundCount;
            IsLastStageCleared = result.IsCleared;
            string settlementId = result.RunId + ":settlement";
            if (_ledger.Contains(settlementId)) return;
            await WaitAsync(eDummyRequest.SETTLEMENT, cancellationToken);
            if (await _ledger.ApplyAsync(settlementId, cancellationToken)) SettlementCount++;
        }

        public async Task ReturnAsync(StageRunResult result, CancellationToken cancellationToken)
        {
            if (!result.IsSettled) throw new InvalidOperationException("Settlement is not complete.");
            await WaitAsync(eDummyRequest.LOBBY, cancellationToken);
            LobbyReturnCount++;
        }

        public bool CompleteLobbyReturn(long requestId) => Complete(requestId, eDummyRequest.LOBBY, 0);

        public bool CompletePreparation(long requestId, bool isSkipped)
        {
            if (isSkipped && !CanSkip) return false;
            return Complete(requestId, eDummyRequest.PREPARATION, isSkipped ? 1 : 0);
        }

        public bool CompleteBattle(long requestId, eBattleResult result)
        {
            if (result != eBattleResult.VICTORY && result != eBattleResult.DEFEAT) return false;
            return Complete(requestId, eDummyRequest.BATTLE, result == eBattleResult.VICTORY ? 1 : 0);
        }

        public bool CompleteSelection(long requestId)
        {
            if (PendingRequest != eDummyRequest.GENERAL_REWARD && PendingRequest != eDummyRequest.AUGMENT)
                return false;
            return Complete(requestId, PendingRequest, 0);
        }

        public bool CompleteSettlement(long requestId)
        {
            return Complete(requestId, eDummyRequest.SETTLEMENT, 0);
        }

        private bool Complete(long requestId, eDummyRequest request, int result)
        {
            if (requestId != _requestId || PendingRequest != request || _pending == null) return false;
            return _pending.TrySetResult(result);
        }

        private async Task<int> WaitAsync(eDummyRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_pending != null) throw new InvalidOperationException("Another dummy request is pending.");
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = completion;
            _requestId++;
            PendingRequest = request;
            try
            {
                using (cancellationToken.Register(() => completion.TrySetCanceled()))
                    return await completion.Task;
            }
            finally
            {
                _pending = null;
                PendingRequest = eDummyRequest.NONE;
            }
        }
    }
}
