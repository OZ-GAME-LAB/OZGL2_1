using System.Collections.Generic;
using System.Reflection;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 하단 스킬 슬롯을 아래쪽 붉은 바(BottomSkillBackground)에 딱 맞게 넣고, 슬롯 아래에 스킬 이름을 보여 준다.
    /// - 슬롯 하나하나를 "아래쪽 가운데"를 기준으로 줄이므로 옆으로 밀리지 않고, 슬롯 사이 간격은 줄어든 폭만큼 보정한다.
    /// - 컨테이너(레이아웃 그룹)의 크기·위치는 건드리지 않고 아래쪽 여백(padding.bottom)만 조정한다.
    /// - 맞추는 값은 전투 화면이 처음 뜬 프레임에, 그리기 전에 한 번에 계산해서 저장한다(몇 번 나눠 맞추면 슬롯이 꿈틀거려 보인다).
    ///   이후에는 그 값을 매 프레임 그대로 적용하므로 배치→전투 전환에서도 처음부터 최종 위치로 나온다.
    /// - 슬롯 아래 이름표는 바의 아래쪽 여백을 비워 두고 그 자리에 그린다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class InGameSkillBarScale : MonoBehaviour
    {
        [SerializeField, Range(0.3f, 1.2f)] private float _scale = 0.7f;
        [SerializeField] private bool _fitToBottomBar = true;
        [SerializeField, Range(0.5f, 1f), Tooltip("슬롯이 차지하는 높이 / 바의 쓸 수 있는 높이")]
        private float _fitFraction = 0.95f;
        [SerializeField, Range(0f, 0.3f), Tooltip("바 윗부분 장식 때문에 비워 두는 비율")]
        private float _topDecorFraction = 0.1f;
        [SerializeField] private bool _showSkillNames = true;
        [SerializeField, Range(0.05f, 0.3f), Tooltip("스킬 이름을 쓰려고 바 아래쪽에 비워 두는 높이 비율")]
        private float _nameReserveFraction = 0.17f;
        [SerializeField, Min(8f)] private float _nameFontSize = 22f;

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FEntry = typeof(UICombatSkillSlotView).GetField("_runtimeEntry", Priv);
        private static readonly FieldInfo FHasEntry = typeof(UICombatSkillSlotView).GetField("_hasRuntimeEntry", Priv);

        private RectTransform _container, _bar;
        private HorizontalLayoutGroup _layout;
        private float _originalSpacing;
        private bool _hasOriginalSpacing;
        private float _currentScale;
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly HashSet<RectTransform> _initialized = new HashSet<RectTransform>();

        // 한 번 계산해 저장한 결과
        private bool _solved;
        private int _solvedPadding;
        private Vector2Int _solvedScreen;
        private float _labelCenterY = float.NaN; // 화면 픽셀

        private RectTransform _labelRoot;
        private readonly Dictionary<UICombatSkillSlotView, TMP_Text> _labels = new Dictionary<UICombatSkillSlotView, TMP_Text>();
        private TMP_FontAsset _font;

        private void LateUpdate()
        {
            if (_container == null && !FindContainer()) return;
            var controller = _container.GetComponentInParent<UIInGameSkillBarController>();
            if (controller != null && controller.UsesSlotPrefab) { HideLabels(); return; }
            if (_currentScale <= 0f) _currentScale = _scale;

            var slots = ActiveSlots();
            if (slots.Count == 0) { HideLabels(); return; }

            if (_fitToBottomBar)
            {
                var screen = new Vector2Int(Screen.width, Screen.height);
                if (_solved && screen != _solvedScreen) _solved = false; // 해상도가 바뀌면 다시 계산
                if (!_solved) TrySolve();
            }

            ApplyLayout(); // 저장된 값을 매 프레임 그대로 적용 — 팀 컨트롤러가 값을 되돌려도 같은 프레임 안에 다시 맞춘다
            UpdateLabels(slots);
        }

        // ───────────── 한 번에 풀기

        private void TrySolve()
        {
            if (_bar == null) _bar = FindBar();
            var canvas = _container.GetComponentInParent<Canvas>();
            if (_bar == null || canvas == null || !_bar.gameObject.activeInHierarchy || _layout == null) return;

            _bar.GetWorldCorners(_corners);
            float barBottom = Mathf.Max(0f, Mathf.Min(_corners[0].y, _corners[2].y));
            float barTop = Mathf.Max(_corners[0].y, _corners[2].y);
            float visible = barTop - barBottom;
            if (visible < 20f) return;

            float regionTop = barTop - visible * _topDecorFraction;
            float regionBottom = barBottom + visible * 0.04f;
            float reserve = _showSkillNames ? (regionTop - regionBottom) * _nameReserveFraction : 0f;
            float slotBottom = regionBottom + reserve;
            float targetHeight = (regionTop - slotBottom) * _fitFraction;
            float targetCenter = (regionTop + slotBottom) * 0.5f;
            float unitsPerPixel = 1f / Mathf.Max(0.01f, canvas.scaleFactor);

            // 같은 프레임 안에서 적용→레이아웃 갱신→측정을 몇 번 되풀이해 수렴시킨다(화면에는 마지막 결과만 그려진다)
            int padding = _layout.padding.bottom;
            for (int i = 0; i < 6; i++)
            {
                _solvedPadding = padding;
                ApplyLayout();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
                if (!MeasureSlots(out float lo, out float hi)) return;

                float height = hi - lo;
                if (height < 5f) return;
                float ratio = targetHeight / height;
                float centerError = targetCenter - (lo + hi) * 0.5f;
                if (Mathf.Abs(ratio - 1f) < 0.02f && Mathf.Abs(centerError) < 2f) break;

                _currentScale = Mathf.Clamp(_currentScale * ratio, 0.3f, 1.2f);
                padding += Mathf.RoundToInt(centerError * unitsPerPixel);
            }

            _solvedPadding = padding;
            _labelCenterY = regionBottom + reserve * 0.5f;
            _solvedScreen = new Vector2Int(Screen.width, Screen.height);
            _solved = true;
        }

        private bool MeasureSlots(out float minY, out float maxY)
        {
            minY = float.MaxValue; maxY = float.MinValue;
            bool any = false;
            foreach (var view in ActiveSlots())
            {
                if (!MeasureOne(DirectChild(view.transform, _container), out float lo, out float hi, out _, out _)) continue;
                minY = Mathf.Min(minY, lo);
                maxY = Mathf.Max(maxY, hi);
                any = true;
            }
            return any;
        }

        private bool MeasureOne(RectTransform root, out float minY, out float maxY, out float minX, out float maxX)
        {
            minY = minX = float.MaxValue; maxY = maxX = float.MinValue;
            if (root == null) return false;
            bool any = false;
            foreach (var g in root.GetComponentsInChildren<Graphic>(false))
            {
                if (!IsMeasurable(g)) continue;
                g.rectTransform.GetWorldCorners(_corners);
                for (int i = 0; i < 4; i++)
                {
                    minY = Mathf.Min(minY, _corners[i].y);
                    maxY = Mathf.Max(maxY, _corners[i].y);
                    minX = Mathf.Min(minX, _corners[i].x);
                    maxX = Mathf.Max(maxX, _corners[i].x);
                }
                any = true;
            }
            return any;
        }

        /// <summary>슬롯의 "그림"만 잰다 — 숫자와 쿨타임 효과(깜빡이는 것)는 제외.</summary>
        private static bool IsMeasurable(Graphic g)
        {
            if (g == null || !g.enabled || g.color.a < 0.05f || g.canvasRenderer.GetAlpha() < 0.05f) return false;
            if (g is TMP_Text) return false;
            string n = g.name;
            return !(n == "CooldownDim" || n == "CooldownSpark" || n.StartsWith("Ready"));
        }

        private RectTransform FindBar()
        {
            var canvas = _container != null ? _container.GetComponentInParent<Canvas>() : null;
            foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (rect.name != "BottomSkillBackground" || !rect.gameObject.scene.IsValid()) continue;
                if (canvas != null && rect.GetComponentInParent<Canvas>() != canvas) continue;
                return rect;
            }
            return null;
        }

        // ───────────── 저장된 값 적용

        private void ApplyLayout()
        {
            float width = 0f;
            foreach (var view in FindObjectsByType<UICombatSkillSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var root = DirectChild(view.transform, _container);
                if (root == null) continue;
                if (width <= 0f) width = root.rect.width;
                if (_initialized.Add(root)) root.pivot = new Vector2(root.pivot.x, 0f); // 처음 한 번: 아래쪽 가운데 기준
                if (Mathf.Abs(root.localScale.x - _currentScale) > 0.0005f) root.localScale = new Vector3(_currentScale, _currentScale, 1f);
            }

            if (_layout == null) return;
            if (width > 0f)
            {
                // 컨트롤러가 간격을 다시 쓰면 그 값을 새 기준으로 삼는다(우리가 만든 값과 구분)
                float expected = _hasOriginalSpacing ? _originalSpacing - (1f - _currentScale) * width : float.NaN;
                if (!_hasOriginalSpacing || Mathf.Abs(_layout.spacing - expected) > 0.5f)
                {
                    if (!_hasOriginalSpacing || _layout.spacing > _originalSpacing + 0.01f)
                        _originalSpacing = _layout.spacing;
                    _hasOriginalSpacing = true;
                }
                float wanted = _originalSpacing - (1f - _currentScale) * width;
                if (Mathf.Abs(_layout.spacing - wanted) > 0.01f) _layout.spacing = wanted;
            }
            if (_fitToBottomBar && (_solved || _solvedPadding != 0) && _layout.padding.bottom != _solvedPadding)
                _layout.padding = new RectOffset(_layout.padding.left, _layout.padding.right, _layout.padding.top, _solvedPadding);
        }

        // ───────────── 스킬 이름표

        private void UpdateLabels(List<UICombatSkillSlotView> slots)
        {
            if (!_showSkillNames) { HideLabels(); return; }
            // 이름표가 있는 새 슬롯은 View가 표시를 소유한다.
            HideLabels();
            bool needsLegacyLabels = slots.Exists(view => view.SkillNameText == null);
            if (!needsLegacyLabels) return;
            EnsureLabelRoot();
            if (_labelRoot == null) return;

            var canvas = _labelRoot.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            foreach (var label in _labels.Values) if (label != null) label.enabled = false;
            foreach (var view in slots)
            {
                if (view.SkillNameText != null) continue;
                string title = SkillName(view);
                if (string.IsNullOrEmpty(title)) continue;
                var root = DirectChild(view.transform, _container);
                if (!MeasureOne(root, out float lo, out _, out float minX, out float maxX)) continue;

                if (!_labels.TryGetValue(view, out var label) || label == null)
                {
                    label = CreateLabel();
                    _labels[view] = label;
                }
                label.enabled = true;
                label.text = title;
                float y = !float.IsNaN(_labelCenterY) ? _labelCenterY : lo - _nameFontSize;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(_labelRoot, new Vector2((minX + maxX) * 0.5f, y), cam, out var world);
                label.rectTransform.position = world;
            }
        }

        private static string SkillName(UICombatSkillSlotView view)
        {
            if (FHasEntry == null || FEntry == null || !(bool)FHasEntry.GetValue(view)) return null;
            var entry = FEntry.GetValue(view) as UISkillPreviewCatalogSO.Entry;
            return entry != null ? entry.DisplayName : null;
        }

        private TMP_Text CreateLabel()
        {
            var go = new GameObject("SkillName", typeof(RectTransform));
            go.transform.SetParent(_labelRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(260f, 36f);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (_font == null) _font = FindKoreanFont();
            if (_font != null) text.font = _font;
            text.fontSize = _nameFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.96f, 0.88f, 1f);
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(20, 8, 8, 255);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static TMP_FontAsset FindKoreanFont()
        {
            if (UiFontOverride.Current != null) return UiFontOverride.Current; // 씬 전체 폰트(던파 비트체)가 있으면 그것을 쓴다
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters("화염구얼음가시운석낙하연쇄번개회오리", out _, true, true)) return font;
            return null;
        }

        private void EnsureLabelRoot()
        {
            if (_labelRoot != null) return;
            var parent = _container.parent as RectTransform;
            if (parent == null) return;
            var go = new GameObject("SkillNameLabels", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _labelRoot = (RectTransform)go.transform;
            _labelRoot.anchorMin = Vector2.zero;
            _labelRoot.anchorMax = Vector2.one;
            _labelRoot.offsetMin = _labelRoot.offsetMax = Vector2.zero;
            _labelRoot.SetAsLastSibling(); // 같은 캔버스 안에서 바·슬롯 위에 그린다
        }

        private void HideLabels()
        {
            foreach (var label in _labels.Values) if (label != null && label.enabled) label.enabled = false;
        }

        // ───────────── 공통

        private List<UICombatSkillSlotView> ActiveSlots()
        {
            var list = new List<UICombatSkillSlotView>();
            foreach (var view in FindObjectsByType<UICombatSkillSlotView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var root = DirectChild(view.transform, _container);
                if (root != null && root.gameObject.activeInHierarchy) list.Add(view);
            }
            return list;
        }

        private bool FindContainer()
        {
            var controller = FindFirstObjectByType<UIInGameSkillBarController>(FindObjectsInactive.Include);
            if (controller == null) return false;
            _container = typeof(UIInGameSkillBarController)
                .GetField("_slotContainer", Priv)?.GetValue(controller) as RectTransform;
            if (_container == null) { enabled = false; return false; }
            _layout = _container.GetComponent<HorizontalLayoutGroup>();
            return true;
        }

        private static RectTransform DirectChild(Transform node, RectTransform container)
        {
            while (node != null && node.parent != container) node = node.parent;
            return node as RectTransform;
        }

        private void OnDestroy()
        {
            if (_labelRoot != null) Destroy(_labelRoot.gameObject);
            if (_container == null) return;
            foreach (var view in FindObjectsByType<UICombatSkillSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var root = DirectChild(view.transform, _container);
                if (root != null) root.localScale = Vector3.one;
            }
            if (_layout != null && _hasOriginalSpacing) _layout.spacing = _originalSpacing;
        }
    }
}
