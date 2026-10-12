using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OZGL2.UIFlow
{
    // 결과의 표시 연출만 담당한다. 경험치 지급, 저장, Scene 이동은 View/코어에 맡긴다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIBattleResultView))]
    [AddComponentMenu("UI/Battle Result Presentation")]
    public sealed class UIBattleResultPresentation : MonoBehaviour
    {
        [Header("등장 대상")]
        [SerializeField] private CanvasGroup _titleGroup;
        [SerializeField] private CanvasGroup _timeRow;
        [SerializeField] private CanvasGroup _killsRow;
        [SerializeField] private CanvasGroup _deploymentsRow;
        [SerializeField] private CanvasGroup _rewardRow;
        [SerializeField] private CanvasGroup _lobbyGroup;
        [SerializeField] private RectTransform _rewardFill;

        [Header("제목 / 기록 / 획득량")]
        [SerializeField, Min(1f)] private float _titleStartScale = 1.2f;
        [SerializeField, Min(0.01f)] private float _titleDuration = 0.3f;
        [SerializeField, Min(0.01f)] private float _rowInterval = 0.2f;
        [SerializeField, Min(0f)] private float _rewardPause = 0.5f;
        [SerializeField, Min(0.01f)] private float _countDuration = 0.8f;

        [Header("경험치 / 로비 버튼")]
        [SerializeField, Min(0f)] private float _fillStartDelay = 0.2f;
        [SerializeField, Min(0.01f)] private float _fillDuration = 0.65f;
        [SerializeField, Min(0f)] private float _fillFollowDelay = 0.2f;
        [SerializeField, Min(0f)] private float _levelUpPause = 0.08f;
        [SerializeField, Min(0.01f)] private float _buttonDuration = 0.2f;

        private enum ePresentationState { IDLE, PLAYING, REVEALING_BUTTON, WAITING_FOR_RELEASE, COMPLETE }

        private UIBattleResultView _view;
        private BattleResultDisplayData _data;
        private BattleResultDisplayData _lastPresentedData;
        private string _lastRunId;
        private Sequence _sequence;
        private ePresentationState _state;
        private Vector3 _titleBaseScale;
        private bool _hasBaseScale;
        private bool _isSkipArmed;
        private int _startedFrame;
        private int _buttonShownFrame;

        public bool IsPlaying => _state == ePresentationState.PLAYING ||
            _state == ePresentationState.REVEALING_BUTTON || _state == ePresentationState.WAITING_FOR_RELEASE;
        public bool CanReturnToLobby => _state == ePresentationState.IDLE || _state == ePresentationState.COMPLETE;

        internal void Bind(UIBattleResultView view)
        {
            _view = view;
            if (!_hasBaseScale && _view != null && _view.TitleTransform != null)
            {
                _titleBaseScale = _view.TitleTransform.localScale;
                _hasBaseScale = true;
            }
        }

        private void Awake()
        {
            if (TryGetComponent(out UIBattleResultView view)) Bind(view);
        }

        public void Play(BattleResultDisplayData data)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || data == null) return;
            if (_view == null && TryGetComponent(out UIBattleResultView view)) Bind(view);
            // 동일 결과를 다시 알리거나 팝업을 재활성화해도 보상 연출을 중복 재생하지 않는다.
            if (_lastPresentedData == data || (!string.IsNullOrEmpty(data.RunId) && _lastRunId == data.RunId)) return;
            ResetToFinal(data);
            if (!HasRequiredReferences())
            {
                Debug.LogWarning("결과 연출의 View, CanvasGroup, 노란 Fill 참조를 확인하세요. 최종 결과를 표시합니다.", this);
                return;
            }

            _lastPresentedData = data;
            _lastRunId = data.RunId;
            _state = ePresentationState.PLAYING;
            _startedFrame = Time.frameCount;
            _isSkipArmed = !IsPointerHeld();
            SetAllGroups(0f);
            SetAlpha(_lobbyGroup, 0f);
            SetButtonAvailable(false);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _view.LobbyButton.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            _view.TitleTransform.localScale = _titleBaseScale * Mathf.Max(1f, _titleStartScale);
            _view.SetAnimatedReward(0);
            _view.SetAnimatedExperience(data.StartLevel, data.StartNormalizedXp, false);
            SetRewardFill(data.StartNormalizedXp);

            Sequence sequence = DOTween.Sequence();
            _sequence = sequence;
            sequence.SetUpdate(true);
            sequence.Append(DOTween.To(() => 0f, UpdateTitle, 1f, Duration(_titleDuration)).SetEase(Ease.OutCubic));
            AppendRow(sequence, _timeRow);
            AppendRow(sequence, _killsRow);
            AppendRow(sequence, _deploymentsRow);
            sequence.AppendInterval(Mathf.Max(0f, _rewardPause));
            sequence.AppendCallback(() => SetAlpha(_rewardRow, 1f));
            sequence.Append(DOTween.To(() => 0f, progress =>
                _view.SetAnimatedReward((int)Math.Round((double)_data.EarnedXp * progress)),
                1f, Duration(_countDuration)).SetEase(Ease.OutQuad));
            sequence.AppendCallback(() => _view.SetAnimatedReward(_data.EarnedXp));
            sequence.AppendInterval(Mathf.Max(0f, _fillStartDelay));
            sequence.Append(DOTween.To(() => 0f, UpdateExperience, 1f,
                (float)(ExperienceSegmentCount() * ExperienceSegmentDuration())).SetEase(Ease.Linear));
            sequence.OnComplete(() =>
            {
                if (_sequence != sequence) return;
                _sequence = null;
                ApplyFinalValues();
                RevealLobbyButton();
            });
        }

        public void Skip()
        {
            if (_state != ePresentationState.PLAYING || !CanReceiveInput()) return;
            StopSequence();
            ApplyFinalValues();
            RevealLobbyButton();
        }

        // 데이터 변경/비활성화/미리보기는 연출 콜백을 실행하지 않고 즉시 표시를 복구한다.
        public void ResetToFinal(BattleResultDisplayData data)
        {
            StopSequence();
            _data = data;
            _state = ePresentationState.IDLE;
            ApplyFinalValues();
            SetAlpha(_lobbyGroup, 1f);
            SetButtonAvailable(true);
        }

        private void Update()
        {
            if (!IsPlaying || !CanReceiveInput()) return;
            if (_state == ePresentationState.WAITING_FOR_RELEASE)
            {
                if (Time.frameCount <= _buttonShownFrame || IsPointerHeld()) return;
                _state = ePresentationState.COMPLETE;
                SetButtonAvailable(true);
                return;
            }
            if (_state != ePresentationState.PLAYING || Time.frameCount <= _startedFrame) return;
            if (!_isSkipArmed)
            {
                if (!IsPointerHeld()) _isSkipArmed = true;
                return;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Skip();
        }

        private bool CanReceiveInput()
        {
            if (_view == null) return false;
            UIPopupPanel popup = _view.Popup;
            return popup == null || popup.Controller == null || popup.Controller.IsTopPopup(popup);
        }

        private static bool IsPointerHeld() =>
            Mouse.current != null && Mouse.current.leftButton.isPressed;

        private void UpdateTitle(float progress)
        {
            SetAlpha(_titleGroup, progress);
            if (_view != null && _view.TitleTransform != null)
                _view.TitleTransform.localScale = _titleBaseScale * Mathf.Lerp(Mathf.Max(1f, _titleStartScale), 1f, progress);
        }

        private void AppendRow(Sequence sequence, CanvasGroup row)
        {
            sequence.Append(DOTween.To(() => 0f, alpha => SetAlpha(row, alpha), 1f,
                Duration(_rowInterval)).SetEase(Ease.OutQuad));
        }

        private void UpdateExperience(float progress)
        {
            if (_data == null || _view == null) return;
            if (progress >= 1f)
            {
                _view.SetAnimatedExperience(_data.Level, _data.NormalizedXp, _data.DidLevelUp);
                SetRewardFill(_data.NormalizedXp);
                return;
            }

            // 매 레벨에서 같은 추격 지연을 유지하고, 두 막대가 찬 모습을 잠시 보여준 뒤 함께 초기화한다.
            long levelSteps = (long)_data.Level - _data.StartLevel;
            long segmentCount = ExperienceSegmentCount();
            double position = Mathf.Clamp01(progress) * (double)segmentCount;
            long segment = Math.Min(segmentCount - 1, (long)position);
            float local = (float)(position - segment);
            float from = segment == 0 ? _data.StartNormalizedXp : 0f;
            float to = segment < levelSteps ? 1f : _data.NormalizedXp;
            float fillDuration = Duration(_fillDuration);
            float delay = Mathf.Max(0f, _fillFollowDelay);
            float elapsed = local * ExperienceSegmentDuration();
            float yellow = EaseOut(Mathf.Clamp01(elapsed / fillDuration));
            float red = EaseOut(Mathf.Clamp01((elapsed - delay) / fillDuration));
            int level = (int)(_data.StartLevel + segment);
            SetRewardFill(Mathf.Lerp(from, to, yellow));
            _view.SetAnimatedExperience(level, Mathf.Lerp(from, to, red), level > _data.StartLevel);
        }

        private long ExperienceSegmentCount()
        {
            long levelSteps = (long)_data.Level - _data.StartLevel;
            return levelSteps + (levelSteps == 0 || _data.NormalizedXp > 0f ? 1L : 0L);
        }

        private float ExperienceSegmentDuration() => Duration(_fillDuration) +
            Mathf.Max(0f, _fillFollowDelay) + Mathf.Max(0f, _levelUpPause);

        private void RevealLobbyButton()
        {
            _state = ePresentationState.REVEALING_BUTTON;
            SetButtonAvailable(false);
            SetAlpha(_lobbyGroup, 0f);
            Sequence sequence = DOTween.Sequence();
            _sequence = sequence;
            sequence.SetUpdate(true);
            sequence.Append(DOTween.To(() => 0f, alpha => SetAlpha(_lobbyGroup, alpha),
                1f, Duration(_buttonDuration)).SetEase(Ease.OutQuad));
            sequence.OnComplete(() =>
            {
                if (_sequence != sequence) return;
                _sequence = null;
                SetAlpha(_lobbyGroup, 1f);
                _buttonShownFrame = Time.frameCount;
                _state = ePresentationState.WAITING_FOR_RELEASE;
            });
        }

        private void ApplyFinalValues()
        {
            SetAllGroups(1f);
            if (_view == null) return;
            if (_hasBaseScale && _view.TitleTransform != null) _view.TitleTransform.localScale = _titleBaseScale;
            _view.SetAnimatedReward(_data != null ? _data.EarnedXp : 0);
            _view.SetAnimatedExperience(_data != null ? _data.Level : 1,
                _data != null ? _data.NormalizedXp : 0f, _data != null && _data.DidLevelUp);
            SetRewardFill(_data != null ? _data.NormalizedXp : 0f);
        }

        private void SetAllGroups(float alpha)
        {
            SetAlpha(_titleGroup, alpha);
            SetAlpha(_timeRow, alpha);
            SetAlpha(_killsRow, alpha);
            SetAlpha(_deploymentsRow, alpha);
            SetAlpha(_rewardRow, alpha);
        }

        private void SetRewardFill(float value)
        {
            if (_rewardFill == null) return;
            Vector2 anchor = _rewardFill.anchorMax;
            anchor.x = Mathf.Clamp01(value);
            _rewardFill.anchorMax = anchor;
        }

        private void SetButtonAvailable(bool available)
        {
            if (_lobbyGroup != null)
            {
                _lobbyGroup.interactable = available;
                _lobbyGroup.blocksRaycasts = available;
            }
            if (_view != null && _view.LobbyButton != null) _view.LobbyButton.interactable = available;
        }

        private bool HasRequiredReferences() => _view != null && _view.HasPresentationTargets &&
            _titleGroup != null && _timeRow != null && _killsRow != null && _deploymentsRow != null &&
            _rewardRow != null && _lobbyGroup != null && _rewardFill != null;

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }

        private static float Duration(float value) => Mathf.Max(0.01f, value);
        private static float EaseOut(float value) => 1f - (1f - value) * (1f - value);

        private void StopSequence()
        {
            Sequence sequence = _sequence;
            _sequence = null;
            if (sequence != null && sequence.IsActive()) sequence.Kill(false);
        }

        private void OnDisable() => ResetToFinal(_data);
        private void OnDestroy() => StopSequence();
    }
}
