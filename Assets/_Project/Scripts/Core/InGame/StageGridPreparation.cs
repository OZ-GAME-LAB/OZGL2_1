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
        public StageGridPreparation(InGameGridSession session, UnitDefinition unit, FootprintDefinition block, Vector2Int anchor)
        { _session = session; _initialUnit = unit; _initialBlock = block; _anchor = anchor; }

        public async Task PrepareAsync(StagePreparationRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var session = _session.Require(request.RunId);
            if (request.RoundNumber == 1)
            {
                PlaceInitial(session, _initialUnit, _initialBlock, _anchor);
                return;
            }
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action changed = () => { if (session.Grid.Phase == eGridPhase.BATTLE) completion.TrySetResult(true); };
            session.Grid.Changed += changed;
            try
            {
                if (!session.TryAllowPreparation(request.RunId, request.RoundNumber, request.CanSkip))
                    throw new InvalidOperationException("Grid rejected the next preparation request.");
                using (token.Register(() => completion.TrySetCanceled())) await completion.Task;
                token.ThrowIfCancellationRequested();
                if (session.Deployment == null) throw new InvalidOperationException("Battle deployment was not captured.");
            }
            finally { session.Grid.Changed -= changed; }
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
            if (!grid.CommitPreview() || !session.TryBeginBattle(session.RunId, 1))
                throw new InvalidOperationException("Initial unit placement or battle validation failed.");
        }
        public void EndRun(string runId) => _session.EndRun(runId);
    }
}
