using UnityEngine;

/// <summary>
/// 전투 이펙트 재생 진입점. CombatEffectCatalogSO를 Resources에서 지연 로드해서 쓴다.
/// 카탈로그가 없거나 특정 이펙트가 비어있으면 조용히 스킵한다(이펙트는 연출이라 전투 로직에 영향 없음).
/// </summary>
public static class CombatEffects
{
    private const string CatalogResourcePath = "CombatEffectCatalog";

    private static CombatEffectCatalogSO _catalog;
    private static bool _loaded;

    private static CombatEffectCatalogSO Catalog
    {
        get
        {
            if (!_loaded)
            {
                _catalog = Resources.Load<CombatEffectCatalogSO>(CatalogResourcePath);
                _loaded = true;
            }
            return _catalog;
        }
    }

    public static void PlayHit(Vector3 position) => SpawnOneShot(Catalog != null ? Catalog.hitEffect : null, position);
    public static void PlayHeal(Vector3 position) => SpawnOneShot(Catalog != null ? Catalog.healEffect : null, position);
    public static void PlayMagicCast(Vector3 position) => SpawnOneShot(Catalog != null ? Catalog.magicCastEffect : null, position);
    public static void PlaySaintCast(Vector3 position) => SpawnOneShot(Catalog != null ? Catalog.saintCastEffect : null, position);

    private static void SpawnOneShot(GameObject prefab, Vector3 position)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
        ApplyScale(instance);
        float lifetime = GetParticleLifetime(instance);
        instance.AddComponent<TimedVisualEffect>().Init(lifetime);
    }

    /// <summary>원본 SPUM 이펙트가 유닛 크기 대비 커서, 카탈로그의 공통 배율만큼 축소해서 재생한다.</summary>
    private static void ApplyScale(GameObject instance)
    {
        float scale = Catalog != null ? Catalog.effectScale : 1f;
        instance.transform.localScale *= scale;
    }

    /// <summary>
    /// 둔화처럼 상태가 유지되는 동안 계속 붙어있어야 하는 이펙트를 시작한다. 호출한 쪽(UnitBase)이
    /// 반환된 인스턴스를 들고 있다가 상태 만료 시 StopPersistent로 직접 정리해야 한다.
    /// </summary>
    public static GameObject StartPersistent(GameObject prefab, Transform parent)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Object.Instantiate(prefab, parent.position, Quaternion.identity, parent);
        instance.transform.localPosition = Vector3.zero;
        ApplyScale(instance);
        return instance;
    }

    public static void StopPersistent(GameObject instance)
    {
        if (instance != null)
        {
            Object.Destroy(instance);
        }
    }

    public static GameObject SlowEffectPrefab => Catalog != null ? Catalog.slowEffect : null;

    private static float GetParticleLifetime(GameObject instance)
    {
        var particle = instance.GetComponentInChildren<ParticleSystem>();
        if (particle == null)
        {
            return 1f;
        }

        var main = particle.main;
        return main.duration + main.startLifetime.constantMax;
    }
}
