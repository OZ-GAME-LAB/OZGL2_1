using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using OZGL2.Grid;
using OZGL2.InGame;

/// <summary>
/// JOB_SEJIN_BossTest 씬 전용 개발용 하니스. 인스펙터의 Boss 값(1~5)으로 어떤 보스를 상대할지 고르고,
/// 시작하자마자 8x5 전체를 발판으로 깔고 직업별 3성·2성 유닛을 보관함에 채워 넣는다.
/// - 보스 선택: bossConfigs[boss-1]의 InGamePrototypeConfigSO(어려움 50 스테이지의 해당 보스 라운드 — 졸개 구성+보스, 그 라운드 배율 — 를 3번 반복하는 스테이지)를
///   씬의 InGamePrototypeBootstrap보다 먼저(Awake) 끼워 넣는다. 에셋은 건드리지 않는다.
/// - 로드아웃: 준비 단계가 시작되면 한 번만 지급(AddUnit→합성으로 성급을 올린다). 보관함 용량은 8x5용 GridSettings가 넉넉히 잡아 둔다.
/// 스테이지 진행/보상 흐름은 기존 InGame 로직이 그대로 담당한다.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class BossSelectTestHarness : MonoBehaviour
{
    public enum BossChoice
    {
        Boss1_FlameKnight = 1,
        Boss2_Paladin = 2,
        Boss3_DragonMage = 3,
        Boss4 = 4,
        Boss5 = 5,
    }

    private static readonly float[] SpeedOptions = { 0.5f, 1f, 2f, 4f, 8f };

    [Header("보스 선택 (플레이 전에 바꾸기 — 1~5)")]
    [SerializeField] private BossChoice boss = BossChoice.Boss1_FlameKnight;
    [Tooltip("보스 1~5 순서대로 InGamePrototypeConfig_BossTest_1~5 (각각 어려움 10·20·30·40·50라운드)")]
    [SerializeField] private InGamePrototypeConfigSO[] bossConfigs = new InGamePrototypeConfigSO[5];

    [Header("로드아웃")]
    [SerializeField] private bool grantLoadout = true;

    private InGamePrototypeBootstrap _bootstrap;
    private bool _loadoutGranted;

    private void Awake()
    {
        _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
        int index = (int)boss - 1;
        if (_bootstrap == null || bossConfigs == null || index < 0 || index >= bossConfigs.Length || bossConfigs[index] == null)
        {
            Debug.LogWarning("[BossSelectTestHarness] 보스 설정을 적용하지 못했습니다(부트스트랩/Config 연결 확인).");
            return;
        }

        // 부트스트랩의 _config는 private SerializeField라 개발용 하니스에서만 리플렉션으로 교체한다.
        FieldInfo field = typeof(InGamePrototypeBootstrap).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            Debug.LogWarning("[BossSelectTestHarness] InGamePrototypeBootstrap._config 필드를 찾지 못했습니다(이름이 바뀌었는지 확인).");
            return;
        }

        field.SetValue(_bootstrap, bossConfigs[index]);
    }

    private void Update()
    {
        if (_loadoutGranted || !grantLoadout)
        {
            return;
        }

        if (_bootstrap == null)
        {
            _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
        }

        GridManager grid = _bootstrap != null ? _bootstrap.GridSession?.Grid : null;
        if (grid == null || _bootstrap.Config == null || grid.Phase != eGridPhase.PREPARATION)
        {
            return; // 세션/그리드가 준비되기 전에는 대기 — 다음 프레임에 다시 시도.
        }

        _loadoutGranted = true;
        try
        {
            FillFloorWithBlocks(grid, _bootstrap.Config.Catalog.CreateInitialBlock());
            GrantUnits(grid, _bootstrap.Config.Catalog.CreateUnits());
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[BossSelectTestHarness] 로드아웃 지급 중 오류: " + exception.Message);
        }
    }

    /// <summary>
    /// 유닛은 발판 위에만 놓을 수 있어서, 이미 깔린 발판에서 이어지는 순서(BFS)로 빈 칸마다 1칸 발판을 놓아
    /// 바닥 전체(8x5)를 덮는다. 발판은 연결돼 있어야 해서 바깥쪽으로 퍼지는 순서로 놓는다.
    /// </summary>
    private static void FillFloorWithBlocks(GridManager grid, FootprintDefinition singleCellBlock)
    {
        var covered = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        foreach (BlockPlacement block in grid.Blocks)
        {
            if (!block.IsPlaced) continue;
            foreach (Vector2Int cell in block.GetCells())
            {
                if (covered.Add(cell)) queue.Enqueue(cell);
            }
        }

        if (queue.Count == 0)
        {
            return; // 시작 발판이 없으면 이어 붙일 기준이 없다.
        }

        int placedCount = 0;
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int direction in GridPlacementRules.Directions)
            {
                Vector2Int next = current + direction;
                if (!grid.Definition.Contains(next) || !grid.HasFloor(next) || !covered.Add(next))
                {
                    continue;
                }

                string id = "boss_test_floor_" + placedCount++;
                grid.AddBlock(id, singleCellBlock.Id, singleCellBlock);
                if (!grid.BeginBlockDrag(id))
                {
                    return;
                }

                grid.MovePreview(next);
                if (!grid.CommitPreview())
                {
                    grid.CancelDrag();
                    return;
                }

                queue.Enqueue(next);
            }
        }
    }

    /// <summary>
    /// 직업(카탈로그 유닛)마다 1성 6개를 넣고 합성해서 3성 1개 + 2성 1개만 남긴다.
    /// (3성 = 1성 4개, 2성 = 1성 2개). 합성은 실제 규칙(GridManager.TryFuseUnits)을 그대로 쓴다.
    /// </summary>
    private static void GrantUnits(GridManager grid, IReadOnlyList<UnitDefinition> catalogUnits)
    {
        for (int i = 0; i < catalogUnits.Count; i++)
        {
            UnitDefinition definition = catalogUnits[i];
            string prefix = "boss_test_" + definition.Id + "_";
            for (int k = 0; k < 6; k++)
            {
                grid.AddUnit(prefix + k, definition);
            }

            // 2성 2개(0,2) → 3성(0). 남은 1성 2개(4,5) → 2성(4).
            bool ok = grid.TryFuseUnits(prefix + 1, prefix + 0)
                      && grid.TryFuseUnits(prefix + 3, prefix + 2)
                      && grid.TryFuseUnits(prefix + 2, prefix + 0)
                      && grid.TryFuseUnits(prefix + 5, prefix + 4);
            if (!ok)
            {
                Debug.LogWarning("[BossSelectTestHarness] 합성 실패: " + definition.Id);
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 290, 110));
        GUILayout.Box($"보스 테스트 — {boss}");

        if (_bootstrap != null && _bootstrap.Stage != null)
        {
            GUILayout.Label($"라운드 {_bootstrap.Stage.CurrentRoundNumber}/{_bootstrap.Stage.TotalRounds} — {_bootstrap.Stage.State}");
        }

        GUILayout.Label($"배속: {Time.timeScale:0.0}x");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < SpeedOptions.Length; i++)
        {
            float speed = SpeedOptions[i];
            if (GUILayout.Button($"{speed:0.#}x", GUILayout.Width(45)))
            {
                Time.timeScale = speed;
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f; // 다른 씬/테스트에 배속이 넘어가 남아있지 않도록 원복
    }
}
