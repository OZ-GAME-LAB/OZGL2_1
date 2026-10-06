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
/// - 로드아웃(준비 단계가 시작되면 한 번만 지급, 합성으로 성급을 올린다):
///   · StrongestParty: 3성만으로 8x5 전체(40칸)를 빈틈없이 채워 자동 배치한 채로 시작 — 근접은 앞줄, 원거리·힐러는 뒷줄.
///   · Inventory: 직업별 3성·2성을 보관함에 넣어 두고 직접 배치.
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

    public enum LoadoutMode
    {
        StrongestParty,
        Inventory,
    }

    [Serializable]
    public struct PartySlot
    {
        public string unitId;
        public int count;
        public PartySlot(string unitId, int count) { this.unitId = unitId; this.count = count; }
    }

    [Header("로드아웃")]
    [SerializeField] private bool grantLoadout = true;
    [SerializeField] private LoadoutMode loadout = LoadoutMode.StrongestParty;
    [Tooltip("StrongestParty: 3성 유닛 구성. 3성은 4칸이라 합계 10기여야 8x5=40칸이 빈틈없이 채워진다.")]
    [SerializeField] private PartySlot[] strongestParty =
    {
        new PartySlot("M_WAR_01", 2), new PartySlot("M_SHD_01", 2), new PartySlot("M_ROG_01", 1),
        new PartySlot("M_ARC_01", 2), new PartySlot("M_MAG_01", 2), new PartySlot("M_HEL_01", 1),
    };

    // 앞줄(적이 오는 위쪽) 우선 / 뒷줄 우선 / 중간 줄 우선 직업 순서 — 배치 탐색이 이 순서로 시도해서 자연스러운 진형이 나온다.
    private static readonly string[] FrontOrder = { "SHD", "WAR", "ROG", "ARC", "MAG", "HEL" };
    private static readonly string[] BackOrder = { "HEL", "MAG", "ARC", "ROG", "WAR", "SHD" };
    private static readonly string[] MiddleOrder = { "ARC", "ROG", "WAR", "MAG", "SHD", "HEL" };

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
            IReadOnlyList<UnitDefinition> catalogUnits = _bootstrap.Config.Catalog.CreateUnits();
            if (loadout == LoadoutMode.StrongestParty) BuildStrongestParty(grid, catalogUnits);
            else GrantUnits(grid, catalogUnits);
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


    // ── 가장 쎈 파티 ─────────────────────────────────────────────────

    private struct Placement
    {
        public string InstanceId;
        public Vector2Int Anchor;
        public int Rotation;
        public bool Mirrored;
    }

    private void BuildStrongestParty(GridManager grid, IReadOnlyList<UnitDefinition> catalogUnits)
    {
        // 시작 때 이미 깔려 있는 1성 유닛은 보관함으로 돌려서 첫 3성의 재료로 쓴다(남아 있으면 칸을 차지한다).
        var spareBases = new Dictionary<string, List<string>>();
        foreach (UnitPlacement unit in new List<UnitPlacement>(grid.Units))
        {
            if (unit.IsPlaced && unit.StarLevel == 1 && grid.TryReturnUnitToTray(unit.InstanceId))
            {
                if (!spareBases.TryGetValue(unit.Definition.Id, out List<string> list)) spareBases[unit.Definition.Id] = list = new List<string>();
                list.Add(unit.InstanceId);
            }
        }

        // 구성대로 3성을 만든다(1성 4개 → 합성 3번). 남는 재료 없이 정확히 4개씩 쓴다.
        var created = new List<(UnitDefinition definition, string instanceId)>();
        foreach (PartySlot slot in strongestParty)
        {
            UnitDefinition definition = FindUnit(catalogUnits, slot.unitId);
            if (definition == null) continue;

            for (int n = 0; n < slot.count; n++)
            {
                var bases = new string[4];
                for (int k = 0; k < 4; k++)
                {
                    if (spareBases.TryGetValue(definition.Id, out List<string> spare) && spare.Count > 0)
                    {
                        bases[k] = spare[spare.Count - 1];
                        spare.RemoveAt(spare.Count - 1);
                    }
                    else
                    {
                        bases[k] = $"boss_test_{definition.Id}_{created.Count}_{k}";
                        grid.AddUnit(bases[k], definition);
                    }
                }

                if (grid.TryFuseUnits(bases[1], bases[0]) && grid.TryFuseUnits(bases[3], bases[2]) && grid.TryFuseUnits(bases[2], bases[0]))
                {
                    created.Add((definition, bases[0]));
                }
                else
                {
                    Debug.LogWarning("[BossSelectTestHarness] 3성 합성 실패: " + definition.Id);
                }
            }
        }

        List<Placement> plan = PlanLayout(grid, created);
        if (plan == null)
        {
            Debug.LogWarning("[BossSelectTestHarness] 빈틈없는 배치를 찾지 못해 3성 유닛을 보관함에 둡니다(구성 칸 수 확인).");
            return;
        }

        foreach (Placement placement in plan)
        {
            if (!PlaceUnit(grid, placement))
            {
                Debug.LogWarning("[BossSelectTestHarness] 배치 실패: " + placement.InstanceId);
            }
        }
    }

    private static UnitDefinition FindUnit(IReadOnlyList<UnitDefinition> units, string id)
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i].Id == id) return units[i];
        }
        return null;
    }

    private static bool PlaceUnit(GridManager grid, Placement placement)
    {
        if (!grid.BeginUnitDrag(placement.InstanceId)) return false;

        // 미리보기 방향을 (회전 0, 반전 없음)으로 되돌린 뒤 원하는 방향을 만든다. 저장 규격은 "반전 후 회전"이다.
        if (grid.PreviewIsMirrored) grid.MirrorPreview();
        for (int guard = 0; grid.PreviewRotation != 0 && guard < 4; guard++) grid.RotatePreview();
        if (placement.Mirrored) grid.MirrorPreview();
        for (int r = 0; r < placement.Rotation; r++) grid.RotatePreview();

        grid.MovePreview(placement.Anchor);
        if (grid.CommitPreview()) return true;

        grid.CancelDrag();
        return false;
    }

    /// <summary>
    /// 3성 유닛들의 발판 모양을 8x5 바닥에 빈틈없이 맞추는 배치를 찾는다(회전·반전 포함, 완전 탐색).
    /// 비어 있는 칸을 앞줄(위)부터 훑으면서 줄 위치에 맞는 직업 순서로 시도하기 때문에, 근접은 앞줄·원거리/힐러는 뒷줄에 모인다.
    /// </summary>
    private static List<Placement> PlanLayout(GridManager grid, List<(UnitDefinition definition, string instanceId)> units)
    {
        Vector2Int size = grid.Definition.MaximumSize;
        var occupied = new HashSet<Vector2Int>(grid.GetOccupiedCells());
        var result = new List<Placement>();
        var used = new bool[units.Count];

        bool TryFill()
        {
            Vector2Int? first = null;
            for (int y = size.y - 1; y >= 0 && first == null; y--)
            {
                for (int x = 0; x < size.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (grid.HasFloor(cell) && !occupied.Contains(cell)) { first = cell; break; }
                }
            }

            if (first == null) return true;

            Vector2Int target = first.Value;
            foreach (int index in CandidateOrder(units, used, target.y, size.y))
            {
                FootprintDefinition shape = units[index].definition.GetFootprint(3);
                var tried = new HashSet<string>();
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    for (int mirror = 0; mirror < 2; mirror++)
                    {
                        Vector2Int[] offsets = shape.GetCells(Vector2Int.zero, rotation, mirror == 1);
                        if (!tried.Add(ShapeKey(offsets))) continue; // 대칭이라 같은 모양이면 건너뜀

                        foreach (Vector2Int pivot in offsets)
                        {
                            Vector2Int anchor = target - pivot;
                            if (!Fits(grid, occupied, offsets, anchor)) continue;

                            foreach (Vector2Int offset in offsets) occupied.Add(anchor + offset);
                            used[index] = true;
                            result.Add(new Placement { InstanceId = units[index].instanceId, Anchor = anchor, Rotation = rotation, Mirrored = mirror == 1 });
                            if (TryFill()) return true;

                            result.RemoveAt(result.Count - 1);
                            used[index] = false;
                            foreach (Vector2Int offset in offsets) occupied.Remove(anchor + offset);
                        }
                    }
                }
            }

            return false;
        }

        return TryFill() ? result : null;
    }

    private static bool Fits(GridManager grid, HashSet<Vector2Int> occupied, Vector2Int[] offsets, Vector2Int anchor)
    {
        foreach (Vector2Int offset in offsets)
        {
            Vector2Int cell = anchor + offset;
            if (!grid.Definition.Contains(cell) || !grid.HasFloor(cell) || occupied.Contains(cell)) return false;
        }
        return true;
    }

    private static string ShapeKey(Vector2Int[] cells)
    {
        var sorted = new List<Vector2Int>(cells);
        sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return string.Join(";", sorted);
    }

    /// <summary>아직 안 쓴 유닛을 줄 위치에 맞는 직업 순서(앞줄=근접 우선, 뒷줄=원거리·힐러 우선)로 늘어놓는다.</summary>
    private static IEnumerable<int> CandidateOrder(List<(UnitDefinition definition, string instanceId)> units, bool[] used, int row, int rows)
    {
        string[] order = row >= rows - 2 ? FrontOrder : row <= 1 ? BackOrder : MiddleOrder;
        var indices = new List<int>();
        for (int i = 0; i < units.Count; i++)
        {
            if (!used[i]) indices.Add(i);
        }

        indices.Sort((a, b) =>
        {
            int byRole = RoleRank(units[a].definition.Id, order).CompareTo(RoleRank(units[b].definition.Id, order));
            return byRole != 0 ? byRole : a.CompareTo(b);
        });
        return indices;
    }

    private static int RoleRank(string unitId, string[] order)
    {
        for (int i = 0; i < order.Length; i++)
        {
            if (unitId.Contains(order[i])) return i;
        }
        return order.Length;
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
