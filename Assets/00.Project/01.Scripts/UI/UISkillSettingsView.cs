using System;
using UnityEngine;

namespace OZGL2.UIFlow
{
    // 목록의 임시 표시 데이터와 선택만 관리한다. 실제 스킬 시스템 연결은 별도 호출자가 담당한다.
    [DisallowMultipleComponent]
    public sealed class UISkillSettingsView : MonoBehaviour
    {
        [Serializable]
        private sealed class SkillPreview
        {
            [SerializeField] private string _skillId;
            [SerializeField] private UISkillSlotView _slot;
            [SerializeField] private string _displayName = "스킬 이름";
            [SerializeField] private Sprite _icon;
            [SerializeField] private bool _isUnlocked;

            public string SkillId => _skillId;
            public UISkillSlotView Slot => _slot;
            public string DisplayName => _displayName;
            public Sprite Icon => _icon;
            public bool IsUnlocked => _isUnlocked;
        }

        [Header("초안 미리보기 — 실제 게임 데이터 및 저장과 무관")]
        [Tooltip("실제 시스템 연결 시 끄고 SetSkillDisplay로 화면 데이터를 전달합니다.")]
        [SerializeField] private bool _usePreviewData = true;
        [SerializeField] private SkillPreview[] _previewSkills = Array.Empty<SkillPreview>();

        private UISkillSlotView _selectedSlot;
        public string SelectedSkillId => _selectedSlot != null ? _selectedSlot.SkillId : string.Empty;

        // 빈 문자열은 선택 해제를 뜻한다. 장착 처리는 이 이벤트를 받는 쪽에서 결정한다.
        public event Action<string> SelectionChanged;

        public void ApplyPreviewData()
        {
            ClearSelection();
            if (_previewSkills == null) return;
            foreach (SkillPreview preview in _previewSkills)
            {
                if (preview == null || preview.Slot == null) continue;
                preview.Slot.ShowSkill(preview.SkillId, preview.DisplayName, preview.Icon, preview.IsUnlocked);
            }
        }

        // 이후 기존 SkillRuntime의 상태를 UI 표시로 전달하는 연결점이다.
        public bool SetSkillDisplay(string skillId, string displayName, Sprite icon, bool isUnlocked)
        {
            if (string.IsNullOrWhiteSpace(skillId) || _previewSkills == null) return false;
            foreach (SkillPreview preview in _previewSkills)
            {
                if (preview == null || preview.Slot == null || preview.SkillId != skillId) continue;
                preview.Slot.ShowSkill(skillId, displayName, icon, isUnlocked);
                return true;
            }
            return false;
        }

        public void ClearSelection()
        {
            bool hadSelection = _selectedSlot != null;
            if (_selectedSlot != null) _selectedSlot.SetSelected(false);
            _selectedSlot = null;
            if (hadSelection) SelectionChanged?.Invoke(string.Empty);
        }

        private void OnEnable()
        {
            if (_previewSkills != null)
            {
                foreach (SkillPreview preview in _previewSkills)
                {
                    if (preview == null || preview.Slot == null) continue;
                    preview.Slot.SelectionRequested -= SelectSlot;
                    preview.Slot.SelectionRequested += SelectSlot;
                    preview.Slot.SelectionInvalidated -= ClearInvalidSelection;
                    preview.Slot.SelectionInvalidated += ClearInvalidSelection;
                }
            }
            if (_usePreviewData) ApplyPreviewData();
        }

        private void OnDisable()
        {
            if (_previewSkills != null)
            {
                foreach (SkillPreview preview in _previewSkills)
                {
                    if (preview == null || preview.Slot == null) continue;
                    preview.Slot.SelectionRequested -= SelectSlot;
                    preview.Slot.SelectionInvalidated -= ClearInvalidSelection;
                }
            }
            ClearSelection();
        }

        private void SelectSlot(UISkillSlotView slot)
        {
            if (!isActiveAndEnabled || slot == null || !slot.IsUnlocked || !slot.isActiveAndEnabled || slot == _selectedSlot) return;
            if (_selectedSlot != null) _selectedSlot.SetSelected(false);
            _selectedSlot = slot;
            _selectedSlot.SetSelected(true);
            SelectionChanged?.Invoke(slot.SkillId);
        }

        private void ClearInvalidSelection(UISkillSlotView slot)
        {
            if (_selectedSlot == slot) ClearSelection();
        }
    }
}
