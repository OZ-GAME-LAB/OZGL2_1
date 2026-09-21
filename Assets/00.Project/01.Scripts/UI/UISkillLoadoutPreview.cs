using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 스킬창 아트 검토를 위한 표시/임시 편집만 담당한다. 게임의 SkillTreeStore/PlayerPrefs는 변경하지 않는다.
    [DisallowMultipleComponent]
    public sealed class UISkillLoadoutPreview : MonoBehaviour
    {
        private const int SLOT_COUNT = 3;
        [SerializeField] private UISkillPreviewCatalogSO _catalog;
        [SerializeField] private UISkillArtButton[] _categoryButtons;
        [SerializeField] private UISkillArtButton[] _cards;
        [SerializeField] private Image[] _cardIcons;
        [SerializeField] private Image[] _equippedIcons;
        [SerializeField] private Image[] _equippedFrames;
        [SerializeField] private Sprite _redFrame;
        [SerializeField] private Sprite _purpleFrame;
        [SerializeField] private TMP_Text _detailName;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TMP_Text _effectLabel;
        [SerializeField] private TMP_Text _effectValue;
        [SerializeField] private TMP_Text _cooldown;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private UISkillArtButton _equipButton;
        [SerializeField] private UISkillArtButton _unequipButton;
        [SerializeField] private UISkillArtButton _saveButton;
        [SerializeField] private int[] _initialEquipped = { 0, 4, 2 };
        [SerializeField] private int _initialSelected = 4;

        private readonly int[] _committed = { -1, -1, -1 };
        private readonly int[] _draft = { -1, -1, -1 };
        private bool _hasInitialized;
        private int _selected = -1;
        private int _category;

        public int CategoryIndex => _category;
        public int EquippedCount { get { int count = 0; foreach (int i in _draft) if (IsValid(i)) count++; return count; } }
        public string SelectedSkillId => IsValid(_selected) ? _catalog.Entries[_selected].Id : string.Empty;
        public bool HasChanges { get { for (int i = 0; i < SLOT_COUNT; i++) if (_draft[i] != _committed[i]) return true; return false; } }
        // 실제 게임 저장을 도입할 때 연결할 이벤트. 미리보기 자체는 디스크에 저장하지 않는다.
        public event Action<IReadOnlyList<string>> SaveRequested;

        private void OnEnable()
        {
            InitializeIfNeeded();
            Array.Copy(_committed, _draft, SLOT_COUNT);
            _category = 0;
            _selected = IsValid(_initialSelected) ? _initialSelected : FindFirstVisible();
            SetStatus(string.Empty);
            Refresh();
        }

        public void ShowCategory(int category)
        {
            _category = Mathf.Clamp(category, 0, 3);
            if (!IsVisible(_selected)) _selected = FindFirstVisible();
            SetStatus(string.Empty);
            Refresh();
        }

        public void SelectSkill(int index)
        {
            if (!IsVisible(index)) return;
            _selected = index;
            SetStatus(string.Empty);
            Refresh();
        }

        public void SelectEquippedSlot(int slot)
        {
            if (slot < 0 || slot >= SLOT_COUNT || !IsValid(_draft[slot])) return;
            _category = 0;
            _selected = _draft[slot];
            SetStatus(string.Empty);
            Refresh();
        }

        public void EquipSelected()
        {
            if (!IsValid(_selected) || Array.IndexOf(_draft, _selected) >= 0) return;
            int slot = Array.IndexOf(_draft, -1);
            if (slot < 0) return;
            _draft[slot] = _selected;
            SetStatus("저장 전 변경사항");
            Refresh();
        }

        public void UnequipSelected()
        {
            if (!IsValid(_selected)) return;
            int slot = Array.IndexOf(_draft, _selected);
            if (slot < 0) return;
            _draft[slot] = -1;
            SetStatus("저장 전 변경사항");
            Refresh();
        }

        public void SavePreview()
        {
            Array.Copy(_draft, _committed, SLOT_COUNT);
            var ids = new List<string>();
            foreach (int index in _committed) if (IsValid(index)) ids.Add(_catalog.Entries[index].Id);
            SaveRequested?.Invoke(ids.AsReadOnly());
            SetStatus("미리보기 저장됨");
            Refresh();
        }

        public string[] GetEquippedIds()
        {
            var ids = new List<string>();
            foreach (int index in _draft) if (IsValid(index)) ids.Add(_catalog.Entries[index].Id);
            return ids.ToArray();
        }

        private void InitializeIfNeeded()
        {
            if (_hasInitialized) return;
            _hasInitialized = true;
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                int candidate = _initialEquipped != null && i < _initialEquipped.Length ? _initialEquipped[i] : -1;
                _committed[i] = IsValid(candidate) && Array.IndexOf(_committed, candidate) < 0 ? candidate : -1;
            }
        }

        private bool IsValid(int index) => _catalog != null && _catalog.Entries != null && index >= 0 && index < _catalog.Entries.Count && _catalog.Entries[index] != null;
        private bool IsVisible(int index) => IsValid(index) && (_category == 0 || (int)_catalog.Entries[index].Category == _category - 1);
        private int FindFirstVisible()
        {
            if (_catalog == null || _catalog.Entries == null) return -1;
            for (int i = 0; i < _catalog.Entries.Count; i++) if (IsVisible(i)) return i;
            return -1;
        }

        private void Refresh()
        {
            if (_categoryButtons != null) for (int i = 0; i < _categoryButtons.Length; i++) if (_categoryButtons[i] != null) _categoryButtons[i].SetChosen(i == _category);
            if (_cards != null) for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                _cards[i].gameObject.SetActive(IsVisible(i));
                _cards[i].SetChosen(i == _selected);
                if (_cardIcons != null && i < _cardIcons.Length && _cardIcons[i] != null)
                {
                    _cardIcons[i].sprite = IsValid(i) ? _catalog.Entries[i].Icon : null;
                    _cardIcons[i].enabled = _cardIcons[i].sprite != null;
                }
            }
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                bool hasSkill = IsValid(_draft[i]);
                if (_equippedIcons != null && i < _equippedIcons.Length && _equippedIcons[i] != null)
                {
                    _equippedIcons[i].sprite = hasSkill ? _catalog.Entries[_draft[i]].Icon : null;
                    _equippedIcons[i].enabled = hasSkill;
                }
                if (_equippedFrames != null && i < _equippedFrames.Length && _equippedFrames[i] != null)
                    _equippedFrames[i].sprite = hasSkill && _catalog.Entries[_draft[i]].IsArcane ? _purpleFrame : _redFrame;
            }
            bool valid = IsValid(_selected);
            if (_detailName != null) _detailName.text = valid ? _catalog.Entries[_selected].DisplayName : "스킬 선택";
            if (_detailIcon != null) { _detailIcon.sprite = valid ? _catalog.Entries[_selected].Icon : null; _detailIcon.enabled = valid && _detailIcon.sprite != null; }
            if (_effectLabel != null) _effectLabel.text = valid ? _catalog.Entries[_selected].EffectLabel : "효과";
            if (_effectValue != null) _effectValue.text = valid ? _catalog.Entries[_selected].EffectValue : "—";
            if (_cooldown != null) _cooldown.text = valid ? _catalog.Entries[_selected].Cooldown : "—";
            bool isEquipped = valid && Array.IndexOf(_draft, _selected) >= 0;
            if (_equipButton != null) _equipButton.interactable = valid && !isEquipped && EquippedCount < SLOT_COUNT;
            if (_unequipButton != null) _unequipButton.interactable = isEquipped;
            if (_saveButton != null) _saveButton.interactable = _catalog != null;
        }

        private void SetStatus(string text) { if (_status != null) _status.text = text; }
    }
}
