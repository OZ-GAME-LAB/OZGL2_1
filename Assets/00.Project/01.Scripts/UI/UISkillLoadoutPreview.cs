using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 표시/임시 장착 편집을 담당한다. 실제 계정 조회·해금·저장은 외부 바인더에 위임한다.
    [DisallowMultipleComponent]
    public sealed class UISkillLoadoutPreview : MonoBehaviour
    {
        private const int SLOT_COUNT = 3;
        private const int MAX_SLOT_COUNT = 5;
        [SerializeField] private UISkillPreviewCatalogSO _catalog;
        [SerializeField] private UILobbyCollectionState _unlockState;
        [SerializeField] private Material _lockedIconMaterial;
        [SerializeField] private Color _lockedIconColor = new Color(0.22f, 0.22f, 0.22f, 1);
        [SerializeField] private Image[] _cardLockIcons;
        [SerializeField] private Image _detailLockIcon;
        [SerializeField] private UISkillArtButton[] _categoryButtons;
        [SerializeField] private UISkillArtButton[] _cards;
        [SerializeField] private Image[] _cardIcons;
        [SerializeField] private Image[] _cardUltimateFrames;
        [SerializeField] private ScrollRect _ownedScrollRect;
        [SerializeField] private Image[] _equippedIcons;
        [SerializeField] private Image[] _equippedFrames;
        [SerializeField] private Image[] _equippedUltimateFrames;
        [SerializeField] private RectTransform[] _equippedSlotRects;
        [SerializeField] private UISkillCategoryStyleSO _categoryStyle;
        [SerializeField] private Image[] _cardSlotTints;
        [SerializeField] private Image[] _equippedSlotTints;
        [SerializeField] private Image _detailSlotTint;
        [SerializeField] private Sprite _redFrame;
        [SerializeField] private TMP_Text _detailName;
        [SerializeField] private TMP_Text _detailDescription;
        [SerializeField] private TMP_Text _detailMetadata;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private Image _detailUltimateFrame;
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

        [Header("Heraldry 계정 UI — 기존 화면은 비활성 유지")]
        [SerializeField] private bool _useHeraldryLayout;
        [SerializeField] private TMP_Text _availableSp;
        [SerializeField] private TMP_Text _equippedCount;
        [SerializeField] private TMP_Text[] _equippedNames;
        [SerializeField] private TMP_Text _ownedCount;
        [SerializeField] private Image _detailFrame;
        [SerializeField] private UISkillArtButton _unlockButton;
        [SerializeField] private UIPopupPanel _unlockConfirmation;
        [SerializeField] private Image _unlockConfirmationIcon;
        [SerializeField] private TMP_Text _unlockConfirmationName;
        [SerializeField] private TMP_Text _unlockConfirmationCost;
        [SerializeField] private TMP_Text _unlockConfirmationPoints;
        [SerializeField] private TMP_Text _unlockConfirmationWarning;
        [SerializeField] private UISkillArtButton _unlockConfirmButton;

        private int[] _committed = { -1, -1, -1 };
        private int[] _draft = { -1, -1, -1 };
        private bool _hasInitialized;
        private int _selected = -1;
        private int _category;
        private UIPopupPanel _panel;
        private UILobbyCollectionState _subscribedUnlockState;
        private readonly Dictionary<string, int> _unlockCosts = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _skillPoints;
        private int _slotCapacity = SLOT_COUNT;
        private bool _hasAccountState;
        private bool _isUnlockPending;
        private string _pendingUnlockId = string.Empty;

        public bool UsesHeraldryLayout => _useHeraldryLayout;
        public int SlotCapacity => _useHeraldryLayout ? _slotCapacity : SLOT_COUNT;
        public int CategoryIndex => _category;
        public int EquippedCount { get { int count = 0; foreach (int i in _draft) if (IsSkillUnlocked(i)) count++; return count; } }
        public string SelectedSkillId => IsValid(_selected) ? _catalog.Entries[_selected].Id : string.Empty;
        public bool HasChanges { get { for (int i = 0; i < _draft.Length; i++) if (_draft[i] != _committed[i]) return true; return false; } }
        public bool IsExitConfirmationOpen => _exitConfirmation != null && _exitConfirmation.gameObject.activeInHierarchy;
        public bool IsUnlockConfirmationOpen => _useHeraldryLayout && _unlockConfirmation != null && _unlockConfirmation.gameObject.activeInHierarchy;
        // 바인더가 실제 계정에 장착 구성을 저장한다. UI는 디스크 저장을 직접 호출하지 않는다.
        public event Action<IReadOnlyList<string>> SaveRequested;
        public event Action<string> UnlockRequested;

        // 비용은 UI 카탈로그가 아니라 실제 스킬 원천을 읽은 바인더가 전달한다.
        public void SetAccountState(int skillPoints, int slotCapacity, IReadOnlyDictionary<string, int> unlockCosts)
        {
            if (!_useHeraldryLayout) return;
            _hasAccountState = true;
            _skillPoints = Mathf.Max(0, skillPoints);
            _slotCapacity = Mathf.Clamp(slotCapacity, SLOT_COUNT, MAX_SLOT_COUNT);
            ResizeSlots(_slotCapacity);
            _unlockCosts.Clear();
            if (unlockCosts != null)
                foreach (var pair in unlockCosts)
                    if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value >= 0) _unlockCosts[pair.Key] = pair.Value;
            if (isActiveAndEnabled) Refresh();
        }

        private void ResizeSlots(int count)
        {
            if (_committed.Length == count) return;
            int previous = _committed.Length;
            Array.Resize(ref _committed, count);
            Array.Resize(ref _draft, count);
            for (int i = previous; i < count; i++) { _committed[i] = -1; _draft[i] = -1; }
        }

        private void OnEnable()
        {
            if (TryGetComponent(out _panel)) _panel.SetDismissGuard(TryDismiss);
            if (_useHeraldryLayout && _unlockConfirmation != null)
                _unlockConfirmation.SetDismissGuard(() => !_isUnlockPending);
            InitializeIfNeeded();
            RemoveLockedSkills();
            Array.Copy(_committed, _draft, _committed.Length);
            _category = 0;
            _selected = IsValid(_initialSelected) ? _initialSelected : FindFirstVisible();
            SetStatus(string.Empty);
            Refresh();
            ResetScrollToTop();
        }

        private void OnDisable()
        {
            if (_panel != null) _panel.SetDismissGuard(null);
            if (_useHeraldryLayout && _unlockConfirmation != null) _unlockConfirmation.SetDismissGuard(null);
            _pendingUnlockId = string.Empty;
            _isUnlockPending = false;
            _hasAccountState = false;
            UnsubscribeUnlockState();
        }

        private bool TryDismiss()
        {
            if (IsUnlockConfirmationOpen || _isUnlockPending) return false;
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
            Array.Copy(_committed, _draft, _committed.Length);
            SetStatus(string.Empty);
            Refresh();
            controller.CloseTopPopup(); // 확인창을 닫고 원래 화면의 포커스를 복원한다.
            if (controller.IsTopPopup(_panel)) controller.CloseTopPopup();
        }

        public void ShowCategory(int category)
        {
            if (_useHeraldryLayout && (IsExitConfirmationOpen || IsUnlockConfirmationOpen || _isUnlockPending)) return;
            _category = Mathf.Clamp(category, 0, 3);
            if (!IsVisible(_selected)) _selected = FindFirstVisible();
            SetStatus(string.Empty);
            Refresh();
            ResetScrollToTop();
        }

        public void SelectSkill(int index)
        {
            if (_useHeraldryLayout && (IsExitConfirmationOpen || IsUnlockConfirmationOpen || _isUnlockPending)) return;
            if (!IsVisible(index)) return;
            _selected = index;
            SetStatus(string.Empty);
            Refresh();
        }

        public void SelectEquippedSlot(int slot)
        {
            if (_useHeraldryLayout && (IsExitConfirmationOpen || IsUnlockConfirmationOpen || _isUnlockPending)) return;
            if (slot < 0 || slot >= SlotCapacity || !IsSkillUnlocked(_draft[slot])) return;
            _category = 0;
            _selected = _draft[slot];
            SetStatus(string.Empty);
            Refresh();
        }

        public void EquipSelected()
        {
            if (IsExitConfirmationOpen || IsUnlockConfirmationOpen || _isUnlockPending) return;
            RemoveLockedSkills();
            if (!IsSkillUnlocked(_selected) || Array.IndexOf(_draft, _selected) >= 0) return;
            int slot = Array.IndexOf(_draft, -1);
            if (slot < 0) return;
            _draft[slot] = _selected;
            SetStatus(string.Empty);
            Refresh();
        }

        public void UnequipSelected()
        {
            if (IsExitConfirmationOpen || IsUnlockConfirmationOpen || _isUnlockPending) return;
            if (!IsValid(_selected)) return;
            int slot = Array.IndexOf(_draft, _selected);
            if (slot < 0) return;
            _draft[slot] = -1;
            SetStatus(string.Empty);
            Refresh();
        }

        public void SavePreview()
        {
            if (_catalog == null || IsExitConfirmationOpen || IsUnlockConfirmationOpen || _isUnlockPending) return;
            RemoveLockedSkills();
            if (!HasChanges) { Refresh(); return; }
            Array.Copy(_draft, _committed, _draft.Length);
            var ids = new List<string>();
            foreach (int index in _committed) if (IsSkillUnlocked(index)) ids.Add(_catalog.Entries[index].Id);
            SaveRequested?.Invoke(ids.AsReadOnly());
            SetStatus(_useHeraldryLayout ? "스킬 구성이 저장되었습니다." : "미리보기 저장됨");
            Refresh();
        }

        public string[] GetEquippedIds()
        {
            var ids = new List<string>();
            foreach (int index in _draft) if (IsSkillUnlocked(index)) ids.Add(_catalog.Entries[index].Id);
            return ids.ToArray();
        }

        public bool IsSkillUnlocked(int index)
        {
            if (!IsValid(index)) return false;
            UISkillPreviewCatalogSO.Entry entry = _catalog.Entries[index];
            if (string.IsNullOrWhiteSpace(entry.Id)) return false;
            return _unlockState != null ? _unlockState.IsUnlocked(entry.Id, entry.DefaultUnlocked) : entry.DefaultUnlocked;
        }

        public void OpenUnlockConfirmation()
        {
            if (!_useHeraldryLayout || IsExitConfirmationOpen || IsUnlockConfirmationOpen || _isUnlockPending || !IsValid(_selected)) return;
            string entryId = _catalog.Entries[_selected].Id;
            if (!CanUnlock(entryId, out string reason)) { SetStatus(reason); return; }
            UIPopupController controller = _panel != null ? _panel.Controller : null;
            if (controller == null || !controller.IsTopPopup(_panel) || _unlockConfirmation == null)
            {
                SetStatus("잠금 해제 확인창 연결을 확인해주세요.");
                return;
            }
            _pendingUnlockId = entryId;
            RefreshUnlockConfirmation();
            controller.OpenPopup(_unlockConfirmation);
        }

        public void CancelUnlock()
        {
            if (_isUnlockPending) return;
            UIPopupController controller = _panel != null ? _panel.Controller : null;
            if (controller != null && controller.IsTopPopup(_unlockConfirmation)) controller.CloseTopPopup();
            _pendingUnlockId = string.Empty;
        }

        public void ConfirmUnlock()
        {
            UIPopupController controller = _panel != null ? _panel.Controller : null;
            if (!_useHeraldryLayout || !IsUnlockConfirmationOpen || _isUnlockPending || controller == null || !controller.IsTopPopup(_unlockConfirmation)) return;
            if (!CanUnlock(_pendingUnlockId, out string reason))
            {
                if (_unlockConfirmationWarning != null) _unlockConfirmationWarning.text = reason;
                RefreshUnlockConfirmation(false);
                return;
            }
            if (UnlockRequested == null)
            {
                if (_unlockConfirmationWarning != null) _unlockConfirmationWarning.text = "계정 연결을 확인해주세요.";
                return;
            }
            _isUnlockPending = true;
            RefreshUnlockConfirmation(false);
            UnlockRequested.Invoke(_pendingUnlockId);
        }

        // 바인더는 실제 계정 상태를 먼저 갱신한 뒤 처리 결과를 전달한다. 장착 draft는 변경하지 않는다.
        public void CompleteUnlock(bool succeeded, string message)
        {
            if (!_useHeraldryLayout || !_isUnlockPending) return;
            _isUnlockPending = false;
            UIPopupController controller = _panel != null ? _panel.Controller : null;
            if (succeeded)
            {
                if (controller != null && controller.IsTopPopup(_unlockConfirmation)) controller.CloseTopPopup();
                _pendingUnlockId = string.Empty;
                SetStatus(string.IsNullOrEmpty(message) ? "스킬 잠금이 해제되었습니다." : message);
            }
            else if (_unlockConfirmationWarning != null) _unlockConfirmationWarning.text = message;
            Refresh();
            if (!succeeded) RefreshUnlockConfirmation(false);
        }

        private bool CanUnlock(string entryId, out string reason)
        {
            reason = string.Empty;
            int index = FindSkillIndex(entryId);
            if (!_hasAccountState || index < 0 || !_unlockCosts.TryGetValue(entryId, out int cost))
                reason = "현재 해금할 수 없는 스킬입니다.";
            else if (IsSkillUnlocked(index)) reason = "이미 잠금 해제된 스킬입니다.";
            else if (_skillPoints < cost) reason = "보유 SP가 부족합니다.";
            return string.IsNullOrEmpty(reason);
        }

        private int FindSkillIndex(string entryId)
        {
            if (string.IsNullOrEmpty(entryId) || _catalog == null || _catalog.Entries == null) return -1;
            for (int i = 0; i < _catalog.Entries.Count; i++)
                if (_catalog.Entries[i] != null && _catalog.Entries[i].Id == entryId) return i;
            return -1;
        }

        private void RefreshUnlockConfirmation(bool resetWarning = true)
        {
            if (!_useHeraldryLayout) return;
            int index = FindSkillIndex(_pendingUnlockId);
            var entry = index >= 0 ? _catalog.Entries[index] : null;
            bool mapped = entry != null && !string.IsNullOrEmpty(entry.Id) && _unlockCosts.TryGetValue(entry.Id, out _);
            int cost = mapped ? _unlockCosts[entry.Id] : 0;
            if (_unlockConfirmationIcon != null)
            {
                _unlockConfirmationIcon.sprite = entry != null ? entry.Icon : null;
                _unlockConfirmationIcon.enabled = _unlockConfirmationIcon.sprite != null;
                ApplyCategoryStyle(_unlockConfirmationIcon, null, entry);
                ApplyLockedStyle(_unlockConfirmationIcon, null, null, entry != null && !IsSkillUnlocked(index));
            }
            if (_unlockConfirmationName != null) _unlockConfirmationName.text = entry != null ? entry.DisplayName : string.Empty;
            if (_unlockConfirmationCost != null) _unlockConfirmationCost.text = mapped ? "해금 비용  " + cost + " SP" : "해금 불가";
            if (_unlockConfirmationPoints != null) _unlockConfirmationPoints.text = mapped ? _skillPoints + " SP  →  " + Mathf.Max(0, _skillPoints - cost) + " SP" : _skillPoints + " SP";
            if (_unlockConfirmationWarning != null && resetWarning)
                _unlockConfirmationWarning.text = "스킬 해제에 사용한 SP 포인트는 환급할 수 없습니다.";
            if (_unlockConfirmButton != null) _unlockConfirmButton.interactable = !_isUnlockPending && CanUnlock(_pendingUnlockId, out _);
        }

        private void InitializeIfNeeded()
        {
            if (_hasInitialized) return;
            _hasInitialized = true;
            for (int i = 0; i < SlotCapacity; i++)
            {
                int candidate = _initialEquipped != null && i < _initialEquipped.Length ? _initialEquipped[i] : -1;
                _committed[i] = IsSkillUnlocked(candidate) && Array.IndexOf(_committed, candidate) < 0 ? candidate : -1;
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
            SubscribeUnlockState();
            RemoveLockedSkills();
            if (_categoryButtons != null) for (int i = 0; i < _categoryButtons.Length; i++) if (_categoryButtons[i] != null) _categoryButtons[i].SetChosen(i == _category);
            if (_cards != null) for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                _cards[i].gameObject.SetActive(IsVisible(i));
                _cards[i].SetChosen(i == _selected);
                if (_useHeraldryLayout && _cards[i].targetGraphic is Image cardFrame)
                    cardFrame.material = IsValid(i) && !IsSkillUnlocked(i) && i != _selected && _categoryStyle != null
                        ? _categoryStyle.EmptyFrameMaterial : null;
                if (_cardIcons != null && i < _cardIcons.Length && _cardIcons[i] != null)
                {
                    _cardIcons[i].sprite = IsValid(i) ? _catalog.Entries[i].Icon : null;
                    _cardIcons[i].enabled = _cardIcons[i].sprite != null;
                    ApplyCategoryStyle(_cardIcons[i], GetImage(_cardSlotTints, i), IsValid(i) ? _catalog.Entries[i] : null);
                }
                ApplyLockedStyle(GetImage(_cardIcons, i), GetImage(_cardSlotTints, i), GetImage(_cardLockIcons, i), IsValid(i) && !IsSkillUnlocked(i));
                ApplyUltimateFrame(GetImage(_cardUltimateFrames, i), IsSkillUnlocked(i) && _catalog.Entries[i].IsUltimate);
            }
            if (_useHeraldryLayout && _equippedSlotRects != null)
                for (int i = 0; i < _equippedSlotRects.Length; i++)
                    if (_equippedSlotRects[i] != null) _equippedSlotRects[i].gameObject.SetActive(i < SlotCapacity);
            for (int i = 0; i < SlotCapacity; i++)
            {
                bool hasSkill = IsSkillUnlocked(_draft[i]);
                if (_useHeraldryLayout && _equippedNames != null && i < _equippedNames.Length && _equippedNames[i] != null)
                    _equippedNames[i].text = hasSkill ? _catalog.Entries[_draft[i]].DisplayName : "빈 슬롯";
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
                ApplyUltimateFrame(GetImage(_equippedUltimateFrames, i), hasSkill && _catalog.Entries[_draft[i]].IsUltimate);
            }
            bool valid = IsValid(_selected);
            bool isUnlocked = IsSkillUnlocked(_selected);
            bool showInformation = valid && (isUnlocked || _useHeraldryLayout);
            if (_detailName != null) _detailName.text = !valid ? "스킬 선택" : showInformation ? _catalog.Entries[_selected].DisplayName : "미발견";
            if (_detailDescription != null) _detailDescription.text = !valid ? string.Empty : showInformation ? _catalog.Entries[_selected].Description : "해금 후 정보 확인 가능";
            if (_detailMetadata != null)
            {
                UISkillPreviewCatalogSO.Entry entry = showInformation ? _catalog.Entries[_selected] : null;
                string costText = entry == null ? string.Empty : _useHeraldryLayout
                    ? (!string.IsNullOrEmpty(entry.Id) && _unlockCosts.TryGetValue(entry.Id, out int cost) ? cost + " SP" : "연결 확인 필요") : entry.UnlockSp + " SP";
                _detailMetadata.text = entry != null ? $"T{entry.Tier} · {entry.Activation} · 해금 {costText}" : string.Empty;
            }
            if (_detailIcon != null) { _detailIcon.sprite = valid ? _catalog.Entries[_selected].Icon : null; _detailIcon.enabled = valid && _detailIcon.sprite != null; }
            ApplyCategoryStyle(_detailIcon, _detailSlotTint, valid ? _catalog.Entries[_selected] : null);
            ApplyLockedStyle(_detailIcon, _detailSlotTint, _detailLockIcon, valid && !isUnlocked);
            if (_useHeraldryLayout && _detailFrame != null)
            {
                Sprite frame = valid && _categoryStyle != null ? _categoryStyle.GetEquippedFrame(_catalog.Entries[_selected].Category) : null;
                _detailFrame.sprite = frame != null ? frame : _redFrame;
                _detailFrame.material = !isUnlocked && _categoryStyle != null ? _categoryStyle.EmptyFrameMaterial : null;
            }
            ApplyUltimateFrame(_detailUltimateFrame, isUnlocked && _catalog.Entries[_selected].IsUltimate);
            if (_effectLabel != null) _effectLabel.text = showInformation ? _catalog.Entries[_selected].EffectLabel : "효과";
            if (_effectValue != null) _effectValue.text = showInformation ? _catalog.Entries[_selected].EffectValue : "—";
            if (_cooldown != null) _cooldown.text = showInformation ? _catalog.Entries[_selected].Cooldown : "—";
            bool isEquipped = isUnlocked && Array.IndexOf(_draft, _selected) >= 0;
            if (_equipButton != null) _equipButton.interactable = isUnlocked && !isEquipped && EquippedCount < SlotCapacity;
            if (_unequipButton != null) _unequipButton.interactable = isEquipped;
            if (_saveButton != null) _saveButton.interactable = _catalog != null && HasChanges;
            if (_useHeraldryLayout)
            {
                if (_availableSp != null) _availableSp.text = "남은 SP  " + (_hasAccountState ? _skillPoints.ToString() : "—");
                if (_equippedCount != null) _equippedCount.text = EquippedCount + " / " + SlotCapacity;
                if (_ownedCount != null)
                {
                    int total = 0;
                    int unlocked = 0;
                    if (_catalog != null && _catalog.Entries != null)
                        for (int i = 0; i < _catalog.Entries.Count; i++)
                            if (IsValid(i)) { total++; if (IsSkillUnlocked(i)) unlocked++; }
                    _ownedCount.text = "보유 스킬 " + unlocked + "/" + total;
                }
                if (_equipButton != null) _equipButton.gameObject.SetActive(!valid || isUnlocked);
                if (_unequipButton != null) _unequipButton.gameObject.SetActive(!valid || isUnlocked);
                if (_unlockButton != null)
                {
                    _unlockButton.gameObject.SetActive(valid && !isUnlocked);
                    // 부족 SP 상태에서도 확인창 대신 상태 문구를 보여 줄 수 있도록 클릭은 허용한다.
                    _unlockButton.interactable = valid && !isUnlocked && _hasAccountState &&
                        !string.IsNullOrEmpty(_catalog.Entries[_selected].Id) && _unlockCosts.ContainsKey(_catalog.Entries[_selected].Id);
                }
                if (IsUnlockConfirmationOpen) RefreshUnlockConfirmation(false);
            }
        }

        private void SubscribeUnlockState()
        {
            if (!isActiveAndEnabled || _subscribedUnlockState == _unlockState) return;
            UnsubscribeUnlockState();
            if (_unlockState == null) return;
            _subscribedUnlockState = _unlockState;
            _subscribedUnlockState.Changed += Refresh;
        }

        private void UnsubscribeUnlockState()
        {
            if (_subscribedUnlockState != null) _subscribedUnlockState.Changed -= Refresh;
            _subscribedUnlockState = null;
        }

        private void RemoveLockedSkills()
        {
            // 강제 재잠금은 저장 구성과 편집 구성을 함께 정리하여, 나머지 사용자의 미저장 편집만 유지한다.
            for (int index = 0; index < _draft.Length; index++)
            {
                // 카탈로그가 잠시 없거나 비어 있는 경우는 재잠금과 다르다. 표시만 숨기고 저장 구성은 보존한다.
                if (IsValid(_committed[index]) && !IsSkillUnlocked(_committed[index])) _committed[index] = -1;
                if (IsValid(_draft[index]) && !IsSkillUnlocked(_draft[index])) _draft[index] = -1;
            }
        }

        private void ApplyLockedStyle(Image icon, Image slotTint, Image lockIcon, bool isLocked)
        {
            if (icon != null)
            {
                if (isLocked)
                {
                    // 분류 표시를 적용한 뒤 덮어써 흰색 본체나 분류색이 잠금 실루엣에 남지 않게 한다.
                    icon.material = _lockedIconMaterial;
                    icon.color = _lockedIconColor;
                    icon.enabled = icon.sprite != null && _lockedIconMaterial != null;
                }
                else if (_categoryStyle == null)
                {
                    icon.material = null;
                    icon.color = Color.white;
                }
            }
            if (isLocked && slotTint != null) slotTint.enabled = false;
            if (lockIcon != null)
            {
                lockIcon.enabled = isLocked && lockIcon.sprite != null;
                lockIcon.raycastTarget = false;
            }
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

        private static void ApplyUltimateFrame(Image frame, bool isVisible)
        {
            if (frame == null) return;
            // 궁극기 장식은 별도 이미지로 표시하여 기존 분류색과 호버/선택 프레임을 보존한다.
            frame.raycastTarget = false;
            frame.enabled = isVisible && frame.sprite != null;
        }

        private void ResetScrollToTop()
        {
            if (_ownedScrollRect == null || _ownedScrollRect.content == null || !_ownedScrollRect.isActiveAndEnabled) return;
            // 분류 변경으로 비활성화된 카드의 배치를 반영한 뒤 상단으로 이동한다. 일반 Refresh는 위치를 보존한다.
            RectTransform content = _ownedScrollRect.content;
            RectTransform viewport = _ownedScrollRect.viewport != null ? _ownedScrollRect.viewport : _ownedScrollRect.transform as RectTransform;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            _ownedScrollRect.StopMovement();
            if (content.parent == viewport && Mathf.Approximately(content.anchorMin.y, 1f) &&
                Mathf.Approximately(content.anchorMax.y, 1f) && Mathf.Approximately(content.pivot.y, 1f))
            {
                // 상단 고정 Content는 로컬 위치로 맞춰 화면 활성화 중 아직 확정되지 않은 월드 Bounds에 의존하지 않는다.
                Vector2 position = content.anchoredPosition;
                position.y = 0f;
                content.anchoredPosition = position;
            }
            else _ownedScrollRect.verticalNormalizedPosition = 1f;
        }

        private static Image GetImage(Image[] images, int index) => images != null && index >= 0 && index < images.Length ? images[index] : null;
        private void SetStatus(string text) { if (_status != null) _status.text = text; }
    }
}
