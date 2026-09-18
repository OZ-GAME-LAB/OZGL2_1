using System.Collections.Generic;
using UnityEngine;
using OZGL2.Skill;
using OZGL2.Progression;
using OZGL2.Augment;
using OZGL2.Synergy;

namespace OZGL2.Sandbox
{
    /// <summary>
    /// 2D 탑뷰 스킬 밸런스 샌드박스. JOB_SUNGMIN 씬에 빈 GameObject 만들고 이거 붙이면 끝.
    /// 용사(적) + 몬스터(아군) 스폰, 스탯 튜닝(IMGUI), SkillManager·SkillBarUI·SkillExecutor 배선.
    /// 스킬 트리(봉인/해금 SP/장착)도 여기 IMGUI 패널로.
    /// </summary>
    public class SkillSandbox : MonoBehaviour
    {
        [Header("용사 (적, 위쪽)")]
        [SerializeField] private int _heroCount = 14;
        [SerializeField] private float _heroHp = 60f;
        [SerializeField] private Vector2 _heroX = new Vector2(-6.5f, 6.5f);
        [SerializeField] private Vector2 _heroY = new Vector2(0.5f, 4.5f);

        [Header("몬스터 (아군, 아래쪽)")]
        [SerializeField] private int _monsterCount = 6;
        [SerializeField] private float _monsterHp = 120f;

        [Header("마왕 레벨업")]
        [SerializeField] private int _heroKillXp = 10;

        [Header("카메라 / 마왕")]
        [SerializeField] private float _orthoSize = 6f;
        [SerializeField] private Vector3 _casterPosition = new Vector3(0f, -3.9f, 0f);

        [Header("스킬 (비우면 Resources/Skills 자동 로드)")]
        [SerializeField] private List<SkillData> _skills = new List<SkillData>();

        [Header("디버그")]
        [Tooltip("체크하면 계정 해금 상태(SkillTreeStore)와 무관하게 모든 스킬을 이번 플레이 세션에서만 봉인 해제 + 전부 장착한다. 계정 데이터는 안 건드림 — 스킬 테스트 전용 씬이라 기본값 켜둠.")]
        [SerializeField] private bool _debugUnlockAllSkills = true;

        private SandboxProviders _providers;
        private SkillManager _skillManager;
        private SkillModifiers _skillMods;
        private MawangLevel _mawang;
        private TraitTree _traits;
        private TraitModifiers _traitMods;
        private AugmentRun _augments;
        private AugmentModifiers _augMods;
        private List<AugmentData> _pendingAugments;
        private SynergyTracker _synergy;
        private SynergyModifiers _synMods;
        private bool _showSynergy;
        private readonly Dictionary<SynergyJob, int> _prevSynergyTier = new Dictionary<SynergyJob, int>();
        private readonly Dictionary<ComboSynergyData, bool> _prevComboActive = new Dictionary<ComboSynergyData, bool>();
        private readonly List<(string text, float expireAt)> _synergyToasts = new List<(string, float)>();
        private const float SynergyToastDuration = 2.5f;
        private SkillBarUI _bar;
        private SkillExecutor _executor;
        private readonly List<SandboxUnit> _heroes = new List<SandboxUnit>();
        private readonly List<SandboxUnit> _monsters = new List<SandboxUnit>();
        private readonly List<SkillRuntime> _runtimes = new List<SkillRuntime>();
        private Sprite _square;
        private bool _noCooldown;
        private bool _showSkillTree;
        private bool _showTraitTree;
        private int _round;
        private Vector2 _treeScroll;
        private Vector2 _traitScroll;
        private Vector2 _synergyScroll;

