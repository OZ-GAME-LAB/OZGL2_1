using UnityEngine;

namespace OZGL2.Contracts
{
    /// <summary>회복·버프를 받을 수 있는 아군(몬스터). 흡혈 의식·축복 오라·광폭화 등이 사용.</summary>
    public interface IHealable
    {
        Vector3 Position { get; }
        bool IsDead { get; }

        /// <summary>amount 만큼 회복 (음수 불가).</summary>
        void Heal(float amount);

        /// <summary>최대 체력 비율(0~1)만큼 회복.</summary>
        void HealFraction(float fraction);

        /// <summary>seconds 동안 스탯 배율 버프. stat 은 문자열 키(attackSpeed/defense/attack).</summary>
        void ApplyBuff(string stat, float multiplier, float seconds);
    }
}
