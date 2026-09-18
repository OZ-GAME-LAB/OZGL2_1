using System.Threading;
using System.Threading.Tasks;
using OZGL2.InGame;
using UnityEngine;

/// <summary>
/// 투사체(Projectile) 쪽 IInGameCombatParticipant 어댑터 (CoreLoop-Connections-TeamGuide.md 참고).
/// 스킬 쪽(SkillManager/SkillExecutor/SkillBarUI 등)은 담당자가 필요한 공개 API를 열어준 뒤
/// 별도 어댑터로 등록해야 한다 — 이 컴포넌트는 발사체만 책임진다.
/// InGamePrototypeBootstrap의 Combat Participants 배열에 이 컴포넌트를 등록해서 사용한다.
/// </summary>
public sealed class ProjectileCombatParticipant : MonoBehaviour, IInGameCombatParticipant
{
    /// <summary>준비/보상/종료 등 전투가 막힌 구간에는 새 발사체가 나가지 않게 한다.</summary>
    public void SetCombatEnabled(bool isEnabled)
    {
        Projectile.CombatEnabled = isEnabled;
    }

    /// <summary>
    /// 발사체는 매 프레임 살아있는 타겟을 직접 조회해서 유도하므로 마왕 위치 같은 캐시된 발사
    /// 기준이 없다 — 이전 라운드에서 못 치우고 넘어온 잔여물만 방어적으로 정리하고 시작한다.
    /// </summary>
    public Task PrepareAsync(InGameCombatContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Projectile.DestroyAllActive();
        return Task.CompletedTask;
    }

    /// <summary>이번 전투에서 날아다니던 발사체를 전부 정리한다. 반복 호출·일부만 준비된 상태에서도 안전.</summary>
    public Task CleanupAsync(InGameCombatContext context)
    {
        Projectile.DestroyAllActive();
        return Task.CompletedTask;
    }
}
