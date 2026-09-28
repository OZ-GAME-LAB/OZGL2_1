using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 카탈로그와 Play 세션 진행도를 연결한다. 화면을 다시 열 때 기존 카드를 재사용한다.
    [DisallowMultipleComponent]
    public sealed class UIAchievementView : MonoBehaviour
    {
        [SerializeField] private UIAchievementCatalogSO _catalog;
        [SerializeField] private UILobbyCollectionState _state;
        [SerializeField] private RectTransform _content;
        [SerializeField] private UIAchievementCardView _cardTemplate;
        [SerializeField] private TMP_Text _summaryText;

        private readonly List<UIAchievementCardView> _cards = new List<UIAchievementCardView>();
        private UILobbyCollectionState _subscribedState;

        public UIAchievementCatalogSO Catalog => _catalog;
        public int TotalCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int CardCount => _cards.Count;

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeState();
        }

        public UIAchievementCardView GetCard(int index)
        {
            return index >= 0 && index < _cards.Count ? _cards[index] : null;
        }

        public void Refresh()
        {
            SubscribeState();
            if (_cardTemplate != null) _cardTemplate.gameObject.SetActive(false);
            int entryCount = _catalog != null ? _catalog.Entries.Count : 0;
            EnsureCardCount(entryCount);
            TotalCount = 0;
            CompletedCount = 0;

            for (int index = 0; index < entryCount; index++)
            {
                UIAchievementCatalogSO.Entry entry = _catalog.Entries[index];
                bool hasEntry = entry != null && !string.IsNullOrWhiteSpace(entry.Id);
                int progress = hasEntry && _state != null ? _state.GetAchievementProgress(entry.Id) : 0;
                UIAchievementCardView card = GetCard(index);
                if (card != null)
                {
                    card.Bind(entry, progress);
                    card.gameObject.SetActive(hasEntry);
                }

                if (!hasEntry) continue;
                TotalCount++;
                if (progress >= entry.Target) CompletedCount++;
            }

            for (int index = entryCount; index < _cards.Count; index++)
                if (_cards[index] != null) _cards[index].gameObject.SetActive(false);

            if (_summaryText != null) _summaryText.text = $"달성 {CompletedCount} / {TotalCount}";
            if (_content != null) LayoutRebuilder.MarkLayoutForRebuild(_content);
        }

        private void EnsureCardCount(int entryCount)
        {
            if (_content == null || _cardTemplate == null) return;
            for (int index = 0; index < entryCount; index++)
            {
                if (index < _cards.Count && _cards[index] != null) continue;
                UIAchievementCardView card = Instantiate(_cardTemplate, _content, false);
                card.name = $"AchievementCard_{index:D2}";
                if (index < _cards.Count) _cards[index] = card;
                else _cards.Add(card);
            }
        }

        private void SubscribeState()
        {
            if (!isActiveAndEnabled || _subscribedState == _state) return;
            UnsubscribeState();
            if (_state == null) return;
            _subscribedState = _state;
            _subscribedState.Changed += Refresh;
        }

        private void UnsubscribeState()
        {
            if (_subscribedState != null) _subscribedState.Changed -= Refresh;
            _subscribedState = null;
        }
    }
}
