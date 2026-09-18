using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 기존 팝업보다 먼저 ESC를 확인해, 같은 키 입력으로 닫기와 열기가 동시에 발생하지 않게 한다.
    [DefaultExecutionOrder(-100)]
    public sealed class UILobbyOverlayView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _lobbyGroup;
        [SerializeField] private UIPopupController _legacyPopupController;
        [SerializeField] private GraphicRaycaster _legacyRaycaster;
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private UITraitOverlayView _traitsScreen;
        [SerializeField] private Selectable _menuFirst;
        [SerializeField] private Selectable _settingsFirst;
        [SerializeField] private Selectable _traitsFirst;
        [SerializeField] private Toggle[] _bgmToggles;
        [SerializeField] private Toggle[] _sfxToggles;
        [SerializeField] private TMP_Text[] _bgmStateLabels;
        [SerializeField] private TMP_Text[] _sfxStateLabels;
        [SerializeField] private UnityEvent<bool> _bgmEnabledChanged = new UnityEvent<bool>();
        [SerializeField] private UnityEvent<bool> _sfxEnabledChanged = new UnityEvent<bool>();
        [SerializeField] private UnityEvent _quitRequested = new UnityEvent();

        private bool _isLocked;
        private bool _wasInteractable;
        private bool _wasBlockingRaycasts;
        private bool _wasLegacyEnabled;
        private bool _wasLegacyRaycasterEnabled;
        private bool _isBgmEnabled = true;
        private bool _isSfxEnabled = true;
        private GameObject _previousSelection;

        public bool IsOpen => IsActive(_menuPanel) || IsActive(_settingsPanel) ||
                              (_traitsScreen != null && _traitsScreen.gameObject.activeSelf);
        public bool IsBgmEnabled => _isBgmEnabled;
        public bool IsSfxEnabled => _isSfxEnabled;

        public void OpenMenu()
        {
            if (_menuPanel == null || HasLegacyPopup()) return;
            LockBackground();
            _menuPanel.SetActive(true);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            SetTraitInteraction(false);
            Select(_menuFirst);
        }

        public void OpenTraits()
        {
            if (_traitsScreen == null || HasLegacyPopup()) return;
            LockBackground();
            if (_menuPanel != null) _menuPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            _traitsScreen.gameObject.SetActive(true);
            SetTraitInteraction(true);
            Select(_traitsFirst);
        }

        public void OpenSettings()
        {
            if (_settingsPanel == null || HasLegacyPopup()) return;
            LockBackground();
            if (_menuPanel != null) _menuPanel.SetActive(false);
            _settingsPanel.SetActive(true);
            SetTraitInteraction(false);
            Select(_settingsFirst);
        }

        public void CloseTop()
        {
            if (IsActive(_settingsPanel))
            {
                OpenMenu();
                return;
            }
            if (IsActive(_menuPanel))
            {
                _menuPanel.SetActive(false);
                SetTraitInteraction(true);
                if (_traitsScreen != null && _traitsScreen.gameObject.activeSelf) Select(_traitsFirst);
                else UnlockBackground();
                return;
            }
            if (_traitsScreen != null && _traitsScreen.gameObject.activeSelf)
            {
                if (_traitsScreen.IsResetConfirmationOpen) _traitsScreen.CancelReset();
                else if (_traitsScreen.IsDetailOpen) _traitsScreen.HideDetail();
                else CloseTraits();
            }
        }

        public void CloseTraits()
        {
            if (_traitsScreen != null)
            {
                _traitsScreen.HideDetail();
                _traitsScreen.gameObject.SetActive(false);
            }
            if (!IsOpen) UnlockBackground();
        }

        public void SetBgmEnabled(bool isEnabled)
        {
            bool hasChanged = _isBgmEnabled != isEnabled;
            _isBgmEnabled = isEnabled;
            UpdateAudioControls(_bgmToggles, _bgmStateLabels, isEnabled);
            if (hasChanged) _bgmEnabledChanged.Invoke(isEnabled);
        }

        public void SetSfxEnabled(bool isEnabled)
        {
            bool hasChanged = _isSfxEnabled != isEnabled;
            _isSfxEnabled = isEnabled;
            UpdateAudioControls(_sfxToggles, _sfxStateLabels, isEnabled);
            if (hasChanged) _sfxEnabledChanged.Invoke(isEnabled);
        }

        // 오디오 저장/믹서와 앱 종료는 UI가 소유하지 않는다. 실제 시스템 연결 지점만 제공한다.
        public void RequestQuit() => _quitRequested.Invoke();

        private void OnEnable()
        {
            UpdateAudioControls(_bgmToggles, _bgmStateLabels, _isBgmEnabled);
            UpdateAudioControls(_sfxToggles, _sfxStateLabels, _isSfxEnabled);
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame || HasLegacyPopup()) return;
            if (IsOpen) CloseTop();
            else OpenMenu();
        }

        private void OnDisable()
        {
            if (_menuPanel != null) _menuPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_traitsScreen != null) _traitsScreen.gameObject.SetActive(false);
            UnlockBackground();
        }

        private bool HasLegacyPopup() => _legacyPopupController != null && _legacyPopupController.OpenCount > 0;
        private static bool IsActive(GameObject panel) => panel != null && panel.activeSelf;

        private void LockBackground()
        {
            if (_isLocked) return;
            _isLocked = true;
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (_lobbyGroup != null)
            {
                _wasInteractable = _lobbyGroup.interactable;
                _wasBlockingRaycasts = _lobbyGroup.blocksRaycasts;
                _lobbyGroup.interactable = false;
                _lobbyGroup.blocksRaycasts = false;
            }
            if (_legacyPopupController != null)
            {
                _wasLegacyEnabled = _legacyPopupController.enabled;
                _legacyPopupController.enabled = false;
            }
            if (_legacyRaycaster != null)
            {
                _wasLegacyRaycasterEnabled = _legacyRaycaster.enabled;
                _legacyRaycaster.enabled = false;
            }
        }

        private void UnlockBackground()
        {
            if (!_isLocked) return;
            _isLocked = false;
            if (_lobbyGroup != null)
            {
                _lobbyGroup.interactable = _wasInteractable;
                _lobbyGroup.blocksRaycasts = _wasBlockingRaycasts;
            }
            if (_legacyPopupController != null) _legacyPopupController.enabled = _wasLegacyEnabled;
            if (_legacyRaycaster != null) _legacyRaycaster.enabled = _wasLegacyRaycasterEnabled;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
        }

        private void SetTraitInteraction(bool isEnabled)
        {
            if (_traitsScreen != null && _traitsScreen.TryGetComponent(out CanvasGroup group))
            {
                group.interactable = isEnabled;
                group.blocksRaycasts = isEnabled;
            }
        }

        private static void Select(Selectable selectable)
        {
            if (selectable != null && selectable.isActiveAndEnabled) selectable.Select();
        }

        private static void UpdateAudioControls(Toggle[] toggles, TMP_Text[] labels, bool isEnabled)
        {
            if (toggles != null) foreach (Toggle toggle in toggles) if (toggle != null) toggle.SetIsOnWithoutNotify(isEnabled);
            if (labels != null) foreach (TMP_Text label in labels) if (label != null) label.text = isEnabled ? "켜짐" : "꺼짐";
        }
    }
}
