using System.Collections;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.Startup
{
    [DisallowMultipleComponent]
    public sealed class TeamLogoIntro : MonoBehaviour
    {
        private const string TEAM_NAME = "STUDIO M4";
        private const float MAX_ANIMATION_FRAME_TIME = 0.1f;

        [Header("화면 연결")]
        [SerializeField] private Image _background;
        [SerializeField] private Image _logo;
        [SerializeField] private TMP_Text _teamName;
        [SerializeField] private Image _fadeOverlay;
        [SerializeField] private UISceneNavigator _navigator;
        [SerializeField] private string _nextScenePath = "Assets/00.Scenes/Builds/Title.unity";

        [Header("연출 시간 (초)")]
        [SerializeField, Min(0f)] private float _blackHoldDuration = 0.25f;
        [SerializeField, Min(0f)] private float _logoFadeDuration = 1.1f;
        [SerializeField, Min(0f)] private float _mintHoldDuration = 0.3f;
        [SerializeField, Min(0f)] private float _backgroundFadeDuration = 1.3f;
        [SerializeField, Min(0f)] private float _fullLogoHoldDuration = 0.15f;
        [SerializeField, Min(0f)] private float _characterInterval = 0.125f;
        [SerializeField, Min(0f)] private float _completeHoldDuration = 1f;
        [SerializeField, Min(0f)] private float _fadeOutDuration = 0.6f;
        [SerializeField, Min(0f)] private float _transitionHoldDuration = 0.15f;

        private Coroutine _introRoutine;
        private bool _isTransitioning;

        private void OnEnable()
        {
            if (_introRoutine != null || _isTransitioning) return;
            if (!HasRequiredReferences())
            {
                LogMissingReferences();
                return;
            }

            _background.color = Color.black;
            _logo.color = new Color(1f, 1f, 1f, 0f);
            _teamName.color = Color.black;
            // 문구 전체의 폭을 먼저 확보해 글자가 늘어날 때 중앙 정렬이 흔들리지 않게 한다.
            _teamName.text = TEAM_NAME;
            _teamName.maxVisibleCharacters = 0;
            _fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
            _introRoutine = StartCoroutine(PlayIntro());
        }

        private void OnDisable()
        {
            if (_introRoutine == null) return;
            StopCoroutine(_introRoutine);
            _introRoutine = null;
        }

        private IEnumerator PlayIntro()
        {
            // Play 진입과 Scene 로딩에 걸린 첫 프레임 시간은 연출에 포함하지 않는다.
            yield return null;

            float logoFadeStart = Mathf.Max(0f, _blackHoldDuration);
            float logoFadeDuration = Mathf.Max(0f, _logoFadeDuration);
            float backgroundFadeStart = logoFadeStart + logoFadeDuration + Mathf.Max(0f, _mintHoldDuration);
            float backgroundFadeDuration = Mathf.Max(0f, _backgroundFadeDuration);
            float typingStart = backgroundFadeStart + backgroundFadeDuration + Mathf.Max(0f, _fullLogoHoldDuration);
            float characterInterval = Mathf.Max(0f, _characterInterval);
            float typingEnd = typingStart + characterInterval * (TEAM_NAME.Length - 1);
            float fadeOutStart = typingEnd + Mathf.Max(0f, _completeHoldDuration);
            float fadeOutDuration = Mathf.Max(0f, _fadeOutDuration);
            float sequenceEnd = fadeOutStart + fadeOutDuration + Mathf.Max(0f, _transitionHoldDuration);
            float elapsed = 0f;

            // 모든 시간이 0이어도 한 프레임은 양보해 코루틴의 시작/종료 순서를 보장한다.
            do
            {
                if (!HasRequiredReferences())
                {
                    LogMissingReferences();
                    _introRoutine = null;
                    yield break;
                }

                float logoAlpha = GetSmoothProgress(elapsed, logoFadeStart, logoFadeDuration);
                float backgroundProgress = GetSmoothProgress(elapsed, backgroundFadeStart, backgroundFadeDuration);
                float overlayAlpha = GetSmoothProgress(elapsed, fadeOutStart, fadeOutDuration);
                _logo.color = new Color(1f, 1f, 1f, logoAlpha);
                _background.color = Color.Lerp(Color.black, Color.white, backgroundProgress);
                _fadeOverlay.color = new Color(0f, 0f, 0f, overlayAlpha);

                if (elapsed < typingStart)
                    _teamName.maxVisibleCharacters = 0;
                else if (characterInterval <= 0f)
                    _teamName.maxVisibleCharacters = TEAM_NAME.Length;
                else
                    _teamName.maxVisibleCharacters = Mathf.Clamp(
                        Mathf.FloorToInt((elapsed - typingStart) / characterInterval) + 1, 0, TEAM_NAME.Length);

                yield return null;
                // unscaledDeltaTime에는 프레임 상한이 없어 로딩 지연 시 연출 전체가 생략될 수 있다.
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MAX_ANIMATION_FRAME_TIME);
            }
            while (elapsed < sequenceEnd);

            _introRoutine = null;
            if (!HasRequiredReferences())
            {
                LogMissingReferences();
                yield break;
            }

            _fadeOverlay.color = Color.black;
            _isTransitioning = true;
            if (!_navigator.TryLoadScene(_nextScenePath, out string error))
            {
                _isTransitioning = false;
                Debug.LogError("팀 로고 연출 후 Scene 이동에 실패했습니다: " + error, this);
            }
        }

        private bool HasRequiredReferences()
        {
            return _background != null && _logo != null && _logo.sprite != null &&
                   _teamName != null && _fadeOverlay != null && _navigator != null &&
                   !string.IsNullOrWhiteSpace(_nextScenePath);
        }

        private void LogMissingReferences()
        {
            Debug.LogError("팀 로고 연출의 Background, Logo(Sprite 포함), Team Name, Fade Overlay, " +
                           "Navigator 및 Next Scene Path 연결을 확인해 주세요.", this);
        }

        private static float GetSmoothProgress(float elapsed, float start, float duration)
        {
            if (duration <= 0f) return elapsed >= start ? 1f : 0f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - start) / duration));
        }
    }
}
