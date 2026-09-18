using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Stage;
using OZGL2.Stage.Prototype;
using OZGL2.UIFlow;
using OZGL2.Synergy;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>InGame의 의존성을 조립한다. UI는 상태 조회와 명령만 사용하고 전투 구현은 IStageBattle로 교체한다.</summary>
    public sealed class InGamePrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private InGamePrototypeConfigSO _config;
        [SerializeField] private UISceneNavigator _navigator;
        [SerializeField] private InGamePhasePresentation _phasePresentation;
        [Tooltip("IInGameCombatParticipant를 구현한 팀원 연결 컴포넌트. 비어 있으면 외부 전투 제어는 미연결.")]
        [SerializeField] private MonoBehaviour[] _combatParticipants = Array.Empty<MonoBehaviour>();
        [Tooltip("IStageRewards를 구현한 실제 증강 선택 연결부. 비어 있으면 더미 사용.")]
        [SerializeField] private MonoBehaviour _augmentProvider;
        private InGameCombatConnection _combatConnection;
        private InGameSynergyConnection _synergyConnection;
        private RealSynergySync _runSynergy;
        public bool HasCombatParticipants => _combatConnection != null && _combatConnection.ParticipantCount > 0;
        public bool HasExternalCombatParticipants => _combatConnection != null &&
            Array.Exists(_combatParticipants, component => component != null && component != _phasePresentation && component is IInGameCombatParticipant);
        public bool HasSynergyConnection => _synergyConnection != null && _synergyConnection.IsConnected;
        public bool UsesDummyAugments => _augmentProvider == null;
        private StageRunHost _host;
        private InGameGridSession _session;
        private bool _isStarting;
        private string _presentationError;
        private bool _isDestroyed;
        private bool _hasCleanupFailure;
        private bool _isReturningToLobby;
        public StageManager Stage { get; private set; }
        public GridRunSession GridSession => _session?.Session;
        public InGamePrototypeConfigSO Config => _config;
        private IDisposable _defenders;
        private HeroPool _heroPool;
        private Task _releaseTask;
        private bool _isNotifying;
        public Exception LastNotificationError { get; private set; }
        public string LastFailedSubscriber { get; private set; }
        public int NotificationErrorCount { get; private set; }
        public bool CanRetry => !_isDestroyed && !_isStarting && !_hasCleanupFailure && !_isReturningToLobby &&
            !(Stage?.IsRunning ?? false) && !(_releaseTask != null && !_releaseTask.IsCompleted);
        public string RetryBlockedReason => _hasCleanupFailure
            ? "Cleanup success is unconfirmed. Retry is locked for this session."
            : _isReturningToLobby ? "Lobby transition requested. Retry is locked." : null;
        public string ExternalOperationStatus => _combatConnection?.OperationStatus ?? "Not connected";
        public string CombatParticipantNames => _combatConnection?.ParticipantNames ?? "None";
        public StageGridRewards Rewards { get; private set; }
        public ManualStageServices Dummy { get; private set; }
        public string Error { get; private set; }
        public event Action Changed;
        public Task Completion { get; private set; }
        private void Start() => StartPrototype();

        public void StartPrototype()
        {
            if (!CanRetry) return;
            Completion = RunAsync();
        }
        private async Task RunAsync()
        {
            _isStarting = true;
            Error = null;
            _presentationError = null;
            LastNotificationError = null; LastFailedSubscriber = null; NotificationErrorCount = 0;
            try
            {
                await ReleaseAsync();
                if (_isDestroyed) return;
                if (_config == null || _navigator == null) throw new InvalidOperationException("InGame configuration and navigator are required.");
                _config.Validate();
                _phasePresentation?.ValidateSetup();
                string directory = Path.Combine(Application.persistentDataPath, "InGamePrototype");
                _session = new InGameGridSession(_config.Catalog.CreateDefinition(),
                    new DummyRewardLedger(Path.Combine(directory, "settlements.json")));
                _runSynergy = FindFirstObjectByType<RealSynergySync>();
                _runSynergy?.Augments.ResetRun();
                _synergyConnection = new InGameSynergyConnection(_config.DemonArmyCatalog, _runSynergy);
                _session.Changed += OnSessionChanged;
                var participants = new List<IInGameCombatParticipant>();
                if (_phasePresentation != null) participants.Add(_phasePresentation);
                foreach (var component in _combatParticipants)
                {
                    if (!(component is IInGameCombatParticipant participant))
                        throw new InvalidOperationException("Combat participant must implement IInGameCombatParticipant.");
                    participants.Add(participant);
                }
                _combatConnection = new InGameCombatConnection(participants, CreateCombatContext, TimeSpan.FromSeconds(_config.ExternalOperationTimeoutSeconds));
                _combatConnection.DisableCombat();
                Dummy = new ManualStageServices();
                IStageRewards augment = Dummy;
                if (_augmentProvider != null)
                    augment = _augmentProvider as IStageRewards ?? throw new InvalidOperationException("Augment provider must implement IStageRewards.");
                Rewards = new StageGridRewards(_session, new GridPrototypeRewards(_config.Catalog), augment);
                var preparation = new StageGridPreparation(_session, _config.Catalog.CreateInitialUnit(),
                    _config.Catalog.CreateInitialBlock(), _config.InitialAnchor);
                var lobby = new CompletedLobby();
                // 전투(IStageBattle)만 실제 유닛 기반(RealStageBattleFactory)으로 교체 — 나머지(보상/준비/세션/로비)는
                // 아직 실제 UI가 없어서 Dummy(ManualStageServices) 그대로 사용.
                IStageBattle battle = RealStageBattleFactory.Create(
                    _config.HeroPoolCatalog, transform, _config.HeroSpawnPosition,
                    () => _session?.Session, _config.DemonArmyCatalog, transform,
                    _config.GridWorldOrigin, _config.CellWorldSize,
                    out _heroPool, resource => _defenders = resource, _combatConnection);
                Stage = new StageManager(battle, Rewards, preparation, _session,
                    new FileStageProgressStore(Path.Combine(directory, "Runs")), lobby);
                Stage.StateChanged += OnStateChanged;
                _host = new GameObject("InGameRunHost").AddComponent<StageRunHost>();
                _host.OwnResource(_heroPool);
                _host.OwnResource(_defenders);
                _host.OwnResource(_session);
                if (!_host.StartRun(Stage, _config.Stage)) throw new InvalidOperationException("Stage run did not start.");
                Notify();
                await _host.CurrentRun;
                if (_isDestroyed) return;
                Error = _presentationError ?? _host.Error;
                bool canReturn = lobby.Result != null && lobby.Result.IsSettled && Error == null;
                await ReleaseAsync();
                if (_isDestroyed) return;
                Notify();
                // 자기 실행 안에서 Host 종료를 기다리지 않고, 실행이 끝난 뒤 팀 UI의 이동 규격을 사용한다.
                if (canReturn)
                {
                    BeginLobbyReturn();
                    _navigator.LoadScene(_config.LobbyScenePath);
                }
            }
            catch (Exception exception)
            {
                Error = exception.Message;
                try { await ReleaseAsync(); }
                catch (Exception cleanupError) { Error = new AggregateException(exception, cleanupError).Message; }
            }
            finally
            {
                _isStarting = false;
                if (!_isDestroyed) Notify();
            }
        }
        public bool TryBeginBattle(bool skip = false) => _phasePresentation != null ? _phasePresentation.RequestBattle(skip) : CommitBattleStart(skip);
        internal bool CommitBattleStart(bool skip = false) => GridSession != null && Stage != null &&
            Stage.State == eStageState.PREPARATION && GridSession.TryBeginBattle(GridSession.RunId, Stage.CurrentRoundNumber, skip);
        public void CancelRun() { _phasePresentation?.CancelPresentation(); _host?.CancelRun(); }
        internal void FailCameraPresentation(string message)
        {
            _presentationError = "Camera presentation failed: " + message;
            Error = _presentationError;
            CancelRun();
        }
        private void BeginLobbyReturn()
        {
            // 표시 구독자가 재실행을 요청해도 씬 이동 요청 이후에는 시작할 수 없다.
            _isReturningToLobby = true;
            Notify();
        }
        private void OnStateChanged(eStageState state) => Notify();
        private void OnSessionChanged()
        {
            _synergyConnection?.Bind(GridSession);
            Notify();
        }
        private InGameCombatContext CreateCombatContext()
        {
            var grid = GridSession;
            if (grid == null || grid.IsEnded || grid.Deployment == null)
                throw new InvalidOperationException("A confirmed deployment is required for combat.");
            var mapping = new GridWorldMapping(_config.GridWorldOrigin,
                Vector3.right * _config.CellWorldSize, Vector3.up * _config.CellWorldSize);
            return new InGameCombatContext(grid.RunId, Stage.CurrentRoundNumber, mapping.GetWorldPosition(grid.Deployment.KingAnchor));
        }
        private void Notify()
        {
            if (_isNotifying) return;
            var handlers = Changed;
            if (handlers == null) return;
            _isNotifying = true;
            try
            {
                foreach (Action handler in handlers.GetInvocationList())
                    try { handler(); }
                    catch (Exception error)
                    {
                        LastNotificationError = error;
                        LastFailedSubscriber = handler.Method.DeclaringType?.FullName + "." + handler.Method.Name;
                        NotificationErrorCount++;
                    }
            }
            finally { _isNotifying = false; }
        }
        private Task ReleaseAsync()
        {
            if (_releaseTask != null && !_releaseTask.IsCompleted) return _releaseTask;
            return _releaseTask = ReleaseCoreAsync();
        }
        private async Task ReleaseCoreAsync()
        {
            var errors = new List<Exception>();
            var host = _host;
            if (host != null)
            {
                try { await host.ShutdownAsync(); }
                catch (Exception exception) { errors.Add(exception); }
                finally
                {
                    if (host != null) Destroy(host.gameObject);
                    if (_host == host) _host = null;
                }
            }
            if (_session != null) _session.Changed -= OnSessionChanged;
            if (Stage != null) Stage.StateChanged -= OnStateChanged;
            try { _synergyConnection?.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            _synergyConnection = null;
            try { if (_runSynergy != null) _runSynergy.Augments.ResetRun(); }
            catch (Exception exception) { errors.Add(exception); }
            _runSynergy = null;
            try { if (_combatConnection != null) await _combatConnection.EndRoundAsync(); }
            catch (Exception exception) { errors.Add(exception); }
            _combatConnection = null;
            try { _defenders?.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            _defenders = null;
            try { _heroPool?.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            _heroPool = null;
            try { _session?.Dispose(); } catch (Exception exception) { errors.Add(exception); }
            if (errors.Count > 0)
            {
                // 참조를 비웠거나 후속 Dispose가 성공해도 외부 효과의 회수 성공을 증명하지 못한다.
                _hasCleanupFailure = true;
                throw new AggregateException("InGame shutdown failed; all connections were processed.", errors);
            }
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
