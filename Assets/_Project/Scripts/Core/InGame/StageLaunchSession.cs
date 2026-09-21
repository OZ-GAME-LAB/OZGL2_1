using System;
using OZGL2.Stage;

namespace OZGL2.InGame
{
    public sealed class StageLaunchRequest
    {
        public string RequestId { get; } = Guid.NewGuid().ToString("N");
        public string StageId { get; }
        public string ScenePath { get; }
        public StageLaunchRequest(string stageId, string scenePath)
        {
            if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(scenePath))
                throw new ArgumentException("Stage ID and destination scene are required.");
            StageId = stageId; ScenePath = scenePath;
        }
    }

    /// <summary>씬 이동 요청 한 건만 보관한다. 영구 저장이나 진행 중인 실행 상태를 소유하지 않는다.</summary>
    public sealed class StageLaunchSession
    {
        public StageLaunchRequest Pending { get; private set; }
        public bool HasLaunchHistory { get; private set; }

        public bool TryQueue(StageLaunchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (Pending != null) return false;
            Pending = request;
            HasLaunchHistory = true;
            return true;
        }

        public StageLaunchRequest Consume(string scenePath)
        {
            var request = Pending;
            if (request == null) return null;
            Pending = null;
            if (request.ScenePath != scenePath) throw new InvalidOperationException("Stage request destination does not match this scene.");
            return request;
        }

        public bool Cancel(string requestId)
        {
            if (Pending == null || Pending.RequestId != requestId) return false;
            Pending = null;
            return true;
        }

        public void EnterSelection()
        {
            Pending = null;
            HasLaunchHistory = true;
        }

        public void ObserveScene(string scenePath)
        {
            if (Pending != null && Pending.ScenePath != scenePath) Cancel(Pending.RequestId);
        }
    }

    /// <summary>시작 시 확정한 읽기 전용 데이터를 재도전에서도 사용한다.</summary>
    public sealed class SelectedStageSource : IStageDataSource
    {
        private readonly StageDefinition _stage;
        public SelectedStageSource(StageDefinition stage)
        { _stage = stage ?? throw new ArgumentNullException(nameof(stage)); }
        public StageDefinition CreateSnapshot() => _stage;
    }
}
