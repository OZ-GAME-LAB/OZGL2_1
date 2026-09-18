using UnityEngine;

namespace OZGL2.Synergy
{
    /// <summary>
    /// RealSynergySync를 씬에 수동 배치할 필요 없이 게임 시작과 동시에 자동 생성한다.
    /// 씬이 바뀌어도 DontDestroyOnLoad로 유지되고, 이미 있으면 중복 생성하지 않는다.
    /// </summary>
    public static class RealCombatBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Object.FindFirstObjectByType<RealSynergySync>() != null) return;

            var go = new GameObject("RealSynergySync (auto)");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<RealSynergySync>();
        }
    }
}
