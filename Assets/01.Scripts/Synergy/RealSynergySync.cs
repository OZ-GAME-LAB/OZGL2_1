using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;
using OZGL2.Progression;
using OZGL2.Augment;
using OZGL2.Skill;

namespace OZGL2.Synergy
{
    /// <summary>
    /// 실제 게임 씬용 특성·증강·시너지 → 전투 배율 동기화 + 마왕 스킬 실장착.
    /// 몬스터/용사 스탯 배율은 CombatModifierHub에 써서 UnitBase가 읽어가게 하고,
    /// 마왕 스킬은 SkillManager/SkillExecutor를 직접 들고 있다가 실제 용사(RealTargetProvider)를 겨냥한다.
    /// 그리드 매니저(김건·준기)가 배치 변경 시 SetCount만 호출해주면 시너지 쪽은 자동 갱신.
    /// 샌드박스(SkillSandbox)와는 별개 — 샌드박스는 테스트용, 이건 실제 게임용.
    /// </summary>
    public class RealSynergySync : MonoBehaviour
    {
        [Header("스킬 (비우면 Resources/Skills 자동 로드)")]
        [SerializeField] private List<SkillData> _skills = new List<SkillData>();

        [Header("마왕 위치 (스킬 시전 기준점 — 비우면 이 오브젝트 위치)")]
        [SerializeField] private Transform _casterTransform;

        [Header("디버그 (테스트용 — 실제 배포 전에는 꺼야 함)")]
        [Tooltip("체크하면 계정 해금/장착 상태 무시하고 모든 스킬을 해금·장착 시도(용량만큼). 계정 저장(PlayerPrefs)은 안 건드림.")]
        [SerializeField] private bool _debugUnlockAllSkills = false;
        [Tooltip("체크하면 화면 오른쪽에 증강 30종 선택 패널이 뜬다 — 카드 후보 필터와 상관없이 골라서 효과를 확인하는 QA용.")]
        [SerializeField] private bool _debugAugments = false;

        private static readonly SynergyJob[] AllJobs = (SynergyJob[])System.Enum.GetValues(typeof(SynergyJob));

        private SynergyTracker _synergy;
        private TraitTree _traits;
        private AugmentRun _augments;

        private SkillModifiers _skillMods;
        private SkillManager _skillManager;
        private SkillExecutor _executor;
        private RealTargetProvider _targets;
        private RealAllyProvider _allies;
        private SkillBarUI _skillBar;
        private bool _usesExternalSkillUi;
        private bool _isUiInputBlocked;

        // ── 증강 실전투 상태 (전부 라운드/런 단위로 초기화)
        private AugmentModifiers _aug = AugmentModifiers.Neutral;
        private float _huntUntil;
        private float _ironUntil;    // 철벽 진형 만료 시각
        private float _chainBonus;   // 연타 본능 누적 공격속도 보너스
        private bool _wasCombat;
        private readonly HashSet<UnitBase> _lowHpHealed = new HashSet<UnitBase>();
        private readonly Dictionary<UnitBase, bool> _monsterDead = new Dictionary<UnitBase, bool>();

        public SynergyTracker Synergy => _synergy;
        public TraitTree Traits => _traits;
        public AugmentRun Augments => _augments;
        public SkillManager SkillManager => _skillManager;

        /// <summary>
        /// 마왕 위치. 인스펙터에 지정된 Transform이 있으면 그걸 쓰고, 없으면 RealDefenders가
        /// 라운드 시작마다 채워두는 UnitRegistry.KingWorldPosition(실제 그리드 King 앵커)을 쓴다.
        /// </summary>
        private Vector3 CasterPosition
        {
            get
            {
                if (_casterTransform != null) return _casterTransform.position;
                if (UnitRegistry.KingWorldPosition.HasValue) return UnitRegistry.KingWorldPosition.Value;
                return transform.position;
            }
        }

