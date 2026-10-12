using System;
using System.Collections;
using System.Collections.Generic;
using OZGL2.InGame;
using OZGL2.Skill;
using OZGL2.Stage;
using OZGL2.Synergy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 인게임에 게임다운 손맛을 얹는 연출 모음. 팀 코드(전투·스테이지)는 건드리지 않고 상태·체력·시전 이벤트를 읽어서 그 위에 덧붙인다.
    /// 1) 웨이브 시작 배너(「WAVE 5」+ 한 줄 안내, 보스 웨이브는 붉은 경고 비네트와 「BOSS」)
    /// 2) 피격 연출: 대미지 숫자(크게 맞으면 노랗고 크게), 맞은 자리의 번쩍임, 쓰러질 때의 먼지 구름
    /// 3) 배치·합성 연출용 먼지·별 폭발 (InGameBoardStyle 이 부른다)
    /// 4) 웨이브 클리어: 마지막 용사를 쓰러뜨리는 순간 슬로모션과 화면 다가감, 「WAVE CLEAR」 배너
    /// 5) 스킬 시전: 화면이 살짝 다가갔다 돌아온다(흔들림은 InGameKingCastFx 가 이미 한다)
    /// 그림은 전부 코드로 만든 부드러운 원·고리·별이라 별도 이미지가 없다. InGamePrototypeBootstrap 이 있는 씬이 열리면 자동으로 붙는다.
    /// </summary>
    [DefaultExecutionOrder(950)]
    public sealed class InGameBattleFx : MonoBehaviour
    {
        public static InGameBattleFx Instance { get; private set; }

        [Header("켜고 끄기")]
        [SerializeField] private bool _waveBanner = true;
        [SerializeField] private bool _damageNumbers = true;
        [SerializeField] private bool _hitSparks = true;
        [SerializeField] private bool _deathPoof = true;
        [SerializeField] private bool _lastKillSlowMo = true;
        [SerializeField] private bool _castPunch = true;
        [Header("조절")]
        [SerializeField, Range(0.05f, 0.6f), Tooltip("마지막 용사를 쓰러뜨릴 때의 배속(작을수록 느리게)")] private float _slowMoScale = 0.22f;
        [SerializeField, Min(0.1f), Tooltip("슬로모션이 이어지는 실제 시간(초)")] private float _slowMoSeconds = 0.85f;
        [SerializeField, Range(0f, 0.12f), Tooltip("스킬을 쓸 때 화면이 다가가는 정도")] private float _castPunchAmount = 0.035f;
        [SerializeField, Range(0.04f, 0.4f), Tooltip("같은 유닛이 연달아 맞을 때 숫자를 하나로 묶는 시간(초)")] private float _numberMerge = 0.12f;

        private sealed class Fx
        {
            public Transform tf;
            public SpriteRenderer sr;
            public TextMesh text, shadow;
            public float age, life;
            public Vector3 start, vel;
            public Action<Fx, float> tick;
        }

        private sealed class Track
        {
            public int hp, max;
            public float pending, pendingAt, lastSpark;
            public bool dead;
        }

        private static readonly UnitSide[] Sides = { UnitSide.Hero, UnitSide.DemonArmy };
        private readonly List<Fx> _active = new List<Fx>();
        private readonly Stack<Fx> _pool = new Stack<Fx>();
        private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>();
        private readonly List<int> _drop = new List<int>();
        private Transform _root;
        private Sprite _glow, _ring, _star;
        private Font _font;
        private float _cell = 1f;

        private InGamePrototypeBootstrap _bootstrap;
        private RealSynergySync _sync;
        private SkillManager _hooked;
        private eStageState _lastState = eStageState.IDLE;
        private int _clearedAtStart, _kills, _combatTotal;
        private bool _slowActive;
        private float _slowSaved = 1f;

        // 배너(화면 위에 덮는 캔버스)
        private Canvas _canvas;
        private TMP_Text _title, _sub;
        private Image _band, _lineTop, _lineBottom, _warn, _warnLeft, _warnRight;
    private RawImage _hazardTop, _hazardBottom;
        private CanvasGroup _bannerGroup;
        private RectTransform _bannerRoot;
        private Coroutine _bannerRoutine;
        private Sprite _soft, _vignette, _warnIcon;
    private Texture2D _stripe;
        private TMP_FontAsset _tmpFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        private static void Install()
        {
            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (bootstrap == null || bootstrap.GetComponent<InGameBattleFx>() != null) return;
            bootstrap.gameObject.AddComponent<InGameBattleFx>();
        }

        private void Awake()
        {
            Instance = this;
            _bootstrap = GetComponent<InGamePrototypeBootstrap>();
        }

        private void OnEnable() => UnitBase.OnHeroKilled += OnHeroKilled;

        private void OnDisable()
        {
            UnitBase.OnHeroKilled -= OnHeroKilled;
            if (_hooked != null) _hooked.CastRequested -= OnCast;
            _hooked = null;
            EndSlowMo(true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_root != null) Destroy(_root.gameObject);
        }

        // ───────────── 매 프레임

        private void Update()
        {
            EnsureAssets();
            HookCast();
            WatchStage();
            if (_damageNumbers || _hitSparks || _deathPoof) PollUnits();
            TickFx(Time.unscaledDeltaTime);
        }

        private void HookCast()
        {
            if (_sync == null) _sync = FindFirstObjectByType<RealSynergySync>();
            var manager = _sync != null ? _sync.SkillManager : null;
            if (manager == _hooked) return;
            if (_hooked != null) _hooked.CastRequested -= OnCast;
            _hooked = manager;
            if (_hooked != null) _hooked.CastRequested += OnCast;
        }

        private void OnCast(SkillCastRequest request)
        {
            if (_castPunch) InGameCameraControl.Punch(_castPunchAmount);
        }

        // ───────────── 1·4. 웨이브 시작·클리어

        private void WatchStage()
        {
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage == null) return;
            var state = stage.State;
            if (state == _lastState) return;
            var previous = _lastState;
            _lastState = state;

            if (state == eStageState.COMBAT)
            {
                _kills = 0;
                _clearedAtStart = stage.ClearedRoundCount;
                var round = _bootstrap.CurrentRoundDefinition;
                _combatTotal = 0;
                if (round != null) foreach (var spawn in round.Spawns) _combatTotal += spawn.Count;
                bool boss = round != null && round.IsBossRound;
                if (_waveBanner) ShowBanner("WAVE " + stage.CurrentRoundNumber, boss ? "DANGER" : string.Empty, boss ? BannerKind.Boss : BannerKind.Wave);
            }
            else if (previous == eStageState.COMBAT)
            {
                EndSlowMo(false);
                if (_waveBanner && stage.ClearedRoundCount > _clearedAtStart && state != eStageState.FAILED)
                    ShowBanner("WAVE CLEAR", "마왕군의 승리!", BannerKind.Clear);
            }
        }

        private void OnHeroKilled(UnitBase hero, int exp)
        {
            _kills++;
            if (!_lastKillSlowMo || _combatTotal <= 0 || _kills < _combatTotal) return;
            if (_bootstrap == null || _bootstrap.Stage == null || _bootstrap.Stage.State != eStageState.COMBAT) return;
            // 마지막 용사가 쓰러지는 순간: 슬로모션 + 화면이 다가감
            StartSlowMo();
            InGameCameraControl.Punch(0.06f);
            if (hero != null) StarBurst(hero.transform.position, 1.3f);
        }

        private void StartSlowMo()
        {
            if (_slowActive) return;
            _slowActive = true;
            _slowSaved = Time.timeScale;
            if (_slowSaved <= 0.01f) { _slowActive = false; return; }   // 정지 중이면 건드리지 않는다
            Time.timeScale = Mathf.Min(_slowSaved, _slowMoScale);
            StartCoroutine(SlowMoRoutine());
        }

        private IEnumerator SlowMoRoutine()
        {
            yield return new WaitForSecondsRealtime(_slowMoSeconds);
            EndSlowMo(false);
        }

        private void EndSlowMo(bool force)
        {
            if (!_slowActive) return;
            _slowActive = false;
            // 그 사이 다른 곳(배속 막대)이 속도를 바꿨다면 그대로 둔다
            if (force || Mathf.Abs(Time.timeScale - Mathf.Min(_slowSaved, _slowMoScale)) < 0.01f) Time.timeScale = _slowSaved;
        }

        // ───────────── 2. 피격 연출

        private void PollUnits()
        {
            foreach (UnitSide side in Sides)
            {
                var list = UnitRegistry.GetUnits(side);
                for (int i = 0; i < list.Count; i++)
                {
                    var unit = list[i];
                    if (unit == null || unit.statData == null || !unit.gameObject.activeInHierarchy) continue;
                    int id = unit.GetInstanceID();
                    if (!_tracks.TryGetValue(id, out var track))
                    {
                        _tracks[id] = new Track { hp = unit.currentHealth, max = Mathf.Max(1, Mathf.Max(unit.currentHealth, unit.statData.maxHealth)), dead = unit.currentState == UnitState.Dead };
                        continue;
                    }

                    bool dead = unit.currentState == UnitState.Dead;
                    if (track.dead && !dead) { track.hp = unit.currentHealth; track.max = Mathf.Max(1, Mathf.Max(unit.currentHealth, unit.statData.maxHealth)); track.pending = 0f; } // 되살아남/재사용
                    int now = unit.currentHealth;
                    track.max = Mathf.Max(track.max, now);
                    if (now < track.hp && !track.dead)
                    {
                        float damage = track.hp - now;
                        track.pending += damage;
                        if (track.pendingAt <= 0f) track.pendingAt = Time.unscaledTime;
                        if (_hitSparks && Time.unscaledTime - track.lastSpark > 0.09f && _active.Count < 140)
                        {
                            track.lastSpark = Time.unscaledTime;
                            HitSpark(unit.transform.position + new Vector3(UnityEngine.Random.Range(-0.18f, 0.18f), 0.45f * _cell, 0f), side == UnitSide.Hero);
                        }
                    }
                    else if (now > track.hp && !track.dead && _damageNumbers && now - track.hp >= 1)
                    {
                        Number(unit.transform.position + Vector3.up * 0.9f * _cell, "+" + (now - track.hp), new Color(0.5f, 1f, 0.6f), 0.8f, false);
                    }
                    track.hp = now;

                    if (track.pending > 0f && Time.unscaledTime - track.pendingAt >= _numberMerge)
                    {
                        FlushNumber(unit, track, side);
                    }
                    if (dead && !track.dead)
                    {
                        if (track.pending > 0f) FlushNumber(unit, track, side);
                        if (_deathPoof) Poof(unit.transform.position, side == UnitSide.Hero);
                    }
                    track.dead = dead;
                }
            }

            // 사라진 유닛 기록 정리
            if (Time.frameCount % 120 == 0)
            {
                _drop.Clear();
                foreach (var pair in _tracks) if (pair.Value == null) _drop.Add(pair.Key);
                if (_tracks.Count > 600) _tracks.Clear();
            }
        }

        private void FlushNumber(UnitBase unit, Track track, UnitSide side)
        {
            int amount = Mathf.RoundToInt(track.pending);
            track.pending = 0f; track.pendingAt = 0f;
            if (!_damageNumbers || amount <= 0) return;
            bool big = amount >= track.max * 0.2f;
            bool hero = side == UnitSide.Hero;
            Color color = hero ? (big ? new Color(1f, 0.82f, 0.25f) : Color.white) : new Color(1f, 0.38f, 0.32f);
            float scale = (hero ? 1f : 0.85f) * (big ? 1.5f : 1f);
            Number(unit.transform.position + new Vector3(UnityEngine.Random.Range(-0.22f, 0.22f), 1.0f * _cell, 0f), (hero ? string.Empty : "-") + amount + (big ? "!" : string.Empty), color, scale, big);
            if (big && hero) InGameCameraControl.Punch(0.012f);
        }

        // ───────────── 효과 만들기(바깥에서도 부른다)

        public void HitSpark(Vector3 pos, bool onHero)
        {
            EnsureAssets();
            Color core = onHero ? new Color(1f, 0.95f, 0.7f, 1f) : new Color(1f, 0.5f, 0.4f, 1f);
            var f = Spawn(_glow, pos, core, 0.16f, 1100);
            f.tick = (fx, k) =>
            {
                float s = Mathf.Lerp(0.15f, 0.9f, Mathf.Sqrt(k)) * _cell;
                fx.tf.localScale = new Vector3(s, s, 1f);
                var c = core; c.a = (1f - k) * 0.95f; fx.sr.color = c;
            };
            var r = Spawn(_ring, pos, Color.white, 0.2f, 1101);
            r.tick = (fx, k) =>
            {
                float s = Mathf.Lerp(0.2f, 0.85f, 1f - (1f - k) * (1f - k)) * _cell;
                fx.tf.localScale = new Vector3(s, s, 1f);
                fx.sr.color = new Color(1f, 1f, 1f, (1f - k) * 0.8f);
            };
        }

        public void Poof(Vector3 pos, bool hero)
        {
            EnsureAssets();
            Color tone = hero ? new Color(0.92f, 0.9f, 0.86f, 0.75f) : new Color(0.75f, 0.62f, 0.85f, 0.75f);
            for (int i = 0; i < 6; i++)
            {
                float ang = (i / 6f) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.4f, 0.4f);
                var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f, 0f);
                var f = Spawn(_glow, pos + Vector3.up * 0.25f * _cell, tone, UnityEngine.Random.Range(0.4f, 0.65f), 1090);
                f.vel = dir * UnityEngine.Random.Range(0.6f, 1.4f) * _cell;
                float size = UnityEngine.Random.Range(0.45f, 0.8f) * _cell;
                f.tick = (fx, k) =>
                {
                    fx.tf.position = fx.start + fx.vel * (1f - Mathf.Pow(1f - k, 2f));
                    float s = Mathf.Lerp(size * 0.5f, size * 1.5f, k);
                    fx.tf.localScale = new Vector3(s, s, 1f);
                    var c = tone; c.a = tone.a * (1f - k); fx.sr.color = c;
                };
            }
        }

        /// <summary>발밑에서 퍼지는 먼지(유닛을 칸에 내려놓을 때).</summary>
        public void Dust(Vector3 feet, float cell)
        {
            EnsureAssets();
            var tone = new Color(0.86f, 0.78f, 0.6f, 0.7f);
            for (int i = 0; i < 7; i++)
            {
                float side = (i / 6f) * 2f - 1f;
                var f = Spawn(_glow, feet + new Vector3(side * 0.12f * cell, 0f, 0f), tone, UnityEngine.Random.Range(0.32f, 0.5f), 1085);
                f.vel = new Vector3(side * 0.55f * cell + UnityEngine.Random.Range(-0.1f, 0.1f), UnityEngine.Random.Range(0.05f, 0.22f) * cell, 0f);
                float size = UnityEngine.Random.Range(0.28f, 0.45f) * cell;
                f.tick = (fx, k) =>
                {
                    fx.tf.position = fx.start + fx.vel * (1f - Mathf.Pow(1f - k, 2f));
                    float s = Mathf.Lerp(size * 0.6f, size * 1.4f, k);
                    fx.tf.localScale = new Vector3(s, s * 0.7f, 1f);
                    var c = tone; c.a = tone.a * (1f - k) * (1f - k); fx.sr.color = c;
                };
            }
        }

        /// <summary>성급이 오를 때(합성) 터지는 금빛 별과 빛 고리.</summary>
        public void StarBurst(Vector3 pos, float cellScale = 1f)
        {
            EnsureAssets();
            var gold = new Color(1f, 0.85f, 0.35f, 1f);
            var ring = Spawn(_ring, pos + Vector3.up * 0.4f * _cell, gold, 0.45f, 1100);
            ring.tick = (fx, k) =>
            {
                float s = Mathf.Lerp(0.3f, 2.2f * cellScale, 1f - (1f - k) * (1f - k)) * _cell;
                fx.tf.localScale = new Vector3(s, s, 1f);
                var c = gold; c.a = (1f - k) * 0.95f; fx.sr.color = c;
            };
            var flash = Spawn(_glow, pos + Vector3.up * 0.4f * _cell, new Color(1f, 0.95f, 0.7f, 0.9f), 0.35f, 1099);
            flash.tick = (fx, k) =>
            {
                float s = Mathf.Lerp(0.6f, 2.6f * cellScale, k) * _cell;
                fx.tf.localScale = new Vector3(s, s, 1f);
                fx.sr.color = new Color(1f, 0.95f, 0.7f, (1f - k) * 0.8f);
            };
            for (int i = 0; i < 9; i++)
            {
                float ang = (i / 9f) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
                var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                var s = Spawn(_star, pos + Vector3.up * 0.4f * _cell, gold, UnityEngine.Random.Range(0.5f, 0.8f), 1102);
                s.vel = dir * UnityEngine.Random.Range(0.9f, 1.8f) * cellScale * _cell;
                float size = UnityEngine.Random.Range(0.18f, 0.32f) * _cell;
                float spin = UnityEngine.Random.Range(-240f, 240f);
                s.tick = (fx, k) =>
                {
                    var p = fx.start + fx.vel * (1f - Mathf.Pow(1f - k, 2f));
                    p.y -= k * k * 0.5f * _cell;   // 살짝 떨어진다
                    fx.tf.position = p;
                    fx.tf.rotation = Quaternion.Euler(0f, 0f, spin * k);
                    float sz = size * (k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f * 0.6f);
                    fx.tf.localScale = new Vector3(sz, sz, 1f);
                    var c = gold; c.a = 1f - k * k; fx.sr.color = c;
                };
            }
        }

        private void Number(Vector3 pos, string label, Color color, float scale, bool big)
        {
            EnsureAssets();
            if (_active.Count > 160) return;
            var f = Spawn(null, pos, color, big ? 0.95f : 0.75f, 1200);
            f.text.text = label; f.shadow.text = label;
            f.text.color = color; f.shadow.color = new Color(0.08f, 0.03f, 0.05f, 0.95f);
            float baseSize = 0.05f * _cell * scale;
            float drift = UnityEngine.Random.Range(-0.25f, 0.25f) * _cell;
            f.tick = (fx, k) =>
            {
                float rise = (1f - Mathf.Pow(1f - k, 3f)) * (big ? 1.1f : 0.8f) * _cell;
                fx.tf.position = fx.start + new Vector3(drift * k, rise, 0f);
                float pop = k < 0.12f ? Mathf.Lerp(0.5f, 1.25f, k / 0.12f) : k < 0.25f ? Mathf.Lerp(1.25f, 1f, (k - 0.12f) / 0.13f) : 1f;
                float s = baseSize * pop;
                fx.tf.localScale = new Vector3(s, s, 1f);
                float a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                var c = color; c.a = a; fx.text.color = c;
                var sc = fx.shadow.color; sc.a = 0.95f * a; fx.shadow.color = sc;
            };
        }

        // ───────────── 효과 관리(풀)

        private Fx Spawn(Sprite sprite, Vector3 pos, Color color, float life, int order)
        {
            Fx f = _pool.Count > 0 ? _pool.Pop() : CreateFx();
            f.age = 0f; f.life = life; f.start = pos; f.vel = Vector3.zero; f.tick = null;
            f.tf.gameObject.SetActive(true);
            f.tf.position = pos;
            f.tf.rotation = Quaternion.identity;
            f.tf.localScale = Vector3.one;
            bool isText = sprite == null;
            f.sr.enabled = !isText;
            f.text.gameObject.SetActive(isText);
            f.shadow.gameObject.SetActive(isText);
            if (!isText) { f.sr.sprite = sprite; f.sr.color = color; f.sr.sortingOrder = order; }
            else { f.text.GetComponent<MeshRenderer>().sortingOrder = order + 1; f.shadow.GetComponent<MeshRenderer>().sortingOrder = order; }
            _active.Add(f);
            return f;
        }

        private Fx CreateFx()
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(_root, false);
            var f = new Fx { tf = go.transform, sr = go.AddComponent<SpriteRenderer>() };
            f.shadow = NewText("Shadow", go.transform, new Vector3(0.05f, -0.05f, 0.01f));
            f.text = NewText("Text", go.transform, Vector3.zero);
            return f;
        }

        private TextMesh NewText(string name, Transform parent, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            var t = go.AddComponent<TextMesh>();
            t.font = _font;
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.fontSize = 64;
            t.characterSize = 1f;
            t.fontStyle = FontStyle.Bold;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font != null ? _font.material : null;
            return t;
        }

        private void TickFx(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var f = _active[i];
                f.age += dt;
                float k = Mathf.Clamp01(f.age / f.life);
                f.tick?.Invoke(f, k);
                if (k >= 1f)
                {
                    f.tf.gameObject.SetActive(false);
                    _pool.Push(f);
                    _active.RemoveAt(i);
                }
            }
        }

        // ───────────── 배너

        private enum BannerKind { Wave, Boss, Clear }

        private void ShowBanner(string title, string sub, BannerKind kind)
        {
            EnsureBannerUi();
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(BannerRoutine(title, sub, kind));
        }

        private IEnumerator BannerRoutine(string title, string sub, BannerKind kind)
        {
            Color main = kind == BannerKind.Boss ? new Color(1f, 0.36f, 0.28f) : kind == BannerKind.Clear ? new Color(0.72f, 1f, 0.6f) : new Color(1f, 0.84f, 0.42f);
            _title.text = title; _sub.text = sub;
            bool hasSub = !string.IsNullOrEmpty(sub);
            _sub.gameObject.SetActive(hasSub);
            _title.rectTransform.anchoredPosition = new Vector2(0f, hasSub ? 22f : 0f);   // 글이 하나뿐이면 가운데
            _title.color = main; _sub.color = kind == BannerKind.Boss ? new Color(1f, 0.82f, 0.25f) : new Color(1f, 0.95f, 0.85f);
            _band.color = new Color(0f, 0f, 0f, 0f);
            var line = new Color(main.r, main.g, main.b, 0.9f);
            _lineTop.color = _lineBottom.color = line;
            bool boss = kind == BannerKind.Boss;
            float total = boss ? 2.5f : 1.5f;
            // 보스: 노랑·검정 빗금 띠와 경고 삼각형, 일반: 가는 선
            _lineTop.gameObject.SetActive(!boss); _lineBottom.gameObject.SetActive(!boss);
            _hazardTop.gameObject.SetActive(boss); _hazardBottom.gameObject.SetActive(boss);
            _warnLeft.gameObject.SetActive(boss); _warnRight.gameObject.SetActive(boss);
            _canvas.enabled = true;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                float k = t / total;
                float inK = Mathf.Clamp01(t / 0.28f);                       // 들어오기
                float outK = Mathf.Clamp01((t - (total - 0.45f)) / 0.45f);   // 나가기
                float a = Mathf.SmoothStep(0f, 1f, inK) * (1f - outK);
                _bannerGroup.alpha = a;
                float pop = Mathf.Lerp(1.7f, 1f, 1f - Mathf.Pow(1f - inK, 3f)) * (1f + outK * 0.12f);
                _bannerRoot.localScale = new Vector3(pop, pop, 1f);
                // 가운데에서 양옆으로 펼쳐지는 띠
                float spread = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.35f));
                _lineTop.rectTransform.sizeDelta = _lineBottom.rectTransform.sizeDelta = new Vector2(1800f * spread, 5f);
                if (boss)
                {
                    float w = 1900f * spread;
                    _hazardTop.rectTransform.sizeDelta = _hazardBottom.rectTransform.sizeDelta = new Vector2(w, 26f);
                    float tiles = w / 52f;                                         // 빗금 한 주기가 52px
                    float scroll = t * 0.9f;                                       // 위 띠는 오른쪽, 아래 띠는 왼쪽으로 흐른다
                    _hazardTop.uvRect = new Rect(scroll, 0f, tiles, 1f);
                    _hazardBottom.uvRect = new Rect(-scroll, 0f, tiles, 1f);
                    float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 7f));
                    float sc = 1f + 0.12f * Mathf.Abs(Mathf.Sin(t * 7f));
                    var wc = new Color(1f, 1f, 1f, a * blink);
                    _warnLeft.color = _warnRight.color = wc;
                    _warnLeft.rectTransform.localScale = _warnRight.rectTransform.localScale = new Vector3(sc, sc, 1f);
                }
                _band.color = new Color(0.04f, 0.01f, 0.03f, 0.62f * a);
                // 보스: 붉은 경고 비네트가 두 번 맥동한다
                if (boss) _warn.color = new Color(0.9f, 0.05f, 0.05f, a * (0.25f + 0.3f * Mathf.Abs(Mathf.Sin(t * 5f))));
                yield return null;
            }
            _warn.color = Color.clear;
            _canvas.enabled = false;
            _bannerRoutine = null;
        }

        private void EnsureBannerUi()
        {
            if (_canvas != null) return;
            var go = new GameObject("WaveBannerCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 27;   // 배속 막대(28) 바로 아래, 손패 위
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;

            _warn = NewImage("Warning", root, _vignette, Color.clear);
            Stretch(_warn.rectTransform);

            _bannerRoot = NewRect("Banner", root);
            _bannerRoot.anchorMin = _bannerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _bannerRoot.pivot = new Vector2(0.5f, 0.5f);
            _bannerRoot.sizeDelta = new Vector2(1920f, 300f);
            _bannerRoot.anchoredPosition = new Vector2(0f, 150f);
            _bannerGroup = _bannerRoot.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.interactable = false; _bannerGroup.blocksRaycasts = false;

            _band = NewImage("Band", _bannerRoot, _soft, new Color(0f, 0f, 0f, 0f));
            _band.rectTransform.anchorMin = new Vector2(0f, 0.5f); _band.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _band.rectTransform.sizeDelta = new Vector2(0f, 260f);
            _lineTop = NewImage("LineTop", _bannerRoot, null, Color.white);
            _lineTop.rectTransform.anchorMin = _lineTop.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _lineTop.rectTransform.anchoredPosition = new Vector2(0f, 96f);
            _lineBottom = NewImage("LineBottom", _bannerRoot, null, Color.white);
            _lineBottom.rectTransform.anchorMin = _lineBottom.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _lineBottom.rectTransform.anchoredPosition = new Vector2(0f, -96f);

            // 보스 웨이브용 위험 표시: 빗금 띠 둘, 경고 삼각형 둘
            _stripe = MakeStripe();
            _hazardTop = NewRaw("HazardTop", _bannerRoot, _stripe);
            _hazardTop.rectTransform.anchorMin = _hazardTop.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _hazardTop.rectTransform.anchoredPosition = new Vector2(0f, 102f);
            _hazardBottom = NewRaw("HazardBottom", _bannerRoot, _stripe);
            _hazardBottom.rectTransform.anchorMin = _hazardBottom.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _hazardBottom.rectTransform.anchoredPosition = new Vector2(0f, -102f);
            _warnIcon = MakeWarnIcon();
            _warnLeft = NewImage("WarnLeft", _bannerRoot, _warnIcon, Color.white);
            _warnLeft.rectTransform.anchorMin = _warnLeft.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _warnLeft.rectTransform.sizeDelta = new Vector2(120f, 120f);
            _warnLeft.rectTransform.anchoredPosition = new Vector2(-560f, 8f);
            _warnRight = NewImage("WarnRight", _bannerRoot, _warnIcon, Color.white);
            _warnRight.rectTransform.anchorMin = _warnRight.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _warnRight.rectTransform.sizeDelta = new Vector2(120f, 120f);
            _warnRight.rectTransform.anchoredPosition = new Vector2(560f, 8f);

            _title = NewText("Title", _bannerRoot, 150f, FontStyles.Bold);
            _title.rectTransform.anchorMin = new Vector2(0f, 0.5f); _title.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _title.rectTransform.sizeDelta = new Vector2(0f, 170f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, 22f);
            _title.outlineWidth = 0.28f; _title.outlineColor = new Color32(40, 8, 14, 255);
            _sub = NewText("Sub", _bannerRoot, 54f, FontStyles.Normal);
            _sub.rectTransform.anchorMin = new Vector2(0f, 0.5f); _sub.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _sub.rectTransform.sizeDelta = new Vector2(0f, 70f);
            _sub.rectTransform.anchoredPosition = new Vector2(0f, -66f);
            _sub.outlineWidth = 0.2f; _sub.outlineColor = new Color32(30, 8, 12, 255);
            _canvas.enabled = false;
        }

        private static RawImage NewRaw(string name, Transform parent, Texture texture)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = texture; raw.raycastTarget = false; raw.color = Color.white;
            return raw;
        }

        private TMP_Text NewText(string name, Transform parent, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_tmpFont == null) _tmpFont = UiFontOverride.Current != null ? UiFontOverride.Current : FindKoreanFont();
            if (_tmpFont != null) t.font = _tmpFont;
            t.fontSize = size; t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static TMP_FontAsset FindKoreanFont()
        {
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters("용사들이쳐들어온다보스웨이브마왕군의승리", out _, true, true)) return font;
            return null;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ───────────── 그림·글꼴

        private void EnsureAssets()
        {
            if (_root != null) return;
            _root = new GameObject("BattleFx").transform;
            _root.SetParent(transform, false);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _glow = MakeGlow();
            _ring = MakeRing();
            _star = MakeStar();
            _soft = MakeBand();
            _vignette = MakeVignette();
            if (_bootstrap != null && _bootstrap.Config != null) _cell = Mathf.Max(0.1f, _bootstrap.Config.CellWorldSize);
        }

        private static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        private static Sprite MakeGlow()
        {
            const int n = 64;
            var tex = NewTex(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        private static Sprite MakeRing()
        {
            const int n = 96;
            var tex = NewTex(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01((1f - d) * 30f) * Mathf.Clamp01((d - 0.82f) * 16f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        private static Sprite MakeStar()
        {
            const int n = 48;
            var tex = NewTex(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - n * 0.5f) / (n * 0.5f), dy = (y + 0.5f - n * 0.5f) / (n * 0.5f);
                    // 네 꼭짓점 별(아스트로이드 모양)
                    float v = Mathf.Pow(Mathf.Abs(dx), 0.6f) + Mathf.Pow(Mathf.Abs(dy), 0.6f);
                    float a = Mathf.Clamp01((1f - v) * 6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        /// <summary>양옆이 부드럽게 사라지는 가로 띠.</summary>
        private static Sprite MakeBand()
        {
            const int w = 128, h = 32;
            var tex = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = Mathf.Abs((x + 0.5f) / w * 2f - 1f), fy = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                    float a = Mathf.Clamp01(1f - Mathf.Pow(fx, 3f)) * Mathf.Clamp01(1f - Mathf.Pow(fy, 6f) * 0.95f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>노랑·검정 빗금(가로로 반복되는 그림). 한 주기가 가로 32칸이라 uvRect 를 옮기면 흐른다.</summary>
        private static Texture2D MakeStripe()
        {
            const int w = 32, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool yellow = ((x + y * 2) % w) < w / 2;
                    tex.SetPixel(x, y, yellow ? new Color(1f, 0.82f, 0.12f, 1f) : new Color(0.07f, 0.04f, 0.04f, 1f));
                }
            tex.Apply();
            return tex;
        }

        /// <summary>경고 삼각형(노란 바탕에 검은 테두리와 느낌표).</summary>
        private static Sprite MakeWarnIcon()
        {
            const int n = 96;
            var tex = NewTex(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float px = (x + 0.5f) / n, py = (y + 0.5f) / n;           // 0~1, 위가 큼
                    // 위가 뾰족한 정삼각형: 아래 변 y=0.1, 꼭짓점 y=0.92
                    float top = 0.92f, bottom = 0.1f;
                    float halfAt = (top - py) / (top - bottom) * 0.46f;       // 그 높이에서의 반폭
                    float d = Mathf.Min(halfAt - Mathf.Abs(px - 0.5f), Mathf.Min(py - bottom, top - py)) * n;
                    float cover = Mathf.Clamp01(d + 0.5f);
                    if (cover <= 0f) { tex.SetPixel(x, y, Color.clear); continue; }
                    bool border = d < 5f;
                    Color c = border ? new Color(0.08f, 0.04f, 0.04f) : new Color(1f, 0.82f, 0.1f);
                    // 느낌표(막대 + 점)
                    bool bar = Mathf.Abs(px - 0.5f) < 0.045f && py > 0.34f && py < 0.66f;
                    bool dot = Vector2.Distance(new Vector2(px, py), new Vector2(0.5f, 0.24f)) < 0.05f;
                    if (!border && (bar || dot)) c = new Color(0.08f, 0.04f, 0.04f);
                    c.a = cover;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeVignette()
        {
            const int n = 64;
            var tex = NewTex(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - (n - 1) * 0.5f) / (n * 0.5f), dy = (y - (n - 1) * 0.5f) / (n * 0.5f);
                    float d = Mathf.Sqrt(dx * dx * 0.9f + dy * dy * 1.1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.5f) / 0.6f))));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
