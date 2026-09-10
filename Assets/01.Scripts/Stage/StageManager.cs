using System;
using System.Threading;
using System.Threading.Tasks;

namespace OZGL2.Stage
{
    public enum eStageState
    {
        IDLE, INITIALIZING, PREPARATION, COMBAT, GENERAL_REWARD, AUGMENT,
        SETTLING, CLEARED, FAILED, CANCELLED, ERROR, RETURNING_TO_LOBBY
    }

    /// <summary>Unity 메인 스레드에서 호출하며, 외부 시스템 완료 후 다음 단계로 진행합니다.</summary>
    public sealed class StageManager
    {
        private readonly IStageBattle _battle;
        private readonly IStageRewards _rewards;
        private readonly IStagePreparation _preparation;
        private readonly IStageSession _session;
        private readonly IStageProgressStore _progressStore;
        private readonly IStageLobby _lobby;
        private StageRunProgress _runProgress;
        private StageRunResult _snapshot;
        private int _isRunning;
        private bool _isPreparationEnded;
        public Exception PreparationCleanupError { get; private set; }

        public eStageState State { get; private set; } = eStageState.IDLE;
        public int CurrentRoundNumber { get; private set; }
        public int TotalRounds { get; private set; }
        public int ClearedRoundCount { get; private set; }
        public bool IsRunning => _isRunning != 0;
        public StageRunResult Progress => _snapshot;
        public Exception PersistenceError { get; private set; }
        public Exception LastNotificationError { get; private set; }
        public string LastFailedSubscriber { get; private set; }
        public int NotificationErrorCount { get; private set; }
        public event Action<eStageState> StateChanged;

        public StageManager(IStageBattle battle, IStageRewards rewards,
            IStagePreparation preparation, IStageSession session, IStageProgressStore progressStore, IStageLobby lobby)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            _preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _progressStore = progressStore ?? throw new ArgumentNullException(nameof(progressStore));
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
        }

