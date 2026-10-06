using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;
using OZGL2.Stage;
using OZGL2.Synergy;

/// <summary>
/// 용사/마왕군 공통 유닛 베이스.
/// Day1: 상태(State) 골격 + 이동 + SPUM 애니메이션. Day2: 타겟팅 + 공격/치료 판정 + 사망 처리.
/// 실제 그리드/스폰 시스템이 붙기 전까지는 SetMoveTarget()으로 월드 좌표를 직접 넘겨 테스트한다.
/// IDamageable/IHealable/IStatusReceiver 구현: 기존 int 기반 API는 그대로 두고, 성민 파트
/// 스킬 시스템이 이 유닛을 직접 때리고 힐하고 CC 걸 수 있도록 명시적 인터페이스 구현만 추가
/// (기존 공개 API 동작 변경 없음).
/// IPooledHeroState/IPooledHeroDeathPresentation 구현: 용사가 HeroPool로 스폰될 때 재사용
/// 리셋과 사망 연출을 이 컴포넌트가 직접 조율한다 (풀링 없이 직접 스폰하는 테스트 환경도 계속 지원).
/// </summary>
public class UnitBase : MonoBehaviour, IDamageable, IHealable, IStatusReceiver, IPooledHeroState, IPooledHeroDeathPresentation
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
    private int _attackCount; // 도적 콤보 스턴(N번째 공격마다) 판정용 — statData.comboStunAttackInterval

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

    // IDamageable (OZGL2.Contracts) — 성민 파트 스킬 시스템 전용 진입점. 기존 int 기반 API와 별개.
    float IDamageable.CurrentHp => currentHealth;
    float IDamageable.MaxHp => statData != null ? statData.maxHealth : 0f;
    bool IDamageable.IsDead => currentState == UnitState.Dead;
    Vector3 IDamageable.Position => transform.position;
    // 스킬 시스템(성민 파트) 진입점 — 스킬 피해는 마법 피격음으로 처리한다.
    void IDamageable.TakeDamage(float amount) => TakeDamage(Mathf.RoundToInt(amount), HitSoundKind.Magic);

    // IHealable (OZGL2.Contracts) — 힐/버프 스킬(흡혈 의식·광폭화 등) 전용 진입점.
    Vector3 IHealable.Position => transform.position;
    bool IHealable.IsDead => currentState == UnitState.Dead;
    void IHealable.Heal(float amount) => Heal(Mathf.RoundToInt(amount));
    void IHealable.HealFraction(float fraction)
    {
        if (statData == null || currentState == UnitState.Dead) return;
        Heal(Mathf.RoundToInt(statData.maxHealth * Mathf.Clamp01(fraction)));
    }
    void IHealable.ApplyBuff(string stat, float multiplier, float seconds)
    {
        switch (stat)
        {
            case "attack": _buffAttackMult = multiplier; _buffAttackExpire = Time.time + seconds; break;
            case "attackSpeed": _buffAttackSpeedMult = multiplier; _buffAttackSpeedExpire = Time.time + seconds; break;
            case "defense": _buffDefenseMult = multiplier; _buffDefenseExpire = Time.time + seconds; break;
        }
    }

    // IStatusReceiver (OZGL2.Contracts) — CC 스킬(정지·둔화·넉백·취약) 전용 진입점.
    // ccResistance(0~1, 보스 등)만큼 기절 지속시간과 둔화 강도를 줄여서 적용한다 — 면역은 아니고 "약하게" 걸림.
    void IStatusReceiver.ApplyStun(float seconds)
    {
        float resistance = statData != null ? statData.ccResistance : 0f;
        float resistedSeconds = seconds * (1f - resistance);
        _stunExpire = Mathf.Max(_stunExpire, Time.time + resistedSeconds);
    }
    void IStatusReceiver.ApplySlow(float multiplier, float seconds)
    {
        float resistance = statData != null ? statData.ccResistance : 0f;
        // multiplier가 1에 가까울수록 저항으로 상쇄 (예: 50% 감속 x 저항 60% = 20% 감속만 적용)
        _slowMult = Mathf.Lerp(1f, multiplier, 1f - resistance);
        _slowExpire = Time.time + seconds;
        // 지속 이펙트라 만료될 때까지 붙여두고, 이미 걸려있으면(갱신) 새로 만들지 않고 유지한다.
        if (_slowEffectInstance == null)
        {
            _slowEffectInstance = CombatEffects.StartPersistent(CombatEffects.SlowEffectPrefab, transform);
        }
    }
    void IStatusReceiver.ApplyKnockback(Vector3 dir, float force) => transform.position += dir.normalized * force;
    void IStatusReceiver.ApplyVulnerable(float multiplier, float seconds) { _vulnerableMult = multiplier; _vulnerableExpire = Time.time + seconds; }

    /// <summary>
    /// 화상(도트) 적용. 아직 팀 공용 IStatusReceiver 계약엔 없는, 이번 보스 전용 기믹이라
    /// UnitBase 공개 메서드로만 추가 — 스킬 시스템에서도 필요해지면 그때 인터페이스로 승격.
    /// 재적용 시 dps는 최신 값으로 덮어쓰고, 지속시간은 더 긴 쪽으로 갱신한다(ApplyStun과 동일 정책).
    /// </summary>
    public void ApplyBurn(float damagePerSecond, float seconds)
    {
        if (currentState == UnitState.Dead || damagePerSecond <= 0f || seconds <= 0f)
        {
            return;
        }

        _burnDps = damagePerSecond;
        _burnExpire = Mathf.Max(_burnExpire, Time.time + seconds);
    }

    private static readonly Color BurnTintColor = new Color(1f, 0.55f, 0.55f);
    private bool _burnTintActive;

    /// <summary>매 프레임 호출: 화상 중이면 BurnTickInterval마다 dps x 간격만큼 피해를 입히고, 화상 상태를 붉은 틴트로 표시한다.</summary>
    private void TickBurn()
    {
        bool burning = Time.time < _burnExpire;
        if (burning != _burnTintActive)
        {
            SetBurnTint(burning);
        }

        if (!burning)
        {
            return;
        }

        _burnTickTimer -= Time.deltaTime;
        if (_burnTickTimer <= 0f)
        {
            _burnTickTimer += BurnTickInterval;
            TakeDamage(Mathf.RoundToInt(_burnDps * BurnTickInterval), HitSoundKind.None); // 도트는 피격음 없음
        }
    }

    private void SetBurnTint(bool active)
    {
        _burnTintActive = active;
        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            if (_bodyRenderers[i] != null)
            {
                // 원래 색에 붉은 틴트를 곱해서 적용 — 해제 시엔 각 파츠의 원래 색으로 정확히 복귀.
                _bodyRenderers[i].color = active ? _bodyRendererOriginalColors[i] * BurnTintColor : _bodyRendererOriginalColors[i];
            }
        }
    }

    /// <summary>매 프레임 호출: auraHealAmount가 설정된 유닛(팔라딘류)은 공격 여부와 무관하게 주기적으로 주변 아군(자신 포함)을 회복한다.</summary>
    private void TickAuraHeal()
    {
        if (statData == null || statData.auraHealAmount <= 0f)
        {
            return;
        }

        _auraHealTimer -= Time.deltaTime;
        if (_auraHealTimer <= 0f)
        {
            _auraHealTimer += Mathf.Max(statData.auraHealInterval, 0.1f);
            ApplyAuraHeal();
        }
    }

    private void ApplyAuraHeal()
    {
        var allies = UnitRegistry.GetUnits(Side);
        float radiusSqr = statData.auraHealRadius * statData.auraHealRadius;
        float healMult = CombatModifierHub.GetHealMult(statData.job, statData.side);
        int amount = Mathf.RoundToInt(statData.auraHealAmount * healMult);

        for (int i = 0; i < allies.Count; i++)
        {
            UnitBase ally = allies[i];
            if (ally == null || ally.currentState == UnitState.Dead)
            {
                continue;
            }

            float distSqr = (ally.transform.position - transform.position).sqrMagnitude;
            if (distSqr <= radiusSqr)
            {
                ally.Heal(amount);
            }
        }
    }

    /// <summary>매 프레임 호출: summonInterval이 설정된 유닛(교황류)은 공격 여부와 무관하게 주기적으로 증원을 직접 소환한다.</summary>
    private void TickSummon()
    {
        if (statData == null || statData.summonPrefab == null || statData.summonInterval <= 0f ||
            !BossSummonTag.CombatEnabled)
        {
            return;
        }

        _summonTimer -= Time.deltaTime;
        if (_summonTimer <= 0f)
        {
            _summonTimer += Mathf.Max(statData.summonInterval, 0.1f);
            SpawnSummonWave();
        }
    }

    /// <summary>
    /// HeroPool/PooledStageBattle을 거치지 않고 직접 Instantiate — 승패 판정과 엮이지 않는 "임시 증원".
    /// BossSummonTag로 등록해서 라운드 경계 정리(BossSummonCombatParticipant)만 받는다.
    /// </summary>
    private void SpawnSummonWave()
    {
        int activeCount = BossSummonTag.ActiveCount;
        int capacity = Mathf.Max(0, statData.summonMaxActive - activeCount);
        int toSpawn = Mathf.Min(statData.summonCountPerWave, capacity);

        for (int i = 0; i < toSpawn; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * Mathf.Max(0.1f, statData.summonSpawnRadius);
            Vector3 spawnPosition = transform.position + new Vector3(offset.x, offset.y, 0f);
            GameObject instance = Instantiate(statData.summonPrefab, spawnPosition, Quaternion.identity);
            instance.AddComponent<BossSummonTag>();

            // HeroPool.Rent()를 거치지 않는 직접 스폰이라 ResetForSpawn이 안 불린다 — 마왕을 향해
            // 걷기 시작하도록 여기서 직접 지정해줘야 한다(안 그러면 가만히 서있기만 함).
            UnitBase summonedUnit = instance.GetComponent<UnitBase>();
            if (summonedUnit != null && UnitRegistry.KingWorldPosition.HasValue)
            {
                summonedUnit.SetMoveTarget(UnitRegistry.KingWorldPosition.Value);
            }
        }

        if (toSpawn > 0 && statData.summonBuffAttackMultiplier > 1f)
        {
            ApplySummonBuff();
        }
    }

    /// <summary>소환과 함께 주변 아군(용사)에게 짧은 공격력 버프("축복")를 건다.</summary>
    private void ApplySummonBuff()
    {
        var allies = UnitRegistry.GetUnits(Side);
        float radiusSqr = statData.summonBuffRadius * statData.summonBuffRadius;

        for (int i = 0; i < allies.Count; i++)
        {
            UnitBase ally = allies[i];
            if (ally == null || ally.currentState == UnitState.Dead)
            {
                continue;
            }

            float distSqr = (ally.transform.position - transform.position).sqrMagnitude;
            if (distSqr <= radiusSqr)
            {
                ((IHealable)ally).ApplyBuff("attack", statData.summonBuffAttackMultiplier, statData.summonBuffDuration);
            }
        }

        CombatEffects.PlaySaintCast(transform.position);
    }

    /// <summary>
    /// TakeDamage에서 체력이 임계값 이하로 떨어진 순간 1회만 호출 — 공격력 자강 버프 + 주변 적(마왕군) 전체 스턴.
    /// "왕이 된 용사"류 최종보스의 체력 50% 각성 연출.
    /// </summary>
    private void TriggerPhaseTransition()
    {
        ((IHealable)this).ApplyBuff("attack", statData.phaseTransitionAttackMultiplier, statData.phaseTransitionBuffDuration);

        UnitSide enemySide = Side == UnitSide.Hero ? UnitSide.DemonArmy : UnitSide.Hero;
        var enemies = UnitRegistry.GetUnits(enemySide);
        float radiusSqr = statData.phaseTransitionRadius * statData.phaseTransitionRadius;

        for (int i = 0; i < enemies.Count; i++)
        {
            UnitBase enemy = enemies[i];
            if (enemy == null || enemy.currentState == UnitState.Dead)
            {
                continue;
            }

            float distSqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distSqr <= radiusSqr)
            {
                ((IStatusReceiver)enemy).ApplyStun(statData.phaseTransitionStunDuration);
            }
        }

        CombatEffects.PlayMagicCast(transform.position);
    }

    // IPooledHeroState (OZGL2.Stage) — HeroPool이 용사를 재사용할 때 호출.
    void IPooledHeroState.ResetForSpawn(long leaseId)
    {
        currentTarget = null;
        hasMoveTarget = false;
        attackCooldownTimer = 0f;

        // 이전 대여 때 남아있던 CC/버프가 재사용된 용사에게 그대로 이어지면 안 되므로 전부 초기화.
        _stunExpire = 0f;
        _slowMult = 1f; _slowExpire = 0f;
        _vulnerableMult = 1f; _vulnerableExpire = 0f;
        _buffAttackMult = 1f; _buffAttackExpire = 0f;
        _buffAttackSpeedMult = 1f; _buffAttackSpeedExpire = 0f;
        _buffDefenseMult = 1f; _buffDefenseExpire = 0f;
        _burnDps = 0f; _burnExpire = 0f; _burnTickTimer = 0f;
        if (_burnTintActive)
        {
            SetBurnTint(false); // 이전 대여 때 화상으로 붉게 물든 채 반납됐을 수 있으니 즉시 원복
        }
        _auraHealTimer = 0f;
        _summonTimer = 0f;
        _hasTriggeredPhaseTransition = false;

        if (statData != null)
        {
            currentHealth = Mathf.RoundToInt(statData.maxHealth * CombatModifierHub.GetHpMult(statData.job, statData.side));
        }

        // PooledStageBattle(팀원 코드)은 스폰 위치만 정하고 이동 명령은 안 줘서, 여기서 직접 마왕을 향해
        // 걷기 시작하도록 지정한다 (4.1절). RealDefenders가 라운드 시작마다 이 값을 미리 채워둔다.
        if (UnitRegistry.KingWorldPosition.HasValue)
        {
            SetMoveTarget(UnitRegistry.KingWorldPosition.Value);
        }
        else
        {
            SetState(UnitState.Idle);
        }

        // 여기서는 아직 GameObject가 비활성 상태다(PooledHero.Rent가 SetActive(true) 전에 이 메서드를
        // 호출함). SPUM PlayAnimation은 Animator.SetBool로 상태를 반영하는데, 비활성 Animator엔 값을
        // 넣어도 실제로 안 먹혀서 여기서 강제 재생해도 소용없었음 — 실제 활성화 시점인 OnEnable에서 다시 재생한다.
    }

    void IPooledHeroState.ResetForReturn()
    {
        currentTarget = null;
        hasMoveTarget = false;
    }

    // IPooledHeroDeathPresentation (OZGL2.Stage) — 사망 연출을 여기서 직접 조율.
    // PooledHero.TryReportDeath()가 호출. 사망 애니메이션 길이만큼 기다렸다가 풀에 반납 완료를 알린다.
    // (풀링 컨텍스트 전용 — 풀링 없이 직접 스폰한 경우는 Die()에서 바로 Destroy로 처리)
    void IPooledHeroDeathPresentation.BeginDeath(HeroLease lease)
    {
        StartCoroutine(DeathPresentationRoutine(lease));
    }

    private IEnumerator DeathPresentationRoutine(HeroLease lease)
    {
        yield return new WaitForSeconds(GetDeathAnimationDuration());
        lease.Hero.TryCompleteDeath(lease.LeaseId);
    }

    // CC/버프 런타임 상태 — 시간 기반이라 값 자체는 만료 후에도 남아있지만 아래 Effective* 계산 시 항상 만료 여부를 같이 체크한다.
    private float _stunExpire;
    private float _slowMult = 1f, _slowExpire;
    private GameObject _slowEffectInstance; // 둔화 지속 이펙트 — 만료 시 Update()에서 정리
    private float _vulnerableMult = 1f, _vulnerableExpire;
    private float _buffAttackMult = 1f, _buffAttackExpire;
    private float _buffAttackSpeedMult = 1f, _buffAttackSpeedExpire;
    private float _buffDefenseMult = 1f, _buffDefenseExpire;

    // 화상(도트): dps/만료시각만 들고, 실제 틱(TickBurn)은 매 프레임 시간 누적으로 처리.
    private float _burnDps;
    private float _burnExpire;
    private float _burnTickTimer;
    private const float BurnTickInterval = 1f;

    // 회복 오라(팔라딘류): 공격 상태와 무관하게 항상 흐르는 별도 타이머.
    private float _auraHealTimer;

    // 소환(교황류): 공격 상태와 무관하게 항상 흐르는 별도 타이머.
    private float _summonTimer;

    // 페이즈 전환(최종보스류): 체력 임계값 발동은 전투당 1회만.
    private bool _hasTriggeredPhaseTransition;

    private float EffectiveSlowMult => Time.time < _slowExpire ? _slowMult : 1f;
    private float EffectiveVulnerableMult => Time.time < _vulnerableExpire ? _vulnerableMult : 1f;
    private float EffectiveBuffAttackMult => Time.time < _buffAttackExpire ? _buffAttackMult : 1f;
    private float EffectiveBuffAttackSpeedMult => Time.time < _buffAttackSpeedExpire ? _buffAttackSpeedMult : 1f;
    private float EffectiveBuffDefenseMult => Time.time < _buffDefenseExpire ? _buffDefenseMult : 1f;

    // SPUM 프리팹의 애니메이션 재생 담당 컴포넌트 (자식 오브젝트에 붙어있음, SPUM 샘플의 PlayerObj 참고)
    protected SPUM_Prefabs spumPrefabs;

    protected virtual void OnEnable()
    {
        UnitRegistry.Register(this);

        // 풀에서 재대여되면 ResetForSpawn()이 SetActive(true) 되기 전에 currentState/이동 목표를
        // 미리 정해두는데, 그때는 아직 비활성이라 SPUM PlayAnimation(Animator.SetBool)이 실제로
        // 안 먹힌다 — 실제로 활성화되는 지금(OnEnable) 다시 재생해서 예전 프레임 애니메이션에
        // 멈춰있던 것처럼 보이는(주로 Idle로 걸어오는) 현상을 막는다.
        PlaySpumAnimation(currentState);
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
            currentHealth = Mathf.RoundToInt(statData.maxHealth * CombatModifierHub.GetHpMult(statData.job, statData.side));
        }
        else
        {
            Debug.LogWarning($"[UnitBase] {name}에 statData가 비어있음. 인스펙터에서 UnitStatData를 할당해줘.", this);
        }

        InitSpumAnimation();
        EnsureClickCollider();
        // 화상 틴트 대상 스냅샷은 체력바를 붙이기 전에 떠야 한다 — 안 그러면 체력바(Background/Fill)
        // 스프라이트까지 자식으로 잡혀서 화상 중에 체력바까지 붉게 물든다.
        CacheBodyRenderers();
        UnitHealthBar.Attach(this);
    }

    /// <summary>화상 등 색상 틴트를 입힐 대상 스프라이트 목록. "Shadow"는 팩마다 톤이 달라 제외(UnitHealthBar와 동일 기준).</summary>
    private SpriteRenderer[] _bodyRenderers = System.Array.Empty<SpriteRenderer>();
    // 파츠마다 원래 색(흰색이 아닐 수 있음 — 염색된 천/가죽 등)이 달라서, 틴트 해제 시 무조건 흰색이
    // 아니라 각자 원래 색으로 되돌려야 한다. 그래서 캐싱 시점의 색을 같이 기억해둔다.
    private Color[] _bodyRendererOriginalColors = System.Array.Empty<Color>();

    private void CacheBodyRenderers()
    {
        var all = GetComponentsInChildren<SpriteRenderer>(true);
        var filtered = new System.Collections.Generic.List<SpriteRenderer>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                filtered.Add(all[i]);
            }
        }
        _bodyRenderers = filtered.ToArray();
        _bodyRendererOriginalColors = new Color[_bodyRenderers.Length];
        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            _bodyRendererOriginalColors[i] = _bodyRenderers[i].color;
        }

        // 데미지 숫자 높이 계산용 스프라이트 키(발 위치 기준) — 보스처럼 큰 유닛도 비율로 맞추기 위해 기억해둔다.
        if (_bodyRenderers.Length > 0)
        {
            Bounds bounds = _bodyRenderers[0].bounds;
            for (int i = 1; i < _bodyRenderers.Length; i++)
            {
                bounds.Encapsulate(_bodyRenderers[i].bounds);
            }
            _spriteHeight = Mathf.Max(0.5f, bounds.max.y - transform.position.y);
        }
    }

    private float _spriteHeight = 1f;

    /// <summary>
    /// 클릭 선택(사거리 표시용) 판정용 콜라이더가 없으면 자식 SpriteRenderer들의 바운즈에 맞춰
    /// 자동으로 하나 붙여준다. 프리팹마다 일일이 콜라이더를 넣어둘 필요가 없게 하기 위함.
    /// </summary>
    private void EnsureClickCollider()
    {
        if (GetComponent<Collider2D>() != null)
        {
            return;
        }

        var renderers = GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 scale = transform.lossyScale;
        Vector2 size = new Vector2(
            Mathf.Abs(scale.x) > 0.0001f ? bounds.size.x / Mathf.Abs(scale.x) : bounds.size.x,
            Mathf.Abs(scale.y) > 0.0001f ? bounds.size.y / Mathf.Abs(scale.y) : bounds.size.y);

        var collider = gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = size;
        collider.offset = transform.InverseTransformPoint(bounds.center);
    }

    private UnitRangeIndicator _rangeIndicator;

    /// <summary>클릭 선택 시 사거리 원을 켜고 끈다(UnitSelectionController가 호출).</summary>
    public void SetRangeIndicatorVisible(bool visible)
    {
        if (_rangeIndicator == null)
        {
            _rangeIndicator = UnitRangeIndicator.Attach(this);
        }
        _rangeIndicator.SetVisible(visible);
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

        // SPUM은 상태 하나에 클립을 여러 개(인덱스순) 등록할 수 있다 — 예를 들어 ATTACK에 근접 스윙(0),
        // 원거리/마법 시전(1+)이 같이 들어있는 유닛도 있음. 지금까진 항상 0번만 재생해서 사거리 유닛도
        // 근접 애니메이션으로 나갔을 수 있다. Attack만 UnitStatData.attackAnimationIndex로 골라 쓴다.
        int index = state == UnitState.Attack && statData != null
            ? Mathf.Clamp(statData.attackAnimationIndex, 0, clips.Count - 1)
            : 0;
        spumPrefabs.PlayAnimation(ToSpumState(state), index);
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
        // 둔화 지속 이펙트 정리: 만료됐는데 아직 안 지워졌으면 여기서 제거.
        if (_slowEffectInstance != null && Time.time >= _slowExpire)
        {
            CombatEffects.StopPersistent(_slowEffectInstance);
            _slowEffectInstance = null;
        }

        // 화상(도트)·회복 오라는 기절 중에도, 어떤 상태(Idle/Move/Attack)든 계속 틱한다 —
        // "행동"이 아니라 지속효과라서 스턴/상태 전이와 무관하게 흘러야 함.
        if (currentState != UnitState.Dead)
        {
            TickBurn();
            TickAuraHeal();
            TickSummon();
        }

        if (Time.time < _stunExpire)
        {
            return; // 스턴 중엔 상태 틱 자체를 건너뛴다 (행동 불가)
        }

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

        // 히어로는 매 프레임 "지금 존재하는 마왕군 중 가장 가까운 쪽"으로 이동 목표를 다시 잡는다.
        // (예전엔 스폰 시 마왕 쪽으로만 고정해서, 경로에서 벗어난 곳에 배치된 마왕군은 사거리에
        // 우연히 걸리지 않는 한 그냥 지나쳐버렸음 — 마왕군이 있으면 사거리와 무관하게 색적해서 찾아감.)
        if (Side == UnitSide.Hero)
        {
            UnitBase seekTarget = FindNearestEnemy();
            if (seekTarget != null)
            {
                moveTarget = seekTarget.transform.position;
                hasMoveTarget = true;
            }
            else if (UnitRegistry.KingWorldPosition.HasValue)
            {
                // 마왕군이 전부 사라졌을 때만 마왕으로 직진.
                moveTarget = UnitRegistry.KingWorldPosition.Value;
                hasMoveTarget = true;
            }
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
        transform.position += direction * statData.moveSpeed * EffectiveSlowMult * CombatModifierHub.GetMoveSpeedMult(Side) * Time.deltaTime; // 증강(냉기 침식), 기본 1
        FaceDirection(direction);
    }

    /// <summary>
    /// 좌우 이동 방향에 맞춰 스프라이트를 뒤집는다 (SPUM 프리팹은 localScale.x 반전 방식 사용).
    /// 기존엔 direction.x >= 0일 때 미러링 없음(양수 스케일)으로 처리했는데, 실제 SPUM 리그의
    /// 기본(미러링 전) 포즈가 왼쪽을 보고 있어서 좌우가 전부 반대로 보이는 문제가 있었다.
    /// 기준을 뒤집어 direction.x < 0(왼쪽)일 때 기본 포즈를 쓰고, 오른쪽으로 향할 때 미러링한다.
    /// </summary>
    protected virtual void FaceDirection(Vector3 direction)
    {
        if (Mathf.Abs(direction.x) < 0.01f)
        {
            return;
        }

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (direction.x < 0f ? 1f : -1f);
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
        // attackCooldownTimer는 건드리지 않는다 — 스폰 직후(0으로 초기화됨)엔 그대로 즉시 첫 공격이 나가고,
        // 이미 교전 중이던 유닛이 대상이 죽어서 재탐색하는 경우엔 남아있던 쿨다운을 그대로 이어간다.
        // 예전엔 여기서 매번 0으로 리셋해서, 대상이 자주 죽는(스플래시 등) 상황에서 사실상 공속이 무제한으로
        // 빨라지는 버그가 있었다 (마법사 타겟 전환마다 쿨다운 없이 즉발 공격).
        SetState(UnitState.Attack);
        return true;
    }

    /// <summary>힐량이 있는 유닛(힐러)은 아군을, 그 외에는 적을 찾는다.</summary>
    protected virtual UnitBase FindAttackTarget()
    {
        if (statData != null && statData.healAmount > 0f)
        {
            // 적이 전멸하면(전투 종료) 더 이상 치료하지 않고 대기 상태로 돌아간다.
            if (!HasLivingEnemies())
            {
                return null;
            }
            return FindLowestHealthAlly();
        }
        if (statData != null && statData.targetLowestHealthEnemy)
        {
            return FindLowestHealthEnemy();
        }
        return FindNearestEnemy();
    }

    /// <summary>반대 진영에 살아있는 유닛이 하나라도 있는지(전투 종료 판정용).</summary>
    protected bool HasLivingEnemies()
    {
        UnitSide enemySide = Side == UnitSide.Hero ? UnitSide.DemonArmy : UnitSide.Hero;
        var candidates = UnitRegistry.GetUnits(enemySide);
        for (int i = 0; i < candidates.Count; i++)
        {
            UnitBase unit = candidates[i];
            if (unit != null && unit.currentState != UnitState.Dead)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 궁수·도적 기믹: 최근접 대신 사거리 안에서 체력이 가장 낮은 적을 우선 타겟한다(마무리에 특화).
    /// FindLowestHealthAlly와 마찬가지로 사거리 밖 후보는 걸러서, 범위 밖 저체력 적 때문에 범위 안
    /// 다른 적을 못 때리는 일이 없게 한다.
    /// </summary>
    protected virtual UnitBase FindLowestHealthEnemy()
    {
        UnitSide enemySide = Side == UnitSide.Hero ? UnitSide.DemonArmy : UnitSide.Hero;
        var candidates = UnitRegistry.GetUnits(enemySide);

        UnitBase lowest = null;
        int lowestHealth = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            UnitBase unit = candidates[i];
            if (unit == null || unit.currentState == UnitState.Dead)
            {
                continue;
            }

            if (statData != null)
            {
                float distSqr = (unit.transform.position - transform.position).sqrMagnitude;
                if (distSqr > statData.attackRange * statData.attackRange)
                {
                    continue;
                }
            }

            if (unit.currentHealth < lowestHealth)
            {
                lowestHealth = unit.currentHealth;
                lowest = unit;
            }
        }

        return lowest;
    }

    /// <summary>
    /// 도발(어그로): 근접(사거리 1 이하) 마왕군은 자기 사거리 안에 든 히어로를 최우선으로 끌어온다.
    /// 지금까지는 순수 최근접이라 방패·전사·도적이 "막아주는" 역할을 못 했음 — 근접 밸류 보완(1번) 반영.
    /// </summary>
    public bool IsTaunting => Side == UnitSide.DemonArmy && statData != null && statData.attackRange <= 1f;
    public float TauntRangeSqr => statData != null ? statData.attackRange * statData.attackRange : 0f;

    protected virtual UnitBase FindNearestEnemy()
    {
        UnitSide enemySide = Side == UnitSide.Hero ? UnitSide.DemonArmy : UnitSide.Hero;
        var candidates = UnitRegistry.GetUnits(enemySide);

        UnitBase nearest = null;
        float nearestDistSqr = float.MaxValue;
        UnitBase tauntPick = null;
        float tauntDistSqr = float.MaxValue;

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

            // 히어로 쪽에서만 도발을 존중한다 (마왕군이 히어로에게 도발당할 일은 없음).
            if (Side == UnitSide.Hero && unit.IsTaunting && distSqr <= unit.TauntRangeSqr && distSqr < tauntDistSqr)
            {
                tauntDistSqr = distSqr;
                tauntPick = unit;
            }
        }

        return tauntPick != null ? tauntPick : nearest;
    }

    /// <summary>같은 진영에서 체력 비율이 가장 낮은(그리고 풀피가 아닌) 아군을 찾는다. 자기 자신도 후보에 포함된다.</summary>
    protected virtual UnitBase FindLowestHealthAlly()
    {
        var candidates = UnitRegistry.GetUnits(Side);

        UnitBase lowest = null;
        float lowestRatio = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            UnitBase unit = candidates[i];
            if (unit == null || unit.currentState == UnitState.Dead || unit.statData == null)
            {
                continue;
            }

            if (unit.currentHealth >= unit.statData.maxHealth)
            {
                continue; // 이미 풀피면 치료 대상 아님
            }

            // 사거리 밖의 다친 아군 때문에 사거리 안의 풀피 판정을 못 하고 헛돌지 않도록, 힐 대상도
            // 사거리로 거른다 — 범위 안이 전부 풀피면 여기서 아무도 안 걸려서 null 반환(대기 상태로 복귀).
            if (statData != null)
            {
                float distSqr = (unit.transform.position - transform.position).sqrMagnitude;
                if (distSqr > statData.attackRange * statData.attackRange)
                {
                    continue;
                }
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

    private readonly List<UnitBase> _extraHealTargets = new List<UnitBase>(3);

    /// <summary>FindLowestHealthAlly와 같은 기준(사거리 안·풀피 제외·체력 비율 최저)에서 이미 고른 대상은 빼고 찾는다.</summary>
    private UnitBase FindLowestHealthAllyExcluding(List<UnitBase> excluded)
    {
        var candidates = UnitRegistry.GetUnits(Side);
        UnitBase lowest = null;
        float lowestRatio = float.MaxValue;
        float rangeSqr = statData.attackRange * statData.attackRange;

        for (int i = 0; i < candidates.Count; i++)
        {
            UnitBase unit = candidates[i];
            if (unit == null || unit.currentState == UnitState.Dead || unit.statData == null ||
                unit.currentHealth >= unit.statData.maxHealth || excluded.Contains(unit))
            {
                continue;
            }

            if ((unit.transform.position - transform.position).sqrMagnitude > rangeSqr)
            {
                continue;
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

        // 힐러가 힐 걸던 대상이 그 사이(자기 힐 포함) 풀피가 됐거나, 힐 도중 적이 전멸해 전투가
        // 끝났으면 계속 붙잡고 있지 말고 대기 상태로 돌아간다(낭비 힐 방지).
        if (statData != null && statData.healAmount > 0f &&
            ((currentTarget.statData != null && currentTarget.currentHealth >= currentTarget.statData.maxHealth)
             || !HasLivingEnemies()))
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
            // 같은 Attack 상태를 유지한 채 반복되는 스윙 — 상태 전이가 없어서 SetState는 다시 안 불리므로
            // 스윙마다(쿨다운 완료 시점마다) 직접 트리거를 다시 넣어준다.
            PlaySpumAnimation(UnitState.Attack);
            // 모션 시작과 동시에 데미지/이펙트가 나가면 어색해서, 실제 적용은 스윙 애니메이션 길이만큼 늦춘다.
            StartCoroutine(DelayedAttack(currentTarget, GetAttackAnimationDuration()));
            attackCooldownTimer = GetAttackInterval();
        }
    }

    /// <summary>스윙 애니메이션이 끝날 때쯤 실제 피해/힐/이펙트를 적용한다.</summary>
    private IEnumerator DelayedAttack(UnitBase target, float delaySeconds)
    {
        if (delaySeconds > 0f)
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        // 대기하는 동안 이 유닛이나 타겟이 죽었으면 적용하지 않는다.
        if (currentState == UnitState.Dead || target == null || target.currentState == UnitState.Dead)
        {
            yield break;
        }

        PerformAttack(target);
    }

    /// <summary>
    /// SPUM ATTACK 클립(attackAnimationIndex로 고른 것) 길이. 클립을 못 찾으면 0(기존처럼 즉시 적용).
    /// </summary>
    protected virtual float GetAttackAnimationDuration()
    {
        if (spumPrefabs != null &&
            spumPrefabs.StateAnimationPairs.TryGetValue(PlayerState.ATTACK.ToString(), out var clips) &&
            clips.Count > 0)
        {
            int index = statData != null ? Mathf.Clamp(statData.attackAnimationIndex, 0, clips.Count - 1) : 0;
            if (clips[index] != null)
            {
                return clips[index].length;
            }
        }

        return 0f;
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
        float speedMult = statData != null ? CombatModifierHub.GetAttackSpeedMult(statData.job, statData.side) : 1f;
        return 1f / Mathf.Max(speed * speedMult * EffectiveSlowMult * EffectiveBuffAttackSpeedMult, 0.01f);
    }

    protected virtual void PerformAttack(UnitBase target)
    {
        if (statData == null || target == null)
        {
            return;
        }

        if (statData.healAmount > 0f)
        {
            float healMult = CombatModifierHub.GetHealMult(statData.job, statData.side);
            int healAmount = Mathf.RoundToInt(statData.healAmount * healMult);
            target.Heal(healAmount);

            // 힐러 성급 기믹: 2성은 2명, 3성은 3명을 동시에 치료 — 주 대상 외에 사거리 안에서 체력 비율이 낮은 순으로 추가.
            int extraTargets = Mathf.Clamp(statData.starLevel, 1, 3) - 1;
            if (extraTargets > 0)
            {
                _extraHealTargets.Clear();
                _extraHealTargets.Add(target);
                for (int i = 0; i < extraTargets; i++)
                {
                    UnitBase extra = FindLowestHealthAllyExcluding(_extraHealTargets);
                    if (extra == null)
                    {
                        break;
                    }

                    extra.Heal(healAmount);
                    _extraHealTargets.Add(extra);
                }
            }
            return;
        }

        CombatModifierHub.NotifyAttack(Side); // 증강(연타 본능) — "공격할 때마다" 효과용 알림
        float targetDefense = (target.statData != null ? target.statData.defensePercent : 0f) * target.EffectiveBuffDefenseMult
                              + CombatModifierHub.GetDefenseAdd(target.Side); // 증강(냉기 침식) 방어율 가감, 기본 0
        float attackMult = CombatModifierHub.GetAttackMult(statData.job, statData.side);
        float rawAttackPower = statData.attackPower * attackMult * EffectiveBuffAttackMult;

        // 궁수 기믹(성급 2 이상): 대상이 잃은 체력 비율만큼 추가 피해 — 마무리 일격이 더 세진다.
        if (statData.starLevel >= 2 && statData.executeDamageBonusPerMissingHealth > 0f &&
            target.statData != null && target.statData.maxHealth > 0)
        {
            float missingRatio = 1f - (float)target.currentHealth / target.statData.maxHealth;
            rawAttackPower *= 1f + statData.executeDamageBonusPerMissingHealth * Mathf.Clamp01(missingRatio);
        }

        int damage = CalculateDamage(rawAttackPower, targetDefense);

        // 도적 기믹(성급 2 이상): N번째 공격마다 대상 기절.
        _attackCount++;
        if (statData.starLevel >= 2 && statData.comboStunAttackInterval > 0 &&
            _attackCount % statData.comboStunAttackInterval == 0)
        {
            ((IStatusReceiver)target).ApplyStun(statData.comboStunDuration);
        }

        // 화상(화염기사류): 기본공격이 명중하는 대상에게 매번 화상을 새로 걸어(갱신) 도트 피해를 추가.
        if (statData.burnDamagePerSecond > 0f)
        {
            target.ApplyBurn(statData.burnDamagePerSecond, statData.burnDuration);
        }

        if (statData.projectilePrefab != null)
        {
            LaunchProjectile(target, damage);
        }
        else
        {
            Sfx.Play(SfxId.AttackMelee);
            target.TakeDamage(damage);
            ApplySplashDamage(target, damage);
        }
    }

    /// <summary>
    /// 스플래시(근접 전사): 주 타겟 위치 기준 반경 안의 다른 적에게도 피해. 간접 피해 비율과 최대
    /// 인원 수는 UnitStatData(splashSecondaryDamagePercent/splashMaxTargets)로 유닛별 조절.
    /// 발사체 공격의 스플래시는 Projectile.ApplySplashDamage가 별도로 처리한다.
    /// </summary>
    protected virtual void ApplySplashDamage(UnitBase primaryTarget, int damage)
    {
        if (statData.splashRadius <= 0f)
        {
            return;
        }

        UnitSide enemySide = Side == UnitSide.Hero ? UnitSide.DemonArmy : UnitSide.Hero;
        var candidates = UnitRegistry.GetUnits(enemySide);
        float radiusSqr = statData.splashRadius * statData.splashRadius;

        var inRange = new System.Collections.Generic.List<(UnitBase unit, float distSqr)>();
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

        // 최대 인원 수 제한이 있으면 가까운 대상부터 우선 적용.
        inRange.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));
        int hitCount = statData.splashMaxTargets > 0 ? Mathf.Min(statData.splashMaxTargets, inRange.Count) : inRange.Count;
        int secondaryDamage = Mathf.Max(0, Mathf.RoundToInt(damage * statData.splashSecondaryDamagePercent));

        for (int i = 0; i < hitCount; i++)
        {
            inRange[i].unit.TakeDamage(secondaryDamage);
        }
    }

    /// <summary>
    /// 관통 없는 단일 대상 유도 발사체 생성. 데미지는 발사 시점 방어율로 미리 계산해서 들려 보내고,
    /// 실제 적용은 Projectile이 명중했을 때(target.TakeDamage) 처리한다.
    /// </summary>
    protected virtual void LaunchProjectile(UnitBase target, int damage)
    {
        // 준비/보상/종료 등 전투가 막힌 구간에는 새 발사체를 만들지 않는다
        // (CoreLoop-Connections-TeamGuide.md IInGameCombatParticipant 계약, ProjectileCombatParticipant가 제어).
        if (!Projectile.CombatEnabled)
        {
            return;
        }

        // 발사체는 궁수도 쓰기 때문에(Arrow), 마법 시전 이펙트는 마법사 직업일 때만 재생한다.
        if (statData.job == SynergyJob.Mage)
        {
            CombatEffects.PlayMagicCast(transform.position);
            Sfx.Play(SfxId.CastMagic);
        }
        else
        {
            Sfx.Play(SfxId.ShootArrow);
        }

        Vector3 spawnPosition = muzzlePoint != null ? muzzlePoint.position : transform.position;
        GameObject projectileObj = Instantiate(statData.projectilePrefab, spawnPosition, Quaternion.identity);
        Projectile projectile = projectileObj.AddComponent<Projectile>();
        projectile.Init(target, damage, statData.projectileSpeed, statData.projectileDefaultFacing,
            statData.splashRadius, statData.splashSecondaryDamagePercent, statData.splashMaxTargets,
            statData.job == SynergyJob.Mage ? HitSoundKind.Magic : HitSoundKind.Physical);
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

        int before = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, statData.maxHealth);
        CombatEffects.PlayHeal(transform.position);
        CombatEffects.PlayHealNumber(transform.position, _spriteHeight, currentHealth - before);
        Sfx.Play(SfxId.Heal);
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

    /// <summary>기본(근접·화살) 피해 — 피격음은 Hit.</summary>
    public void TakeDamage(int amount) => TakeDamage(amount, HitSoundKind.Physical);

    public virtual void TakeDamage(int amount, HitSoundKind soundKind)
    {
        if (currentState == UnitState.Dead)
        {
            return;
        }

        // 증강(철벽 진형·수호의 방패) 보정 — 값이 없으면 amount 그대로 반환하므로 기존 동작은 변하지 않는다.
        amount = CombatModifierHub.FilterIncomingDamage(GetInstanceID(), Side, amount);
        int appliedDamage = Mathf.RoundToInt(amount * EffectiveVulnerableMult);
        currentHealth -= appliedDamage;
        CombatEffects.PlayHit(transform.position);
        CombatEffects.PlayDamageNumber(transform.position, _spriteHeight, appliedDamage, Side);
        if (appliedDamage > 0 && soundKind != HitSoundKind.None)
        {
            Sfx.Play(soundKind == HitSoundKind.Magic ? SfxId.HitMagic : SfxId.Hit);
        }
        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        // 페이즈 전환(최종보스류): 체력이 임계값 아래로 떨어진 "그 순간" 1회만 발동.
        if (!_hasTriggeredPhaseTransition && statData != null && statData.phaseTransitionHealthRatio > 0f &&
            currentHealth <= statData.maxHealth * statData.phaseTransitionHealthRatio)
        {
            _hasTriggeredPhaseTransition = true;
            TriggerPhaseTransition();
        }
    }

    protected virtual void Die()
    {
        currentHealth = 0;
        hasMoveTarget = false;
        currentTarget = null;
        SetState(UnitState.Dead);
        Sfx.Play(Side == UnitSide.Hero ? SfxId.DeathHero : SfxId.DeathAlly);

        if (Side == UnitSide.Hero)
        {
            int expReward = statData != null ? statData.killExpReward : 0;
            OnHeroKilled?.Invoke(this, expReward);

            PooledHero pooledHero = GetComponent<PooledHero>();
            if (pooledHero != null && pooledHero.IsLeased)
            {
                // 풀링 컨텍스트: HeroPool/PooledStageBattle에 사망을 통지한다.
                // 이후 연출은 BeginDeath()가, 최종 반납·승패 판정(RoundCompletionEvaluator)은 그쪽 시스템이
                // RealDefenders.AliveCount와 함께 처리 — 여기서 Destroy 하지 않는다.
                pooledHero.TryReportDeath(pooledHero.LeaseId);
            }
            else
            {
                // 풀링 없이 직접 스폰한 경우(JOB_SEJIN 테스트 등) — 기존처럼 애니메이션 재생 후 제거
                Destroy(gameObject, GetDeathAnimationDuration());
            }
        }
        // 마왕군은 Dead 상태로 남겨둔다 — RealDefenders.PrepareRoundAsync()가 다음 라운드 시작 시 Revive()를 호출해 부활시킨다 (4.3절)
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

        currentHealth = Mathf.RoundToInt(statData.maxHealth * CombatModifierHub.GetHpMult(statData.job, statData.side));
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
        statData.attackRange = baseStatData.attackRange + baseStatData.rangeBonusPerStar * (newStar - 1);

        currentHealth = Mathf.RoundToInt(statData.maxHealth * CombatModifierHub.GetHpMult(statData.job, statData.side)); // 합성 시 풀피로 시작
    }

    /// <summary>
    /// 밸런스 시트(05.보통_라운드)의 라운드별 HP/공격력 배율(1.068^(R-1), 1.045^(R-1))을 적용한다.
    /// PooledStageBattle이 용사(적) 스폰 직후 호출 — 마왕군(그리드 합성) 쪽은 ApplyStarLevel로 별도 관리되므로 대상이 아니다.
    /// </summary>
    public virtual void ApplyRoundDifficultyMultiplier(float hpMultiplier, float attackMultiplier)
    {
        if (statData == null)
        {
            return;
        }

        if (baseStatData == null)
        {
            baseStatData = statData; // 원본(1라운드 기준값) 기억, 최초 1회만
        }

        if (statData == baseStatData)
        {
            statData = Instantiate(baseStatData); // 이 유닛 전용 복제본으로 교체 — 원본 에셋은 보호됨
        }

        statData.maxHealth = Mathf.RoundToInt(baseStatData.maxHealth * hpMultiplier);
        statData.attackPower = baseStatData.attackPower * attackMultiplier;

        currentHealth = Mathf.RoundToInt(statData.maxHealth * CombatModifierHub.GetHpMult(statData.job, statData.side)); // 스폰 시 풀피로 시작
    }
}
