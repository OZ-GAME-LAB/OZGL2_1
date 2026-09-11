#if UNITY_EDITOR
namespace OZGL2.Stage.Prototype
{
    /// <summary>연출 완료를 수동 지연시키는 테스트 컴포넌트. 게임 빌드에는 포함되지 않습니다.</summary>
    public sealed class DeathPresentationProbe : UnityEngine.MonoBehaviour, IPooledHeroDeathPresentation, IPooledHeroState
    {
        public HeroLease PendingLease { get; private set; }
        public int BeginCount { get; private set; }
        public bool IsPlaying { get; private set; }
        public void BeginDeath(HeroLease lease)
        {
            PendingLease = lease;
            BeginCount++;
            IsPlaying = true;
        }
        public void ResetForSpawn(long leaseId) { PendingLease = default; IsPlaying = false; }
        public void ResetForReturn() { PendingLease = default; IsPlaying = false; }
    }
}
#endif
