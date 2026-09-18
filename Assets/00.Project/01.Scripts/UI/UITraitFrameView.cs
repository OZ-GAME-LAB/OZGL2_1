using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OZGL2.Progression;

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
        [SerializeField] private TraitData _definition;
        [SerializeField] private Image _frame;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private Sprite _defaultFrameSprite;
        [SerializeField] private Sprite _maxLevelFrameSprite;
        [SerializeField] private Color _defaultNameColor = new Color32(248, 242, 235, 255);
        [SerializeField] private Color _lockedNameColor = new Color32(155, 150, 146, 255);
        [SerializeField] private Color _maxLevelNameColor = new Color32(155, 192, 140, 255);
        private Button _button;
        private UITraitOverlayView _owner;

        public TraitData Definition => _definition;
        public string DisplayName => _definition != null ? _definition.displayName : _displayName;
        public string Description => (_definition != null ? _definition.description : _description)?.Replace('−', '-');
        public bool IsSpecialized => _definition != null ? _definition.line == TraitLine.Capstone : _isSpecialized;
        public Sprite Icon => _icon != null ? _icon.sprite : null;
        public Sprite DefaultFrameSprite => _defaultFrameSprite;
        public Sprite MaxLevelFrameSprite => _maxLevelFrameSprite;
        public Color MaxLevelNameColor => _maxLevelNameColor;
        public Color LockedNameColor => _lockedNameColor;

        public void ShowRank(int rank, bool isOpen)
        {
            if (_definition == null) return;
            bool isMaxed = _definition.maxRank > 0 && rank >= _definition.maxRank;
            if (_nameLabel != null)
            {
                _nameLabel.text = DisplayName;
                _nameLabel.color = isMaxed ? _maxLevelNameColor : isOpen ? _defaultNameColor : _lockedNameColor;
            }
            // 아이콘/클릭 영역은 유지하고, 만렙일 때만 금테와 계열색 내부가 있는 프레임으로 교체한다.
            if (_frame != null)
            {
                Sprite sprite = isMaxed && _maxLevelFrameSprite != null ? _maxLevelFrameSprite : _defaultFrameSprite;
                if (sprite != null) _frame.sprite = sprite;
            }
            // 잠긴 특성도 선택해 설명과 선행 조건을 확인할 수 있다.
            if (_frame != null) _frame.color = isOpen ? Color.white : new Color(0.42f, 0.42f, 0.45f);
            if (_icon != null) _icon.color = isOpen ? Color.white : new Color(0.55f, 0.55f, 0.58f);
        }

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
            if (_defaultFrameSprite == null && _frame != null) _defaultFrameSprite = _frame.sprite;
            if (_button == null) TryGetComponent(out _button);
            if (_owner == null) _owner = GetComponentInParent<UITraitOverlayView>();
            if (_button != null) _button.onClick.AddListener(SelectFrame);
            if (_icon != null) _icon.enabled = _icon.sprite != null;
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(SelectFrame);
        }

        private void SelectFrame()
        {
            if (_owner != null) _owner.ShowSelection(this);
        }
    }
}
