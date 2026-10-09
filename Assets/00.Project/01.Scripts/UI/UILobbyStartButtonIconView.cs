using OZGL2.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>기존 난이도 해금 상태를 읽어 시작 버튼 아이콘만 교체한다. 입력과 저장은 기존 코드가 담당한다.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Lobby Start Button Icon View")]
    public sealed class UILobbyStartButtonIconView : MonoBehaviour
    {
        [SerializeField] private UILobbyDifficultySelector _selector;
        [SerializeField] private Image _iconVisual;
        [SerializeField] private Sprite _unlockedIcon;
        [SerializeField] private Sprite _lockedIcon;

        private UILobbyDifficultySelector _subscribedSelector;
        private bool _hasAppliedIcon;
        private bool _isLocked;

        private void OnEnable()
        {
            UnsubscribeSelection();
            _hasAppliedIcon = false;
            if (!Application.isPlaying) return;

            _subscribedSelector = _selector;
            if (_subscribedSelector != null)
                _subscribedSelector.SelectionChanged += OnSelectionChanged;
            RefreshIcon();
        }

        private void LateUpdate()
        {
            // 디버그 해금에는 변경 이벤트가 없으므로 상태만 읽고, 같은 아이콘은 다시 쓰지 않는다.
            RefreshIcon();
        }

        private void OnSelectionChanged(eLobbyDifficulty difficulty)
        {
            RefreshIcon();
        }

        private void RefreshIcon()
        {
            if (!isActiveAndEnabled || _selector == null || !_selector.isActiveAndEnabled
                || _selector.IsTransitioning || _iconVisual == null
                || _unlockedIcon == null || _lockedIcon == null) return;

            int index = _selector.SelectedIndex;
            if (_selector.Catalog == null || index < 0 || index >= _selector.Catalog.Count) return;

            // enum 이름이 아닌, 기존 잠금 코드와 같은 난이도 순서를 사용한다.
            ApplyIcon(!StageClearStore.IsDifficultyUnlocked(index));
        }

        private void ApplyIcon(bool isLocked)
        {
            Sprite icon = isLocked ? _lockedIcon : _unlockedIcon;
            if (_iconVisual == null || icon == null) return;
            if (_hasAppliedIcon && _isLocked == isLocked
                && _iconVisual.sprite == icon && _iconVisual.overrideSprite == icon) return;

            _iconVisual.sprite = icon;
            _iconVisual.overrideSprite = icon;
            _isLocked = isLocked;
            _hasAppliedIcon = true;
        }

        private void OnDisable()
        {
            UnsubscribeSelection();
            RestoreIcon();
        }

        private void OnDestroy()
        {
            UnsubscribeSelection();
            RestoreIcon();
        }

        private void UnsubscribeSelection()
        {
            if (_subscribedSelector != null)
                _subscribedSelector.SelectionChanged -= OnSelectionChanged;
            _subscribedSelector = null;
        }

        private void RestoreIcon()
        {
            if (_hasAppliedIcon && _iconVisual != null && _unlockedIcon != null)
            {
                _iconVisual.sprite = _unlockedIcon;
                _iconVisual.overrideSprite = _unlockedIcon;
            }
            _hasAppliedIcon = false;
        }
    }
}
