using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid.Prototype;
using OZGL2.Stage;

namespace OZGL2.InGame
{
    /// <summary>선택 클릭이 아닌 Grid 지급 완료를 일반 보상 완료로 인정한다.</summary>
    public sealed class StageGridRewards : IStageRewards
    {
        private readonly InGameGridSession _session;
        private readonly IStageRewards _augment;
        private readonly GridPrototypeRewards _candidates;
        private readonly HashSet<string> _applied = new HashSet<string>();
        public RewardRequest Pending { get; private set; }
        public StageGridRewards(InGameGridSession session, GridPrototypeRewards candidates, IStageRewards augment)
        { _session = session; _candidates = candidates; _augment = augment; }
        public async Task SelectGeneralRewardAsync(RewardRequest request, string rewardId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_applied.Contains(request.RequestId)) return;
            if (Pending != null) throw new InvalidOperationException("Another reward is pending.");
            var session = _session.Require(request.RunId);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action changed = () =>
            {
                if (session.PendingRewardId == null && !session.IsEnded && session.CompletedRounds == request.RoundNumber)
                    completion.TrySetResult(true);
            };
            Pending = request;
            session.Grid.Changed += changed;
            try
            {
                if (!session.TryFinishBattle(request.RunId, request.RoundNumber, request.RequestId))
                    throw new InvalidOperationException("Grid rejected the completed battle.");
                using (token.Register(() => completion.TrySetCanceled())) await completion.Task;
                // 지급 직후 취소되어도 이미 반영된 요청은 재지급하지 않는다.
                _applied.Add(request.RequestId);
                token.ThrowIfCancellationRequested();
            }
            finally { session.Grid.Changed -= changed; Pending = null; }
        }
        public bool TryChooseUnit(string requestId, int option)
        {
            if (Pending == null || Pending.RequestId != requestId || option < 0 || option > 1) return false;
            return _candidates.TryChoose(_session.Require(Pending.RunId), option);
        }
        public bool TryChooseExpansion(string requestId)
        {
            if (Pending == null || Pending.RequestId != requestId) return false;
            return _session.Require(Pending.RunId).TryChooseExpansion(Pending.RunId, requestId);
        }
        public string GetCandidateName(int option) => Pending == null ? string.Empty :
            _candidates.GetCandidate(Pending.RoundNumber, option).DisplayName;
        public Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken token)
            => _augment.SelectAugmentAsync(request, weights, token);
    }
}