        /// <summary>1+percent 배율 두 개/세 개를 합친다 (SkillSandbox.RefreshMods와 동일한 공식).</summary>
        private static float Combine(float a, float b) => a + b - 1f;
        private static float Combine(float a, float b, float c) => a + b + c - 2f;

        private void Awake()
        {
            _synergy = new SynergyTracker(Resources.LoadAll<SynergyData>("Synergies"), Resources.LoadAll<ComboSynergyData>("ComboSynergies"));
            _traits = new TraitTree(Resources.LoadAll<TraitData>("Traits"));
            _augments = new AugmentRun(Resources.LoadAll<AugmentData>("Augments"));

            _synergy.Changed += Sync;
            _traits.Changed += Sync;
            _augments.Changed += Sync;
            _augments.Picked += OnAugmentPicked;
            UnitBase.OnHeroKilled += OnHeroKilled;
            CombatModifierHub.AttackPerformed += OnAttackPerformed;
            if (MawangXpBridge.Mawang != null) MawangXpBridge.Mawang.LeveledUp += OnMawangLeveledUp; // 레벨이 오르면 스킬 피해 배율을 다시 계산

            SetupSkills();
            Sync();
        }

        private void OnDestroy()
        {
            StopCombat();
            UnitBase.OnHeroKilled -= OnHeroKilled;
            CombatModifierHub.AttackPerformed -= OnAttackPerformed;
            if (MawangXpBridge.Mawang != null) MawangXpBridge.Mawang.LeveledUp -= OnMawangLeveledUp;
            if (_synergy != null) _synergy.Changed -= Sync;
            if (_traits != null) _traits.Changed -= Sync;
            if (_augments != null)
            {
                _augments.Changed -= Sync;
                _augments.Picked -= OnAugmentPicked;
            }
        }

        private void OnMawangLeveledUp(int level) => Sync();

        public void SetCount(SynergyJob job, int count) => _synergy.SetCount(job, count);

        // 라운드별 용사 스탯 배율(밸런스 시트 05·06·07의 HP/공격/공속/힐량 배율) — 밸런스 테스트 루프 전용.
        // 기본값 1이라 이 API를 아무도 안 부르는 일반 플레이에는 영향이 없다.
        private int _traitKillXp;
        private float _roundHeroHp = 1f, _roundHeroAtk = 1f, _roundHeroSpd = 1f, _roundHeroHeal = 1f;

        /// <summary>현재 라운드의 용사 HP·공격·공속·힐량 배율을 시트 값으로 지정한다. HP는 용사가 스폰될 때
        /// (UnitBase 초기화) 읽히므로 준비 단계에서 미리 호출해야 하고, 나머지는 공격 시점에 읽힌다.</summary>
        public void SetHeroRoundScaling(float hp, float atk, float attackSpeed, float heal)
        {
            _roundHeroHp = Mathf.Max(0.01f, hp);
            _roundHeroAtk = Mathf.Max(0.01f, atk);
            _roundHeroSpd = Mathf.Max(0.01f, attackSpeed);
            _roundHeroHeal = Mathf.Max(0.01f, heal);
            Sync();
        }

        /// <summary>true면 새 런을 시작해도 마왕 레벨·XP를 Lv1로 되돌리지 않는다 — 밸런스 테스트 루프 전용
        /// (레벨이 계정에 그대로 저장·이어짐). 기본 false라 일반 플레이(런마다 Lv1)는 그대로다.</summary>
        public static bool KeepMawangLevelOnRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => KeepMawangLevelOnRun = false;

        /// <summary>새 게임에서만 계정 장착 정보를 다시 읽는다. 라운드 전환에는 호출하지 않는다.</summary>
        public void BeginRun()
        {
            _roundHeroHp = _roundHeroAtk = _roundHeroSpd = _roundHeroHeal = 1f;
            StopCombat();
            if (_executor != null)
            {
                _executor.gameObject.SetActive(false);
                Destroy(_executor.gameObject);
            }
            if (_skillBar != null) Destroy(_skillBar.gameObject);
            _augments.ResetRun();
            if (!KeepMawangLevelOnRun) MawangXpBridge.Mawang?.ResetForNewRun(); // 마왕 레벨·XP는 런마다 Lv1, LP는 이월
            ResetRoundAugmentState();
            SetupSkills();
            SetCombatEnabled(false);
            Sync();
        }

