#if UNITY_EDITOR
using System;
using UnityEngine;

namespace OZGL2.Stage.Prototype
{
    /// <summary>정리 실패 격리 검사용. 게임 빌드에는 포함되지 않습니다.</summary>
    public sealed class CleanupFailureProbe : MonoBehaviour, IPooledHeroState, IPooledHeroDeathPresentation
    {
        [SerializeField] private bool _canThrowOnSpawn;
        public bool CanThrowOnSpawn { get => _canThrowOnSpawn; set => _canThrowOnSpawn = value; }
        public bool CanThrowOnReturn { get; set; }
        public int ReturnAttempts { get; private set; }
        public bool CanThrowOnDeath { get; set; }
        public bool CanThrowAfterDeathReturn { get; set; }
        public void BeginDeath(HeroLease lease)
        {
            if (CanThrowOnDeath) throw new InvalidOperationException("Injected death presentation failure");
            lease.Hero.TryCompleteDeath(lease.LeaseId);
            if (CanThrowAfterDeathReturn) throw new InvalidOperationException("Injected failure after death return");
        }
        public void ResetForSpawn(long leaseId)
        {
            if (_canThrowOnSpawn) throw new InvalidOperationException("Injected spawn reset failure");
        }
        public void ResetForReturn()
        {
            ReturnAttempts++;
            if (CanThrowOnReturn) throw new InvalidOperationException("Injected return reset failure");
        }
    }
}
#endif
