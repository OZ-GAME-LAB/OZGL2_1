using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using OZGL2.Progression;
using OZGL2.Synergy;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 화면 아래 스킬 슬롯에 마우스를 올리면 그 스킬의 설명을 짧게 보여 준다(이름 · 종류/발동/재사용 · 설명 · 피해량).
    /// 슬롯 아이콘은 클릭을 받지 않게 돼 있어서(raycastTarget 꺼짐) 포인터 이벤트 대신 마우스 위치를 직접 슬롯 영역과 비교한다.
    /// 팀 파일(UICombatSkillSlotView)은 건드리지 않고, 슬롯이 이미 가진 스킬 정보(카탈로그 항목)를 리플렉션으로 읽는다 — 스킬 세팅 화면과 같은 설명이다.
    /// 피해 숫자는 지금 적용 중인 배율(마왕 레벨 · 특성 · 증강)을 곱해 실제 값으로 보여 준다.
    /// 마우스를 누르고 있는 동안(범위 조준·드래그)에는 숨겨서 조작을 가리지 않는다.
    /// </summary>
    public sealed class InGameSkillTooltip : MonoBehaviour
    {
        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FEntry = typeof(UICombatSkillSlotView).GetField("_runtimeEntry", Priv);
        private static readonly FieldInfo FHasEntry = typeof(UICombatSkillSlotView).GetField("_hasRuntimeEntry", Priv);
        private static readonly FieldInfo FNameText = typeof(UICombatSkillSlotView).GetField("_skillNameText", Priv);

        [SerializeField, Min(0f), Tooltip("마우스를 올리고 이 시간(초)이 지나면 보인다")] private float _showDelay = 0.12f;

        private Canvas _canvas;
        private RectTransform _panel;
        private TMP_Text _title, _meta, _body;
        private Image _bg;
        private readonly List<UICombatSkillSlotView> _slots = new List<UICombatSkillSlotView>();
        private float _nextScan, _hoverTime;
        private UICombatSkillSlotView _hovered;
        private RealSynergySync _sync;
        private readonly Vector3[] _corners = new Vector3[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        private static void Install()
        {
            if (FindFirstObjectByType<OZGL2.InGame.InGamePrototypeBootstrap>() == null) return;
            if (FindFirstObjectByType<InGameSkillTooltip>() != null) return;
            new GameObject("SkillTooltip").AddComponent<InGameSkillTooltip>();
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || FHasEntry == null || FEntry == null) { Hide(); return; }

            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.5f;
                _slots.Clear();
                foreach (var v in FindObjectsByType<UICombatSkillSlotView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) _slots.Add(v);
            }

            var pos = mouse.position.ReadValue();
            UICombatSkillSlotView over = null;
            bool pressing = mouse.leftButton.isPressed || mouse.rightButton.isPressed;
            if (!pressing)
                foreach (var v in _slots)
                {
                    if (v == null || !v.isActiveAndEnabled || EntryOf(v) == null) continue;
                    if (SlotRect(v).Contains(pos)) { over = v; break; }
                }

            if (over == null) { _hovered = null; _hoverTime = 0f; Hide(); return; }
            if (over != _hovered) { _hovered = over; _hoverTime = 0f; }
            _hoverTime += Time.unscaledDeltaTime;
            if (_hoverTime < _showDelay) { Hide(); return; }

            Build();
            Fill(EntryOf(over));
            Place(SlotRect(over));
            _canvas.enabled = true;
        }

        private void OnDisable() => Hide();

        private void Hide()
        {
            if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
        }

        private static UISkillPreviewCatalogSO.Entry EntryOf(UICombatSkillSlotView view)
        {
            if (!(bool)FHasEntry.GetValue(view)) return null;
            var entry = FEntry.GetValue(view) as UISkillPreviewCatalogSO.Entry;
            return entry != null && !string.IsNullOrWhiteSpace(entry.Id) ? entry : null;
        }

        private Rect SlotRect(UICombatSkillSlotView view)
        {
            var rt = (RectTransform)view.transform;
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            rt.GetWorldCorners(_corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]), b = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // ───────────── 내용

        private float PowerMult()
        {
            if (_sync == null) _sync = FindFirstObjectByType<RealSynergySync>();
            return _sync != null ? _sync.SkillPowerMultNow : SkillLevelScaling.CurrentPowerMult();
        }

        private static string Number(float v) =>
            v >= 10f ? Mathf.RoundToInt(v).ToString() : v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        private void Fill(UISkillPreviewCatalogSO.Entry entry)
        {
            float mult = PowerMult();
            string kind = entry.IsUltimate ? "궁극기" : entry.Category == eSkillPreviewCategory.BUFF ? "버프" : entry.Category == eSkillPreviewCategory.DEBUFF ? "디버프" : "딜";
            string activation = string.IsNullOrEmpty(entry.Activation) ? string.Empty : " · " + entry.Activation + " 발동";
            _title.text = entry.DisplayName;
            _meta.text = kind + activation + " · 재사용 " + entry.Cooldown;

            string body = Regex.Replace(entry.Description ?? string.Empty, @"\s*\n\s*", " ").Trim();
            if (mult > 1.004f)
                body = Regex.Replace(body, @"(\d+(?:\.\d+)?)(?=의 피해)", m =>
                    Number(float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture) * mult));
            string label = entry.EffectLabel;
            if (label == "피해" || label == "연쇄 피해" || label == "지속 피해" || label == "연속 피해")
            {
                var match = Regex.Match(entry.EffectValue ?? string.Empty, @"\d+(\.\d+)?");
                if (match.Success)
                {
                    float scaled = float.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture) * mult;
                    string value = entry.EffectValue.Remove(match.Index, match.Length).Insert(match.Index, Number(scaled));
                    body += "\n<color=#FFD27A>" + label + " " + value + (mult > 1.004f ? "  (+" + Mathf.RoundToInt((mult - 1f) * 100f) + "%)" : string.Empty) + "</color>";
                }
            }
            else if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(entry.EffectValue))
                body += "\n<color=#FFD27A>" + label + " " + entry.EffectValue + "</color>";
            _body.text = body;
        }

        // ───────────── 만들기·자리잡기

        private void Build()
        {
            if (_canvas != null) return;
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 29; // 배속 막대(28) 위, 시너지 설명(30)·팝업 아래
            _canvas.enabled = false;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panelGo.transform.SetParent(go.transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = _panel.anchorMax = Vector2.zero;
            _panel.pivot = new Vector2(0.5f, 0f);
            _bg = panelGo.GetComponent<Image>();
            _bg.color = new Color(0.06f, 0.03f, 0.05f, 0.96f);
            _bg.raycastTarget = false;
            var border = panelGo.GetComponent<Outline>();
            border.effectColor = new Color(0.79f, 0.64f, 0.29f, 0.9f); border.effectDistance = new Vector2(2f, -2f);

            TMP_FontAsset font = null;
            foreach (var v in _slots)
                if (v != null && FNameText != null && FNameText.GetValue(v) is TMP_Text t && t != null && t.font != null) { font = t.font; break; }
            if (font == null) font = UiFontOverride.Current;

            _title = NewText("Title", 30f, new Color(1f, 0.85f, 0.5f), FontStyles.Bold, font);
            _meta = NewText("Meta", 22f, new Color(0.78f, 0.72f, 0.62f), FontStyles.Normal, font);
            _body = NewText("Body", 25f, new Color(1f, 0.96f, 0.88f), FontStyles.Normal, font);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.richText = true;
        }

        private TMP_Text NewText(string name, float size, Color color, FontStyles style, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_panel, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size; text.color = color; text.fontStyle = style;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private void Place(Rect slot)
        {
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            float width = 460f * s, pad = 18f * s, inner = width - pad * 2f;
            _panel.localScale = Vector3.one;

            float y = -pad;
            float titleH = LayoutText(_title, ref y, inner, pad, s, 30f);
            y -= 2f * s;
            LayoutText(_meta, ref y, inner, pad, s, 22f);
            y -= 10f * s;
            LayoutText(_body, ref y, inner, pad, s, 25f);
            float height = -y + pad;
            _panel.sizeDelta = new Vector2(width, height);

            float x = Mathf.Clamp(slot.center.x, width * 0.5f + 8f * s, Screen.width - width * 0.5f - 8f * s);
            float py = Mathf.Min(slot.yMax + 14f * s, Screen.height - height - 8f * s);
            _panel.anchoredPosition = new Vector2(x, py);
        }

        /// <summary>글자를 폭에 맞춰 놓고 아래로 쌓는다. 크기는 화면 비율(s)에 맞춘다.</summary>
        private static float LayoutText(TMP_Text text, ref float y, float inner, float pad, float s, float baseSize)
        {
            text.fontSize = baseSize * s;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(inner, 10f);
            float h = text.GetPreferredValues(text.text, inner, 0f).y;
            rt.sizeDelta = new Vector2(inner, h);
            rt.anchoredPosition = new Vector2(pad, y);
            y -= h;
            return h;
        }
    }
}
