using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// 전투 메뉴 팝업의 메뉴/설정 전환과 미리보기용 음향 상태를 관리한다.
    /// 실제 음향 저장과 게임 종료는 외부 시스템이 UnityEvent에 연결한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup), typeof(UIPopupPanel))]
    public sealed class UIBattleMenuPopupView : MonoBehaviour
    {
        [Header("팝업")]
        [SerializeField] private UIPopupPanel _popupPanel;
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private Selectable _menuFirst;
        [SerializeField] private Selectable _settingsFirst;

        [Header("오디오 설정")]
        [SerializeField] private Toggle[] _bgmToggles;
        [SerializeField] private Toggle[] _sfxToggles;
        [SerializeField] private TMP_Text[] _bgmStateLabels;
        [SerializeField] private TMP_Text[] _sfxStateLabels;
        [SerializeField] private UnityEvent<bool> _bgmEnabledChanged = new UnityEvent<bool>();
        [SerializeField] private UnityEvent<bool> _sfxEnabledChanged = new UnityEvent<bool>();
        [SerializeField] private bool _isBgmEnabled = true;
        [SerializeField] private bool _isSfxEnabled = true;

        [Header("메뉴 요청")]
        [SerializeField] private UnityEvent _quitRequested = new UnityEvent();

        public bool IsBgmEnabled => _isBgmEnabled;
        public bool IsSfxEnabled => _isSfxEnabled;
        public bool IsSettingsOpen => _settingsPanel != null && _settingsPanel.activeSelf;

        public void OpenMenu()
        {
            if (_menuPanel == null || _settingsPanel == null) return;
            _menuPanel.SetActive(true);
            _settingsPanel.SetActive(false);
            Select(_menuFirst);
        }

        public void OpenSettings()
        {
            if (_menuPanel == null || _settingsPanel == null) return;
            _menuPanel.SetActive(false);
            _settingsPanel.SetActive(true);
            Select(_settingsFirst);
        }

        public void Close()
        {
            if (_popupPanel != null && _popupPanel.Controller != null)
            {
                _popupPanel.Controller.CloseTopPopup();
                return;
            }

            gameObject.SetActive(false);
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

        public void RequestQuit() => _quitRequested.Invoke();

        private void OnEnable()
        {
            if (_popupPanel == null) _popupPanel = GetComponent<UIPopupPanel>();
            if (_popupPanel != null) _popupPanel.SetDismissGuard(HandleDismissRequest);
            UpdateAudioControls(_bgmToggles, _bgmStateLabels, _isBgmEnabled);
            UpdateAudioControls(_sfxToggles, _sfxStateLabels, _isSfxEnabled);
            OpenMenu();
        }

        private void OnDisable()
        {
            if (_popupPanel != null) _popupPanel.SetDismissGuard(null);
            if (_menuPanel != null) _menuPanel.SetActive(true);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        private bool HandleDismissRequest()
        {
            if (!IsSettingsOpen) return true;
            OpenMenu();
            return false;
        }

        private static void Select(Selectable selectable)
        {
            if (selectable != null && selectable.isActiveAndEnabled) selectable.Select();
        }

        private static void UpdateAudioControls(Toggle[] toggles, TMP_Text[] labels, bool isEnabled)
        {
            if (toggles != null)
                foreach (Toggle toggle in toggles)
                    if (toggle != null) toggle.SetIsOnWithoutNotify(isEnabled);

            if (labels != null)
                foreach (TMP_Text label in labels)
                    if (label != null) label.text = isEnabled ? "켜짐" : "꺼짐";
        }
    }
}
