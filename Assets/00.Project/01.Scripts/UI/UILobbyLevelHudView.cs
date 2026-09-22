using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 계정 성장/저장은 소유하지 않고, 외부에서 전달한 레벨과 현재 레벨 내 XP만 표시한다.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Lobby Level HUD View")]
    public sealed class UILobbyLevelHudView : MonoBehaviour
    {
        [Header("표시 연결")]
        [SerializeField] private TMP_Text _levelText;
        [SerializeField, Tooltip("연결하면 이 Slider의 Min/Max/Value가 경험치 표시의 기준이 된다.")]
        private Slider _experienceSlider;
        [SerializeField] private Image _experienceFill;
        [SerializeField] private Image _fillCap;

        [Header("계정 시스템 연결 전 레벨 미리보기")]
        [SerializeField, Min(1)] private int _previewLevel = 20;
        [Header("UsePreviewValues 호출 시 복구값 / Slider 미연결 시 대체값")]
        [SerializeField, Min(0f)] private float _previewExperience = 60f;
        [SerializeField, Min(0.001f)] private float _previewRequiredExperience = 100f;

        private bool _hasRuntimeValues;
        private int _level;
        private float _experience;
        private float _requiredExperience;
        private bool _needsRefresh;
        private Slider _subscribedSlider;
        private float _lastProgress = -1f;

        public int DisplayLevel => Mathf.Max(1, _hasRuntimeValues ? _level : _previewLevel);
        public float Progress => _experienceSlider != null
            ? CalculateProgress(_experienceSlider.value - _experienceSlider.minValue,
                _experienceSlider.maxValue - _experienceSlider.minValue)
            : CalculateProgress(_hasRuntimeValues ? _experience : _previewExperience,
                _hasRuntimeValues ? _requiredExperience : _previewRequiredExperience);
        public float RemainingRatio => 1f - Progress;

        /// <summary>experience는 누적 총 XP가 아니라 현재 레벨에서 모은 XP다. 저장/레벨업은 수행하지 않는다.</summary>
        public void SetProgress(int level, float experience, float requiredExperience)
        {
            _hasRuntimeValues = true;
            _level = level;
            _experience = experience;
            _requiredExperience = requiredExperience;
            SetSliderProgress(experience, requiredExperience);
            RefreshVisuals();
        }

        public void UsePreviewValues()
        {
            _hasRuntimeValues = false;
            SetSliderProgress(_previewExperience, _previewRequiredExperience);
            RefreshVisuals();
        }

        private void SetSliderProgress(float experience, float requiredExperience)
        {
            if (_experienceSlider == null) return;
            // 잘못된 입력이 Slider 내부에 NaN/Infinity로 남지 않도록 경계에서 정리한다.
            bool hasValidMaximum = requiredExperience > 0f && !float.IsInfinity(requiredExperience);
            float maximum = hasValidMaximum ? requiredExperience : 1f;
            float value = hasValidMaximum && !float.IsNaN(experience) && !float.IsInfinity(experience)
                ? Mathf.Clamp(experience, 0f, maximum) : 0f;
            _experienceSlider.minValue = 0f;
            _experienceSlider.maxValue = maximum;
            _experienceSlider.SetValueWithoutNotify(value);
        }

        public static float CalculateProgress(float experience, float requiredExperience)
        {
            if (float.IsNaN(experience) || float.IsInfinity(experience) ||
                float.IsNaN(requiredExperience) || float.IsInfinity(requiredExperience) || requiredExperience <= 0f)
                return 0f;
            return Mathf.Clamp01(experience / requiredExperience);
        }

        public void RefreshVisuals()
        {
            _needsRefresh = false;
            SyncSliderSubscription();
            float progress = Progress;
            _lastProgress = progress;
            if (_levelText != null) _levelText.text = "LV. " + DisplayLevel;
            if (_experienceFill != null)
            {
                _experienceFill.type = Image.Type.Filled;
                _experienceFill.fillMethod = Image.FillMethod.Horizontal;
                _experienceFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                // Slider 값을 기준으로 표시한다. Inspector의 지연 갱신에서도 Fill과 끝 장식을 맞춘다.
                // Slider Value 자체를 미리보기 값으로 덮어쓰지는 않는다.
                _experienceFill.fillAmount = progress;
                _experienceFill.raycastTarget = false;
            }
            if (_fillCap != null)
            {
                // Fill과 동일한 영역의 자식이어야 너비 변경 시에도 경계가 일치한다.
                RectTransform capRect = _fillCap.rectTransform;
                Vector2 anchor = new Vector2(progress, 0.5f);
                capRect.anchorMin = anchor;
                capRect.anchorMax = anchor;
                capRect.anchoredPosition = Vector2.zero;
                _fillCap.enabled = progress > 0f && _fillCap.sprite != null;
                _fillCap.raycastTarget = false;
            }
        }

        private void SyncSliderSubscription()
        {
            Slider target = isActiveAndEnabled ? _experienceSlider : null;
            if (_subscribedSlider == target) return;
            if (_subscribedSlider != null) _subscribedSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            _subscribedSlider = target;
            if (_subscribedSlider != null) _subscribedSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        private void OnSliderValueChanged(float value) => RefreshVisuals();

        private void OnEnable()
        {
            SyncSliderSubscription();
            _needsRefresh = true;
        }

        private void OnDisable()
        {
            if (_subscribedSlider != null) _subscribedSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            _subscribedSlider = null;
        }

        // Inspector 로드 중 TMP/RectTransform을 즉시 바꾸지 않는다.
        private void OnValidate() => _needsRefresh = true;

        private void LateUpdate()
        {
            // Inspector 편집 및 SetValueWithoutNotify도 끝 장식에 반영하되 변경 시에만 갱신한다.
            if (_needsRefresh || _subscribedSlider != _experienceSlider ||
                !Mathf.Approximately(_lastProgress, Progress)) RefreshVisuals();
        }
    }
}
