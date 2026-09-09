using UnityEngine;

namespace OZGL2.Contracts
{
    /// <summary>
    /// 상태이상(정지·둔화·넉백·취약 등)을 받을 수 있는 대상. 스킬·시너지·디버프가 사용.
    /// IDamageable 과 별개 인터페이스.
    /// </summary>
    public interface IStatusReceiver
    {
        /// <summary>seconds 동안 행동 불가.</summary>
        void ApplyStun(float seconds);

        /// <summary>seconds 동안 이동·공격 속도에 multiplier(0~1) 배율.</summary>
        void ApplySlow(float multiplier, float seconds);

        /// <summary>dir 방향으로 force 만큼 밀려남.</summary>
        void ApplyKnockback(Vector3 dir, float force);

        /// <summary>seconds 동안 받는 피해에 multiplier(>1) 배율.</summary>
        void ApplyVulnerable(float multiplier, float seconds);
    }
}
