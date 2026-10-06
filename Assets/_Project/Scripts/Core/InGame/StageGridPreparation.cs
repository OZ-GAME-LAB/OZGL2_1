using System;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Stage;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>Stage 준비 요청을 Grid의 배치 확정까지 연결한다. 화면 존재 여부와 무관하다.</summary>
    public sealed class StageGridPreparation : IStagePreparation
    {
        private readonly InGameGridSession _session;
        private readonly UnitDefinition _initialUnit;
        private readonly FootprintDefinition _initialBlock;
        private readonly Vector2Int _anchor;

        /// <summary>
        /// true 면 첫 라운드에 기본 유닛을 칸에 미리 깔지 않고 보관함 카드로 지급해, 플레이어가 직접 끌어다 놓게 한다(튜토리얼용).
        /// 기본값(false)이면 기존처럼 미리 배치한다. 실행 전에 바꾸는 값이며 실행 중에는 읽기만 한다.
        /// </summary>
        public static bool InitialAsCard { get; set; }

        public StageGridPreparation(InGameGridSession session, UnitDefinition unit, FootprintDefinition block, Vector2Int anchor)
        { _session = session; _initialUnit = unit; _initialBlock = block; _anchor = anchor; }

        public async Task PrepareAsync(StagePreparationRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var session = _session.Require(request.RunId);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action changed = () => { if (session.Grid.Phase == eGridPhase.BATTLE) completion.TrySetResult(true); };
            session.Grid.Changed += changed;
            try
            {
                if (request.RoundNumber == 1)
                {
                    if (InitialAsCard) GrantInitialCard(session, _initialUnit, _initialBlock);
                    else PlaceInitial(session, _initialUnit, _initialBlock, _anchor);
                }
                else if (!session.TryAllowPreparation(request.RunId, request.RoundNumber, request.CanSkip))
                    throw new InvalidOperationException("Grid rejected the next preparation request.");
                using (token.Register(() => completion.TrySetCanceled())) await completion.Task;
                token.ThrowIfCancellationRequested();
                if (session.Deployment == null) throw new InvalidOperationException("Battle deployment was not captured.");
            }
            finally { session.Grid.Changed -= changed; }
        }

        /// <summary>기본 유닛과 발판을 칸에 놓지 않고 보관함에만 넣은 채 준비 단계를 연다. 유닛은 카드로 보이고, 놓는 것은 플레이어가 한다.</summary>
        public static void GrantInitialCard(GridRunSession session, UnitDefinition unit, FootprintDefinition block)
        {
            if (session.NextRound != 1 || session.Grid.Units.Count != 0 || session.Grid.Blocks.Count != 0)
                throw new InvalidOperationException("Initial card requires a fresh grid session.");
            session.Grid.AddBlock(session.RunId + ":initial_block", block.Id, block);
            session.Grid.AddUnit(session.RunId + ":initial_unit", unit);
            if (!session.TryAllowPreparation(session.RunId, 1, false))
                throw new InvalidOperationException("Initial preparation could not start.");
        }

        public static void PlaceInitial(GridRunSession session, UnitDefinition unit, FootprintDefinition block, Vector2Int anchor)
        {
            if (session.NextRound != 1 || session.Grid.Units.Count != 0 || session.Grid.Blocks.Count != 0)
                throw new InvalidOperationException("Initial placement requires a fresh grid session.");
            var grid = session.Grid;
            string blockId = session.RunId + ":initial_block";
            string unitId = session.RunId + ":initial_unit";
            grid.AddBlock(blockId, block.Id, block);
            grid.AddUnit(unitId, unit);
            if (!session.TryAllowPreparation(session.RunId, 1, false) || !grid.BeginBlockDrag(blockId))
                throw new InvalidOperationException("Initial block cannot be selected.");
            grid.MovePreview(anchor);
            if (!grid.CommitPreview() || !grid.BeginUnitDrag(unitId))
                throw new InvalidOperationException("Initial block placement failed.");
            grid.MovePreview(anchor);
            if (!grid.CommitPreview() || !session.CanBeginBattle)
                throw new InvalidOperationException("Initial unit placement or battle validation failed.");
        }
        public void EndRun(string runId) => _session.EndRun(runId);
    }
}
