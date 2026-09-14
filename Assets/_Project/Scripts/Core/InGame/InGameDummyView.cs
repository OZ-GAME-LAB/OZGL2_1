using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Stage;
using OZGL2.Stage.Prototype;
using OZGL2.UIFlow;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>교체 가능한 개발용 화면. 팀의 최종 UI는 Bootstrap 상태/명령과 GridRunSession에 연결한다.</summary>
    public sealed class InGameDummyView : MonoBehaviour
    {
        [SerializeField] private InGamePrototypeBootstrap _bootstrap;
        [SerializeField] private GridPrototypeRunner _gridView;
        [SerializeField] private UIPageGroup _pages;
        [SerializeField] private GameObject _battlePage;
        private void OnEnable()
        {
            if (_bootstrap == null) return;
            _bootstrap.Changed += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if (_bootstrap != null) _bootstrap.Changed -= Refresh;
            if (_gridView != null) _gridView.Unbind();
        }
        private void Refresh()
        {
            if (_gridView == null || _pages == null) return;
            var session = _bootstrap.GridSession;
            if (_gridView.Session != session || (session != null && session.IsEnded))
            {
                _gridView.Unbind();
                if (session != null && !session.IsEnded) _gridView.Bind(session);
            }
            bool showGrid = session != null && !session.IsEnded &&
                ((session.Grid.Phase == eGridPhase.PREPARATION && session.NextRound > 1) || session.Grid.HasPendingStorage);
            _pages.ShowPage(showGrid ? _gridView.gameObject : _battlePage);
        }
        private void OnGUI()
        {
            if (_bootstrap == null || _battlePage == null || !_battlePage.activeSelf) return;
            float width = Mathf.Min(600, Screen.width - 24);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, 24, width, Screen.height - 48), GUI.skin.box);
            GUILayout.Label("INGAME / PROTOTYPE CORE LOOP");
            GUILayout.Label("Dummy battle and rewards · Final UI will replace this screen");
            var stage = _bootstrap.Stage;
            if (stage != null)
            {
                GUILayout.Label("Round " + stage.CurrentRoundNumber + " / " + stage.TotalRounds + " — " + stage.State);
                GUILayout.Label("Cleared: " + stage.ClearedRoundCount);
                if (_bootstrap.GridSession?.Deployment != null)
                    GUILayout.Label("Deployment captured. First battle starts with the basic unit automatically.");
            }
            GUILayout.Space(16);
            var dummy = _bootstrap.Dummy;
            if (dummy != null && dummy.PendingRequest == eDummyRequest.BATTLE)
            {
                if (GUILayout.Button("Dummy victory", GUILayout.Height(44))) dummy.CompleteBattle(dummy.RequestId, eBattleResult.VICTORY);
                if (GUILayout.Button("Dummy defeat", GUILayout.Height(44))) dummy.CompleteBattle(dummy.RequestId, eBattleResult.DEFEAT);
            }
            var rewards = _bootstrap.Rewards;
            if (rewards?.Pending != null)
            {
                string id = rewards.Pending.RequestId;
                for (int i = 0; i < 2; i++)
                    if (GUILayout.Button(rewards.GetCandidateName(i) + " + matching block", GUILayout.Height(44))) rewards.TryChooseUnit(id, i);
                GUI.enabled = _bootstrap.GridSession != null && _bootstrap.GridSession.Grid.CanExpand;
                if (GUILayout.Button("Floor expansion +2 (place in next preparation)", GUILayout.Height(44))) rewards.TryChooseExpansion(id);
                GUI.enabled = true;
            }
            if (dummy != null && dummy.PendingRequest == eDummyRequest.AUGMENT)
            {
                GUILayout.Label("Dummy augment confirmation. Actual tier selection is not connected.");
                if (GUILayout.Button("Confirm dummy augment", GUILayout.Height(44))) dummy.CompleteSelection(dummy.RequestId);
            }
            if (!string.IsNullOrEmpty(_bootstrap.Error)) GUILayout.Label("Start/run blocked: " + _bootstrap.Error);
            if (stage != null && !stage.IsRunning)
            {
                if (GUILayout.Button("Retry from round 1", GUILayout.Height(44))) _bootstrap.StartPrototype();
            }
            GUILayout.EndArea();
        }
    }
}
