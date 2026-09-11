using System;

namespace OZGL2.Grid.Prototype
{
    /// <summary>Stage 연결 전 단독 검증용 진행 소유자. 실제 게임 화면에는 주입하지 않는다.</summary>
    public sealed class GridPrototypeFlow
    {
        public GridRunSession Session { get; }
        public GridPrototypeRewards Rewards { get; }
        public GridPrototypeFlow(GridRunSession session, GridPrototypeRewards rewards)
        { Session = session; Rewards = rewards; }
        public bool TryFinishBattle() => Session.TryFinishBattle(Session.RunId, Session.NextRound, Guid.NewGuid().ToString("N"));
        public bool TryChooseUnit(int option)
        {
            if (!Rewards.TryChoose(Session, option)) return false;
            return AllowPreparation();
        }
        public bool TryChooseExpansion()
        {
            if (!Session.TryChooseExpansion(Session.RunId, Session.PendingRewardId)) return false;
            return AllowPreparation();
        }
        private bool AllowPreparation() => Session.TryAllowPreparation(Session.RunId, Session.NextRound, true);
    }
}
