using System;
using System.Collections.Generic;
using OZGL2.Augment;
using UnityEngine;

namespace OZGL2.UIFlow
{
    // 후보 표시와 선택 이벤트만 제공한다. Draw3/Pick 및 보상 지급은 호출하지 않는다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIPopupPanel))]
    public sealed class UIAugmentSelectionView : MonoBehaviour
    {
        [SerializeField] private UIPopupPanel _panel;
        [SerializeField] private UIAugmentCardView[] _cards = Array.Empty<UIAugmentCardView>();
        [SerializeField] private UIAugmentVisualCatalogSO _catalog;
        [SerializeField] private AugmentData[] _previewChoices = Array.Empty<AugmentData>();
        private readonly List<AugmentData> _activeChoices = new List<AugmentData>(3);
        private bool _hasInjectedChoices;
        private bool _hasConfirmedSelection;

        public event Action<AugmentData> AugmentSelected;

        public void Configure(UIPopupPanel panel, UIAugmentCardView[] cards,
            UIAugmentVisualCatalogSO catalog, AugmentData[] previewChoices)
        {
            UnsubscribeCards();
            _panel = panel;
            _cards = cards ?? Array.Empty<UIAugmentCardView>();
            _catalog = catalog;
            _previewChoices = previewChoices ?? Array.Empty<AugmentData>();
            if (isActiveAndEnabled) SubscribeCards();
            RefreshChoices();
        }

        public void SetChoices(IReadOnlyList<AugmentData> choices)
        {
            if (!TryCopyChoices(choices)) return;
            _hasInjectedChoices = true;
            BindCards();
        }

        public void UsePreviewChoices()
        {
            _hasInjectedChoices = false;
            RefreshChoices();
        }

        public void RefreshChoices()
        {
            // 비활성 상태에서 주입한 후보를 OnEnable의 미리보기 값으로 덮어쓰지 않는다.
            if (!_hasInjectedChoices && !TryCopyChoices(_previewChoices)) return;
            BindCards();
        }

        private bool TryCopyChoices(IReadOnlyList<AugmentData> choices)
        {
            int tier = 0;
            int count = choices != null ? Mathf.Min(3, choices.Count) : 0;
            for (int i = 0; i < count; i++)
            {
                AugmentData choice = choices[i];
                if (choice == null) continue;
                if (tier == 0) tier = choice.tier;
                else if (tier != choice.tier)
                {
                    Debug.LogWarning("증강 선택 후보는 기존 추첨 규칙과 동일하게 같은 등급으로 전달해야 합니다.", this);
                    return false;
                }
            }
            _activeChoices.Clear();
            for (int i = 0; i < count; i++)
                if (choices[i] != null) _activeChoices.Add(choices[i]);
            return true;
        }

        private void BindCards()
        {
            _hasConfirmedSelection = false;
            if (_cards == null) return;
            for (int i = 0; i < _cards.Length; i++)
            {
                UIAugmentCardView card = _cards[i];
                if (card == null) continue;
                AugmentData data = i < _activeChoices.Count ? _activeChoices[i] : null;
                card.Bind(data, _catalog);
                card.gameObject.SetActive(data != null);
            }
        }

        private void HandleSelected(AugmentData data)
        {
            if (_hasConfirmedSelection || data == null || !_activeChoices.Contains(data)) return;
            UIPopupController controller = _panel != null ? _panel.Controller : null;
            if (controller != null && !controller.IsTopPopup(_panel)) return;
            _hasConfirmedSelection = true;
            if (_cards != null)
                foreach (UIAugmentCardView card in _cards)
                    if (card != null) card.SetSelectionState(card.Data == data, false);
            AugmentSelected?.Invoke(data);
            // 외부 구독자가 다른 창을 열거나 닫았으면 그 창까지 닫지 않는다.
            if (controller != null && controller.IsTopPopup(_panel)) controller.CloseConfirmedPopup();
        }

        private void Awake()
        {
            if (_panel == null) TryGetComponent(out _panel);
        }

        private void OnEnable()
        {
            if (_panel == null) TryGetComponent(out _panel);
            SubscribeCards();
            RefreshChoices();
        }

        private void OnDisable() => UnsubscribeCards();

        private void SubscribeCards()
        {
            if (_cards == null) return;
            foreach (UIAugmentCardView card in _cards)
            {
                if (card == null) continue;
                card.Selected -= HandleSelected;
                card.Selected += HandleSelected;
            }
        }

        private void UnsubscribeCards()
        {
            if (_cards == null) return;
            foreach (UIAugmentCardView card in _cards)
                if (card != null) card.Selected -= HandleSelected;
        }
    }
}
