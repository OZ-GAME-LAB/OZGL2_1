using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.InGame
{
    /// <summary>씬 경계에서만 공유하는 요청함. 도메인 재로드를 끈 Play에서도 이전 요청을 비운다.</summary>
    public static class StageLaunchRuntime
    {
        public static StageLaunchSession Session { get; private set; } = new StageLaunchSession();
        private static int _initialSceneHandle = -1;
        public static bool IsDirectEditorEntry(Scene scene) => Application.isEditor && scene.handle == _initialSceneHandle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Session = new StageLaunchSession();
            _initialSceneHandle = -1;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CaptureInitialScene() => _initialSceneHandle = SceneManager.GetActiveScene().handle;

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Session.ObserveScene(scene.path);
        }
    }
}