        public void PrepareCombat(Vector3 casterPosition)
        {
            StopCombat();
            _skillManager?.ResetCooldowns();
            _executor.SetCasterPosition(casterPosition);
            _skillBar.RefreshContext(Camera.main, casterPosition);
        }

        public void SetCombatEnabled(bool isEnabled)
        {
            if (_skillManager != null) _skillManager.IsCastingEnabled = isEnabled;
            if (_skillBar == null) return;

            if (_usesExternalSkillUi)
            {
                // 외부 스킨을 써도 팀 스킬바의 조준 상태 머신은 계속 실행되어야 한다.
                if (!_skillBar.gameObject.activeSelf) _skillBar.gameObject.SetActive(true);
                if (!isEnabled) _skillBar.CancelExternalCast();
                _skillBar.RefreshInternalBarVisibility();
                return;
            }

            // 외부 스킨이 없을 때는 팀원이 만든 기존 활성화 규칙을 그대로 유지한다.
            _skillBar.gameObject.SetActive(isEnabled);
        }

        /// <summary>
        /// Scene HUD가 실제 스킬 슬롯을 표시할 때 기존 런타임 생성 스킬바만 숨긴다.
        /// SkillManager와 SkillExecutor는 그대로 유지된다.
        /// </summary>
        public void SetExternalSkillUiActive(bool isActive)
        {
            _usesExternalSkillUi = isActive;
            if (_skillBar == null) return;

            if (!_skillBar.gameObject.activeSelf) _skillBar.gameObject.SetActive(true);
            _skillBar.SetExternalSkinActive(isActive);
            if (!isActive && (_skillManager == null || !_skillManager.IsCastingEnabled))
                _skillBar.gameObject.SetActive(false);
        }

        public void SetUiInputBlocked(bool isBlocked)
        {
            _isUiInputBlocked = isBlocked;
            if (isBlocked) _skillBar?.RefreshContext(Camera.main, CasterPosition);
        }

        public void StopCombat()
        {
            SetCombatEnabled(false);
            if (_executor != null) _executor.CancelActiveEffects();
        }

        /// <summary>처음부터 해금돼 있고 자동 장착되는 기본 스킬(화염구) 여부 — 로비 UI도 같은 기준으로 표시한다.</summary>
        public static bool IsStarterSkill(SkillData data) => data != null && data.displayName == "화염구";

