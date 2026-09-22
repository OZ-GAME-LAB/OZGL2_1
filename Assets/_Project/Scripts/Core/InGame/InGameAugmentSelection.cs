using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Augment;
using OZGL2.Stage;

namespace OZGL2.InGame
{
    /// <summary>한 실행의 선택 요청과 완료만 소유한다. 효과 계산은 AugmentRun에 맡긴다.</summary>
    public sealed class InGameAugmentSelection : IDisposable
    {
        private sealed class Pending
        {
            internal string Id;
            internal IReadOnlyList<AugmentData> Candidates;
            internal CancellationToken Token;
            internal bool IsApplying;
            internal readonly TaskCompletionSource<bool> Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        private readonly AugmentRun _augments;
        private readonly Func<bool> _isCurrent;
        private readonly Predicate<AugmentData> _canOffer;
        private readonly HashSet<string> _completed = new HashSet<string>();
        private Pending _pending;
        private string _runId;
        private bool _isDisposed;
        private bool _hasApplyFailure;
        public bool IsPending => _pending != null && !_pending.Completion.Task.IsCompleted;
        public string RequestId => IsPending ? _pending.Id : null;
        public IReadOnlyList<AugmentData> Candidates => IsPending ? _pending.Candidates : Array.Empty<AugmentData>();
        public string Error { get; private set; }

        public InGameAugmentSelection(AugmentRun augments, Func<bool> isCurrent, Predicate<AugmentData> canOffer = null)
        {
            _augments = augments ?? throw new ArgumentNullException(nameof(augments));
            _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
            _canOffer = canOffer;
        }

        public async Task SelectAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_isDisposed || !_isCurrent()) throw new InvalidOperationException("Augment run is no longer active.");
            if (_hasApplyFailure) throw new InvalidOperationException("Augment application failed; start a fresh run before retrying.");
            if (request == null || request.Kind != eRewardKind.AUGMENT || weights == null) throw new ArgumentException("Augment request and weights are required.");
            if (_runId != null && _runId != request.RunId) throw new InvalidOperationException("Augment request belongs to another run.");
            if (_pending != null) throw new InvalidOperationException("An augment request is already in progress.");
            if (_completed.Contains(request.RequestId)) return;
            _runId = request.RunId;
            Error = null;
            var candidates = _augments.Draw3(weights.Silver, weights.Gold, weights.Platinum, _canOffer);
            if (candidates.Count == 0) throw new InvalidOperationException("No eligible augment candidates remain for these tier weights.");
            var pending = new Pending { Id = request.RequestId, Candidates = candidates.AsReadOnly(), Token = token };
            _pending = pending;
            // 취소 콜백은 Unity 객체를 만지지 않고 자신이 캡처한 요청만 취소한다.
            using (token.Register(() => { lock (pending) { if (!pending.IsApplying) pending.Completion.TrySetCanceled(); } }))
            {
                try { await pending.Completion.Task; }
                finally { if (ReferenceEquals(_pending, pending)) _pending = null; }
            }
        }

        /// <summary>Unity 메인 스레드에서 호출. 표시 당시 요청 ID를 전달해야 오래된 클릭을 차단한다.</summary>
        public bool TrySelect(string requestId, AugmentData candidate)
        {
            var pending = _pending;
            if (pending == null) return false;
            lock (pending) return ApplySelection(pending, requestId, candidate);
        }

        private bool ApplySelection(Pending pending, string requestId, AugmentData candidate)
        {
            if (!IsPending || pending.IsApplying || _isDisposed || pending.Token.IsCancellationRequested || pending.Id != requestId) return false;
            if (!_isCurrent()) { Dispose(); return false; }
            bool offered = false;
            foreach (var item in pending.Candidates) if (ReferenceEquals(item, candidate)) offered = true;
            if (!offered) return false;
            // 적용 중 재진입을 차단한다. 적용 예외는 성공으로 간주하거나 재지급하지 않는다.
            pending.IsApplying = true;
            try
            {
                if (!_augments.Pick(candidate)) throw new InvalidOperationException("Selected augment could not be applied.");
                _completed.Add(requestId);
                pending.Completion.TrySetResult(true);
                return true;
            }
            catch (Exception exception)
            {
                Error = exception.Message;
                _hasApplyFailure = true;
                pending.Completion.TrySetException(exception);
                return false;
            }
            finally { pending.IsApplying = false; }
        }
        public void Dispose()
        {
            _isDisposed = true;
            _pending?.Completion.TrySetCanceled();
            _pending = null;
        }
    }
}
