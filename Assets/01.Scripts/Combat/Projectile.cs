using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원래 타겟을 매 프레임 다시 조준하는(유도) 단일 명중(비관통) 발사체.
/// 원래 타겟이 죽거나 사라지면 다른 적으로 갈아타지 않고 그 시점의 방향 그대로 직진한다. 직진 중 경로에 닿는 적이 있으면
/// 그 적에게 피해를 주고, 없으면 일정 거리(LostTargetFlightDistance)를 더 날아가다 사라진다. 어느 경우든 딱 한 대상만 맞고 사라진다(관통 없음).
/// 이동 후 거리로 명중을 판정하지 않고, "이번 프레임에 도달/지나칠 거리인가"로 미리 판정해서
/// 빠른 이동에 스쳐 지나가는(오버슈트) 문제를 막는다.
/// 프리팹 자체(Arrow, Fireball 등)엔 비주얼(SpriteRenderer/Animator)만 있고, 이 컴포넌트는
/// UnitBase.LaunchProjectile()에서 인스턴스에 런타임으로 AddComponent 해서 붙인다.
/// 프리팹마다 스프라이트/애니메이션 기본 방향이 다를 수 있어서(Arrow=위, Fireball=오른쪽 등),
/// UnitStatData.projectileDefaultFacing 값을 Init에서 받아 그 방향을 기준으로 회전 계산한다.
/// </summary>
public class Projectile : MonoBehaviour
{
    // CoreLoop-Connections-TeamGuide.md의 IInGameCombatParticipant 계약(ProjectileCombatParticipant)이
    // 라운드 전환 시 여기를 통해 신규 발사를 막고, 남은 발사체를 정리한다.
    public static bool CombatEnabled { get; set; } = true;
    private static readonly HashSet<Projectile> ActiveProjectiles = new HashSet<Projectile>();

    /// <summary>현재 날아다니는 발사체를 전부 즉시 파괴한다(라운드 종료/전환 시 잔여물 정리용).</summary>
    public static void DestroyAllActive()
    {
        foreach (Projectile projectile in new List<Projectile>(ActiveProjectiles))
        {
            if (projectile != null)
            {
                Destroy(projectile.gameObject);
            }
        }
        ActiveProjectiles.Clear();
    }

    public float maxTravelDistance = 20f; // 아무도 못 맞히고 계속 날아갈 때 소멸시키는 최대 거리

    private HitSoundKind hitSoundKind = HitSoundKind.Physical; // 명중 시 대상이 내는 피격음 종류(화살=Physical, 마법=Magic)
    private UnitBase originalTarget;
    private UnitSide enemySide;
    private Vector3 direction;
    private int damage;
    private float speed;
    private float traveledDistance;
    private Vector2 defaultFacing = Vector2.up;
    private float splashRadius;
    private float splashSecondaryDamagePercent = 1f;
    private int splashMaxTargets;

    private bool targetLost; // 표적이 죽어서 더는 유도하지 않고 직진 중

    private const float HitDistance = 0.15f;
    private const float StrayHitRadius = 0.4f;          // 직진 중 이 반경 안에 닿는 적에게 명중
    private const float LostTargetFlightDistance = 6f;  // 표적을 잃은 뒤 이만큼 더 날아가고 사라진다
    // 유닛 스프라이트(파츠별 sortingOrder 0~25)와 이펙트에 가려지지 않게 위로 올린다. 체력바(1000)·데미지 숫자(2000)보다는 아래.
    private const int SortingOrderOffset = 900;

    public void Init(UnitBase targetUnit, int damageAmount, float moveSpeed, Vector2 spriteDefaultFacing,
        float splashRadiusAmount = 0f, float splashSecondaryDamagePercentAmount = 1f, int splashMaxTargetsAmount = 0,
        HitSoundKind hitSound = HitSoundKind.Physical)
    {
        hitSoundKind = hitSound;
        originalTarget = targetUnit;
        damage = damageAmount;
        speed = moveSpeed;
        defaultFacing = spriteDefaultFacing.sqrMagnitude > 0.0001f ? spriteDefaultFacing.normalized : Vector2.up;
        splashRadius = splashRadiusAmount;
        splashSecondaryDamagePercent = splashSecondaryDamagePercentAmount;
        splashMaxTargets = splashMaxTargetsAmount;
        enemySide = targetUnit != null ? targetUnit.Side : UnitSide.Hero;

        Vector3 aimPoint = targetUnit != null ? targetUnit.transform.position : transform.position;
        direction = (aimPoint - transform.position).normalized;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.down;
        }