        /// <summary>스킬 매니저·실행기를 만들고, 계정 영구 해금/장착 상태 그대로 실제 씬에 올린다.</summary>
        private void SetupSkills()
        {
            _skillMods = new SkillModifiers();
            _targets = new RealTargetProvider();
            _allies = new RealAllyProvider();
            _skillManager = new SkillManager(_targets, _skillMods);

            var defs = _skills.Count > 0 ? _skills : new List<SkillData>(Resources.LoadAll<SkillData>("Skills"));

            // 장착 용량은 디버그 모드에서도 실제와 동일(기본 3 + 특성) — 디버그는 "해금 상태"만 전부 풀어서
            // 22종 중 뭘 그 칸에 넣을지 자유롭게 고를 수 있게 해줄 뿐, 슬롯 수 자체는 안 바꾼다.
            _skillManager.EquipCapacity = 3 + _traits.BuildModifiers().ExtraSkillSlots;

            var equipped = SkillTreeStore.GetEquipped();
            foreach (var data in defs)
            {
                if (data == null) continue;
                // 화염구는 항상 기본 해금이고, 스킬 세팅에서 저장한 적이 없는 신규 계정에게만 자동 장착한다.
                // 한 번이라도 장착을 저장했다면 그 목록을 그대로 따른다(화염구를 뺐으면 인게임에도 없다).
                bool starterFree = IsStarterSkill(data);
                bool starterAutoEquip = starterFree && !SkillTreeStore.HasSavedEquipment;
                bool unlocked = starterFree || _debugUnlockAllSkills || SkillTreeStore.IsUnlocked(data.skillId);
                var runtime = _skillManager.Register(data, unlocked);
                // 디버그 모드: 용량 찰 때까지 등록 순서대로 우선 채워 넣고, 나머지는 디버그 패널에서 직접 스왑.
                bool shouldEquip = unlocked && (starterAutoEquip || (_debugUnlockAllSkills
                    ? _skillManager.EquippedCount < _skillManager.EquipCapacity
                    : equipped.Contains(data.skillId)));
                if (shouldEquip) _skillManager.TryEquip(runtime);
            }

            var execObj = new GameObject("RealSkillExecutor");
            execObj.transform.SetParent(transform);
            _executor = execObj.AddComponent<SkillExecutor>();
            _executor.Bind(_skillManager, _targets, _allies, CasterPosition, _skillMods);

            var barObj = new GameObject("RealSkillBar");
            barObj.transform.SetParent(transform);
            _skillBar = barObj.AddComponent<SkillBarUI>();
            _skillBar.Bind(_skillManager, Camera.main, CasterPosition);
        }

        /// <summary>
        /// 임시 시전 입력 — 실제 스킬바 UI(희수 파트)가 붙기 전까지, 장착된 스킬을 순서대로 숫자키 1~5에 배정.
        /// Targeted 스킬은 가장 가까운 용사를 자동 조준한다(없으면 마왕 위치).
        /// </summary>
        private void Update()
        {
            // F9 — 증강 디버그 패널 토글 (에디터·개발 빌드 전용, 배포 빌드에서는 무시)
            if (Debug.isDebugBuild && Input.GetKeyDown(KeyCode.F9)) _debugAugments = !_debugAugments;

            TickAugments();

            if (_skillManager == null || !_skillManager.IsCastingEnabled || _isUiInputBlocked) return;

            // Bind() 시점엔 라운드가 아직 시작 안 돼서 UnitRegistry.KingWorldPosition이 비어있을 수
            // 있어(그러면 스킬 시전 위치가 (0,0,0) 같은 엉뚱한 곳에 고정됨) — 왕 위치가 실제로 잡힐
            // 때까지, 그리고 라운드마다 바뀔 수 있으니 계속 최신 위치로 앵커를 맞춰준다.
            _executor?.SetCasterPosition(CasterPosition);
            _skillBar?.SetCasterPosition(CasterPosition);

            int i = 0;
            foreach (var skill in _skillManager.EquippedSkills)
            {
                if (i >= 5) break;
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    TryCastSkill(skill);
                }
                i++;
            }
        }

        private Vector2 _debugSkillScroll;

        /// <summary>디버그 모드 전용 — 등록된 스킬 목록에서 자유롭게 장착/해제(스왑)하는 패널.</summary>
        private Vector2 _debugAugScroll;

