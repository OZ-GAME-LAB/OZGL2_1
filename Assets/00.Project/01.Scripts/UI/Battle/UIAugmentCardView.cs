using System;
using OZGL2.Augment;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 표시와 입력만 담당한다. 이 카드 자체가 증강 효과를 적용하거나 런 데이터를 바꾸지 않는다.
    [DisallowMultipleComponent]
    public sealed class UIAugmentCardView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private const float CREST_SOURCE_SIZE = 512f;
        private const float CREST_ICON_Y = 36f;

        [Header("분리 아트 / 문장 내부 아이콘")]
        [SerializeField] private Image _velvetBackground;
        [SerializeField] private Image _descriptionPanel;
        [SerializeField] private Image _crest;
        [SerializeField] private Image _icon;
        [Header("이미지와 분리된 표시 문구")]
        [SerializeField] private TMP_Text _gradeText;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _effectText;
        [Header("등급 아트를 덮어쓰지 않는 별도 입력 강조")]
        [SerializeField] private Image _stateHighlight;
        [SerializeField] private Button _button;
        [SerializeField] private Color _hoverColor = new Color(0.86f, 0.71f, 0.43f, 0.12f);
        [SerializeField] private Color _selectedColor = new Color(0.94f, 0.79f, 0.46f, 0.24f);

        private AugmentData _data;
        private UIAugmentVisualCatalogSO _catalog;
        private bool _isHovered;
        private bool _isFocused;
        private bool _isSelected;
        private bool _canSelect;

        public AugmentData Data => _data;
        public Button Button => _button;
        public event Action<AugmentData> Selected;

        public void Configure(Image velvetBackground, Image descriptionPanel, Image crest,
            Image icon, TMP_Text gradeText, TMP_Text nameText, TMP_Text descriptionText,
            TMP_Text effectText, Image stateHighlight, Button button)
        {
            UnsubscribeButton();
            _velvetBackground = velvetBackground;
            _descriptionPanel = descriptionPanel;
            _crest = crest;
            _icon = icon;
            _gradeText = gradeText;
            _nameText = nameText;
            _descriptionText = descriptionText;
            _effectText = effectText;
            _stateHighlight = stateHighlight;
            _button = button;
            if (isActiveAndEnabled) SubscribeButton();
        }

        public void Bind(AugmentData data, UIAugmentVisualCatalogSO catalog)
        {
            _data = data;
            _catalog = catalog;
            _isHovered = false;
            _isFocused = false;
            _isSelected = false;
            _canSelect = data != null;
            RefreshVisuals();
        }

        public void SetSelectionState(bool isSelected, bool canSelect)
        {
            _isSelected = isSelected;
            _canSelect = canSelect && _data != null;
            RefreshInteraction();
        }

        public void RefreshVisuals()
        {
            UIAugmentVisualCatalogSO.TierVisual visual = _data != null && _catalog != null
                ? _catalog.GetTierVisual(_data.tier) : null;
            SetSprite(_velvetBackground, visual != null ? visual.VelvetBackground : null);
            SetSprite(_descriptionPanel, visual != null ? visual.DescriptionPanel : null);
            SetSprite(_crest, visual != null ? visual.Crest : null);
            SetSprite(_stateHighlight, visual != null ? visual.Crest : null);
            SetSprite(_icon, _data != null && _catalog != null ? _catalog.GetIcon(_data.augmentId) : null);
            if (_icon != null) _icon.color = Color.white;
            SetText(_gradeText, _data != null ? AugmentData.TierName(_data.tier) : string.Empty);
            SetText(_nameText, _data != null ? _data.displayName : string.Empty);
            SetText(_descriptionText, _data != null ? _data.description : string.Empty);
            SetText(_effectText, _data == null ? string.Empty : _data.isInstant ? "즉시 효과" : "런 한정 증강");
            if (_gradeText != null) _gradeText.color = visual != null ? visual.GradeColor : Color.white;
            AlignCrestContents(visual);
            RefreshInteraction();
        }

        private void AlignCrestContents(UIAugmentVisualCatalogSO.TierVisual visual)
        {
            if (_crest == null || visual == null) return;
            float scale = _crest.rectTransform.rect.height / CREST_SOURCE_SIZE;
            // 두 오브젝트를 Crest 자식으로 배치하면 등급별 빈 라벨/아이콘 구멍에 정렬된다.
            if (_icon != null && _icon.transform.parent == _crest.transform)
                _icon.rectTransform.anchoredPosition = new Vector2(0f, CREST_ICON_Y * scale);
            if (_gradeText != null && _gradeText.transform.parent == _crest.transform)
                _gradeText.rectTransform.anchoredPosition = new Vector2(0f, visual.GradeLabelY * scale);
        }

        private void HandleClick()
        {
            if (!_canSelect || _data == null || _button == null || !_button.IsInteractable()) return;
            Selected?.Invoke(_data);
        }

        private void RefreshInteraction()
        {
            if (_button != null) _button.interactable = _canSelect;
            if (_stateHighlight == null) return;
            _stateHighlight.raycastTarget = false;
            _stateHighlight.color = _isSelected ? _selectedColor :
                _canSelect && (_isHovered || _isFocused) ? _hoverColor : Color.clear;
        }

        public void OnPointerEnter(PointerEventData eventData) { _isHovered = true; RefreshInteraction(); }
        public void OnPointerExit(PointerEventData eventData) { _isHovered = false; RefreshInteraction(); }
        public void OnSelect(BaseEventData eventData) { _isFocused = true; RefreshInteraction(); }
        public void OnDeselect(BaseEventData eventData) { _isFocused = false; RefreshInteraction(); }

        private void OnEnable() { SubscribeButton(); RefreshInteraction(); }
        private void OnDisable()
        {
            UnsubscribeButton();
            _isHovered = false;
            _isFocused = false;
            RefreshInteraction();
        }

        private void OnValidate()
        {
            // Inspector 변경으로 실행 중 선택 상태나 선택 이벤트가 초기화되지 않게 한다.
            _hoverColor.a = Mathf.Clamp01(_hoverColor.a);
            _selectedColor.a = Mathf.Clamp01(_selectedColor.a);
        }

        private void SubscribeButton()
        {
            if (_button == null) return;
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }

        private void UnsubscribeButton()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        private static void SetSprite(Image target, Sprite sprite)
        {
            if (target == null) return;
            target.sprite = sprite;
            target.enabled = sprite != null;
            target.preserveAspect = true;
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null) target.text = text ?? string.Empty;
        }
    }
}
