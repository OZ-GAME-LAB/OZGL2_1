namespace OZGL2.Contracts
{
    /// <summary>
    /// 상태이상(정지·둔화 등)을 받을 수 있는 대상. 빙결 결계·시간 정지·저주 시너지 등이 사용.
    /// IDamageable 과 별개 인터페이스 — 피해는 못 받아도 상태이상은 받는 대상이 있을 수 있고, 그 반대도.
    /// </summary>
    public interface IStatusReceiver
    {
        /// <summary>seconds 동안 행동 불가.</summary>
        void ApplyStun(float seconds);

        /// <summary>seconds 동안 이동·공격 속도에 multiplier(0~1) 배율.</summary>
        void ApplySlow(float multiplier, float seconds);
    }
}
