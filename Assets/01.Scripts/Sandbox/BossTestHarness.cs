using UnityEngine;
using OZGL2.InGame;

/// <summary>
/// JOB_SEJIN 테스트 씬 전용 개발용 오버레이. 보스 3종(화염기사/팔라딘/용인족 마법사)을
/// 빠르게 반복 확인하기 위해 배속 조절 버튼과 현재 라운드 상태만 표시한다.
/// 스테이지 진행/보상 흐름 자체는 기존 InGameDummyView/InGameAugmentDummyView가 그대로 담당 —
/// 이 컴포넌트는 그 위에 얹는 순수 표시/배속 전용 오버레이라 다른 로직을 건드리지 않는다.
/// </summary>
public class BossTestHarness : MonoBehaviour
{
    private static readonly float[] SpeedOptions = { 0.5f, 1f, 2f, 4f, 8f };
    private InGamePrototypeBootstrap _bootstrap;

    private void OnGUI()
    {
        if (_bootstrap == null)
        {
            _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
        }

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
