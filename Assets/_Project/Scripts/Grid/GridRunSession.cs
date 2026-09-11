using System;
using System.Collections.Generic;

namespace OZGL2.Grid
{
    /// <summary>외부 실행 ID와 라운드 요청으로 진행한다. 진행 순서는 외부 소유자가 결정한다.</summary>
    public sealed class GridRunSession : IGridPreparation, IDisposable
    {
        private readonly HashSet<string> _rewardIds = new HashSet<string>();
        private bool _isChanging;
        private bool _hasResolvedReward = true;
        public GridManager Grid { get; }
        public string RunId { get; }
        public int CompletedRounds { get; private set; }
        public int NextRound => CompletedRounds + 1;
        public string PendingRewardId { get; private set; }
        public GridDeploymentSnapshot Deployment { get; private set; }
        public bool IsEnded { get; private set; }
        public bool CanBeginBattle => !IsEnded && Grid.CanBeginBattle;
        public bool CanSkipPreparation => !IsEnded && Grid.CanSkipPreparation;
        public bool RequiresExpansionPlacement => Grid.RequiresExpansionPlacement;
        public GridRunSession(string runId, GridDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("External run ID required.");
            RunId = runId; Grid = new GridManager(definition);
        }
        private bool CanHandle(string runId) => !IsEnded && !_isChanging && runId == RunId;
        public bool TryAllowPreparation(string runId, int round, bool canSkip)
        {
            if (!CanHandle(runId) || round != NextRound || !_hasResolvedReward) return false;
            _isChanging = true;
            try { return Grid.TryAllowPreparation(CompletedRounds > 0 && canSkip); }
            finally { _isChanging = false; }
        }
        public bool TryBeginBattle(string runId, int round, bool skip = false)
        {
            if (!CanHandle(runId) || round != NextRound || !(skip ? CanSkipPreparation : CanBeginBattle)) return false;
            _isChanging = true;
            try
            {
                Deployment = new GridDeploymentSnapshot(Grid);
                return Grid.TryBeginBattle();
            }
            finally { _isChanging = false; }
        }
        /// <summary>다음 일반 보상이 있는 승리만 통지한다. 최종 승리·패배·취소는 Dispose로 종료한다.</summary>
        public bool TryFinishBattle(string runId, int round, string rewardRequestId)
        {
            if (!CanHandle(runId) || round != NextRound || Grid.Phase != eGridPhase.BATTLE ||
                string.IsNullOrWhiteSpace(rewardRequestId) || _rewardIds.Contains(rewardRequestId)) return false;
            _isChanging = true;
            try
            {
                CompletedRounds++; PendingRewardId = rewardRequestId;
                _rewardIds.Add(rewardRequestId); _hasResolvedReward = false;
                return Grid.TryFinishBattle();
            }
            finally { _isChanging = false; }
        }
        private bool CanChooseReward(string runId, string rewardId) => CanHandle(runId) &&
            rewardId != null && rewardId == PendingRewardId && !_hasResolvedReward && Grid.Phase == eGridPhase.REWARD;
        public bool TryChooseExpansion(string runId, string rewardId)
        {
            if (!CanChooseReward(runId, rewardId) || !Grid.CanExpand) return false;
            return ApplyReward(rewardId, () => Grid.TryAcceptExpansionReward());
        }
        public bool TryChooseUnit(string runId, string rewardId, UnitDefinition definition, FootprintDefinition shape)
        {
            if (!CanChooseReward(runId, rewardId) || definition == null || shape == null || definition.RequiredBlockId != shape.Id) return false;
            var unit = new UnitPlacement(rewardId + "_unit", definition);
            var block = new BlockPlacement(rewardId + "_block", shape.Id, shape);
            return ApplyReward(rewardId, () => Grid.TryAcceptUnitReward(unit, block));
        }
        private bool ApplyReward(string rewardId, Func<bool> apply)
        {
            _isChanging = true;
            try
            {
                PendingRewardId = null; _hasResolvedReward = true;
                if (apply()) return true;
                PendingRewardId = rewardId; _hasResolvedReward = false; return false;
            }
            finally { _isChanging = false; }
        }
        public void Dispose()
        {
            if (IsEnded) return;
            IsEnded = true; PendingRewardId = null; Grid.EndRun();
        }
    }
}
