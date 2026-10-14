using System.Collections.Generic;
using OZGL2.Progression;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 업적을 달성하면 화면 위쪽에 「업적 달성!」 팝업을 띄운다(전투 중이든 로비든 같다).
    /// - 진행도(AchievementStore)가 목표에 닿은 업적을 찾아 한 번씩만 알려 주고, 보여 준 업적은 저장해 두어 다시 띄우지 않는다.
    /// - 한꺼번에 달성하면 차례로 하나씩 보여 준다(전투 중에 방해되지 않게 3초 남짓).
    /// - 위에서 내려왔다가 올라가는 모양이고, 효과음(레벨업 소리)이 함께 난다. 「처음부터」를 누르면 알림 기록도 같이 지워진다.
    /// 배속이나 일시정지와 상관없이 같은 속도로 움직인다.
    /// </summary>
    public sealed class AchievementToast : MonoBehaviour
    {
        [SerializeField, Tooltip("업적 목록(AchievementCatalog)")] private UIAchievementCatalogSO _catalog;
        [SerializeField, Min(1f)] private float _holdSeconds = 3.2f;
        [SerializeField, Range(0.05f, 0.4f), Tooltip("화면 위에서 팝업 가운데까지의 위치(화면 높이 비율)")] private float _topRatio = 0.2f;
        [SerializeField, Min(0f), Tooltip("1080p 기준 왼쪽 여백. 전투 웨이브/증강 목록과 겹치지 않도록 미리보기에서 확인합니다.")] private float _leftInset = 32f;

        private readonly Queue<UIAchievementCatalogSO.Entry> _queue = new Queue<UIAchievementCatalogSO.Entry>();
        private readonly HashSet<string> _queued = new HashSet<string>();
        private Canvas _canvas;
        private RectTransform _panel;
        private Image _icon;
        private TMP_Text _title, _name, _desc;
        private UIAchievementCatalogSO.Entry _current;
        private float _shownAt, _nextCheck;
        private Sprite _glow;

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + 0.5f;
                Scan();
            }
            if (_current == null && _queue.Count > 0) Begin(_queue.Dequeue());
            if (_current != null) Animate();
        }

        private void Scan()
        {
            var entries = _catalog != null ? _catalog.Entries : null;
            if (entries == null) return;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || _queued.Contains(entry.Id)) continue;
                if (AchievementStore.IsNotified(entry.Id)) continue;
                if (AchievementStore.ProgressOf(entry.Id) < entry.Target) continue;
                _queued.Add(entry.Id);
                _queue.Enqueue(entry);
            }
        }

        // ───────────── 보여 주기

        private void Begin(UIAchievementCatalogSO.Entry entry)
        {
            EnsureUi();
            _current = entry;
            _shownAt = Time.unscaledTime;
            AchievementStore.MarkNotified(entry.Id);
            _title.text = "업적 달성!";
            _name.text = entry.DisplayName;
            _desc.text = entry.Description;
            _icon.sprite = entry.Icon;
            _icon.enabled = entry.Icon != null;
            LayoutContent();
            _canvas.enabled = true;
            Sfx.Play(SfxId.LevelUp);
        }

        private void Animate()
        {
            float s = Mathf.Max(0.01f, Mathf.Min(Screen.height / 1080f, (Screen.width - 24f) / _panel.rect.width));
            s = Mathf.Min(s, Mathf.Max(0.01f, (Screen.height - 24f) / _panel.rect.height));
            float t = Time.unscaledTime - _shownAt;
            const float slide = 0.4f;
            float total = slide + _holdSeconds + slide;
            if (t >= total)
            {
                _current = null;
                _canvas.enabled = false;
                return;
            }
            float k = t < slide ? t / slide : t > slide + _holdSeconds ? 1f - (t - slide - _holdSeconds) / slide : 1f;
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);
            float height = _panel.rect.height * s;
            float shownY = Mathf.Clamp(Screen.height * (1f - _topRatio), height * 0.5f + 8f, Screen.height - height * 0.5f - 8f);
            float hiddenY = Screen.height + height;
            float halfWidth = _panel.rect.width * s * 0.5f;
            float x = Mathf.Clamp(_leftInset * s + halfWidth, halfWidth + 8f, Screen.width - halfWidth - 8f);
            _panel.anchoredPosition = new Vector2(x, Mathf.Lerp(hiddenY, shownY, ease));
            _panel.localScale = Vector3.one * s;
        }

        private void EnsureUi()
        {
            if (_canvas != null) return;
            var go = new GameObject("AchievementToast", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 60;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(go.transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = _panel.anchorMax = Vector2.zero;
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(520f, 148f);
            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.075f, 0.035f, 0.05f, 0.96f);
            bg.raycastTarget = false;
            var style = UIHeraldrySupplement.Style;
            if (style != null) bg.color = style.BackgroundColor;
            if (!UIHeraldrySupplement.ApplyPanel(bg) && UIHeraldrySupplement.AddFrame(_panel) == null)
            {
                var outline = panelGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.95f, 0.76f, 0.32f, 1f);
                outline.effectDistance = new Vector2(3f, -3f);
                _glow = MakeGlow();
                var glow = NewImage(_panel, "Glow", _glow, new Color(1f, 0.76f, 0.3f, 0.32f));
                glow.rectTransform.anchorMin = new Vector2(-0.08f, -0.5f); glow.rectTransform.anchorMax = new Vector2(1.08f, 1.5f);
                glow.rectTransform.offsetMin = glow.rectTransform.offsetMax = Vector2.zero;
                glow.transform.SetAsFirstSibling();
            }

            var medallion = style != null ? style.MedallionSprite : null;
            var frame = NewImage(_panel, "IconFrame", medallion != null ? medallion : _glow, medallion != null ? Color.white : new Color(0.16f, 0.07f, 0.09f, 1f));
            frame.preserveAspect = true;
            var fr = frame.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0f, 0.5f);
            fr.pivot = new Vector2(0f, 0.5f);
            fr.sizeDelta = new Vector2(106f, 106f); fr.anchoredPosition = new Vector2(18f, 0f);
            _icon = NewImage(frame.rectTransform, "Icon", null, Color.white);
            _icon.preserveAspect = true;
            var ir = _icon.rectTransform;
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one; ir.offsetMin = new Vector2(23f, 23f); ir.offsetMax = new Vector2(-23f, -23f);

            _title = NewText(_panel, "Title", 23f, new Color(1f, 0.82f, 0.36f), TextAlignmentOptions.TopLeft);
            _name = NewText(_panel, "Name", 32f, new Color(1f, 0.97f, 0.88f), TextAlignmentOptions.TopLeft);
            _desc = NewText(_panel, "Desc", 22f, new Color(0.84f, 0.8f, 0.72f), TextAlignmentOptions.TopLeft);
            UIHeraldrySupplement.ApplyTitle(_name);
            _name.textWrappingMode = TextWrappingModes.Normal;
            _desc.textWrappingMode = TextWrappingModes.Normal;
            LayoutContent();
            _canvas.enabled = false;
        }

        private void LayoutContent()
        {
            const float left = 140f, width = 348f;
            float top = 20f;
            Place(_title, left, width, ref top);
            top += 2f;
            Place(_name, left, width, ref top);
            top += 4f;
            Place(_desc, left, width, ref top);
            _panel.sizeDelta = new Vector2(520f, Mathf.Max(148f, top + 22f));
        }

        private static void Place(TMP_Text text, float left, float width, ref float top)
        {
            float height = text.GetPreferredValues(text.text, width, 0f).y;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, -top); rt.sizeDelta = new Vector2(width, height);
            top += height;
        }

        private static TMP_Text NewText(Transform parent, string name, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (UiFontOverride.Current != null) text.font = UiFontOverride.Current;
            UIHeraldrySupplement.ApplyBody(text);
            text.fontSize = size; text.color = color; text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        private static Sprite MakeGlow()
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * 64 + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1.15f - d) * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
