using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Stage
{
    /// <summary>Unity 메인 스레드 전용. 정상 사망은 반환하며, 초기화 실패 객체와 풀 폐기 시에만 파괴합니다.</summary>
    public sealed class HeroPool : IDisposable
    {
        private sealed class Bucket
        {
            internal readonly HeroPoolEntry Entry;
            internal readonly Stack<PooledHero> Available = new Stack<PooledHero>();
            internal Bucket(HeroPoolEntry entry) { Entry = entry; }
        }
        private readonly Dictionary<string, Bucket> _buckets = new Dictionary<string, Bucket>();
        private readonly Dictionary<PooledHero, Bucket> _owners = new Dictionary<PooledHero, Bucket>();
        private readonly Dictionary<PooledHero, long> _active = new Dictionary<PooledHero, long>();
        private readonly Transform _root;
        private long _nextLease;
        private bool _isDisposed;
        public int CreatedCount => _owners.Count;
        public int ActiveCount => _active.Count;
        public HeroPool(IEnumerable<HeroPoolEntry> entries, Transform root)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.HeroId) || entry.Prefab == null ||
                    entry.InitialCapacity < 0 || entry.GrowthCount <= 0 || entry.Experience < 0)
                    throw new ArgumentException("Invalid hero pool entry.");
                _buckets.Add(entry.HeroId, new Bucket(entry));
            }
            try { foreach (var bucket in _buckets.Values) Grow(bucket, bucket.Entry.InitialCapacity); }
            catch (Exception initializationError)
            {
                try { Dispose(); }
                catch (Exception cleanupError) { throw new AggregateException(initializationError, cleanupError); }
                throw;
            }
        }
        public int GetExperience(string heroId) => _buckets[heroId].Entry.Experience;
        public bool Contains(string heroId) => _buckets.ContainsKey(heroId);
        public HeroLease Rent(string heroId, Vector3 position, Action<HeroLease> onDeath, Action<HeroLease> onReturnReady,
            Action<HeroLease, Exception> onFault = null)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(HeroPool));
            if (onDeath == null) throw new ArgumentNullException(nameof(onDeath));
            if (onReturnReady == null) throw new ArgumentNullException(nameof(onReturnReady));
            var bucket = _buckets[heroId];
            if (bucket.Available.Count == 0) Grow(bucket, bucket.Entry.GrowthCount);
            var hero = bucket.Available.Pop();
            long id = checked(++_nextLease);
            _active.Add(hero, id);
            try
            {
                hero.transform.SetPositionAndRotation(position, Quaternion.identity);
                hero.Rent(id, onDeath, onReturnReady, onFault);
            }
            catch (Exception initializationError)
            {
                // Spawn 초기화 실패도 반환 성공 여부와 무관하게 재사용 대상에서 제외한다.
                _active.Remove(hero); _owners.Remove(hero);
                var errors = new List<Exception> { initializationError };
                try { if (hero != null) hero.Return(); }
                catch (Exception exception) { errors.Add(exception); }
                try { if (hero != null) UnityEngine.Object.Destroy(hero.gameObject); }
                catch (Exception exception) { errors.Add(exception); }
                throw new AggregateException("Hero spawn reset failed; instance discarded.", errors);
            }
            return new HeroLease(hero, id);
        }
        public bool Return(HeroLease lease)
        {
            if (lease.Hero == null || !_active.TryGetValue(lease.Hero, out var id) || id != lease.LeaseId) return false;
            _active.Remove(lease.Hero);
            try { lease.Hero.Return(); }
            catch (Exception resetError)
            {
                _owners.Remove(lease.Hero);
                try { if (lease.Hero != null) UnityEngine.Object.Destroy(lease.Hero.gameObject); }
                catch (Exception destroyError) { throw new AggregateException(resetError, destroyError); }
                throw;
            }
            _owners[lease.Hero].Available.Push(lease.Hero);
            return true;
        }
        private void Grow(Bucket bucket, int count)
        {
            // 비활성 부모 아래서 생성하여 초기화 전에 OnEnable이 실행되지 않게 한다.
            var staging = new GameObject("Pool initialization");
            staging.SetActive(false);
            staging.transform.SetParent(_root, false);
            try
            {
                for (int index = 0; index < count; index++)
                {
                    var hero = UnityEngine.Object.Instantiate(bucket.Entry.Prefab, staging.transform);
                    hero.gameObject.SetActive(false);
                    hero.Initialize();
                    hero.transform.SetParent(_root, false);
                    _owners.Add(hero, bucket);
                    bucket.Available.Push(hero);
                }
            }
            finally { UnityEngine.Object.Destroy(staging); }
        }
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            List<Exception> errors = null;
            foreach (var hero in _owners.Keys)
            {
                if (hero == null) continue;
                try { if (hero.IsLeased) hero.Return(); }
                catch (Exception exception) { (errors ??= new List<Exception>()).Add(exception); }
                try { if (hero != null) UnityEngine.Object.Destroy(hero.gameObject); }
                catch (Exception exception) { (errors ??= new List<Exception>()).Add(exception); }
            }
            _active.Clear(); _owners.Clear(); _buckets.Clear();
            if (errors != null) throw new AggregateException("Pool disposal failed; all instances were processed.", errors);
        }
    }
}
