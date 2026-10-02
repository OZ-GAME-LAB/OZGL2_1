using System.Threading;
using System.Threading.Tasks;
using OZGL2.InGame;
using UnityEngine;

/// <summary>
/// 보스 소환(BossSummonTag) 쪽 IInGameCombatParticipant 어댑터 (CoreLoop-Connections-TeamGuide.md 참고).
/// ProjectileCombatParticipant와 완전히 같은 모양 — 전투가 막힌 구간엔 새 소환이 안 나가게 하고,
/// 라운드 시작/종료 시 잔여 소환수를 정리한다. InGamePrototypeBootstrap의 Combat Participants
/// 배열에 이 컴포넌트를 등록해서 사용한다.
/// </summary>
public sealed class BossSummonCombatParticipant : MonoBehaviour, IInGameCombatParticipant
{
    /// <summary>준비/보상/종료 등 전투가 막힌 구간에는 보스가 새로 소환하지 않게 한다.</summary>
    public void SetCombatEnabled(bool isEnabled)
    {
        BossSummonTag.CombatEnabled = isEnabled;
    }

    /// <summary>이전 라운드에서 못 치우고 넘어온 잔여 소환수만 방어적으로 정리하고 시작한다.</summary>
    public Task PrepareAsync(InGameCombatContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        BossSummonTag.DestroyAllActive();
        return Task.CompletedTask;
    }

    /// <summary>이번 전투에서 소환됐던 증원을 전부 정리한다. 반복 호출·일부만 준비된 상태에서도 안전.</summary>
    public Task CleanupAsync(InGameCombatContext context)
    {
        BossSummonTag.DestroyAllActive();
        return Task.CompletedTask;
    }
}
