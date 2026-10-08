using System.Reflection;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비 가운데 난이도 카드 위에 마우스를 올리면 아래 「기록」 패널(최고 웨이브 · 클리어 시간)이 펼쳐지고, 마우스를 내리면 접힌다.
    /// 예전에는 카드 아래 명패(「용사들의 첫 번째 침입」) 위에 올려야만 나왔다. 명패 위에 올리는 동작은 그대로 두고, 카드 위에서도 같은 패널이 나오게 했다.
    /// 패널은 더 읽기 쉽게 크게 키운다(_panelScale). 난이도 카드를 넘기면 접힌다. 패널 쪽에는 「밖에서 펼침」 스위치만 추가했고(UILobbyDifficultyRecordPanel.SetExternalOpen), 카드 프리팹은 건드리지 않는다.
    /// </summary>
    public sealed class LobbyCardRecordToggle : MonoBehaviour
    {
        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FCur = typeof(UILobbyDifficultySelector).GetField("_currentSlot", Priv),
            FCard = typeof(UILobbyDifficultySlotView).GetField("_card", Priv);

        private static readonly FieldInfo FPanelRect = typeof(UILobbyDifficultyRecordPanel).GetField("_panelRect", Priv);

        [SerializeField, Range(1f, 2f), Tooltip("기록 패널을 얼마나 키울지(1 = 원래 크기)")] private float _panelScale = 1.15f;

        private UILobbyDifficultySelector _selector;
        private UILobbyDifficultyRecordPanel _panel;
        private RectTransform _area;
        private bool _open, _scaled;
        private float _next;

        private void OnDestroy()
        {
            if (_selector != null) _selector.SelectionChanged -= OnSelectionChanged;
        }

        private void Update()
        {
            if (_selector == null || _panel == null || _area == null)
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 0.5f;
                if (_selector == null)
                {
                    _selector = FindFirstObjectByType<UILobbyDifficultySelector>(FindObjectsInactive.Include);
                    if (_selector != null) _selector.SelectionChanged += OnSelectionChanged;
                }
                if (_panel == null) _panel = FindFirstObjectByType<UILobbyDifficultyRecordPanel>(FindObjectsInactive.Include);
                EnsureArea();
                ScalePanel();
            }
        }

        /// <summary>기록 패널(그림 + 마우스를 받는 영역)을 같은 비율로 키운다. 아래 가운데를 기준으로 커져서 자리는 그대로이고 위·옆으로만 늘어난다.</summary>
        private void ScalePanel()
        {
            if (_scaled || _panel == null) return;
            var visual = FPanelRect != null ? FPanelRect.GetValue(_panel) as RectTransform : null;
            var hit = _panel.transform as RectTransform;
            if (visual == null || hit == null) return;
            visual.localScale = new Vector3(_panelScale, _panelScale, 1f);
            hit.localScale = new Vector3(_panelScale, _panelScale, 1f);
            _scaled = true;
        }

        private void EnsureArea()
        {
            if (_selector == null || FCur == null || FCard == null) return;
            var slot = FCur.GetValue(_selector) as UILobbyDifficultySlotView;
            var card = slot != null ? FCard.GetValue(slot) as Component : null;
            var rect = card != null ? card.transform as RectTransform : null;
            if (rect == null) return;
            if (_area != null && _area.parent == rect) return;

            var go = new GameObject("RecordClickArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(rect, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            _area = (RectTransform)go.transform;
            _area.anchorMin = Vector2.zero; _area.anchorMax = Vector2.one;
            _area.offsetMin = _area.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);   // 보이지 않지만 누름을 받는다
            image.raycastTarget = true;
            go.AddComponent<HoverRelay>().Owner = this;
            _area.SetAsLastSibling();
        }

        private void OnSelectionChanged(eLobbyDifficulty _) => SetOpen(false);

        private void SetOpen(bool open)
        {
            _open = open;
            if (_panel != null) _panel.SetExternalOpen(open);
        }

        private sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public LobbyCardRecordToggle Owner;

            public void OnPointerEnter(PointerEventData eventData) { if (Owner != null) Owner.SetOpen(true); }
            public void OnPointerExit(PointerEventData eventData) { if (Owner != null) Owner.SetOpen(false); }
        }
    }
}
