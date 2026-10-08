using OZGL2.InGame;
using OZGL2.Progression;
using OZGL2.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투가 시작되면 그 웨이브에 나오는 용사를 도감에 기록한다(CodexStore). 로비 도감은 기록된 용사만 열어 준다.
    /// 처음 만난 용사는 화면에 「도감 등록」 문구를 띄운다. InGamePrototypeBootstrap 이 있는 씬이 로드되면 자동으로 붙는다.
    /// </summary>
    public sealed class HeroCodexRecorder : MonoBehaviour
    {
        private InGamePrototypeBootstrap _bootstrap;
        private eStageState _last = eStageState.IDLE;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        private static void Install()
        {
            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (bootstrap != null && bootstrap.GetComponent<HeroCodexRecorder>() == null) bootstrap.gameObject.AddComponent<HeroCodexRecorder>();
        }

        private void Awake() => _bootstrap = GetComponent<InGamePrototypeBootstrap>();

        private void Update()
        {
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage == null) return;
            var state = stage.State;
            bool started = state == eStageState.COMBAT && _last != eStageState.COMBAT;
            _last = state;
            if (started) RecordCurrentRound();
        }

        /// <summary>지금 웨이브에 나오는 용사를 전부 기록한다(디버그 패널에서도 부른다).</summary>
        public void RecordCurrentRound()
        {
            var round = _bootstrap != null ? _bootstrap.CurrentRoundDefinition : null;
            if (round == null) return;
            foreach (var spawn in round.Spawns)
            {
                if (!CodexStore.MarkHeroSeen(spawn.HeroId)) continue;
                string name = CodexStore.DisplayName(spawn.HeroId);
                Debug.Log("[도감] 새 용사 발견 → " + name);
                var hud = FindFirstObjectByType<InGameCurrencyHud>();
                if (hud != null) hud.AnnounceCodex("도감 등록: " + name);
            }
        }
    }
}
