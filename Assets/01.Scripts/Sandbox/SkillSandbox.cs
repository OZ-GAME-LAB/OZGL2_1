using System.Collections.Generic;
using UnityEngine;
using OZGL2.Skill;

namespace OZGL2.Sandbox
{
    /// <summary>
    /// 2D 탑뷰 스킬 밸런스 샌드박스. JOB_SUNGMIN 씬에 빈 GameObject 만들고 이거 붙이면 끝.
    /// 용사(적) + 몬스터(아군) 스폰, 스탯 튜닝(IMGUI), SkillManager·SkillBarUI·SkillExecutor 배선.
    /// 스킬 발동·조준 UI 는 SkillBarUI, 효과 실행은 SkillExecutor 담당.
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

        [Header("카메라 / 마왕")]
        [SerializeField] private float _orthoSize = 6f;
        [SerializeField] private Vector3 _casterPosition = new Vector3(0f, -3.9f, 0f);

        [Header("스킬 (비우면 Resources/Skills 자동 로드)")]
        [SerializeField] private List<SkillData> _skills = new List<SkillData>();

        private SandboxProviders _providers;
        private SkillManager _skillManager;
        private readonly List<SandboxUnit> _heroes = new List<SandboxUnit>();
        private readonly List<SandboxUnit> _monsters = new List<SandboxUnit>();
        private Sprite _square;
        private bool _noCooldown;

        private void Start()
        {
            _providers = new SandboxProviders();
            _skillManager = new SkillManager(_providers);

            foreach (var s in ResolveSkills())
            {
                _skillManager.Unlock(s);
            }

            EnsureCamera();
            BuildCaster();
            SpawnAll();

            var bar = new GameObject("SkillBarUI");
            bar.transform.SetParent(transform);
            bar.AddComponent<SkillBarUI>().Bind(_skillManager, Camera.main, _casterPosition);

            var exec = new GameObject("SkillExecutor");
            exec.transform.SetParent(transform);
            exec.AddComponent<SkillExecutor>().Bind(_skillManager, _providers, _providers, _casterPosition);
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

            // 폴백 최소 세트 (Resources 비었을 때)
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

        // ─────────────────────────────────────────── IMGUI 튜닝

        private void Update()
        {
            if (_noCooldown && _skillManager != null)
            {
                foreach (var s in _skillManager.Skills) s.ResetCooldown();
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 240f, 270f), GUI.skin.box);
            GUILayout.Label($"<b>샌드박스 — 디버그</b>\n용사 {AliveHeroes()}/{_heroes.Count}  몬스터 {AliveMonsters()}/{_monsters.Count}");

            GUILayout.Label($"용사 수 {_heroCount}");
            _heroCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(_heroCount, 1f, 40f));
            GUILayout.Label($"용사 체력 {_heroHp:0}");
            _heroHp = Mathf.Round(GUILayout.HorizontalSlider(_heroHp, 10f, 2000f) / 10f) * 10f;

            GUILayout.Space(4f);
            _noCooldown = GUILayout.Toggle(_noCooldown, " 쿨타임 무시 (연타)");
            if (GUILayout.Button("쿨타임 전체 초기화")) ResetAllCooldowns();
            GUILayout.Space(4f);
            if (GUILayout.Button("전체 리스폰")) SpawnAll();
            if (GUILayout.Button("몬스터 절반 처치 (부활 테스트)")) KillHalfMonsters();
            GUILayout.EndArea();
        }

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
