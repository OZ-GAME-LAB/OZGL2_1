using UnityEngine;

/// <summary>
/// 용사/마왕군 공통 유닛 베이스.
/// Day1: 상태(State) 골격 + 이동 + SPUM 애니메이션. Day2: 타겟팅 + 공격/치료 판정 + 사망 처리.
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

    [Header("전투 (Day2)")]
    public UnitBase currentTarget;
    protected float attackCooldownTimer;

    /// <summary>죽었을 때(Hero) EXP 지급 등을 위해 다른 파트(성민 - 성장시스템)가 구독할 수 있는 훅.</summary>
    public static event System.Action<UnitBase, int> OnHeroKilled;

    public UnitSide Side => statData != null ? statData.side : UnitSide.Hero;

    // SPUM 프리팹의 애니메이션 재생 담당 컴포넌트 (자식 오브젝트에 붙어있음, SPUM 샘플의 PlayerObj 참고)
    protected SPUM_Prefabs spumPrefabs;

    protected virtual void OnEnable()
    {
        UnitRegistry.Register(this);
    }

    protected virtual void OnDisable()
    {
        UnitRegistry.Unregister(this);
    }

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

        InitSpumAnimation();
    }

    /// <summary>
    /// SPUM 프리팹의 애니메이션 클립 목록을 채우고 OverrideController를 초기화한다.
    /// SPUM이 안 붙은(자식에 SPUM_Prefabs 없는) 테스트용 오브젝트에서도 에러 없이 그냥 스킵되게 처리.
    /// </summary>
    protected virtual void InitSpumAnimation()
    {
        spumPrefabs = GetComponentInChildren<SPUM_Prefabs>();
        if (spumPrefabs == null)
        {
            return;
        }

        if (!spumPrefabs.allListsHaveItemsExist())
        {
            spumPrefabs.PopulateAnimationLists();
        }
        spumPrefabs.OverrideControllerInit();
        PlaySpumAnimation(currentState);
    }

    /// <summary>
    /// UnitState → SPUM의 PlayerState로 매핑해서 애니메이션 재생.
    /// 해당 상태 클립이 아예 없는 유닛(예: 공격 애니메이션 미보유)은 조용히 스킵한다.
    /// </summary>
    protected virtual void PlaySpumAnimation(UnitState state)
    {
        if (spumPrefabs == null)
        {
            return;
        }

        string stateKey = ToSpumState(state).ToString();
        if (!spumPrefabs.StateAnimationPairs.TryGetValue(stateKey, out var clips) || clips.Count == 0)
        {
            return;
        }

        spumPrefabs.PlayAnimation(ToSpumState(state), 0);
    }

    protected static PlayerState ToSpumState(UnitState state)
    {
        switch (state)
        {
            case UnitState.Idle: return PlayerState.IDLE;
            case UnitState.Move: return PlayerState.MOVE;
            case UnitState.Attack: return PlayerState.ATTACK;
            case UnitState.Dead: return PlayerState.DEATH;
            default: return PlayerState.IDLE;
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
                TickAttack();
                break;
            case UnitState.Dead:
                // 별도 틱 없음 — Die()에서 진영별로 즉시 처리(용사 제거) 또는 대기(마왕군, Revive() 대기)
                break;
        }

        // SPUM 자체 샘플(PlayerObj.Update)과 동일하게 상태 전이 여부와 무관하게 매 프레임 재생 요청한다.
        // SPUM 애니메이터가 Trigger 기반이라, 상태가 바뀔 때 한 번만 호출하면 공격처럼 같은 상태를
        // 유지하며 반복되는 동작(스윙 반복 등)에서 두 번째 이후 재생이 안 됨.
        if (currentState != UnitState.Dead)
        {
            PlaySpumAnimation(currentState);
        }
    }

    protected virtual void TickIdle()
    {
        // 정지 상태(주로 마왕군)도 매 프레임 사거리 안에 교전 대상이 들어왔는지 확인한다.
        TryAcquireTarget();
    }

    protected virtual void TickMove()
    {
        if (TryAcquireTarget())
        {
            return;
        }

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

    /// <summary>
    /// 사거리 안에 교전(또는 치료) 대상이 있으면 Attack 상태로 전이한다. Idle/Move 양쪽에서 매 프레임 호출.
    /// </summary>
    protected virtual bool TryAcquireTarget()
    {
        if (statData == null)
        {
            return false;
        }

        UnitBase target = FindAttackTarget();
        if (target == null)
        {
            return false;
        }

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > statData.attackRange)
        {
            return false;
        }

        currentTarget = target;
        attackCooldownTimer = 0f; // 사거리 진입 즉시 첫 공격/치료가 나가도록
        SetState(UnitState.Attack);
        return true;
    }

    /// <summary>힐량이 있는 유닛(힐러)은 아군을, 그 외에는 적을 찾는다.</summary>
    protected virtual UnitBase FindAttackTarget()
    {
        if (statData != null && statData.healAmount > 0f)
        {
            return FindLowestHealthAlly();
        }
        return FindNearestEnemy();
    }

    protected virtual UnitBase FindNearestEnemy()
    {
        UnitSide enemySide = Side == UnitSide.Hero ? UnitSide.DemonArmy : UnitSide.Hero;
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

        return nearest;
    }

    /// <summary>같은 진영에서 체력 비율이 가장 낮은(그리고 풀피가 아닌) 아군을 찾는다.</summary>
    protected virtual UnitBase FindLowestHealthAlly()
    {
        var candidates = UnitRegistry.GetUnits(Side);

        UnitBase lowest = null;
        float lowestRatio = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            UnitBase unit = candidates[i];
            if (unit == null || unit == this || unit.currentState == UnitState.Dead || unit.statData == null)
            {
                continue;
            }

            if (unit.currentHealth >= unit.statData.maxHealth)
            {
                continue; // 이미 풀피면 치료 대상 아님
            }

            float ratio = (float)unit.currentHealth / unit.statData.maxHealth;
            if (ratio < lowestRatio)
            {
                lowestRatio = ratio;
                lowest = unit;
            }
        }

        return lowest;
    }

    protected virtual void TickAttack()
    {
        if (currentTarget == null || currentTarget.currentState == UnitState.Dead)
        {
            currentTarget = null;
            SetState(UnitState.Move);
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (statData != null && dist > statData.attackRange)
        {
            // 대상이 사거리를 벗어남 (이동형 대상 등) — 재탐색하도록 이동 상태로 복귀
            currentTarget = null;
            SetState(UnitState.Move);
            return;
        }

        FaceDirection(currentTarget.transform.position - transform.position);

        attackCooldownTimer -= Time.deltaTime;
        if (attackCooldownTimer <= 0f)
        {
            PerformAttack(currentTarget);
            attackCooldownTimer = GetAttackInterval();
        }
    }

    protected float GetAttackInterval()
    {
        float speed = statData != null ? statData.attackSpeed : 1f;
        return 1f / Mathf.Max(speed, 0.01f);
    }

    protected virtual void PerformAttack(UnitBase target)
    {
        if (statData == null || target == null)
        {
            return;
        }

        if (statData.healAmount > 0f)
        {
            target.Heal(Mathf.RoundToInt(statData.healAmount));
        }
        else
        {
            float targetDefense = target.statData != null ? target.statData.defensePercent : 0f;
            int damage = CalculateDamage(statData.attackPower, targetDefense);
            target.TakeDamage(damage);
        }
    }

    /// <summary>
    /// 밸런스시트 03.전투공식: 받는 피해 = 공격력 x (1 - min(방어%, 0.8)). 방어율 상한 80%(항상 최소 20% 관통).
    /// </summary>
    public static int CalculateDamage(float attackPower, float targetDefensePercent)
    {
        float mitigatedDefense = Mathf.Min(targetDefensePercent, 0.8f);
        float damage = attackPower * (1f - mitigatedDefense);
        return Mathf.Max(0, Mathf.RoundToInt(damage));
    }

    public virtual void Heal(int amount)
    {
        if (statData == null || currentState == UnitState.Dead)
        {
            return;
        }

        currentHealth = Mathf.Min(currentHealth + amount, statData.maxHealth);
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
        // 실제 재생은 Update()에서 매 프레임 처리 (SPUM 트리거 특성상 반복 호출이 필요함)
    }

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
        currentTarget = null;
        SetState(UnitState.Dead);

        if (Side == UnitSide.Hero)
        {
            int expReward = statData != null ? statData.killExpReward : 0;
            OnHeroKilled?.Invoke(this, expReward);
            Destroy(gameObject); // 용사는 처치 즉시 제거
        }
        // 마왕군은 Dead 상태로 남겨둔다 — 다음 라운드 시작 시 라운드 매니저(코어루프 파트)가 Revive()를 호출해 부활시키는 구조로 예정 (4.3절)
    }

    /// <summary>
    /// 라운드 전환 시 죽은 마왕군을 되살리기 위한 진입점 (4.3절: 라운드 중 죽은 마왕군은 다음 라운드 시작 시 부활).
    /// 실제 호출은 라운드 매니저(코어루프 파트, 아직 없음)가 담당할 예정 — 지금은 메서드만 준비.
    /// </summary>
    public virtual void Revive()
    {
        if (statData == null)
        {
            return;
        }

        currentHealth = statData.maxHealth;
        SetState(UnitState.Idle);
    }
}