        FaceDirection(direction);
        RaiseSortingOrder();
    }

    /// <summary>프리팹 안의 모든 렌더러(스프라이트·파티클·트레일)를 원래 순서를 유지한 채 위 레이어로 올린다.</summary>
    private void RaiseSortingOrder()
    {
        foreach (Renderer projectileRenderer in GetComponentsInChildren<Renderer>(true))
        {
            projectileRenderer.sortingOrder += SortingOrderOffset;
        }
    }

    private void OnEnable()
    {
        ActiveProjectiles.Add(this);
    }

    private void OnDisable()
    {
        ActiveProjectiles.Remove(this);
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;

        // 표적이 죽거나 사라지면 다른 적으로 갈아타지 않고, 그 시점의 진행 방향 그대로 직진한다.
        if (!targetLost && (originalTarget == null || originalTarget.currentState == UnitState.Dead))
        {
            targetLost = true;
            maxTravelDistance = Mathf.Min(maxTravelDistance, traveledDistance + LostTargetFlightDistance);
        }

        if (!targetLost)
        {
            Vector3 toTarget = originalTarget.transform.position - transform.position;
            float distance = toTarget.magnitude;

            // 이번 프레임 이동거리(step)가 남은 거리보다 크면(=지나쳐버릴 프레임) 그 자리에서 바로 명중 처리.
            // 이동 후에 거리를 재는 방식은 한 프레임에 훌쩍 지나쳐버려서 계속 못 맞히는 문제가 있었음.
            if (distance <= Mathf.Max(step, HitDistance))
            {
                transform.position = originalTarget.transform.position;
                Hit(originalTarget);
                return;
            }

            direction = toTarget / distance;
            FaceDirection(direction);
        }
        else
        {
            // 직진 중에는 이번 프레임 이동 경로에 걸리는 가장 가까운 적에게 부딪힌다.
            UnitBase struck = FindStruckEnemy(step);
            if (struck != null)
            {
                transform.position = struck.transform.position;
                Hit(struck);
                return;
            }
        }

        transform.position += direction * step;
        traveledDistance += step;

        if (traveledDistance >= maxTravelDistance)
        {
            Destroy(gameObject); // 아무도 못 맞히고 사거리 끝까지 날아감 — 그대로 소멸
        }
    }

    /// <summary>
    /// 표적을 잃은 뒤 직진하는 동안의 충돌 판정: 이번 프레임 이동 구간(현재 위치→step만큼 앞)과 StrayHitRadius 안에 들어오는
    /// 살아있는 적 중 가장 먼저 닿는 하나를 고른다. 구간으로 판정해서 빠른 발사체가 적을 스쳐 지나치지 않는다.
    /// </summary>
    private UnitBase FindStruckEnemy(float step)
    {
        var candidates = UnitRegistry.GetUnits(enemySide);
        UnitBase struck = null;
        float struckAlong = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            UnitBase unit = candidates[i];
            if (unit == null || unit.currentState == UnitState.Dead)
            {
                continue;
            }

            Vector3 toUnit = unit.transform.position - transform.position;
            float along = Vector3.Dot(toUnit, direction);
            if (along < -StrayHitRadius || along > step + StrayHitRadius)
            {
                continue;
            }

            Vector3 closest = direction * Mathf.Clamp(along, 0f, step);
            if ((toUnit - closest).sqrMagnitude <= StrayHitRadius * StrayHitRadius && along < struckAlong)
            {
                struckAlong = along;
                struck = unit;
            }
        }

        return struck;
    }

    private void Hit(UnitBase target)
    {
        target.TakeDamage(damage, hitSoundKind);
        ApplySplashDamage(target);
        Destroy(gameObject);
    }

    /// <summary>
    /// 스플래시(마법사 광역 등, splashRadius > 0인 경우만): 명중 지점 기준 반경 안의 다른 적도 피해.
    /// 간접 피해 비율(splashSecondaryDamagePercent)과 최대 인원 수(splashMaxTargets, 0=무제한)로 너프 가능.
    /// 인원 제한이 있으면 가까운 대상부터 우선 적용한다.
    /// </summary>
    private void ApplySplashDamage(UnitBase primaryTarget)
    {
        if (splashRadius <= 0f)
        {
            return;
        }

        var candidates = UnitRegistry.GetUnits(enemySide);
        float radiusSqr = splashRadius * splashRadius;

        var inRange = new List<(UnitBase unit, float distSqr)>();
        for (int i = 0; i < candidates.Count; i++)
        {
            UnitBase unit = candidates[i];
            if (unit == null || unit == primaryTarget || unit.currentState == UnitState.Dead)
            {
                continue;
            }

            float distSqr = (unit.transform.position - primaryTarget.transform.position).sqrMagnitude;
            if (distSqr <= radiusSqr)
            {
                inRange.Add((unit, distSqr));
            }
        }

        inRange.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));
        int hitCount = splashMaxTargets > 0 ? Mathf.Min(splashMaxTargets, inRange.Count) : inRange.Count;
        int secondaryDamage = Mathf.Max(0, Mathf.RoundToInt(damage * splashSecondaryDamagePercent));

        for (int i = 0; i < hitCount; i++)
        {
            inRange[i].unit.TakeDamage(secondaryDamage, hitSoundKind);
        }
    }

    /// <summary>defaultFacing(프리팹 스프라이트 기본 방향)을 기준으로 진행 방향을 바라보게 회전.</summary>
    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float defaultAngle = Mathf.Atan2(defaultFacing.y, defaultFacing.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle - defaultAngle);
    }
}
