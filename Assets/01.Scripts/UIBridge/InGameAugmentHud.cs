using System.Collections.Generic;
using OZGL2.Augment;
using OZGL2.Synergy;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 이번 판에 고른 증강을 화면 왼쪽에 희수의 어두운 프레임 패널로 보여 준다(위 가장자리에 걸친 「증강 N」 이름표, 네 모서리 마름모).
    /// 칸은 동그란 아이콘 + 등급(실버·골드·플래티넘) 색 이중 테두리, 골드 이상은 은은하게 빛나고, 등급만큼 마름모가 아래에 붙는다.
    /// 같은 증강을 여러 번 골랐으면 오른쪽 아래에 배지로 횟수가 붙는다. 마우스를 올리면 이름·등급·설명이 뜬다.
    /// 보상 패널 바로 아래 왼쪽 줄에 3칸씩 세로로 쌓고, 넘치면 오른쪽으로 새 줄을 만든다(왼쪽 아래 재화 아이콘을 가리지 않는 범위).
    /// 값은 실제 증강 상태(AugmentRun.PickedList)에서 읽으므로 이어하기로 복원한 증강도 그대로 보인다.
    /// </summary>
    public sealed class InGameAugmentHud : MonoBehaviour
    {
        [SerializeField, Tooltip("증강 아이콘·등급 문장 모음(UIAugmentVisualCatalog)")] private UIAugmentVisualCatalogSO _visuals;
        [SerializeField, Range(0.45f, 0.62f), Tooltip("첫 줄의 세로 위치(화면 위에서부터의 비율)")] private float _topRatio = 0.545f;
        [SerializeField, Min(2)] private int _rows = 3;
        [Header("희수 UI 조각(없으면 단색 상자로 대신)")]
        [SerializeField, Tooltip("패널 프레임(Frame_WavePreview_Flat, 9분할)")] private Sprite _panelFrame;
        [SerializeField, Tooltip("이름표(Frame_SynergyNameplate_Flat, 9분할)")] private Sprite _namePlate;
        [SerializeField, Tooltip("마름모(Ornament_Diamond_Flat)")] private Sprite _diamond;

        private static readonly Color[] TierColors =
        {
            new Color(0.78f, 0.80f, 0.86f), // 실버
            new Color(1f, 0.80f, 0.30f),    // 골드
            new Color(0.55f, 0.90f, 1f),    // 플래티넘
        };

        private sealed class Entry
        {
            public string id;
            public int count;
            public AugmentData data;
        }

        private RealSynergySync _sync;
        private Canvas _canvas;
        private RectTransform _root, _tip;
        private TMP_Text _heading, _tipTitle, _tipBody;
        private Sprite _disc, _ring, _glow;
        private readonly List<(Image img, float baseAlpha, float phase)> _glows = new List<(Image, float, float)>();
        private readonly List<GameObject> _cells = new List<GameObject>();
        private string _signature = string.Empty;
        private float _next;

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void Update()
        {
            // 골드 이상 칸의 빛이 천천히 숨 쉰다
            float t = Time.unscaledTime;
            foreach (var g in _glows)
                if (g.img != null)
                {
                    var c = g.img.color;
                    c.a = g.baseAlpha * (0.7f + 0.3f * Mathf.Sin(t * 1.8f + g.phase));
                    g.img.color = c;
                }
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            if (_sync == null) _sync = FindFirstObjectByType<RealSynergySync>();
            var run = _sync != null ? _sync.Augments : null;
            var entries = Collect(run);
            string sig = Signature(entries);
            if (entries.Count == 0)
            {
                if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
                _signature = sig;
                return;
            }
            EnsureCanvas();
            _canvas.enabled = true;
            if (sig == _signature && _cells.Count > 0) return;
            _signature = sig;
            Rebuild(entries);
        }

        private static List<Entry> Collect(AugmentRun run)
        {
            var result = new List<Entry>();
            if (run == null) return result;
            var map = new Dictionary<string, Entry>();
            foreach (var d in run.PickedList)
            {
                if (d == null || string.IsNullOrEmpty(d.augmentId)) continue;
                if (!map.TryGetValue(d.augmentId, out var e))
                {
                    e = new Entry { id = d.augmentId, data = d };
                    map[d.augmentId] = e;
                    result.Add(e);
                }
                e.count++;
            }
            return result;
        }

        private static string Signature(List<Entry> entries)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var e in entries) sb.Append(e.id).Append(':').Append(e.count).Append('|');
            return sb.ToString();
        }

        // ───────────── 만들기

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            _disc = MakeCircle(64, 0f);
            _ring = MakeCircle(64, 0.84f);
            _glow = MakeGlow(64);
            var go = new GameObject("AugmentHud", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9;
            go.AddComponent<GraphicRaycaster>();

            var rootGo = new GameObject("Root", typeof(RectTransform));
            rootGo.transform.SetParent(go.transform, false);
            _root = (RectTransform)rootGo.transform;
            _root.anchorMin = _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);

            _heading = NewText(_root, "Heading", 24f, new Color(0.96f, 0.82f, 0.45f), TextAlignmentOptions.Center);

            // 설명 말풍선
            var tipGo = new GameObject("Tooltip", typeof(RectTransform), typeof(Image));
            tipGo.transform.SetParent(go.transform, false);
            _tip = (RectTransform)tipGo.transform;
            _tip.anchorMin = _tip.anchorMax = new Vector2(0f, 1f);
            _tip.pivot = new Vector2(0f, 0.5f);
            var bg = tipGo.GetComponent<Image>();
            bg.color = new Color(0.07f, 0.04f, 0.05f, 0.96f);
            bg.raycastTarget = false;
            var outline = tipGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.3f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            _tipTitle = NewText(_tip, "Title", 28f, new Color(1f, 0.86f, 0.5f), TextAlignmentOptions.TopLeft);
            _tipBody = NewText(_tip, "Body", 24f, new Color(0.96f, 0.93f, 0.86f), TextAlignmentOptions.TopLeft);
            _tipBody.textWrappingMode = TextWrappingModes.Normal;
            _tip.gameObject.SetActive(false);
        }

        private void Rebuild(List<Entry> entries)
        {
            foreach (var c in _cells) if (c != null) Destroy(c);
            _cells.Clear();
            _glows.Clear();
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            float cell = 60f * s, gap = 10f * s, pad = 16f * s, plateH = 42f * s;
            int rows = Mathf.Min(_rows, entries.Count), cols = (entries.Count + _rows - 1) / _rows;
            float topPad = plateH * 0.5f + 16f * s;
            float gridW = cols * cell + (cols - 1) * gap;
            float width = Mathf.Max(pad * 2f + gridW, 150f * s);
            float height = topPad + rows * cell + (rows - 1) * gap + pad;
            _root.anchoredPosition = new Vector2(16f * s, -Screen.height * _topRatio);
            _root.sizeDelta = new Vector2(width, height);

            // 패널(어두운 프레임) — 칸보다 뒤에 깐다
            var panel = NewImage(_root, "Panel", _panelFrame, _panelFrame != null ? new Color(1f, 1f, 1f, 0.94f) : new Color(0.08f, 0.04f, 0.06f, 0.88f), false);
            if (_panelFrame != null) panel.type = Image.Type.Sliced;
            Stretch(panel.rectTransform, 0f);
            panel.rectTransform.SetAsFirstSibling();
            _cells.Add(panel.gameObject);
            if (_diamond != null)
                foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
                {
                    var d = NewImage(_root, "Corner", _diamond, Color.white, false);
                    var dr = d.rectTransform;
                    dr.anchorMin = dr.anchorMax = corner; dr.pivot = new Vector2(0.5f, 0.5f);
                    dr.sizeDelta = new Vector2(22f * s, 22f * s); dr.anchoredPosition = Vector2.zero;
                    _cells.Add(d.gameObject);
                }

            // 위 가장자리에 걸친 이름표
            var plate = NewImage(_root, "Plate", _namePlate, _namePlate != null ? Color.white : new Color(0.3f, 0.1f, 0.1f, 1f), false);
            if (_namePlate != null) plate.type = Image.Type.Sliced;
            var pr = plate.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(Mathf.Min(width - 16f * s, 150f * s), plateH); pr.anchoredPosition = Vector2.zero;
            _cells.Add(plate.gameObject);
            int total = 0;
            foreach (var e in entries) total += e.count;
            _heading.transform.SetParent(pr, false);
            _heading.transform.SetAsLastSibling();
            _heading.text = "증강 " + total;
            _heading.enableAutoSizing = true; _heading.fontSizeMin = 14f * s; _heading.fontSizeMax = 24f * s;
            var hr = _heading.rectTransform;
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one;
            hr.offsetMin = new Vector2(8f * s, 2f * s); hr.offsetMax = new Vector2(-8f * s, -2f * s);

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                int col = i / _rows, row = i % _rows;
                var cellGo = new GameObject("Aug_" + e.id, typeof(RectTransform));
                cellGo.transform.SetParent(_root, false);
                var rt = (RectTransform)cellGo.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cell, cell);
                rt.anchoredPosition = new Vector2((width - gridW) * 0.5f + col * (cell + gap), -(topPad + row * (cell + gap)));

                int tierIndex = Mathf.Clamp(e.data.tier, 1, 3);
                Color tier = TierColors[tierIndex - 1];
                if (tierIndex >= 2)
                {
                    var glow = NewImage(rt, "Glow", _glow, new Color(tier.r, tier.g, tier.b, 0.34f), false);
                    var gr = glow.rectTransform;
                    gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f); gr.sizeDelta = new Vector2(cell * 1.55f, cell * 1.55f);
                    _glows.Add((glow, glow.color.a, i * 0.9f));
                }
                var back = NewImage(rt, "Back", _disc, new Color(0.08f, 0.04f, 0.06f, 0.96f), true);
                Stretch(back.rectTransform, 0f);
                var ring = NewImage(rt, "Ring", _ring, new Color(tier.r, tier.g, tier.b, 0.98f), false);
                Stretch(ring.rectTransform, 0f);
                var inner = NewImage(rt, "InnerRing", _ring, new Color(tier.r * 0.7f, tier.g * 0.7f, tier.b * 0.7f, 0.55f), false);
                Stretch(inner.rectTransform, cell * 0.09f);
                var icon = NewImage(rt, "Icon", _visuals != null ? _visuals.GetIcon(e.id) : null, Color.white, false);
                Stretch(icon.rectTransform, cell * 0.2f);
                icon.preserveAspect = true;
                if (icon.sprite == null) icon.enabled = false;

                // 등급만큼 마름모(아래 가운데)
                float pip = 11f * s, pipGap = 3f * s;
                float pipsW = tierIndex * pip + (tierIndex - 1) * pipGap;
                for (int k = 0; k < tierIndex; k++)
                {
                    var p = NewImage(rt, "Pip", _diamond, new Color(tier.r, tier.g, tier.b, 1f), false);
                    var prt = p.rectTransform;
                    prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f); prt.pivot = new Vector2(0.5f, 0.5f);
                    prt.sizeDelta = new Vector2(pip, pip);
                    prt.anchoredPosition = new Vector2(-pipsW * 0.5f + pip * 0.5f + k * (pip + pipGap), -2f * s);
                    if (_diamond == null) prt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }

                if (e.count > 1)
                {
                    var badge = NewImage(rt, "CountBadge", _disc, new Color(0.1f, 0.05f, 0.07f, 1f), false);
                    var br = badge.rectTransform;
                    br.anchorMin = br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0.5f);
                    br.sizeDelta = new Vector2(26f * s, 26f * s); br.anchoredPosition = new Vector2(-4f * s, 8f * s);
                    var badgeRing = NewImage(br, "Ring", _ring, new Color(1f, 0.84f, 0.48f, 1f), false);
                    Stretch(badgeRing.rectTransform, 0f);
                    var count = NewText(br, "Count", 17f * s, Color.white, TextAlignmentOptions.Center);
                    Stretch(count.rectTransform, 0f);
                    count.text = e.count.ToString();
                    count.enableAutoSizing = true; count.fontSizeMin = 10f * s; count.fontSizeMax = 17f * s;
                }
                var hover = back.gameObject.AddComponent<Hover>();
                hover.Hud = this; hover.Entry = e; hover.Cell = rt;
                _cells.Add(cellGo);
            }
        }

        // ───────────── 설명 말풍선

        private void ShowTip(Entry e, RectTransform cell)
        {
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            _tipTitle.text = e.data.displayName + (e.count > 1 ? "  ×" + e.count : string.Empty) + "  <size=70%>" + AugmentData.TierName(e.data.tier) + "</size>";
            _tipBody.text = string.IsNullOrWhiteSpace(e.data.description) ? "설명이 없어요." : e.data.description;
            float width = 440f * s;
            _tipTitle.fontSize = 26f * s; _tipBody.fontSize = 22f * s;
            var body = _tipBody.GetPreferredValues(_tipBody.text, width - 28f * s, 0f);
            float titleH = 36f * s, height = titleH + body.y + 28f * s;
            _tip.sizeDelta = new Vector2(width, height);
            var tr = _tipTitle.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(14f * s, -10f * s); tr.sizeDelta = new Vector2(-28f * s, titleH);
            var br = _tipBody.rectTransform;
            br.anchorMin = new Vector2(0f, 1f); br.anchorMax = new Vector2(1f, 1f); br.pivot = new Vector2(0f, 1f);
            br.anchoredPosition = new Vector2(14f * s, -(10f * s + titleH)); br.sizeDelta = new Vector2(-28f * s, body.y + 6f * s);
            // 아이콘 오른쪽에 띄운다
            var corners = new Vector3[4];
            cell.GetWorldCorners(corners);
            float x = corners[3].x + 10f * s;
            float y = (corners[0].y + corners[1].y) * 0.5f - Screen.height; // 앵커가 왼쪽 위라 화면 아래쪽 좌표를 음수로 바꾼다
            _tip.anchoredPosition = new Vector2(x, Mathf.Clamp(y, -(Screen.height - height * 0.5f - 6f), -(height * 0.5f + 6f)));
            _tip.gameObject.SetActive(true);
            _tip.SetAsLastSibling();
        }

        private void HideTip() { if (_tip != null) _tip.gameObject.SetActive(false); }

        private sealed class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public InGameAugmentHud Hud; public Entry Entry; public RectTransform Cell;
            public void OnPointerEnter(PointerEventData eventData) => Hud.ShowTip(Entry, Cell);
            public void OnPointerExit(PointerEventData eventData) => Hud.HideTip();
        }

        // ───────────── 도우미

        private static TMP_Text NewText(Transform parent, string name, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (UiFontOverride.Current != null) text.font = UiFontOverride.Current;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = true;
            return text;
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite, Color color, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = raycast;
            return image;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static Sprite MakeGlow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeCircle(int size, float inner)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float outer = size * 0.5f - 1f, innerR = outer * inner;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    float a = Mathf.Clamp01(outer - d) * (inner > 0f ? Mathf.Clamp01(d - innerR) : 1f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
