using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Stage;
using OZGL2.Stage.Prototype;
using OZGL2.UIFlow;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>InGame의 의존성을 조립한다. UI는 상태 조회와 명령만 사용하고 전투 구현은 IStageBattle로 교체한다.</summary>
    public sealed class InGamePrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private InGamePrototypeConfigSO _config;
        [SerializeField] private UISceneNavigator _navigator;
        [Tooltip("IStageRewards 구현체(선택) — 지정하면 증강 선택에서 Dummy 대신 이걸 사용(성민 파트 실제 증강 연동용). 비우면 기존 동작 그대로.")]
        [SerializeField] private MonoBehaviour _augmentRewardsOverride;
        private StageRunHost _host;
        private InGameGridSession _session;
        private bool _isStarting;
        private bool _isDestroyed;
        public StageManager Stage { get; private set; }
        public GridRunSession GridSession => _session?.Session;
        public StageGridRewards Rewards { get; private set; }
        public ManualStageServices Dummy { get; private set; }
        public string Error { get; private set; }
        public event Action Changed;
        public Task Completion { get; private set; }
        private void Start() => StartPrototype();

        public void StartPrototype()
        {
            if (_isStarting || _isDestroyed || (Stage != null && Stage.IsRunning)) return;
            Completion = RunAsync();
        }
        private async Task RunAsync()
        {
            _isStarting = true;
            Error = null;
            try
            {
                await ReleaseAsync();
                if (_isDestroyed) return;
                if (_config == null || _navigator == null) throw new InvalidOperationException("InGame configuration and navigator are required.");
                _config.Validate();
                string directory = Path.Combine(Application.persistentDataPath, "InGamePrototype");
                _session = new InGameGridSession(_config.Catalog.CreateDefinition(),
                    new DummyRewardLedger(Path.Combine(directory, "settlements.json")));
                _session.Changed += Notify;
                Dummy = new ManualStageServices();
                IStageRewards augmentHandler = _augmentRewardsOverride as IStageRewards ?? Dummy;
                Rewards = new StageGridRewards(_session, new GridPrototypeRewards(_config.Catalog), augmentHandler);
                var preparation = new StageGridPreparation(_session, _config.Catalog.CreateInitialUnit(),
                    _config.Catalog.CreateInitialBlock(), _config.InitialAnchor);
                var lobby = new CompletedLobby();
                // 전투(IStageBattle)만 실제 유닛 기반(RealStageBattleFactory)으로 교체 — 나머지(보상/준비/세션/로비)는
                // 아직 실제 UI가 없어서 Dummy(ManualStageServices) 그대로 사용.
                IStageBattle battle = RealStageBattleFactory.Create(
                    _config.HeroPoolCatalog, transform, _config.HeroSpawnPosition,
                    () => _session?.Session, _config.DemonArmyCatalog, transform,
                    _config.GridWorldOrigin, _config.CellWorldSize,
                    out HeroPool heroPool);
                Stage = new StageManager(battle, Rewards, preparation, _session,
                    new FileStageProgressStore(Path.Combine(directory, "Runs")), lobby);
                Stage.StateChanged += OnStateChanged;
                _host = new GameObject("InGameRunHost").AddComponent<StageRunHost>();
                _host.OwnResource(heroPool);
                _host.OwnResource(_session);
                if (!_host.StartRun(Stage, _config.Stage)) throw new InvalidOperationException("Stage run did not start.");
                Notify();
                await _host.CurrentRun;
                if (_isDestroyed) return;
                Error = _host.Error;
                bool canReturn = lobby.Result != null && lobby.Result.IsSettled && Error == null;
                await ReleaseAsync();
                if (_isDestroyed) return;
                Notify();
                // 자기 실행 안에서 Host 종료를 기다리지 않고, 실행이 끝난 뒤 팀 UI의 이동 규격을 사용한다.
                if (canReturn) _navigator.LoadScene(_config.LobbyScenePath);
            }
            catch (Exception exception)
            {
                Error = exception.Message;
                await ReleaseAsync();
                if (!_isDestroyed) Notify();
            }
            finally { _isStarting = false; }
        }
        public bool TryBeginBattle(bool skip = false) => GridSession != null && Stage != null &&
            Stage.State == eStageState.PREPARATION && GridSession.TryBeginBattle(GridSession.RunId, Stage.CurrentRoundNumber, skip);
        public void CancelRun() => _host?.CancelRun();
        private void OnStateChanged(eStageState state) => Notify();
        private void Notify() => Changed?.Invoke();
        private async Task ReleaseAsync()
        {
            var host = _host;
            if (host != null)
            {
                await host.ShutdownAsync();
                if (host != null) Destroy(host.gameObject);
                if (_host == host) _host = null;
            }
            if (_session != null) _session.Changed -= Notify;
            if (Stage != null) Stage.StateChanged -= OnStateChanged;
        }
        private async void OnDestroy()
        {
            _isDestroyed = true;
            try { await ReleaseAsync(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        private sealed class CompletedLobby : IStageLobby
        {
            public StageRunResult Result { get; private set; }
            public Task ReturnAsync(StageRunResult result, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (!result.IsSettled) throw new InvalidOperationException("Settlement must finish before returning.");
                Result = result;
                return Task.CompletedTask;
            }
        }
    }
}
