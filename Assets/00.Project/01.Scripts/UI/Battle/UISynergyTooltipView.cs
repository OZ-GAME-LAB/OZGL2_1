using System.Collections.Generic;
using OZGL2.Synergy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UI
{
    /// <summary>시너지 효과를 변경하지 않고 헤더·단계 비교·조합 설명의 표시와 높이만 관리합니다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIAdaptiveTooltipPanel))]
    public sealed class UISynergyTooltipView : MonoBehaviour
    {
        [Header("프리팹 표시 연결")]
        [SerializeField] private UIAdaptiveTooltipPanel _panel;
        [SerializeField] private RectTransform _header;
        [SerializeField] private RawImage _headerGradient;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _count;
        [SerializeField] private RectTransform _headerDivider;
        [SerializeField] private RectTransform _tiers;
        [SerializeField] private TMP_Text _tier1Title;
        [SerializeField] private TMP_Text _tier1Requirement;
        [SerializeField] private TMP_Text _tier1Effect;
        [SerializeField] private TMP_Text _tier2Title;
        [SerializeField] private TMP_Text _tier2Requirement;
        [SerializeField] private TMP_Text _tier2Effect;
        [SerializeField] private TMP_Text _arrow;
        [SerializeField] private RectTransform _comboHeading;
        [SerializeField] private RawImage _comboGradient;
        [SerializeField] private TMP_Text _comboTitle;
        [SerializeField] private RectTransform _comboRoot;
        [SerializeField] private RectTransform _entryTemplate;

        [Header("기준 크기·간격 (1080p)")]
        [SerializeField, Min(1f)] private float _iconSize = 112f;
        [SerializeField, Min(1f)] private float _titleSize = 46f;
        [SerializeField, Min(1f)] private float _sectionTitleSize = 40f;
        [SerializeField, Min(1f)] private float _comboNameSize = 30f;
        [SerializeField, Min(1f)] private float _tierTitleSize = 36f;
        [SerializeField, Min(1f)] private float _bodySize = 26f;
        [SerializeField, Min(0f)] private float _sectionGap = 16f;
        [SerializeField, Min(0f)] private float _lineGap = 6f;
        [SerializeField, Min(0f)] private float _entryGap = 28f;
        [SerializeField, Range(0f, 1f)] private float _headerAlpha = 0.28f;
        [SerializeField, Range(0f, 1f)] private float _goldAlpha = 0.24f;
        [SerializeField] private Color _gold = new Color32(242, 220, 140, 255);
        [SerializeField] private Color _bodyColor = new Color32(245, 242, 232, 255);
        [SerializeField] private Color _activeColor = new Color32(155, 229, 155, 255);
        [SerializeField] private Color _tier1Color = new Color32(156, 210, 255, 255);

        private readonly List<Entry> _entries = new List<Entry>();
        private bool _hasCachedEntries;
        private RectTransform _cachedComboRoot;

        public bool IsConfigured => _panel != null && _panel.TitleFont != null && _panel.BodyFont != null
            && _header != null && _headerGradient != null && _headerGradient.texture != null
            && _icon != null && _title != null && _count != null && _headerDivider != null
            && _tiers != null && _tier1Title != null && _tier1Requirement != null && _tier1Effect != null
            && _tier2Title != null && _tier2Requirement != null && _tier2Effect != null && _arrow != null
            && _comboHeading != null && _comboGradient != null && _comboGradient.texture != null
            && _comboTitle != null && _comboRoot != null && _entryTemplate != null
            && Entry.HasLabels(_entryTemplate);

        /// <summary>읽기 전용 시너지 값으로 표시합니다. 게임 상태나 ScriptableObject는 수정하지 않습니다.</summary>
        public void Render(SynergyTracker tracker, SynergyData def, Sprite icon, Color jobColor,
            float screenScale, float screenWidth)
        {
            if (tracker == null || def == null || !IsConfigured) return;
            float s = Mathf.Max(0.01f, screenScale);
            float width = _panel.GetWidth(s, screenWidth);
            Vector4 padding = _panel.GetPadding(s);
            float contentWidth = Mathf.Max(1f, width - padding.x - padding.z);
            float gap = _sectionGap * s;
            float lineGap = _lineGap * s;
            float iconSize = Mathf.Min(_iconSize * s, contentWidth * 0.3f);
            float titleX = padding.x + iconSize + 18f * s;
            float titleWidth = Mathf.Max(1f, width - padding.z - titleX);
            int count = tracker.CountOf(def.job);

            Configure(_title, def.displayName + " 시너지", _titleSize * s, _gold, true);
            Configure(_count, "현재 " + count + "명 배치", _bodySize * s, _bodyColor);
            float titleHeight = Measure(_title, titleWidth);
            float countHeight = Measure(_count, titleWidth);
            float textHeight = titleHeight + lineGap + countHeight;
            float headerHeight = Mathf.Max(iconSize, textHeight);
            float textTop = padding.y + (headerHeight - textHeight) * 0.5f;
            Place(_header, 0f, 0f, width, padding.y + headerHeight + gap);
            jobColor.a = _headerAlpha;
            _headerGradient.color = jobColor;
            Place(_headerGradient.rectTransform, 10f * s, 10f * s,
                Mathf.Max(1f, width - 20f * s), padding.y + headerHeight + gap - 10f * s);
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _icon.color = Color.white;
            _icon.preserveAspect = true;
            Place(_icon.rectTransform, padding.x, padding.y + (headerHeight - iconSize) * 0.5f, iconSize, iconSize);
            Place(_title.rectTransform, titleX, textTop, titleWidth, titleHeight);
            Place(_count.rectTransform, titleX, textTop + titleHeight + lineGap, titleWidth, countHeight);

            float y = padding.y + headerHeight + gap;
            Place(_headerDivider, padding.x, y, contentWidth, 2f * s);
            y += 2f * s + gap;

            float inset = 14f * s;
            float arrowWidth = 28f * s;
            float columnWidth = Mathf.Max(1f, (contentWidth - inset * 2f - arrowWidth - 24f * s) * 0.5f);
            float leftHeight = LayoutTier(_tier1Title, _tier1Requirement, _tier1Effect,
                "1단계", def.tier1Threshold, Short(def, def.tier1Desc), count >= def.tier1Threshold,
                _tier1Color, inset, inset, columnWidth, s);
            float rightX = contentWidth - inset - columnWidth;
            float rightHeight = LayoutTier(_tier2Title, _tier2Requirement, _tier2Effect,
                "2단계", def.tier2Threshold, Short(def, def.tier2Desc), count >= def.tier2Threshold,
                _gold, rightX, inset, columnWidth, s);
            float tiersHeight = Mathf.Max(leftHeight, rightHeight) + inset * 2f;
            Place(_tiers, padding.x, y, contentWidth, tiersHeight);
            Configure(_arrow, ">", 36f * s, _gold, true, TextAlignmentOptions.Center);
            Place(_arrow.rectTransform, (contentWidth - arrowWidth) * 0.5f, 0f, arrowWidth, tiersHeight);
            y += tiersHeight;

            CacheEntries();
            int usedEntries = 0;
            float comboHeight = 0f;
            foreach (ComboSynergyData combo in tracker.ComboDefs)
            {
                if (combo == null || (combo.jobA != def.job && combo.jobB != def.job)) continue;
                Entry entry = GetEntry(usedEntries);
                entry.Root.gameObject.SetActive(true);
                bool active = tracker.IsComboActive(combo);
                Configure(entry.Name, combo.displayName, _comboNameSize * s, active ? _activeColor : _gold, true);
                Configure(entry.Condition, JobName(tracker, combo.jobA) + "·" + JobName(tracker, combo.jobB)
                    + " 각 " + combo.requiredCountEach + "명", _bodySize * s, _bodyColor);
                Configure(entry.Effect, ComboEffect(combo.desc), _bodySize * s, active ? _activeColor : _bodyColor);
                float nameHeight = Measure(entry.Name, contentWidth);
                float conditionHeight = Measure(entry.Condition, contentWidth);
                float effectHeight = Measure(entry.Effect, contentWidth);
                float conditionY = nameHeight + lineGap;
                float effectY = conditionY + conditionHeight + lineGap;
                float entryHeight = effectY + effectHeight;
                if (usedEntries > 0) comboHeight += _entryGap * s;
                Place(entry.Root, 0f, comboHeight, contentWidth, entryHeight);
                Place(entry.Name.rectTransform, 0f, 0f, contentWidth, nameHeight);
                Place(entry.Condition.rectTransform, 0f, conditionY, contentWidth, conditionHeight);
                Place(entry.Effect.rectTransform, 0f, effectY, contentWidth, effectHeight);
                entry.Divider.gameObject.SetActive(usedEntries > 0);
                Place(entry.Divider.rectTransform, 0f, -_entryGap * s * 0.5f, contentWidth, s);
                comboHeight += entryHeight;
                usedEntries++;
            }
            for (int i = usedEntries; i < _entries.Count; i++) _entries[i].Root.gameObject.SetActive(false);
            bool hasCombos = usedEntries > 0;
            _comboHeading.gameObject.SetActive(hasCombos);
            _comboRoot.gameObject.SetActive(hasCombos);
            if (hasCombos)
            {
                y += gap;
                Configure(_comboTitle, "조합 시너지", _sectionTitleSize * s, _gold, true);
                float headingHeight = Measure(_comboTitle, contentWidth) + 14f * s;
                Place(_comboHeading, 0f, y, width, headingHeight);
                Color bandColor = _gold;
                bandColor.a = _goldAlpha;
                _comboGradient.color = bandColor;
                Place(_comboGradient.rectTransform, 10f * s, 0f, Mathf.Max(1f, width - 20f * s), headingHeight);
                Place(_comboTitle.rectTransform, padding.x, 7f * s, contentWidth, headingHeight - 14f * s);
                y += headingHeight + 20f * s;
                Place(_comboRoot, padding.x, y, contentWidth, comboHeight);
                y += comboHeight;
            }
            // 마지막 효과의 실제 줄바꿈 높이 다음에만 아래 여백을 더합니다.
            _panel.ApplySize(width, y + padding.w, s);
        }

        private float LayoutTier(TMP_Text title, TMP_Text requirement, TMP_Text effect, string label,
            int threshold, string description, bool active, Color labelColor, float x, float y, float width, float scale)
        {
            Configure(title, label, _tierTitleSize * scale, active ? _activeColor : labelColor, true, TextAlignmentOptions.Center);
            Configure(requirement, threshold + "명 필요", _bodySize * scale, _bodyColor, false, TextAlignmentOptions.Center);
            Configure(effect, description, _bodySize * scale, active ? _activeColor : _bodyColor, false, TextAlignmentOptions.Center);
            float titleHeight = Measure(title, width);
            float requirementHeight = Measure(requirement, width);
            float effectHeight = Measure(effect, width);
            float gap = _lineGap * scale;
            Place(title.rectTransform, x, y, width, titleHeight);
            Place(requirement.rectTransform, x, y + titleHeight + gap, width, requirementHeight);
            Place(effect.rectTransform, x, y + titleHeight + requirementHeight + gap * 2f, width, effectHeight);
            return titleHeight + requirementHeight + effectHeight + gap * 2f;
        }

        private void Configure(TMP_Text text, string value, float size, Color color, bool title = false,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            text.font = title ? _panel.TitleFont : _panel.BodyFont;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = alignment;
            text.lineSpacing = 0f;
            text.color = color;
            text.text = value ?? string.Empty;
            text.raycastTarget = false;
        }

        private static float Measure(TMP_Text text, float width)
            => Mathf.Ceil(text.GetPreferredValues(text.text, Mathf.Max(1f, width), Mathf.Infinity).y);

        private static void Place(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void CacheEntries()
        {
            bool valid = _hasCachedEntries && _cachedComboRoot == _comboRoot;
            if (valid)
                foreach (Entry entry in _entries)
                    if (!entry.IsAlive) { valid = false; break; }
            if (valid) return;
            // Prefab Mode 재적용·Undo로 자식이 교체되면 이전 오브젝트 참조를 버립니다.
            _entries.Clear();
            _cachedComboRoot = _comboRoot;
            _hasCachedEntries = true;
            for (int i = 0; i < _comboRoot.childCount; i++)
            {
                var child = _comboRoot.GetChild(i) as RectTransform;
                if (child != _entryTemplate && child != null && child.name.StartsWith("Combo_") && Entry.HasLabels(child))
                    _entries.Add(new Entry(child));
            }
        }

        private Entry GetEntry(int index)
        {
            if (index < _entries.Count) return _entries[index];
            RectTransform root = Instantiate(_entryTemplate, _comboRoot, false);
            root.name = "Combo_" + index;
            var entry = new Entry(root);
            _entries.Add(entry);
            return entry;
        }

        private static string JobName(SynergyTracker tracker, SynergyJob job)
        {
            SynergyData def = tracker.Def(job);
            return def != null ? def.displayName : job.ToString();
        }

        private static string Short(SynergyData def, string description)
        {
            if (string.IsNullOrEmpty(description)) return string.Empty;
            string prefix = def.displayName + " ";
            return description.StartsWith(prefix) ? description.Substring(prefix.Length) : description;
        }

        private static string ComboEffect(string description)
        {
            if (string.IsNullOrEmpty(description)) return string.Empty;
            int dash = description.IndexOf('—');
            return dash >= 0 ? description.Substring(dash + 1).Trim() : description;
        }

        private sealed class Entry
        {
            public readonly RectTransform Root;
            public readonly TMP_Text Name;
            public readonly TMP_Text Condition;
            public readonly TMP_Text Effect;
            public readonly Image Divider;
            public bool IsAlive => Root != null && Name != null && Condition != null && Effect != null && Divider != null;

            public Entry(RectTransform root)
            {
                Root = root;
                Name = root.Find("Name").GetComponent<TMP_Text>();
                Condition = root.Find("Condition").GetComponent<TMP_Text>();
                Effect = root.Find("Effect").GetComponent<TMP_Text>();
                Divider = root.Find("Divider").GetComponent<Image>();
            }

            public static bool HasLabels(RectTransform root) => root != null
                && Has<TMP_Text>(root, "Name") && Has<TMP_Text>(root, "Condition")
                && Has<TMP_Text>(root, "Effect") && Has<Image>(root, "Divider");

            private static bool Has<T>(RectTransform root, string name) where T : Component
            {
                Transform child = root.Find(name);
                return child != null && child.TryGetComponent<T>(out _);
            }
        }
    }
}
