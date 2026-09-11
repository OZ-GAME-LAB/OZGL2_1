using UnityEngine;

/// <summary>
/// 발사 시점 방향으로 고정된 직선 비행을 하는 단일 명중(비관통) 발사체.
/// 명중 우선순위: ① 원래 타겟이 아직 살아있고 사거리에 들어오면 그 대상.
///              ② 원래 타겟이 도중에 사라졌으면(죽음 등) 비행 경로상에서 사거리에 들어오는 다른 적.
///              ③ 그마저도 없으면 최대 사거리까지 그대로 날아가다 소멸.
/// 어느 경우든 딱 한 대상만 맞고 사라진다(관통 없음).
/// 프리팹 자체(Arrow 등)엔 비주얼(SpriteRenderer/Animator)만 있고, 이 컴포넌트는
/// UnitBase.LaunchProjectile()에서 인스턴스에 런타임으로 AddComponent 해서 붙인다.
/// 스프라이트 기본 방향이 "위(+Y)"라고 가정하고 회전 계산함 (다른 방향 스프라이트면 FaceDirection만 수정).
/// </summary>
public class Projectile : MonoBehaviour
{
    public float maxTravelDistance = 20f; // 아무도 못 맞히고 계속 날아갈 때 소멸시키는 최대 거리

    private UnitBase originalTarget;
    private UnitSide enemySide;
    private Vector3 direction;
    private int damage;
    private float speed;
    private float traveledDistance;

    private const float HitDistance = 0.15f;

    public void Init(UnitBase targetUnit, int damageAmount, float moveSpeed)
    {
        originalTarget = targetUnit;
        damage = damageAmount;
        speed = moveSpeed;
        enemySide = targetUnit != null ? targetUnit.Side : UnitSide.Hero;

        Vector3 aimPoint = targetUnit != null ? targetUnit.transform.position : transform.position;
        direction = (aimPoint - transform.position).normalized;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.down;
        }

        FaceDirection(direction);
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;
        transform.position += direction * step;
        traveledDistance += step;

        if (TryHitOriginalTarget())
        {
            return;
        }

        if (TryHitFallbackEnemy())
        {
            return;
        }

        if (traveledDistance >= maxTravelDistance)
        {
            Destroy(gameObject); // 아무도 못 맞히고 사거리 끝까지 날아감 — 그대로 소멸
        }
    }

    /// <summary>원래 타겟이 아직 살아있고 사거리 안에 들어오면 명중시킨다.</summary>
    private bool TryHitOriginalTarget()
    {
        if (originalTarget == null || originalTarget.currentState == UnitState.Dead)
        {
            return false;
        }

        if (Vector3.Distance(transform.position, originalTarget.transform.position) > HitDistance)
        {
            return false;
        }

        Hit(originalTarget);
        return true;
    }

    /// <summary>
    /// 원래 타겟이 이미 사라진 경우에만, 지금 위치 근처에 있는 다른 적(같은 진영)을 대신 명중시킨다.
    /// 원래 타겟이 아직 살아있는 동안엔 다른 대상으로 새지 않는다(관통 아님).
    /// </summary>
    private bool TryHitFallbackEnemy()
    {
        if (originalTarget != null && originalTarget.currentState != UnitState.Dead)
        {
            return false;
        }

        var candidates = UnitRegistry.GetUnits(enemySide);
        for (int i = 0; i < candidates.Count; i++)
        {
            UnitBase candidate = candidates[i];
            if (candidate == null || candidate.currentState == UnitState.Dead)
            {
                continue;
            }

            if (Vector3.Distance(transform.position, candidate.transform.position) <= HitDistance)
            {
                Hit(candidate);
                return true;
            }
        }

        return false;
    }

    private void Hit(UnitBase target)
    {
        target.TakeDamage(damage);
        Destroy(gameObject);
    }

    /// <summary>스프라이트 기본 방향이 위(+Y)인 것을 기준으로 진행 방향을 바라보게 회전.</summary>
    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
