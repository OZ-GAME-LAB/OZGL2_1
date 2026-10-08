using System.Reflection;
using UnityEngine.SceneManagement;
using OZGL2.InGame;
using UnityEngine;
using UnityEngine.Events;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비 설정 창(UILobbyOverlayView)과 전투 메뉴(UIBattleMenuPopupView)의 「배경음악」「효과음」 토글, 「게임 종료」를 실제로 연결한다.
    /// 두 창은 "오디오 저장·종료는 외부 시스템이 UnityEvent 에 연결한다"는 규칙이라 이벤트만 비어 있다 — 팀 UI·프리팹은 수정하지 않고
    /// 실행 중에 리스너를 달고, 저장된 설정을 토글에 먼저 반영한다.
    /// </summary>
    public sealed class AudioSettingsWire : MonoBehaviour
    {
        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private bool _wired;
        private float _next;

        private void Update()
        {
            if (_wired || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            _wired = Wire();
        }

        private static bool Wire()
        {
            bool any = false;
            foreach (var view in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (view == null) continue;
                string name = view.GetType().Name;
                if (name != "UILobbyOverlayView" && name != "UIBattleMenuPopupView") continue;
                var type = view.GetType();
                if (type.GetField("_bgmEnabledChanged", Priv)?.GetValue(view) is UnityEvent<bool> bgm) bgm.AddListener(OnBgm);
                if (type.GetField("_sfxEnabledChanged", Priv)?.GetValue(view) is UnityEvent<bool> sfx) sfx.AddListener(GameAudioSettings.SetSfx);
                // 로비의 「게임 종료」는 앱을 끄지만, 전투 메뉴의 나가기(메인 로비로)는 로비 화면으로 돌아간다
                if (type.GetField("_quitRequested", Priv)?.GetValue(view) is UnityEvent quit) quit.AddListener(name == "UIBattleMenuPopupView" ? (UnityAction)ReturnToLobby : Quit);
                // 저장된 설정을 토글 모양에 먼저 반영한다(바뀐 경우에만 이벤트가 나가므로 같은 값이면 아무 일도 없다)
                type.GetMethod("SetBgmEnabled", Priv)?.Invoke(view, new object[] { GameAudioSettings.BgmEnabled });
                type.GetMethod("SetSfxEnabled", Priv)?.Invoke(view, new object[] { GameAudioSettings.SfxEnabled });
                any = true;
            }
            return any;
        }

        private static void OnBgm(bool enabled)
        {
            GameAudioSettings.SetBgm(enabled);
            // 전투 중에 켰다면 전투 BGM 을 다시 시작한다(타이틀·로비는 SceneBgm 이 알아서 다시 튼다)
            if (enabled && FindFirstObjectByType<InGamePrototypeBootstrap>() != null) Sfx.PlayBattleBgm();
        }

        /// <summary>전투 메뉴에서 「메인 로비로」: 진행 중인 판을 멈추고 로비 씬으로 간다. 저장은 계속 남아 있어 다음에 이어할 수 있다.</summary>
        private static void ReturnToLobby()
        {
            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            string lobbyPath = bootstrap != null && bootstrap.Config != null ? bootstrap.Config.LobbyScenePath : null;
            if (string.IsNullOrEmpty(lobbyPath)) lobbyPath = "Assets/00.Scenes/Builds/Lobby.unity";
            FindFirstObjectByType<InGameRunSaver>()?.FlushNow(); // 나가기 직전 배치까지 저장
            Time.timeScale = 1f;
            bootstrap?.CancelRun();
            if (Application.CanStreamedLevelBeLoaded(lobbyPath)) SceneManager.LoadScene(lobbyPath, LoadSceneMode.Single);
            else SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
