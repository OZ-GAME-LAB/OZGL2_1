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

            SetupSkills();
            Sync();
        }

        private void OnDestroy()
        {
            if (_synergy != null) _synergy.Changed -= Sync;
            if (_traits != null) _traits.Changed -= Sync;
            if (_augments != null) _augments.Changed -= Sync;
        }

        public void SetCount(SynergyJob job, int count) => _synergy.SetCount(job, count);

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
                // 화염구는 계정 상태·초기화 여부와 무관하게 항상 기본 해금 + 자동 장착 — 신규 플레이어 최소 공격 수단.
                bool starterFree = data.displayName == "화염구";
                bool unlocked = starterFree || _debugUnlockAllSkills || SkillTreeStore.IsUnlocked(data.skillId);
                var runtime = _skillManager.Register(data, unlocked);
                // 디버그 모드: 용량 찰 때까지 등록 순서대로 우선 채워 넣고, 나머지는 디버그 패널에서 직접 스왑.
                bool shouldEquip = unlocked && (starterFree || (_debugUnlockAllSkills
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
            if (_skillManager == null) return;

            int i = 0;
            foreach (var skill in _skillManager.EquippedSkills)
            {
                if (i >= 5) break;
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    TryCast(skill);
                }
                i++;
            }
        }

        private Vector2 _debugSkillScroll;

        /// <summary>디버그 모드 전용 — 등록된 스킬 목록에서 자유롭게 장착/해제(스왑)하는 패널.</summary>
        private void OnGUI()
        {
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

        private void TryCast(SkillRuntime skill)
        {
            if (skill.Data.castMode == SkillCastMode.Instant)
            {
                _skillManager.TryCastInstant(skill);
                return;
            }

            var nearest = _targets.Nearest(CasterPosition);
            Vector3 point = nearest != null ? nearest.Position : CasterPosition;
            _skillManager.TryCastTargeted(skill, point);
        }

        private void Sync()
        {
            var syn = _synergy.BuildModifiers();
            var trait = _traits.BuildModifiers();
            var aug = _augments.BuildModifiers();

            foreach (var job in AllJobs)
            {
                // 마왕군(아군) — 특성·증강의 "몬스터" 배율 + 시너지 직업 배율
                float synHp = job == SynergyJob.Shield ? syn.ShieldHpMult : 1f;
                CombatModifierHub.SetAttackMult(job, UnitSide.DemonArmy, Combine(trait.MonsterAttackMult, aug.MonsterAttackMult, JobAttackComponent(job, syn)));
                CombatModifierHub.SetAttackSpeedMult(job, UnitSide.DemonArmy, Combine(trait.MonsterAttackSpeedMult, aug.MonsterAttackSpeedMult, JobSpeedComponent(job, syn)));
                CombatModifierHub.SetHpMult(job, UnitSide.DemonArmy, Combine(trait.MonsterHpMult, aug.MonsterHpMult, synHp));

                // 용사(적) — 특성·증강의 "용사 약화" 배율만(시너지는 아군 배치 전용이라 관여 안 함).
                // 별도 저장소를 진영으로 분리했기 때문에 마왕군 강화 배율이 적 용사한테는 안 넘어간다.
                CombatModifierHub.SetAttackMult(job, UnitSide.Hero, Combine(trait.HeroAttackMult, aug.HeroAttackMult));
                CombatModifierHub.SetAttackSpeedMult(job, UnitSide.Hero, trait.HeroAttackSpeedMult);
                CombatModifierHub.SetHpMult(job, UnitSide.Hero, trait.HeroHpMult);
            }
            CombatModifierHub.SetHealMult(SynergyJob.Healer, UnitSide.DemonArmy, syn.HealerHealAmountMult);

            if (MawangXpBridge.Mawang != null)
                MawangXpBridge.Mawang.XpGainMult = Combine(trait.XpGainMult, aug.XpGainMult);

            if (_skillMods != null)
            {
                _skillMods.PowerMult = Combine(trait.SkillPowerMult, aug.SkillPowerMult);
                _skillMods.CooldownMult = Mathf.Max(0.3f, Combine(trait.SkillCooldownMult, aug.SkillCooldownMult));
                _skillMods.RadiusMult = Combine(trait.SkillRadiusMult, aug.SkillRadiusMult);
                _skillMods.BuffDurationMult = Combine(trait.SkillBuffDurationMult, aug.SkillBuffDurationMult);
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
