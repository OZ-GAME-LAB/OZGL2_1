using System;
using System.Collections.Generic;
using OZGL2.Stage;
using OZGL2.UIFlow;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>실제 UI와 더미 UI가 공통으로 호출하는 선택/이동 진입점.</summary>
    public sealed class StageSelectionController : MonoBehaviour
    {
        [SerializeField] private InGamePrototypeConfigSO _inGameConfig;
        [SerializeField] private UISceneNavigator _navigator;
        [SerializeField] private string _inGameScenePath;
        [SerializeField] private string _lobbyScenePath;
        private bool _isLoading;
        public bool IsLoading => _isLoading;
        public string Error { get; private set; }
        public IReadOnlyList<StageDefinition> GetStages() => _inGameConfig != null && _inGameConfig.StageCatalog != null
            ? _inGameConfig.StageCatalog.CreateDefinitions() : throw new InvalidOperationException("Stage catalog is required.");

        private void Awake() => StageLaunchRuntime.Session.EnterSelection();
        public void StartStage(string stageId) => TryStartStage(stageId);

        public bool TryStartStage(string stageId)
        {
            if (_isLoading) return false;
            Error = null;
            StageLaunchRequest request = null;
            try
            {
                if (_inGameConfig == null || _inGameConfig.StageCatalog == null || _navigator == null)
                    throw new InvalidOperationException("Stage selection references are incomplete.");
                _inGameConfig.Validate(_inGameConfig.StageCatalog.Resolve(stageId));
                request = new StageLaunchRequest(stageId, _inGameScenePath);
                if (!StageLaunchRuntime.Session.TryQueue(request)) throw new InvalidOperationException("Another stage request is pending.");
                _isLoading = true;
                if (!_navigator.TryLoadScene(_inGameScenePath, out var error)) throw new InvalidOperationException(error);
                return true;
            }
            catch (Exception exception)
            {
                if (request != null) StageLaunchRuntime.Session.Cancel(request.RequestId);
                _isLoading = false;
                Error = exception.Message;
                return false;
            }
        }

        public bool TryReturnToLobby()
        {
            if (_isLoading) return false;
            if (_navigator == null) { Error = "Scene navigator is required."; return false; }
            if (!_navigator.TryLoadScene(_lobbyScenePath, out var error)) { Error = error; return false; }
            StageLaunchRuntime.Session.EnterSelection();
            _isLoading = true;
            Error = null;
            return true;
        }
    }
}
