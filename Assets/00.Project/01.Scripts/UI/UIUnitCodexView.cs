using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 카탈로그 항목만큼 카드를 재사용하며 진영별 표시를 갱신한다. 공용 Overlay의 열기/닫기는 소유하지 않는다.
    [DisallowMultipleComponent]
    public sealed class UIUnitCodexView : MonoBehaviour
    {
        [SerializeField] private UIUnitCatalogSO _catalog;
        [SerializeField] private UILobbyCollectionState _state;
        [SerializeField] private RectTransform _content;
        [SerializeField] private UIUnitCodexCardView _cardTemplate;
        [SerializeField] private Material _silhouetteMaterial;
        [SerializeField] private Sprite _lockSprite;
        [SerializeField] private UISkillArtButton[] _factionButtons;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private TMP_Text _pageText;
        [SerializeField] private TMP_Text _emptyText;

        private readonly List<UIUnitCodexCardView> _cards = new List<UIUnitCodexCardView>();
        private UILobbyCollectionState _subscribedState;
        private int _faction;

        public int FactionIndex => _faction;
        public int VisibleCount { get; private set; }
        public int UnlockedCount { get; private set; }
        public int CardCount => _cards.Count;

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeState();
        }

        public void ShowFaction(int faction)
        {
            _faction = Mathf.Clamp(faction, 0, 1);
            Refresh();
            if (_scrollRect != null)
            {
                _scrollRect.StopMovement();
                _scrollRect.verticalNormalizedPosition = 1;
            }
        }

        public UIUnitCodexCardView GetCard(int index)
        {
            return index >= 0 && index < _cards.Count ? _cards[index] : null;
        }

        public void Refresh()
        {
            SubscribeState();
            if (_cardTemplate != null) _cardTemplate.gameObject.SetActive(false);
            int entryCount = _catalog != null && _catalog.Entries != null ? _catalog.Entries.Count : 0;
            EnsureCardCount(entryCount);
            VisibleCount = 0;
            UnlockedCount = 0;

            for (int index = 0; index < _cards.Count; index++)
            {
                UIUnitCodexCardView card = _cards[index];
                if (card == null) continue;
                UIUnitCatalogSO.Entry entry = index < entryCount ? _catalog.Entries[index] : null;
                bool hasEntry = entry != null && !string.IsNullOrWhiteSpace(entry.Id);
                bool isVisible = hasEntry && (int)entry.Faction == _faction;
                bool isUnlocked = hasEntry && (_state != null ? _state.IsUnlocked(entry.Id, entry.DefaultUnlocked) : entry.DefaultUnlocked);
                card.ShowUnit(entry, isUnlocked, _silhouetteMaterial, _lockSprite);
                card.gameObject.SetActive(isVisible);
                if (!isVisible) continue;
                VisibleCount++;
                if (isUnlocked) UnlockedCount++;
            }

            if (_factionButtons != null)
                for (int index = 0; index < _factionButtons.Length; index++)
                    if (_factionButtons[index] != null) _factionButtons[index].SetChosen(index == _faction);

            if (_pageText != null) _pageText.text = $"발견 {UnlockedCount} / {VisibleCount}";
            if (_emptyText != null)
            {
                _emptyText.text = "등록된 유닛이 없습니다.";
                _emptyText.gameObject.SetActive(VisibleCount == 0);
            }
            if (_content != null) LayoutRebuilder.MarkLayoutForRebuild(_content);
        }

        private void EnsureCardCount(int entryCount)
        {
            if (_content == null || _cardTemplate == null) return;
            for (int index = 0; index < entryCount; index++)
            {
                if (index < _cards.Count && _cards[index] != null) continue;
                UIUnitCodexCardView card = Instantiate(_cardTemplate, _content, false);
                card.name = $"UnitCard_{index:D2}";
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
