using System.Reflection;
using OZGL2.InGame;
using OZGL2.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 마왕군 유닛 해금을 실제 카드 보상에 연결한다. 팀 파일(GeneralRewardSource·StageGridRewards)은 수정하지 않는다.
    /// - 카드 보상 후보를 뽑는 쪽이 이미 갖고 있는 해금 확인 자리(IUnitRewardUnlocks)에 UnitUnlockStore 기준 판정을 끼워 넣는다.
    ///   (라운드를 클리어한 그 순간의 보상 후보에 방금 해금된 유닛이 바로 들어가도록, 저장된 해금 + 이번 클리어로 해금되는 것을 함께 본다.)
    /// - 라운드를 클리어하면 해금을 저장하고 화면에 "새 마왕군 해금" 알림을 띄운다.
    /// InGamePrototypeBootstrap이 있는 씬이 로드되면 자동으로 붙는다.
    /// </summary>
    public sealed class UnitUnlockBridge : MonoBehaviour
    {
        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FSource = typeof(StageGridRewards).GetField("_source", Priv);
        private static readonly FieldInfo FUnlocks = typeof(GeneralRewardSource).GetField("_unlocks", Priv);

        private InGamePrototypeBootstrap _bootstrap;
        private object _stage;
        private int _seenCleared;
        private float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        private static void Install()
        {
            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (bootstrap != null && bootstrap.GetComponent<UnitUnlockBridge>() == null)
                bootstrap.gameObject.AddComponent<UnitUnlockBridge>();
        }

        private void Awake() => _bootstrap = GetComponent<InGamePrototypeBootstrap>();

        private void Update()
        {
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage != null)
            {
                if (!ReferenceEquals(stage, _stage)) { _stage = stage; _seenCleared = stage.ClearedRoundCount; }
                if (stage.ClearedRoundCount > _seenCleared)
                {
                    _seenCleared = stage.ClearedRoundCount;
                    foreach (var id in UnitUnlockStore.UnlockForRound(_seenCleared))
                    {
                        Debug.Log("[해금] 라운드 " + _seenCleared + " 클리어 → 마왕군 " + UnitUnlockStore.DisplayName(id) + " 해금");
                        var hud = FindFirstObjectByType<InGameCurrencyHud>();
                        if (hud != null) hud.AnnounceUnitUnlock(id);
                    }
                }
            }

            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.25f;
            HookRewardSource();
        }

        /// <summary>카드 보상 후보를 뽑는 쪽의 해금 판정을 우리 것으로 바꾼다(스테이지가 새로 만들어질 때마다 다시 확인).</summary>
        private void HookRewardSource()
        {
            if (_bootstrap == null || _bootstrap.Rewards == null || FSource == null || FUnlocks == null) return;
            var source = FSource.GetValue(_bootstrap.Rewards) as GeneralRewardSource;
            if (source == null || FUnlocks.GetValue(source) is AccountUnitUnlocks) return;
            FUnlocks.SetValue(source, new AccountUnitUnlocks(() => _bootstrap != null && _bootstrap.Stage != null ? _bootstrap.Stage.ClearedRoundCount : 0));
        }

        private sealed class AccountUnitUnlocks : IUnitRewardUnlocks
        {
            private readonly System.Func<int> _clearedRounds;
            public AccountUnitUnlocks(System.Func<int> clearedRounds) { _clearedRounds = clearedRounds; }
            public bool IsUnlocked(string unitId) => UnitUnlockStore.IsUnlockedAtRound(unitId, _clearedRounds());
        }
    }
}
