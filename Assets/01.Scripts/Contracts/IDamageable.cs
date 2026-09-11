using UnityEngine;

namespace OZGL2.Contracts
{
    /// <summary>
    /// 피해를 받을 수 있는 대상. 스킬·증강·시너지 시스템은 구체 유닛(큐브든 세진의 용사든)이 아니라
    /// 이 인터페이스에만 의존한다. 샌드박스에서는 CubeUnit 이, 실제 씬에서는 세진의 유닛이 구현.
    /// </summary>
    public interface IDamageable
    {
        float CurrentHp { get; }
        float MaxHp { get; }
        bool IsDead { get; }
        Vector3 Position { get; }

        void TakeDamage(float amount);
    }
}
