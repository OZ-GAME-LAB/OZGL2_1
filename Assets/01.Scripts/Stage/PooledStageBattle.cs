using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OZGL2.Stage
{
    public interface IStageDefenders
    {
        int AliveCount { get; }
        Task PrepareRoundAsync(CancellationToken cancellationToken);
    }
    /// <summary>스폰과 처치를 수집한 후 Unity 프레임 경계에서 승패를 판정합니다.</summary>
    public sealed class PooledStageBattle : IStageBattle
    {
        private readonly HeroPool _pool;
        private readonly IStageDefenders _defenders;
        private readonly Vector3 _spawnPosition;
        private readonly Dictionary<long, HeroLease> _alive = new Dictionary<long, HeroLease>();
        private readonly Dictionary<long, HeroLease> _leased = new Dictionary<long, HeroLease>();
        private readonly Dictionary<long, int> _experience = new Dictionary<long, int>();
        private readonly Action<HeroLease> _deathHandler;
        private readonly Action<HeroLease> _returnHandler;
        private long _earned;
        private bool _isSpawningComplete;
        private bool _isRunning;
        public AggregateException CleanupError { get; private set; }
        public int AliveHeroCount => _alive.Count;
        public int UnreturnedHeroCount => _leased.Count;
        public long EarnedExperience => _earned;
        public bool IsSpawningComplete => _isSpawningComplete;
        public eRoundOutcome Outcome { get; private set; }
        public PooledStageBattle(HeroPool pool, IStageDefenders defenders, Vector3 spawnPosition)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _defenders = defenders ?? throw new ArgumentNullException(nameof(defenders));
            _spawnPosition = spawnPosition;
            _deathHandler = RecordDeath;
            _returnHandler = ReturnHero;
        }
        public async Task<RoundResult> RunRoundAsync(RoundDefinition round, CancellationToken cancellationToken)
        {
            if (_isRunning) throw new InvalidOperationException("Battle is already running.");
            _isRunning = true; _earned = 0; _isSpawningComplete = false; Outcome = eRoundOutcome.ONGOING;
            CleanupError = null;
            using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                Task spawning = null;
                Exception executionError = null;
                try
                {
                    foreach (var entry in round.Spawns)
                        if (!_pool.Contains(entry.HeroId)) throw new InvalidOperationException("Missing pool: " + entry.HeroId);
                    await _defenders.PrepareRoundAsync(lifetime.Token);
                    spawning = SpawnAsync(round, lifetime.Token);
                    while (true)
                    {
                        await Task.Yield();
                        lifetime.Token.ThrowIfCancellationRequested();
                        if (CleanupError != null) throw CleanupError;
                        if (spawning.IsFaulted) await spawning;
                        Outcome = RoundCompletionEvaluator.Evaluate(new BattleProgress(_isSpawningComplete, _alive.Count, _defenders.AliveCount));
                        if (Outcome == eRoundOutcome.SIMULTANEOUS)
                            throw new InvalidOperationException("Simultaneous extinction policy is not decided. No settlement was requested.");
                        if (Outcome == eRoundOutcome.VICTORY || Outcome == eRoundOutcome.DEFEAT)
                            return new RoundResult(Outcome == eRoundOutcome.VICTORY ? eBattleResult.VICTORY : eBattleResult.DEFEAT, _earned);
                    }
                }
                catch (Exception exception)
                {
                    executionError = exception;
                    throw;
                }
                finally
                {
                    List<Exception> errors = null;
                    try { lifetime.Cancel(); }
                    catch (Exception exception) { (errors ??= new List<Exception>()).Add(exception); }
                    try { if (spawning != null) await spawning; }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                    catch (Exception exception)
                    {
                        if (!ReferenceEquals(exception, executionError)) (errors ??= new List<Exception>()).Add(exception);
                    }
                    foreach (var lease in _leased.Values)
                    {
                        try { _pool.Return(lease); }
                        catch (Exception exception) { (errors ??= new List<Exception>()).Add(exception); }
                    }
                    _leased.Clear(); _alive.Clear(); _experience.Clear(); _isRunning = false;
                    if (CleanupError != null && !ReferenceEquals(CleanupError, executionError))
                        (errors ??= new List<Exception>()).Add(CleanupError);
                    if (errors != null)
                    {
                        if (executionError != null) errors.Insert(0, executionError);
                        CleanupError = new AggregateException("Battle cleanup failed; all leases were processed.", errors);
                        throw CleanupError;
                    }
                }
            }
        }
        private async Task SpawnAsync(RoundDefinition round, CancellationToken token)
        {
            float previousInterval = 0;
            foreach (var entry in round.Spawns)
                for (int index = 0; index < entry.Count; index++)
                {
                    if (previousInterval > 0)
                        await Task.Delay(TimeSpan.FromSeconds(previousInterval), token);
                    token.ThrowIfCancellationRequested();
                    var lease = _pool.Rent(entry.HeroId, _spawnPosition, _deathHandler, _returnHandler);
                    _leased.Add(lease.LeaseId, lease);
                    _alive.Add(lease.LeaseId, lease);
                    _experience.Add(lease.LeaseId, _pool.GetExperience(entry.HeroId));
                    // OnEnable에서 사망 통지가 와도 먼저 등록된 대여만 처리한다.
                    lease.Hero.gameObject.SetActive(true);
                    previousInterval = entry.IntervalSeconds;
                }
            _isSpawningComplete = true;
        }
        private void RecordDeath(HeroLease lease)
        {
            if (!_alive.TryGetValue(lease.LeaseId, out var current) || current.Hero != lease.Hero) return;
            _earned = checked(_earned + _experience[lease.LeaseId]);
            _alive.Remove(lease.LeaseId); _experience.Remove(lease.LeaseId);
        }
        private void ReturnHero(HeroLease lease)
        {
            if (!_leased.TryGetValue(lease.LeaseId, out var current) || current.Hero != lease.Hero) return;
            _leased.Remove(lease.LeaseId);
            try { _pool.Return(lease); }
            catch (Exception exception)
            {
                // 외부 사망 연출 콜백에서 난 정리 오류도 전투 Task가 관찰하고 종료한다.
                CleanupError = CleanupError == null
                    ? new AggregateException("Hero return failed.", exception)
                    : new AggregateException(CleanupError, exception);
            }
        }
        public bool DefeatOneHero()
        {
            foreach (var lease in _alive.Values) return lease.Hero.TryReportDeath(lease.LeaseId);
            return false;
        }
    }
}
