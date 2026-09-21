using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Grid.UI;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>단계 표시와 시작 요청을 연결한다. 전투 로직과 최종 UI를 소유하지 않는다.</summary>
    public sealed class InGamePhasePresentation : MonoBehaviour, IInGameCombatParticipant
    {
        [SerializeField] private InGamePrototypeBootstrap _bootstrap;
        [SerializeField] private GridPrototypeRunner _runner;
        [SerializeField] private InGameCameraTransition _camera;
        private GridRunSession _session;
        private GridWorldPreparationView _preview;
        private readonly Dictionary<Renderer, bool> _hidden = new Dictionary<Renderer, bool>();
        private bool _preparing;
        private bool _startingBattle;
        private bool _busy;
        private int _generation;
        private Vector2Int _resolution;
        public GridWorldInputSurface Surface { get; private set; }
        public bool IsTransitioning => _busy;
        public string Error { get; private set; }
        public bool CanRetryCamera => !_busy && Error != null && IsCurrentPreparation;
        public bool CanExitAfterCameraError => !_busy && Error != null && _session != null && !_session.IsEnded;
        private bool IsCurrentPreparation => _session != null && !_session.IsEnded &&
            _session.NextRound > 1 && _session.Grid.Phase == eGridPhase.PREPARATION;
        public void ValidateSetup()
        {
            if (_bootstrap == null || _runner == null || _camera == null) throw new InvalidOperationException("Bootstrap, preparation UI and camera transition are required.");
            _camera.ValidateFraming(GetBounds(false), false);
        }
        public bool CanInteract => !_busy && Error == null && _session != null && !_session.IsEnded &&
            _session.NextRound > 1 && _session.Grid.Phase == eGridPhase.PREPARATION;
        private void OnEnable()
        {
            if (_bootstrap == null) return;
            _bootstrap.Changed += Refresh; Refresh();
        }
        private void OnDisable()
        {
            if (_bootstrap != null) _bootstrap.Changed -= Refresh;
            Reset();
        }
        private void Reset()
        {
            _generation++; _camera?.Cancel(); _busy = false; _startingBattle = false; _preparing = false;
            Error = null;
            RestoreRenderers(); _preview?.Dispose(); _preview = null; _session = null; Surface = null;
            if (_runner != null) { _runner.Unbind(); _runner.ConfigureWorld(null, null, null); }
        }
        private void Refresh()
        {
            var session = _bootstrap.GridSession;
            if (session != _session || (session != null && session.IsEnded))
            {
                Reset();
                if (session == null || session.IsEnded) return;
                _camera.Validate(); Error = null; _session = session;
                var config = _bootstrap.Config;
                var mapping = new GridWorldMapping(config.GridWorldOrigin, Vector3.right * config.CellWorldSize, Vector3.up * config.CellWorldSize);
                _preview = new GridWorldPreparationView(session.Grid, mapping, config.CellWorldSize,
                    (id, star) => { var prefab = config.DemonArmyCatalog.FindPrefab(id, star); return prefab != null ? prefab.gameObject : null; }, transform);
                // 배치 중 드래그하는 유닛의 사거리 표시용 (전투 프리팹의 statData.attackRange, 월드 단위 그대로).
                _preview.RangeProvider = (id, star) =>
                {
                    var prefab = config.DemonArmyCatalog.FindPrefab(id, star);
                    return prefab != null && prefab.statData != null ? prefab.statData.attackRange : 0f;
                };
                Surface = new GridWorldInputSurface(_camera.Camera, config.GridWorldOrigin, config.CellWorldSize, () => _preview?.Refresh());
                _runner.ConfigureWorld(Surface, RequestBattle, () => CanInteract, () => Error ?? (_busy ? "Camera transitioning..." : null));
                _runner.ConfigureRecovery(() => CanRetryCamera, () => RetryCamera(), () => ExitAfterCameraError());
                _runner.Bind(session);
                if (!FrameBattleSafely()) return;
                _resolution = new Vector2Int(Screen.width, Screen.height);
            }
            if (_session == null) return;
            bool prepare = _session.NextRound > 1 && _session.Grid.Phase == eGridPhase.PREPARATION;
            if (prepare && !_preparing)
            {
                _preparing = true; HideRenderers(); _preview.SetVisible(true);
                EnterPreparationAsync();
            }
            _runner.RefreshControls();
        }
        private Bounds GetBounds(bool preparation)
        {
            var config = _bootstrap.Config;
            var size = _session != null ? _session.Grid.Definition.MaximumSize : config.Catalog.CreateDefinition().MaximumSize;
            var min = config.GridWorldOrigin + new Vector3(-0.5f, -1.5f, 0) * config.CellWorldSize;
            var max = config.GridWorldOrigin + new Vector3(size.x - 0.5f, size.y - 0.5f, 0) * config.CellWorldSize;
            var bounds = new Bounds((min + max) * 0.5f, max - min);
            if (!preparation) bounds.Encapsulate(new Vector3(config.HeroSpawnPosition.x, config.HeroSpawnPosition.y, config.GridWorldOrigin.z));
            return bounds;
        }
        private async void EnterPreparationAsync()
        {
            int generation = ++_generation; _busy = true; _runner.RefreshControls();
            try { await _camera.FrameAsync(GetBounds(true), true); }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (generation == _generation) Error = error.Message; }
            finally { if (generation == _generation) { _busy = false; _runner.RefreshControls(); } }
        }
        public bool RequestBattle(bool skip)
        {
            if (!CanInteract || !(skip ? _session.CanSkipPreparation : _session.CanBeginBattle)) return false;
            StartBattleAsync(skip); return true;
        }
        public bool RetryCamera()
        {
            if (!CanRetryCamera) return false;
            RecoverCameraAsync(); return true;
        }
        public bool ExitAfterCameraError()
        {
            if (!CanExitAfterCameraError) return false;
            _bootstrap.CancelRun(); return true;
        }
        private async void RecoverCameraAsync()
        {
            int generation = ++_generation; _busy = true; _runner.RefreshControls();
            try
            {
                await _camera.FrameAsync(GetBounds(true), true);
                if (generation == _generation && IsCurrentPreparation) Error = null;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (generation == _generation) Error = error.Message; }
            finally { if (generation == _generation) { _busy = false; _runner.RefreshControls(); } }
        }
        private async void StartBattleAsync(bool skip)
        {
            int generation = ++_generation; var session = _session; int round = session.NextRound;
            _busy = true; _startingBattle = true; _runner.RefreshControls();
            try
            {
                await _camera.FrameAsync(GetBounds(false), false);
                if (generation != _generation || session.IsEnded || _session != session || session.NextRound != round) return;
                if (!_bootstrap.CommitBattleStart(skip)) throw new InvalidOperationException("Battle start was rejected after camera transition.");
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (generation == _generation)
                {
                    Error = error.Message;
                    if (IsCurrentPreparation)
                    {
                        try { await _camera.FrameAsync(GetBounds(true), true, true); }
                        catch (OperationCanceledException) { }
                        catch (Exception recoveryError)
                        { if (generation == _generation) Error = error.Message + " / Preparation recovery failed: " + recoveryError.Message; }
                    }
                }
            }
            finally { if (generation == _generation) { _busy = false; _startingBattle = false; _runner.RefreshControls(); } }
        }
        // 화면 크기 변경 시 안전 영역을 다시 맞춘다. 매 프레임 배치나 카메라 목표를 재계산하지 않는다.
        private void LateUpdate()
        {
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (_session == null || _session.IsEnded || resolution == _resolution || _startingBattle || Error != null) return;
            _resolution = resolution;
            if (_preparing) EnterPreparationAsync();
            else FrameBattleSafely();
        }
        private bool FrameBattleSafely()
        {
            try { _camera.FrameAsync(GetBounds(false), false, true); return true; }
            catch (Exception error)
            {
                // 전투 중 화면 오류를 준비 재시도로 돌리면 전투 상태와 배치 상태가 어긋난다.
                _bootstrap.FailCameraPresentation(error.Message);
                return false;
            }
        }
        private void HideRenderers()
        {
            foreach (var unit in _bootstrap.GetComponentsInChildren<UnitBase>(true))
                foreach (var renderer in unit.GetComponentsInChildren<Renderer>(true))
                    if (!_hidden.ContainsKey(renderer)) { _hidden.Add(renderer, renderer.forceRenderingOff); renderer.forceRenderingOff = true; }
        }
        private void RestoreRenderers()
        {
            foreach (var item in _hidden) if (item.Key != null) item.Key.forceRenderingOff = item.Value;
            _hidden.Clear();
        }
        public void SetCombatEnabled(bool isEnabled)
        {
            if (!isEnabled) return;
            _preparing = false; _preview?.SetVisible(false); RestoreRenderers();
        }
        public Task PrepareAsync(InGameCombatContext context, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public Task CleanupAsync(InGameCombatContext context) => Task.CompletedTask;
        public void CancelPresentation()
        { Reset(); }
    }
}
