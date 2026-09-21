using TMPro;
using UnityEngine;
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
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Color _textColor = new Color32(248, 242, 235, 255);
        [SerializeField] private Color _disabledTextColor = new Color32(122, 116, 116, 255);
        private bool _isChosen;

        public bool IsChosen => _isChosen;

        public void SetChosen(bool isChosen)
        {
            _isChosen = isChosen;
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (!(targetGraphic is Image image)) return;
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
        }
    }
}
