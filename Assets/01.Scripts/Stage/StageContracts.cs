using System.Threading;
using System.Threading.Tasks;

namespace OZGL2.Stage
{
    public interface IStageDataSource
    {
        StageDefinition CreateSnapshot();
    }

    public enum eBattleResult { VICTORY, DEFEAT }

    /// <summary>구현체가 스폰·마왕군 부활·전투 결과 수신을 연결합니다.</summary>
    public interface IStageBattle
    {
        Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken cancellationToken);
    }

    public interface IStageRewards
    {
        // 같은 RequestId의 재호출은 재지급하지 않아야 합니다. 실제 지급과 영수증은 함께 저장합니다.
        Task SelectGeneralRewardAsync(RewardRequest request, string rewardId, CancellationToken cancellationToken);
        Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken cancellationToken);
    }

    public interface IStagePreparation
    {
        Task PrepareAsync(bool canSkip, CancellationToken cancellationToken);
    }

    /// <summary>런 초기화와 정산 지급·저장을 담당합니다. 씬 이동은 포함하지 않습니다.</summary>
    public interface IStageSession
    {
        Task BeginAsync(string stageId, CancellationToken cancellationToken);
        // RunId를 정산 중복 방지 키로 사용합니다. 지급 결과와 처리 키를 함께 저장합니다.
        Task SettleAsync(StageRunResult result, CancellationToken cancellationToken);
    }

    public interface IStageLobby
    {
        // 정산 완료 기록의 저장 성공 이후 호출됩니다. 정산을 재지급하지 않습니다.
        Task ReturnAsync(StageRunResult result, CancellationToken cancellationToken);
    }

    public interface IStageProgressStore
    {
        Task SaveAsync(StageRunResult snapshot, CancellationToken cancellationToken);
    }
}
