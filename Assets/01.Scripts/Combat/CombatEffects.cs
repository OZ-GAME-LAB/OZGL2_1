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

    public static void PlayHit(Vector3 position)
    {
        float extraScale = Catalog != null ? Catalog.hitEffectScaleMultiplier : 1f;
        float alpha = Catalog != null ? Catalog.hitEffectAlpha : 1f;
        SpawnOneShot(Catalog != null ? Catalog.hitEffect : null, position, extraScale, alpha);
    }
    public static void PlayHeal(Vector3 position) => SpawnOneShot(Catalog != null ? Catalog.healEffect : null, position);
    public static void PlayMagicCast(Vector3 position) => SpawnOneShot(Catalog != null ? Catalog.magicCastEffect : null, position);
    public static void PlaySaintCast(Vector3 position) => SpawnOneShot(Catalog != null ? Catalog.saintCastEffect : null, position);

    /// <summary>피격 데미지 숫자를 position 위로 띄운다. amount가 0 이하(보호막 흡수 등)면 아무것도 안 띄운다.</summary>
    public static void PlayDamageNumber(Vector3 feetPosition, float spriteHeight, int amount, UnitSide victimSide)
    {
        if (amount <= 0 || Catalog == null || !Catalog.showDamageNumbers)
        {
            return;
        }

        Vector3 position = feetPosition + Vector3.up * (spriteHeight * Catalog.damageNumberHeightRatio);
        Color color = victimSide == UnitSide.Hero ? Catalog.heroDamageColor : Catalog.demonArmyDamageColor;
        DamageNumber.Show(position, amount, color, Catalog.damageNumberFontSize,
            Catalog.damageNumberRiseDistance, Catalog.damageNumberDuration);
    }

    /// <summary>회복량(실제로 차오른 양) 숫자를 초록색 "+N"으로 띄운다. 이미 풀피라 0이면 안 띄운다.</summary>
    public static void PlayHealNumber(Vector3 feetPosition, float spriteHeight, int healedAmount)
    {
        if (healedAmount <= 0 || Catalog == null || !Catalog.showDamageNumbers)
        {
            return;
        }

        Vector3 position = feetPosition + Vector3.up * (spriteHeight * Catalog.damageNumberHeightRatio);
        DamageNumber.Show(position, healedAmount, Catalog.healNumberColor, Catalog.damageNumberFontSize,
            Catalog.damageNumberRiseDistance, Catalog.damageNumberDuration, "+");
    }

    private static void SpawnOneShot(GameObject prefab, Vector3 position, float extraScale = 1f, float alpha = 1f)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
        ApplyScale(instance);
        if (!Mathf.Approximately(extraScale, 1f))
        {
            instance.transform.localScale *= extraScale;
        }
        ApplyAlpha(instance, alpha);
        float lifetime = GetParticleLifetime(instance);
        instance.AddComponent<TimedVisualEffect>().Init(lifetime);
    }

    /// <summary>이펙트를 반투명하게: 파티클 시작색과 스프라이트 색의 알파에 배율을 곱한다.</summary>
    private static void ApplyAlpha(GameObject instance, float alpha)
    {
        if (alpha >= 0.999f)
        {
            return;
        }

        foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            main.startColor = ScaleAlpha(main.startColor, alpha);
        }

        foreach (var sprite in instance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Color c = sprite.color;
            c.a *= alpha;
            sprite.color = c;
        }
    }

    private static ParticleSystem.MinMaxGradient ScaleAlpha(ParticleSystem.MinMaxGradient source, float alpha)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
            {
                Color c = source.color;
                c.a *= alpha;
                return new ParticleSystem.MinMaxGradient(c);
            }
            case ParticleSystemGradientMode.TwoColors:
            {
                Color lo = source.colorMin;
                Color hi = source.colorMax;
                lo.a *= alpha;
                hi.a *= alpha;
                return new ParticleSystem.MinMaxGradient(lo, hi);
            }
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(ScaleGradientAlpha(source.gradient, alpha));
            case ParticleSystemGradientMode.RandomColor:
                return new ParticleSystem.MinMaxGradient(ScaleGradientAlpha(source.gradient, alpha))
                {
                    mode = ParticleSystemGradientMode.RandomColor
                };
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    ScaleGradientAlpha(source.gradientMin, alpha), ScaleGradientAlpha(source.gradientMax, alpha));
            default:
                return source;
        }
    }

    private static Gradient ScaleGradientAlpha(Gradient source, float alpha)
    {
        var result = new Gradient();
        if (source == null)
        {
            return result;
        }

        GradientAlphaKey[] alphaKeys = source.alphaKeys;
        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i].alpha *= alpha;
        }
        result.SetKeys(source.colorKeys, alphaKeys);
        return result;
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
