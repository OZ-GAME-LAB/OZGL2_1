using OZGL2.Progression;
using OZGL2.UIFlow;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비 난이도 카드의 기록 패널(최고 웨이브 · 클리어 시간)에 저장된 기록을 넣어 준다.
    /// 패널은 "기록은 외부가 주입한다"는 규칙이라 비어 있었다. 로비가 열릴 때 한 번, 이후 주기적으로 다시 읽어 맞춘다.
    /// 난이도 카드 순서(보통·어려움·지옥)는 StageClearStore.OrderedStageIds 의 순서와 같다.
    /// </summary>
    public sealed class LobbyRecordBridge : MonoBehaviour
    {
        private UILobbyDifficultyRecordPanel _panel;
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            if (_panel == null)
            {
                _panel = FindFirstObjectByType<UILobbyDifficultyRecordPanel>(FindObjectsInactive.Include);
                if (_panel == null) return;
            }

            var ids = StageClearStore.OrderedStageIds;
            for (int i = 0; i < ids.Length; i++)
            {
                var difficulty = (eLobbyDifficulty)i;
                int wave = StageRecordStore.BestWave(ids[i]);
                if (wave <= 0) { _panel.ClearRecord(difficulty); continue; }
                _panel.SetRecord(difficulty, wave, StageRecordStore.BestClearSeconds(ids[i]));
            }
        }
    }
}
