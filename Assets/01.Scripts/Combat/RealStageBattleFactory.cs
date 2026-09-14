using System;
using UnityEngine;
using OZGL2.Grid;
using OZGL2.Stage;

/// <summary>
/// 실제 IStageBattle(PooledStageBattle) 조립. InGame 조립 루트(InGamePrototypeBootstrap 등)가
/// Dummy(ManualStageServices) 대신 이걸로 battle을 교체해서 쓴다.
/// HeroPool은 IDisposable이라, 반환된 pool은 호출자가 반드시 정리(StageRunHost.OwnResource 등)해야 한다.
/// </summary>
public static class RealStageBattleFactory
{
    /// <summary>
    /// gridSessionProvider: 호출 시점마다 현재 GridRunSession을 조회하는 델리게이트.
    /// 조립 시점엔 아직 BeginAsync 전이라 세션이 null일 수 있어서 지연 평가로 받는다 (예: () => bootstrap.GridSession).
    /// </summary>
    public static IStageBattle Create(
        HeroPoolCatalogSO heroCatalog,
        Transform heroSpawnRoot,
        Vector3 heroSpawnPosition,
        Func<GridRunSession> gridSessionProvider,
        DemonArmyCatalog demonArmyCatalog,
        Transform demonArmySpawnRoot,
        Vector3 gridWorldOrigin,
        float cellWorldSize,
        out HeroPool pool)
    {
        pool = new HeroPool(heroCatalog.CreateSnapshot(), heroSpawnRoot);
        var defenders = new RealDefenders(gridSessionProvider, demonArmyCatalog, demonArmySpawnRoot, gridWorldOrigin, cellWorldSize);
        return new PooledStageBattle(pool, defenders, heroSpawnPosition);
    }
}
