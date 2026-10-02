using System.Collections.Generic;
using UnityEngine;
using OZGL2.Grid;
using OZGL2.InGame;

/// <summary>
/// JOB_SEJIN 테스트 씬 전용 개발용 오버레이. 보스 3종(화염기사/팔라딘/용인족 마법사)을
/// 빠르게 반복 확인하기 위해 배속 조절 버튼과 현재 라운드 상태만 표시한다.
/// 스테이지 진행/보상 흐름 자체는 기존 InGameDummyView/InGameAugmentDummyView가 그대로 담당 —
/// 이 컴포넌트는 그 위에 얹는 순수 표시/배속 전용 오버레이라 다른 로직을 건드리지 않는다.
/// 추가로, 실제로는 9라운드를 깨고 와야 쌓였을 유닛/발판을 시작하자마자 보관함에 한 번 넣어줘서
/// "10라운드 시점 플레이어" 상태를 흉내낸다(초기 배치 유닛 1개뿐인 맨땅 시작 방지).
/// </summary>
public class BossTestHarness : MonoBehaviour
{
    private static readonly float[] SpeedOptions = { 0.5f, 1f, 2f, 4f, 8f };
    // 초기 배치 유닛(M_WAR_01)은 이미 그리드에 깔려 있으니 제외하고, 나머지 5개 직업을 한 벌씩 지급한다.
    private static readonly string[] Round10LoadoutUnitIds = { "M_SHD_01", "M_ARC_01", "M_MAG_01", "M_ROG_01", "M_HEL_01" };

    private InGamePrototypeBootstrap _bootstrap;
    private bool _loadoutGranted;

    private void Update()
    {
        if (_loadoutGranted)
        {
            return;
        }

        if (_bootstrap == null)
        {
            _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
        }

        var grid = _bootstrap != null ? _bootstrap.GridSession?.Grid : null;
        if (grid == null || _bootstrap.Config == null || grid.Phase != eGridPhase.PREPARATION)
        {
            return; // 세션/그리드가 준비되기 전에는 대기 — 다음 프레임에 다시 시도.
        }

        GrantRound10Loadout(grid, _bootstrap.Config.Catalog.CreateUnits());
        _loadoutGranted = true;
    }

    /// <summary>직업별 유닛 1개 + 그 유닛 전용 1성 발판 1개를 보관함에 넣는다. 보관함 용량(10칸)을 넘지 않게 체크.</summary>
    private void GrantRound10Loadout(GridManager grid, IReadOnlyList<UnitDefinition> catalogUnits)
    {
        int index = 0;
        foreach (string unitId in Round10LoadoutUnitIds)
        {
            UnitDefinition definition = null;
            for (int i = 0; i < catalogUnits.Count; i++)
            {
                if (catalogUnits[i].Id == unitId)
                {
                    definition = catalogUnits[i];
                    break;
                }
            }
            if (definition == null)
            {
                continue; // 캐탈로그에 없는 ID면 조용히 건너뜀(테스트 전용 코드라 상황 변화에 과민 반응할 필요 없음)
            }

            if (grid.StoredCount >= grid.Definition.StorageCapacity)
            {
                break;
            }
            grid.AddUnit($"boss_test_unit_{index}", definition);

            if (grid.StoredCount >= grid.Definition.StorageCapacity)
            {
                break;
            }
            grid.AddBlock($"boss_test_block_{index}", definition.RewardBlockId, definition.Footprint);

            index++;
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 290, 90));
        GUILayout.Box("보스 테스트 (StageBossTest)");

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
