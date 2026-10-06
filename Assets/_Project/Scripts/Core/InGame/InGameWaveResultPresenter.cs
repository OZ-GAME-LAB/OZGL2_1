using System;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Stage;
using OZGL2.UIFlow;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>항상 활성인 UI 호스트에 둔다. 팝업 열기/닫기와 확인 명령만 연결한다.</summary>
    public sealed class InGameWaveResultPresenter : MonoBehaviour, IStageWaveResults
    {
        [SerializeField] private UIPopupController _popupController;
        [SerializeField] private UIPopupPanel _popup;
        [SerializeField] private InGameWaveResultView _view;
        private readonly WaveResultConfirmation _confirmation = new WaveResultConfirmation();
        public StageWaveResult Pending => _confirmation.Pending;

        public void ValidateSetup()
        {
            if (_popupController == null || _popup == null || _view == null || !isActiveAndEnabled)
                throw new InvalidOperationException("Wave result presenter is not connected or active.");
            if (_popup.CanDismiss) throw new InvalidOperationException("Wave results must require confirmation.");
            _view.ValidateSetup();
        }

        public async Task ShowAsync(StageWaveResult result, CancellationToken cancellationToken)
        {
            ValidateSetup();
            if (Pending != null) throw new InvalidOperationException("A wave result is already displayed.");
            cancellationToken.ThrowIfCancellationRequested();
            var wait = _confirmation.ShowAsync(result, cancellationToken);
            bool hasOpened = false;
            try
            {
                _view.Present(result, Confirm);
                _popupController.OpenPopup(_popup);
                hasOpened = _popupController.IsTopPopup(_popup);
                if (!hasOpened) throw new InvalidOperationException("Wave result popup could not open.");
                await wait;
            }
            finally
            {
                // 생성/표시 오류도 대기 Task를 남기지 않는다. 종료 시 상위 메뉴도 함께 정리한다.
                _confirmation.CancelPending();
                try { await wait; } catch (OperationCanceledException) { }
                if (_view != null) _view.Clear();
                if (hasOpened && _popupController != null && _popup != null && _popup.gameObject.activeSelf)
                {
                    while (_popupController.OpenCount > 0 && !_popupController.IsTopPopup(_popup))
                        _popupController.CloseConfirmedPopup();
                    if (_popupController.IsTopPopup(_popup)) _popupController.CloseConfirmedPopup();
                }
            }
        }

        private void Confirm(string requestId)
        {
            if (_popupController.IsTopPopup(_popup) && _confirmation.TryConfirm(requestId))
                _view.Clear();
        }

        private void OnDisable() => _confirmation.CancelPending();
    }
}
