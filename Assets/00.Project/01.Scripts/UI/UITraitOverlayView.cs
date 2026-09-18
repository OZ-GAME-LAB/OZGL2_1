using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace OZGL2.UIFlow
{
    public sealed class UITraitOverlayView : MonoBehaviour
    {
        [SerializeField] private ScrollRect _treeScroll;
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private TMP_Text _detailTitle;
        [SerializeField] private TMP_Text _detailType;
        [SerializeField] private TMP_Text _detailDescription;
        [SerializeField] private TMP_Text _detailNextDescription;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TMP_Text _pointsLabel;
        [SerializeField] private TMP_Text _detailLevel;
        [SerializeField] private TMP_Text _detailStatus;
        [SerializeField] private TMP_Text _upgradeCost;
        [SerializeField] private TMP_Text _downgradeRefund;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _downgradeButton;
        [SerializeField] private TMP_Text _upgradeLabel;
        [SerializeField] private TMP_Text _downgradeLabel;
        [SerializeField] private Button _resetButton;
        [SerializeField] private TMP_Text _resetLabel;
        [SerializeField] private GameObject _resetConfirmation;
        [SerializeField] private TMP_Text _resetMessage;
        [SerializeField] private Button _confirmResetButton;
        [SerializeField] private Button _cancelResetButton;
        private int _allocatedPoints;
        private UITraitFrameView _selectedFrame;

        public bool IsDetailOpen => _detailPanel != null && _detailPanel.activeSelf;
        public bool IsResetConfirmationOpen => _resetConfirmation != null && _resetConfirmation.activeSelf;
        public UITraitFrameView SelectedFrame => _selectedFrame;
        public event Action SelectionChanged;
        public event Action UpgradeRequested;
        public event Action DowngradeRequested;
        public event Action ResetRequested;

        public void ShowResetState(int allocatedPoints)
        {
            _allocatedPoints = Mathf.Max(0, allocatedPoints);
            SetActionState(_resetButton, _resetLabel, _allocatedPoints > 0);
            if (_confirmResetButton != null) _confirmResetButton.interactable = _allocatedPoints > 0;
            if (_resetMessage != null)
                _resetMessage.text = "모든 특성을 초기화할까요?\n투자한 " + _allocatedPoints + " LP를 돌려받습니다.\n마왕 레벨과 경험치는 유지됩니다.";
        }

        private void OpenResetConfirmation()
        {
            if (_allocatedPoints <= 0 || _resetConfirmation == null) return;
            HideDetail();
            if (_treeScroll != null) _treeScroll.StopMovement();
            _resetConfirmation.SetActive(true);
            // 실수로 즉시 확정하지 않도록 취소를 기본 선택한다.
            if (_cancelResetButton != null) _cancelResetButton.Select();
        }

        public void CancelReset()
        {
            if (_resetConfirmation != null) _resetConfirmation.SetActive(false);
        }

        private void ConfirmReset()
        {
            if (!IsResetConfirmationOpen || _allocatedPoints <= 0) return;
            CancelReset();
            ResetRequested?.Invoke();
        }

        public void ShowProgression(int rank, int maxRank, bool canUpgrade, bool canDowngrade,
            int nextCost, int refund, string status)
        {
            if (_detailLevel != null) _detailLevel.text = "현재 레벨  " + rank + " / " + maxRank;
            if (_detailStatus != null) _detailStatus.text = status;
            if (_upgradeCost != null) _upgradeCost.text = rank >= maxRank ? "최대 레벨" : "필요 " + nextCost + " LP";
            if (_downgradeRefund != null) _downgradeRefund.text = rank <= 0 ? "최소 레벨" : "환급 " + Mathf.Max(0, refund) + " LP";
            SetActionState(_upgradeButton, _upgradeLabel, canUpgrade);
            SetActionState(_downgradeButton, _downgradeLabel, canDowngrade);
        }

        public void ShowEffectComparison(int rank, int maxRank, string currentEffect, string nextEffect)
        {
            if (_detailDescription != null)
                _detailDescription.text = "(현재) " + rank + "p :\n" + currentEffect;
            if (_detailNextDescription != null)
            {
                _detailNextDescription.color = _selectedFrame != null ? _selectedFrame.LockedNameColor : new Color32(155, 150, 146, 255);
                _detailNextDescription.text = rank < maxRank
                    ? "(다음 레벨) " + (rank + 1) + "p :\n" + nextEffect
                    : "(다음 레벨)\n최대 레벨입니다.";
            }
        }

        private static void SetActionState(Button button, TMP_Text label, bool enabled)
        {
            if (button != null) button.interactable = enabled;
            if (label != null) label.color = enabled ? new Color32(248, 242, 235, 255) : new Color32(101, 98, 98, 255);
        }

        public void SetAvailablePoints(int points)
        {
            if (_pointsLabel != null) _pointsLabel.text = "보유 포인트  " + Mathf.Max(0, points);
        }

        public void ShowSelection(UITraitFrameView frame)
        {
            if (frame == null || _detailPanel == null) return;
            if (_selectedFrame != null) _selectedFrame.SetSelected(false);
            _selectedFrame = frame;
            frame.SetSelected(true);
            if (_detailTitle != null) _detailTitle.text = frame.DisplayName;
            if (_detailType != null) _detailType.text = frame.IsSpecialized ? "특화 특성" : "일반 특성";
            if (_detailDescription != null) _detailDescription.text = frame.Description;
            if (_detailIcon != null)
            {
                _detailIcon.sprite = frame.Icon;
                _detailIcon.enabled = frame.Icon != null;
            }
            _detailPanel.SetActive(true);
            if (_treeScroll != null) _treeScroll.StopMovement();
            SelectionChanged?.Invoke();
        }

        public void HideDetail()
        {
            if (_selectedFrame != null) _selectedFrame.SetSelected(false);
            _selectedFrame = null;
            if (_detailPanel != null) _detailPanel.SetActive(false);
        }

        public void ResetView()
        {
            if (_treeScroll == null || _treeScroll.content == null) return;
            _treeScroll.StopMovement();
            _treeScroll.content.anchoredPosition = Vector2.zero;
            HideDetail();
        }

        private void OnEnable()
        {
            if (_upgradeButton != null) _upgradeButton.onClick.AddListener(RequestUpgrade);
            if (_downgradeButton != null) _downgradeButton.onClick.AddListener(RequestDowngrade);
            if (_resetButton != null) _resetButton.onClick.AddListener(OpenResetConfirmation);
            if (_confirmResetButton != null) _confirmResetButton.onClick.AddListener(ConfirmReset);
            if (_cancelResetButton != null) _cancelResetButton.onClick.AddListener(CancelReset);
        }

        private void RequestUpgrade() => UpgradeRequested?.Invoke();
        private void RequestDowngrade() => DowngradeRequested?.Invoke();

        private void OnDisable()
        {
            if (_upgradeButton != null) _upgradeButton.onClick.RemoveListener(RequestUpgrade);
            if (_downgradeButton != null) _downgradeButton.onClick.RemoveListener(RequestDowngrade);
            if (_resetButton != null) _resetButton.onClick.RemoveListener(OpenResetConfirmation);
            if (_confirmResetButton != null) _confirmResetButton.onClick.RemoveListener(ConfirmReset);
            if (_cancelResetButton != null) _cancelResetButton.onClick.RemoveListener(CancelReset);
            CancelReset();
            HideDetail();
            if (_treeScroll != null) _treeScroll.StopMovement();
        }
    }
}
