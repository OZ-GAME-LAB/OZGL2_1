using System;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Progression;
using OZGL2.Stage;

namespace OZGL2.InGame
{
    /// <summary>전투 중 실제 지급 XP만 기록한다. 풀 카탈로그의 기본 XP와 배율/추가 지급을 혼동하지 않는다.</summary>
    public sealed class InGameExperienceBattle : IStageBattle
    {
        private readonly IStageBattle _battle;
        private readonly MawangLevel _level;

        public InGameExperienceBattle(IStageBattle battle, MawangLevel level)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _level = level ?? throw new ArgumentNullException(nameof(level));
        }

        public async Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken cancellationToken)
        {
            long before = _level.TotalEarnedXp;
            try
            {
                var result = await _battle.RunRoundAsync(round, cancellationToken);
                return new RoundResult(result.Outcome, _level.TotalEarnedXp - before);
            }
            catch (StageBattleCleanupException exception)
            {
                throw new StageBattleCleanupException(
                    new RoundResult(exception.ConfirmedResult.Outcome, _level.TotalEarnedXp - before), exception.InnerExceptions);
            }
        }
    }
}
