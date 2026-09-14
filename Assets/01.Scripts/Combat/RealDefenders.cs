using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using OZGL2.Grid;
using OZGL2.Stage;

/// <summary>
/// IStageDefenders의 실제 구현.
/// 매 라운드 시작(PrepareRoundAsync)마다: ① 그리드의 확정 배치(GridDeploymentSnapshot)를 읽어서
/// 아직 안 만든 마왕군을 DemonArmyCatalog 기준으로 스폰하고, ② 이전 라운드에 죽은 마왕군을 부활(4.3절)시킨다.
/// PooledStageBattle(김건·준기 파트)의 RunRoundAsync()가 이 AliveCount로 승패를 판정한다.
/// </summary>
public sealed class RealDefenders : IStageDefenders
{
    // GridRunSession을 생성 시점에 바로 캡처하면 아직 BeginAsync 전이라 null일 수 있어서,
    // 실제로 쓰는 시점(PrepareRoundAsync)마다 다시 조회하도록 지연 평가(Func)로 받는다.
    private readonly Func<GridRunSession> gridSessionProvider;
    private readonly DemonArmyCatalog catalog;
    private readonly Transform spawnRoot;
    private readonly Vector3 worldOrigin;
    private readonly float cellWorldSize;
    private readonly Dictionary<string, UnitBase> spawnedByInstanceId = new Dictionary<string, UnitBase>();

    /// <summary>
    /// cellWorldSize: 그리드 1칸 = 몇 월드 유닛인지. ⚠ 준기·김건과 아직 최종 협의 전이라 기본값(1)을
    /// 임시로 씀 — 실제 값 정해지면 호출부(RealStageBattleFactory)에서 넘기는 값만 바꾸면 된다.
    /// </summary>
    public RealDefenders(Func<GridRunSession> gridSessionProvider, DemonArmyCatalog catalog, Transform spawnRoot,
        Vector3 worldOrigin, float cellWorldSize)
    {
        this.gridSessionProvider = gridSessionProvider;
        this.catalog = catalog;
        this.spawnRoot = spawnRoot;
        this.worldOrigin = worldOrigin;
        this.cellWorldSize = cellWorldSize <= 0f ? 1f : cellWorldSize;
    }

    public int AliveCount => UnitRegistry.GetAliveCount(UnitSide.DemonArmy);

    public Task PrepareRoundAsync(CancellationToken cancellationToken)
    {
        GridRunSession session = gridSessionProvider?.Invoke();
        GridWorldMapping mapping = new GridWorldMapping(worldOrigin, Vector3.right * cellWorldSize, Vector3.up * cellWorldSize);

        UpdateKingWorldPosition(session, mapping);
        SyncDeployment(session, mapping);
        ReviveDead();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 마왕 월드 좌표를 계산해서 UnitRegistry에 채워둔다. PooledStageBattle(팀원 코드)이 용사 스폰만 하고
    /// 이동 명령은 안 주기 때문에, 용사가 풀에서 나올 때(UnitBase.ResetForSpawn) 이 값으로 자동으로
    /// 마왕을 향해 걷기 시작하도록 연결해뒀다 (4.1절).
    /// </summary>
    private static void UpdateKingWorldPosition(GridRunSession session, GridWorldMapping mapping)
    {
        if (session == null)
        {
            return;
        }

        UnitRegistry.KingWorldPosition = mapping.GetWorldPosition(session.Grid.Definition.KingAnchor);
    }

    /// <summary>확정된 배치 중 아직 실제 GameObject로 안 만든 유닛을 스폰한다. 이미 스폰된 건 건너뜀.</summary>
    private void SyncDeployment(GridRunSession session, GridWorldMapping mapping)
    {
        GridDeploymentSnapshot deployment = session?.Deployment;
        if (deployment == null || catalog == null)
        {
            return;
        }

        foreach (GridDeployedUnit unit in deployment.Units)
        {
            if (spawnedByInstanceId.ContainsKey(unit.InstanceId))
            {
                continue;
            }

            UnitBase prefab = catalog.FindPrefab(unit.ContentId);
            if (prefab == null)
            {
                Debug.LogWarning($"[RealDefenders] '{unit.ContentId}'에 해당하는 프리팹을 DemonArmyCatalog에서 못 찾음.");
                continue;
            }

            Vector3 worldPosition = AverageCellWorldPosition(mapping, unit.Cells);
            UnitBase instance = UnityEngine.Object.Instantiate(prefab, worldPosition, Quaternion.identity,
                spawnRoot != null ? spawnRoot : null);
            spawnedByInstanceId[unit.InstanceId] = instance;
        }
    }

    /// <summary>여러 칸을 차지하는 유닛(향후 대비)도 처리하도록 차지한 칸들의 중심 좌표를 스폰 위치로 쓴다.</summary>
    private static Vector3 AverageCellWorldPosition(GridWorldMapping mapping, IReadOnlyList<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
        {
            return mapping.GetWorldPosition(Vector2.zero);
        }

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < cells.Count; i++)
        {
            sum += mapping.GetWorldPosition(new Vector2(cells[i].x, cells[i].y));
        }

        return sum / cells.Count;
    }

    private void ReviveDead()
    {
        var units = UnitRegistry.GetUnits(UnitSide.DemonArmy);
        for (int i = 0; i < units.Count; i++)
        {
            UnitBase unit = units[i];
            if (unit != null && unit.currentState == UnitState.Dead)
            {
                unit.Revive();
            }
        }
    }
}
