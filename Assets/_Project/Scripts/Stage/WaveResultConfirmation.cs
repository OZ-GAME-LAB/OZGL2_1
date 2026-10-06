using System;
using System.Threading;
using System.Threading.Tasks;

namespace OZGL2.Stage
{
    /// <summary>Unity 메인 스레드에서 사용하는 확인 대기 상태. 화면/보상 지급과 독립적이다.</summary>
    public sealed class WaveResultConfirmation : IStageWaveResults
    {
        private TaskCompletionSource<bool> _completion;
        private CancellationToken _token;
        public StageWaveResult Pending { get; private set; }

        public async Task ShowAsync(StageWaveResult result, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (_completion != null) throw new InvalidOperationException("A wave result is already pending.");
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion;
            _token = cancellationToken;
            Pending = result;
            try
            {
                using (cancellationToken.Register(() => completion.TrySetCanceled()))
                    await completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                Pending = null;
                _completion = null;
                _token = default;
            }
        }

        public bool TryConfirm(string requestId) => !_token.IsCancellationRequested &&
            Pending != null && Pending.RequestId == requestId && _completion.TrySetResult(true);

        public void CancelPending() => _completion?.TrySetCanceled();
    }
}
