using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>확정된 스테이지 선택을 배경에 전달한다. 씬 진입 요청은 소비하지 않는다.</summary>
    public sealed class OutdoorBattlefieldStageBinding : MonoBehaviour
    {
        [SerializeField] private InGamePrototypeBootstrap _bootstrap;
        [SerializeField] private OutdoorBattlefieldPresentation _presentation;

        private void OnEnable()
        {
            if (_bootstrap == null || _presentation == null) return;
            _bootstrap.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_bootstrap != null) _bootstrap.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (!string.IsNullOrEmpty(_bootstrap.SelectedStageId))
                _presentation.SetStage(_bootstrap.SelectedStageId);
        }
    }
}
