using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>확인창의 편집 가능한 표시와 버튼 연결만 담당한다. 저장 초기화는 TitleScreen에 맡긴다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleConfirmationView : MonoBehaviour
    {
        [Header("삭제하고 시작")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Image _confirmBody;
        [SerializeField] private Image _confirmGlow;
        [SerializeField] private TMP_Text _confirmLabel;
        [Header("취소")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Image _cancelBody;
        [SerializeField] private Image _cancelGlow;
        [SerializeField] private TMP_Text _cancelLabel;
        [SerializeField] private Color _cancelHoverTextColor = new Color(1f, 0.84f, 0.48f);

        private UnityAction _onConfirmed;
        private UnityAction _onCancelled;

        /// <summary>이벤트와 기존 타이틀 버튼 효과만 연결한다. 프리팹의 배치·문구·글꼴은 덮어쓰지 않는다.</summary>
        public bool Bind(UnityAction onConfirmed, UnityAction onCancelled)
        {
            if (_confirmButton == null || _cancelButton == null) return false;

            Unbind();
            _onConfirmed = onConfirmed;
            _onCancelled = onCancelled;
            if (_onConfirmed != null) _confirmButton.onClick.AddListener(_onConfirmed);
            if (_onCancelled != null) _cancelButton.onClick.AddListener(_onCancelled);

            ConfigureButtonEffect(_confirmButton, _confirmBody, _confirmGlow, _confirmLabel, true, Color.white);
            ConfigureButtonEffect(_cancelButton, _cancelBody, _cancelGlow, _cancelLabel, false, _cancelHoverTextColor);
            return true;
        }

        private static void ConfigureButtonEffect(Button button, Image body, Image glow, TMP_Text label,
            bool primary, Color hoverTextColor)
        {
            if (body == null || glow == null || label == null) return;
            if (!button.TryGetComponent<TitleButtonFx>(out var effect))
                effect = button.gameObject.AddComponent<TitleButtonFx>();
            effect.Setup(body, null, glow, null, label, body.color, Color.white,
                Color.clear, Color.clear, label.color, hoverTextColor, glow.color, primary);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (_confirmButton != null && _onConfirmed != null) _confirmButton.onClick.RemoveListener(_onConfirmed);
            if (_cancelButton != null && _onCancelled != null) _cancelButton.onClick.RemoveListener(_onCancelled);
            _onConfirmed = null;
            _onCancelled = null;
        }
    }
}
