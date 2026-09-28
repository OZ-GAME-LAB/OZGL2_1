using UnityEngine;

/// <summary>
/// 원샷 전투 이펙트(피격/힐/시전 등)를 지정한 시간 뒤에 자동으로 파괴한다.
/// CombatEffects.SpawnOneShot()에서 인스턴스에 런타임으로 AddComponent 해서 붙인다.
/// </summary>
public sealed class TimedVisualEffect : MonoBehaviour
{
    public void Init(float lifetimeSeconds)
    {
        Destroy(gameObject, Mathf.Max(0.05f, lifetimeSeconds));
    }
}
