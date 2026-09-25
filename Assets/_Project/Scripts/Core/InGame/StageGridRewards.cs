using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Stage;

namespace OZGL2.InGame
{
    /// <summary>선택 클릭이 아닌 Grid 지급 완료를 일반 보상 완료로 인정한다.</summary>
    public sealed class StageGridRewards : IStageRewards
    {
        private readonly InGameGridSession _session;
        private readonly IStageRewards _augment;
        private readonly GeneralRewardSource _source;
        private readonly HashSet<string> _applied = new HashSet<string>();
        private CancellationToken _selectionToken;
        public RewardRequest Pending { get; private set; }
        public IReadOnlyList<GeneralRewardOption> Candidates { get; private set; } = Array.Empty<GeneralRewardOption>();
        public StageGridRewards(InGameGridSession session, GeneralRewardSource source, IStageRewards augment)
        { _session = session; _source = source; _augment = augment; }
        public async Task SelectGeneralRewardAsync(RewardRequest request, string rewardId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_applied.Contains(request.RequestId)) return;
            if (Pending != null) throw new InvalidOperationException("Another reward is pending.");
            var session = _session.Require(request.RunId);
            var candidates = _source.Draw(session.Grid.CanExpand);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action changed = () =>
            {
                if (session.PendingRewardId == null && !session.IsEnded && session.CompletedRounds == request.RoundNumber)
                    completion.TrySetResult(true);
            };
            Pending = request;
            _selectionToken = token;
            Candidates = candidates;
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
            finally { session.Grid.Changed -= changed; Pending = null; Candidates = Array.Empty<GeneralRewardOption>(); }
        }
        public bool TryChooseUnit(string requestId, int option)
        {
            if (!CanSelect(requestId) || option < 0 || option >= Candidates.Count ||
                Candidates[option].Kind != eGeneralRewardKind.UNIT) return false;
            var candidate = Candidates[option];
            return _session.Require(Pending.RunId).TryChooseUnit(Pending.RunId, requestId, candidate.Unit, candidate.Block);
        }
        public bool TryChooseExpansion(string requestId)
        {
            if (!CanSelect(requestId)) return false;
            bool isOffered = false;
            foreach (var candidate in Candidates) if (candidate.Kind == eGeneralRewardKind.EXPANSION) isOffered = true;
            if (!isOffered) return false;
            return _session.Require(Pending.RunId).TryChooseExpansion(Pending.RunId, requestId);
        }
        public bool TrySelect(string requestId, int option)
        {
            if (Pending == null || Pending.RequestId != requestId || option < 0 || option >= Candidates.Count) return false;
            return Candidates[option].Kind == eGeneralRewardKind.EXPANSION
                ? TryChooseExpansion(requestId) : TryChooseUnit(requestId, option);
        }
        private bool CanSelect(string requestId) => Pending != null && Pending.RequestId == requestId &&
            !_selectionToken.IsCancellationRequested && _session.Session != null && !_session.Session.IsEnded &&
            _session.Session.PendingRewardId == requestId;
        public string GetCandidateName(int option) => Pending == null || option < 0 || option >= Candidates.Count
            ? string.Empty : Candidates[option].Unit?.DisplayName ?? "Floor expansion";
        public Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken token)
            => _augment.SelectAugmentAsync(request, weights, token);
    }
}
