using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>로비 난이도의 런타임 선택과 고정 프레임의 페이드 전환을 담당한다. 실제 전투 시작/저장은 외부 책임이다.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Lobby Difficulty Selector")]
    public sealed class UILobbyDifficultySelector : MonoBehaviour
    {
        [Header("교체 가능한 표시 설정")]
        [SerializeField] private LobbyDifficultyCatalogSO _catalog;
        [SerializeField] private LobbyDifficultyArtworkSetSO _artworkSet;
        [SerializeField] private eLobbyDifficulty _initialDifficulty = eLobbyDifficulty.NORMAL;

        [Header("위치가 고정된 슬롯")]
        [SerializeField] private UILobbyDifficultySlotView _previousSlot;
        [SerializeField] private UILobbyDifficultySlotView _currentSlot;
        [SerializeField] private UILobbyDifficultySlotView _nextSlot;
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _startButton;

        [Header("DOTween 전환")]
        [SerializeField, Min(0.01f)] private float _fadeOutDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float _fadeInDuration = 0.21f;
        [SerializeField, Range(0f, 0.15f)] private float _artworkSlideRatio = 0.06f;
        [SerializeField] private Ease _fadeOutEase = Ease.OutQuad;
        [SerializeField] private Ease _fadeInEase = Ease.OutCubic;

        private Sequence _transition;
        private int _selectedIndex = -1;
        private bool _isTransitioning;
        private bool _refreshPending;
        private bool _hasInputSubscription;
        private bool _hasStartButtonLease;
        private bool _wasStartInteractable;
        private Button _subscribedPreviousButton;
        private Button _subscribedNextButton;
        private Button _leasedStartButton;

        public LobbyDifficultyCatalogSO Catalog => _catalog;
        public LobbyDifficultyArtworkSetSO ArtworkSet => _artworkSet;
        public bool IsTransitioning => _isTransitioning;
        public int SelectedIndex => _selectedIndex;
        public eLobbyDifficulty SelectedDifficulty => SelectedEntry != null ? SelectedEntry.Difficulty : _initialDifficulty;
        public LobbyDifficultyCatalogSO.Entry SelectedEntry => _catalog == null ? null : _catalog.GetEntry(_selectedIndex);
        public event Action<eLobbyDifficulty> SelectionChanged;

        public bool TryValidate(out string reason)
        {
            if (_catalog == null)
            {
                reason = "난이도 Catalog가 연결되지 않았습니다.";
                return false;
            }
            if (!_catalog.TryValidate(out reason)) return false;
            if (_artworkSet == null || !_artworkSet.IsComplete)
            {
                reason = "Artwork Set의 쉬움 / 보통 / 어려움 그림을 모두 연결하세요.";
                return false;
            }
            if (_previousSlot == null || _currentSlot == null || _nextSlot == null
                || !_previousSlot.HasRequiredReferences || !_currentSlot.HasRequiredReferences || !_nextSlot.HasRequiredReferences
                || _previousButton == null || _nextButton == null)
            {
                reason = "세 슬롯 또는 좌우 버튼의 Inspector 참조를 확인하세요.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public void SelectPrevious() => TrySelectIndex(_selectedIndex - 1, true);
        public void SelectNext() => TrySelectIndex(_selectedIndex + 1, true);

        public bool TrySelect(eLobbyDifficulty difficulty, bool animate = true)
        {
            return _catalog != null && TrySelectIndex(_catalog.GetIndex(difficulty), animate);
        }

        /// <summary>런타임에서 그림 세트만 교체해도 선택된 난이도는 유지한다.</summary>
        public bool SetArtworkSet(LobbyDifficultyArtworkSetSO artworkSet)
        {
            if (artworkSet == null || !artworkSet.IsComplete) return false;
            CancelTransition();
            _artworkSet = artworkSet;
            RefreshPresentation();
            return true;
        }

        [ContextMenu("Refresh Difficulty Presentation")]
        public void RefreshPresentation()
        {
            _refreshPending = false;
            CancelTransition();
            if (!TryValidate(out _))
            {
                BindSlots(-1);
                RefreshButtons();
                return;
            }
            if (!Application.isPlaying || _selectedIndex < 0 || _selectedIndex >= _catalog.Count)
                _selectedIndex = _catalog.GetIndex(_initialDifficulty);
            BindSlots(_selectedIndex);
            RefreshButtons();
        }

        private bool TrySelectIndex(int index, bool animate)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _isTransitioning || !TryValidate(out _)
                || index < 0 || index >= _catalog.Count || index == _selectedIndex) return false;

            if (!animate)
            {
                _selectedIndex = index;
                BindSlots(_selectedIndex);
                RefreshButtons();
                SelectionChanged?.Invoke(SelectedDifficulty);
                return true;
            }

            int direction = index > _selectedIndex ? 1 : -1;
            _isTransitioning = true;
            LeaseStartButton();
            RefreshButtons();

            Sequence sequence = DOTween.Sequence();
            _transition = sequence;
            sequence.SetUpdate(true);
            sequence.Append(DOTween.To(() => 0f, progress =>
                SetTransition(1f - progress, -direction * _artworkSlideRatio * progress),
                1f, Mathf.Max(0.01f, _fadeOutDuration)).SetEase(_fadeOutEase));
            sequence.AppendCallback(() =>
            {
                // 보이지 않는 순간 표시만 갱신하고, 선택값은 페이드인 완료 후 확정한다.
                BindSlots(index);
                SetTransition(0f, direction * _artworkSlideRatio);
            });
            sequence.Append(DOTween.To(() => 0f, progress =>
                SetTransition(progress, direction * _artworkSlideRatio * (1f - progress)),
                1f, Mathf.Max(0.01f, _fadeInDuration)).SetEase(_fadeInEase));
            sequence.OnComplete(() =>
            {
                if (_transition != sequence) return;
                _transition = null;
                _isTransitioning = false;
                _selectedIndex = index;
                RestoreStartButton();
                BindSlots(_selectedIndex);
                RefreshButtons();
                SelectionChanged?.Invoke(SelectedDifficulty);
            });
            sequence.OnKill(() =>
            {
                if (_transition != sequence) return;
                _transition = null;
                _isTransitioning = false;
                RestoreStartButton();
                BindSlots(_selectedIndex);
                RefreshButtons();
            });
            return true;
        }

        private void BindSlots(int centerIndex)
        {
            bool hasCenter = _catalog != null && centerIndex >= 0 && centerIndex < _catalog.Count;
            BindSlot(_previousSlot, hasCenter ? centerIndex - 1 : -1);
            BindSlot(_currentSlot, hasCenter ? centerIndex : -1);
            BindSlot(_nextSlot, hasCenter ? centerIndex + 1 : -1);
        }

        private void BindSlot(UILobbyDifficultySlotView slot, int index)
        {
            if (slot == null) return;
            LobbyDifficultyCatalogSO.Entry entry = _catalog == null ? null : _catalog.GetEntry(index);
            Sprite artwork = entry == null || _artworkSet == null ? null : _artworkSet.GetArtwork(entry.Difficulty);
            slot.Bind(entry, artwork);
        }

        private void SetTransition(float opacity, float offsetRatio)
        {
            if (_previousSlot != null) _previousSlot.SetTransition(opacity, offsetRatio);
            if (_currentSlot != null) _currentSlot.SetTransition(opacity, offsetRatio);
            if (_nextSlot != null) _nextSlot.SetTransition(opacity, offsetRatio);
        }

        private void RefreshButtons()
        {
            bool canSelect = isActiveAndEnabled && !_isTransitioning && TryValidate(out _) && _selectedIndex >= 0;
            if (_previousButton != null) _previousButton.interactable = canSelect && _selectedIndex > 0;
            if (_nextButton != null) _nextButton.interactable = canSelect && _selectedIndex < _catalog.Count - 1;
        }

        private void LeaseStartButton()
        {
            if (_startButton == null || _hasStartButtonLease) return;
            _leasedStartButton = _startButton;
            _wasStartInteractable = _startButton.interactable;
            _hasStartButtonLease = true;
            _startButton.interactable = false;
        }

        private void RestoreStartButton()
        {
            if (!_hasStartButtonLease) return;
            if (_leasedStartButton != null) _leasedStartButton.interactable = _wasStartInteractable;
            _hasStartButtonLease = false;
            _leasedStartButton = null;
        }

        private void CancelTransition()
        {
            Sequence sequence = _transition;
            _transition = null;
            _isTransitioning = false;
            if (sequence != null && sequence.IsActive()) sequence.Kill(false);
            RestoreStartButton();
        }

        private void SubscribeInput()
        {
            UnsubscribeInput();
            _subscribedPreviousButton = _previousButton;
            _subscribedNextButton = _nextButton;
            if (_subscribedPreviousButton != null) _subscribedPreviousButton.onClick.AddListener(SelectPrevious);
            if (_subscribedNextButton != null) _subscribedNextButton.onClick.AddListener(SelectNext);
            _hasInputSubscription = true;
        }

        private void UnsubscribeInput()
        {
            if (!_hasInputSubscription) return;
            if (_subscribedPreviousButton != null) _subscribedPreviousButton.onClick.RemoveListener(SelectPrevious);
            if (_subscribedNextButton != null) _subscribedNextButton.onClick.RemoveListener(SelectNext);
            _hasInputSubscription = false;
            _subscribedPreviousButton = null;
            _subscribedNextButton = null;
        }

        private void OnEnable()
        {
            _refreshPending = true;
            if (!Application.isPlaying) return;
            RefreshPresentation();
            SubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            CancelTransition();
            // Edit Mode의 Undo/컴포넌트 제거 도중 복구되는 표시 값을 다시 덮어쓰지 않는다.
            if (!Application.isPlaying) return;
            BindSlots(_selectedIndex);
            RefreshButtons();
        }

        private void OnDestroy()
        {
            UnsubscribeInput();
            CancelTransition();
        }

        private void OnValidate()
        {
            // Unity 검증 콜백 중에는 다른 오브젝트에 값을 쓰지 않는다.
            _refreshPending = true;
        }

        private void LateUpdate()
        {
            if (!_refreshPending) return;
            RefreshPresentation();
            if (Application.isPlaying) SubscribeInput();
        }
    }
}
