using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스(교황류)가 전투 중 직접 Instantiate로 소환한 증원 용사에 붙는 마커.
/// HeroPool/PooledStageBattle을 거치지 않는 소환이라 승패 판정(살아있는 적 수 집계)과는
/// 엮이지 않는다 — 대신 라운드 경계에서 잔여 소환수를 정리할 수 있도록 Projectile과 똑같은
/// static 레지스트리 패턴을 쓴다 (CoreLoop-Connections-TeamGuide.md의 IInGameCombatParticipant
/// 계약, BossSummonCombatParticipant가 이걸 통해 정리한다).
/// </summary>
public sealed class BossSummonTag : MonoBehaviour
{
    public static bool CombatEnabled { get; set; } = true;
    private static readonly HashSet<BossSummonTag> ActiveSummons = new HashSet<BossSummonTag>();

    public static int ActiveCount => ActiveSummons.Count;

    /// <summary>현재 살아있는 소환수를 전부 즉시 파괴한다(라운드 종료/전환 시 잔여물 정리용).</summary>
    public static void DestroyAllActive()
    {
        foreach (BossSummonTag tag in new List<BossSummonTag>(ActiveSummons))
        {
            if (tag != null)
            {
                Destroy(tag.gameObject);
            }
        }
        ActiveSummons.Clear();
    }

    private void OnEnable()
    {
        ActiveSummons.Add(this);
    }

    private void OnDisable()
    {
        ActiveSummons.Remove(this);
    }
}
