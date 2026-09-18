using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.Synergy
{
    /// <summary>
    /// RealSynergySync를 씬에 수동 배치할 필요 없이 게임 시작과 동시에 자동 생성한다.
    /// 씬이 바뀌어도 DontDestroyOnLoad로 유지되고, 이미 있으면 중복 생성하지 않는다.
    /// [RuntimeInitializeOnLoadMethod]는 씬 구분 없이 Play 누르는 모든 씬에서 실행되기 때문에,
    /// 실제로 스킬바가 필요한 씬(실제 인게임 + 내 개인 테스트 루프)에서만 생성하도록 제한한다 —
    /// 안 그러면 팀원들 각자 테스트 씬(JOB_세진·JOB_김건 등)에서도 플레이할 때마다 스킬창이 떠버림.
    /// JOB_SUNGMIN_SkillTest는 SkillSandbox가 이미 자기 SkillManager/스킬바를 직접 만들어 쓰기 때문에
    /// 여기서 제외한다 — 안 그러면 스킬바 2개가 같은 자리에 겹쳐서 뜬다(화염구 등 아이콘 중복 원인).
    /// </summary>
    public static class RealCombatBootstrap
    {
        private static readonly string[] RelevantScenes =
        {
            "InGame", "JOB_SUNGMIN_Lobby", "JOB_SUNGMIN_StageChoice", "JOB_SUNGMIN_InGame",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (System.Array.IndexOf(RelevantScenes, sceneName) < 0) return;

            if (Object.FindFirstObjectByType<RealSynergySync>() != null) return;

            var go = new GameObject("RealSynergySync (auto)");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<RealSynergySync>();
        }
    }
}