        /// <summary>디버그 전용 — 증강 30종을 후보 필터와 무관하게 직접 골라서 실전투 효과를 확인하는 패널.</summary>
        private void DrawAugmentDebug()
        {
            if (_augments == null) return;

            const float w = 300f;
            GUILayout.BeginArea(new Rect(Screen.width - w - 10f, 10f, w, Mathf.Min(Screen.height - 20f, 620f)), GUI.skin.box);
            GUILayout.Label($"<b>디버그 — 증강 ({_augments.PickedCount}/{_augments.TotalCount})</b>");
            if (GUILayout.Button("증강 전부 초기화")) _augments.ResetRun();
            _debugAugScroll = GUILayout.BeginScrollView(_debugAugScroll);
            for (int tier = 1; tier <= 3; tier++)
            {
                GUILayout.Label($"── {AugmentData.TierName(tier)} ──");
                foreach (var d in _augments.Pool)
                {
                    if (d == null || d.tier != tier) continue;
                    bool picked = _augments.IsMaxed(d);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(picked ? $"✔ {d.displayName}" : d.displayName, GUILayout.Width(190f));
                    GUI.enabled = !picked;
                    if (GUILayout.Button("선택", GUILayout.Width(60f))) _augments.Pick(d);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void OnGUI()
        {
            if (_debugAugments) DrawAugmentDebug();

            if (!_debugUnlockAllSkills || _skillManager == null) return;

            GUILayout.BeginArea(new Rect(10f, 10f, 260f, Mathf.Min(Screen.height - 20f, 500f)), GUI.skin.box);
            GUILayout.Label($"<b>디버그 — 스킬 장착 ({_skillManager.EquippedCount}/{_skillManager.EquipCapacity})</b>");
            _debugSkillScroll = GUILayout.BeginScrollView(_debugSkillScroll);
            foreach (var skill in _skillManager.Skills)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(skill.Data.displayName, GUILayout.Width(150f));
                if (skill.IsEquipped)
                {
                    if (GUILayout.Button("해제", GUILayout.Width(50f)))
                    {
                        _skillManager.Unequip(skill);
                        _skillBar.Rebuild();
                    }
                }
                else if (GUILayout.Button("장착", GUILayout.Width(50f)))
                {
                    if (_skillManager.TryEquip(skill)) _skillBar.Rebuild();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public bool TryCastSkill(SkillRuntime skill)
        {
            if (_skillManager == null || !_skillManager.IsCastingEnabled || _isUiInputBlocked || skill == null) return false;
            if (skill.Data.castMode == SkillCastMode.Instant)
            {
                return _skillManager.TryCastInstant(skill);
            }

            var nearest = _targets.Nearest(CasterPosition);
            Vector3 point = nearest != null ? nearest.Position : CasterPosition;
            return _skillManager.TryCastTargeted(skill, point);
        }

        /// <summary>외부 HUD 스킨의 슬롯 입력을 기존 SkillBarUI 조준 시스템으로 전달한다.</summary>
        public bool TryBeginSkillInput(SkillRuntime skill, IReadOnlyList<RectTransform> cancelAreas)
        {
            return !_isUiInputBlocked && _skillBar != null &&
                _skillBar.TryBeginExternalCast(skill, cancelAreas);
        }

        public void CancelSkillInput()
        {
            _skillBar?.CancelExternalCast();
        }

        // ─────────────────────────────────────────── 증강 (즉시 효과 · 처치 훅 · 전투 중 감시)

        /// <summary>즉시 발동형 증강 — 고르는 순간 1회 실행 (샌드박스 SkillSandbox.OnAugmentPicked 와 같은 동작).</summary>
        private void OnAugmentPicked(AugmentData d)
        {
            switch (d.effect)
            {
                case AugmentEffect.InstantSp:
                    SkillTreeStore.SkillPoints += Mathf.RoundToInt(d.value);
                    break;
                case AugmentEffect.InstantXp:
                    MawangXpBridge.Mawang?.AddXp(Mathf.RoundToInt(d.value));
                    break;
                case AugmentEffect.InstantResetCooldowns:
                    _skillManager?.ResetCooldowns();
                    break;
                case AugmentEffect.InstantHealMonsters:
                    var allies = UnitRegistry.GetUnits(UnitSide.DemonArmy);
                    for (int i = 0; i < allies.Count; i++)
                        if (allies[i] != null && allies[i].currentState != UnitState.Dead)
                            ((IHealable)allies[i]).HealFraction(d.value);
                    break;
            }
            Debug.Log($"[증강] {d.displayName} 발동");
        }

        /// <summary>용사 처치 훅 — 처형 재충전·연쇄 폭발·백성의 성원·사냥 개시.</summary>
        private void OnHeroKilled(UnitBase hero, int expReward)
        {
            if (_traitKillXp > 0 && MawangXpBridge.Mawang != null) MawangXpBridge.Mawang.AddXp(_traitKillXp); // 특성: 처치 XP 보너스

            if (_aug.CooldownOnKillSeconds > 0f && _skillManager != null)
                _skillManager.ReduceCooldowns(_aug.CooldownOnKillSeconds);

            if (_aug.ExplodeOnDeathPower > 0f && _executor != null && hero != null)
                _executor.Detonate(hero.transform.position, _aug.ExplodeOnDeathPower, 1.5f);

            if (_aug.XpPerAliveMonster > 0f && MawangXpBridge.Mawang != null)
            {
                int alive = UnitRegistry.GetAliveCount(UnitSide.DemonArmy);
                MawangXpBridge.Mawang.AddXp(Mathf.RoundToInt(_aug.XpPerAliveMonster * alive));
            }

            if (_aug.HuntAttackBonus > 0f)
            {
                _huntUntil = Time.time + AugmentTuning.HuntSeconds;
                Sync(); // 공격력 배율에 사냥 개시 보너스 반영
            }
        }

        /// <summary>연타 본능 — 몬스터가 공격할 때마다 공격속도 보너스를 쌓는다 (상한 도달 후에는 Sync 안 함).</summary>
        private void OnAttackPerformed(UnitSide side)
        {
            if (side != UnitSide.DemonArmy || _aug.ChainStrikeStep <= 0f) return;
            float next = Mathf.Min(AugmentTuning.ChainStrikeCap, _chainBonus + _aug.ChainStrikeStep);
            if (next <= _chainBonus) return;
            _chainBonus = next;
            Sync();
        }

        /// <summary>전투 시작 때 라운드 단위 증강 상태를 비운다.</summary>
        private void ResetRoundAugmentState()
        {
            _lowHpHealed.Clear();
            _monsterDead.Clear();
            CombatModifierHub.ResetShields(); // 수호의 방패 — 라운드마다 보호막을 다시 두른다

            bool dirty = _huntUntil != 0f || _ironUntil != 0f || _chainBonus != 0f;
            _huntUntil = 0f;
            _ironUntil = 0f;
            _chainBonus = 0f;
            if (dirty && _synergy != null) Sync();
        }

        /// <summary>
        /// 전투 중 매 프레임 감시 — UnitBase 를 건드리지 않고 상태 변화를 읽어서 처리한다.
        ///  · 사냥 개시 만료 → 공격력 배율 원복
        ///  · 불사의 진영: 몬스터가 살아있음→죽음으로 바뀐 순간 확률로 즉시 부활
        ///  · 위기의 치유: 몬스터 체력이 30% 이하가 되면 유닛당 라운드 1회 회복
        /// </summary>
        private void TickAugments()
        {
            bool expired = false;
            if (_huntUntil > 0f && Time.time >= _huntUntil) { _huntUntil = 0f; expired = true; }
            if (_ironUntil > 0f && Time.time >= _ironUntil) { _ironUntil = 0f; expired = true; }
            if (expired) Sync();

            bool inCombat = _skillManager != null && _skillManager.IsCastingEnabled;
            if (!inCombat)
            {
                _wasCombat = false;
                return;
            }

            var monsters = UnitRegistry.GetUnits(UnitSide.DemonArmy);
            if (!_wasCombat)
            {
                // 전투 시작 — 이전 라운드에 죽어 있던 몬스터를 "새로 죽은 것"으로 오인하지 않게 현재 상태를 기록.
                _wasCombat = true;
                ResetRoundAugmentState();
                for (int i = 0; i < monsters.Count; i++)
                    if (monsters[i] != null) _monsterDead[monsters[i]] = monsters[i].currentState == UnitState.Dead;

                // 철벽 진형 — 전투 시작 시점부터 8초간 몬스터가 받는 피해 감소
                if (_aug.IronFormationReduction > 0f)
                {
                    _ironUntil = Time.time + AugmentTuning.IronFormationSeconds;
                    Sync();
                }
            }

            if (_aug.MonsterReviveChance <= 0f && _aug.LowHpHealFraction <= 0f) return;

            for (int i = 0; i < monsters.Count; i++)
            {
                var u = monsters[i];
                if (u == null) continue;

                bool dead = u.currentState == UnitState.Dead;
                _monsterDead.TryGetValue(u, out bool wasDead);

                if (dead && !wasDead && _aug.MonsterReviveChance > 0f && Random.value < _aug.MonsterReviveChance)
                {
                    u.Revive();
                    dead = false;
                    Debug.Log("[증강] 불사의 진영 — 몬스터 부활");
                }
                _monsterDead[u] = dead;

                if (!dead && _aug.LowHpHealFraction > 0f && u.statData != null && !_lowHpHealed.Contains(u))
                {
                    float max = u.statData.maxHealth * CombatModifierHub.GetHpMult(u.statData.job, u.statData.side);
                    if (max > 0f && u.currentHealth <= max * AugmentTuning.LowHpThreshold)
                    {
                        u.Heal(Mathf.RoundToInt(u.statData.maxHealth * _aug.LowHpHealFraction));
                        _lowHpHealed.Add(u);
                        Debug.Log("[증강] 위기의 치유 — 몬스터 회복");
                    }
                }
            }
        }

        private void Sync()
        {
            var syn = _synergy.BuildModifiers();
            var trait = _traits.BuildModifiers();
            var aug = _augments.BuildModifiers();
            _aug = aug;

            float huntMult = Time.time < _huntUntil ? 1f + aug.HuntAttackBonus : 1f; // 사냥 개시
            float chainMult = 1f + _chainBonus;                                       // 연타 본능

            // 철벽 진형(전투 시작 후 일정 시간 받는 피해 감소) · 수호의 방패(첫 피격 무효) — 마왕군 대상
            bool ironActive = _ironUntil > 0f && Time.time < _ironUntil;
            CombatModifierHub.SetDamageTakenMult(UnitSide.DemonArmy, ironActive ? Mathf.Max(0.1f, 1f - aug.IronFormationReduction) : 1f);
            CombatModifierHub.SetFirstHitShield(UnitSide.DemonArmy, aug.MonsterShieldActive);

            // 냉기 침식 — 용사 이동속도·방어력 동시 감소
            // 특성(용사 이동속도·방어 약화)과 증강을 합산. 마왕군 방어(특성)는 마왕군 쪽에 더한다.
            CombatModifierHub.SetMoveSpeedMult(UnitSide.Hero, Mathf.Max(0.3f, Combine(trait.HeroMoveSpeedMult, aug.HeroMoveSpeedMult)));
            CombatModifierHub.SetDefenseAdd(UnitSide.Hero, aug.HeroDefenseAdd + trait.HeroDefenseAdd);
            CombatModifierHub.SetDefenseAdd(UnitSide.DemonArmy, trait.MonsterDefenseAdd);

            foreach (var job in AllJobs)
            {
                // 마왕군(아군) — 특성·증강의 "몬스터" 배율 + 시너지 직업 배율
                float synHp = job == SynergyJob.Shield ? syn.ShieldHpMult : 1f;
                CombatModifierHub.SetAttackMult(job, UnitSide.DemonArmy, Combine(trait.MonsterAttackMult, aug.MonsterAttackMult, JobAttackComponent(job, syn)) * huntMult);
                CombatModifierHub.SetAttackSpeedMult(job, UnitSide.DemonArmy, Combine(trait.MonsterAttackSpeedMult, aug.MonsterAttackSpeedMult, JobSpeedComponent(job, syn)) * chainMult);
                CombatModifierHub.SetHpMult(job, UnitSide.DemonArmy, Combine(trait.MonsterHpMult, aug.MonsterHpMult, synHp));

                // 용사(적) — 특성·증강의 "용사 약화" 배율만(시너지는 아군 배치 전용이라 관여 안 함).
                // 별도 저장소를 진영으로 분리했기 때문에 마왕군 강화 배율이 적 용사한테는 안 넘어간다.
                CombatModifierHub.SetAttackMult(job, UnitSide.Hero, Combine(trait.HeroAttackMult, aug.HeroAttackMult) * _roundHeroAtk);
                CombatModifierHub.SetAttackSpeedMult(job, UnitSide.Hero, trait.HeroAttackSpeedMult * _roundHeroSpd);
                CombatModifierHub.SetHpMult(job, UnitSide.Hero, trait.HeroHpMult * _roundHeroHp);
            }
            CombatModifierHub.SetHealMult(SynergyJob.Healer, UnitSide.DemonArmy, syn.HealerHealAmountMult);
            CombatModifierHub.SetHealMult(SynergyJob.Healer, UnitSide.Hero, _roundHeroHeal);

            _traitKillXp = trait.KillXpBonus;
            if (MawangXpBridge.Mawang != null)
            {
                MawangXpBridge.Mawang.XpGainMult = Combine(trait.XpGainMult, aug.XpGainMult);
                TraitMawangSettings.Apply(trait, MawangXpBridge.Mawang); // 레벨업 필요 XP·레벨 보너스 LP·5레벨 XP
            }

            if (_skillMods != null)
            {
                // 절망 낙인(용사가 받는 스킬 피해 +%) — 스킬은 용사만 때리므로 스킬 피해 배율에 곱한다.
                // 특성·증강 배율에 마왕 레벨 배율(레벨 1당 +4%)을 곱한다. 증강은 고르는 즉시 이 값에 반영된다.
                _skillMods.PowerMult = Combine(trait.SkillPowerMult, aug.SkillPowerMult) * aug.HeroIncomingSkillMult * SkillLevelScaling.CurrentPowerMult();
                _skillMods.CooldownMult = Mathf.Max(0.3f, Combine(trait.SkillCooldownMult, aug.SkillCooldownMult));
                _skillMods.RadiusMult = Combine(trait.SkillRadiusMult, aug.SkillRadiusMult);
                _skillMods.BuffDurationMult = Combine(trait.SkillBuffDurationMult, aug.SkillBuffDurationMult);
                _skillMods.UltCooldownMult = trait.SkillUltCooldownMult;
                _skillMods.CritChance = aug.CritChance;
                _skillMods.EchoChance = aug.EchoChance;
                _skillMods.OnHitSlowAmount = aug.OnHitSlowAmount;
                _skillMods.ReviveBonus = 0;
            }
            if (_skillManager != null && !_debugUnlockAllSkills) _skillManager.EquipCapacity = 3 + trait.ExtraSkillSlots;
        }

        /// <summary>직업별 시너지 공격력 성분. 학살자 콤보(전사+도적)의 처치 보너스는 두 직업 공격력에 얹는다.</summary>
        private static float JobAttackComponent(SynergyJob job, SynergyModifiers syn)
        {
            switch (job)
            {
                case SynergyJob.Warrior: return syn.WarriorAttackMult + syn.ComboExecuteBonusMult;
                case SynergyJob.Mage: return syn.MageAttackMult;
                case SynergyJob.Rogue: return 1f + syn.ComboExecuteBonusMult;
                default: return 1f;
            }
        }

        private static float JobSpeedComponent(SynergyJob job, SynergyModifiers syn)
        {
            switch (job)
            {
                case SynergyJob.Archer: return syn.ArcherAttackSpeedMult;
                case SynergyJob.Rogue: return syn.RogueAttackSpeedMult;
                default: return 1f;
            }
        }
    }
}
