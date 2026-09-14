using System;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Stage;
using UnityEngine;

namespace OZGL2.InGame
{
    [CreateAssetMenu(menuName = "OZGL2/InGame/Prototype Config")]
    public sealed class InGamePrototypeConfigSO : ScriptableObject
    {
        [SerializeField] private StageDataSO _stage;
        [SerializeField] private GridPrototypeCatalogSO _catalog;
        [SerializeField] private Vector2Int _initialAnchor;
        [SerializeField] private string _lobbyScenePath;
        public StageDataSO Stage => _stage;
        public GridPrototypeCatalogSO Catalog => _catalog;
        public Vector2Int InitialAnchor => _initialAnchor;
        public string LobbyScenePath => _lobbyScenePath;

        public void Validate()
        {
            if (_stage == null || _catalog == null || string.IsNullOrWhiteSpace(_lobbyScenePath))
                throw new InvalidOperationException("InGame stage, grid catalog and lobby scene are required.");
            _stage.CreateSnapshot();
            var unit = _catalog.CreateInitialUnit();
            var block = _catalog.CreateInitialBlock();
            if (unit.Footprint.Cells.Count != 1 || block.Cells.Count != 1 || unit.RewardBlockId != block.Id)
                throw new InvalidOperationException("The first battle requires one basic unit and its single-cell block.");
            new GridPrototypeRewards(_catalog);
            // 실제 시작과 동일한 배치 경로로 검증하여 잘못된 SO는 실행 전에 거부한다.
            using (var session = new GridRunSession("configuration_check", _catalog.CreateDefinition()))
                StageGridPreparation.PlaceInitial(session, unit, block, _initialAnchor);
        }
    }
}
