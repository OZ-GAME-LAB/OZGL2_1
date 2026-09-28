using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>웨이브 표시 데이터와 분리된, 마스크 내부 패널의 펼침/접힘만 담당한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UIWavePreviewDisclosure : MonoBehaviour
    {
        [Header("클릭 대상")]
        [SerializeField] private Button _waveLabelButton;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private RectTransform _arrow;

        [Header("상단 바 아래의 마스크와 이동할 패널")]
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private CanvasGroup _panelCanvasGroup;

        [Header("펼침 설정")]
        [SerializeField] private bool _startExpanded = true;
        [SerializeField, Min(0f)] private float _duration = 0.25f;
        [SerializeField] private Ease _ease = Ease.OutCubic;

        private Tween _transition;
        private float _progress;
        private bool _isExpanded;
        private bool _isInitialized;

        public bool IsExpanded => _isExpanded;
        public bool IsTransitioning => _transition != null && _transition.IsActive();

        private void OnEnable()
        {
            Initialize();
            ApplyProgress(_isExpanded ? 1f : 0f);
            if (_waveLabelButton != null) _waveLabelButton.onClick.AddListener(OnWaveLabelClicked);
            if (_toggleButton != null) _toggleButton.onClick.AddListener(OnToggleClicked);
        }

        private void OnDisable()
        {
            if (_waveLabelButton != null) _waveLabelButton.onClick.RemoveListener(OnWaveLabelClicked);
            if (_toggleButton != null) _toggleButton.onClick.RemoveListener(OnToggleClicked);
            StopTransition();
            // 재활성화 시 중간 위치에 멈추지 않도록 마지막 목표 상태를 유지한다.
            if (_isInitialized) ApplyProgress(_isExpanded ? 1f : 0f);
        }

        private void OnDestroy() => StopTransition();

        private void OnValidate() => _duration = Mathf.Max(0f, _duration);

        private void OnRectTransformDimensionsChange()
        {
            if (_isInitialized) ApplyProgress(_progress);
        }

        private void Initialize()
        {
            if (_isInitialized) return;
            _isExpanded = _startExpanded;
            _progress = _isExpanded ? 1f : 0f;
            _isInitialized = true;
        }

        private void OnWaveLabelClicked()
        {
            if (_waveLabelButton != null && _waveLabelButton.IsInteractable()) Toggle();
        }

        private void OnToggleClicked()
        {
            if (_toggleButton != null && _toggleButton.IsInteractable()) Toggle();
        }

        public void Toggle()
        {
            Initialize();
            SetExpanded(!_isExpanded);
        }

        /// <summary>외부 UI에서도 열림 상태를 지정할 수 있다. 웨이브/전투 데이터는 변경하지 않는다.</summary>
        public void SetExpanded(bool expanded, bool animate = true)
        {
            Initialize();
            StopTransition();
            _isExpanded = expanded;
            float target = expanded ? 1f : 0f;
            if (!animate || !Application.isPlaying || !isActiveAndEnabled || _duration <= 0f ||
                _panel == null || _viewport == null || Mathf.Approximately(_progress, target))
            {
                ApplyProgress(target);
                return;
            }

            ApplyProgress(_progress);
            // 빠른 반전도 현재 위치에서 시작하고, 일시정지 중에도 UI는 동작한다.
            float remainingDuration = _duration * Mathf.Abs(target - _progress);
            _transition = DOTween.To(() => _progress, ApplyProgress, target, remainingDuration)
                .SetEase(_ease).SetUpdate(true)
                .OnComplete(() =>
                {
                    _transition = null;
                    ApplyProgress(target);
                });
        }

        private void ApplyProgress(float progress)
        {
            _progress = Mathf.Clamp01(progress);
            if (_panel != null && _viewport != null)
            {
                // 패널/Viewport는 좌상단 pivot. 마스크 위쪽으로 완전히 이동시킨다.
                float hiddenOffset = Mathf.Max(_viewport.rect.height, _panel.rect.height);
                _panel.anchoredPosition = new Vector2(0f, hiddenOffset * (1f - _progress));
            }
            if (_arrow != null)
                _arrow.localRotation = Quaternion.Euler(0f, 0f, 180f * _progress);
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = _progress > 0f ? 1f : 0f;
                _panelCanvasGroup.interactable = _isExpanded && _progress >= 1f;
                _panelCanvasGroup.blocksRaycasts = _isExpanded && _progress >= 1f;
            }
        }

        private void StopTransition()
        {
            Tween previous = _transition;
            _transition = null;
            if (previous != null && previous.IsActive()) previous.Kill(false);
        }
    }
}
