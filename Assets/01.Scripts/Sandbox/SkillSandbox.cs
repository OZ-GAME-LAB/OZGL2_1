using System.Collections.Generic;
using UnityEngine;
using OZGL2.Skill;
using OZGL2.Progression;

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

        private SandboxProviders _providers;
        private SkillManager _skillManager;
        private SkillModifiers _skillMods;
        private MawangLevel _mawang;
        private TraitTree _traits;
        private TraitModifiers _mods;
        private SkillBarUI _bar;
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

        private void Start()
        {
            _skillMods = new SkillModifiers();
            _mawang = new MawangLevel(); // 레벨·XP·LP 전부 계정 영구
            _providers = new SandboxProviders();
            _skillManager = new SkillManager(_providers, _skillMods);

            _traits = new TraitTree(Resources.LoadAll<TraitData>("Traits"));
            _traits.Changed += RefreshMods;
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
            exec.AddComponent<SkillExecutor>().Bind(_skillManager, _providers, _providers, _casterPosition);
        }

        /// <summary>특성 랭크가 바뀔 때마다 — 모든 소비처에 배율 재적용.</summary>
        private void RefreshMods()
        {
            _mods = _traits.BuildModifiers();
            _traits.ApplyToSkills(_skillMods);

            _skillManager.EquipCapacity = 3 + _mods.ExtraSkillSlots;

            _mawang.XpGainMult = _mods.XpGainMult;
            _mawang.XpNeedMult = _mods.XpNeedMult;
            _mawang.MilestoneBonusLp = _mods.MilestoneBonusLp;
            _mawang.SurgeXpOnMilestone = _mods.SurgeXpPer5Level;
            // 몬스터/용사 체력·취약 배율은 다음 리스폰 때 적용 (SpawnUnit)
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

                bool unlocked = starter || SkillTreeStore.IsUnlocked(data.skillId);
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
            if (u != null && u.Side == UnitSide.Hero && _mawang != null)
            {
                _mawang.AddXp(_heroKillXp + _mods.KillXpBonus);
            }
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
                u.ApplyPermanentMods(_mods.MonsterHpMult, 1f);
            else
                u.ApplyPermanentMods(_mods.HeroHpMult, _mods.HeroIncomingSkillMult);

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
                int g = SkillTreeStore.GrantForRound(_round, _mods.MilestoneSpBonus);
                Debug.Log($"[SP] R{_round} 마일스톤 → +{g} SP (총 {SkillTreeStore.SkillPoints})");
            }
            _showSkillTree = GUILayout.Toggle(_showSkillTree, " 스킬 트리 열기");
            _showTraitTree = GUILayout.Toggle(_showTraitTree, " 특성 트리 열기");
            if (GUILayout.Button("스킬 트리 저장 초기화")) WipeSkillTree();
            if (GUILayout.Button("특성 트리 저장 초기화")) { _traits.ResetAll(); SpawnAll(); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (_showSkillTree) DrawSkillTree();
            if (_showTraitTree) DrawTraitTree();
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