        private void Start()
        {
            _skillMods = new SkillModifiers();
            _mawang = new MawangLevel(); // 레벨·XP·LP 전부 계정 영구
            _providers = new SandboxProviders();
            _skillManager = new SkillManager(_providers, _skillMods);

            _traits = new TraitTree(Resources.LoadAll<TraitData>("Traits"));
            _traits.Changed += RefreshMods;

            _augments = new AugmentRun(Resources.LoadAll<AugmentData>("Augments"));
            _augments.Changed += RefreshMods;
            _augments.Picked += OnAugmentPicked;

            _synergy = new SynergyTracker(Resources.LoadAll<SynergyData>("Synergies"), Resources.LoadAll<ComboSynergyData>("ComboSynergies"));
            _synergy.Changed += RefreshMods;
            _synergy.Changed += OnSynergyChanged;

            RefreshMods();

            RegisterSkills();

            EnsureCamera();
            BuildCaster();
            SpawnAll();

            var barGo = new GameObject("SkillBarUI");
            barGo.transform.SetParent(transform);
            _bar = barGo.AddComponent<SkillBarUI>();
            _bar.Bind(_skillManager, Camera.main, _casterPosition);

            var exec = new GameObject("SkillExecutor");
            exec.transform.SetParent(transform);
            _executor = exec.AddComponent<SkillExecutor>();
            _executor.Bind(_skillManager, _providers, _providers, _casterPosition, _skillMods);
        }

        /// <summary>1+percent 배율 두 개를 합친다 (둘 다 base=1인 배율값 기준).</summary>
        private static float Combine(float a, float b) => a + b - 1f;
        private static float Combine(float a, float b, float c) => a + b + c - 2f;
        private static float CombineFloor(float a, float b, float floor) => Mathf.Max(floor, a + b - 1f);

        /// <summary>특성 랭크·증강 픽·시너지 배치 수가 바뀔 때마다 — 모든 소비처에 배율 재적용.</summary>
        private void RefreshMods()
        {
            _traitMods = _traits.BuildModifiers();
            _augMods = _augments.BuildModifiers();
            _synMods = _synergy.BuildModifiers();

            _skillMods.PowerMult = Combine(_traitMods.SkillPowerMult, _augMods.SkillPowerMult);
            float cdBeforeCombo = CombineFloor(_traitMods.SkillCooldownMult, _augMods.SkillCooldownMult, 0.3f);
            _skillMods.CooldownMult = Mathf.Max(0.3f, cdBeforeCombo * _synMods.ComboSkillCooldownMult); // 현자의 결속(힐러+마법사)
            _skillMods.RadiusMult = Combine(_traitMods.SkillRadiusMult, _augMods.SkillRadiusMult);
            _skillMods.BuffDurationMult = Combine(_traitMods.SkillBuffDurationMult, _augMods.SkillBuffDurationMult);
            _skillMods.ReviveBonus = 0;
            _skillMods.CritChance = _augMods.CritChance + _synMods.ComboCritChanceBonus; // 증강 + 원거리 포격대(궁수+마법사)
            _skillMods.EchoChance = _augMods.EchoChance;
            _skillMods.OnHitSlowAmount = _augMods.OnHitSlowAmount;

            // 디버그 모드는 등록된 스킬을 전부 장착해서 써볼 수 있게 슬롯을 넉넉히 열어둔다(실제 게임은 3+특성).
            _skillManager.EquipCapacity = _debugUnlockAllSkills ? 30 : 3 + _traitMods.ExtraSkillSlots;

            _mawang.XpGainMult = Combine(_traitMods.XpGainMult, _augMods.XpGainMult);
            _mawang.XpNeedMult = _traitMods.XpNeedMult;
            _mawang.MilestoneBonusLp = _traitMods.MilestoneBonusLp;
            _mawang.SurgeXpOnMilestone = _traitMods.SurgeXpPer5Level;
            // 몬스터/용사 체력·취약 배율은 다음 리스폰 때 적용 (SpawnUnit)
        }

        /// <summary>배치 수가 바뀌어 새로 발동한 시너지·조합 시너지를 감지해 화면 알림 큐에 넣는다.</summary>
        private void OnSynergyChanged()
        {
            foreach (var d in _synergy.Defs)
            {
                int tier = _synergy.TierOf(d.job);
                _prevSynergyTier.TryGetValue(d.job, out int prevTier);
                if (tier > prevTier) PushSynergyToast($"시너지 발동: {d.displayName} {tier}단계");
                _prevSynergyTier[d.job] = tier;
            }
            foreach (var c in _synergy.ComboDefs)
            {
                bool active = _synergy.IsComboActive(c);
                _prevComboActive.TryGetValue(c, out bool prevActive);
                if (active && !prevActive) PushSynergyToast($"조합 시너지 발동: {c.displayName}");
                _prevComboActive[c] = active;
            }
        }

