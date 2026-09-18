using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Stage;
using UnityEngine;

namespace OZGL2.InGame
{
    public sealed class InGameCombatContext
    {
        public string RunId { get; }
        public int RoundNumber { get; }
        public Vector3 KingPosition { get; }
        public InGameCombatContext(string runId, int roundNumber, Vector3 kingPosition)
        { RunId = runId; RoundNumber = roundNumber; KingPosition = kingPosition; }
    }

    /// <summary>팀원 어댑터가 구현한다. 종료는 반복 호출에 안전해야 하며 정리 완료 후 반환한다.</summary>
    public interface IInGameCombatParticipant
    {
        void SetCombatEnabled(bool isEnabled);
        Task PrepareAsync(InGameCombatContext context, CancellationToken token);
        Task CleanupAsync(InGameCombatContext context);
    }

    /// <summary>필수 연결은 화면 알림 이벤트와 분리해 await하고, 실패하면 전투를 진행하지 않는다.</summary>
    public sealed class InGameCombatConnection : IStageBattleLifecycle
    {
        public const int DEFAULT_TIMEOUT_SECONDS = 10;
        private readonly IInGameCombatParticipant[] _participants;
        private readonly TimeSpan _timeout;
        private readonly Func<InGameCombatContext> _contextProvider;
        private InGameCombatContext _context;
        private Task _ending;
        public int ParticipantCount => _participants.Length;
        public string ParticipantNames { get; }
        public string OperationStatus { get; private set; } = "Idle";

        public InGameCombatConnection(IEnumerable<IInGameCombatParticipant> participants, Func<InGameCombatContext> contextProvider, TimeSpan? timeout = null)
        {
            _timeout = timeout ?? TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS);
            if (_timeout <= TimeSpan.Zero || _timeout.TotalMilliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(timeout));
            _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
            var unique = new List<IInGameCombatParticipant>();
            foreach (var participant in participants)
            {
                if (participant == null) throw new ArgumentException("Missing combat participant.");
                if (!unique.Contains(participant)) unique.Add(participant);
            }
            _participants = unique.ToArray();
            ParticipantNames = unique.Count == 0 ? "None" : string.Join(", ", unique.ConvertAll(p => p.GetType().Name));
        }

        public void DisableCombat()
        {
            List<Exception> errors = null;
            foreach (var participant in _participants)
                try { participant.SetCombatEnabled(false); }
                catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            if (errors != null) throw new AggregateException("Combat input shutdown failed.", errors);
        }

        public async Task BeginRoundAsync(CancellationToken token)
        {
            if (_context != null) throw new InvalidOperationException("Combat connection is already active.");
            token.ThrowIfCancellationRequested();
            _ending = null;
            _context = _contextProvider() ?? throw new InvalidOperationException("Missing combat context.");
            DisableCombat();
            foreach (var participant in _participants)
            {
                using (var preparation = CancellationTokenSource.CreateLinkedTokenSource(token))
                    await WaitForCompletionAsync(participant.PrepareAsync(_context, preparation.Token),
                        "Prepare: " + participant.GetType().Name, preparation);
                token.ThrowIfCancellationRequested();
            }
            foreach (var participant in _participants) participant.SetCombatEnabled(true);
            OperationStatus = "Combat enabled";
        }

        public Task EndRoundAsync()
        {
            if (_ending != null) return _ending;
            if (_context == null) return Task.CompletedTask;
            var completion = new TaskCompletionSource<bool>();
            _ending = completion.Task;
            CompleteEndAsync(completion);
            return _ending;
        }

        private async void CompleteEndAsync(TaskCompletionSource<bool> completion)
        {
            var context = _context;
            var errors = new List<Exception>();
            try
            {
                try { DisableCombat(); } catch (Exception error) { errors.Add(error); }
                var pending = new List<Task>();
                foreach (var participant in _participants)
                    try { pending.Add(participant.CleanupAsync(context) ?? throw new InvalidOperationException("Cleanup returned no Task.")); }
                    catch (Exception error) { errors.Add(error); }
                // 먼저 모든 정리를 요청해야 한 시스템의 지연이 다른 시스템의 정리 시작을 막지 않는다.
                try { await WaitForCompletionAsync(Task.WhenAll(pending), "Cleanup: " + ParticipantNames, null); }
                catch (Exception error)
                {
                    errors.Add(error);
                    foreach (var task in pending)
                        if (task.IsFaulted && task.Exception != null) errors.Add(task.Exception);
                }
            }
            finally
            {
                _context = null;
                OperationStatus = errors.Count > 0 ? "Stopped with cleanup error" : "Stopped";
                if (errors.Count > 0) completion.TrySetException(new AggregateException("Combat cleanup failed; all participants were processed.", errors));
                else completion.TrySetResult(true);
            }
        }

        private async Task WaitForCompletionAsync(Task operation, string label, CancellationTokenSource cancellation)
        {
            if (operation == null) throw new InvalidOperationException(label + " returned no Task.");
            OperationStatus = label;
            using (var timer = new CancellationTokenSource())
            {
                var deadline = Task.Delay(_timeout, timer.Token);
                if (await Task.WhenAny(operation, deadline) == operation)
                {
                    timer.Cancel();
                    await operation;
                    return;
                }
                OperationStatus = "TIMEOUT — restart blocked until completion: " + label;
                var timeout = new TimeoutException(label + " exceeded " + _timeout.TotalSeconds + " seconds.");
                var errors = new List<Exception> { timeout };
                try { cancellation?.Cancel(); } catch (Exception error) { errors.Add(error); }
                // 시간 초과는 외부 효과가 사라졌다는 뜻이 아니다. 늦은 완료를 관찰할 때까지 풀 재사용을 막는다.
                try { await operation; } catch (Exception error) { errors.Add(error); }
                if (errors.Count == 1) throw timeout;
                throw new AggregateException(errors);
            }
        }
    }
}
