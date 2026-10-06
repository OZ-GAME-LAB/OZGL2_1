using TMPro;
using UnityEngine;

namespace OZGL2.UIFlow
{
    /// <summary>고정된 카드 한 자리의 내용과 투명도만 변경한다. 선택 순서와 Tween은 제어부가 관리한다.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Lobby Difficulty Slot View")]
    public sealed class UILobbyDifficultySlotView : MonoBehaviour
    {
        [SerializeField] private UILobbyStageCardView _card;
        [SerializeField] private CanvasGroup _visualGroup;
        [SerializeField] private CanvasGroup _frameGroup;
        [SerializeField] private CanvasGroup _captionGroup;
        [SerializeField] private CanvasGroup _artworkGroup;
        [SerializeField] private RectTransform _artworkMotion;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _descriptionLabel;
        [SerializeField, TextArea(2, 3)] private string _easyDescriptionOverride = string.Empty;
        [SerializeField, TextArea(2, 3)] private string _normalDescriptionOverride = string.Empty;
        [SerializeField, TextArea(2, 3)] private string _hardDescriptionOverride = string.Empty;
        [SerializeField] private TMP_Text _summaryLabel;
        [SerializeField] private UILobbyDifficultyRecordPanel _recordPanel;
        [SerializeField] private GameObject _easyPortraitGroup;
        [SerializeField] private GameObject _normalPortraitGroup;
        [SerializeField] private GameObject _hardPortraitGroup;

        private bool _hasEntry;

        public bool HasEntry => _hasEntry;
        public bool HasRequiredReferences => _card != null && _visualGroup != null && _frameGroup != null
            && _captionGroup != null && _artworkGroup != null && _artworkMotion != null && _nameLabel != null;

        public void Bind(LobbyDifficultyCatalogSO.Entry entry, Sprite artwork)
        {
            _hasEntry = entry != null;
            bool hasPortraitGroup = SetPortraitGroups(entry);
            if (_card != null)
            {
                // 선택 난이도에 개별 유닛 묶음이 연결된 경우에만 기존 단일 그림을 숨긴다.
                _card.SetArtwork(_hasEntry && !hasPortraitGroup ? artwork : null);
                _card.SetLocked(false);
            }
            if (_nameLabel != null) _nameLabel.text = _hasEntry ? entry.DisplayName : string.Empty;
            if (_descriptionLabel != null) _descriptionLabel.text = GetDescription(entry);
            if (_summaryLabel != null) _summaryLabel.text = _hasEntry ? entry.Description : string.Empty;
            // 중앙 기록도 투명 상태의 바인딩 시점에 함께 바꿔 이전 난이도 기록이 비치지 않게 한다.
            if (_recordPanel != null) _recordPanel.Bind(entry);
            if (_visualGroup != null) _visualGroup.alpha = _hasEntry ? 1f : 0f;
            SetTransition(1f, 0f);
        }

        private string GetDescription(LobbyDifficultyCatalogSO.Entry entry)
        {
            if (entry == null) return string.Empty;

            string descriptionOverride = null;
            switch (entry.Difficulty)
            {
                case eLobbyDifficulty.EASY: descriptionOverride = _easyDescriptionOverride; break;
                case eLobbyDifficulty.NORMAL: descriptionOverride = _normalDescriptionOverride; break;
                case eLobbyDifficulty.HARD: descriptionOverride = _hardDescriptionOverride; break;
            }

            // 카드 본문만 별도 문구로 표시하며, 미설정 시 기존 카탈로그 설명을 유지한다.
            return string.IsNullOrWhiteSpace(descriptionOverride) ? entry.Description : descriptionOverride;
        }

        private bool SetPortraitGroups(LobbyDifficultyCatalogSO.Entry entry)
        {
            GameObject selectedGroup = null;
            if (entry != null)
            {
                switch (entry.Difficulty)
                {
                    case eLobbyDifficulty.EASY: selectedGroup = _easyPortraitGroup; break;
                    case eLobbyDifficulty.NORMAL: selectedGroup = _normalPortraitGroup; break;
                    case eLobbyDifficulty.HARD: selectedGroup = _hardPortraitGroup; break;
                }
            }

            if (_easyPortraitGroup != null) _easyPortraitGroup.SetActive(_easyPortraitGroup == selectedGroup);
            if (_normalPortraitGroup != null) _normalPortraitGroup.SetActive(_normalPortraitGroup == selectedGroup);
            if (_hardPortraitGroup != null) _hardPortraitGroup.SetActive(_hardPortraitGroup == selectedGroup);
            return selectedGroup != null;
        }

        /// <param name="opacity">테두리/캡션/그림의 전환 투명도. 검정 바탕과 마름모 Mask는 고정한다.</param>
        /// <param name="horizontalOffsetRatio">그림 폭에 대한 좌우 이동 비율. Fitter가 있는 Image 대신 전용 부모를 이동한다.</param>
        public void SetTransition(float opacity, float horizontalOffsetRatio)
        {
            float alpha = _hasEntry ? Mathf.Clamp01(opacity) : 0f;
            if (_frameGroup != null) _frameGroup.alpha = alpha;
            if (_captionGroup != null) _captionGroup.alpha = alpha;
            if (_artworkGroup != null) _artworkGroup.alpha = alpha;
            if (_artworkMotion != null)
                _artworkMotion.anchoredPosition = new Vector2(_artworkMotion.rect.width * horizontalOffsetRatio, 0f);
        }
    }
}
