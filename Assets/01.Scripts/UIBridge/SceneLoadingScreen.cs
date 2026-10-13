using System.Collections;
using OZGL2.InGame;
using OZGL2.Stage;
using OZGL2.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 씬을 넘어갈 때 보여 주는 진짜 로딩 화면 — 타이틀 → 로비, 로비 → 인게임, 인게임 → 로비 모두 이 화면을 거친다(UISceneNavigator·TitleScreen·포기/나가기가 부른다).
    /// 지금 화면 위로 어둡게 덮은 뒤 씬을 비동기로 불러오는 동안 실제 진행도를 보여 주고, 새 씬이 열린 뒤에도 인게임이 준비될 때까지 기다렸다가 서서히 걷힌다.
    /// - 게임 로고 + 금빛 테두리의 진행 막대(불러온 만큼 차오르고, 뒤따라오는 밝은 잔상) + 막대 위를 달리는 실제 마왕(StoryCast 프리팹을 실시간으로 그림)
    /// - 아래에는 「불러오는 중…」과 퍼센트, 그리고 게임 팁이 몇 초마다 바뀐다
    /// - 막대는 실제 불러오기 진행도를 따라간다(너무 빨리 끝나 화면이 번쩍이지 않게 아주 짧은 최소 시간만 둔다)
    /// 팀 UI·씬은 건드리지 않고 DontDestroyOnLoad 캔버스를 직접 만든다. 마왕 그림이나 로고를 못 찾으면 그 부분만 빼고 보여 준다.
    /// </summary>
    public sealed class SceneLoadingScreen : MonoBehaviour
    {
        private static readonly string[] Tips =
        {
            "같은 유닛을 합성하면 성급이 올라 더 강해져요.",
            "보스 웨이브를 넘기면 증강을 고를 수 있어요.",
            "전투 중에 마우스 휠로 화면을 확대하고, 우클릭으로 옮길 수 있어요.",
            "Space 키로 전투를 잠깐 멈출 수 있어요.",
            "처음 만난 용사는 도감에 기록돼요.",
            "마왕이 레벨업하면 스킬 포인트를 얻어요.",
            "스킬은 전투 중에 직접 눌러 쓸 수 있어요.",
            "자동 카메라는 배속 막대 옆 단추로 켜고 끌 수 있어요.",
        };

        private const float RunSpeed = 0.55f;       // 막대가 차는 최고 속도(초당 비율) — 0%에서 100%까지 마왕이 달리는 데 최소 약 1.8초
        private const float MinSeconds = 0.7f;      // 번쩍임 방지용 최소 표시 시간(진행 막대는 이 시간과 상관없이 실제 진행도를 따른다)
        private static Sprite _logoCache;
        private static bool _busy;
        private const float BarWidth = 1000f, BarHeight = 30f;
        private const float KingLift = 128f;        // 마왕 그림의 가운데가 막대 위로 떠 있는 높이(발이 막대에 닿게)
        private static readonly Color Gold = new Color(1f, 0.82f, 0.4f, 1f);
        private static readonly Color Cream = new Color(1f, 0.95f, 0.84f, 1f);

        private string _target;
        private Sprite _logo;
        private Canvas _canvas;
        private RectTransform _root, _king, _fillRt, _trailRt, _glowRt;
        private Image _bg, _fill, _trail, _glow;
        private CanvasGroup _group;
        private TMP_Text _label, _tip, _pct;
        private RectTransform _pctRt;
        private readonly System.Collections.Generic.List<(RectTransform rt, Image img, float born)> _dust = new System.Collections.Generic.List<(RectTransform, Image, float)>();
        private float _nextDust;
        private TMP_FontAsset _font;
        private DemonKingPortrait _portrait;
        private Sprite _pill, _soft, _vignette;
        private float _shown, _real;
        private bool _done;

        /// <summary>로딩 화면을 띄우고 target 씬을 불러온다. 화면은 이미 검게 덮인 뒤에 부른다.</summary>
        public static void Load(string target, Sprite logo)
        {
            if (_busy) return;
            if (logo != null) _logoCache = logo;     // 타이틀에서 한 번 받아 두면 로비·인게임 사이 이동에서도 로고를 쓴다
            _busy = true;
            var go = new GameObject("SceneLoadingScreen");
            DontDestroyOnLoad(go);
            var screen = go.AddComponent<SceneLoadingScreen>();
            screen._target = target;
            screen._logo = logo != null ? logo : _logoCache;
        }

        private void Start()
        {
            _font = UiFontOverride.Current != null ? UiFontOverride.Current : FindFont();
            Build();
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float start = Time.unscaledTime;
            var tipTimer = 0f;
            _tip.text = Tips[Random.Range(0, Tips.Length)];
            // 지금 보이는 화면 위로 어둡게 덮이면서 그림들이 서서히 켜진다
            _bg.color = new Color(0.02f, 0.01f, 0.04f, 0f);
            for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.22f);
                _bg.color = new Color(0.02f, 0.01f, 0.04f, k);
                _group.alpha = k;
                yield return null;
            }
            _bg.color = new Color(0.02f, 0.01f, 0.04f, 1f);
            _group.alpha = 1f;

            var op = SceneManager.LoadSceneAsync(_target);
            if (op == null) { _busy = false; SceneManager.LoadScene(_target); yield break; }
            op.allowSceneActivation = false;

            int tipIndex = System.Array.IndexOf(Tips, _tip.text);
            while (true)
            {
                _real = Mathf.Clamp01(op.progress / 0.9f);                       // 활성화 전에는 0.9 에서 멈춘다
                _shown = Mathf.MoveTowards(_shown, _real, Time.unscaledDeltaTime * RunSpeed);   // 실제 진행도를 따라가되 마왕이 달리는 모습이 보이게 천천히(앞지르지 않는다)
                Visuals();

                tipTimer += Time.unscaledDeltaTime;
                if (tipTimer > 3.2f)
                {
                    tipTimer = 0f;
                    tipIndex = (tipIndex + 1) % Tips.Length;
                    _tip.text = Tips[tipIndex];
                }
                if (_real >= 1f && _shown >= 0.999f && Time.unscaledTime - start >= MinSeconds) break;
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.25f);                      // 가득 찬 막대를 잠깐 보여 준다
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;
            // 인게임은 씬이 열린 뒤에도 스테이지를 준비하는 시간이 있다 — 준비될 때까지(최대 4초) 로딩 화면을 유지한다
            _label.text = "준비 중...";
            for (float wait = 0f; wait < 4f; wait += Time.unscaledDeltaTime)
            {
                var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
                if (bootstrap == null) break;
                var stage = bootstrap.Stage;
                if (stage != null && stage.State != eStageState.IDLE && stage.State != eStageState.INITIALIZING) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.1f);

            // 새 씬이 열린 뒤 로딩 화면을 서서히 걷는다
            _done = true;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
                _group.alpha = 1f - k;
                _bg.color = new Color(0.02f, 0.01f, 0.04f, 1f - k);
                yield return null;
            }
            _busy = false;
            Destroy(gameObject);
        }

        private void OnDisable() { if (_done) _busy = false; }

        private void Update()
        {
            if (_label == null || _done) return;
            if (_shown >= 0.999f && _real >= 1f) return;      // 다 불러온 뒤에는 「준비 중...」 글자를 그대로 둔다
            int dots = Mathf.FloorToInt(Time.unscaledTime * 2.5f) % 4;
            _label.text = "불러오는 중" + new string('.', dots) + "   ";
        }

        private void Visuals()
        {
            float w = BarWidth * Mathf.Clamp01(_shown);
            _fillRt.sizeDelta = new Vector2(w, _fillRt.sizeDelta.y);
            // 잔상: 채움보다 살짝 앞서 밝게 번졌다가 따라온다
            _trailRt.sizeDelta = new Vector2(Mathf.Min(BarWidth, w + 26f), _trailRt.sizeDelta.y);
            _glowRt.anchoredPosition = new Vector2(w, 0f);
            _glow.color = new Color(1f, 0.85f, 0.45f, (_shown > 0.01f && _shown < 0.995f ? 0.8f : 0f));
            // 마왕은 막대를 따라 0%에서 100%까지 달린다. 머리 위 숫자가 함께 오른다
            float kx = -BarWidth * 0.5f + w;
            if (_king != null) _king.anchoredPosition = new Vector2(kx, BarHeight * 0.5f + KingLift + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f)) * 6f);
            if (_pctRt != null)
            {
                _pctRt.anchoredPosition = new Vector2(Mathf.Clamp(kx, -BarWidth * 0.5f + 20f, BarWidth * 0.5f - 20f), BarHeight * 0.5f + KingLift + 150f);
                _pct.text = Mathf.RoundToInt(Mathf.Clamp01(_shown) * 100f) + "%";
            }
            // 달리는 동안 발밑에서 먼지가 인다
            if (_king != null && _shown > 0.005f && _shown < 0.999f && Time.unscaledTime >= _nextDust)
            {
                _nextDust = Time.unscaledTime + 0.09f;
                var d = NewImage("Dust", _fillRt.parent, _soft, new Color(0.8f, 0.7f, 0.6f, 0f));
                d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                d.rectTransform.sizeDelta = new Vector2(34f, 34f);
                d.rectTransform.anchoredPosition = new Vector2(kx - 36f, BarHeight * 0.5f + 12f);
                d.rectTransform.SetSiblingIndex(_king.GetSiblingIndex());
                _dust.Add((d.rectTransform, d, Time.unscaledTime));
            }
            for (int i = _dust.Count - 1; i >= 0; i--)
            {
                var (drt, dimg, born) = _dust[i];
                float k = (Time.unscaledTime - born) / 0.6f;
                if (drt == null || k >= 1f) { if (drt != null) Destroy(drt.gameObject); _dust.RemoveAt(i); continue; }
                dimg.color = new Color(0.85f, 0.75f, 0.65f, Mathf.Sin(k * Mathf.PI) * 0.4f);
                drt.sizeDelta = Vector2.one * (34f + 40f * k);
                drt.anchoredPosition += new Vector2(-30f * Time.unscaledDeltaTime, 22f * Time.unscaledDeltaTime);
            }
        }

        // ───────────── 화면 만들기

        private void Build()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9500;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
            _pill = MakePill(); _soft = MakeSoft(); _vignette = MakeVignette();

            _bg = NewImage("Bg", _root, null, new Color(0.02f, 0.01f, 0.04f, 1f));
            Stretch(_bg.rectTransform);

            var content = NewRect("Content", _root);
            Stretch(content);
            _group = content.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // 가운데에서 번지는 붉은 빛 + 가장자리 어둠
            var glow = NewImage("Aura", content, _soft, new Color(0.55f, 0.1f, 0.2f, 0.42f));
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(1700f, 1000f);
            var vig = NewImage("Vignette", content, _vignette, new Color(0f, 0f, 0f, 0.7f));
            Stretch(vig.rectTransform);

            // 로고
            if (_logo != null)
            {
                var logo = NewImage("Logo", content, _logo, Color.white);
                logo.preserveAspect = true;
                logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                logo.rectTransform.sizeDelta = new Vector2(980f, 482f);
                logo.rectTransform.anchoredPosition = new Vector2(0f, 190f);
            }

            // 진행 막대: 어두운 외곽선 → 금빛 테두리 → 홈 → 잔상 → 채움 → 윗면 광택
            var barRoot = NewRect("Bar", content);
            barRoot.anchorMin = barRoot.anchorMax = new Vector2(0.5f, 0.5f);
            barRoot.sizeDelta = new Vector2(BarWidth, BarHeight);
            barRoot.anchoredPosition = new Vector2(0f, -170f);
            Slice("Outline", barRoot, new Color(0.02f, 0.01f, 0.03f, 0.95f), 10f);
            Slice("Frame", barRoot, new Color(0.88f, 0.68f, 0.36f, 1f), 5f);
            Slice("Groove", barRoot, new Color(0.1f, 0.05f, 0.09f, 1f), 0f);
            _trail = BarPart("Trail", barRoot, new Color(1f, 0.93f, 0.55f, 0.55f), out _trailRt);
            _fill = BarPart("Fill", barRoot, new Color(0.86f, 0.18f, 0.22f, 1f), out _fillRt);
            var shine = NewImage("Shine", _fillRt, _pill, new Color(1f, 1f, 1f, 0.28f));
            shine.type = Image.Type.Sliced; shine.pixelsPerUnitMultiplier = 3f; shine.raycastTarget = false;
            shine.rectTransform.anchorMin = new Vector2(0f, 0.5f); shine.rectTransform.anchorMax = Vector2.one;
            shine.rectTransform.offsetMin = new Vector2(3f, 0f); shine.rectTransform.offsetMax = new Vector2(-3f, -3f);
            // 채움 끝의 번지는 빛
            _glow = NewImage("EdgeGlow", barRoot, _soft, new Color(1f, 0.85f, 0.45f, 0f));
            _glowRt = _glow.rectTransform;
            _glowRt.anchorMin = _glowRt.anchorMax = new Vector2(0f, 0.5f);
            _glowRt.sizeDelta = new Vector2(90f, 90f);
            // 바 이름은 위치 기준으로만 쓴다

            // 막대 위를 달리는 마왕
            BuildKing(barRoot);

            // 글자
            _label = NewText("Label", content, 40f, Gold, TextAlignmentOptions.Left);
            _label.rectTransform.anchorMin = _label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _label.rectTransform.pivot = new Vector2(0f, 0.5f);
            _label.rectTransform.sizeDelta = new Vector2(500f, 56f);
            _label.rectTransform.anchoredPosition = new Vector2(-BarWidth * 0.5f, -232f);
            _label.text = "불러오는 중";
            _pct = NewText("Percent", barRoot, 54f, Gold, TextAlignmentOptions.Center);
            _pct.fontStyle = FontStyles.Bold;
            _pctRt = _pct.rectTransform;
            _pctRt.anchorMin = _pctRt.anchorMax = new Vector2(0.5f, 0.5f);
            _pctRt.sizeDelta = new Vector2(220f, 70f);
            _tip = NewText("Tip", content, 36f, Cream, TextAlignmentOptions.Center);
            _tip.rectTransform.anchorMin = _tip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _tip.rectTransform.sizeDelta = new Vector2(1500f, 60f);
            _tip.rectTransform.anchoredPosition = new Vector2(0f, -330f);
            Visuals();
        }

        private void BuildKing(RectTransform barRoot)
        {
            var cast = Resources.Load<StoryCast>("Story/StoryCast");
            var prefab = cast != null ? cast.Get("king") : null;
            if (prefab == null) return;
            var pgo = new GameObject("LoadingKing");
            pgo.transform.SetParent(transform, false);
            _portrait = pgo.AddComponent<DemonKingPortrait>();
            _portrait.Configure(new Vector3(9000f, 9000f, 0f), 384);
            _portrait.Build(prefab);
            if (!_portrait.IsReady) { Destroy(pgo); _portrait = null; return; }
            _portrait.SetShown(true);
            _portrait.Play(PlayerState.MOVE);

            var raw = new GameObject("King", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(barRoot, false);
            raw.texture = _portrait.Texture;
            raw.raycastTarget = false;
            raw.uvRect = new Rect(1f, 0f, -1f, 1f);                 // 프리팹은 왼쪽을 보므로 뒤집어 오른쪽(막대가 차는 쪽)으로 달리게
            _king = raw.rectTransform;
            _king.anchorMin = _king.anchorMax = new Vector2(0.5f, 0.5f);
            _king.sizeDelta = new Vector2(270f, 270f);
        }

        private Image BarPart(string name, RectTransform parent, Color color, out RectTransform rt)
        {
            var img = NewImage(name, parent, _pill, color);
            img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 3f;
            rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 0f);
            return img;
        }

        private void Slice(string name, RectTransform parent, Color color, float grow)
        {
            var img = NewImage(name, parent, _pill, color);
            img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 3f;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-grow, -grow); rt.offsetMax = new Vector2(grow, grow);
        }

        private void OnDestroy()
        {
            if (_portrait != null) Destroy(_portrait.gameObject);
        }

        // ───────────── 도우미

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

        private TMP_Text NewText(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.22f; t.outlineColor = new Color32(20, 6, 12, 255);
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static TMP_FontAsset FindFont()
        {
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters("불러오는중합성증강", out _, true, true)) return font;
            return null;
        }

        private static Sprite MakePill()
        {
            const int n = 64, radius = 28;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (n - radius), 0f);
                    float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (n - radius), 0f);
                    float dist = radius - Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(dist + 0.5f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite MakeSoft()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - (n - 1) * 0.5f) / (n * 0.5f), dy = (y - (n - 1) * 0.5f) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeVignette()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - (n - 1) * 0.5f) / (n * 0.5f), dy = (y - (n - 1) * 0.5f) / (n * 0.5f);
                    float d = Mathf.Sqrt(dx * dx * 0.85f + dy * dy * 1.1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.6f))));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
