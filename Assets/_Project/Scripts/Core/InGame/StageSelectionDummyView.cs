using System;
using System.Collections.Generic;
using OZGL2.Stage;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>UI 담당자가 교체할 임시 표시. 선택 규칙과 씬 전환은 Controller에 위임한다.</summary>
    public sealed class StageSelectionDummyView : MonoBehaviour
    {
        [SerializeField] private StageSelectionController _controller;
        private IReadOnlyList<StageDefinition> _stages;
        private string _error;
        private void Start()
        {
            try { _stages = _controller.GetStages(); }
            catch (Exception exception) { _error = exception.Message; }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect((Screen.width - 460) / 2f, (Screen.height - 280) / 2f, 460, 280), GUI.skin.box);
            GUILayout.Label("STAGE SELECTION / PROTOTYPE");
            GUILayout.Label("Final UI will replace this view.");
            bool previous = GUI.enabled;
            GUI.enabled = previous && _controller != null && !_controller.IsLoading;
            if (_stages != null)
                foreach (var stage in _stages)
                    if (GUILayout.Button(stage.StageId + " / " + stage.Rounds.Count + " rounds", GUILayout.Height(44)))
                        _controller.TryStartStage(stage.StageId);
            if (GUILayout.Button("Back to lobby")) _controller.TryReturnToLobby();
            GUI.enabled = previous;
            GUILayout.Label(_error ?? (_controller != null ? _controller.Error : "Missing controller") ?? string.Empty);
            GUILayout.EndArea();
        }
    }
}
