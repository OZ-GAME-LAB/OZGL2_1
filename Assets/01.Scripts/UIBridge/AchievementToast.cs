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
            _canvas.enabled = true;
            Sfx.Play(SfxId.LevelUp);
        }

        private void Animate()
        {
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
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
            float height = _panel.rect.height;
            float shownY = Screen.height * (1f - _topRatio);
            float hiddenY = Screen.height + height;
            _panel.anchoredPosition = new Vector2(Screen.width * 0.5f, Mathf.Lerp(hiddenY, shownY, ease));
            float pulse = 1f + 0.03f * Mathf.Sin(t * 6f);
            _panel.localScale = Vector3.one * (s * pulse);
        }

        private void EnsureUi()
        {
            if (_canvas != null) return;
            _glow = MakeGlow();
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
            _panel.sizeDelta = new Vector2(620f, 132f);
            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.075f, 0.035f, 0.05f, 0.96f);
            bg.raycastTarget = false;
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.76f, 0.32f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            // 금빛 번짐
            var glow = NewImage(_panel, "Glow", _glow, new Color(1f, 0.76f, 0.3f, 0.32f));
            glow.rectTransform.anchorMin = new Vector2(-0.08f, -0.5f); glow.rectTransform.anchorMax = new Vector2(1.08f, 1.5f);
            glow.rectTransform.offsetMin = glow.rectTransform.offsetMax = Vector2.zero;
            glow.transform.SetAsFirstSibling();
            // 안쪽 얇은 선
            var inner = NewImage(_panel, "Inner", null, new Color(0.9f, 0.45f, 0.35f, 0.5f));
            inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
            inner.rectTransform.offsetMin = new Vector2(9f, 9f); inner.rectTransform.offsetMax = new Vector2(-9f, -9f);
            inner.gameObject.AddComponent<Outline>().effectColor = new Color(0.9f, 0.45f, 0.35f, 0.55f);
            inner.color = new Color(0f, 0f, 0f, 0f);

            var frame = NewImage(_panel, "IconFrame", _glow, new Color(0.16f, 0.07f, 0.09f, 1f));
            var fr = frame.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0f, 0.5f);
            fr.pivot = new Vector2(0f, 0.5f);
            fr.sizeDelta = new Vector2(96f, 96f); fr.anchoredPosition = new Vector2(20f, 0f);
            _icon = NewImage(frame.rectTransform, "Icon", null, Color.white);
            _icon.preserveAspect = true;
            var ir = _icon.rectTransform;
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one; ir.offsetMin = new Vector2(10f, 10f); ir.offsetMax = new Vector2(-10f, -10f);

            _title = NewText(_panel, "Title", 26f, new Color(1f, 0.82f, 0.36f), TextAlignmentOptions.Left);
            Place(_title.rectTransform, 132f, 40f, 30f);
            _name = NewText(_panel, "Name", 38f, new Color(1f, 0.97f, 0.88f), TextAlignmentOptions.Left);
            Place(_name.rectTransform, 132f, 70f, 46f);
            _desc = NewText(_panel, "Desc", 24f, new Color(0.84f, 0.8f, 0.72f), TextAlignmentOptions.Left);
            Place(_desc.rectTransform, 132f, 112f, 30f);
            _canvas.enabled = false;
        }

        /// <summary>패널 왼쪽 위 기준으로 글자 칸을 놓는다(left·top 은 패널 안쪽 위치, 높이는 글자 칸 높이).</summary>
        private static void Place(RectTransform rt, float left, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(left, -top - height * 0.5f); rt.offsetMax = new Vector2(-24f, -top + height * 0.5f);
        }

        private static TMP_Text NewText(Transform parent, string name, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (UiFontOverride.Current != null) text.font = UiFontOverride.Current;
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
