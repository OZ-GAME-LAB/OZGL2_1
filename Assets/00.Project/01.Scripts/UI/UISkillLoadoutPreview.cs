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
        [SerializeField] private RectTransform[] _equippedSlotRects;
        [SerializeField] private UISkillCategoryStyleSO _categoryStyle;
        [SerializeField] private Image[] _cardSlotTints;
        [SerializeField] private Image[] _equippedSlotTints;
        [SerializeField] private Image _detailSlotTint;
        [SerializeField] private Sprite _redFrame;
        [SerializeField] private TMP_Text _detailName;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TMP_Text _effectLabel;
        [SerializeField] private TMP_Text _effectValue;
        [SerializeField] private TMP_Text _cooldown;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private UISkillArtButton _equipButton;
        [SerializeField] private UISkillArtButton _unequipButton;
        [SerializeField] private UISkillArtButton _saveButton;
        [SerializeField] private UIPopupPanel _exitConfirmation;
        [SerializeField] private int[] _initialEquipped = { 0, 4, 2 };
        [SerializeField] private int _initialSelected = 4;

        private readonly int[] _committed = { -1, -1, -1 };
        private readonly int[] _draft = { -1, -1, -1 };
        private bool _hasInitialized;
        private int _selected = -1;
        private int _category;
        private UIPopupPanel _panel;

        public int CategoryIndex => _category;
        public int EquippedCount { get { int count = 0; foreach (int i in _draft) if (IsValid(i)) count++; return count; } }
        public string SelectedSkillId => IsValid(_selected) ? _catalog.Entries[_selected].Id : string.Empty;
        public bool HasChanges { get { for (int i = 0; i < SLOT_COUNT; i++) if (_draft[i] != _committed[i]) return true; return false; } }
        public bool IsExitConfirmationOpen => _exitConfirmation != null && _exitConfirmation.gameObject.activeInHierarchy;
        // 실제 게임 저장을 도입할 때 연결할 이벤트. 미리보기 자체는 디스크에 저장하지 않는다.
        public event Action<IReadOnlyList<string>> SaveRequested;

        private void OnEnable()
        {
            if (TryGetComponent(out _panel)) _panel.SetDismissGuard(TryDismiss);
            InitializeIfNeeded();
            Array.Copy(_committed, _draft, SLOT_COUNT);
            _category = 0;
            _selected = IsValid(_initialSelected) ? _initialSelected : FindFirstVisible();
            SetStatus(string.Empty);
            Refresh();
        }

        private void OnDisable()
        {
            if (_panel != null) _panel.SetDismissGuard(null);
        }

        private bool TryDismiss()
        {
            if (!HasChanges) return true;
            if (_panel != null && _panel.Controller != null && _exitConfirmation != null)
                _panel.Controller.OpenPopup(_exitConfirmation);
            else
                Debug.LogWarning("스킬 나가기 확인창 연결이 없습니다. 변경 사항을 보호하기 위해 닫기를 중단합니다.", this);
            return false;
        }

        public void CancelExit()
        {
            UIPopupController controller = _panel != null ? _panel.Controller : null;
            if (controller != null && controller.IsTopPopup(_exitConfirmation)) controller.CloseTopPopup();
        }

        public void ConfirmExitWithoutSaving()
        {
            UIPopupController controller = _panel != null ? _panel.Controller : null;
            if (controller == null || !controller.IsTopPopup(_exitConfirmation)) return;
            Array.Copy(_committed, _draft, SLOT_COUNT);
            SetStatus(string.Empty);
            Refresh();
            controller.CloseTopPopup(); // 확인창을 닫고 원래 화면의 포커스를 복원한다.
            if (controller.IsTopPopup(_panel)) controller.CloseTopPopup();
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
            if (IsExitConfirmationOpen) return;
            if (!IsValid(_selected) || Array.IndexOf(_draft, _selected) >= 0) return;
            int slot = Array.IndexOf(_draft, -1);
            if (slot < 0) return;
            _draft[slot] = _selected;
            SetStatus(string.Empty);
            Refresh();
        }

        public void UnequipSelected()
        {
            if (IsExitConfirmationOpen) return;
            if (!IsValid(_selected)) return;
            int slot = Array.IndexOf(_draft, _selected);
            if (slot < 0) return;
            _draft[slot] = -1;
            SetStatus(string.Empty);
            Refresh();
        }

        public void SavePreview()
        {
            if (_catalog == null || !HasChanges || IsExitConfirmationOpen) return;
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
                    ApplyCategoryStyle(_cardIcons[i], GetImage(_cardSlotTints, i), IsValid(i) ? _catalog.Entries[i] : null);
                }
            }
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                bool hasSkill = IsValid(_draft[i]);
                if (_equippedIcons != null && i < _equippedIcons.Length && _equippedIcons[i] != null)
                {
                    _equippedIcons[i].sprite = hasSkill ? _catalog.Entries[_draft[i]].Icon : null;
                    _equippedIcons[i].enabled = hasSkill;
                    ApplyCategoryStyle(_equippedIcons[i], GetImage(_equippedSlotTints, i), hasSkill ? _catalog.Entries[_draft[i]] : null);
                }
                if (_equippedFrames != null && i < _equippedFrames.Length && _equippedFrames[i] != null)
                {
                    Sprite frame = hasSkill && _categoryStyle != null ? _categoryStyle.GetEquippedFrame(_catalog.Entries[_draft[i]].Category) : null;
                    _equippedFrames[i].sprite = frame != null ? frame : _redFrame;
                    _equippedFrames[i].material = !hasSkill && _categoryStyle != null ? _categoryStyle.EmptyFrameMaterial : null;
                    if (_equippedSlotRects != null && i < _equippedSlotRects.Length && _equippedSlotRects[i] != null && _equippedSlotRects[i] != _equippedFrames[i].rectTransform)
                    {
                        Vector4 layout = frame != null ? _categoryStyle.GetEquippedFrameLayout(_catalog.Entries[_draft[i]].Category) : new Vector4(1, 1, 0, 0);
                        Vector2 size = _equippedSlotRects[i].rect.size;
                        _equippedFrames[i].rectTransform.sizeDelta = new Vector2(size.x * layout.x, size.y * layout.y);
                        _equippedFrames[i].rectTransform.anchoredPosition = new Vector2(size.x * layout.z, size.y * layout.w);
                    }
                }
            }
            bool valid = IsValid(_selected);
            if (_detailName != null) _detailName.text = valid ? _catalog.Entries[_selected].DisplayName : "스킬 선택";
            if (_detailIcon != null) { _detailIcon.sprite = valid ? _catalog.Entries[_selected].Icon : null; _detailIcon.enabled = valid && _detailIcon.sprite != null; }
            ApplyCategoryStyle(_detailIcon, _detailSlotTint, valid ? _catalog.Entries[_selected] : null);
            if (_effectLabel != null) _effectLabel.text = valid ? _catalog.Entries[_selected].EffectLabel : "효과";
            if (_effectValue != null) _effectValue.text = valid ? _catalog.Entries[_selected].EffectValue : "—";
            if (_cooldown != null) _cooldown.text = valid ? _catalog.Entries[_selected].Cooldown : "—";
            bool isEquipped = valid && Array.IndexOf(_draft, _selected) >= 0;
            if (_equipButton != null) _equipButton.interactable = valid && !isEquipped && EquippedCount < SLOT_COUNT;
            if (_unequipButton != null) _unequipButton.interactable = isEquipped;
            if (_saveButton != null) _saveButton.interactable = _catalog != null && HasChanges;
        }

        private void ApplyCategoryStyle(Image icon, Image slotTint, UISkillPreviewCatalogSO.Entry entry)
        {
            if (_categoryStyle == null) return; // 설정되지 않은 기존 UI는 원래 표시를 유지한다.
            if (icon != null)
            {
                icon.material = entry != null ? _categoryStyle.GetIconMaterial(entry.Category) : null;
                icon.color = Color.white;
            }
            if (slotTint == null) return;
            slotTint.enabled = entry != null && icon != null && icon.enabled;
            slotTint.material = _categoryStyle.SlotTintMaterial;
            slotTint.color = entry != null ? _categoryStyle.GetSlotColor(entry.Category) : Color.clear;
        }

        private static Image GetImage(Image[] images, int index) => images != null && index >= 0 && index < images.Length ? images[index] : null;
        private void SetStatus(string text) { if (_status != null) _status.text = text; }
    }
}
