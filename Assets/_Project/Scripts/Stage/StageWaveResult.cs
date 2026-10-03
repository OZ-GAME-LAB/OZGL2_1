using System;
using System.Threading;
using System.Threading.Tasks;

namespace OZGL2.Stage
{
    /// <summary>이미 확정된 웨이브 기록. 표시와 확인은 경험치를 다시 지급하지 않는다.</summary>
    public sealed class StageWaveResult
    {
        public string RunId { get; }
        public int WaveNumber { get; }
        public eBattleResult Outcome { get; }
        public long EarnedExperience { get; }
        public bool IsCleared => Outcome == eBattleResult.VICTORY;
        public string RequestId => RunId + ":wave_result:" + WaveNumber;

        public StageWaveResult(string runId, int waveNumber, RoundResult result)
        {
            if (!Guid.TryParseExact(runId, "N", out _)) throw new ArgumentException("Expected run GUID.", nameof(runId));
            if (waveNumber <= 0) throw new ArgumentOutOfRangeException(nameof(waveNumber));
            if (result == null) throw new ArgumentNullException(nameof(result));
            RunId = runId;
            WaveNumber = waveNumber;
            Outcome = result.Outcome;
            EarnedExperience = result.EarnedExperience;
        }
    }

    public interface IStageWaveResults
    {
        /// <summary>확인이 완료될 때까지 다음 보상 선택을 시작하지 않는다.</summary>
        Task ShowAsync(StageWaveResult result, CancellationToken cancellationToken);
    }
}
