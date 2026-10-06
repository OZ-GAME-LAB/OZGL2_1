using OZGL2.InGame;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>스테이지를 끝까지 클리어하면 기록해 다음 난이도를 연다(보통 → 어려움 → 지옥).</summary>
    public sealed class InGameClearRecorder : MonoBehaviour
    {
        private InGamePrototypeBootstrap _bootstrap;
        private string _recordedRun;

        private void Update()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            var result = _bootstrap != null ? _bootstrap.Result : null;
            var progress = result != null ? result.Progress : null;
            if (progress == null || !progress.IsCleared || progress.RunId == _recordedRun) return;
            _recordedRun = progress.RunId;
            OZGL2.Progression.StageClearStore.MarkCleared(progress.StageId);
            Debug.Log("[난이도] 스테이지 클리어 기록: " + progress.StageId);
        }
    }
}
