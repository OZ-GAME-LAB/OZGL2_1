using System;

namespace OZGL2.Stage
{
    public enum eRoundOutcome { ONGOING, VICTORY, DEFEAT, SIMULTANEOUS }
    public readonly struct BattleProgress
    {
        public bool IsSpawningComplete { get; }
        public int AliveHeroCount { get; }
        public int AliveDefenderCount { get; }
        public BattleProgress(bool isSpawningComplete, int aliveHeroCount, int aliveDefenderCount)
        {
            if (aliveHeroCount < 0 || aliveDefenderCount < 0) throw new ArgumentOutOfRangeException();
            IsSpawningComplete = isSpawningComplete;
            AliveHeroCount = aliveHeroCount;
            AliveDefenderCount = aliveDefenderCount;
        }
    }
    public static class RoundCompletionEvaluator
    {
        public static eRoundOutcome Evaluate(BattleProgress progress)
        {
            bool isVictory = progress.IsSpawningComplete && progress.AliveHeroCount == 0;
            bool isDefeat = progress.AliveDefenderCount == 0;
            if (isVictory && isDefeat) return eRoundOutcome.SIMULTANEOUS;
            if (isDefeat) return eRoundOutcome.DEFEAT;
            return isVictory ? eRoundOutcome.VICTORY : eRoundOutcome.ONGOING;
        }
    }
}
