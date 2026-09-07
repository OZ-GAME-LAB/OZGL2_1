using UnityEngine;

/// <summary>
/// 용사/마왕군 공통 유닛 베이스.
/// Day1 범위: 상태(State) 골격 + 이동만 구현. 타겟팅/공격은 Day2(Combat)에서 확장.
/// 실제 그리드/스폰 시스템이 붙기 전까지는 SetMoveTarget()으로 월드 좌표를 직접 넘겨 테스트한다.
/// </summary>
public class UnitBase : MonoBehaviour
{
    [Header("데이터")]
    public UnitStatData statData;

    [Header("런타임 상태")]
    public UnitState currentState = UnitState.Idle;
    public int currentHealth;

    [Header("이동")]
    public Vector3 moveTarget;
    public bool hasMoveTarget;

    [Header("도착 판정 (칸 이동이 아니라 월드 좌표 기준 임시 값)")]
    public float arriveThreshold = 0.05f;

    protected virtual void Awake()
    {
        if (statData != null)
        {
            currentHealth = statData.maxHealth;
        }
        else
        {
            Debug.LogWarning($"[UnitBase] {name}에 statData가 비어있음. 인스펙터에서 UnitStatData를 할당해줘.", this);
        }
    }

    protected virtual void Update()
    {
        switch (currentState)
        {
            case UnitState.Idle:
                TickIdle();
                break;
            case UnitState.Move:
                TickMove();
                break;
            case UnitState.Attack:
                // Day2에서 구현 (타겟팅 → 사거리 진입 시 이쪽 상태로 전이)
                break;
            case UnitState.Dead:
                // Day2에서 처리 (마왕군은 다음 라운드 부활, 용사는 즉시 제거 등)
                break;
        }
    }

    protected virtual void TickIdle()
    {
        // Day1에서는 별도 동작 없음. 애니메이션 훅은 나중에 여기 연결.
    }

    protected virtual void TickMove()
    {
        if (!hasMoveTarget || statData == null)
        {
            return;
        }

        Vector3 toTarget = moveTarget - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= arriveThreshold)
        {
            hasMoveTarget = false;
            SetState(UnitState.Idle);
            return;
        }

        Vector3 direction = toTarget.normalized;
        transform.position += direction * statData.moveSpeed * Time.deltaTime;
        FaceDirection(direction);
    }

    /// <summary>
    /// 좌우 이동 방향에 맞춰 스프라이트를 뒤집는다 (SPUM 프리팹은 localScale.x 반전 방식 사용).
    /// </summary>
    protected virtual void FaceDirection(Vector3 direction)
    {
        if (Mathf.Abs(direction.x) < 0.01f)
        {
            return;
        }

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (direction.x < 0f ? -1f : 1f);
        transform.localScale = scale;
    }

    public virtual void SetMoveTarget(Vector3 targetPosition)
    {
        moveTarget = targetPosition;
        hasMoveTarget = true;
        SetState(UnitState.Move);
    }

    public virtual void SetState(UnitState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        currentState = newState;
    }

    /// <summary>
    /// Day2에서 전투 판정 붙을 때 실제로 호출될 예정. 지금은 상태머신이 죽음까지 안 끊기는지만 확인용.
    /// </summary>
    public virtual void TakeDamage(int amount)
    {
        if (currentState == UnitState.Dead)
        {
            return;
        }

        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        currentHealth = 0;
        hasMoveTarget = false;
        SetState(UnitState.Dead);
    }
}
