using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    [RequireComponent(typeof(Button))]
    public sealed class UITraitFrameView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private GameObject _selectionIndicator;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private bool _isSpecialized;
        private Button _button;

        public string DisplayName => _displayName;
        public string Description => _description;
        public bool IsSpecialized => _isSpecialized;
        public Sprite Icon => _icon != null ? _icon.sprite : null;

        // 테두리와 아이콘을 분리한다. 강화/포인트 차감/저장은 별도 게임 시스템의 책임이다.
        public void SetContent(Sprite icon, string displayName, string description)
        {
            _displayName = displayName;
            _description = description;
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }
        }

        public void SetSelected(bool isSelected)
        {
            if (_selectionIndicator != null) _selectionIndicator.SetActive(isSelected);
        }

        private void OnEnable()
        {
            if (_button == null) TryGetComponent(out _button);
            if (_button != null) _button.onClick.AddListener(SelectFrame);
            if (_icon != null) _icon.enabled = _icon.sprite != null;
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(SelectFrame);
        }

        private void SelectFrame()
        {
            UITraitOverlayView view = GetComponentInParent<UITraitOverlayView>();
            if (view != null) view.ShowSelection(this);
        }
    }
}
