using System.Collections;
using OZGL2.InGame;
using OZGL2.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 웨이브 결과창(InGameWaveResultView의 Popup_WaveResult)을 실행 중에 꾸민다. 표시용 뷰의 글자·확인 단추는 그대로 쓰고
    /// (결과 집계·진행 순서는 건드리지 않는다), 그 위에 겉모습만 얹는다. 프리팹·팀 스크립트는 수정하지 않는다.
    /// - 어두운 바탕 + 금빛 테두리 프레임 + 붉은 웨이브 리본, 큰 「클리어」 글자와 양옆 장식, 금빛 구분선
    /// - 획득 경험치가 0에서 올라가는 숫자와, 마왕 레벨 경험치 바가 차오르는 연출(레벨이 오르면 「레벨 업!」)
    /// - 창이 열릴 때 살짝 커지며 나타나고, 확인 단추는 붉은 장식 단추로 바뀌어 마우스를 올리면 커진다
    /// </summary>
    public sealed class InGameWaveResultStyle : MonoBehaviour
    {
        [SerializeField, Tooltip("9분할 장식 프레임(Frame_CostPlate)")] private Sprite _frameSprite;
        [SerializeField, Tooltip("제목 양옆 마름모 장식(Frame_DiamondRed)")] private Sprite _ornamentSprite;

        private static readonly Color Gold = new Color(1f, 0.82f, 0.42f), Cream = new Color(0.96f, 0.92f, 0.82f);

        private InGameWaveResultView _view;
        private InGameWaveResultPresenter _presenter;
        private bool _built, _wasActive;
        private Coroutine _play;
        private float _nextFind;

        private RectTransform _frame, _panel, _banner, _outcomeRect, _fill;
        private CanvasGroup _group;
        private Image _glow, _fillImage, _confirmImage;
        private TMP_Text _wave, _outcome, _xp, _xpLabel, _level, _levelValue, _levelUp;
        private Sprite _dot, _glowSprite;

        private void Update()
        {
            if (_view == null)
            {
                if (Time.unscaledTime < _nextFind) return;
                _nextFind = Time.unscaledTime + 0.5f;
                _view = FindFirstObjectByType<InGameWaveResultView>(FindObjectsInactive.Include);
                _presenter = FindFirstObjectByType<InGameWaveResultPresenter>(FindObjectsInactive.Include);
                if (_view == null) return;
            }
            bool active = _view.gameObject.activeInHierarchy;
            if (active && !_wasActive)
            {
                if (!_built) _built = Build();
                if (_built)
                {
                    if (_play != null) StopCoroutine(_play);
                    _play = StartCoroutine(Play());
                }
            }
            _wasActive = active;
        }

        // ───────────── 한 번만 만든다

        private bool Build()
        {
            var root = (RectTransform)_view.transform;
            _frame = root.Find("Frame") as RectTransform;
            _panel = _frame != null ? _frame.Find("Panel") as RectTransform : null;
            if (_frame == null || _panel == null) return false;
            _wave = FindText("Wave"); _outcome = FindText("Outcome"); _xp = FindText("Experience");
            var confirm = _panel.Find("Confirm") as RectTransform;
            if (_wave == null || _outcome == null || _xp == null || confirm == null) return false;

            _dot = MakeDot(); _glowSprite = MakeGlow();
            var frameImage = _frame.GetComponent<Image>(); if (frameImage != null) frameImage.enabled = false; // 납작한 금색 테두리는 가린다
            var panelImage = _panel.GetComponent<Image>(); if (panelImage != null) panelImage.enabled = false;
            _frame.sizeDelta = _panel.sizeDelta = new Vector2(820f, 520f);
            _group = _frame.GetComponent<CanvasGroup>();
            if (_group == null) _group = _frame.gameObject.AddComponent<CanvasGroup>();

            // 바탕·프레임·빛(맨 뒤에 깐다)
            var decor = NewRect("Styled", _panel);
            Stretch(decor); decor.SetAsFirstSibling();
            _glow = NewImage("Glow", decor, _glowSprite, new Color(1f, 0.62f, 0.28f, 0.16f));
            Center(_glow.rectTransform, new Vector2(1250f, 820f), Vector2.zero);
            var bg = NewImage("Bg", decor, null, new Color(0.06f, 0.03f, 0.05f, 0.97f));
            Stretch(bg.rectTransform); bg.rectTransform.offsetMin = new Vector2(6f, 6f); bg.rectTransform.offsetMax = new Vector2(-6f, -6f);
            if (_frameSprite != null)
            {
                var fr = NewImage("FrameArt", decor, _frameSprite, Color.white);
                fr.type = Image.Type.Sliced; fr.pixelsPerUnitMultiplier = 2.6f;
                Stretch(fr.rectTransform);
            }

            // 위쪽 붉은 리본(웨이브 번호)
            if (_frameSprite != null)
            {
                var banner = NewImage("Banner", decor, _frameSprite, new Color(0.62f, 0.12f, 0.17f));
                banner.type = Image.Type.Sliced; banner.pixelsPerUnitMultiplier = 3.2f;
                _banner = banner.rectTransform;
                Center(_banner, new Vector2(380f, 72f), new Vector2(0f, 262f));
            }

            // 구분선과 가운데 마름모
            var line = NewImage("Divider", decor, null, new Color(Gold.r, Gold.g, Gold.b, 0.7f));
            Center(line.rectTransform, new Vector2(560f, 3f), new Vector2(0f, 38f));
            if (_ornamentSprite != null)
            {
                foreach (float x in new[] { 0f })
                {
                    var gem = NewImage("DividerGem", decor, _ornamentSprite, Color.white);
                    Center(gem.rectTransform, new Vector2(34f, 34f), new Vector2(x, 38f));
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    var orn = NewImage("TitleOrnament", decor, _ornamentSprite, Color.white);
                    Center(orn.rectTransform, new Vector2(40f, 40f), new Vector2(side * 270f, 118f));
                }
            }

            // 경험치 바(트랙 + 차오르는 부분)
            var track = NewImage("XpTrack", decor, null, new Color(1f, 1f, 1f, 0.12f));
            Center(track.rectTransform, new Vector2(560f, 22f), new Vector2(0f, -150f));
            var trackLine = NewImage("XpTrackLine", track.rectTransform, null, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
            Stretch(trackLine.rectTransform); trackLine.rectTransform.offsetMin = new Vector2(-2f, -2f); trackLine.rectTransform.offsetMax = new Vector2(2f, 2f);
            trackLine.transform.SetAsFirstSibling();
            _fillImage = NewImage("XpFill", track.rectTransform, null, new Color(0.55f, 0.88f, 1f));
            _fill = _fillImage.rectTransform;
            _fill.anchorMin = new Vector2(0f, 0f); _fill.anchorMax = new Vector2(0f, 1f); _fill.pivot = new Vector2(0f, 0.5f);
            _fill.offsetMin = Vector2.zero; _fill.offsetMax = Vector2.zero;

            // 글자 모양과 자리
            Place(_wave, new Vector2(0f, 262f), new Vector2(380f, 72f), 36f, Cream, TextAlignmentOptions.Center);
            Place(_outcome, new Vector2(0f, 118f), new Vector2(480f, 120f), 104f, Gold, TextAlignmentOptions.Center);
            _outcomeRect = _outcome.rectTransform;
            _xpLabel = NewText("XpLabel", _panel, _xp.font, "획득 경험치", 34f, Cream, TextAlignmentOptions.Center);
            Center(_xpLabel.rectTransform, new Vector2(500f, 46f), new Vector2(0f, -4f));
            Place(_xp, new Vector2(0f, -68f), new Vector2(600f, 100f), 82f, new Color(0.6f, 0.9f, 1f), TextAlignmentOptions.Center);
            _level = NewText("Level", _panel, _xp.font, "", 30f, Cream, TextAlignmentOptions.Left);
            Center(_level.rectTransform, new Vector2(260f, 40f), new Vector2(-150f, -120f));
            _levelValue = NewText("LevelValue", _panel, _xp.font, "", 28f, new Color(0.8f, 0.86f, 0.9f), TextAlignmentOptions.Right);
            Center(_levelValue.rectTransform, new Vector2(260f, 40f), new Vector2(150f, -120f));
            _levelUp = NewText("LevelUp", _panel, _xp.font, "레벨 업!", 52f, new Color(0.62f, 1f, 0.62f), TextAlignmentOptions.Center);
            Center(_levelUp.rectTransform, new Vector2(400f, 70f), new Vector2(0f, -128f));
            _levelUp.gameObject.SetActive(false);
            foreach (var t in new[] { _wave, _outcome, _xp, _xpLabel, _level, _levelValue, _levelUp }) AddShadow(t);
            _outcome.outlineWidth = 0.22f; _outcome.outlineColor = new Color32(70, 30, 10, 255);

            // 확인 단추: 붉은 장식 단추 + 호버 반응
            var confirmRect = confirm;
            Center(confirmRect, new Vector2(320f, 78f), new Vector2(0f, -216f));
            _confirmImage = confirm.GetComponent<Image>();
            if (_confirmImage != null && _frameSprite != null)
            {
                _confirmImage.sprite = _frameSprite; _confirmImage.type = Image.Type.Sliced; _confirmImage.pixelsPerUnitMultiplier = 3.2f;
                _confirmImage.color = new Color(0.66f, 0.13f, 0.18f);
            }
            var label = confirm.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.fontSize = 38f; label.color = Cream; label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero; AddShadow(label); }
            if (confirm.GetComponent<WaveResultHoverScale>() == null) confirm.gameObject.AddComponent<WaveResultHoverScale>();
            return true;
        }

        // ───────────── 열릴 때마다 보여 주는 연출

        private IEnumerator Play()
        {
            var result = _presenter != null ? _presenter.Pending : null;
            bool cleared = result == null || result.IsCleared;
            int earned = result != null ? (int)System.Math.Min(int.MaxValue, result.EarnedExperience) : 0;
            int wave = result != null ? result.WaveNumber : 0;
            var mawang = MawangXpBridge.Mawang;

            Color accent = cleared ? Gold : new Color(0.95f, 0.38f, 0.36f);
            _outcome.color = accent;
            _glow.color = cleared ? new Color(1f, 0.62f, 0.28f, 0.16f) : new Color(0.9f, 0.18f, 0.2f, 0.18f);
            _wave.text = wave > 0 ? "웨이브 " + wave : _wave.text;
            _outcome.text = cleared ? "클리어" : "실패";

            // 레벨 경험치: 이번에 올라간 만큼을 보여 준다(레벨이 올랐으면 처음부터 채운다)
            int level = mawang != null ? mawang.Level : 1;
            int need = mawang != null ? Mathf.Max(1, mawang.XpToNext) : 1;
            int xpNow = mawang != null ? mawang.Xp : 0;
            bool leveled = mawang != null && xpNow < earned;
            float endFrac = Mathf.Clamp01(xpNow / (float)need);
            float startFrac = leveled ? 0f : Mathf.Clamp01((xpNow - earned) / (float)need);
            _level.text = "Lv " + level;
            _levelValue.text = xpNow + " / " + need;
            _levelUp.gameObject.SetActive(false);
            _xpLabel.text = "획득 경험치";
            _fill.anchorMax = new Vector2(startFrac, 1f);
            _xp.text = "+0 XP";

            // 팝업: 살짝 커지며 나타난다
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.4f;
                _group.alpha = Mathf.Clamp01(k * 2.2f);
                _frame.localScale = Vector3.one * Mathf.LerpUnclamped(0.78f, 1f, EaseOutBack(k));
                _outcomeRect.localScale = Vector3.one * Mathf.Lerp(1.45f, 1f, EaseOutCubic(Mathf.Clamp01((k - 0.15f) / 0.85f)));
                yield return null;
            }
            _group.alpha = 1f; _frame.localScale = Vector3.one; _outcomeRect.localScale = Vector3.one;

            // 경험치 숫자와 바
            const float count = 0.9f;
            for (float t = 0f; t < count; t += Time.unscaledDeltaTime)
            {
                float k = EaseOutCubic(t / count);
                _xp.text = "+" + Mathf.RoundToInt(earned * k).ToString("N0") + " XP";
                _fill.anchorMax = new Vector2(Mathf.Lerp(startFrac, endFrac, k), 1f);
                _glow.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * 6f));
                yield return null;
            }
            _xp.text = "+" + earned.ToString("N0") + " XP";
            _fill.anchorMax = new Vector2(endFrac, 1f);

            if (leveled)
            {
                _levelUp.gameObject.SetActive(true);
                for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
                {
                    _levelUp.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, EaseOutBack(t / 0.4f));
                    yield return null;
                }
                _levelUp.rectTransform.localScale = Vector3.one;
            }
            // 이후 숨 쉬는 빛
            while (_view != null && _view.gameObject.activeInHierarchy)
            {
                float breathe = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
                _glow.rectTransform.localScale = Vector3.one * (1f + 0.05f * breathe);
                yield return null;
            }
        }

        // ───────────── 도우미

        private TMP_Text FindText(string name)
        {
            foreach (var t in _panel.GetComponentsInChildren<TMP_Text>(true)) if (t.name == name) return t;
            return null;
        }

        private static void Place(TMP_Text text, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
        {
            text.fontSize = fontSize; text.color = color; text.alignment = align; text.textWrappingMode = TextWrappingModes.NoWrap; text.enableAutoSizing = false;
            Center(text.rectTransform, size, pos);
        }

        private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text; tmp.fontSize = size; tmp.color = color; tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.raycastTarget = false;
            return tmp;
        }

        private static void AddShadow(TMP_Text text)
        {
            if (text.GetComponent<Shadow>() != null) return;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -3f);
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

        private static void Center(RectTransform rt, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

        private static Sprite MakeDot()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((r - 1f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r))) * 0.9f)));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeGlow()
        {
            const int n = 128;
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

    /// <summary>확인 단추 위에 마우스를 올리면 살짝 커지고, 누르면 살짝 눌린다.</summary>
    internal sealed class WaveResultHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private bool _hover, _down;
        private float _scale = 1f;

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;
        private void OnDisable() { _hover = false; _down = false; _scale = 1f; transform.localScale = Vector3.one; }

        private void Update()
        {
            _scale = Mathf.Lerp(_scale, _down ? 0.96f : _hover ? 1.07f : 1f, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
            transform.localScale = Vector3.one * _scale;
        }
    }
}
