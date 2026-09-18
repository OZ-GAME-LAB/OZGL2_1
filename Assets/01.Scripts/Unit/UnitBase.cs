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

    // 원거리 유닛의 발사체가 몸 안쪽이 아니라 무기(활 등) 위치에서 나가도록 지정하는 자식 트랜스폼.
    // 프리팹마다(팩마다) 리그 구조가 달라서 자동 탐색 대신 인스펙터에서 직접 지정한다.
    // 비워두면 기존처럼 유닛 루트 위치에서 스폰됨(근접 유닛은 안 써도 무방).
    [Header("원거리 발사 위치 (선택)")]
    public Transform muzzlePoint;

    [Header("사망 처리")]
    // DEATH 애니메이션 클립 길이를 못 읽어올 때 쓰는 기본 대기 시간(초). 용사 제거 딜레이용.
    public float deathDestroyDelay = 1.2f;

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

    protected virtual void OnDestroy()
    {
        // ApplyStarLevel()에서 만든 런타임 복제본이면 같이 정리 (원본 공용 에셋은 절대 여기서 안 지움)
        if (baseStatData != null && statData != null && statData != baseStatData)
        {
            Destroy(statData);
        }
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
            SetState(GetPostCombatState());
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (statData != null && dist > statData.attackRange)
        {
            // 대상이 사거리를 벗어남 (이동형 대상 등) — 재탐색 상태로 복귀
            currentTarget = null;
            SetState(GetPostCombatState());
            return;
        }

        FaceDirection(currentTarget.transform.position - transform.position);

        attackCooldownTimer -= Time.deltaTime;
        if (attackCooldownTimer <= 0f)
        {
            PerformAttack(currentTarget);
            // 같은 Attack 상태를 유지한 채 반복되는 스윙 — 상태 전이가 없어서 SetState는 다시 안 불리므로
            // 스윙마다(쿨다운 완료 시점마다) 직접 트리거를 다시 넣어준다.
            PlaySpumAnimation(UnitState.Attack);
            attackCooldownTimer = GetAttackInterval();
        }
    }

    /// <summary>
    /// 전투가 끝난 뒤 돌아갈 상태. 용사는 마왕을 향해 계속 전진해야 하니 Move(4.1절),
    /// 마왕군은 배치형이라 제자리에서 Idle로 대기하며 재탐지한다.
    /// </summary>
    protected virtual UnitState GetPostCombatState()
    {
        return Side == UnitSide.Hero ? UnitState.Move : UnitState.Idle;
    }

    /// <summary>
    /// SPUM DEATH 클립(index 0)의 실제 길이를 읽어서 반환. 클립을 못 찾으면 deathDestroyDelay로 대체.
    /// </summary>
    protected virtual float GetDeathAnimationDuration()
    {
        if (spumPrefabs != null &&
            spumPrefabs.StateAnimationPairs.TryGetValue(PlayerState.DEATH.ToString(), out var clips) &&
            clips.Count > 0 && clips[0] != null)
        {
            return clips[0].length;
        }

        return deathDestroyDelay;
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
            return;
        }

        float targetDefense = target.statData != null ? target.statData.defensePercent : 0f;
        int damage = CalculateDamage(statData.attackPower, targetDefense);

        if (statData.projectilePrefab != null)
        {
            LaunchProjectile(target, damage);
        }
        else
        {
            target.TakeDamage(damage);
        }
    }

    /// <summary>
    /// 관통 없는 단일 대상 유도 발사체 생성. 데미지는 발사 시점 방어율로 미리 계산해서 들려 보내고,
    /// 실제 적용은 Projectile이 명중했을 때(target.TakeDamage) 처리한다.
    /// </summary>
    protected virtual void LaunchProjectile(UnitBase target, int damage)
    {
        Vector3 spawnPosition = muzzlePoint != null ? muzzlePoint.position : transform.position;
        GameObject projectileObj = Instantiate(statData.projectilePrefab, spawnPosition, Quaternion.identity);
        Projectile projectile = projectileObj.AddComponent<Projectile>();
        projectile.Init(target, damage, statData.projectileSpeed);
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

        // 상태 진입 시 1회 재생. MOVE/DEBUFF는 Bool이라 계속 켜둬도 안전하지만
        // IDLE/ATTACK/DEATH/OTHER는 Trigger라서 매 프레임 다시 넣으면 전환이 끝나기도 전에
        // 계속 재시작당해 애니메이션이 멈춘 것처럼 보인다 — 그래서 여기서 "전이 시 1회"만 재생한다.
        // Attack 상태가 유지되는 동안 반복되는 스윙은 TickAttack()에서 공격이 실제로 나갈 때마다 따로 재생.
        PlaySpumAnimation(newState);
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
            // 사망 애니메이션이 다 재생될 시간을 준 다음 제거 (즉시 Destroy하면 트리거만 넣고 바로 사라짐)
            Destroy(gameObject, GetDeathAnimationDuration());
        }
        // 마왕군은 Dead 상태로 남겨둔다 — 다음 라운드 시작 시 라운드 매니저(코어루프 파트)가 Revive()를 호출해 부활시키는 구조로 예정 (4.3절)

        CheckRoundOutcome();
    }

    /// <summary>
    /// 승패 스텁 — 정식 라운드 매니저(코어루프 파트)가 붙기 전까지 콘솔 로그로만 확인.
    /// 마왕군 전멸=패배, 용사 전멸=라운드 클리어. 실제 UI/씬 전환 등은 코어루프 파트가 담당할 예정.
    /// </summary>
    protected virtual void CheckRoundOutcome()
    {
        if (UnitRegistry.GetAliveCount(UnitSide.DemonArmy) <= 0)
        {
            Debug.Log("[Round] 패배 — 마왕군 전멸");
        }
        else if (UnitRegistry.GetAliveCount(UnitSide.Hero) <= 0)
        {
            Debug.Log("[Round] 라운드 클리어 — 용사 전멸");
        }
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

    // 항상 1성 기준값(원본 공용 에셋)을 기억해두는 참조 — 성급 재계산의 기준.
    protected UnitStatData baseStatData;

    /// <summary>
    /// 합성(5.2절)으로 성급이 오를 때 FusionRules가 호출한다.
    /// UnitStatData는 같은 종류 유닛끼리 공유하는 에셋이라 직접 수정하면 그 종류 전체가 같이
    /// 바뀌어버리므로, 최초 호출 시 이 유닛만의 런타임 복제본을 만들어서 배율을 적용한다.
    /// </summary>
    public virtual void ApplyStarLevel(int newStar)
    {
        if (statData == null)
        {
            return;
        }

        if (baseStatData == null)
        {
            baseStatData = statData; // 원본(1성 값) 기억, 최초 1회만
        }

        if (statData == baseStatData)
        {
            statData = Instantiate(baseStatData); // 이 유닛 전용 복제본으로 교체 — 원본 에셋은 보호됨
        }

        float multiplier = UnitStatData.GetStarMultiplier(newStar);
        statData.starLevel = newStar;
        statData.maxHealth = Mathf.RoundToInt(baseStatData.maxHealth * multiplier);
        statData.attackPower = baseStatData.attackPower * multiplier;
        statData.healAmount = baseStatData.healAmount * multiplier;

        currentHealth = statData.maxHealth; // 합성 시 풀피로 시작
    }
}
