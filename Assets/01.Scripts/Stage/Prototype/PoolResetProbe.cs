#if UNITY_EDITOR
using UnityEngine;

namespace OZGL2.Stage.Prototype
{
    /// <summary>재사용 경계 검사용 컴포넌트. 게임 빌드에는 포함되지 않습니다.</summary>
    public sealed class PoolResetProbe : MonoBehaviour, IPooledHeroState
    {
        public long CurrentLease { get; private set; }
        public int SpawnCount { get; private set; }
        public int ReturnCount { get; private set; }
        public void ResetForSpawn(long leaseId) { CurrentLease = leaseId; SpawnCount++; }
        public void ResetForReturn() { CurrentLease = 0; ReturnCount++; }
    }
}
#endif
