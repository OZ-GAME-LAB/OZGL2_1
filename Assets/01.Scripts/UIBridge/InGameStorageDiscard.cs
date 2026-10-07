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
        private readonly List<(GridStoredItem item, Image back, Outline outline)> _buttons = new List<(GridStoredItem, Image, Outline)>();

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
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            _panel.localScale = Vector3.one * s;

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
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.55f, 0.4f, 0.25f, 0.8f);
                outline.effectDistance = new Vector2(2f, -2f);
                var button = go.GetComponent<Button>();
                button.targetGraphic = back;
                button.onClick.AddListener(() => Toggle(captured, request));
                var label = NewText(go.transform, "Label", 24f, new Color(0.97f, 0.93f, 0.84f), TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.text = Describe(session.Grid, item);
                var lr = label.rectTransform;
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(8f, 4f); lr.offsetMax = new Vector2(-8f, -4f);
                _buttons.Add((item, back, outline));
            }
        }

        private void Toggle(GridStoredItem item, GridStorageRequest request)
        {
            if (_picked.Contains(item)) _picked.Remove(item);
            else if (_picked.Count < request.RequiredDiscardCount) _picked.Add(item);
            Sfx.Play(SfxId.UiClick);
        }

        private void Refresh(GridStorageRequest request)
        {
            foreach (var (item, back, outline) in _buttons)
            {
                bool on = _picked.Contains(item);
                back.color = on ? new Color(0.55f, 0.14f, 0.18f, 0.98f) : new Color(0.14f, 0.07f, 0.09f, 0.96f);
                outline.effectColor = on ? new Color(1f, 0.82f, 0.36f, 1f) : new Color(0.55f, 0.4f, 0.25f, 0.8f);
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
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.76f, 0.32f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            _title = NewText(_panel, "Title", 42f, new Color(1f, 0.84f, 0.42f), TextAlignmentOptions.Center);
            Anchor(_title.rectTransform, 0f, -26f, 0f, 60f);
            _sub = NewText(_panel, "Sub", 26f, new Color(0.95f, 0.9f, 0.82f), TextAlignmentOptions.Center);
            _sub.textWrappingMode = TextWrappingModes.Normal;
            Anchor(_sub.rectTransform, 0f, -92f, 0f, 84f);

            var listGo = new GameObject("List", typeof(RectTransform), typeof(GridLayoutGroup));
            listGo.transform.SetParent(_panel, false);
            _list = (RectTransform)listGo.transform;
            _list.anchorMin = new Vector2(0f, 0f); _list.anchorMax = new Vector2(1f, 1f);
            _list.offsetMin = new Vector2(40f, 120f); _list.offsetMax = new Vector2(-40f, -190f);
            var grid = listGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(250f, 74f);
            grid.spacing = new Vector2(14f, 14f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;

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
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.9f, 0.7f, 0.35f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
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
            text.fontSize = size; text.color = color; text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
