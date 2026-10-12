using System;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 설정 창(로비 설정 · 전투 메뉴의 설정)의 「배경음악」「효과음」 줄 아래에 음량 슬라이더를 붙인다. 켜기·끄기 토글은 그대로 두고, 음량만 따로 조절한다.
    /// 팀 프리팹은 수정하지 않고 실행 중에 만든다 — 줄 위치를 위로 조금 올리고, 각 줄 아래에
    ///   [금빛 테두리 홈 + 붉은 채움 + 마름모 손잡이] 슬라이더와 「72%」 숫자를 놓는다.
    /// 손잡이는 마우스를 올리면 커지며 빛나고, 끌 때도 같다. 토글이 꺼져 있으면 슬라이더는 흐리게 보인다(그래도 조절은 된다).
    /// 값은 GameAudioSettings 가 저장하고(기본 100%), 배경음악은 움직이는 동안 바로 들리고, 효과음은 손을 떼면 확인용 소리가 한 번 난다.
    /// </summary>
    public static class AudioVolumeSliderUi
    {
        private static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
        private static readonly Color GoldBright = new Color(1f, 0.85f, 0.5f, 1f);
        private static readonly Color Crimson = new Color(0.72f, 0.13f, 0.19f, 1f);
        private static readonly Color Dark = new Color(0.075f, 0.045f, 0.07f, 1f);

        private static Sprite _pill, _rim, _inset, _fill, _diamond, _soft;

        /// <summary>view 아래에서 「BgmLabel」을 가진 설정 패널을 찾아 슬라이더를 붙인다. 이미 붙어 있거나 해당 패널이 없으면 아무것도 하지 않는다.</summary>
        public static void AttachTo(Transform view)
        {
            if (view == null) return;
            foreach (var rect in view.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name != "BgmLabel") continue;
                var panel = rect.parent as RectTransform;
                if (panel == null) continue;
                var commonSettings = panel.GetComponentInParent<UICommonSettingsView>(true);
                // 공통 Prefab에 연결된 슬라이더와 행 배치는 그대로 사용한다.
                if (commonSettings != null && commonSettings.IsIndividualVolumeConfigured) continue;
                if (panel.Find("BgmVolume") == null) Attach(panel);
            }
        }

        private static void Attach(RectTransform panel)
        {
            var bgmLabel = panel.Find("BgmLabel") as RectTransform;
            var sfxLabel = panel.Find("SfxLabel") as RectTransform;
            var bgmToggle = panel.Find("BGMToggle") as RectTransform;
            var sfxToggle = panel.Find("SFXToggle") as RectTransform;
            if (bgmLabel == null || sfxLabel == null || bgmToggle == null || sfxToggle == null) return;

            EnsureSprites();
            var font = bgmLabel.GetComponentInChildren<TMP_Text>(true);

            // 줄을 위로 올려 슬라이더 자리를 만든다
            const float bgmY = 92f, sfxY = -18f, gap = 52f;
            SetY(bgmLabel, bgmY); SetY(bgmToggle, bgmY);
            SetY(sfxLabel, sfxY); SetY(sfxToggle, sfxY);

            Build(panel, "BgmVolume", bgmY - gap, font, () => GameAudioSettings.BgmEnabled, GameAudioSettings.BgmVolume,
                GameAudioSettings.SetBgmVolume, false);
            Build(panel, "SfxVolume", sfxY - gap, font, () => GameAudioSettings.SfxEnabled, GameAudioSettings.SfxVolume,
                GameAudioSettings.SetSfxVolume, true);
        }

        private static void SetY(RectTransform rect, float y)
        {
            var p = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(p.x, y);
        }

        private static void Build(RectTransform panel, string name, float y, TMP_Text fontSource, Func<bool> enabled, float value, Action<float> apply, bool previewOnRelease)
        {
            const float width = 300f, height = 44f;
            var root = NewRect(name, panel);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(width, height);
            root.anchoredPosition = new Vector2(-62f, y);
            root.gameObject.AddComponent<CanvasGroup>();

            // 누를 수 있는 영역(눈에 안 보임) — 홈이 가늘어도 위아래로 넉넉히 눌린다
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            // 금속 테두리(위는 밝고 아래는 어두운 금) + 안쪽으로 파인 어두운 홈
            var rim = NewImage("Rim", root, _rim, new Color(1f, 0.83f, 0.46f, 1f), false);
            Anchor(rim.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -11f), new Vector2(0f, 11f));
            var inset = NewImage("Inset", root, _inset, new Color(0.10f, 0.055f, 0.08f, 1f), false);
            Anchor(inset.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(2.5f, -8.5f), new Vector2(-2.5f, 8.5f));

            // 채움: 위가 밝은 붉은 막대 + 윗면 광택
            var fillArea = NewRect("Fill Area", root);
            Anchor(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(5f, -6f), new Vector2(-5f, 6f));
            var fill = NewImage("Fill", fillArea, _fill, new Color(0.9f, 0.17f, 0.24f, 1f), false);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var gloss = NewImage("Gloss", fill.rectTransform, _pill, new Color(1f, 1f, 1f, 0.24f), false);
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            gloss.rectTransform.anchorMax = new Vector2(1f, 0.95f);
            gloss.rectTransform.offsetMin = new Vector2(3f, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-3f, 0f);

            // 눈금: 홈 아래의 작은 금빛 마름모(25·50·75%)
            for (int i = 1; i <= 3; i++)
            {
                var tick = NewImage("Tick" + i, root, _diamond, new Color(0.79f, 0.64f, 0.29f, 0.7f), false);
                tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(i * 0.25f, 0.5f);
                tick.rectTransform.sizeDelta = new Vector2(7f, 7f);
                tick.rectTransform.anchoredPosition = new Vector2(0f, -19f);
            }

            // 손잡이: 그림자 · 어두운 금 테두리 · 밝은 금 마름모 · 붉은 보석 · 하이라이트 · 빛무리
            // (슬라이더는 손잡이의 세로 앵커를 0~1로 펴 버리므로, 슬라이드 영역을 높이 0으로 둬서 손잡이가 세로로 늘어나지 않게 한다)
            var slide = NewRect("Handle Slide Area", root);
            Anchor(slide, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(17f, 0f), new Vector2(-17f, 0f));
            slide.sizeDelta = new Vector2(slide.sizeDelta.x, 0f);
            var handle = NewRect("Handle", slide);
            handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(34f, 34f);
            var glow = NewImage("Glow", handle, _soft, new Color(1f, 0.82f, 0.45f, 0f), false);
            Center(glow.rectTransform, 84f, Vector2.zero);
            var shadow = NewImage("Shadow", handle, _soft, new Color(0f, 0f, 0f, 0.55f), false);
            Center(shadow.rectTransform, 46f, new Vector2(0f, -3f));
            var edge = NewImage("Edge", handle, _diamond, new Color(0.42f, 0.27f, 0.09f, 1f), false);
            Center(edge.rectTransform, 34f, Vector2.zero);
            var gold = NewImage("Gold", handle, _diamond, new Color(0.95f, 0.76f, 0.34f, 1f), false);
            Center(gold.rectTransform, 29f, new Vector2(0f, 0.8f));
            var well = NewImage("Well", handle, _diamond, new Color(0.30f, 0.17f, 0.06f, 1f), false);
            Center(well.rectTransform, 19f, Vector2.zero);
            var gem = NewImage("Gem", handle, _diamond, new Color(0.88f, 0.15f, 0.22f, 1f), false);
            Center(gem.rectTransform, 15f, Vector2.zero);
            var shine = NewImage("Shine", handle, _diamond, new Color(1f, 0.85f, 0.85f, 0.75f), false);
            Center(shine.rectTransform, 5f, new Vector2(-2f, 2.5f));

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;
            slider.targetGraphic = gold;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };

            // 숫자
            var percent = NewText("Percent", panel, fontSource, 32f);
            percent.rectTransform.anchorMin = percent.rectTransform.anchorMax = percent.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            percent.rectTransform.sizeDelta = new Vector2(118f, 44f);
            percent.rectTransform.anchoredPosition = new Vector2(167f, y);
            percent.alignment = TextAlignmentOptions.Center;
            percent.color = GoldBright;

            var fx = root.gameObject.AddComponent<SliderFx>();
            fx.Init(slider, glow, handle, fill, enabled, percent, previewOnRelease);

            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            percent.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            slider.onValueChanged.AddListener(v =>
            {
                apply(v);
                percent.text = Mathf.RoundToInt(v * 100f) + "%";
            });
        }

        private static void Center(RectTransform rect, float size, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = offset;
        }

        // ───────────── 손잡이 효과

        private sealed class SliderFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
        {
            private Slider _slider;
            private Image _glow;
            private RectTransform _handle;
            private Image _fill;
            private Func<bool> _enabled;
            private TMP_Text _percent;
            private CanvasGroup _group;
            private bool _hover, _drag, _preview;
            private float _k;

            public void Init(Slider slider, Image glow, RectTransform handle, Image fill, Func<bool> enabled, TMP_Text percent, bool previewOnRelease)
            {
                _slider = slider; _glow = glow; _handle = handle; _fill = fill; _enabled = enabled; _percent = percent; _preview = previewOnRelease;
                _group = GetComponent<CanvasGroup>();
            }

            public void OnPointerEnter(PointerEventData e) => _hover = true;
            public void OnPointerExit(PointerEventData e) => _hover = false;
            public void OnPointerDown(PointerEventData e) => _drag = true;

            public void OnPointerUp(PointerEventData e)
            {
                if (!_drag) return;
                _drag = false;
                GameAudioSettings.CommitVolumes();
                if (_preview) Sfx.Play(SfxId.UnitPlace); // 효과음 크기를 귀로 확인할 수 있게
            }

            private void OnDisable() { _hover = _drag = false; }

            private void Update()
            {
                float target = _drag ? 1f : _hover ? 0.7f : 0f;
                _k = Mathf.MoveTowards(_k, target, Time.unscaledDeltaTime * 7f);
                float scale = 1f + 0.22f * _k;
                _handle.localScale = new Vector3(scale, scale, 1f);
                var c = _glow.color; c.a = 0.55f * _k; _glow.color = c;
                float lift = 1f + 0.18f * _k;   // 손잡이를 잡거나 올리면 채움도 살짝 밝아진다
                _fill.color = new Color(0.9f * lift, 0.17f * lift, 0.24f * lift, 1f);

                // 토글이 꺼져 있으면 흐리게(조절은 가능)
                bool on = _enabled == null || _enabled();
                float alpha = Mathf.MoveTowards(_group.alpha, on ? 1f : 0.45f, Time.unscaledDeltaTime * 5f);
                _group.alpha = alpha;
                if (_percent != null) { var pc = _percent.color; pc.a = alpha; _percent.color = pc; }
            }
        }

        // ───────────── 도우미

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect) => Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        private static TMP_Text NewText(string name, Transform parent, TMP_Text source, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (source != null) { text.font = source.font; text.fontSharedMaterial = source.fontSharedMaterial; }
            text.fontSize = size;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        // ───────────── 직접 그리는 그림(알약·마름모·빛무리)

        private static void EnsureSprites()
        {
            if (_pill != null) return;
            _pill = MakePill(1f, 1f);
            _rim = MakePill(1f, 0.5f);     // 위가 밝은 금속 테두리
            _inset = MakePill(0.55f, 1f);  // 위쪽이 더 어두운, 안으로 파인 홈
            _fill = MakePill(1f, 0.62f);   // 위가 밝은 붉은 채움
            _diamond = MakeDiamond();
            _soft = MakeSoft();
        }

        private static Sprite MakePill(float topShade, float bottomShade)
        {
            const int w = 64, h = 32, r = 15;
            var tex = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float px = Mathf.Clamp(x + 0.5f, r, w - r), py = h * 0.5f;
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(px, py)) - (h * 0.5f - 0.5f);
                    float shade = Mathf.Lerp(bottomShade, topShade, (y + 0.5f) / h);
                    tex.SetPixel(x, y, new Color(shade, shade, shade, Mathf.Clamp01(0.5f - d)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private static Sprite MakeDiamond()
        {
            const int s = 64;
            var tex = NewTex(s, s);
            float c = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = (Mathf.Abs(x - c) + Mathf.Abs(y - c)) - (c - 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d * 0.7f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeSoft()
        {
            const int s = 64;
            var tex = NewTex(s, s);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f)) / (s * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
    }
}
