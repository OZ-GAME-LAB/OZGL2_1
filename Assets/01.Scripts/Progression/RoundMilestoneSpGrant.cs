using OZGL2.InGame;
using OZGL2.Synergy;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.Progression
{
    /// <summary>
    /// 라운드 마일스톤 SP 지급 — 라운드를 클리어할 때마다 SkillTreeStore.GrantForRound(R10 +1 · R20 +2 · R30 +3 …)를 호출한다.
    /// 스테이지 흐름(InGamePrototypeBootstrap·StageManager)은 건드리지 않고 읽기만 한다.
    /// GrantForRound가 최고 마일스톤을 기록해 두므로 재도전으로 같은 마일스톤을 다시 받지는 않는다.
    /// InGamePrototypeBootstrap이 있는 씬이 로드되면 자동으로 붙는다.
    /// </summary>
    public sealed class RoundMilestoneSpGrant : MonoBehaviour
    {
        private InGamePrototypeBootstrap _bootstrap;
        private object _stage;
        private int _seenCleared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        private static void Install()
        {
            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (bootstrap != null && bootstrap.GetComponent<RoundMilestoneSpGrant>() == null)
                bootstrap.gameObject.AddComponent<RoundMilestoneSpGrant>();
        }

        private void Awake() => _bootstrap = GetComponent<InGamePrototypeBootstrap>();

        private void Update()
        {
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage == null) return;
            if (!ReferenceEquals(stage, _stage)) // 재도전으로 새 StageManager가 만들어지면 카운트를 새로 시작
            {
                _stage = stage;
                _seenCleared = stage.ClearedRoundCount;
            }
            if (stage.ClearedRoundCount <= _seenCleared) return;
            _seenCleared = stage.ClearedRoundCount;

            var sync = FindFirstObjectByType<RealSynergySync>();
            var traits = sync != null && sync.Traits != null ? sync.Traits : new TraitTree(Resources.LoadAll<TraitData>("Traits"));
            int granted = SkillTreeStore.GrantForRound(_seenCleared, traits.BuildModifiers().MilestoneSpBonus);
            if (granted > 0) Debug.Log("[SP] R" + _seenCleared + " 마일스톤 → +" + granted + " SP (총 " + SkillTreeStore.SkillPoints + ")");
        }
    }
}
