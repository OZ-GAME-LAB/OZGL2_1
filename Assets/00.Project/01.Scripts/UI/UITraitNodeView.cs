using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    [DisallowMultipleComponent]
    public sealed class UITraitNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button _button;
        [SerializeField] private Text _rankText;
        [SerializeField] private UITraitTooltipController _tooltipController;

        private UITraitDisplayData _data;
        public UITraitDisplayData Data => _data;
        public bool CanReceiveInput => isActiveAndEnabled && _button != null && _button.IsInteractable();
        public event Action<UITraitNodeView> UpgradeRequested;

        public void ShowTrait(UITraitDisplayData data)
        {
            _data = data;
            // 잠긴 노드도 호버 설명을 받을 수 있도록 Button 자체는 비활성화하지 않는다.
            if (_rankText != null)
                _rankText.text = !data.IsUnlocked ? "잠금" : $"{data.CurrentRank}/{data.MaxRank}";
            if (_tooltipController != null) _tooltipController.RefreshTrait(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CanReceiveInput && _tooltipController != null)
                _tooltipController.BeginHover(this, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_tooltipController != null) _tooltipController.EndHover(this);
        }

        private void OnEnable()
        {
            if (_button == null) TryGetComponent(out _button);
            if (_button == null) return;
            _button.onClick.RemoveListener(RequestUpgrade);
            _button.onClick.AddListener(RequestUpgrade);
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(RequestUpgrade);
            if (_tooltipController != null) _tooltipController.EndHover(this);
        }

        private void RequestUpgrade()
        {
            if (!CanReceiveInput) return;
            // 잠금/최대 단계 등으로 상승하지 못한 클릭도 현재 방문의 설명을 닫는다.
            if (_tooltipController != null) _tooltipController.SuppressHoverUntilExit(this);
            if (_data.CanUpgrade) UpgradeRequested?.Invoke(this);
        }
    }
}
