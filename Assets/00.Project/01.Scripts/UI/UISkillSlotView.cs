using System;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // Scene에 배치한 두 표시 프리팹만 전환한다. 해금·장착·저장 상태는 소유하지 않는다.
    [DisallowMultipleComponent]
    public sealed class UISkillSlotView : MonoBehaviour
    {
        [Header("표시 프리팹 인스턴스")]
        [SerializeField] private GameObject _unlockedView;
        [SerializeField] private GameObject _lockedView;

        [Header("해금 표시 내부 참조")]
        [SerializeField] private Button _selectButton;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _selectionLabel;

        private string _skillId = string.Empty;
        private bool _isUnlocked;
        private bool _isSelected;

        public string SkillId => _skillId;
        public bool IsUnlocked => _isUnlocked;
        public bool IsSelected => _isSelected;

        public event Action<UISkillSlotView> SelectionRequested;
        public event Action<UISkillSlotView> SelectionInvalidated;

        public void ShowSkill(string skillId, string displayName, Sprite icon, bool isUnlocked)
        {
            bool hasValidId = !string.IsNullOrWhiteSpace(skillId);
            bool canKeepSelection = _isSelected && _skillId == skillId && isUnlocked && hasValidId;
            bool wasSelected = _isSelected;
            _skillId = hasValidId ? skillId : string.Empty;
            _isUnlocked = isUnlocked && hasValidId;

            if (_unlockedView != null) _unlockedView.SetActive(_isUnlocked);
            if (_lockedView != null) _lockedView.SetActive(!_isUnlocked);
            if (_selectButton != null) _selectButton.interactable = _isUnlocked;
            if (_nameText != null) _nameText.text = displayName ?? string.Empty;
            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = icon != null;
            }

            SetSelected(canKeepSelection);
            if (wasSelected && !canKeepSelection) SelectionInvalidated?.Invoke(this);
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected && _isUnlocked;
            if (_selectionLabel != null) _selectionLabel.text = _isSelected ? "선택" : string.Empty;
        }

        private void OnEnable()
        {
            if (_selectButton == null) return;
            _selectButton.onClick.RemoveListener(RequestSelection);
            _selectButton.onClick.AddListener(RequestSelection);
        }

        private void OnDisable()
        {
            if (_selectButton != null) _selectButton.onClick.RemoveListener(RequestSelection);
            bool wasSelected = _isSelected;
            SetSelected(false);
            if (wasSelected) SelectionInvalidated?.Invoke(this);
        }

        private void RequestSelection()
        {
            if (!isActiveAndEnabled || !_isUnlocked || _selectButton == null || !_selectButton.IsInteractable()) return;
            SelectionRequested?.Invoke(this);
        }
    }
}
