using System;
using OZGL2.InGame;
using OZGL2.Stage;
using OZGL2.Tutorial;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 중 위쪽 가운데에 뜨는 조절 막대 두 개.
    /// - 배속 막대: 정지 · ▶ x1 · ▶▶ x2 (최대 2배). 고른 칸을 금빛 선택 막대가 미끄러져 가며 가리킨다. Space 로 정지/재생.
    /// - 카메라 단추 하나: 「자동」 ON(전투 자동 구도) / OFF(수동 카메라 — 휠 확대·끌기 이동으로만). 눌러서 바꾸며, 켜지면 금빛으로 밝아진다. 고른 값은 저장된다(InGameCameraControl).
    /// 마우스를 올리면 밝아지고, 누르면 톡 튀어나온다. 씬을 떠날 때 배속은 1배로 되돌린다.
    /// </summary>
    public sealed class InGameSpeedControl : MonoBehaviour
    {
        private static readonly float[] Speeds = { 0f, 1f, 2f };
        private static readonly string[] Labels = { "정지", "x1", "x2" };
        private const int ToggleCount = 1;

        private static readonly Color Panel = new Color(0.07f, 0.045f, 0.095f, 0.92f);
        private static readonly Color Gold = new Color(1f, 0.80f, 0.32f, 1f);
        private static readonly Color GoldDeep = new Color(0.93f, 0.62f, 0.18f, 1f);
        private static readonly Color Cream = new Color(0.97f, 0.93f, 0.84f, 1f);
        private static readonly Color Ink = new Color(0.22f, 0.12f, 0.06f, 1f);
        private static readonly Color Off = new Color(0.62f, 0.58f, 0.60f, 1f);
        private static readonly Color Ring = new Color(0.88f, 0.68f, 0.36f, 0.95f);

        private int _chosen = 1;
        private Canvas _canvas;
        private RectTransform _bar, _camBar, _selector;
        private Image _selectorGlow, _selectorShine;
        private readonly Image[] _icons = new Image[3];
        private readonly Image[][] _triangles = new Image[3][];
        private readonly TMP_Text[] _labels = new TMP_Text[3];
        private readonly bool[] _hover = new bool[3 + ToggleCount];
        // 카메라 단추
        private readonly RectTransform[] _toggleRt = new RectTransform[ToggleCount];
        private readonly Image[] _togglePlate = new Image[ToggleCount], _toggleRing = new Image[ToggleCount], _toggleIcon = new Image[ToggleCount], _toggleLed = new Image[ToggleCount];
        private readonly TMP_Text[] _toggleLabel = new TMP_Text[ToggleCount];
        private readonly float[] _toggleT = new float[ToggleCount], _togglePunch = new float[ToggleCount];

        private Sprite _pill, _ring, _triangle, _bars, _glow, _disc, _cameraIcon;
        private TutorialDirector _tutorial;
        private float _selectorX, _punch;
        private int _lastScreenHeight;
        private InGamePrototypeBootstrap _bootstrap;
        private bool _inCombat, _applyPending;
        private float _hudBottom = -1f, _nextHudScan;   // 맨 위 상단 바 아래쪽 끝(화면 픽셀, 아래가 0). 찾기 전에는 -1

        private float S => Mathf.Max(0.8f, Screen.height / 1080f);

        private void OnDestroy()
        {
            Time.timeScale = 1f; // 씬을 떠날 때 배속이 남지 않게
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void Start() => Build();

        private static bool ToggleState(int i) => InGameCameraControl.AutoEnabled;

        private void Update()
        {
            if (_bar == null) return;
            if (Time.unscaledTime >= _nextHudScan)
            {
                _nextHudScan = Time.unscaledTime + 0.5f;
                float bottom = FindHudBottom();
                if (Mathf.Abs(bottom - _hudBottom) > 1f) { _hudBottom = bottom; _lastScreenHeight = 0; }
            }
            if (_lastScreenHeight != Screen.height) Layout();

            if (_tutorial == null) _tutorial = FindFirstObjectByType<TutorialDirector>();
            bool talking = _tutorial != null && _tutorial.IsPlaying;
            if (!UpdateCombatVisibility(talking)) return;
            var kb = Keyboard.current;
            if (!talking && kb != null && kb.spaceKey.wasPressedThisFrame) Choose(_chosen == 0 ? 1 : 0);

            float dt = Time.unscaledDeltaTime;

            // 선택 막대가 고른 칸으로 미끄러진다
            float target = SegmentCenterX(_chosen);
            _selectorX = Mathf.Lerp(_selectorX, target, 1f - Mathf.Exp(-16f * dt));
            _selector.anchoredPosition = new Vector2(_selectorX, 0f);
            _punch = Mathf.MoveTowards(_punch, 0f, dt * 4f);
            _selector.localScale = Vector3.one * (1f + Mathf.Sin(_punch * Mathf.PI) * 0.10f);
            if (_selectorGlow != null) _selectorGlow.color = new Color(1f, 0.85f, 0.4f, 0.26f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f));

            for (int i = 0; i < Speeds.Length; i++)
            {
                bool active = i == _chosen;
                Color c = active ? Ink : _hover[i] ? Color.white : Cream;
                if (_icons[i] != null) _icons[i].color = c;
                if (_triangles[i] != null) foreach (var icon in _triangles[i]) icon.color = c;
                _labels[i].color = active ? Ink : _hover[i] ? Color.white : new Color(0.85f, 0.80f, 0.72f);
            }

            // 카메라 단추: 켜짐 정도에 따라 금빛으로 밝아진다
            for (int i = 0; i < ToggleCount; i++)
            {
                bool on = ToggleState(i);
                _toggleT[i] = Mathf.MoveTowards(_toggleT[i], on ? 1f : 0f, dt * 7f);
                _togglePunch[i] = Mathf.MoveTowards(_togglePunch[i], 0f, dt * 4.5f);
                float k = Mathf.SmoothStep(0f, 1f, _toggleT[i]);
                float hover = _hover[3 + i] ? 1f : 0f;
                _togglePlate[i].color = new Color(GoldDeep.r, GoldDeep.g, GoldDeep.b, 0.10f + 0.30f * k + 0.08f * hover);
                _toggleRing[i].color = Color.Lerp(new Color(Ring.r, Ring.g, Ring.b, 0.22f + 0.2f * hover), new Color(Gold.r, Gold.g, Gold.b, 1f), k);
                Color icon = Color.Lerp(Off, Gold, k);
                if (_hover[3 + i]) icon = Color.Lerp(icon, Color.white, 0.35f);
                _toggleIcon[i].color = icon;
                _toggleLed[i].color = Color.Lerp(new Color(0.3f, 0.27f, 0.3f, 1f), new Color(0.55f, 1f, 0.5f, 1f), k);
                _toggleLabel[i].color = Color.Lerp(new Color(0.7f, 0.65f, 0.66f), Cream, k);
                _toggleRt[i].localScale = Vector3.one * (1f + Mathf.Sin(_togglePunch[i] * Mathf.PI) * 0.10f);
            }
        }

        /// <summary>
        /// 조절 막대는 전투 중에만 보인다. 배치·보상·결과 화면에서는 숨기고 배속을 1배로 돌려 놓는다(정지로 끝났다면 다음 전투는 1배로 시작).
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

        private void Toggle(int index)
        {
            InGameCameraControl.AutoEnabled = !InGameCameraControl.AutoEnabled;
            _togglePunch[index] = 1f;
            RefreshToggleLabel(index);
        }

        private void RefreshToggleLabel(int i)
            => _toggleLabel[i].text = "자동 <size=78%>" + (ToggleState(i) ? "ON" : "OFF") + "</size>";

        // ───────────── 만들기

        private void Build()
        {
            _pill = MakePill(false); _ring = MakePill(true); _triangle = MakeTriangle(); _bars = MakeBars(); _glow = MakeGlow();
            _disc = MakeDisc(); _cameraIcon = MakeCameraIcon();

            var go = new GameObject("SpeedControl", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 28; // 손패(10~11)·드롭 존(15) 위, 시너지 설명(30)·팝업 아래
            go.AddComponent<GraphicRaycaster>();

            // ── 배속 막대
            _bar = BuildPanel("Bar", go.transform, out _);
            _selector = BuildSelector(_bar);
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
                seg.gameObject.AddComponent<SpeedSegmentHover>().Setup(h => _hover[index] = h);

                // 아이콘: 정지는 막대 두 개, 나머지는 속도만큼의 재생 삼각형(x1=1개, x2=2개)
                if (i == 0)
                {
                    _icons[i] = NewImage("Icon", rt, _bars, Cream);
                    _icons[i].raycastTarget = false; _icons[i].preserveAspect = true;
                }
                else
                {
                    _triangles[i] = new Image[i];
                    for (int k = 0; k < i; k++)
                    {
                        var icon = NewImage("Icon" + k, rt, _triangle, Cream);
                        icon.raycastTarget = false; icon.preserveAspect = true;
                        _triangles[i][k] = icon;
                    }
                }
                _labels[i] = NewText("Label", rt, 20f, Cream);
                _labels[i].text = Labels[i];
                _labels[i].fontStyle = FontStyles.Bold;
            }

            // ── 카메라 막대
            _camBar = BuildPanel("CamBar", go.transform, out _);
            for (int i = 0; i < ToggleCount; i++)
            {
                int index = i;
                var root = NewImage("Toggle" + i, _camBar, null, new Color(0f, 0f, 0f, 0.003f));
                _toggleRt[i] = root.rectTransform;
                _toggleRt[i].anchorMin = _toggleRt[i].anchorMax = new Vector2(0f, 0.5f);
                _toggleRt[i].pivot = new Vector2(0.5f, 0.5f);
                var button = root.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => Toggle(index));
                root.gameObject.AddComponent<SpeedSegmentHover>().Setup(h => _hover[3 + index] = h);

                _togglePlate[i] = NewImage("Plate", _toggleRt[i], _pill, new Color(GoldDeep.r, GoldDeep.g, GoldDeep.b, 0.1f));
                _togglePlate[i].type = Image.Type.Sliced; _togglePlate[i].pixelsPerUnitMultiplier = 1.8f; _togglePlate[i].raycastTarget = false;
                _toggleRing[i] = NewImage("Ring", _toggleRt[i], _ring, Ring);
                _toggleRing[i].type = Image.Type.Sliced; _toggleRing[i].pixelsPerUnitMultiplier = 1.8f; _toggleRing[i].raycastTarget = false;
                _toggleIcon[i] = NewImage("Icon", _toggleRt[i], _cameraIcon, Off);
                _toggleIcon[i].raycastTarget = false; _toggleIcon[i].preserveAspect = true;
                _toggleLed[i] = NewImage("Led", _toggleRt[i], _disc, Off);
                _toggleLed[i].raycastTarget = false;
                _toggleLabel[i] = NewText("Label", _toggleRt[i], 20f, Cream);
                _toggleLabel[i].fontStyle = FontStyles.Bold;
                RefreshToggleLabel(i);
                _toggleT[i] = ToggleState(i) ? 1f : 0f;
            }

            Layout();
            _selectorX = SegmentCenterX(_chosen);
        }

        /// <summary>둥근 알약 모양 바탕(그림자 · 바탕 · 윗면 하이라이트 · 금빛 테두리). 이름이 Bar 이면 튜토리얼이 이것을 가리킨다.</summary>
        private RectTransform BuildPanel(string name, Transform parent, out Image body)
        {
            var shadow = NewImage(name + "Shadow", parent, _glow, new Color(0f, 0f, 0f, 0.5f));
            shadow.raycastTarget = false;
            var shadowRt = shadow.rectTransform;
            shadowRt.anchorMin = shadowRt.anchorMax = new Vector2(0.5f, 1f);
            shadowRt.pivot = new Vector2(0.5f, 0.5f);

            body = NewImage(name, parent, _pill, Panel);
            body.type = Image.Type.Sliced; body.pixelsPerUnitMultiplier = 1.6f;
            var rt = body.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);

            var sheen = NewImage("Sheen", rt, _pill, new Color(1f, 1f, 1f, 0.07f));
            sheen.type = Image.Type.Sliced; sheen.pixelsPerUnitMultiplier = 2.4f; sheen.raycastTarget = false;
            sheen.rectTransform.anchorMin = new Vector2(0f, 0.5f); sheen.rectTransform.anchorMax = Vector2.one;
            sheen.rectTransform.offsetMin = new Vector2(5f, 0f); sheen.rectTransform.offsetMax = new Vector2(-5f, -4f);

            var ring = NewImage("Ring", rt, _ring, Ring);
            ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 1.6f; ring.raycastTarget = false;
            Stretch(ring.rectTransform);
            var inner = NewImage("InnerRing", rt, _ring, new Color(1f, 0.9f, 0.6f, 0.16f));
            inner.type = Image.Type.Sliced; inner.pixelsPerUnitMultiplier = 2.6f; inner.raycastTarget = false;
            inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
            inner.rectTransform.offsetMin = new Vector2(5f, 5f); inner.rectTransform.offsetMax = new Vector2(-5f, -5f);
            return rt;
        }

        /// <summary>금빛 선택 막대(번지는 빛 · 윗면 반짝임 포함).</summary>
        private RectTransform BuildSelector(RectTransform bar)
        {
            var sel = NewImage("Selector", bar, _pill, Gold);
            sel.type = Image.Type.Sliced; sel.pixelsPerUnitMultiplier = 1.8f; sel.raycastTarget = false;
            var rt = sel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            _selectorGlow = NewImage("Glow", rt, _glow, new Color(1f, 0.85f, 0.4f, 0.3f));
            _selectorGlow.raycastTarget = false;
            var gr = _selectorGlow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
            gr.SetAsFirstSibling();
            _selectorShine = NewImage("Shine", rt, _pill, new Color(1f, 0.97f, 0.8f, 0.55f));
            _selectorShine.type = Image.Type.Sliced; _selectorShine.pixelsPerUnitMultiplier = 2.6f; _selectorShine.raycastTarget = false;
            _selectorShine.rectTransform.anchorMin = new Vector2(0f, 0.5f); _selectorShine.rectTransform.anchorMax = Vector2.one;
            _selectorShine.rectTransform.offsetMin = new Vector2(4f, 0f); _selectorShine.rectTransform.offsetMax = new Vector2(-4f, -3f);
            return rt;
        }

        // 가로 치수(막대 안쪽 여백 · 칸 폭)
        private float SegW => 80f * S;
        private float TogW => 98f * S;
        private float Pad => 11f * S;
        private float Gap => 16f * S;

        private float SegmentCenterX(int i) => Pad + SegW * (i + 0.5f);

        /// <summary>
        /// 전투 화면 맨 위 상단 바(레벨 글자를 품은 가로로 넓은 띠)의 아래쪽 끝을 화면 픽셀로 돌려준다. 팀 UI(UIBattleMutedPreviewView)는 건드리지 않고 읽기만 한다.
        /// 못 찾으면 -1.
        /// </summary>
        private static float FindHudBottom()
        {
            var hud = FindFirstObjectByType<UIBattleMutedPreviewView>();
            if (hud == null) return -1f;
            var field = typeof(UIBattleMutedPreviewView).GetField("_levelText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var text = field != null ? field.GetValue(hud) as Graphic : null;
            if (text == null || !text.gameObject.activeInHierarchy) return -1f;
            var corners = new Vector3[4];
            for (var rt = text.rectTransform.parent as RectTransform; rt != null; rt = rt.parent as RectTransform)
            {
                rt.GetWorldCorners(corners);
                float width = corners[2].x - corners[0].x;
                if (width >= Screen.width * 0.6f && corners[0].y > Screen.height * 0.5f) return corners[0].y;   // 위쪽 절반에 있는 넓은 띠
            }
            return -1f;
        }

        private void Layout()
        {
            _lastScreenHeight = Screen.height;
            float s = S, segW = SegW, togW = TogW, pad = Pad, height = 68f * s;
            float speedW = pad * 2f + segW * Speeds.Length;
            float camW = pad * 2f + togW * ToggleCount;
            float total = speedW + Gap + camW;
            // 맨 위 상단 바(재화·레벨·경험치) 바로 아래에 붙이되 겹치지 않게 조금 띄운다. 바를 못 찾으면 화면 비율로 정한다.
            float topY = _hudBottom > 0f ? -(Screen.height - _hudBottom) - 12f * s : -Screen.height * 0.105f - 24f * s;

            PlacePanel(_bar, new Vector2(-total * 0.5f + speedW * 0.5f, topY), new Vector2(speedW, height));
            PlacePanel(_camBar, new Vector2(total * 0.5f - camW * 0.5f, topY), new Vector2(camW, height));

            _selector.sizeDelta = new Vector2(segW - 6f * s, height - 14f * s);
            _selectorGlow.rectTransform.sizeDelta = _selector.sizeDelta * 1.7f;

            for (int i = 0; i < Speeds.Length; i++)
            {
                var rt = (RectTransform)_bar.Find("Seg" + i);
                rt.sizeDelta = new Vector2(segW, height);
                rt.anchoredPosition = new Vector2(SegmentCenterX(i), 0f);
                float iconSize = (i == 0 ? 22f : 19f) * s;
                if (i == 0) PlaceIcon(_icons[i].rectTransform, iconSize, 0f, 9f * s);
                else
                {
                    int n = _triangles[i].Length;
                    float step = iconSize * 0.72f;
                    float totalW = iconSize + step * (n - 1);
                    for (int k = 0; k < n; k++)
                        PlaceIcon(_triangles[i][k].rectTransform, iconSize, -totalW * 0.5f + iconSize * 0.5f + step * k, 9f * s);
                }
                var lr = _labels[i].rectTransform;
                _labels[i].fontSize = 20f * s;
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(segW, 26f * s);
                lr.anchoredPosition = new Vector2(0f, -16f * s);
            }

            for (int i = 0; i < ToggleCount; i++)
            {
                var rt = _toggleRt[i];
                rt.sizeDelta = new Vector2(togW - 4f * s, height - 14f * s);
                rt.anchoredPosition = new Vector2(pad + togW * (i + 0.5f), 0f);
                Stretch(_togglePlate[i].rectTransform);
                Stretch(_toggleRing[i].rectTransform);
                PlaceIcon(_toggleIcon[i].rectTransform, 25f * s, 0f, 9f * s);
                var led = _toggleLed[i].rectTransform;
                led.anchorMin = led.anchorMax = new Vector2(1f, 1f);
                led.pivot = new Vector2(0.5f, 0.5f);
                led.sizeDelta = Vector2.one * 8f * s;
                led.anchoredPosition = new Vector2(-12f * s, -11f * s);
                var lr = _toggleLabel[i].rectTransform;
                _toggleLabel[i].fontSize = 20f * s;
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(togW, 26f * s);
                lr.anchoredPosition = new Vector2(0f, -16f * s);
            }
        }

        private void PlacePanel(RectTransform bar, Vector2 pos, Vector2 size)
        {
            bar.sizeDelta = size;
            bar.anchoredPosition = pos;
            var shadow = (RectTransform)bar.parent.Find(bar.name + "Shadow");
            if (shadow != null)
            {
                shadow.sizeDelta = size * 1.28f + new Vector2(30f, 36f) * S;
                shadow.anchoredPosition = pos + new Vector2(0f, -size.y * 0.5f - 6f * S);
            }
        }

        private static void PlaceIcon(RectTransform ir, float size, float x, float y)
        {
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = new Vector2(size, size);
            ir.anchoredPosition = new Vector2(x, y);
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
                    if (font != null && font.HasCharacters("정지자동수동ONFx12", out _, true, true)) { tmp.font = font; break; }
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.richText = true;
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
                    float d1 = (px - 10f);                                        // 왼쪽 변까지
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

        private static Sprite MakeDisc()
        {
            return Raster(64, (x, y) => (x - 32f) * (x - 32f) + (y - 32f) * (y - 32f) <= 26f * 26f);
        }

        /// <summary>그림 한 장을 3x3 겹샘플로 그려 가장자리를 부드럽게 만든다. inside(x, y) 가 참이면 그 점은 채워진다(좌표는 0~64, 위쪽이 큰 y).</summary>
        private static Sprite Raster(int n, Func<float, float, bool> inside)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float scale = 64f / n;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                            if (inside((x + (sx + 0.5f) / 3f) * scale, (y + (sy + 0.5f) / 3f) * scale)) hit++;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, hit / 9f));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool RoundRect(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float cx = Mathf.Clamp(x, x0 + r, x1 - r), cy = Mathf.Clamp(y, y0 + r, y1 - r);
            return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
        }

        /// <summary>카메라 아이콘(자동 카메라): 몸통 + 위쪽 볼록 + 렌즈 + 뷰파인더 점.</summary>
        private static Sprite MakeCameraIcon()
        {
            return Raster(64, (x, y) =>
            {
                bool body = RoundRect(x, y, 6f, 12f, 58f, 46f, 7f) || RoundRect(x, y, 20f, 42f, 44f, 54f, 4f);
                if (!body) return false;
                float dx = x - 32f, dy = y - 29f, d2 = dx * dx + dy * dy;
                if (d2 <= 12.5f * 12.5f && d2 >= 8.5f * 8.5f) return false;   // 렌즈 둘레 홈
                if (d2 < 8.5f * 8.5f) return d2 <= 5.2f * 5.2f;                // 렌즈 안쪽 동공
                float ex = x - 50f, ey = y - 38f;
                if (ex * ex + ey * ey <= 2.4f * 2.4f) return false;            // 뷰파인더 점
                return true;
            });
        }
    }

    /// <summary>칸 위에 마우스가 올라와 있는지 알려 준다.</summary>
    internal sealed class SpeedSegmentHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action<bool> _callback;
        public void Setup(Action<bool> callback) => _callback = callback;
        public void OnPointerEnter(PointerEventData e) => _callback?.Invoke(true);
        public void OnPointerExit(PointerEventData e) => _callback?.Invoke(false);
    }
}
