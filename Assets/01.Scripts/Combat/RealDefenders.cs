using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using OZGL2.Grid;
using OZGL2.Stage;

/// <summary>라운드 확정 배치의 위치·성급·생존 개체를 실제 전투에 반영한다.</summary>
public sealed class RealDefenders : IStageDefenders, IDisposable
{
    private readonly Func<GridRunSession> _sessionProvider;
    private readonly DemonArmyCatalog _catalog;
    private readonly Transform _spawnRoot;
    private readonly GridWorldMapping _mapping;
    private readonly Dictionary<string, UnitBase> _spawned = new Dictionary<string, UnitBase>();
    private readonly Dictionary<string, UnitBase> _spawnedPrefabs = new Dictionary<string, UnitBase>();
    private bool _isDisposed;

    public RealDefenders(Func<GridRunSession> gridSessionProvider, DemonArmyCatalog catalog, Transform spawnRoot,
        Vector3 worldOrigin, float cellWorldSize)
    {
        _sessionProvider = gridSessionProvider;
        _catalog = catalog;
        _spawnRoot = spawnRoot;
        if (!float.IsFinite(cellWorldSize) || cellWorldSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellWorldSize));
        _mapping = new GridWorldMapping(worldOrigin, Vector3.right * cellWorldSize, Vector3.up * cellWorldSize);
    }
    public int AliveCount
    {
        get
        {
            int count = 0;
            foreach (var unit in _spawned.Values)
                if (unit != null && unit.gameObject.activeInHierarchy && unit.currentState != UnitState.Dead) count++;
            return count;
        }
    }
    public Task PrepareRoundAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_isDisposed) throw new ObjectDisposedException(nameof(RealDefenders));
        var session = _sessionProvider?.Invoke();
        var deployment = session?.Deployment;
        if (session == null || session.IsEnded || deployment == null || _catalog == null)
            throw new InvalidOperationException("Active grid deployment and demon army catalog are required.");

        // 누락된 콘텐츠 때문에 일부만 배치한 상태로 전투가 시작되지 않게 사전 검증한다.
        foreach (var unit in deployment.Units)
        {
            var prefab = _catalog.FindPrefab(unit.ContentId, unit.StarLevel);
            if (prefab == null || prefab.statData == null)
                throw new InvalidOperationException("Missing demon army prefab/stat data: " + unit.ContentId);
        }
        UnitRegistry.KingWorldPosition = _mapping.GetWorldPosition(deployment.KingAnchor);
        var retained = new HashSet<string>();
        foreach (var unit in deployment.Units) retained.Add(unit.InstanceId);
        foreach (var id in new List<string>(_spawned.Keys))
            if (!retained.Contains(id)) { Release(_spawned[id]); _spawned.Remove(id); _spawnedPrefabs.Remove(id); }

        foreach (var unit in deployment.Units)
        {
            UnitBase instance;
            var prefab = _catalog.FindPrefab(unit.ContentId, unit.StarLevel);
            if (!_spawned.TryGetValue(unit.InstanceId, out instance) || instance == null ||
                !_spawnedPrefabs.TryGetValue(unit.InstanceId, out var previousPrefab) || previousPrefab != prefab)
            {
                // 성급 외형이 바뀔 때만 교체한다. 동일 외형은 기존 개체와 체력을 유지한다.
                Release(instance);
                instance = UnityEngine.Object.Instantiate(prefab,
                    _mapping.GetWorldPosition(unit.Anchor), Quaternion.identity, _spawnRoot);
                _spawned[unit.InstanceId] = instance;
                _spawnedPrefabs[unit.InstanceId] = prefab;
            }
            instance.transform.position = _mapping.GetWorldPosition(unit.Anchor);
            instance.hasMoveTarget = false;
            instance.currentTarget = null;
            if (instance.statData.starLevel != unit.StarLevel) instance.ApplyStarLevel(unit.StarLevel);
            if (instance.currentState == UnitState.Dead) instance.Revive();
            else instance.SetState(UnitState.Idle);
        }
        return Task.CompletedTask;
    }
    private static void Release(UnitBase unit)
    {
        if (unit == null) return;
        // Destroy는 프레임 끝에 처리되므로 레지스트리에서 먼저 제외한다.
        unit.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(unit.gameObject);
    }
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        foreach (var unit in _spawned.Values) Release(unit);
        _spawned.Clear();
        _spawnedPrefabs.Clear();
    }
}
