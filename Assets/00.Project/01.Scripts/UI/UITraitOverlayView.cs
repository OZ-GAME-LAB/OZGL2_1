using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    public sealed class UITraitOverlayView : MonoBehaviour
    {
        [SerializeField] private ScrollRect _treeScroll;
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private TMP_Text _detailTitle;
        [SerializeField] private TMP_Text _detailType;
        [SerializeField] private TMP_Text _detailDescription;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TMP_Text _pointsLabel;
        private UITraitFrameView _selectedFrame;

        public bool IsDetailOpen => _detailPanel != null && _detailPanel.activeSelf;

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
        }

        private void OnDisable()
        {
            HideDetail();
            if (_treeScroll != null) _treeScroll.StopMovement();
        }
    }
}
