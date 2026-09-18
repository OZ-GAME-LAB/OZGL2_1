using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원래 타겟을 매 프레임 다시 조준하는(유도) 단일 명중(비관통) 발사체.
/// 원래 타겟이 죽거나 사라지면 그 시점에 가장 가까운 살아있는 적으로 갈아타서 계속 유도한다.
/// 아무도 없으면 마지막 방향으로 최대 사거리까지 날아가다 소멸. 어느 경우든 딱 한 대상만 맞고 사라진다(관통 없음).
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

    private UnitBase originalTarget;
    private UnitSide enemySide;
    private Vector3 direction;
    private int damage;
    private float speed;
    private float traveledDistance;
    private Vector2 defaultFacing = Vector2.up;
    private float splashRadius;

    private const float HitDistance = 0.15f;

    public void Init(UnitBase targetUnit, int damageAmount, float moveSpeed, Vector2 spriteDefaultFacing, float splashRadiusAmount = 0f)
    {
        originalTarget = targetUnit;
        damage = damageAmount;
        speed = moveSpeed;
        defaultFacing = spriteDefaultFacing.sqrMagnitude > 0.0001f ? spriteDefaultFacing.normalized : Vector2.up;
        splashRadius = splashRadiusAmount;
        enemySide = targetUnit != null ? targetUnit.Side : UnitSide.Hero;

        Vector3 aimPoint = targetUnit != null ? targetUnit.transform.position : transform.position;
        direction = (aimPoint - transform.position).normalized;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.down;
        }

        FaceDirection(direction);
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
        UnitBase liveTarget = ResolveLiveTarget();
        float step = speed * Time.deltaTime;

        if (liveTarget != null)
        {
            Vector3 toTarget = liveTarget.transform.position - transform.position;
            float distance = toTarget.magnitude;

            // 이번 프레임 이동거리(step)가 남은 거리보다 크면(=지나쳐버릴 프레임) 그 자리에서 바로 명중 처리.
            // 이동 후에 거리를 재는 방식은 한 프레임에 훌쩍 지나쳐버려서 계속 못 맞히는 문제가 있었음.
            if (distance <= Mathf.Max(step, HitDistance))
            {
                transform.position = liveTarget.transform.position;
                Hit(liveTarget);
                return;
            }

            direction = toTarget / distance;
            FaceDirection(direction);
        }

        transform.position += direction * step;
        traveledDistance += step;

        if (traveledDistance >= maxTravelDistance)
        {
            Destroy(gameObject); // 아무도 못 맞히고 사거리 끝까지 날아감 — 그대로 소멸
        }
    }

    /// <summary>
    /// 유도 대상 결정: 원래 타겟이 살아있으면 그대로 유지. 죽었거나 사라졌으면 그 시점에 가장 가까운
    /// 살아있는 적으로 갈아타서 계속 유도한다(예전엔 방향을 고정해버려서 옆에 다른 적이 있어도 그냥 지나쳤음).
    /// </summary>
    private UnitBase ResolveLiveTarget()
    {
        if (originalTarget != null && originalTarget.currentState != UnitState.Dead)
        {
            return originalTarget;
        }

        var candidates = UnitRegistry.GetUnits(enemySide);
        UnitBase nearest = null;
        float nearestDistSqr = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            UnitBase unit = candidates[i];
            if (unit == null || unit.currentState == UnitState.Dead)
            {
                continue;
            }

            float distSqr = (unit.transform.position - transform.position).sqrMagnitude;
            if (distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest = unit;
            }
        }

        originalTarget = nearest; // 갈아탄 대상을 새 유도 대상으로 갱신
        return nearest;
    }

    private void Hit(UnitBase target)
    {
        target.TakeDamage(damage);
        ApplySplashDamage(target);
        Destroy(gameObject);
    }

    /// <summary>스플래시(마법사 광역 등, splashRadius > 0인 경우만): 명중 지점 기준 반경 안의 다른 적도 동일 피해.</summary>
    private void ApplySplashDamage(UnitBase primaryTarget)
    {
        if (splashRadius <= 0f)
        {
            return;
        }

        var candidates = UnitRegistry.GetUnits(enemySide);
        float radiusSqr = splashRadius * splashRadius;

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
                unit.TakeDamage(damage);
            }
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
