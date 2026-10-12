using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// 중앙 난이도 카드의 호버 기록 표시와 구형 명패 확장을 담당한다.
    /// 기록 집계/저장은 외부 책임이며 Catalog SO에 런타임 기록을 쓰지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UI/Lobby Difficulty Record Panel")]
    public sealed class UILobbyDifficultyRecordPanel : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, ICanvasRaycastFilter
    {
        private const float POINTER_RECHECK_INTERVAL = 0.08f;

        [Header("전환 연결 / 별도 HitArea에 부착")]
        [SerializeField] private UILobbyDifficultySelector _selector;

        [Header("카드 내부 기록 표시")]
        [SerializeField] private bool _useInlineDisplay;
        [SerializeField] private CanvasGroup _descriptionGroup;
        [SerializeField] private CanvasGroup _shadeGroup;
        [SerializeField] private UILobbyStageCardView _stageCard;

        [Header("하단 기준 확장 레이아웃")]
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private RectTransform _revealRect;
        [SerializeField] private CanvasGroup _recordGroup;
        [SerializeField] private TMP_Text _waveValueLabel;
        [SerializeField] private TMP_Text _timeValueLabel;
        [SerializeField, Min(1f)] private float _collapsedHeight = 120f;
        [SerializeField, Min(1f)] private float _expandedHeight = 220f;

        [Header("호버 애니메이션 (시간 배율 무관)")]
        [SerializeField, Min(0f)] private float _enterDelay = 0.08f;
        [SerializeField, Min(0f)] private float _exitDelay = 0.10f;
        [SerializeField, Min(0.01f)] private float _expandDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float _collapseDuration = 0.16f;
        [SerializeField] private Ease _expandEase = Ease.OutCubic;
        [SerializeField] private Ease _collapseEase = Ease.OutQuad;
        [SerializeField, Range(0f, 0.95f)] private float _recordFadeStart = 0.25f;

        private struct RecordSnapshot
        {
            public readonly int HighestClearedWave;
            public readonly float? ClearTimeSeconds;

            public RecordSnapshot(int highestClearedWave, float? clearTimeSeconds)
            {
                HighestClearedWave = highestClearedWave;
                ClearTimeSeconds = clearTimeSeconds;
            }
        }

        private readonly Dictionary<eLobbyDifficulty, RecordSnapshot> _records =
            new Dictionary<eLobbyDifficulty, RecordSnapshot>();
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
        private readonly List<CanvasGroup> _parentCanvasGroups = new List<CanvasGroup>();
        private RectTransform _hitArea;
        private Canvas _canvas;
        private UILobbyDifficultySelector _subscribedSelector;
        private UILobbyStageCardView _subscribedStageCard;
        private EventSystem _pointerEventSystem;
        private PointerEventData _raycastPointer;
        private Tween _motion;
        private bool _motionExpands;
        private float _expansion;
        private float _pointerDelayRemaining;
        private float _pointerRecheckRemaining;
        private bool _hasPendingPointerRequest;
        private bool _pendingExpanded;
        private bool _needsPointerRefresh;
        private bool _isTransitionLocked;
        private bool _hasEntry;
        private bool _hasFocus = true;
        private eLobbyDifficulty _displayedDifficulty;

        public float Expansion => _expansion;
        public bool UsesInlineDisplay => _useInlineDisplay;
        private bool _externalOpen;

        /// <summary>마우스가 명패 위에 없어도 펼쳐 둔다(난이도 카드를 눌렀을 때). false 로 하면 평소의 호버 동작으로 돌아간다. 난이도를 넘기는 중에는 자동으로 풀린다.</summary>
        public void SetExternalOpen(bool open)
        {
            // 카드 내부 모드는 하나의 HitArea와 실제 최상단 UI 검사만 사용한다.
            if (_useInlineDisplay)
            {
                _externalOpen = false;
                return;
            }
            _externalOpen = open;
            if (CanExpand()) SchedulePointerRequest(open || IsPointerOverPanel());
        }

        public bool IsExpanded => _expansion >= 0.999f;
        public bool IsTransitionLocked => _isTransitionLocked;
        public bool HasEntry => _hasEntry;
        public eLobbyDifficulty DisplayedDifficulty => _displayedDifficulty;
        public bool HasRecord => _hasEntry && _records.ContainsKey(_displayedDifficulty);

        public bool TryValidate(out string reason)
        {
            if (_selector == null || _recordGroup == null
                || _waveValueLabel == null || _timeValueLabel == null)
            {
                reason = "Selector, 기록 CanvasGroup과 값 TMP 참조를 확인하세요.";
                return false;
            }
            if (_useInlineDisplay)
            {
                if (_descriptionGroup == null || _shadeGroup == null)
                {
                    reason = "카드 내부 표시의 Description과 Shade CanvasGroup 참조를 확인하세요.";
                    return false;
                }
            }
            else if (_panelRect == null || _revealRect == null)
            {
                reason = "구형 확장 표시의 Panel과 Reveal 참조를 확인하세요.";
                return false;
            }
            else if (_expandedHeight <= _collapsedHeight)
            {
                reason = "확장 높이는 축소 높이보다 커야 합니다.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary>슬롯 표시가 교체되는 순간 호출한다. SelectionChanged보다 앞선 투명 시점이다.</summary>
        public void Bind(LobbyDifficultyCatalogSO.Entry entry)
        {
            _hasEntry = entry != null;
            if (_hasEntry) _displayedDifficulty = entry.Difficulty;
            RefreshRecordLabels();
            if (_useInlineDisplay || !_hasEntry)
            {
                // 난이도가 투명한 바인딩 시점에 기록을 접어 이전 표시 진행률이 이어지지 않게 한다.
                if (_useInlineDisplay) _externalOpen = false;
                CancelMotion();
                _hasPendingPointerRequest = false;
                _pointerRecheckRemaining = 0f;
                SetExpansion(0f);
            }
            _needsPointerRefresh = true;
        }

        /// <summary>
        /// 외부 기록 제공자가 난이도별 표시 스냅샷을 주입한다. 시간 미확정 시 null을 넣는다.
        /// 최고 웨이브와 시간의 집계 기준/동일 플레이 여부는 호출자가 책임진다.
        /// </summary>
        public bool SetRecord(eLobbyDifficulty difficulty, int highestClearedWave, float? clearTimeSeconds = null)
        {
            if (!Enum.IsDefined(typeof(eLobbyDifficulty), difficulty) || highestClearedWave < 0
                || (clearTimeSeconds.HasValue && (float.IsNaN(clearTimeSeconds.Value)
                    || float.IsInfinity(clearTimeSeconds.Value) || clearTimeSeconds.Value < 0f))) return false;
            _records[difficulty] = new RecordSnapshot(highestClearedWave, clearTimeSeconds);
            if (_hasEntry && _displayedDifficulty == difficulty) RefreshRecordLabels();
            return true;
        }

        public void ClearRecord(eLobbyDifficulty difficulty)
        {
            _records.Remove(difficulty);
            if (_hasEntry && _displayedDifficulty == difficulty) RefreshRecordLabels();
        }

        public void ClearAllRecords()
        {
            _records.Clear();
            RefreshRecordLabels();
        }

        [ContextMenu("Refresh Record Panel Layout")]
        public void RefreshLayout()
        {
            CacheLayoutReferences();
            SetExpansion(Application.isPlaying ? _expansion : 0f);
            RefreshRecordLabels();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CanExpand()) SchedulePointerRequest(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Application.isPlaying && isActiveAndEnabled && !_isTransitionLocked)
                SchedulePointerRequest(false);
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            // 표시용 captionGroup 밖에 둔 HitArea만 입력을 받는다. 전환 중에는 아래 UI도 차단하지 않는다.
            return Application.isPlaying && isActiveAndEnabled && _hasEntry && !_isTransitionLocked
                && !IsInlineCardLocked() && IsInlineInputAllowed();
        }

        private bool CanExpand()
        {
            return Application.isPlaying && isActiveAndEnabled && _hasFocus && _hasEntry && !_isTransitionLocked
                && !IsInlineCardLocked() && IsInlineInputAllowed();
        }

        private bool IsInlineCardLocked() => _useInlineDisplay && _stageCard != null && _stageCard.IsLocked;

        private bool IsInlineInputAllowed()
        {
            if (!_useInlineDisplay) return true;

            Transform ignoreParentsAbove = null;
            foreach (CanvasGroup group in _parentCanvasGroups)
            {
                if (group == null || !group.isActiveAndEnabled) continue;
                // 같은 오브젝트의 그룹은 모두 검사하되, ignoreParentGroups 위쪽 조상은 무시한다.
                if (ignoreParentsAbove != null && group.transform != ignoreParentsAbove) break;
                if (!group.interactable || !group.blocksRaycasts || group.alpha <= 0.001f) return false;
                if (group.ignoreParentGroups) ignoreParentsAbove = group.transform;
            }
            return true;
        }

        private void RefreshInlineInputState()
        {
            if (!_useInlineDisplay) return;
            _needsPointerRefresh = IsInlineInputAllowed();
            if (_needsPointerRefresh) return;
            // 팝업이 부모의 interactable만 내려도 설명을 복구하고 남은 호버 동작을 정리한다.
            _externalOpen = false;
            _hasPendingPointerRequest = false;
            _pointerRecheckRemaining = 0f;
            CancelMotion();
            SetExpansion(0f);
        }

        private void SchedulePointerRequest(bool expand)
        {
            if (_hasPendingPointerRequest && _pendingExpanded == expand) return;
            // 접히는 중 재진입하면 현재 높이를 보존한다. 진입 유예 중 판이 포인터 아래에서 사라지지 않게 한다.
            if (expand && _motion != null && !_motionExpands) CancelMotion();
            _pendingExpanded = expand;
            _pointerDelayRemaining = Mathf.Max(0f, expand ? _enterDelay : _exitDelay);
            _hasPendingPointerRequest = true;
        }

        private void RecheckPointer()
        {
            _needsPointerRefresh = false;
            _pointerRecheckRemaining = POINTER_RECHECK_INTERVAL;
            if (!CanExpand()) return;
            SchedulePointerRequest(IsPointerOverPanel());
        }

        private void RecheckActivePointerPeriodically()
        {
            // 완전히 접힌 대기 상태는 EventSystem의 진입 이벤트만 사용한다.
            if (_expansion <= 0f && !_hasPendingPointerRequest)
            {
                _pointerRecheckRemaining = 0f;
                return;
            }
            _pointerRecheckRemaining -= Time.unscaledDeltaTime;
            if (_pointerRecheckRemaining > 0f) return;
            _pointerRecheckRemaining = POINTER_RECHECK_INTERVAL;

            // 포인터 이벤트가 누락되거나 정지한 포인터 위에 다른 UI가 생겨도 접힘을 놓치지 않는다.
            bool shouldExpand = CanExpand() && IsPointerOverPanel();
            if (_hasPendingPointerRequest)
            {
                if (_pendingExpanded != shouldExpand) SchedulePointerRequest(shouldExpand);
                return;
            }
            bool isMovingToTarget = _motion != null && _motionExpands == shouldExpand;
            bool isAtTarget = shouldExpand ? _expansion >= 0.999f : _expansion <= 0.001f;
            // 같은 방향의 유예/Tween은 유지해 반복 검사로 애니메이션을 재시작하지 않는다.
            if (!isMovingToTarget && !isAtTarget) SchedulePointerRequest(shouldExpand);
        }

        private bool IsPointerOverPanel()
        {
            if (_externalOpen && !_useInlineDisplay) return true;
            Mouse mouse = Mouse.current;
            EventSystem eventSystem = EventSystem.current;
            if (mouse == null || eventSystem == null || _hitArea == null) return false;
            Vector2 position = mouse.position.ReadValue();
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_hitArea, position, eventCamera)) return false;

            if (_raycastPointer == null || _pointerEventSystem != eventSystem)
            {
                _pointerEventSystem = eventSystem;
                _raycastPointer = new PointerEventData(eventSystem);
            }
            _raycastPointer.position = position;
            _raycastResults.Clear();
            eventSystem.RaycastAll(_raycastPointer, _raycastResults);
            if (_raycastResults.Count == 0 || _raycastResults[0].gameObject == null) return false;
            Transform hit = _raycastResults[0].gameObject.transform;
            return hit == transform || hit.IsChildOf(transform);
        }

        private void AnimateTo(bool expand)
        {
            CancelMotion();
            _motionExpands = expand;
            float target = expand ? 1f : 0f;
            float distance = Mathf.Abs(target - _expansion);
            if (distance < 0.001f)
            {
                SetExpansion(target);
                return;
            }
            float fullDuration = expand ? _expandDuration : _collapseDuration;
            Tween motion = DOTween.To(() => _expansion, SetExpansion, target,
                Mathf.Max(0.01f, fullDuration * distance))
                .SetEase(expand ? _expandEase : _collapseEase).SetUpdate(true);
            _motion = motion;
            motion.OnComplete(() =>
            {
                if (_motion == motion) _motion = null;
            });
            motion.OnKill(() =>
            {
                if (_motion == motion) _motion = null;
            });
        }

        private void SetExpansion(float expansion)
        {
            _expansion = Mathf.Clamp01(expansion);
            if (_useInlineDisplay)
            {
                // 봉인 조건을 기록 호버가 덮어쓰지 않도록 잠금 전환 시에도 같은 표시 규칙을 적용한다.
                bool isLocked = IsInlineCardLocked();
                if (isLocked) _expansion = 0f;
                // 상위 Caption의 난이도 전환 alpha는 유지하고 표시 전용 자식만 교차 전환한다.
                if (_descriptionGroup != null) _descriptionGroup.alpha = isLocked ? 0f : 1f - _expansion;
                if (_shadeGroup != null) _shadeGroup.alpha = _expansion;
                if (_recordGroup != null) _recordGroup.alpha = _expansion;
                return;
            }
            float collapsed = Mathf.Max(1f, _collapsedHeight);
            float expanded = Mathf.Max(collapsed, _expandedHeight);
            float height = Mathf.Lerp(collapsed, expanded, _expansion);
            if (_panelRect != null) _panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (_hitArea != null) _hitArea.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (_revealRect != null)
                _revealRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, height - collapsed));
            if (_recordGroup != null)
                _recordGroup.alpha = Mathf.InverseLerp(Mathf.Clamp(_recordFadeStart, 0f, 0.95f), 1f, _expansion);
        }

        private void RefreshRecordLabels()
        {
            RecordSnapshot record = default;
            bool hasRecord = _hasEntry && _records.TryGetValue(_displayedDifficulty, out record);
            if (_waveValueLabel != null)
                _waveValueLabel.text = hasRecord ? record.HighestClearedWave + " 웨이브" : "기록 없음";
            if (_timeValueLabel != null)
                _timeValueLabel.text = hasRecord && record.ClearTimeSeconds.HasValue
                    ? FormatTime(record.ClearTimeSeconds.Value) : "--:--";
        }

        private static string FormatTime(float seconds)
        {
            // 60분을 넘어도 시간 값이 한 바퀴 돌아가지 않도록 총 분을 표시한다.
            double roundedSeconds = Math.Floor(seconds);
            double totalMinutes = Math.Floor(roundedSeconds / 60d);
            double remainingSeconds = roundedSeconds % 60d;
            return totalMinutes.ToString("00") + ":" + remainingSeconds.ToString("00");
        }

        private void HandleTransitionStateChanged(bool isTransitioning)
        {
            if (_isTransitionLocked == isTransitioning) return;
            _isTransitionLocked = isTransitioning;
            _hasPendingPointerRequest = false;
            _pointerRecheckRemaining = 0f;
            if (isTransitioning) { _externalOpen = false; AnimateTo(false); }
            else _needsPointerRefresh = true;
        }

        private void HandleLockStateChanged(bool isLocked)
        {
            if (!_useInlineDisplay) return;
            _externalOpen = false;
            _hasPendingPointerRequest = false;
            _pointerRecheckRemaining = 0f;
            CancelMotion();
            SetExpansion(0f);
            _needsPointerRefresh = !isLocked;
        }

        private void CacheLayoutReferences()
        {
            if (_hitArea == null) _hitArea = transform as RectTransform;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            GetComponentsInParent<CanvasGroup>(true, _parentCanvasGroups);
        }

        private void OnCanvasGroupChanged()
        {
            GetComponentsInParent<CanvasGroup>(true, _parentCanvasGroups);
            RefreshInlineInputState();
        }

        private void OnTransformParentChanged()
        {
            _canvas = null;
            CacheLayoutReferences();
            RefreshInlineInputState();
        }

        private void Subscribe()
        {
            Unsubscribe();
            _subscribedSelector = _selector;
            if (_subscribedSelector != null)
                _subscribedSelector.TransitionStateChanged += HandleTransitionStateChanged;
            _subscribedStageCard = _stageCard;
            if (_subscribedStageCard != null)
                _subscribedStageCard.LockStateChanged += HandleLockStateChanged;
        }

        private void Unsubscribe()
        {
            if (_subscribedSelector != null)
                _subscribedSelector.TransitionStateChanged -= HandleTransitionStateChanged;
            _subscribedSelector = null;
            if (_subscribedStageCard != null)
                _subscribedStageCard.LockStateChanged -= HandleLockStateChanged;
            _subscribedStageCard = null;
        }

        private void CancelMotion()
        {
            Tween motion = _motion;
            _motion = null;
            if (motion != null && motion.IsActive()) motion.Kill(false);
        }

        private void OnEnable()
        {
            CacheLayoutReferences();
            Subscribe();
            if (_useInlineDisplay) _externalOpen = false;
            _hasFocus = true;
            _isTransitionLocked = _selector != null && _selector.IsTransitioning;
            _hasPendingPointerRequest = false;
            _pointerRecheckRemaining = 0f;
            _needsPointerRefresh = true;
            SetExpansion(0f);
            RefreshRecordLabels();
        }

        private void OnDisable()
        {
            Unsubscribe();
            CancelMotion();
            if (_useInlineDisplay) _externalOpen = false;
            _hasPendingPointerRequest = false;
            _pointerRecheckRemaining = 0f;
            _needsPointerRefresh = false;
            SetExpansion(0f);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            CancelMotion();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            _pointerRecheckRemaining = 0f;
            if (hasFocus) _needsPointerRefresh = true;
            else
            {
                if (_useInlineDisplay) _externalOpen = false;
                _hasPendingPointerRequest = false;
                CancelMotion();
                SetExpansion(0f);
            }
        }

        private void LateUpdate()
        {
            if (_needsPointerRefresh) RecheckPointer();
            RecheckActivePointerPeriodically();
            if (!_hasPendingPointerRequest) return;
            _pointerDelayRemaining -= Time.unscaledDeltaTime;
            if (_pointerDelayRemaining > 0f) return;
            _hasPendingPointerRequest = false;
            // 지연 중 확장된 HitArea나 다른 팝업 때문에 포인터 상태가 달라졌는지 다시 확인한다.
            AnimateTo(CanExpand() && IsPointerOverPanel());
        }
    }
}
