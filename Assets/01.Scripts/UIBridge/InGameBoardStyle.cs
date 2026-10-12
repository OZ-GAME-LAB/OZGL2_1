using System.Collections.Generic;
using OZGL2.Grid;
using OZGL2.InGame;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 배치판을 한눈에 또렷하게 보이도록 꾸민다(귀여운 오토배틀러의 타일 판처럼).
    /// - 칸: 바닥이 있는 칸마다 어두운 외곽선·윗면 하이라이트·아랫면 그늘이 있는 둥근 모래색 타일을 깔고, 타일 사이에 틈을 둬서 칸이 한눈에 구분된다(발판이 놓인 칸은 조금 더 짙은 색).
    /// - 마왕군: 배치 단계와 전투 단계 모두 조금 크게(기본 1.3배) 키워 잘 보이게 한다.
    /// - 그림자: 모든 유닛 발밑에 부드러운 타원 그림자를 깔아 바닥에 서 있는 느낌을 준다.
    /// - 모든 칸을 따로따로 떨어진 블록으로 보이게 한다: 원래 바닥 타일·테두리를 가리고, 칸을 메우던 배치 표시(점유·미리보기 색판)를 타일 크기로 줄이고, 잠긴 칸도 흐린 블록으로 그린다.
    /// 팀 파일(그리드·배치 화면)은 수정하지 않고, 이미 만들어진 칸(Surface_x_y)·유닛 그림(Preview_*)·전투 유닛(UnitRegistry) 위에 런타임으로 덧붙인다.
    /// InGamePrototypeBootstrap 이 있는 씬이 열리면 자동으로 붙는다.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class InGameBoardStyle : MonoBehaviour
    {
        private const string PreviewPrefix = "Preview_";
        private const string DraggedName = "DraggedUnit";

        [SerializeField, Range(1f, 1.8f), Tooltip("마왕군을 얼마나 키울지(배치·전투 공통). 1이면 원래 크기")] private float _demonScale = 1.3f;
        [SerializeField, Range(1f, 1.6f), Tooltip("용사를 얼마나 키울지. 1이면 원래 크기")] private float _heroScale = 1.15f;
        [SerializeField, Tooltip("유닛 발밑에 팀 색 고리(마왕군 보라 · 용사 빨강)를 깐다 — 배경 위에서 어느 쪽 유닛인지 한눈에 보인다")] private bool _teamRings = true;
        [SerializeField] private bool _cellTiles = true;
        [SerializeField, Range(0.6f, 1f), Tooltip("칸 타일이 칸 안에서 차지하는 비율(나머지는 칸 사이 틈). 작을수록 칸이 띄엄띄엄 보인다")] private float _tileFill = 0.78f;
        [SerializeField] private Color _tileColor = new Color(0.93f, 0.85f, 0.67f, 1f);
        [SerializeField] private Color _platformTileColor = new Color(0.82f, 0.68f, 0.46f, 1f);

        private InGamePrototypeBootstrap _bootstrap;
        private GridManager _grid;
        private Transform _boardRoot;
        private SpriteRenderer[,] _plates, _shadowsUnder;
        private readonly List<SpriteRenderer> _originalTiles = new List<SpriteRenderer>();
        private Transform _prepRoot;
        private float _cell = 1f;
        private float _next;

        private sealed class Follow
        {
            public Transform target;
            public GameObject root;
            public SpriteRenderer shadow, teamRing, ring, fill;
            public TextMesh number;
            public int stars;
        }

        private readonly Dictionary<int, Follow> _follows = new Dictionary<int, Follow>();
        private readonly HashSet<int> _scaled = new HashSet<int>();

        // 배치 연출(찌그러졌다 튀어 오름) — 놓거나 옮기거나 합성한 유닛에 한 번씩
        private sealed class ActorFx { public Transform tf; public Vector3 baseScale; public float t = -1f; public float strength; }
        private readonly Dictionary<int, ActorFx> _actorFx = new Dictionary<int, ActorFx>();
        private readonly Dictionary<string, Vector3> _lastPos = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, int> _lastStars = new Dictionary<string, int>();
        private float _fxGrace = -1f;
        private const float PopSeconds = 0.4f;
        private readonly List<int> _drop = new List<int>();
        private Transform _followRoot;
        private Sprite _plateSprite, _discSprite, _softSprite, _ringSprite, _outlineSprite, _outlineGlowSprite, _fillSprite;

        /// <summary>타일과 같은 모서리의 칸 테두리·빛무리·받침·채움 그림(다른 연출이 같이 쓴다). 한 번만 만든다.</summary>
        private static Sprite _sharedThin, _sharedGlow, _sharedBase, _sharedFill;
        public static Sprite SharedThin => _sharedThin != null ? _sharedThin : (_sharedThin = MakeOutline(0));
        public static Sprite SharedGlow => _sharedGlow != null ? _sharedGlow : (_sharedGlow = MakeOutline(1));
        public static Sprite SharedBase => _sharedBase != null ? _sharedBase : (_sharedBase = MakeOutline(2));
        public static Sprite SharedFill => _sharedFill != null ? _sharedFill : (_sharedFill = MakeRoundFill());
        /// <summary>칸 타일이 칸 안에서 차지하는 비율(칸 테두리 크기를 맞출 때 읽는다).</summary>
        public static float CurrentTileFill { get; private set; } = 0.78f;

        private Font _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        private static void Install()
        {
            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (bootstrap == null || bootstrap.GetComponent<InGameBoardStyle>() != null) return;
            bootstrap.gameObject.AddComponent<InGameBoardStyle>();
        }

        private void Awake()
        {
            _bootstrap = GetComponent<InGamePrototypeBootstrap>();
            CurrentTileFill = _tileFill;
        }

        private void OnDestroy()
        {
            if (_grid != null) { _grid.LayoutChanged -= OnLayoutChanged; _grid.Changed -= OnGridChanged; }
            if (_followRoot != null) Destroy(_followRoot.gameObject);
        }

        private void OnLayoutChanged()
        {
            _next = 0f;
            HideOriginalTiles();   // 그리드가 칸을 다시 그린 같은 프레임에 바로 다시 가린다
        }

        private void OnGridChanged() => ShrinkPlacementOverlays();

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + 0.2f;
                Scan();
            }
            UpdateFollows();
        }

        // ───────────── 찾기·갱신

        private void Scan()
        {
            if (_bootstrap == null) return;
            var grid = _bootstrap.GridSession != null ? _bootstrap.GridSession.Grid : null;
            if (grid != _grid)
            {
                if (_grid != null) { _grid.LayoutChanged -= OnLayoutChanged; _grid.Changed -= OnGridChanged; }
                _grid = grid;
                if (_grid != null) { _grid.LayoutChanged += OnLayoutChanged; _grid.Changed += OnGridChanged; }
                _plates = null; _shadowsUnder = null; _boardRoot = null;
            }
            EnsureSprites();
            if (_cellTiles && _grid != null) RefreshTiles();
            ScanPreviewActors();
            ShrinkPlacementOverlays();
            ScanCombatUnits();
        }

        // ───────────── 칸 타일

        private void RefreshTiles()
        {
            if (_boardRoot == null)
            {
                var root = GameObject.Find("GridWorldBoard");
                if (root == null) return;
                _boardRoot = root.transform;
                var g00 = _boardRoot.Find("Surface_0_0");
                var g10 = _boardRoot.Find("Surface_1_0");
                if (g00 == null || g10 == null) { _boardRoot = null; return; }
                _cell = Vector3.Distance(g00.position, g10.position);
                int w = _grid.Definition.MaximumSize.x, h = _grid.Definition.MaximumSize.y;
                _plates = new SpriteRenderer[w, h];
                _shadowsUnder = new SpriteRenderer[w, h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var surface = _boardRoot.Find("Surface_" + x + "_" + y);
                        if (surface == null) continue;
                        _shadowsUnder[x, y] = NewSprite("CellShadow", surface, _plateSprite, -98);
                        _shadowsUnder[x, y].color = new Color(0f, 0f, 0f, 0.38f);
                        _shadowsUnder[x, y].transform.localPosition = new Vector3(0.025f * _cell, -0.04f * _cell, 0f);
                        _plates[x, y] = NewSprite("CellPlate", surface, _plateSprite, -97);
                        float s = _cell * _tileFill;
                        _plates[x, y].transform.localScale = new Vector3(s, s, 1f);
                        _shadowsUnder[x, y].transform.localScale = new Vector3(s, s, 1f);
                        // 원래 바닥 타일·테두리·오염 그림(칸이 서로 이어져 보이게 하던 것)은 가린다
                        foreach (Transform child in surface)
                        {
                            if (child.name == "CellPlate" || child.name == "CellShadow") continue;
                            var sr = child.GetComponent<SpriteRenderer>();
                            if (sr != null) _originalTiles.Add(sr);
                        }
                    }
                HideOriginalTiles();
            }

            for (int y = 0; y < _plates.GetLength(1); y++)
                for (int x = 0; x < _plates.GetLength(0); x++)
                {
                    var plate = _plates[x, y];
                    if (plate == null) continue;
                    var cell = new Vector2Int(x, y);
                    bool floor = _grid.HasFloor(cell);
                    // 바닥이 없는(잠긴) 칸도 흐린 블록으로 그려서, 칸이 이어진 격자가 아니라 따로 떨어진 블록으로 보이게 한다
                    plate.enabled = true;
                    _shadowsUnder[x, y].enabled = floor;
                    plate.color = floor ? (_grid.GetBlockAt(cell) != null ? _platformTileColor : _tileColor) : new Color(0.18f, 0.24f, 0.15f, 0.34f);
                }
        }

        private void HideOriginalTiles()
        {
            foreach (var sr in _originalTiles) if (sr != null && sr.enabled) sr.enabled = false;
        }

        /// <summary>
        /// 배치 화면이 칸 가장자리까지 채워 그리던 표시(점유·미리보기 사각형, 가장자리 테두리)를 우리 타일 크기로 줄이고 테두리는 숨긴다.
        /// 그대로 두면 사각형이 칸 사이의 틈을 메워서 칸들이 다시 붙어 보인다.
        /// </summary>
        private void ShrinkPlacementOverlays()
        {
            if (_prepRoot == null)
            {
                var go = GameObject.Find("PreparationVisuals");
                if (go == null) return;
                _prepRoot = go.transform;
            }
            // 칸 가장자리까지 채워 그리던 반투명 사각형만 타일 크기로 줄인다. 테두리선은 건드리지 않는다(원래 모양 그대로).
            float limit = _cell * _tileFill;
            foreach (Transform child in _prepRoot)
            {
                if (child.name != "PlacementOverlay") continue;
                var s = child.localScale;
                if (s.x > limit) child.localScale = new Vector3(limit, limit, 1f);
            }
        }

        // ───────────── 배치 화면 유닛

        private void ScanPreviewActors()
        {
            var root = GameObject.Find("PreparationVisuals");
            if (root == null) return;
            foreach (Transform actor in root.transform)
            {
                bool preview = actor.name.StartsWith(PreviewPrefix), dragged = actor.name == DraggedName;
                if (!preview && !dragged) continue;
                int id = actor.GetInstanceID();
                if (_scaled.Add(id)) actor.localScale *= _demonScale;   // 크게
                if (preview)
                {
                    int stars = GetStars(actor);
                    EnsureFollow(id, actor, false, stars, 0.78f, true);
                    DetectPlacement(id, actor, stars);
                }
                // 예전에 숨겨 둔 머리 위 별 배지가 있으면 다시 켠다(성급 동그라미는 없앴다)
                var badge = actor.Find("StarBadge");
                if (badge != null && !badge.gameObject.activeSelf) badge.gameObject.SetActive(true);
            }
        }

        private static int GetStars(Transform actor)
        {
            var label = actor.GetComponentInChildren<TextMesh>(true);
            if (label == null || string.IsNullOrEmpty(label.text)) return 1;
            for (int i = label.text.Length - 1; i >= 0; i--)
                if (char.IsDigit(label.text[i])) return Mathf.Clamp(label.text[i] - '0', 1, 3);
            return 1;
        }

        // ───────────── 전투 유닛

        private void ScanCombatUnits()
        {
            foreach (var unit in UnitRegistry.GetUnits(UnitSide.DemonArmy))
            {
                if (unit == null || unit.currentState == UnitState.Dead) continue;
                int id = unit.transform.GetInstanceID();
                if (_scaled.Add(id)) unit.transform.localScale *= _demonScale;
                EnsureFollow(id, unit.transform, false, 0, 0.78f, true);
            }
            foreach (var unit in UnitRegistry.GetUnits(UnitSide.Hero))
            {
                if (unit == null || unit.currentState == UnitState.Dead) continue;
                int hid = unit.transform.GetInstanceID();
                if (_scaled.Add(hid)) unit.transform.localScale *= _heroScale;
                EnsureFollow(hid, unit.transform, false, 0, 0.62f, false);
            }
        }

        // ───────────── 그림자·성급 동그라미(유닛을 따라다닌다)

        private void EnsureFollow(int id, Transform target, bool starBadge, int stars, float width, bool demon)
        {
            if (_followRoot == null)
            {
                _followRoot = new GameObject("BoardStyleFollowers").transform;
                _followRoot.SetParent(transform, false);
            }
            if (!_follows.TryGetValue(id, out var follow) || follow.root == null)
            {
                follow = new Follow { target = target, root = new GameObject("F_" + target.name) };
                follow.root.transform.SetParent(_followRoot, false);
                follow.shadow = NewSprite("Shadow", follow.root.transform, _softSprite, -55);
                follow.shadow.color = new Color(0f, 0f, 0f, 0.5f);
                follow.shadow.transform.localScale = new Vector3(width * _cell * (_demonScale > 1f ? 1f : 1f), width * 0.42f * _cell, 1f);
                follow.shadow.transform.localPosition = new Vector3(0f, -0.04f * _cell, 0f);
                if (_teamRings)
                {
                    // 팀 색 고리(타원 테두리) — 그림자 바로 위, 유닛 아래
                    follow.teamRing = NewSprite("TeamRing", follow.root.transform, _ringSprite, -54);
                    follow.teamRing.color = demon ? new Color(0.74f, 0.42f, 1f, 0.95f) : new Color(1f, 0.33f, 0.27f, 0.9f);
                    follow.teamRing.transform.localScale = new Vector3(width * 1.12f * _cell, width * 0.5f * _cell, 1f);
                    follow.teamRing.transform.localPosition = new Vector3(0f, -0.04f * _cell, 0f);
                }
                if (starBadge)
                {
                    float d = 0.32f * _cell;
                    follow.ring = NewSprite("BadgeRing", follow.root.transform, _discSprite, 560);
                    follow.fill = NewSprite("BadgeFill", follow.root.transform, _discSprite, 561);
                    follow.ring.color = new Color(0.1f, 0.06f, 0.08f, 1f);
                    follow.ring.transform.localScale = Vector3.one * d;
                    follow.ring.transform.localPosition = new Vector3(-0.36f * _cell, -0.30f * _cell, 0f);
                    follow.fill.transform.localScale = Vector3.one * d * 0.82f;
                    follow.fill.transform.localPosition = follow.ring.transform.localPosition;
                    var go = new GameObject("Number");
                    go.transform.SetParent(follow.root.transform, false);
                    go.transform.localPosition = follow.ring.transform.localPosition + new Vector3(0f, 0.005f * _cell, -0.01f);
                    follow.number = go.AddComponent<TextMesh>();
                    follow.number.font = _font;
                    follow.number.anchor = TextAnchor.MiddleCenter;
                    follow.number.alignment = TextAlignment.Center;
                    follow.number.fontSize = 64;
                    follow.number.characterSize = 0.0105f * _cell;
                    follow.number.fontStyle = FontStyle.Bold;
                    go.GetComponent<MeshRenderer>().sortingOrder = 562;
                    follow.stars = 0;
                }
                _follows[id] = follow;
            }
            if (starBadge && follow.number != null && follow.stars != stars)
            {
                follow.stars = stars;
                ApplyStars(follow, stars);
            }
        }

        private static void ApplyStars(Follow f, int stars)
        {
            Color fill = stars >= 3 ? new Color(0.66f, 0.34f, 0.96f) : stars == 2 ? new Color(0.28f, 0.58f, 1f) : new Color(0.97f, 0.96f, 0.92f);
            f.fill.color = fill;
            f.number.text = stars.ToString();
            f.number.color = stars >= 2 ? Color.white : new Color(0.16f, 0.1f, 0.14f);
        }

        private void UpdateFollows()
        {
            TickActorFx(Time.unscaledDeltaTime);
            _drop.Clear();
            foreach (var pair in _follows)
            {
                var f = pair.Value;
                if (f.target == null || f.root == null) { _drop.Add(pair.Key); continue; }
                bool on = f.target.gameObject.activeInHierarchy;
                if (f.root.activeSelf != on) f.root.SetActive(on);
                if (on) f.root.transform.position = f.target.position;
            }
            foreach (var key in _drop)
            {
                if (_follows[key].root != null) Destroy(_follows[key].root);
                _follows.Remove(key);
            }
        }

        // ───────────── 그림 만들기

        private static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>
        /// 유닛을 새로 놓거나 다른 칸으로 옮기면 찌그러졌다 튀어 오르며 먼지가 일고, 합성해서 성급이 오르면 더 크게 튀며 별이 터진다.
        /// 장면이 열린 직후(저장된 배치를 되살리는 순간)에는 연출하지 않는다.
        /// </summary>
        private void DetectPlacement(int id, Transform actor, int stars)
        {
            string key = actor.name;
            if (_fxGrace < 0f) _fxGrace = Time.unscaledTime + 1.5f;
            bool fresh = !_lastPos.TryGetValue(key, out var lastPos);
            bool moved = !fresh && Vector3.Distance(lastPos, actor.position) > 0.4f * _cell;
            bool starUp = _lastStars.TryGetValue(key, out int lastStars) && stars > lastStars;
            _lastPos[key] = actor.position;
            _lastStars[key] = stars;
            if (Time.unscaledTime < _fxGrace) return;
            if (!fresh && !moved && !starUp) return;

            if (!_actorFx.TryGetValue(id, out var fx) || fx.tf == null) { fx = new ActorFx { tf = actor, baseScale = actor.localScale }; _actorFx[id] = fx; }
            if (fx.t < 0f) fx.baseScale = actor.localScale;   // 연출 중이 아닐 때의 크기가 기준
            fx.t = 0f;
            fx.strength = starUp ? 1.6f : 1f;
            var fxHost = InGameBattleFx.Instance;
            if (fxHost != null)
            {
                fxHost.Dust(actor.position + new Vector3(0f, -0.04f * _cell, 0f), _cell);
                if (starUp) fxHost.StarBurst(actor.position, 1f);
            }
        }

        private void TickActorFx(float dt)
        {
            if (_actorFx.Count == 0) return;
            List<int> done = null;
            foreach (var pair in _actorFx)
            {
                var fx = pair.Value;
                if (fx.tf == null) { (done ?? (done = new List<int>())).Add(pair.Key); continue; }
                if (fx.t < 0f) continue;
                fx.t += dt;
                float k = fx.t / PopSeconds;
                if (k >= 1f)
                {
                    fx.tf.localScale = fx.baseScale;
                    fx.t = -1f;
                    continue;
                }
                // 찌그러졌다(옆으로 넓고 납작) 위로 늘어났다가(길쭉) 자리를 잡는다
                float wave = Mathf.Sin(k * Mathf.PI * 2.6f) * Mathf.Exp(-k * 3.2f) * fx.strength;
                fx.tf.localScale = new Vector3(fx.baseScale.x * (1f + 0.16f * -wave), fx.baseScale.y * (1f + 0.22f * wave), fx.baseScale.z);
            }
            if (done != null) foreach (var key in done) _actorFx.Remove(key);
        }

        private void EnsureSprites()
        {
            if (_plateSprite != null) return;
            _plateSprite = MakePlate();
            _discSprite = MakeDisc();
            _softSprite = MakeSoft();
            _ringSprite = MakeRing();
            _outlineSprite = SharedThin;
            _fillSprite = SharedFill;
            _outlineGlowSprite = SharedGlow;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>
        /// 가는 어두운 외곽선 · 그 안쪽의 은은한 윗면 하이라이트/아랫면 그늘 · 부드러운 그라데이션이 있는 둥근 타일(128칸 = 월드 1). 색은 SpriteRenderer.color 가 곱한다.
        /// 모서리까지 한 줄 두께로 일정하도록 둥근 사각형의 거리장(SDF)으로 그리고, 외곽선과 안쪽 색은 단계 없이 부드럽게 이어 붙인다.
        /// </summary>
        private static Sprite MakePlate()
        {
            const int n = 128;
            const float radius = 22f, outline = 2.4f, rim = 5.2f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float half = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // 둥근 사각형의 부호 있는 거리: 안쪽이 양수(가장자리에서의 깊이)
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float depth = -outside;
                    float coverage = Mathf.Clamp01(depth + 0.5f);          // 가장자리 안티앨리어싱
                    if (coverage <= 0f) { tex.SetPixel(x, y, Color.clear); continue; }

                    float v = y / (float)(n - 1);                           // 0 아래 → 1 위
                    float shade = Mathf.Lerp(0.88f, 1.0f, v);
                    float noise = (Hash(x / 5, y / 5) - 0.5f) * 0.03f;     // 아주 은은한 결
                    Color body = new Color(shade + noise, shade + noise, shade + noise);

                    // 안쪽 테두리 띠: 위쪽은 살짝 밝게, 아래쪽은 살짝 어둡게 (두께 outline~rim)
                    float rimK = Mathf.Clamp01((depth - outline) / (rim - outline));            // 0 외곽선 쪽 → 1 안쪽
                    float bevel = Mathf.Lerp(v > 0.5f ? 1.1f : 0.86f, 1f, rimK);
                    body = new Color(body.r * bevel, body.g * bevel, body.b * bevel);

                    // 가는 어두운 외곽선 → 안쪽 색으로 부드럽게 이어진다
                    Color line = new Color(0.2f, 0.12f, 0.08f);
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((depth - outline * 0.55f) / (outline * 0.9f)));
                    Color c = Color.Lerp(line, body, t);
                    c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b);
                    c.a = coverage;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        /// <summary>
        /// 칸 테두리용 그림: 타일과 같은 둥근 사각형의 가는 선(고리). glow 면 선 주변이 은은하게 번지는 빛무리.
        /// 그림은 타일 크기를 월드 1로 보고, 빛무리가 번질 바깥 여백을 두려고 160칸 그림 가운데에 128칸 사각형을 놓았다.
        /// </summary>
        private static Sprite MakeOutline(int kind)
        {
            const int n = 160;
            const float radius = 22f, inner = 128f * 0.5f;
            float thick = kind == 2 ? 5.6f : 3.2f;         // 선 두께(128칸 기준) — 받침(2)은 색 선보다 조금 두꺼워 안쪽 가장자리에 어두운 줄이 남는다
            bool glow = kind == 1;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float half = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (inner - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (inner - radius);
                    float dist = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;   // 안쪽이 음수
                    float depth = -dist;                                                    // 안쪽이 양수
                    float a;
                    if (!glow) a = Mathf.Clamp01(depth + 0.5f) * Mathf.Clamp01(thick - depth + 0.5f);   // 가장자리에서 안쪽으로 thick 만큼의 선
                    else
                    {
                        float d = Mathf.Abs(depth - thick * 0.5f) / 7.5f;                  // 선 중심에서 멀어질수록 옅어진다
                        a = Mathf.Exp(-d * d) * 0.55f;
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 128f);
        }

        /// <summary>배치 표시(점유·미리보기)를 칠하는 둥근 사각형 — 타일(MakePlate)과 같은 모서리 반경이라 테두리와 딱 맞는다.</summary>
        private static Sprite MakeRoundFill()
        {
            const int n = 128;
            const float radius = 22f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float half = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float dist = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - dist)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        private static Sprite MakeDisc()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        /// <summary>속이 빈 타원 고리(바깥은 또렷하고 안쪽으로 갈수록 투명). 가로·세로 비율은 크기로 정한다.</summary>
        private static Sprite MakeRing()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float outer = Mathf.Clamp01((1f - d) * 40f);                 // 바깥 가장자리는 또렷하게
                    float band = Mathf.Clamp01((d - 0.78f) * 18f);                // 안쪽은 비운다(고리 두께 약 0.2)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, outer * band));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        private static Sprite MakeSoft()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * Mathf.Sqrt(a)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