        public async Task RunAsync(IStageDataSource source, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                throw new InvalidOperationException("A stage is already running.");

            try
            {
                CurrentRoundNumber = 0;
                ClearedRoundCount = 0;
                TotalRounds = 0;
                _runProgress = null;
                _snapshot = null;
                PersistenceError = null;
                PreparationCleanupError = null; _isPreparationEnded = false;
                LastNotificationError = null;
                LastFailedSubscriber = null;
                NotificationErrorCount = 0;
                cancellationToken.ThrowIfCancellationRequested();
                if (source == null) throw new ArgumentNullException(nameof(source));
                var stage = source.CreateSnapshot()
                    ?? throw new InvalidOperationException("Stage data source returned null.");
                TotalRounds = stage.Rounds.Count;
                _runProgress = new StageRunProgress(stage);
                _snapshot = _runProgress.CreateSnapshot();
                SetState(eStageState.INITIALIZING);
                await _session.BeginAsync(new StageRunContext(_snapshot.RunId, stage.StageId), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await SaveProgressAsync(cancellationToken);

                for (int index = 0; index < stage.Rounds.Count; index++)
                {
                    CurrentRoundNumber = index + 1;
                    _runProgress.BeginRound(CurrentRoundNumber);
                    _snapshot = _runProgress.CreateSnapshot();
                    SetState(eStageState.PREPARATION);
                    await _preparation.PrepareAsync(new StagePreparationRequest(_snapshot.RunId, CurrentRoundNumber, index > 0), cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    var round = stage.Rounds[index];
                    SetState(eStageState.COMBAT);
                    var result = await _battle.RunRoundAsync(round, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    _runProgress.RecordRound(CurrentRoundNumber, result);
                    ClearedRoundCount = _runProgress.CreateSnapshot().ClearedRoundCount;
                    await SaveProgressAsync(cancellationToken);
                    if (result.Outcome == eBattleResult.DEFEAT)
                    {
                        await FinishAsync(false, cancellationToken);
                        return;
                    }
                    if (result.Outcome != eBattleResult.VICTORY)
                        throw new InvalidOperationException("Unknown battle result.");

                    if (ClearedRoundCount == TotalRounds)
                    {
                        // 최종 라운드는 다음 전투용 보상 선택을 생략한다.
                        await FinishAsync(true, cancellationToken);
                        return;
                    }

                    SetState(eStageState.GENERAL_REWARD);
                    await _rewards.SelectGeneralRewardAsync(_runProgress.CreateRewardRequest(eRewardKind.GENERAL),
                        round.RewardId, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    _runProgress.MarkRewardApplied(eRewardKind.GENERAL);
                    await SaveProgressAsync(cancellationToken);
                    if (round.IsBossRound)
                    {
                        SetState(eStageState.AUGMENT);
                        await _rewards.SelectAugmentAsync(_runProgress.CreateRewardRequest(eRewardKind.AUGMENT),
                            round.AugmentWeights, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        _runProgress.MarkRewardApplied(eRewardKind.AUGMENT);
                        await SaveProgressAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                EndPreparation();
                await SaveInterruptionAsync(true);
                SetState(eStageState.CANCELLED);
                throw;
            }
            catch
            {
                EndPreparation();
                await SaveInterruptionAsync(false);
                SetState(eStageState.ERROR);
                throw;
            }
            finally
            {
                EndPreparation();
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        private async Task FinishAsync(bool isCleared, CancellationToken cancellationToken)
        {
            EndPreparation();
            if (PreparationCleanupError != null) throw new AggregateException("Preparation shutdown failed.", PreparationCleanupError);
            SetState(eStageState.SETTLING);
            await _session.SettleAsync(_runProgress.CreateSnapshot(), cancellationToken);
            // 정산이 성공했다면 직후 도착한 취소도 지급 완료 사실을 지우지 않는다.
            _runProgress.MarkSettled();
            await SaveProgressAsync(cancellationToken);
            SetState(eStageState.RETURNING_TO_LOBBY);
            await _lobby.ReturnAsync(_snapshot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SetState(isCleared ? eStageState.CLEARED : eStageState.FAILED);
        }

        private async Task SaveProgressAsync(CancellationToken token)
        {
            _snapshot = _runProgress.CreateSnapshot();
            await _progressStore.SaveAsync(_snapshot, token);
            token.ThrowIfCancellationRequested();
        }
        private void EndPreparation()
        {
            if (_isPreparationEnded || _runProgress == null) return;
            _isPreparationEnded = true;
            try { _preparation.EndRun(_runProgress.CreateSnapshot().RunId); }
            catch (Exception exception) { PreparationCleanupError = exception; }
        }

        private async Task SaveInterruptionAsync(bool isCancelled)
        {
            if (_runProgress == null) return;
            // 이미 확정된 승패는 정산/저장 중 오류가 나도 덮어쓰지 않는다.
            if (_runProgress.CreateSnapshot().Status == eRunStatus.RUNNING)
                _runProgress.MarkInterrupted(isCancelled);
            _snapshot = _runProgress.CreateSnapshot();
            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    await _progressStore.SaveAsync(_snapshot, timeout.Token);
            }
            catch (Exception exception) { PersistenceError = exception; }
        }

        private void SetState(eStageState state)
        {
            State = state;
            var handlers = StateChanged;
            if (handlers == null) return;
            foreach (Action<eStageState> handler in handlers.GetInvocationList())
            {
                try { handler(state); }
                catch (Exception exception)
                {
                    // 표시 계층의 실패를 전투/저장 실패로 처리하지 않고 진단 정보로 남긴다.
                    LastNotificationError = exception;
                    LastFailedSubscriber = handler.Method.DeclaringType?.FullName + "." + handler.Method.Name;
                    NotificationErrorCount++;
                }
            }
        }
    }
}