        private void PushSynergyToast(string text) => _synergyToasts.Add((text, Time.time + SynergyToastDuration));

        private void OnAugmentPicked(AugmentData d)
        {
            switch (d.effect)
            {
                case AugmentEffect.InstantSp:
                    SkillTreeStore.SkillPoints += Mathf.RoundToInt(d.value);
                    Debug.Log($"[증강] {d.displayName} → SP +{d.value:0}");
                    break;
                case AugmentEffect.InstantXp:
                    _mawang.AddXp(Mathf.RoundToInt(d.value));
                    Debug.Log($"[증강] {d.displayName} → XP +{d.value:0}");
                    break;
                case AugmentEffect.InstantResetCooldowns:
                    foreach (var s in _skillManager.Skills) s.ResetCooldown();
                    Debug.Log($"[증강] {d.displayName} → 모든 스킬 쿨타임 초기화");
                    break;
                case AugmentEffect.InstantHealMonsters:
                    foreach (var u in _monsters)
                    {
                        if (u != null && !u.IsDead) u.HealFraction(d.value);
                    }
                    Debug.Log($"[증강] {d.displayName} → 몬스터 전체 {d.value:P0} 회복");
                    break;
            }
        }

        // ─────────────────────────────────────────── 스킬 등록 / 트리

        private void RegisterSkills()
        {
            _runtimes.Clear();
            bool firstDamage = true;
            foreach (var data in ResolveSkills())
            {
                bool starter = firstDamage && data.category == SkillCategory.Damage;
                if (data.category == SkillCategory.Damage) firstDamage = false;

                bool unlocked = starter || _debugUnlockAllSkills || SkillTreeStore.IsUnlocked(data.skillId);
                var rt = _skillManager.Register(data, unlocked);
                _runtimes.Add(rt);

                if (starter && !SkillTreeStore.IsUnlocked(data.skillId))
                {
                    SkillTreeStore.SetUnlocked(data.skillId, true);
                }
            }

            RestoreEquipped();
        }

        private void RestoreEquipped()
        {
            if (_debugUnlockAllSkills)
            {
                // 계정에 저장된 장착 구성은 무시하고 전부 장착 — 저장(SaveEquipped)도 안 해서 계정 데이터는 그대로.
                foreach (var rt in _runtimes) _skillManager.TryEquip(rt);
                return;
            }

            var saved = SkillTreeStore.GetEquipped();
            foreach (var id in saved)
            {
                var rt = _runtimes.Find(r => r.Data.skillId == id);
                if (rt != null) _skillManager.TryEquip(rt);
            }

            // 저장된 장착이 없으면 해금된 것 중 앞에서부터 자동 장착
            if (_skillManager.EquippedCount == 0)
            {
                foreach (var rt in _runtimes)
                {
                    if (rt.IsUnlocked && _skillManager.TryEquip(rt) && _skillManager.EquippedCount >= _skillManager.EquipCapacity)
                        break;
                }
                SaveEquipped();
            }
        }

        private void SaveEquipped()
        {
            var ids = new List<string>();
            foreach (var rt in _skillManager.EquippedSkills) ids.Add(rt.Data.skillId);
            SkillTreeStore.SetEquipped(ids);
        }

        private bool TryUnlock(SkillRuntime rt)
        {
            if (rt.IsUnlocked) return false;
            int cost = rt.UnlockCost;
            if (SkillTreeStore.SkillPoints < cost) return false;

            SkillTreeStore.SkillPoints -= cost;
            _skillManager.SetUnlocked(rt, true);
            SkillTreeStore.SetUnlocked(rt.Data.skillId, true);
            return true;
        }

