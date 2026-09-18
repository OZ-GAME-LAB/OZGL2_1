using UnityEngine;
using UnityEngine.EventSystems;

namespace OZGL2.UIFlow
{
    // 빈 영역 클릭만 처리한다. 노드 Button의 클릭과 ScrollRect의 드래그를 가로채지 않는다.
    public sealed class UITraitDismissSurface : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private UITraitOverlayView _view;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_view != null && eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
                _view.HideDetail();
        }
    }
}
