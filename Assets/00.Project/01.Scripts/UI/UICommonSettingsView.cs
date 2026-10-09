using System.Collections;
using OZGL2.Tutorial;
using OZGL2.UIBridge;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// 공통 설정창의 음량·전체화면·설명 초기화와 표시를 관리한다.
    /// 로비/전투의 BGM·SFX와 닫기는 기존 View에 맡기고, 타이틀만 독립 연결한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UICommonSettingsView : MonoBehaviour
    {
        [Header("소리 크기")]
        [SerializeField] private Slider _volumeSlider;
        [SerializeField] private TMP_Text _volumeLabel;

        [Header("전체화면")]
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private TMP_Text _fullscreenState;

        [Header("마왕의 설명")]
        [SerializeField] private Button _tutorialButton;
        [SerializeField] private TMP_Text _tutorialState;
        [SerializeField, Min(0f)] private float _tutorialFeedbackSeconds = 1.6f;

        [Header("기존 오디오 표시 / 타이틀 독립 연결")]
        [SerializeField] private Toggle _bgmToggle;
        [SerializeField] private Toggle _sfxToggle;
        [SerializeField] private TMP_Text _bgmState;
        [SerializeField] private TMP_Text _sfxState;
        [SerializeField] private Button _backButton;

        private const string VOLUME_KEY = "OZGL2.Volume";
        private const string TUTORIAL_DEFAULT_TEXT = "다시 보기";
        private const string TUTORIAL_FEEDBACK_TEXT = "초기화됨!";

        private bool _isFullscreen;
        private bool _isStandalone;
        private bool _hasBoundListeners;
        private UnityAction _closeAction;
        private Coroutine _tutorialFeedbackRoutine;

        public bool IsConfigured => _volumeSlider != null && _volumeLabel != null &&
                                    _fullscreenToggle != null && _fullscreenState != null &&
                                    _tutorialButton != null && _tutorialState != null &&
                                    _bgmToggle != null && _sfxToggle != null &&
                                    _bgmState != null && _sfxState != null && _backButton != null &&
                                    Mathf.Approximately(_volumeSlider.minValue, 0f) &&
                                    Mathf.Approximately(_volumeSlider.maxValue, 1f) &&
                                    !_volumeSlider.wholeNumbers;

        // 설정창은 시작 비활성이므로 Awake 이전에도 직접 실행한 씬에 저장 음량을 적용한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeStoredVolume()
        {
            float volume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
            AudioListener.volume = float.IsNaN(volume) ? 1f : Mathf.Clamp01(volume);
        }

        private void Awake()
        {
            InitializeStoredVolume();
        }

        /// <summary>타이틀만 BGM·SFX·닫기를 직접 연결한다. 로비/전투에서는 호출하지 않는다.</summary>
        public void ConfigureStandalone(UnityAction closeAction)
        {
            UnbindListeners();
            _isStandalone = true;
            _closeAction = closeAction;
            if (isActiveAndEnabled) BindListeners();
        }

        private void OnEnable()
        {
            RefreshControls();
            BindListeners();
        }

        private void OnDisable()
        {
            UnbindListeners();
            StopTutorialFeedback();
            if (_tutorialState != null) _tutorialState.text = TUTORIAL_DEFAULT_TEXT;
        }

        private void RefreshControls()
        {
            float volume = Mathf.Clamp01(AudioListener.volume);
            if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(volume);
            RefreshVolumeLabel(volume);

            _isFullscreen = Screen.fullScreen;
            if (_fullscreenToggle != null) _fullscreenToggle.SetIsOnWithoutNotify(_isFullscreen);
            RefreshFullscreenState();
            RefreshAudioStates();
            if (_tutorialState != null) _tutorialState.text = TUTORIAL_DEFAULT_TEXT;
        }

        private void BindListeners()
        {
            if (_hasBoundListeners) return;
            if (_volumeSlider != null) _volumeSlider.onValueChanged.AddListener(SetVolume);
            if (_fullscreenToggle != null) _fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            if (_tutorialButton != null) _tutorialButton.onClick.AddListener(ResetTutorial);

            // 같은 토글에 기존 View 경로와 직접 저장 경로를 함께 연결하지 않는다.
            if (_isStandalone)
            {
                if (_bgmToggle != null) _bgmToggle.onValueChanged.AddListener(SetBgm);
                if (_sfxToggle != null) _sfxToggle.onValueChanged.AddListener(SetSfx);
                if (_backButton != null && _closeAction != null) _backButton.onClick.AddListener(_closeAction);
            }

            _hasBoundListeners = true;
        }

        private void UnbindListeners()
        {
            if (!_hasBoundListeners) return;
            if (_volumeSlider != null) _volumeSlider.onValueChanged.RemoveListener(SetVolume);
            if (_fullscreenToggle != null) _fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
            if (_tutorialButton != null) _tutorialButton.onClick.RemoveListener(ResetTutorial);
            if (_isStandalone)
            {
                if (_bgmToggle != null) _bgmToggle.onValueChanged.RemoveListener(SetBgm);
                if (_sfxToggle != null) _sfxToggle.onValueChanged.RemoveListener(SetSfx);
                if (_backButton != null && _closeAction != null) _backButton.onClick.RemoveListener(_closeAction);
            }

            _hasBoundListeners = false;
        }

        private void SetVolume(float value)
        {
            float volume = float.IsNaN(value) ? 1f : Mathf.Clamp01(value);
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(VOLUME_KEY, volume);
            RefreshVolumeLabel(volume);
        }

        private void RefreshVolumeLabel(float volume)
        {
            if (_volumeLabel != null) _volumeLabel.text = Mathf.RoundToInt(volume * 100f) + "%";
        }

        private void SetFullscreen(bool isEnabled)
        {
            _isFullscreen = isEnabled;
            Screen.fullScreen = isEnabled;
            RefreshFullscreenState();
        }

        private void RefreshFullscreenState()
        {
            if (_fullscreenState != null) _fullscreenState.text = _isFullscreen ? "켜짐" : "꺼짐";
        }

        private void SetBgm(bool isEnabled)
        {
            GameAudioSettings.SetBgm(isEnabled);
            RefreshAudioStates();
        }

        private void SetSfx(bool isEnabled)
        {
            GameAudioSettings.SetSfx(isEnabled);
            RefreshAudioStates();
        }

        private void RefreshAudioStates()
        {
            bool isBgmEnabled = GameAudioSettings.BgmEnabled;
            bool isSfxEnabled = GameAudioSettings.SfxEnabled;
            if (_bgmToggle != null) _bgmToggle.SetIsOnWithoutNotify(isBgmEnabled);
            if (_sfxToggle != null) _sfxToggle.SetIsOnWithoutNotify(isSfxEnabled);
            if (_bgmState != null) _bgmState.text = isBgmEnabled ? "켜짐" : "꺼짐";
            if (_sfxState != null) _sfxState.text = isSfxEnabled ? "켜짐" : "꺼짐";
        }

        private void ResetTutorial()
        {
            TutorialStore.ResetAll();
            ShowTutorialResetFeedback();
        }

        // 표시만 분리해 계정의 튜토리얼 기록을 초기화하지 않고도 피드백을 검증할 수 있다.
        private void ShowTutorialResetFeedback()
        {
            StopTutorialFeedback();
            if (_tutorialState == null) return;
            _tutorialState.text = TUTORIAL_FEEDBACK_TEXT;
            _tutorialFeedbackRoutine = StartCoroutine(RestoreTutorialState());
        }

        private IEnumerator RestoreTutorialState()
        {
            yield return new WaitForSecondsRealtime(_tutorialFeedbackSeconds);
            if (_tutorialState != null) _tutorialState.text = TUTORIAL_DEFAULT_TEXT;
            _tutorialFeedbackRoutine = null;
        }

        private void StopTutorialFeedback()
        {
            if (_tutorialFeedbackRoutine == null) return;
            StopCoroutine(_tutorialFeedbackRoutine);
            _tutorialFeedbackRoutine = null;
        }
    }
}
