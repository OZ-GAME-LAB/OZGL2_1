using System.Collections.Generic;
using UnityEngine;
using OZGL2.Skill;

namespace OZGL2.Sandbox
{
    /// <summary>
    /// 2D 탑뷰 스킬 밸런스 샌드박스. JOB_SUNGMIN 씬에 빈 GameObject 만들고 이거 붙이면 끝.
    ///
    /// 역할: 큐브(용사) 스폰 + 스탯 튜닝(IMGUI) + SkillManager·SkillBarUI 배선.
    /// 스킬 발동·조준 UI 는 SkillBarUI(재사용 가능) 담당 — 이 클래스는 그거에 의존하지 않는다.
    ///
    /// 실제 씬 이전: SkillManager / SkillBarUI / SkillData / Contracts 는 그대로.
    /// 이 클래스만 버리고, 실제 스포너가 ITargetProvider 를, 실제 컨트롤러가 SkillBarUI.Bind 를 호출.
    /// </summary>
    public class SkillSandbox : MonoBehaviour
    {
        [Header("스폰 (2D 탑뷰 — 위쪽에서 등장)")]
        [SerializeField] private int _enemyCount = 12;
        [SerializeField] private float _enemyHp = 60f;
        [SerializeField] private Vector2 _spawnXRange = new Vector2(-6.5f, 6.5f);
        [SerializeField] private Vector2 _spawnYRange = new Vector2(1f, 4.5f);

        [Header("카메라 / 마왕 위치")]
        [SerializeField] private float _orthoSize = 5.5f;
        [SerializeField] private Vector3 _casterPosition = new Vector3(0f, -4.2f, 0f);

        [Header("스킬 (비워두면 기본 4종 자동 생성)")]
        [SerializeField] private List<SkillData> _skills = new List<SkillData>();

        private SandboxTargetProvider _targets;
        private SkillManager _skillManager;
        private readonly List<SandboxUnit> _units = new List<SandboxUnit>();
        private Sprite _squareSprite;

        private void Start()
        {
            _targets = new SandboxTargetProvider();
            _skillManager = new SkillManager(_targets);

            foreach (var data in ResolveSkills())
            {
                _skillManager.Unlock(data);
            }

            EnsureCamera();
            BuildCaster();
            Spawn();

            var barGo = new GameObject("SkillBarUI");
            barGo.transform.SetParent(transform);
            barGo.AddComponent<SkillBarUI>().Bind(_skillManager, Camera.main, _casterPosition);

            var vfxGo = new GameObject("SkillVfxController");
            vfxGo.transform.SetParent(transform);
            vfxGo.AddComponent<SkillVfxController>().Bind(_skillManager, _casterPosition);
        }

        // ─────────────────────────────────────────── 스킬 폴백

        private IEnumerable<SkillData> ResolveSkills()
        {
            // 1순위: 인스펙터에 직접 넣은 것
            bool hasAny = false;
            foreach (var s in _skills)
            {
                if (s != null)
                {
                    hasAny = true;
                    yield return s;
                }
            }

            if (hasAny)
            {
                yield break;
            }

            // 2순위: Resources/Skills/ (메뉴 OZGL2 > Sandbox > Create Skill Assets 로 생성 — VFX 연결됨)
            var fromResources = Resources.LoadAll<SkillData>("Skills");
            if (fromResources != null && fromResources.Length > 0)
            {
                System.Array.Sort(fromResources, (a, b) => a.tier.CompareTo(b.tier));
                foreach (var s in fromResources)
                {
                    yield return s;
                }

                yield break;
            }

            // 3순위: 코드 하드코딩 (VFX 없음, 디스크 연출)
            yield return MakeSkill("fireball", "화염구", 1, SkillCastMode.Targeted, SkillEffectType.AreaDamage,
                cooldown: 12f, power: 120f, radius: 1.5f);
            yield return MakeSkill("frost_field", "빙결 결계", 2, SkillCastMode.Targeted, SkillEffectType.AreaStun,
                cooldown: 20f, radius: 2f, duration: 2f);
            yield return MakeSkill("chain_lightning", "연쇄 번개", 2, SkillCastMode.Instant, SkillEffectType.ChainDamage,
                cooldown: 15f, power: 80f, chain: 3);
            yield return MakeSkill("time_stop", "시간 정지", 3, SkillCastMode.Instant, SkillEffectType.AreaStun,
                cooldown: 40f, radius: 99f, duration: 1.5f);
        }

        private static SkillData MakeSkill(string id, string name, int tier, SkillCastMode mode,
            SkillEffectType effect, float cooldown, float power = 0f, float radius = 1.5f,
            int chain = 3, float duration = 2f)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillId = id;
            data.displayName = name;
            data.tier = tier;
            data.castMode = mode;
            data.effectType = effect;
            data.cooldown = cooldown;
            data.skillPower = power;
            data.radius = radius;
            data.chainCount = chain;
            data.duration = duration;
            return data;
        }

        // ─────────────────────────────────────────── 스폰

        private void Spawn()
        {
            foreach (var unit in _units)
            {
                if (unit != null)
                {
                    Destroy(unit.gameObject);
                }
            }

            _units.Clear();
            _targets.Clear();

            for (int i = 0; i < _enemyCount; i++)
            {
                var go = new GameObject($"Hero_{i}");
                go.transform.SetParent(transform);
                go.transform.position = new Vector3(
                    Random.Range(_spawnXRange.x, _spawnXRange.y),
                    Random.Range(_spawnYRange.x, _spawnYRange.y),
                    0f);
                go.transform.localScale = Vector3.one * 0.8f;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GetSquareSprite();
                sr.sortingOrder = 1;

                var unit = go.AddComponent<SandboxUnit>();
                unit.Init(_enemyHp, new Color(0.9f, 0.32f, 0.3f));

                _units.Add(unit);
                _targets.Register(unit);
            }
        }

        private void BuildCaster()
        {
            var caster = new GameObject("Caster(마왕)");
            caster.transform.SetParent(transform);
            caster.transform.position = _casterPosition;
            caster.transform.localScale = Vector3.one * 1.1f;
            var sr = caster.AddComponent<SpriteRenderer>();
            sr.sprite = GetSquareSprite();
            sr.color = new Color(0.68f, 0.42f, 1f);
            sr.sortingOrder = 1;
        }

        private void EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("SandboxCamera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }

            cam.orthographic = true;
            cam.orthographicSize = _orthoSize;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
        }

        private Sprite GetSquareSprite()
        {
            if (_squareSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _squareSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width);
            }

            return _squareSprite;
        }

        // ─────────────────────────────────────────── IMGUI (튜닝 전용)

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 230f, 190f), GUI.skin.box);
            GUILayout.Label($"<b>샌드박스 — 튜닝</b>\n살아있는 적: {AliveCount()} / {_units.Count}");

            GUILayout.Space(4f);
            GUILayout.Label($"적 수: {_enemyCount}");
            _enemyCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(_enemyCount, 1f, 40f));

            GUILayout.Label($"적 체력: {_enemyHp:0}");
            _enemyHp = Mathf.Round(GUILayout.HorizontalSlider(_enemyHp, 10f, 2000f) / 10f) * 10f;

            GUILayout.Space(4f);
            if (GUILayout.Button("리스폰"))
            {
                Spawn();
            }

            GUILayout.EndArea();
        }

        private int AliveCount()
        {
            int count = 0;
            foreach (var unit in _units)
            {
                if (unit != null && !unit.IsDead)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
