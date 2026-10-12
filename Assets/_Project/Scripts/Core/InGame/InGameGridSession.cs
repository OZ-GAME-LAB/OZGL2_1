using System;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Progression;
using OZGL2.UIBridge;
using OZGL2.Stage;
using OZGL2.Stage.Prototype;

namespace OZGL2.InGame
{
    /// <summary>동일 RunId의 Grid 수명과 프로토타입 정산만 소유한다. 계정 성장 데이터는 변경하지 않는다.</summary>
    public sealed class InGameGridSession : IStageSession, IDisposable
    {
        private readonly GridDefinition _definition;
        private readonly DummyRewardLedger _settlements;
        public GridRunSession Session { get; private set; }
        /// <summary>새 판 시작 때 시작 바닥에 더해 줄 보너스 칸 수(특성). BeginAsync 전에 정한다.</summary>
        public int BonusFloorCells { get; set; }
        public event Action Changed;
        public InGameGridSession(GridDefinition definition, DummyRewardLedger settlements)
        { _definition = definition; _settlements = settlements; }
        public Task BeginAsync(StageRunContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Session != null) throw new InvalidOperationException("Create a new adapter for each run.");
            Session = new GridRunSession(context.RunId, _definition, GridFusionPolicy.CanFuse);
            // 저장된 판을 이어하는 경우: 깬 웨이브 수와 마왕성 배치·증강을 되돌린 뒤 준비를 연다
            var resume = RunResume.Active;
            bool resumed = resume != null && resume.stageId == context.StageId;
            if (resumed)
            {
                RunSnapshotCodec.Restore(resume, Session, RunResume.Catalog);
                RunSnapshotCodec.RestoreAugments(resume, OZGL2.Synergy.RealCombatBootstrap.EnsureInitialized()?.Augments);
            }
            if (!resumed && BonusFloorCells > 0) Session.Grid.GrantBonusFloor(BonusFloorCells); // 이어하기는 저장된 바닥에 이미 포함 // 특성 "규모 확장"·"대군세"
            Session.Grid.Changed += Notify;
            Notify();
            return Task.CompletedTask;
        }
        public GridRunSession Require(string runId)
        {
            if (Session == null || Session.IsEnded || Session.RunId != runId)
                throw new InvalidOperationException("The request does not belong to the active grid run.");
            return Session;
        }
        public async Task SettleAsync(StageRunResult result, CancellationToken token)
        { await _settlements.ApplyAsync(result.RunId + ":settlement", token); }
        public void EndRun(string runId)
        { if (Session != null && Session.RunId == runId) Dispose(); }
        private void Notify() => Changed?.Invoke();
        public void Dispose()
        {
            if (Session == null || Session.IsEnded) return;
            Session.Grid.Changed -= Notify;
            Session.Dispose();
            Notify();
        }
    }
}
