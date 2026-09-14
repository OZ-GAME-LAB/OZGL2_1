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

        public void SetInteractable(bool isInteractable)
        {
            if (!TryGetComponent(out CanvasGroup group)) return;
            group.interactable = isInteractable;
            group.blocksRaycasts = isInteractable;
        }
    }
}
