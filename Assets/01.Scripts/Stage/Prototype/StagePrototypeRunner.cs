using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace OZGL2.Stage.Prototype
{
    /// <summary>JOB_KIMGUN의 테스트 화면. 화면 비활성화/제거는 실행 취소가 아닙니다.</summary>
    public sealed class StagePrototypeRunner : MonoBehaviour
    {
        [SerializeField] private StageDataSO _normalStage;
        [SerializeField] private StageDataSO _hardStage;
        [SerializeField] private HeroPoolCatalogSO _heroPoolCatalog;
        private StageRunHost _host;
        private HeroPool _pool;
        private PooledStageBattle _pooledBattle;
        private DummyDefenders _defenders;
        private string _error;
        public StageManager Manager => _host != null ? _host.Manager : null;
        public ManualStageServices Services { get; private set; }
        public Task CurrentRun => _host != null ? _host.CurrentRun : null;
        public string Error => _error ?? (_host != null ? _host.Error : null);
        public StageRunHost Host => _host;
        public PooledStageBattle PooledBattle => _pooledBattle;
        public DummyDefenders Defenders => _defenders;
        public HeroPool Pool => _pool;
        public void StartNormalStage() => StartStage(_normalStage);
        public void StartHardStage() => StartStage(_hardStage);
        public void StartPooledNormalStage() => StartStage(_normalStage, true);
        public void StartPooledHardStage() => StartStage(_hardStage, true);
        public void CancelStage() { if (_host != null) _host.CancelRun(); }

        private void StartStage(StageDataSO data, bool isPooled = false)
        {
            if (!Application.isPlaying || (CurrentRun != null && !CurrentRun.IsCompleted)) return;
            if (data == null) { _error = "Assign a StageDataSO first."; return; }
            _error = null;
            try
            {
                if (_host == null) _host = new GameObject(nameof(StageRunHost)).AddComponent<StageRunHost>();
                string directory = Path.Combine(Application.persistentDataPath, "stage_prototype");
                Services = new ManualStageServices(new DummyRewardLedger(Path.Combine(directory, "dummy_rewards.json")));
                IStageBattle battle = Services;
                _pooledBattle = null;
                if (isPooled)
                {
                    if (_heroPoolCatalog == null) throw new InvalidOperationException("Assign a HeroPoolCatalogSO first.");
                    if (_pool == null)
                    {
                        _pool = new HeroPool(_heroPoolCatalog.CreateSnapshot(), _host.transform);
                        _host.OwnResource(_pool);
                    }
                    _defenders = new DummyDefenders(_heroPoolCatalog.DummyDefenderCount);
                    _pooledBattle = new PooledStageBattle(_pool, _defenders, _host.transform.position);
                    battle = _pooledBattle;
                }
                _host.StartRun(new StageManager(battle, Services, Services, Services,
                    new FileStageProgressStore(directory), Services), data);
            }
            catch (Exception exception) { _error = exception.Message; }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 530, 730), GUI.skin.box);
            GUILayout.Label("Stage prototype - round records saved to disk");
            GUILayout.Label("Rewards and defenders are dummy. Pooled mode spawns test heroes.");
            bool isRunning = CurrentRun != null && !CurrentRun.IsCompleted;
            GUI.enabled = !isRunning;
            if (GUILayout.Button("Start Normal (manual 30 rounds)")) StartNormalStage();
            if (GUILayout.Button("Start Hard (manual 50 rounds)")) StartHardStage();
            if (GUILayout.Button("Start pooled Normal")) StartPooledNormalStage();
            if (GUILayout.Button("Start pooled Hard")) StartPooledHardStage();
            GUI.enabled = true;
            if (Manager != null)
            {
                GUILayout.Label($"State: {Manager.State}");
                GUILayout.Label($"Round: {Manager.CurrentRoundNumber}/{Manager.TotalRounds}, Cleared: {Manager.ClearedRoundCount}");
                GUILayout.Label($"Experience: {Manager.Progress?.EarnedExperience}");
                GUILayout.Label($"General rewards: {Services.GeneralRewardCount} / Augments: {Services.AugmentCount}");
                if (_pooledBattle != null && Manager.State == eStageState.COMBAT)
                {
                    GUILayout.Label($"Heroes: {_pooledBattle.AliveHeroCount}, Defenders: {_defenders.AliveCount}");
                    GUILayout.Label($"Spawn complete: {_pooledBattle.IsSpawningComplete}, Pool created: {_pool.CreatedCount}");
                    if (GUILayout.Button("Defeat one hero")) _pooledBattle.DefeatOneHero();
                    if (GUILayout.Button("Defeat one defender")) _defenders.DefeatOne();
                }
                long requestId = Services.RequestId;
                switch (Services.PendingRequest)
                {
                    case eDummyRequest.PREPARATION:
                        if (GUILayout.Button("Preparation complete")) Services.CompletePreparation(requestId, false);
                        GUI.enabled = Services.CanSkip;
                        if (GUILayout.Button("Skip preparation")) Services.CompletePreparation(requestId, true);
                        GUI.enabled = true;
                        break;
                    case eDummyRequest.BATTLE:
                        if (GUILayout.Button("Victory (manual flow test)")) Services.CompleteBattle(requestId, eBattleResult.VICTORY);
                        if (GUILayout.Button("Defeat (manual flow test)")) Services.CompleteBattle(requestId, eBattleResult.DEFEAT);
                        break;
                    case eDummyRequest.GENERAL_REWARD:
                        if (GUILayout.Button("Confirm dummy general reward")) Services.CompleteSelection(requestId);
                        break;
                    case eDummyRequest.AUGMENT:
                        var weights = Services.AugmentWeights;
                        GUILayout.Label($"Dummy weights S/G/P: {weights.Silver}/{weights.Gold}/{weights.Platinum}");
                        if (GUILayout.Button("Confirm dummy augment")) Services.CompleteSelection(requestId);
                        break;
                    case eDummyRequest.SETTLEMENT:
                        if (GUILayout.Button("Confirm settlement")) Services.CompleteSettlement(requestId);
                        break;
                    case eDummyRequest.LOBBY:
                        if (GUILayout.Button("Return to dummy lobby")) Services.CompleteLobbyReturn(requestId);
                        break;
                }
            }
            if (isRunning && GUILayout.Button("Cancel run")) CancelStage();
            if (!string.IsNullOrEmpty(Error)) GUILayout.Label("Error: " + Error);
            if (Manager?.PersistenceError != null) GUILayout.Label("Save error: " + Manager.PersistenceError.Message);
            if (Manager?.LastNotificationError != null)
                GUILayout.Label($"UI notification errors: {Manager.NotificationErrorCount} ({Manager.LastFailedSubscriber})");
            GUILayout.EndArea();
        }
    }
}
