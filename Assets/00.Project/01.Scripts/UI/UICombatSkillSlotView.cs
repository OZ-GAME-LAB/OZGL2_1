using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 전투 슬롯의 표시만 담당한다. 장착 저장, 시전, 쿨다운 시간 진행은 외부 시스템의 책임이다.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Combat Skill Slot View")]
    public sealed class UICombatSkillSlotView : MonoBehaviour
    {
        private static readonly Color SILVER = new Color32(186, 190, 196, 255);

        [Header("공유 분류 스타일 / 표시 연결")]
        [SerializeField] private UISkillCategoryStyleSO _categoryStyle;
        [SerializeField] private Image _icon;
        [SerializeField] private Image _slotTint;
        [SerializeField] private Image _frame;
        [SerializeField] private Sprite _damageFrame;
        [SerializeField] private Sprite _buffFrame;
        [SerializeField] private Sprite _debuffFrame;

        [Header("쿨다운 표시 연결")]
        [SerializeField] private Image _cooldownTrack;
        [SerializeField] private Image _cooldownFill;
        [SerializeField] private TMP_Text _remainingText;
        [SerializeField, Range(0f, 1f)] private float _cooldownTrackOpacity = 0.22f;
        [SerializeField, Range(0f, 1f)] private float _cooldownFillOpacity = 0.85f;
        [SerializeField] private Color _cooldownFillColor = new Color32(248, 242, 235, 255);

        [Header("실제 전투와 연결되지 않은 미리보기 값")]
        [SerializeField] private UISkillPreviewCatalogSO _previewCatalog;
        [SerializeField] private string _previewEntryId = string.Empty;
        [SerializeField, Min(0f)] private float _previewRemainingSeconds;
        [SerializeField, Min(0f)] private float _previewDuration = 1f;

        private UISkillPreviewCatalogSO.Entry _runtimeEntry;
        private bool _hasRuntimeEntry;
        private bool _hasRuntimeCooldown;
        private float _runtimeRemainingSeconds;
        private float _runtimeDuration;
        private Image _originalFrame;
        private Sprite _defaultFrameSprite;
        private Material _defaultFrameMaterial;
        private volatile bool _needsRefresh;

        public void ShowSkill(UISkillPreviewCatalogSO.Entry entry)
        {
            // 이전 스킬의 쿨다운이 새 스킬에 남지 않도록 표시 상태만 초기화한다.
            if (!_hasRuntimeEntry || !ReferenceEquals(_runtimeEntry, entry))
            {
                _runtimeRemainingSeconds = 0f;
                _runtimeDuration = 0f;
                _hasRuntimeCooldown = true;
            }

            _runtimeEntry = entry;
            _hasRuntimeEntry = true;
            RefreshView();
        }

        // 남은 시간 비율이다. 1은 쿨다운 전체, 0은 사용 가능 상태를 표시한다.
        public void SetCooldown(float remainingSeconds, float duration)
        {
            _runtimeRemainingSeconds = NonNegativeFinite(remainingSeconds);
            _runtimeDuration = NonNegativeFinite(duration);
            _hasRuntimeCooldown = true;
            RefreshView();
        }

        public void UsePreviewValues()
        {
            _runtimeEntry = null;
            _hasRuntimeEntry = false;
            _hasRuntimeCooldown = false;
            RefreshView();
        }

        private void OnEnable()
        {
            Canvas.preWillRenderCanvases -= RefreshBeforeRender;
            Canvas.preWillRenderCanvases += RefreshBeforeRender;
            RefreshView();
        }

        private void OnDisable() => Canvas.preWillRenderCanvases -= RefreshBeforeRender;

        // Inspector 로드 중에는 Unity 오브젝트를 수정하지 않고 메인 스레드의 렌더 직전으로 미룬다.
        private void OnValidate() => _needsRefresh = true;

        private void RefreshBeforeRender()
        {
            if (_needsRefresh && this != null && isActiveAndEnabled) RefreshView();
        }

        private void RefreshView()
        {
            _needsRefresh = false;
            UISkillPreviewCatalogSO.Entry entry = _hasRuntimeEntry ? _runtimeEntry : FindPreviewEntry();
            bool hasSkill = entry != null && !string.IsNullOrWhiteSpace(entry.Id);
            Color categoryColor = SILVER;
            Material iconMaterial = null;
            if (hasSkill && _categoryStyle != null)
            {
                iconMaterial = _categoryStyle.GetIconMaterial(entry.Category);
                if (iconMaterial != null) categoryColor = _categoryStyle.GetSlotColor(entry.Category);
            }

            if (_icon != null)
            {
                _icon.raycastTarget = false;
                _icon.sprite = hasSkill ? entry.Icon : null;
                _icon.enabled = _icon.sprite != null;
                _icon.color = Color.white;
                _icon.material = iconMaterial;
            }

            if (_slotTint != null)
            {
                _slotTint.raycastTarget = false;
                _slotTint.material = _categoryStyle != null ? _categoryStyle.SlotTintMaterial : null;
                _slotTint.color = hasSkill && _categoryStyle != null
                    ? _categoryStyle.GetSlotColor(entry.Category) : Color.clear;
                _slotTint.enabled = hasSkill && _slotTint.color.a > 0f;
            }

            RefreshFrame(hasSkill ? entry : null);
            float remainingSeconds = _hasRuntimeCooldown ? _runtimeRemainingSeconds : _previewRemainingSeconds;
            float duration = _hasRuntimeCooldown ? _runtimeDuration : _previewDuration;
            RefreshCooldown(hasSkill, categoryColor, remainingSeconds, duration);
        }

        private void RefreshFrame(UISkillPreviewCatalogSO.Entry entry)
        {
            if (_frame == null) return;
            if (_originalFrame != _frame)
            {
                // Builder가 연결한 은색 기본 프레임을 빈 슬롯과 미설정 분류용으로 보관한다.
                _originalFrame = _frame;
                _defaultFrameSprite = _frame.sprite;
                _defaultFrameMaterial = _frame.material;
            }

            Sprite categoryFrame = entry != null ? GetFrame(entry.Category) : null;
            _frame.sprite = categoryFrame != null ? categoryFrame : _defaultFrameSprite;
            _frame.material = categoryFrame != null ? null :
                _categoryStyle != null && _categoryStyle.EmptyFrameMaterial != null
                    ? _categoryStyle.EmptyFrameMaterial : _defaultFrameMaterial;
            _frame.color = Color.white;
            _frame.enabled = _frame.sprite != null;
            _frame.raycastTarget = false;
        }

        private void RefreshCooldown(bool hasSkill, Color categoryColor, float remainingSeconds, float duration)
        {
            float safeDuration = NonNegativeFinite(duration);
            float safeRemaining = hasSkill && safeDuration > 0f ? NonNegativeFinite(remainingSeconds) : 0f;
            // double로 먼저 나눠 유효한 극단값 사이에서도 float 나눗셈의 오버플로를 피한다.
            float ratio = safeDuration > 0f
                ? Mathf.Clamp01((float)Math.Min(1d, (double)safeRemaining / safeDuration)) : 0f;

            if (_cooldownTrack != null)
            {
                _cooldownTrack.raycastTarget = false;
                _cooldownTrack.color = WithOpacity(categoryColor, _cooldownTrackOpacity);
                _cooldownTrack.enabled = hasSkill && _cooldownTrack.sprite != null;
            }

            if (_cooldownFill != null)
            {
                _cooldownFill.raycastTarget = false;
                _cooldownFill.type = Image.Type.Filled;
                _cooldownFill.fillMethod = Image.FillMethod.Radial360;
                _cooldownFill.fillOrigin = (int)Image.Origin360.Top;
                _cooldownFill.fillClockwise = true;
                _cooldownFill.fillAmount = ratio;
                _cooldownFill.color = WithOpacity(_cooldownFillColor, _cooldownFillOpacity);
                _cooldownFill.enabled = hasSkill && ratio > 0f && _cooldownFill.sprite != null;
            }

            if (_remainingText != null)
            {
                _remainingText.raycastTarget = false;
                _remainingText.text = safeRemaining > 0f
                    ? Math.Ceiling((double)safeRemaining).ToString("0", CultureInfo.InvariantCulture) : string.Empty;
                _remainingText.enabled = safeRemaining > 0f;
            }
        }

        private UISkillPreviewCatalogSO.Entry FindPreviewEntry()
        {
            if (_previewCatalog == null || _previewCatalog.Entries == null ||
                string.IsNullOrWhiteSpace(_previewEntryId)) return null;

            foreach (UISkillPreviewCatalogSO.Entry entry in _previewCatalog.Entries)
                if (entry != null && string.Equals(entry.Id, _previewEntryId, StringComparison.Ordinal)) return entry;
            return null;
        }

        private Sprite GetFrame(eSkillPreviewCategory category)
        {
            switch (category)
            {
                case eSkillPreviewCategory.DAMAGE: return _damageFrame;
                case eSkillPreviewCategory.BUFF: return _buffFrame;
                case eSkillPreviewCategory.DEBUFF: return _debuffFrame;
                default: return null;
            }
        }

        private static Color WithOpacity(Color color, float opacity)
        {
            color.a = Mathf.Clamp01(NonNegativeFinite(opacity));
            return color;
        }

        private static float NonNegativeFinite(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }
}
