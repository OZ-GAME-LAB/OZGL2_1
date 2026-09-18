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
        out HeroPool pool, Action<IDisposable> ownDefenders = null, IStageBattleLifecycle lifecycle = null)
    {
        pool = null;
        if (heroCatalog == null || demonArmyCatalog == null || heroSpawnRoot == null || demonArmySpawnRoot == null || gridSessionProvider == null)
            throw new ArgumentException("Battle catalogs, roots and grid session provider are required.");
        // 좌표 오류 때문에 풀을 만든 뒤 실패하지 않도록 생성 전에 검증한다.
        if (!float.IsFinite(cellWorldSize) || cellWorldSize <= 0 ||
            !float.IsFinite(heroSpawnPosition.x) || !float.IsFinite(heroSpawnPosition.y) || !float.IsFinite(heroSpawnPosition.z))
            throw new ArgumentException("Battle coordinates and cell size must be finite and valid.");
        _ = new GridWorldMapping(gridWorldOrigin, Vector3.right * cellWorldSize, Vector3.up * cellWorldSize);
        HeroPool createdPool = null;
        RealDefenders defenders = null;
        try
        {
            createdPool = new HeroPool(heroCatalog.CreateSnapshot(), heroSpawnRoot);
            defenders = new RealDefenders(gridSessionProvider, demonArmyCatalog, demonArmySpawnRoot, gridWorldOrigin, cellWorldSize);
            var battle = new PooledStageBattle(createdPool, defenders, heroSpawnPosition, lifecycle);
            ownDefenders?.Invoke(defenders);
            pool = createdPool;
            return battle;
        }
        catch (Exception error)
        {
            var errors = new System.Collections.Generic.List<Exception> { error };
            try { defenders?.Dispose(); } catch (Exception cleanup) { errors.Add(cleanup); }
            try { createdPool?.Dispose(); } catch (Exception cleanup) { errors.Add(cleanup); }
            if (errors.Count > 1) throw new AggregateException("Battle assembly failed; resources were released.", errors);
            throw;
        }
    }
}