        private IEnumerable<SkillData> ResolveSkills()
        {
            bool any = false;
            foreach (var s in _skills)
            {
                if (s != null) { any = true; yield return s; }
            }

            if (any) yield break;

            var loaded = Resources.LoadAll<SkillData>("Skills");
            if (loaded != null && loaded.Length > 0)
            {
                System.Array.Sort(loaded, (a, b) =>
                {
                    int c = a.category.CompareTo(b.category);
                    return c != 0 ? c : a.tier.CompareTo(b.tier);
                });
                foreach (var s in loaded) yield return s;
                yield break;
            }

            yield return Make("fireball", "화염구", SkillCategory.Damage, 1, SkillCastMode.Targeted, SkillEffectType.AreaDamage, 12f, power: 120f, radius: 1.5f);
            yield return Make("chain", "연쇄 번개", SkillCategory.Damage, 2, SkillCastMode.Instant, SkillEffectType.ChainDamage, 15f, power: 80f);
        }

        private static SkillData Make(string id, string name, SkillCategory cat, int tier,
            SkillCastMode mode, SkillEffectType fx, float cd, float power = 0f, float radius = 1.5f)
        {
            var d = ScriptableObject.CreateInstance<SkillData>();
            d.skillId = id; d.displayName = name; d.category = cat; d.tier = tier;
            d.castMode = mode; d.effectType = fx; d.cooldown = cd; d.skillPower = power; d.radius = radius;
            return d;
        }

        // ─────────────────────────────────────────── 스폰

        private void SpawnAll()
        {
            foreach (var u in _heroes) if (u) Destroy(u.gameObject);
            foreach (var u in _monsters) if (u) Destroy(u.gameObject);
            _heroes.Clear();
            _monsters.Clear();
            _providers.Clear();

            for (int i = 0; i < _heroCount; i++)
            {
                var u = SpawnUnit($"Hero_{i}",
                    new Vector3(Random.Range(_heroX.x, _heroX.y), Random.Range(_heroY.x, _heroY.y), 0f),
                    _heroHp, new Color(0.9f, 0.32f, 0.3f), UnitSide.Hero);
                u.Died += OnUnitDied;
                _heroes.Add(u);
                _providers.RegisterHero(u);
            }

            for (int i = 0; i < _monsterCount; i++)
            {
                float x = (i - (_monsterCount - 1) * 0.5f) * 1.4f;
                var u = SpawnUnit($"Monster_{i}", new Vector3(x, -2.6f, 0f),
                    _monsterHp, new Color(0.4f, 0.55f, 0.95f), UnitSide.Monster);
                _monsters.Add(u);
                _providers.RegisterMonster(u);
            }
        }

        private void OnUnitDied(SandboxUnit u)
        {
            if (u == null) return;

            if (u.Side == UnitSide.Monster)
            {
                // 증강 "불사의 진영" — 몬스터 사망 시 확률로 즉시 부활
                if (_augMods.MonsterReviveChance > 0f && Random.value < _augMods.MonsterReviveChance)
                {
                    _providers.ReviveDead(1);
                }
                return;
            }

            // 증강 "백성의 성원" — 생존 몬스터 수만큼 처치 XP 보너스
            int aliveBonus = Mathf.RoundToInt(_augMods.XpPerAliveMonster * AliveMonsters());
            if (_mawang != null) _mawang.AddXp(_heroKillXp + _traitMods.KillXpBonus + aliveBonus);

            // 증강 "처형자의 축복" — 처치 시 전체 스킬 쿨탐 감소
            if (_augMods.CooldownOnKillSeconds > 0f) _skillManager.ReduceCooldowns(_augMods.CooldownOnKillSeconds);

            // 증강 "연쇄 폭발" — 죽은 자리에서 주변 용사에게 폭발 피해
            if (_augMods.ExplodeOnDeathPower > 0f && _executor != null) _executor.Detonate(u.Position, _augMods.ExplodeOnDeathPower, 1.5f);
        }

