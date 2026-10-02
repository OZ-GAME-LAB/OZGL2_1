using System;
using OZGL2.InGame;
using OZGL2.UIFlow;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비의 "시작" 버튼 → 선택한 난이도의 스테이지로 인게임 진입.
    /// 로비 난이도 카드(eLobbyDifficulty)를 스테이지 ID로 바꿔 팀의 공용 진입점(StageSelectionController.TryStartStage)에 넘긴다.
    /// 그 진입점이 스테이지 요청을 대기열에 넣고 InGame 씬을 로드하므로 실제 인게임과 같은 경로를 탄다.
    /// </summary>
    public sealed class LobbyStageLauncher : MonoBehaviour
    {
        [Serializable]
        public struct Mapping
        {
            public eLobbyDifficulty difficulty;
            public string stageId;
        }

        [SerializeField] private UILobbyDifficultySelector _selector;
        [SerializeField] private StageSelectionController _controller;
        [SerializeField] private Mapping[] _mappings = Array.Empty<Mapping>();

        /// <summary>시작 버튼의 UnityEvent에서 호출한다.</summary>
        public void StartSelected()
        {
            if (_selector == null || _controller == null)
            {
                Debug.LogWarning("로비 시작 연결이 비어 있습니다 (selector/controller).", this);
                return;
            }
            if (_selector.IsTransitioning || _controller.IsLoading) return;

            var difficulty = _selector.SelectedDifficulty;
            string stageId = FindStageId(difficulty);
            if (string.IsNullOrEmpty(stageId))
            {
                Debug.LogWarning("'" + difficulty + "' 난이도에 연결된 스테이지가 아직 없습니다.", this);
                return;
            }
            if (!_controller.TryStartStage(stageId))
                Debug.LogWarning("스테이지 시작 실패(" + stageId + "): " + _controller.Error, this);
        }

        private string FindStageId(eLobbyDifficulty difficulty)
        {
            if (_mappings == null) return null;
            foreach (var mapping in _mappings)
                if (mapping.difficulty == difficulty) return mapping.stageId;
            return null;
        }
    }
}
