using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OZGL2.UIFlow
{
    // Scene에 미리 배치한 팝업을 켜고 끈다. UI 오브젝트나 표시 내용을 생성하지 않는다.
    public sealed class UIPopupController : MonoBehaviour
    {
        [SerializeField] private Transform _popupRoot;
        [SerializeField] private CanvasGroup _screenGroup;
        private readonly List<UIPopupPanel> _openPopups = new List<UIPopupPanel>();
        private readonly List<GameObject> _previousSelections = new List<GameObject>();
        public int OpenCount => _openPopups.Count;
        public bool IsTopPopup(UIPopupPanel popup) => popup != null && _openPopups.Count > 0 && _openPopups[_openPopups.Count - 1] == popup;

        public void OpenPopup(UIPopupPanel popup)
        {
            if (popup == null || _popupRoot == null || !popup.transform.IsChildOf(_popupRoot) || _openPopups.Contains(popup)) return;
            _previousSelections.Add(EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null);
            if (_openPopups.Count > 0) _openPopups[_openPopups.Count - 1].SetInteractable(false);
            if (_screenGroup != null) _screenGroup.interactable = false;
            _openPopups.Add(popup);
            popup.transform.SetAsLastSibling();
            popup.BindController(this);
            popup.gameObject.SetActive(true);
            popup.SetInteractable(true);
            if (popup.FirstSelected != null) popup.FirstSelected.Select();
            else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void CloseTopPopup()
        {
            if (_openPopups.Count == 0) return;
            UIPopupPanel popup = _openPopups[_openPopups.Count - 1];
            // 확인창이 열리면 대상이 바뀌므로, 원래 팝업이 여전히 맨 위인지도 확인한다.
            if (!popup.TryDismiss() || !IsTopPopup(popup)) return;
            CloseConfirmedPopup();
        }

        // 증강처럼 선택이 필요한 창은 확인 버튼으로만 닫을 수 있다.
        public void CloseConfirmedPopup()
        {
            if (_openPopups.Count == 0) return;
            int index = _openPopups.Count - 1;
            UIPopupPanel popup = _openPopups[index];
            GameObject previousSelection = _previousSelections[index];
            _openPopups.RemoveAt(index);
            _previousSelections.RemoveAt(index);
            if (popup != null)
            {
                popup.gameObject.SetActive(false);
                popup.BindController(null);
            }
            if (_openPopups.Count > 0) _openPopups[_openPopups.Count - 1].SetInteractable(true);
            else if (_screenGroup != null) _screenGroup.interactable = true;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseTopPopup();
        }

        private void OnDisable()
        {
            while (_openPopups.Count > 0) CloseConfirmedPopup();
        }
    }
}