        private SandboxUnit SpawnUnit(string name, Vector3 pos, float hp, Color color, UnitSide side)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (side == UnitSide.Monster ? 0.9f : 0.8f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square();
            sr.sortingOrder = 1;
            var u = go.AddComponent<SandboxUnit>();
            u.Init(hp, color, side);

            if (side == UnitSide.Monster)
            {
                u.ApplyPermanentMods(Combine(_traitMods.MonsterHpMult, _augMods.MonsterHpMult, _synMods.ShieldHpMult), 1f);
                if (_augMods.MonsterShieldActive) u.GrantShield(); // 증강 "수호의 방패"
            }
            else
                u.ApplyPermanentMods(_traitMods.HeroHpMult, Combine(_traitMods.HeroIncomingSkillMult, _augMods.HeroIncomingSkillMult));

            return u;
        }

        private void BuildCaster()
        {
            var c = new GameObject("Caster(마왕)");
            c.transform.SetParent(transform);
            c.transform.position = _casterPosition;
            c.transform.localScale = Vector3.one * 1.2f;
            var sr = c.AddComponent<SpriteRenderer>();
            sr.sprite = Square();
            sr.color = new Color(0.68f, 0.42f, 1f);
            sr.sortingOrder = 1;
        }

        private void EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var g = new GameObject("SandboxCamera");
                g.tag = "MainCamera";
                cam = g.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = _orthoSize;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
        }

