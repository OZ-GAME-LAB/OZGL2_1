using OZGL2.InGame;
using OZGL2.Stage;
using OZGL2.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 배속 조절 — 상단 바 아래 가운데의 알약 모양 선택 막대(정지 · ▶ 1배 · ▶▶ 2배 · ▶▶▶ 4배 · ▶▶▶▶ 8배).
    /// 고른 칸을 금빛 선택 막대가 부드럽게 미끄러져 가며 가리키고, 칸마다 재생 삼각형 개수로 빠르기를 보여 준다. 마우스를 올리면 밝아진다.
    /// Space로 정지/재생을 바꿀 수 있다(마왕 설명 중에는 동작하지 않는다). 씬을 떠날 때 배속은 1배로 되돌린다.
    /// </summary>
    public sealed class InGameSpeedControl : MonoBehaviour
    {
        private static readonly float[] Speeds = { 0f, 1f, 2f, 4f, 8f };
        private static readonly string[] Labels = { "정지", "x1", "x2", "x4", "x8" };

        private static readonly Color Panel = new Color(0.08f, 0.05f, 0.10f, 0.88f);
        private static readonly Color Gold = new Color(1f, 0.80f, 0.32f, 1f);
        private static readonly Color Cream = new Color(0.97f, 0.93f, 0.84f, 1f);
        private static readonly Color Ink = new Color(0.22f, 0.12f, 0.06f, 1f);

        private int _chosen = 1;
        private Canvas _canvas;
        private RectTransform _bar, _selector;
        private Image _selectorGlow;
        private readonly Image[][] _icons = new Image[5][];
        private readonly TMP_Text[] _labels = new TMP_Text[5];
        private readonly bool[] _hover = new bool[5];
        private Sprite _pill, _ring, _triangle, _bars, _glow;
        private TutorialDirector _tutorial;
        private float _selectorX, _punch;
        private int _lastScreenHeight;
        private InGamePrototypeBootstrap _bootstrap;
        private bool _inCombat, _applyPending;

        private float S => Mathf.Max(0.8f, Screen.height / 1080f);

        private void OnDestroy()
        {
            Time.timeScale = 1f; // 씬을 떠날 때 배속이 남지 않게
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void Start() => Build();

        private void Update()
        {
            if (_bar == null) return;
            if (_lastScreenHeight != Screen.height) Layout();

            if (_tutorial == null) _tutorial = FindFirstObjectByType<TutorialDirector>();
            bool talking = _tutorial != null && _tutorial.IsPlaying;
            if (!UpdateCombatVisibility(talking)) return;
            var kb = Keyboard.current;
            if (!talking && kb != null && kb.spaceKey.wasPressedThisFrame) Choose(_chosen == 0 ? 1 : 0);

            // 선택 막대가 고른 칸으로 미끄러진다
            float target = SegmentCenterX(_chosen);
            _selectorX = Mathf.Lerp(_selectorX, target, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
            _selector.anchoredPosition = new Vector2(_selectorX, 0f);
            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 4f);
            _selector.localScale = Vector3.one * (1f + Mathf.Sin(_punch * Mathf.PI) * 0.10f);
            if (_selectorGlow != null) _selectorGlow.color = new Color(1f, 0.85f, 0.4f, 0.28f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f));

            for (int i = 0; i < Speeds.Length; i++)
            {
                bool active = i == _chosen;
                Color c = active ? Ink : _hover[i] ? Color.white : Cream;
                foreach (var icon in _icons[i]) icon.color = c;
                _labels[i].color = active ? Ink : _hover[i] ? Color.white : new Color(0.85f, 0.80f, 0.72f);
            }
        }

        /// <summary>
        /// 배속 막대는 전투 중에만 보인다. 배치·보상·결과 화면에서는 숨기고 배속을 1배로 돌려 놓는다(정지로 끝났다면 다음 전투는 1배로 시작).
        /// 다음 전투가 시작되면 고른 배속을 다시 적용한다. 마왕 설명으로 멈춰 있는 동안에는 설명이 끝난 뒤에 적용한다.
        /// </summary>
        private bool UpdateCombatVisibility(bool talking)
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            bool combat = stage != null && stage.State == eStageState.COMBAT;
            if (combat != _inCombat)
            {
                _inCombat = combat;
                if (combat) _applyPending = true;
                else
                {
                    if (_chosen == 0) _chosen = 1;
                    _selectorX = SegmentCenterX(_chosen);
                    Time.timeScale = 1f;
                    _applyPending = false;
                }
            }
            if (_inCombat && _applyPending && !talking)
            {
                Time.timeScale = Speeds[_chosen];
                _applyPending = false;
            }
            if (_canvas != null && _canvas.enabled != _inCombat) _canvas.enabled = _inCombat;
            return _inCombat;
        }

        private void Choose(int index)
        {
            if (index == _chosen) return;
            _chosen = index;
            _punch = 1f;
            Time.timeScale = Speeds[index];
        }

        // ───────────── 만들기

        private void Build()
        {
            _pill = MakePill(false); _ring = MakePill(true); _triangle = MakeTriangle(); _bars = MakeBars(); _glow = MakeGlow();

            var go = new GameObject("SpeedControl", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 28; // 손패(10~11)·드롭 존(15) 위, 시너지 설명(30)·팝업 아래
            go.AddComponent<GraphicRaycaster>();

            var barImage = NewImage("Bar", go.transform, _pill, Panel);
            barImage.type = Image.Type.Sliced; barImage.pixelsPerUnitMultiplier = 1.6f;
            _bar = barImage.rectTransform;
            _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 1f);
            _bar.pivot = new Vector2(0.5f, 1f);
            var ring = NewImage("Ring", _bar, _ring, new Color(0.88f, 0.68f, 0.36f, 0.95f));
            ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 1.6f; ring.raycastTarget = false;
            Stretch(ring.rectTransform);

            // 금빛 선택 막대(번지는 빛 포함)
            var sel = NewImage("Selector", _bar, _pill, Gold);
            sel.type = Image.Type.Sliced; sel.pixelsPerUnitMultiplier = 1.8f; sel.raycastTarget = false;
            _selector = sel.rectTransform;
            _selector.anchorMin = _selector.anchorMax = new Vector2(0f, 0.5f);
            _selector.pivot = new Vector2(0.5f, 0.5f);
            _selectorGlow = NewImage("Glow", _selector, _glow, new Color(1f, 0.85f, 0.4f, 0.3f));
            _selectorGlow.raycastTarget = false;
            var gr = _selectorGlow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
            gr.SetAsFirstSibling();

            for (int i = 0; i < Speeds.Length; i++)
            {
                int index = i;
                var seg = NewImage("Seg" + i, _bar, null, new Color(0f, 0f, 0f, 0.003f));
                var rt = seg.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var button = seg.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => Choose(index));
                var hover = seg.gameObject.AddComponent<SpeedSegmentHover>();
                hover.Setup(h => _hover[index] = h);

                // 아이콘: 정지는 막대 두 개, 나머지는 속도만큼의 재생 삼각형
                int count = i == 0 ? 1 : i; // 1x=1개 · 2x=2개 · 4x=3개 · 8x=4개
                _icons[i] = new Image[count];
                for (int k = 0; k < count; k++)
                {
                    var icon = NewImage("Icon" + k, rt, i == 0 ? _bars : _triangle, Cream);
                    icon.raycastTarget = false;
                    icon.preserveAspect = true;
                    _icons[i][k] = icon;
                }
                var label = NewText("Label", rt, 20f, Cream);
                label.text = Labels[i];
                _labels[i] = label;
            }

            Layout();
            _selectorX = SegmentCenterX(_chosen);
        }

        private float SegmentCenterX(int i)
        {
            float s = S, segW = 74f * s, pad = 12f * s;
            return pad + segW * (i + 0.5f);
        }

        private void Layout()
        {
            _lastScreenHeight = Screen.height;
            float s = S, segW = 74f * s, pad = 12f * s, height = 64f * s;
            _bar.sizeDelta = new Vector2(pad * 2f + segW * Speeds.Length, height);
            _bar.anchoredPosition = new Vector2(0f, -Screen.height * 0.105f);

            _selector.sizeDelta = new Vector2(segW - 6f * s, height - 12f * s);
            var gr = _selectorGlow.rectTransform;
            gr.sizeDelta = _selector.sizeDelta * 1.7f;

            for (int i = 0; i < Speeds.Length; i++)
            {
                var rt = (RectTransform)_bar.Find("Seg" + i);
                rt.sizeDelta = new Vector2(segW, height);
                rt.anchoredPosition = new Vector2(SegmentCenterX(i), 0f);
                int n = _icons[i].Length;
                float iconSize = (i == 0 ? 22f : 19f) * s;
                float step = iconSize * 0.72f;
                float total = iconSize + step * (n - 1);
                for (int k = 0; k < n; k++)
                {
                    var ir = _icons[i][k].rectTransform;
                    ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
                    ir.pivot = new Vector2(0.5f, 0.5f);
                    ir.sizeDelta = new Vector2(iconSize, iconSize);
                    ir.anchoredPosition = new Vector2(-total * 0.5f + iconSize * 0.5f + step * k, 9f * s);
                }
                var lr = _labels[i].rectTransform;
                _labels[i].fontSize = 20f * s;
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(segW, 26f * s);
                lr.anchoredPosition = new Vector2(0f, -16f * s);
            }
        }

        // ───────────── 도우미

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        private static TMP_Text NewText(string name, Transform parent, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (UiFontOverride.Current != null) tmp.font = UiFontOverride.Current;
            else
                foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                    if (font != null && font.HasCharacters("정지x1248", out _, true, true)) { tmp.font = font; break; }
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Sprite MakePill(bool ring)
        {
            const int n = 64, radius = 28;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (n - radius), 0f);
                    float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (n - radius), 0f);
                    float dist = radius - Mathf.Sqrt(dx * dx + dy * dy);
                    float inside = Mathf.Clamp01(dist + 0.5f);
                    float a = ring ? Mathf.Clamp01(inside - Mathf.Clamp01(dist - 3f + 0.5f)) : inside;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        /// <summary>오른쪽을 가리키는 재생 삼각형(모서리를 살짝 둥글게).</summary>
        private static Sprite MakeTriangle()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // 꼭짓점: (10,6) (10,58) (56,32)
                    float px = x + 0.5f, py = y + 0.5f;
                    float d1 = (px - 10f) ;                                       // 왼쪽 변까지
                    float d2 = ((56f - px) * 26f - (py - 32f) * 46f) / 52.8f;     // 위쪽 빗변
                    float d3 = ((56f - px) * 26f + (py - 32f) * 46f) / 52.8f;     // 아래쪽 빗변
                    float d = Mathf.Min(d1, Mathf.Min(d2, d3));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(d * 0.5f + 0.5f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeBars()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool bar = (x >= 14 && x < 28 || x >= 36 && x < 50) && y >= 8 && y < 56;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, bar ? 1f : 0f));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeGlow()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>배속 칸 위에 마우스가 올라와 있는지 알려 준다.</summary>
    internal sealed class SpeedSegmentHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private System.Action<bool> _callback;
        public void Setup(System.Action<bool> callback) => _callback = callback;
        public void OnPointerEnter(PointerEventData e) => _callback?.Invoke(true);
        public void OnPointerExit(PointerEventData e) => _callback?.Invoke(false);
    }
}
