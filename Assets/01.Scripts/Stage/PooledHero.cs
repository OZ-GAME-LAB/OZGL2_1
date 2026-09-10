using System;
using UnityEngine;

namespace OZGL2.Stage
{
    /// <summary>팀원 유닛이 구현할 재사용 경계. 타깃/체력/상태이상/구독/비동기 작업을 정리합니다.</summary>
    public interface IPooledHeroState
    {
        void ResetForSpawn(long leaseId);
        void ResetForReturn();
    }
    /// <summary>프리팹당 한 구현체가 연출을 조율하고, 완료 시 해당 LeaseId로 TryCompleteDeath를 호출합니다.</summary>
    public interface IPooledHeroDeathPresentation
    {
        void BeginDeath(HeroLease lease);
    }
    public readonly struct HeroLease
    {
        public PooledHero Hero { get; }
        public long LeaseId { get; }
        public HeroLease(PooledHero hero, long leaseId) { Hero = hero; LeaseId = leaseId; }
    }
    public sealed class PooledHero : MonoBehaviour
    {
        private IPooledHeroState[] _states;
        private Action<HeroLease> _onDeath;
        private Action<HeroLease> _onReturnReady;
        private Action<HeroLease, Exception> _onFault;
        private IPooledHeroDeathPresentation _deathPresentation;
        private long _leaseId;
        private bool _isDead;
        private bool _isReturnRequested;
        public bool IsLeased => _onDeath != null;
        public long LeaseId => _leaseId;
        public bool IsDead => _isDead;
        internal void Initialize()
        {
            var components = GetComponentsInChildren<MonoBehaviour>(true);
            var states = new System.Collections.Generic.List<IPooledHeroState>();
            foreach (var component in components)
            {
                if (component is IPooledHeroState state) states.Add(state);
                if (component is IPooledHeroDeathPresentation presentation)
                {
                    if (_deathPresentation != null) throw new InvalidOperationException("Use one death presentation coordinator per hero.");
                    _deathPresentation = presentation;
                }
            }
            _states = states.ToArray();
        }
        internal void Rent(long id, Action<HeroLease> onDeath, Action<HeroLease> onReturnReady, Action<HeroLease, Exception> onFault)
        {
            _leaseId = id; _isDead = false; _onDeath = null;
            _isReturnRequested = false; _onReturnReady = null; _onFault = null;
            foreach (var state in _states) state.ResetForSpawn(id);
            _onDeath = onDeath;
            _onReturnReady = onReturnReady;
            _onFault = onFault;
        }
        /// <summary>공격/사망 작업 시작 때 받은 leaseId를 전달해야 늦은 이벤트를 차단할 수 있습니다.</summary>
        public bool TryReportDeath(long leaseId)
        {
            if (!IsLeased || _isDead || _leaseId != leaseId) return false;
            _isDead = true;
            var lease = new HeroLease(this, leaseId);
            // 연출이 동기 반환·재대여 후 실패하더라도 원래 전투로 오류를 전달한다.
            var onFault = _onFault;
            try
            {
                _onDeath(lease);
                if (IsLeased && _leaseId == leaseId && !_isReturnRequested)
                {
                    if (_deathPresentation == null) TryCompleteDeath(leaseId);
                    else _deathPresentation.BeginDeath(lease);
                }
            }
            catch (Exception exception)
            {
                Exception failure = exception;
                try { TryCompleteDeath(leaseId); }
                catch (Exception returnError) { failure = new AggregateException(exception, returnError); }
                if (onFault == null) throw new AggregateException("Hero death failed; return was attempted.", failure);
                onFault(lease, failure);
            }
            return true;
        }
        public bool TryCompleteDeath(long leaseId)
        {
            if (!IsLeased || !_isDead || _isReturnRequested || leaseId != _leaseId) return false;
            _isReturnRequested = true;
            _onReturnReady(new HeroLease(this, leaseId));
            return true;
        }
        internal void Return()
        {
            _onDeath = null;
            _onReturnReady = null;
            _onFault = null;
            System.Collections.Generic.List<Exception> errors = null;
            foreach (var state in _states)
            {
                try { state.ResetForReturn(); }
                catch (Exception exception) { (errors ??= new System.Collections.Generic.List<Exception>()).Add(exception); }
            }
            try { gameObject.SetActive(false); }
            catch (Exception exception) { (errors ??= new System.Collections.Generic.List<Exception>()).Add(exception); }
            if (errors != null) throw new AggregateException("Hero reset failed; all reset hooks were attempted.", errors);
        }
    }
}
