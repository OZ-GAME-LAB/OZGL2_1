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

        private static readonly SynergyJob[] AllJobs = (SynergyJob[])System.Enum.GetValues(typeof(SynergyJob));

        private SynergyTracker _synergy;
        private TraitTree _traits;
        private AugmentRun _augments;

        private SkillModifiers _skillMods;
        private SkillManager _skillManager;
        private SkillExecutor _executor;
        private RealTargetProvider _targets;
        private RealAllyProvider _allies;

        public SynergyTracker Synergy => _synergy;
        public TraitTree Traits => _traits;
        public AugmentRun Augments => _augments;
        public SkillManager SkillManager => _skillManager;

        private Vector3 CasterPosition => _casterTransform != null ? _casterTransform.position : transform.position;

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
            var equipped = SkillTreeStore.GetEquipped();
            foreach (var data in defs)
            {
                if (data == null) continue;
                bool unlocked = SkillTreeStore.IsUnlocked(data.skillId);
                var runtime = _skillManager.Register(data, unlocked);
                if (unlocked && equipped.Contains(data.skillId)) _skillManager.TryEquip(runtime);
            }

            var execObj = new GameObject("RealSkillExecutor");
            execObj.transform.SetParent(transform);
            _executor = execObj.AddComponent<SkillExecutor>();
            _executor.Bind(_skillManager, _targets, _allies, CasterPosition, _skillMods);
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
                float synHp = job == SynergyJob.Shield ? syn.ShieldHpMult : 1f;
                CombatModifierHub.SetAttackMult(job, Combine(trait.MonsterAttackMult, aug.MonsterAttackMult, JobAttackComponent(job, syn)));
                CombatModifierHub.SetAttackSpeedMult(job, Combine(trait.MonsterAttackSpeedMult, aug.MonsterAttackSpeedMult, JobSpeedComponent(job, syn)));
                CombatModifierHub.SetHpMult(job, Combine(trait.MonsterHpMult, aug.MonsterHpMult, synHp));
            }
            CombatModifierHub.SetHealMult(SynergyJob.Healer, syn.HealerHealAmountMult);

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
            if (_skillManager != null) _skillManager.EquipCapacity = 3 + trait.ExtraSkillSlots;
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
