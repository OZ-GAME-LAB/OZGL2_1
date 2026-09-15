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
        public GridRunSession(string runId, GridDefinition definition, Func<UnitPlacement, UnitPlacement, bool> canFuse = null)
        {
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("External run ID required.");
            RunId = runId; Grid = new GridManager(definition, canFuse);
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
                return Grid.TryBeginBattle(() => Deployment = new GridDeploymentSnapshot(Grid));
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
            !Grid.HasPendingStorage && rewardId != null && rewardId == PendingRewardId && !_hasResolvedReward && Grid.Phase == eGridPhase.REWARD;
        public bool TryChooseExpansion(string runId, string rewardId)
        {
            if (!CanChooseReward(runId, rewardId) || !Grid.CanExpand) return false;
            return ApplyReward(rewardId, () => Grid.TryAcceptExpansionReward());
        }
        public bool TryChooseUnit(string runId, string rewardId, UnitDefinition definition, FootprintDefinition shape)
        {
            if (!CanChooseReward(runId, rewardId) || definition == null || shape == null || definition.RewardBlockId != shape.Id) return false;
            var unit = new UnitPlacement(rewardId + "_unit", definition);
            var block = new BlockPlacement(rewardId + "_block", shape.Id, shape);
            _isChanging = true;
            try
            {
                return Grid.TryAcceptUnitReward(unit, block, () => { PendingRewardId = null; _hasResolvedReward = true; });
            }
            finally { _isChanging = false; }
        }
        /// <summary>false여도 Grid.PendingStorage가 있으면 공간 확보 대기 중이다. 지급 완료 전 다음 준비를 허용하지 않는다.</summary>
        public bool TryConfirmStorage(string runId, string requestId, IReadOnlyCollection<GridStoredItem> discard)
        {
            if (!CanHandle(runId)) return false;
            _isChanging = true;
            try { return Grid.TryConfirmStorage(requestId, discard); }
            finally { _isChanging = false; }
        }
        public bool TryCancelStorage(string runId, string requestId)
        {
            if (!CanHandle(runId)) return false;
            _isChanging = true;
            try { return Grid.TryCancelStorage(requestId); }
            finally { _isChanging = false; }
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
