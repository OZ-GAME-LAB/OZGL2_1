using System.Collections.Generic;
using OZGL2.Grid;
using OZGL2.InGame;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 보관함이 가득 찼는데 새 카드(보상·되돌린 유닛)가 들어오면 "무엇을 버릴지" 고르는 창을 띄운다.
    /// 옛 UI Toolkit 화면에만 이 창이 있어서, 지금 UI에서는 창이 안 뜬 채 보상 단계에서 멈춰 버렸다(특히 유닛을 배치하지 않고 웨이브를 넘기면 보관함 10칸이 금방 찬다).
    /// - 새로 받는 것을 보여 주고, 버릴 후보 중 필요한 개수만큼 골라서 「버리고 받기」를 누르면 그대로 진행된다.
    /// - 「취소」를 누르면 아무것도 바꾸지 않고 돌아간다(보상은 다시 고를 수 있다).
    /// 실제 확정은 팀의 규칙(GridRunSession.TryConfirmStorage / TryCancelStorage)을 그대로 쓴다.
    /// </summary>
    public sealed class InGameStorageDiscard : MonoBehaviour
    {
        private InGamePrototypeBootstrap _bootstrap;
        private UIUnitCatalogSO _catalog;
        private Canvas _canvas;
        private RectTransform _panel, _list;
        private TMP_Text _title, _sub;
        private Button _confirm;
        private TMP_Text _confirmLabel;
        private string _requestId;
        private readonly HashSet<GridStoredItem> _picked = new HashSet<GridStoredItem>();
        private ScrollRect _scroll;
        private readonly List<(GridStoredItem item, Image back, Image frame, Outline outline)> _buttons = new List<(GridStoredItem, Image, Image, Outline)>();

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void LateUpdate()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            var session = _bootstrap != null ? _bootstrap.GridSession : null;
            var request = session != null ? session.Grid.PendingStorage : null;
            if (request == null)
            {
                if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
                _requestId = null;
                return;
            }
            EnsureUi();
            _canvas.enabled = true;
            if (_requestId != request.RequestId)
            {
                _requestId = request.RequestId;
                _picked.Clear();
                Rebuild(session, request);
            }
            Refresh(request);
        }

        // ───────────── 목록

        private void Rebuild(GridRunSession session, GridStorageRequest request)
        {
            foreach (Transform child in _list) Destroy(child.gameObject);
            _buttons.Clear();
            var incoming = new List<string>();
            foreach (var item in request.Incoming) incoming.Add(Describe(session.Grid, item));
            _title.text = "보관함이 가득 찼어요";
            _sub.text = "새로 받는 것: " + (incoming.Count > 0 ? string.Join(", ", incoming) : "—") + "\n버릴 것 " + request.RequiredDiscardCount + "개를 골라 주세요. 고른 것은 사라져요.";

            foreach (var item in request.DiscardCandidates)
            {
                var captured = item;
                var go = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_list, false);
                var back = go.GetComponent<Image>();
                back.color = new Color(0.14f, 0.07f, 0.09f, 0.96f);
                var frame = UIHeraldrySupplement.AddFrame((RectTransform)go.transform);
                Outline outline = null;
                if (frame == null)
                {
                    outline = go.AddComponent<Outline>();
                    outline.effectColor = new Color(0.55f, 0.4f, 0.25f, 0.8f);
                    outline.effectDistance = new Vector2(2f, -2f);
                }
                var button = go.GetComponent<Button>();
                button.targetGraphic = back;
                button.onClick.AddListener(() => Toggle(captured, request));
                var label = NewText(go.transform, "Label", 24f, new Color(0.97f, 0.93f, 0.84f), TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.text = Describe(session.Grid, item);
                var lr = label.rectTransform;
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(8f, 4f); lr.offsetMax = new Vector2(-8f, -4f);
                _buttons.Add((item, back, frame, outline));
            }
            Canvas.ForceUpdateCanvases();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        private void Toggle(GridStoredItem item, GridStorageRequest request)
        {
            if (_picked.Contains(item)) _picked.Remove(item);
            else if (_picked.Count < request.RequiredDiscardCount) _picked.Add(item);
            Sfx.Play(SfxId.UiClick);
        }

        private void Refresh(GridStorageRequest request)
        {
            float scale = Mathf.Min(Screen.height / 1080f, (Screen.width - 32f) / 1180f, (Screen.height - 80f) / 640f);
            _panel.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            foreach (var (item, back, frame, outline) in _buttons)
            {
                bool on = _picked.Contains(item);
                back.color = on ? new Color(0.55f, 0.14f, 0.18f, 0.98f) : new Color(0.14f, 0.07f, 0.09f, 0.96f);
                if (frame != null) frame.color = on ? Color.white : new Color(0.62f, 0.55f, 0.45f, 0.9f);
                if (outline != null) outline.effectColor = on ? new Color(1f, 0.82f, 0.36f, 1f) : new Color(0.55f, 0.4f, 0.25f, 0.8f);
            }
            bool ready = _picked.Count == request.RequiredDiscardCount;
            _confirm.interactable = ready;
            _confirmLabel.text = "버리고 받기 (" + _picked.Count + "/" + request.RequiredDiscardCount + ")";
            _confirmLabel.color = ready ? new Color(1f, 0.95f, 0.8f) : new Color(0.6f, 0.55f, 0.5f);
        }

        private string Describe(GridManager grid, GridStoredItem item)
        {
            if (item.Kind == eGridDragKind.UNIT)
            {
                var unit = grid.FindUnit(item.InstanceId);
                if (unit == null) return item.DisplayName;
                return UnitName(unit.Definition) + "  ★" + unit.StarLevel;
            }
            var block = grid.FindBlock(item.InstanceId);
            return "발판 " + (block != null ? block.Footprint.Cells.Count + "칸" : string.Empty);
        }

        private string UnitName(UnitDefinition definition)
        {
            if (_catalog == null)
                foreach (var c in Resources.FindObjectsOfTypeAll<UIUnitCatalogSO>()) { _catalog = c; break; }
            if (_catalog != null)
                foreach (var entry in _catalog.Entries)
                    if (entry != null && entry.Id == "unit." + definition.Id && !string.IsNullOrWhiteSpace(entry.DisplayName)) return entry.DisplayName;
            return definition.DisplayName;
        }

        // ───────────── 창

        private void OnConfirm()
        {
            var session = _bootstrap != null ? _bootstrap.GridSession : null;
            var request = session != null ? session.Grid.PendingStorage : null;
            if (request == null) return;
            session.TryConfirmStorage(session.RunId, request.RequestId, new List<GridStoredItem>(_picked));
        }

        private void OnCancel()
        {
            var session = _bootstrap != null ? _bootstrap.GridSession : null;
            var request = session != null ? session.Grid.PendingStorage : null;
            if (request != null) session.TryCancelStorage(session.RunId, request.RequestId);
            Sfx.Play(SfxId.UiCancel);
        }

        private void EnsureUi()
        {
            if (_canvas != null) return;
            var go = new GameObject("StorageDiscard", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 110; // 팝업 캔버스(100)의 보이지 않는 전체 화면 막보다 위에 둬서 클릭이 막히지 않게 한다
            go.AddComponent<GraphicRaycaster>();

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(go.transform, false);
            var dr = (RectTransform)dim.transform;
            dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;
            var dimImage = dim.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.66f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(go.transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(1180f, 640f);
            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.085f, 0.04f, 0.055f, 0.98f);
            var style = UIHeraldrySupplement.Style;
            if (style != null) bg.color = style.BackgroundColor;
            if (UIHeraldrySupplement.AddFrame(_panel, true) == null)
            {
                var outline = panelGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.95f, 0.76f, 0.32f, 1f);
                outline.effectDistance = new Vector2(3f, -3f);
            }

            _title = NewText(_panel, "Title", 42f, new Color(1f, 0.84f, 0.42f), TextAlignmentOptions.Center);
            UIHeraldrySupplement.ApplyTitle(_title);
            Anchor(_title.rectTransform, 0f, -48f, 0f, 64f);
            _sub = NewText(_panel, "Sub", 26f, new Color(0.95f, 0.9f, 0.82f), TextAlignmentOptions.Center);
            _sub.textWrappingMode = TextWrappingModes.Normal;
            Anchor(_sub.rectTransform, 0f, -118f, 0f, 84f);

            // 후보 수가 늘어도 확정/취소 영역을 침범하지 않도록 목록만 스크롤한다.
            var scrollGo = new GameObject("ItemScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(_panel, false);
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = Vector2.zero; scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(40f, 120f); scrollRect.offsetMax = new Vector2(-40f, -220f);
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollRect, false);
            var viewport = (RectTransform)viewportGo.transform;
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = Color.clear;
            var listGo = new GameObject("List", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            listGo.transform.SetParent(viewport, false);
            _list = (RectTransform)listGo.transform;
            _list.anchorMin = new Vector2(0f, 1f); _list.anchorMax = Vector2.one; _list.pivot = new Vector2(0.5f, 1f);
            _list.anchoredPosition = Vector2.zero; _list.sizeDelta = Vector2.zero;
            var grid = listGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(250f, 74f);
            grid.spacing = new Vector2(14f, 14f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(4, 4, 4, 4);
            listGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.viewport = viewport; _scroll.content = _list;
            _scroll.horizontal = false; _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;

            _confirm = NewButton(_panel, "Confirm", new Vector2(-150f, 52f), new Color(0.42f, 0.1f, 0.14f, 1f), out _confirmLabel);
            _confirm.onClick.AddListener(OnConfirm);
            var cancel = NewButton(_panel, "Cancel", new Vector2(150f, 52f), new Color(0.2f, 0.17f, 0.17f, 1f), out var cancelLabel);
            cancelLabel.text = "취소";
            cancel.onClick.AddListener(OnCancel);
            _canvas.enabled = false;
        }

        private static void Anchor(RectTransform rt, float x, float yFromTop, float unused, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(30f, 0f); rt.offsetMax = new Vector2(-30f, 0f);
            rt.anchoredPosition = new Vector2(x, yFromTop);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        }

        private static Button NewButton(Transform parent, string name, Vector2 pos, Color color, out TMP_Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(280f, 66f);
            rt.anchoredPosition = pos;
            var image = go.GetComponent<Image>();
            image.color = color;
            var style = UIHeraldrySupplement.Style;
            Sprite sprite = style != null ? (name == "Confirm" ? style.RedButtonSprite : style.DarkButtonSprite) : null;
            if (sprite != null)
            {
                image.sprite = sprite; image.type = Image.Type.Sliced; image.color = Color.white;
                image.pixelsPerUnitMultiplier = style.FrameMultiplier;
            }
            else if (UIHeraldrySupplement.AddFrame(rt) == null)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.9f, 0.7f, 0.35f, 0.9f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            label = NewText(go.transform, "Label", 28f, new Color(1f, 0.95f, 0.8f), TextAlignmentOptions.Center);
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            return button;
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
            return text;
        }
    }
}
