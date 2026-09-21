using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 키보드 포커스와 카테고리 선택을 분리하고, 글자 없는 상태별 Sprite 위에 TMP를 표시한다.
    [AddComponentMenu("UI/Skill Art Button")]
    public sealed class UISkillArtButton : Button
    {
        [SerializeField] private Sprite _normal;
        [SerializeField] private Sprite _hover;
        [SerializeField] private Sprite _pressed;
        [SerializeField] private Sprite _chosen;
        [SerializeField] private Sprite _disabled;
        [SerializeField] private bool _keepChosenWhilePressed;
        [SerializeField] private bool _separatePointerHover;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Color _textColor = new Color32(248, 242, 235, 255);
        [SerializeField] private Color _disabledTextColor = new Color32(122, 116, 116, 255);
        private bool _isChosen;
        private bool _isPointerInside;

        public bool IsChosen => _isChosen;

        public override void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            base.OnPointerEnter(eventData);
            if (_separatePointerHover) DoStateTransition(currentSelectionState, false);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            base.OnPointerExit(eventData);
            if (_separatePointerHover) DoStateTransition(currentSelectionState, false);
        }

        protected override void OnDisable()
        {
            _isPointerInside = false;
            base.OnDisable();
        }

        public void SetChosen(bool isChosen)
        {
            _isChosen = isChosen;
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (!(targetGraphic is Image image)) return;
            bool hasFocus = state == SelectionState.Selected;
            // 확인창은 취소에 기본 포커스를 주되, 프레임 호버는 실제 포인터로 구분한다.
            if (_separatePointerHover && hasFocus)
                state = _isPointerInside ? SelectionState.Highlighted : SelectionState.Normal;
            Sprite sprite;
            if (state == SelectionState.Disabled) sprite = _disabled;
            // 스킬 카드는 다시 눌러도 실제 선택 표시를 유지한다. 다른 버튼은 기존 누름 상태를 따른다.
            else if (_isChosen && _keepChosenWhilePressed) sprite = _chosen;
            else if (state == SelectionState.Pressed) sprite = _pressed;
            else if (_isChosen) sprite = _chosen;
            else if (state == SelectionState.Highlighted || state == SelectionState.Selected) sprite = _hover;
            else sprite = _normal;
            image.overrideSprite = sprite != null ? sprite : _normal;
            if (_label != null) _label.color = state == SelectionState.Disabled ? _disabledTextColor : _textColor;
            if (_separatePointerHover)
            {
                Color tint = colors.normalColor;
                if (state == SelectionState.Disabled) tint = colors.disabledColor;
                else if (state == SelectionState.Pressed) tint = colors.pressedColor;
                else if (state == SelectionState.Highlighted) tint = colors.highlightedColor;
                image.CrossFadeColor(tint * colors.colorMultiplier, instant ? 0 : colors.fadeDuration, true, true);
                if (_label != null && hasFocus && state != SelectionState.Disabled)
                    _label.color = colors.selectedColor; // 키보드 포커스는 글자 색으로 유지한다.
            }
        }
    }
}
