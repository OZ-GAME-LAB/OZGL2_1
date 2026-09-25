using System;
using OZGL2.Stage;

namespace OZGL2.InGame
{
    /// <summary>전투 자원 해제와 독립된 결과 표시용 스냅샷. XP를 추가 지급하지 않는다.</summary>
    public sealed class InGameRunResult
    {
        public StageRunResult Progress { get; }
        public string RunId => Progress.RunId;
        public int TotalRounds { get; }
        public int Level { get; }
        public int CurrentLevelXp { get; }
        public InGameRunResult(StageRunResult progress, int totalRounds, int level, int currentLevelXp)
        {
            if (progress == null || !progress.IsSettled ||
                (progress.Status != eRunStatus.CLEARED && progress.Status != eRunStatus.FAILED))
                throw new ArgumentException("A settled victory or defeat is required.", nameof(progress));
            Progress = progress;
            TotalRounds = totalRounds;
            Level = level;
            CurrentLevelXp = currentLevelXp;
        }
    }

    /// <summary>결과 한 건에 대해 한 개의 이동 명령만 접수한다. 실패한 이동은 같은 결과에서 재시도한다.</summary>
    public sealed class InGameResultSelection
    {
        public InGameRunResult Result { get; private set; }
        public bool IsBusy { get; private set; }
        public void Present(InGameRunResult result)
        {
            if (Result != null) throw new InvalidOperationException("A result is already pending.");
            Result = result ?? throw new ArgumentNullException(nameof(result));
            IsBusy = false;
        }
        public bool TryBegin(string runId)
        {
            if (Result == null || Result.RunId != runId || IsBusy) return false;
            IsBusy = true;
            return true;
        }
        public void Restore(string runId)
        {
            if (Result?.RunId == runId) IsBusy = false;
        }
        public void Clear() { Result = null; IsBusy = false; }
    }
}
