using OZGL2.Grid;
using OZGL2.InGame;
using OZGL2.Stage;
using OZGL2.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZGL2.UIFlow
{
    /// <summary>전투 상태를 읽어 ESC 메뉴 열기만 요청한다. 팝업 닫기는 기존 Controller가 처리한다.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class UIBattleMenuEscapeInput : MonoBehaviour
    {
        [Header("항상 활성인 UI 호스트에 연결")]
        [SerializeField] private UIPopupController _popupController;
        [SerializeField] private UIPopupPanel _menuPopup;

        [Header("읽기 전용 전투 상태")]
        [Tooltip("실제 InGame Scene에서는 명시적으로 연결한다. 비어 있으면 UI 표시 미리보기로 동작한다.")]
        [SerializeField] private InGamePrototypeBootstrap _bootstrap;
        [SerializeField] private InGamePhasePresentation _phasePresentation;
        [SerializeField] private InGameAugmentRewards _augmentProvider;
        [SerializeField] private InGameWaveResultPresenter _waveResultPresenter;
        [SerializeField] private TutorialDirector _tutorialDirector;

        private bool _hasOpenRequest;
        private int _requestedFrame = -1;
        private GridRunSession _requestedSession;

        private void OnEnable()
        {
            ClearOpenRequest();
            ResolveReferences();
        }

        private void OnDisable() => ClearOpenRequest();

        private void Update()
        {
            ClearOpenRequest();
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame || !CanOpenMenu()) return;

            // 기존 팝업 닫기와 드래그 취소보다 먼저 상태를 저장해 같은 ESC로 메뉴가 다시 열리지 않게 한다.
            _hasOpenRequest = true;
            _requestedFrame = Time.frameCount;
            _requestedSession = _bootstrap != null ? _bootstrap.GridSession : null;
        }

        private void LateUpdate()
        {
            bool hasOpenRequest = _hasOpenRequest;
            int requestedFrame = _requestedFrame;
            GridRunSession requestedSession = _requestedSession;
            ClearOpenRequest();

            // 모든 Update의 ESC 닫기 처리가 끝난 뒤, 같은 실행의 유효한 요청만 한 번 연다.
            if (!hasOpenRequest || requestedFrame != Time.frameCount || !CanOpenMenu()) return;
            GridRunSession currentSession = _bootstrap != null ? _bootstrap.GridSession : null;
            if (!ReferenceEquals(requestedSession, currentSession)) return;
            _popupController.OpenPopup(_menuPopup);
        }

        private bool CanOpenMenu()
        {
            if (_popupController == null || !_popupController.isActiveAndEnabled ||
                _popupController.OpenCount != 0 || _menuPopup == null || _menuPopup.gameObject.activeSelf) return false;
            if (_phasePresentation != null && _phasePresentation.IsTransitioning) return false;
            if (_augmentProvider != null && _augmentProvider.IsPending) return false;
            if (_waveResultPresenter != null && _waveResultPresenter.Pending != null) return false;
            if (_tutorialDirector != null && _tutorialDirector.IsPlaying) return false;

            // Runtime 소스 없는 UI 미리보기에서도 기존 팝업 표시를 확인할 수 있다.
            if (_bootstrap == null) return true;
            if (_bootstrap.IsUiInputBlocked || _bootstrap.Result != null || _bootstrap.Stage == null) return false;
            eStageState state = _bootstrap.Stage.State;
            if (state != eStageState.PREPARATION && state != eStageState.COMBAT) return false;

            GridRunSession session = _bootstrap.GridSession;
            return session != null && !session.IsEnded && session.Grid != null &&
                !session.Grid.HasSelection && !session.Grid.HasPendingStorage;
        }

        private void ResolveReferences()
        {
            if (_popupController == null) _popupController = GetComponentInParent<UIPopupController>(true);
            if (_popupController == null) _popupController = GetComponentInChildren<UIPopupController>(true);
            if (_menuPopup == null)
            {
                UIBattleMenuPopupView menuView = GetComponentInChildren<UIBattleMenuPopupView>(true);
                if (menuView != null && menuView.TryGetComponent(out UIPopupPanel menuPopup)) _menuPopup = menuPopup;
            }

            if (_bootstrap == null) _bootstrap = GetComponentInParent<InGamePrototypeBootstrap>(true);
            if (_phasePresentation == null) _phasePresentation = GetComponentInParent<InGamePhasePresentation>(true);
            if (_augmentProvider == null) _augmentProvider = GetComponentInParent<InGameAugmentRewards>(true);
            if (_waveResultPresenter == null) _waveResultPresenter = GetComponentInParent<InGameWaveResultPresenter>(true);
            if (_tutorialDirector == null) _tutorialDirector = GetComponentInParent<TutorialDirector>(true);
        }

        private void ClearOpenRequest()
        {
            _hasOpenRequest = false;
            _requestedFrame = -1;
            _requestedSession = null;
        }
    }
}
