using OZGL2.InGame;
using OZGL2.Progression;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 진행을 기록한다.
    /// - 웨이브를 깰 때마다 그 난이도의 최고 웨이브를 저장한다(도중에 나가도 남는다). 끝까지 클리어하면 걸린 시간도 저장한다.
    /// - 스테이지를 끝까지 클리어하면 다음 난이도를 연다(보통 → 어려움 → 지옥).
    /// 저장된 기록은 로비 난이도 카드의 기록 패널에 나온다.
    /// </summary>
    public sealed class InGameClearRecorder : MonoBehaviour
    {
        private InGamePrototypeBootstrap _bootstrap;
        private string _runId;
        private float _runStartedAt;
        private int _submittedWaves;
        private string _recordedClear;

        private void Update()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (_bootstrap == null) return;

            var progress = _bootstrap.Stage != null ? _bootstrap.Stage.Progress : null;
            if (progress == null) progress = _bootstrap.Result != null ? _bootstrap.Result.Progress : null;
            if (progress == null) return;

            if (progress.RunId != _runId)
            {
                _runId = progress.RunId;
                _runStartedAt = Time.unscaledTime;
                _submittedWaves = 0;
            }

            if (progress.ClearedRoundCount > _submittedWaves)
            {
                _submittedWaves = progress.ClearedRoundCount;
                StageRecordStore.Submit(progress.StageId, _submittedWaves, false, 0f);
            }

            if (progress.IsCleared && progress.RunId != _recordedClear)
            {
                _recordedClear = progress.RunId;
                StageRecordStore.Submit(progress.StageId, progress.ClearedRoundCount, true, Time.unscaledTime - _runStartedAt);
                StageClearStore.MarkCleared(progress.StageId);
                Debug.Log("[난이도] 스테이지 클리어 기록: " + progress.StageId);
            }
        }
    }
}
