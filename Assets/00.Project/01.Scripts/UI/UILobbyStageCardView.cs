using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>스테이지 카드의 그림·테두리·잠금 표시만 관리한다. 해금 판정과 저장은 외부 시스템의 책임이다.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Lobby Stage Card View")]
    public sealed class UILobbyStageCardView : MonoBehaviour
    {
        [Header("표시 연결")]
        [SerializeField] private Image _artworkImage;
        [SerializeField] private AspectRatioFitter _artworkFitter;
        [SerializeField] private Image _frameImage;
        [SerializeField] private Image _lockImage;
        [SerializeField] private CanvasGroup _lockGroup;
        [SerializeField] private Image _lockedShade;

        [Header("교체 가능한 아트")]
        [SerializeField] private Sprite _artworkSprite;
        [SerializeField] private Sprite _frameSprite;
        [SerializeField] private Sprite _lockSprite;

        [Header("잠금 표시")]
        [SerializeField] private bool _isLocked;
        [SerializeField] private Color _lockedShadeColor = new Color(0f, 0f, 0f, 0.3f);

        private bool _needsRefresh;

        public Sprite ArtworkSprite => _artworkSprite;
        public Sprite FrameSprite => _frameSprite;
        public Sprite LockSprite => _lockSprite;
        public bool IsLocked => _isLocked;

        public void SetArtwork(Sprite sprite)
        {
            _artworkSprite = sprite;
            RefreshVisuals();
        }

        public void SetFrame(Sprite sprite)
        {
            _frameSprite = sprite;
            RefreshVisuals();
        }

        public void SetLockSprite(Sprite sprite)
        {
            _lockSprite = sprite;
            RefreshVisuals();
        }

        /// <summary>실제 해금 상태를 저장하거나 버튼 기능을 바꾸지 않고 잠금 표시만 전환한다.</summary>
        public void SetLocked(bool isLocked)
        {
            _isLocked = isLocked;
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            _needsRefresh = false;

            if (_artworkImage != null)
            {
                _artworkImage.sprite = _artworkSprite;
                _artworkImage.enabled = _artworkSprite != null;
                _artworkImage.type = Image.Type.Simple;
                // 비율 유지는 Fitter가 담당하며 마스크 영역을 빈틈 없이 채운다.
                _artworkImage.preserveAspect = false;
                _artworkImage.raycastTarget = false;
            }

            if (_artworkFitter != null)
            {
                float ratio = 1f;
                if (_artworkSprite != null && _artworkSprite.rect.height > 0f)
                    ratio = _artworkSprite.rect.width / _artworkSprite.rect.height;

                _artworkFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                _artworkFitter.aspectRatio = Mathf.Clamp(ratio, 0.001f, 1000f);
            }

            if (_frameImage != null)
            {
                _frameImage.sprite = _frameSprite;
                _frameImage.enabled = _frameSprite != null;
                _frameImage.raycastTarget = false;
                // 기존 프레임의 색상은 카드별 Inspector 설정을 유지한다.
            }

            if (_lockImage != null)
            {
                _lockImage.sprite = _lockSprite;
                _lockImage.enabled = _lockSprite != null && (_lockGroup != null || _isLocked);
                _lockImage.raycastTarget = false;
            }

            if (_lockGroup != null)
            {
                _lockGroup.alpha = _isLocked ? 1f : 0f;
                _lockGroup.interactable = false;
                _lockGroup.blocksRaycasts = false;
            }

            if (_lockedShade != null)
            {
                Color shadeColor = _lockedShadeColor;
                if (!_isLocked) shadeColor.a = 0f;
                _lockedShade.color = shadeColor;
                _lockedShade.raycastTarget = false;
            }
        }

        private void OnEnable()
        {
            _needsRefresh = true;
        }

        private void OnValidate()
        {
            // Fitter는 값을 설정할 때 RectTransform을 즉시 수정하므로 로드/검증 콜백에서는 예약만 한다.
            _needsRefresh = true;
        }

        private void LateUpdate()
        {
            if (_needsRefresh) RefreshVisuals();
        }
    }
}
