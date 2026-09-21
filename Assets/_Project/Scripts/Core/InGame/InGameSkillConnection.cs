using System;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Synergy;
using OZGL2.Skill;

namespace OZGL2.InGame
{
    /// <summary>스킬 시스템의 공개 수명주기 API를 코어루프의 전투 단계에 연결한다.</summary>
    public sealed class InGameSkillConnection : IInGameCombatParticipant
    {
        private readonly RealSynergySync _sync;
        private readonly SkillManager _manager;
        public bool IsCurrent => _sync != null && ReferenceEquals(_sync.SkillManager, _manager);

        public InGameSkillConnection(RealSynergySync sync)
        {
            _sync = sync != null ? sync : throw new ArgumentNullException(nameof(sync));
            _manager = sync.SkillManager;
        }

        public void SetCombatEnabled(bool isEnabled)
        {
            if (isEnabled && !IsCurrent) throw new InvalidOperationException("Skill run was replaced.");
            if (IsCurrent) _sync.SetCombatEnabled(isEnabled);
        }

        public Task PrepareAsync(InGameCombatContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!IsCurrent) throw new InvalidOperationException("Skill connection was destroyed or replaced.");
            _sync.PrepareCombat(context.KingPosition);
            return Task.CompletedTask;
        }

        public Task CleanupAsync(InGameCombatContext context)
        {
            StopCombat();
            return Task.CompletedTask;
        }

        public void StopCombat()
        {
            // 이전 씬의 늦은 종료가 새 게임의 입력이나 효과를 지우지 않게 한다.
            if (IsCurrent) _sync.StopCombat();
        }
    }
}
