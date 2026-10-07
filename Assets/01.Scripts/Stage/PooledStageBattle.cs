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
        private readonly IStageBattleLifecycle _lifecycle;
        private readonly Vector3[] _spawnPositions;
        private readonly Dictionary<long, HeroLease> _alive = new Dictionary<long, HeroLease>();
        private readonly Dictionary<long, HeroLease> _leased = new Dictionary<long, HeroLease>();
        private readonly Dictionary<long, int> _experience = new Dictionary<long, int>();
        private readonly Action<HeroLease> _deathHandler;
        private readonly Action<HeroLease> _returnHandler;
        private readonly Action<HeroLease, Exception> _faultHandler;
        private long _earned;
        private bool _isSpawningComplete;
        private bool _isRunning;
        public AggregateException CleanupError { get; private set; }
        public int AliveHeroCount => _alive.Count;
        public int UnreturnedHeroCount => _leased.Count;
        public long EarnedExperience => _earned;
        public bool IsSpawningComplete => _isSpawningComplete;
        public eRoundOutcome Outcome { get; private set; }
        public PooledStageBattle(HeroPool pool, IStageDefenders defenders, Vector3 spawnPosition, IStageBattleLifecycle lifecycle = null,
            IReadOnlyList<Vector3> spawnPositions = null)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _defenders = defenders ?? throw new ArgumentNullException(nameof(defenders));
            _lifecycle = lifecycle;
            if (spawnPositions != null && spawnPositions.Count == 0) throw new ArgumentException("Spawn positions cannot be empty.");
            _spawnPositions = new Vector3[spawnPositions?.Count ?? 1];
            for (int i = 0; i < _spawnPositions.Length; i++)
            {
                var point = spawnPositions == null ? spawnPosition : spawnPositions[i];
                if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                    throw new ArgumentException("Spawn positions must be finite.");
                _spawnPositions[i] = point;
            }
            _deathHandler = RecordDeath;
            _returnHandler = ReturnHero;
            _faultHandler = RecordHeroFault;
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
                RoundResult confirmedResult = null;
                try
                {
                    foreach (var entry in round.Spawns)
                        if (!_pool.Contains(entry.HeroId)) throw new InvalidOperationException("Missing pool: " + entry.HeroId);
                    await _defenders.PrepareRoundAsync(lifetime.Token);
                    if (_lifecycle != null) await _lifecycle.BeginRoundAsync(lifetime.Token);
                    lifetime.Token.ThrowIfCancellationRequested();
                    spawning = SpawnAsync(round, lifetime.Token);
                    while (true)
                    {
                        await Task.Yield();
                        lifetime.Token.ThrowIfCancellationRequested();
                        if (CleanupError != null) throw CleanupError;
                        if (spawning.IsFaulted) await spawning;
                        Outcome = RoundCompletionEvaluator.Evaluate(new BattleProgress(_isSpawningComplete, _alive.Count, _defenders.AliveCount));
                        // 동시 전멸은 패배로 정산하되 진단용 Outcome은 유지한다.
                        if (Outcome != eRoundOutcome.ONGOING)
                        {
                            confirmedResult = new RoundResult(Outcome == eRoundOutcome.VICTORY ? eBattleResult.VICTORY : eBattleResult.DEFEAT, _earned);
                            return confirmedResult;
                        }
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
                    try { if (_lifecycle != null) await _lifecycle.EndRoundAsync(); }
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
                        CleanupError = confirmedResult != null
                            ? new StageBattleCleanupException(confirmedResult, errors)
                            : new AggregateException("Battle cleanup failed; all leases were processed.", errors);
                        throw CleanupError;
                    }
                }
            }
        }
        /// <summary>true면 직업별로 몰아서 내보내지 않고 모든 직업을 비율대로 섞어서 한 줄로 내보낸다(보스는 웨이브 중간).</summary>
        private static readonly bool InterleaveSpawns = true;
        /// <summary>보스가 나오는 시점(0=맨 처음, 1=맨 마지막)을 전체 스폰 순서 대비 비율로.</summary>
        private const float BossSpawnPoint = 0.5f;

        private readonly struct SpawnSlot
        {
            public readonly string HeroId;
            public readonly float IntervalSeconds;
            public SpawnSlot(string heroId, float intervalSeconds) { HeroId = heroId; IntervalSeconds = intervalSeconds; }
        }

        /// <summary>
        /// 라운드의 스폰 목록을 실제로 내보낼 순서로 펼친다. 시트 순서(전사 전부 → 방패병 전부 → …)는 근접이 먼저 다 죽고
        /// 원거리가 뒤에 한꺼번에 몰려 오는 흐름을 만들어서, 직업별 수를 균등한 간격으로 섞는다(예: 6:4면 A B A B A A B A B A ...).
        /// 각 항목의 k번째 개체에 (k+0.5)/수량 위치를 주고 그 위치 순으로 정렬하는 방식이라 항상 같은 결과가 나온다.
        /// </summary>
        private static List<SpawnSlot> BuildSpawnOrder(RoundDefinition round)
        {
            var slots = new List<SpawnSlot>();
            if (!InterleaveSpawns)
            {
                foreach (var entry in round.Spawns)
                    for (int index = 0; index < entry.Count; index++) slots.Add(new SpawnSlot(entry.HeroId, entry.IntervalSeconds));
                return slots;
            }

            var mixed = new List<(float position, int entryIndex, int index, SpawnSlot slot)>();
            var tail = new List<SpawnSlot>();
            int entryNumber = 0;
            foreach (var entry in round.Spawns)
            {
                bool isBoss = entry.HeroId.IndexOf("BOSS", StringComparison.OrdinalIgnoreCase) >= 0;
                for (int index = 0; index < entry.Count; index++)
                {
                    var slot = new SpawnSlot(entry.HeroId, entry.IntervalSeconds);
                    if (isBoss) tail.Add(slot);
                    else mixed.Add(((index + 0.5f) / entry.Count, entryNumber, index, slot));
                }
                entryNumber++;
            }

            mixed.Sort((a, b) =>
            {
                int byPosition = a.position.CompareTo(b.position);
                if (byPosition != 0) return byPosition;
                int byEntry = a.entryIndex.CompareTo(b.entryIndex);
                return byEntry != 0 ? byEntry : a.index.CompareTo(b.index);
            });
            foreach (var item in mixed) slots.Add(item.slot);
            // 보스는 마지막이 아니라 웨이브 중간쯤에 등장시킨다(앞뒤로 졸개가 이어진다).
            slots.InsertRange(Mathf.RoundToInt(slots.Count * BossSpawnPoint), tail);
            return slots;
        }

        private async Task SpawnAsync(RoundDefinition round, CancellationToken token)
        {
            float previousInterval = 0;
            int spawnIndex = 0;
            foreach (var slot in BuildSpawnOrder(round))
            {
                if (previousInterval > 0)
                    await Task.Delay(TimeSpan.FromSeconds(previousInterval), token);
                token.ThrowIfCancellationRequested();
                var lease = _pool.Rent(slot.HeroId, _spawnPositions[spawnIndex], _deathHandler, _returnHandler, _faultHandler);
                spawnIndex = (spawnIndex + 1) % _spawnPositions.Length;
                if (round.HpMultiplier != 1f || round.AttackMultiplier != 1f)
                {
                    var unit = lease.Hero.GetComponent<UnitBase>();
                    if (unit != null) unit.ApplyRoundDifficultyMultiplier(round.HpMultiplier, round.AttackMultiplier);
                }
                _leased.Add(lease.LeaseId, lease);
                _alive.Add(lease.LeaseId, lease);
                _experience.Add(lease.LeaseId, _pool.GetExperience(slot.HeroId));
                // OnEnable에서 사망 통지가 와도 먼저 등록된 대여만 처리한다.
                lease.Hero.gameObject.SetActive(true);
                previousInterval = slot.IntervalSeconds;
            }
            _isSpawningComplete = true;
        }
        private void RecordHeroFault(HeroLease lease, Exception exception)
        {
            CleanupError = CleanupError == null
                ? new AggregateException("Hero death processing failed.", exception)
                : new AggregateException(CleanupError, exception);
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
