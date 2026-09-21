using System;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 팝업의 배치는 Scene에 보관하고, 이 컴포넌트는 닫기 정책과 기본 선택만 제공한다.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIPopupPanel : MonoBehaviour
    {
        [SerializeField] private bool _canDismiss = true;
        [SerializeField] private Selectable _firstSelected;
        public bool CanDismiss => _canDismiss;
        public Selectable FirstSelected => _firstSelected;
        internal UIPopupController Controller { get; private set; }
        private Func<bool> _dismissGuard;

        // 필요한 화면만 등록한다. 등록하지 않은 기존 팝업의 닫기 정책은 그대로 유지한다.
        public void SetDismissGuard(Func<bool> guard) => _dismissGuard = guard;
        internal void BindController(UIPopupController controller) => Controller = controller;
        internal bool TryDismiss() => _canDismiss && (_dismissGuard == null || _dismissGuard());

        public void SetInteractable(bool isInteractable)
        {
            if (!TryGetComponent(out CanvasGroup group)) return;
            group.interactable = isInteractable;
            group.blocksRaycasts = isInteractable;
        }
    }
}