        private Sprite Square()
        {
            if (_square == null)
            {
                var t = Texture2D.whiteTexture;
                _square = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), t.width);
            }
            return _square;
        }

        // ─────────────────────────────────────────── IMGUI

        private void Update()
        {
            if (_noCooldown && _skillManager != null)
            {
                foreach (var s in _skillManager.Skills) s.ResetCooldown();
            }
        }

        private Vector2 _debugScroll;

        private void OnGUI()
        {
            float h = Mathf.Min(Screen.height - 20f, 560f);
            GUILayout.BeginArea(new Rect(10f, 10f, 262f, h), GUI.skin.box);
            _debugScroll = GUILayout.BeginScrollView(_debugScroll);
            GUILayout.Label($"<b>샌드박스 — 디버그</b>\n용사 {AliveHeroes()}/{_heroes.Count}  몬스터 {AliveMonsters()}/{_monsters.Count}");

            GUILayout.Space(4f);
            GUILayout.Label($"<b>마왕 Lv {_mawang.Level}</b>   XP {_mawang.Xp}/{_mawang.XpToNext}   LP {_mawang.Points}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("XP +50")) _mawang.AddXp(50);
            if (GUILayout.Button("XP +200")) _mawang.AddXp(200);
            if (GUILayout.Button("XP +1000")) _mawang.AddXp(1000);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("마왕 레벨·LP 저장 초기화")) _mawang.ClearSaved();

            GUILayout.Label($"용사 수 {_heroCount}");
            _heroCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(_heroCount, 1f, 40f));
            GUILayout.Label($"용사 체력 {_heroHp:0}");
            _heroHp = Mathf.Round(GUILayout.HorizontalSlider(_heroHp, 10f, 2000f) / 10f) * 10f;

            GUILayout.Space(4f);
            _noCooldown = GUILayout.Toggle(_noCooldown, " 쿨타임 무시 (연타)");
            if (GUILayout.Button("쿨타임 전체 초기화")) ResetAllCooldowns();
            if (GUILayout.Button("전체 리스폰")) SpawnAll();
            if (GUILayout.Button("몬스터 절반 처치 (부활 테스트)")) KillHalfMonsters();

            GUILayout.Space(6f);
            GUILayout.Label($"<b>스킬 포인트: {SkillTreeStore.SkillPoints} SP</b>   (도달 R{_round})");
            if (GUILayout.Button("다음 마일스톤 (+10R 클리어)"))
            {
                _round += 10;
                int g = SkillTreeStore.GrantForRound(_round, _traitMods.MilestoneSpBonus);
                Debug.Log($"[SP] R{_round} 마일스톤 → +{g} SP (총 {SkillTreeStore.SkillPoints})");
                _pendingAugments = _augments.Draw3(_round / 10);
            }
            GUILayout.Label($"증강 보유: {_augments.PickedCount}/{_augments.TotalCount} (전부 유니크)");
            DrawAugmentHud();
            _showSkillTree = GUILayout.Toggle(_showSkillTree, " 스킬 트리 열기");
            _showTraitTree = GUILayout.Toggle(_showTraitTree, " 특성 트리 열기");
            _showSynergy = GUILayout.Toggle(_showSynergy, " 시너지 패널 열기");
            if (GUILayout.Button("스킬 트리 저장 초기화")) WipeSkillTree();
            if (GUILayout.Button("특성 트리 저장 초기화")) { _traits.ResetAll(); SpawnAll(); }
            if (GUILayout.Button("증강 초기화 (새 런)")) { _augments.ResetRun(); SpawnAll(); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (_showSkillTree) DrawSkillTree();
            if (_showTraitTree) DrawTraitTree();
            if (_showSynergy) DrawSynergyPanel();
            if (_pendingAugments != null && _pendingAugments.Count > 0) DrawAugmentPicker();

            DrawActiveSynergyHud();
            DrawSynergyToasts();
        }

        /// <summary>디버그 패널을 안 열어도 항상 보이는 "현재 발동 중" 시너지 목록 — 실제 플레이 테스트용 HUD.</summary>
        private void DrawActiveSynergyHud()
        {
            var active = new List<string>();
            foreach (var d in _synergy.Defs)
            {
                int tier = _synergy.TierOf(d.job);
                if (tier > 0) active.Add($"{d.displayName} {tier}단계");
            }
            foreach (var c in _synergy.ComboDefs)
                if (_synergy.IsComboActive(c)) active.Add(c.displayName);

            if (active.Count == 0) return;

            float h = 24f * active.Count + 12f;
            GUILayout.BeginArea(new Rect(10f, Screen.height - h - 10f, 220f, h), GUI.skin.box);
            GUILayout.Label("<b>발동 중인 시너지</b>");
            foreach (var s in active) GUILayout.Label($"<color=#7fffb0>◆ {s}</color>");
            GUILayout.EndArea();
        }

        /// <summary>시너지가 새로 발동한 순간 화면 중앙 상단에 잠깐 뜨는 알림.</summary>
        private void DrawSynergyToasts()
        {
            _synergyToasts.RemoveAll(t => Time.time >= t.expireAt);
            if (_synergyToasts.Count == 0) return;

            float w = 320f;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - w / 2f, 20f, w, 24f * _synergyToasts.Count));
            foreach (var t in _synergyToasts)
                GUILayout.Label($"<color=#ffdd55><b>{t.text}</b></color>");
            GUILayout.EndArea();
        }

        /// <summary>
        /// 시너지 검증 패널 — 실제 그리드가 없어서 직업별 "배치 수"를 직접 입력해 판정 로직만 확인.
        /// 그리드 붙으면 이 숫자는 그리드 매니저(김건·준기)가 SetCount 로 채워주면 됨.
        /// </summary>
        private void DrawSynergyPanel()
        {
            float h = Mathf.Min(Screen.height - 20f, 560f);
            GUILayout.BeginArea(new Rect(280f, 10f, 340f, h), GUI.skin.box);
            _synergyScroll = GUILayout.BeginScrollView(_synergyScroll);
            GUILayout.Label("<b>시너지 (배치 수 시뮬레이션)</b>");

            foreach (var d in _synergy.Defs)
            {
                int count = _synergy.CountOf(d.job);
                int tier = _synergy.TierOf(d.job);
                string tierTag = tier == 2 ? "[2단계]" : tier == 1 ? "[1단계]" : "[비활성]";

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{d.displayName} {tierTag}", GUILayout.Width(110f));
                if (GUILayout.Button("-", GUILayout.Width(24f))) _synergy.SetCount(d.job, count - 1);
                GUILayout.Label(count.ToString(), GUILayout.Width(24f));
                if (GUILayout.Button("+", GUILayout.Width(24f))) _synergy.SetCount(d.job, count + 1);
                GUILayout.Label($"<color=#888>{d.connStatus}</color>", GUILayout.Width(60f));
                GUILayout.EndHorizontal();

                string desc = tier == 2 ? d.tier2Desc : tier == 1 ? d.tier1Desc : $"{d.tier1Threshold}명·{d.tier2Threshold}명 필요";
                GUILayout.Label($"<color=#aaa>  {desc}</color>");
            }

            GUILayout.Space(8f);
            GUILayout.Label("<b>조합 시너지 (서로 다른 직업)</b>");
            foreach (var c in _synergy.ComboDefs)
            {
                bool active = _synergy.IsComboActive(c);
                string tag = active ? "[발동]" : "[비활성]";
                GUILayout.Label($"{c.displayName} {tag} <color=#888>{c.connStatus}</color>");
                GUILayout.Label($"<color=#aaa>  {c.desc}</color>");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// 이번 런에 고른 증강 목록 — 희수가 실제 인게임 HUD(아이콘 줄) 만들 때 참고할 자리.
        /// AugmentRun.PickedList 그대로 노출, 여기선 텍스트로만.
        /// </summary>
        private void DrawAugmentHud()
        {
            if (_augments.PickedList.Count == 0)
            {
                GUILayout.Label("<color=#888>(고른 증강 없음)</color>");
                return;
            }

            foreach (var a in _augments.PickedList)
            {
                string tag = a.isInstant ? "[즉시] " : "";
                GUILayout.Label($"<color=#bbb>· {tag}{a.displayName}</color>");
            }
        }

        private void DrawAugmentPicker()
        {
            const float w = 620f, h = 190f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 20f, w, h);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("<b>증강 선택 — 3장 중 1장</b>");
            GUILayout.BeginHorizontal();
            foreach (var a in _pendingAugments)
            {
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(190f));
                GUILayout.Label($"<b>[{AugmentData.TierName(a.tier)}] {a.displayName}</b>");
                GUILayout.Label(a.description);
                GUILayout.Label($"<color=#888>{a.connStatus}</color>");
                if (GUILayout.Button("선택"))
                {
                    _augments.Pick(a);
                    _pendingAugments = null;
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawTraitTree()
        {
            float x = _showSkillTree ? Screen.width - 700f : Screen.width - 360f;
            GUILayout.BeginArea(new Rect(x, 10f, 350f, Screen.height - 20f), GUI.skin.box);
            GUILayout.Label($"<b>특성 트리</b>   보유 {_mawang.Points} LP");
            if (GUILayout.Button("전체 리스폰 (체력 특성 반영)")) SpawnAll();
            _traitScroll = GUILayout.BeginScrollView(_traitScroll);

            foreach (TraitBranch br in System.Enum.GetValues(typeof(TraitBranch)))
            {
                GUILayout.Space(4f);
                GUILayout.Label($"<b>── {BranchName(br)} ──</b>");
                foreach (TraitLine ln in System.Enum.GetValues(typeof(TraitLine)))
                {
                    foreach (var d in _traits.Defs)
                    {
                        if (d.branch != br || d.line != ln) continue;
                        DrawTraitRow(d);
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTraitRow(TraitData d)
        {
            int rank = _traits.RankOf(d.id);
            bool open = _traits.IsOpen(d.id);
            bool maxed = _traits.IsMaxed(d.id);
            int cost = _traits.NextCost(d.id);

            GUILayout.BeginHorizontal();
            string prefix = d.line == TraitLine.Capstone ? "★ " : (open ? "" : "[잠금] ");
            GUILayout.Label($"{prefix}{d.displayName}  {rank}/{d.maxRank}  <color=#888>{d.connStatus}</color>", GUILayout.Width(230f));

            if (maxed)
            {
                GUILayout.Label("만렙", GUILayout.Width(90f));
            }
            else if (!open)
            {
                GUILayout.Label("전제 미충족", GUILayout.Width(90f));
            }
            else
            {
                GUI.enabled = _mawang.Points >= cost;
                if (GUILayout.Button($"랭크업 {cost}LP", GUILayout.Width(90f))) _traits.TryRank(d.id, _mawang);
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }

        private static string BranchName(TraitBranch b) => b switch
        {
            TraitBranch.Monster => "몬스터",
            TraitBranch.Hero => "용사",
            TraitBranch.Skill => "스킬",
            TraitBranch.Economy => "재화",
            _ => b.ToString(),
        };

        private void DrawSkillTree()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 340f, 10f, 330f, Screen.height - 20f), GUI.skin.box);
            GUILayout.Label($"<b>스킬 트리</b>   장착 {_skillManager.EquippedCount}/{_skillManager.EquipCapacity}   ·   {SkillTreeStore.SkillPoints} SP");
            _treeScroll = GUILayout.BeginScrollView(_treeScroll);

            foreach (SkillCategory cat in System.Enum.GetValues(typeof(SkillCategory)))
            {
                bool header = false;
                foreach (var rt in _runtimes)
                {
                    if (rt.Data.category != cat) continue;
                    if (!header)
                    {
                        GUILayout.Space(4f);
                        GUILayout.Label($"<b>{CategoryName(cat)}</b>");
                        header = true;
                    }
                    DrawSkillRow(rt);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSkillRow(SkillRuntime rt)
        {
            GUILayout.BeginHorizontal();
            string tag = rt.IsEquipped ? "[장착] " : (rt.IsUnlocked ? "[보유] " : "[봉인] ");
            GUILayout.Label($"{tag}{rt.Data.displayName}  T{rt.Data.tier}", GUILayout.Width(190f));

            if (!rt.IsUnlocked)
            {
                GUI.enabled = SkillTreeStore.SkillPoints >= rt.UnlockCost;
                if (GUILayout.Button($"해금 {rt.UnlockCost}SP", GUILayout.Width(90f))) TryUnlock(rt);
                GUI.enabled = true;
            }
            else if (rt.IsEquipped)
            {
                if (GUILayout.Button("해제", GUILayout.Width(90f)))
                {
                    _skillManager.Unequip(rt);
                    SaveEquipped();
                    _bar.Rebuild();
                }
            }
            else
            {
                GUI.enabled = _skillManager.EquippedCount < _skillManager.EquipCapacity;
                if (GUILayout.Button("장착", GUILayout.Width(90f)))
                {
                    if (_skillManager.TryEquip(rt)) { SaveEquipped(); _bar.Rebuild(); }
                }
                GUI.enabled = true;
            }

            GUILayout.EndHorizontal();
        }

        private void WipeSkillTree()
        {
            var ids = new List<string>();
            foreach (var rt in _runtimes) ids.Add(rt.Data.skillId);
            SkillTreeStore.Wipe(ids);
            _round = 0;

            foreach (var rt in _runtimes)
            {
                _skillManager.Unequip(rt);
                _skillManager.SetUnlocked(rt, false);
            }
            RegisterStarterAgain();
            _bar.Rebuild();
        }

        private void RegisterStarterAgain()
        {
            foreach (var rt in _runtimes)
            {
                if (rt.Data.category == SkillCategory.Damage)
                {
                    _skillManager.SetUnlocked(rt, true);
                    SkillTreeStore.SetUnlocked(rt.Data.skillId, true);
                    _skillManager.TryEquip(rt);
                    SaveEquipped();
                    break;
                }
            }
        }

        private static string CategoryName(SkillCategory c) => c switch
        {
            SkillCategory.Damage => "딜",
            SkillCategory.Debuff => "디버프",
            SkillCategory.Buff => "버프",
            SkillCategory.Ultimate => "궁극",
            _ => c.ToString(),
        };

        private void ResetAllCooldowns()
        {
            foreach (var s in _skillManager.Skills) s.ResetCooldown();
        }

        private void KillHalfMonsters()
        {
            int n = _monsters.Count / 2;
            for (int i = 0; i < n; i++)
            {
                if (_monsters[i] != null) _monsters[i].TakeDamage(99999f);
            }
        }

        private int AliveHeroes()
        {
            int c = 0;
            foreach (var u in _heroes) if (u != null && !u.IsDead) c++;
            return c;
        }

        private int AliveMonsters()
        {
            int c = 0;
            foreach (var u in _monsters) if (u != null && !u.IsDead) c++;
            return c;
        }
    }
}
