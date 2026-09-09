using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OZGL2.Stage
{
    /// <summary>씬 화면과 독립된 실행 소유자. 부트스트랩에서 한 개를 만들고 화면에 참조를 주입합니다.</summary>
    public sealed class StageRunHost : MonoBehaviour
    {
        private CancellationTokenSource _lifetime;
        private bool _isDestroyed;
        private Task _shutdown;
        private readonly System.Collections.Generic.List<IDisposable> _resources = new System.Collections.Generic.List<IDisposable>();
        public void OwnResource(IDisposable resource)
        {
            if (_isDestroyed) throw new InvalidOperationException("Host is shutting down.");
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (!_resources.Contains(resource)) _resources.Add(resource);
        }
        public StageManager Manager { get; private set; }
        public Task CurrentRun { get; private set; }
        public string Error { get; private set; }
        public AggregateException CleanupError { get; private set; }
        public bool IsShutdownComplete { get; private set; }
        private void Awake()
        {
            if (transform.parent != null) throw new InvalidOperationException("StageRunHost must be a root object.");
            DontDestroyOnLoad(gameObject);
        }
        public bool StartRun(StageManager manager, IStageDataSource source)
        {
            if (_isDestroyed || (CurrentRun != null && !CurrentRun.IsCompleted)) return false;
            _lifetime?.Dispose();
            _lifetime = new CancellationTokenSource();
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            Error = null;
            CurrentRun = RunAsync(source, _lifetime.Token);
            return true;
        }
        public void CancelRun() => _lifetime?.Cancel();
        private async Task RunAsync(IStageDataSource source, CancellationToken token)
        {
            try { await Manager.RunAsync(source, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception) { Error = exception.Message; }
        }
        /// <summary>명시적 종료 시 await합니다. 오류가 있어도 모든 자원과 취소 토큰 정리를 마칩니다.</summary>
        public Task ShutdownAsync()
        {
            if (_shutdown != null) return _shutdown;
            _isDestroyed = true;
            _shutdown = ShutdownCoreAsync();
            return _shutdown;
        }
        private async Task ShutdownCoreAsync()
        {
            System.Collections.Generic.List<Exception> errors = null;
            try { CancelRun(); }
            catch (Exception exception) { (errors ??= new System.Collections.Generic.List<Exception>()).Add(exception); }
            try { if (CurrentRun != null) await CurrentRun; }
            catch (Exception exception) { (errors ??= new System.Collections.Generic.List<Exception>()).Add(exception); }
            foreach (var resource in _resources)
            {
                try { resource.Dispose(); }
                catch (Exception exception) { (errors ??= new System.Collections.Generic.List<Exception>()).Add(exception); }
            }
            _resources.Clear();
            try { _lifetime?.Dispose(); }
            catch (Exception exception) { (errors ??= new System.Collections.Generic.List<Exception>()).Add(exception); }
            finally { _lifetime = null; IsShutdownComplete = true; }
            if (errors != null)
            {
                CleanupError = new AggregateException("Host shutdown failed; all resources were processed.", errors);
                throw CleanupError;
            }
        }
        private async void OnDestroy()
        {
            // Unity는 async OnDestroy를 기다리지 않는다. 명시적 종료는 ShutdownAsync를 먼저 await한다.
            try { await ShutdownAsync(); }
            catch (AggregateException) { /* CleanupError에 보관되어 있으며 명시적 호출자는 예외를 받는다. */ }
        }
    }
}
