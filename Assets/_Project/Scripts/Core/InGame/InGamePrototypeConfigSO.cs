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
        [SerializeField] private HeroPoolCatalogSO _heroPoolCatalog;
        [SerializeField] private Vector3 _heroSpawnPosition;
        [SerializeField] private DemonArmyCatalog _demonArmyCatalog;
        [SerializeField] private Vector3 _gridWorldOrigin;
        [SerializeField, Tooltip("그리드 1칸 = 몇 월드 유닛인지. ⚠ 준기·김건과 협의 전 임시값(기본 1).")]
        private float _cellWorldSize = 1f;
        [SerializeField, Min(1)] private float _externalOperationTimeoutSeconds = InGameCombatConnection.DEFAULT_TIMEOUT_SECONDS;
        public float ExternalOperationTimeoutSeconds => _externalOperationTimeoutSeconds;
        public StageDataSO Stage => _stage;
        public GridPrototypeCatalogSO Catalog => _catalog;
        public Vector2Int InitialAnchor => _initialAnchor;
        public string LobbyScenePath => _lobbyScenePath;
        public HeroPoolCatalogSO HeroPoolCatalog => _heroPoolCatalog;
        public Vector3 HeroSpawnPosition => _heroSpawnPosition;
        public DemonArmyCatalog DemonArmyCatalog => _demonArmyCatalog;
        public Vector3 GridWorldOrigin => _gridWorldOrigin;
        public float CellWorldSize => _cellWorldSize;

        public void Validate()
        {
            if (_stage == null || _catalog == null || string.IsNullOrWhiteSpace(_lobbyScenePath))
                throw new InvalidOperationException("InGame stage, grid catalog and lobby scene are required.");
            if (_heroPoolCatalog == null)
                throw new InvalidOperationException("InGame hero pool catalog is required.");
            if (_demonArmyCatalog == null)
                throw new InvalidOperationException("InGame demon army catalog is required.");
            if (!float.IsFinite(_cellWorldSize) || _cellWorldSize <= 0 ||
                !float.IsFinite(_externalOperationTimeoutSeconds) || _externalOperationTimeoutSeconds < 1 || _externalOperationTimeoutSeconds > 3600 ||
                !float.IsFinite(_heroSpawnPosition.x) || !float.IsFinite(_heroSpawnPosition.y) || !float.IsFinite(_heroSpawnPosition.z))
                throw new InvalidOperationException("Invalid grid coordinates or external operation timeout (1–3600 seconds).");
            _ = new GridWorldMapping(_gridWorldOrigin, Vector3.right * _cellWorldSize, Vector3.up * _cellWorldSize);
            var stage = _stage.CreateSnapshot();
            var heroIds = new System.Collections.Generic.HashSet<string>();
            foreach (var entry in _heroPoolCatalog.CreateSnapshot())
            {
                // 같은 heroId에 모델링 변형을 여러 개 등록하는 게 정상 구성이라(HeroPool이 Rent 시 랜덤
                // 선택), heroId 중복 자체는 더 이상 오류가 아니다 — 항목 하나하나의 유효성만 검사한다.
                if (string.IsNullOrWhiteSpace(entry.HeroId) || entry.Prefab == null ||
                    entry.InitialCapacity < 0 || entry.GrowthCount <= 0 || entry.Experience < 0)
                    throw new InvalidOperationException("Invalid hero pool entry.");
                heroIds.Add(entry.HeroId);
            }
            foreach (var round in stage.Rounds)
                foreach (var spawn in round.Spawns)
                    if (!heroIds.Contains(spawn.HeroId)) throw new InvalidOperationException("Missing hero pool ID: " + spawn.HeroId);
            foreach (var candidate in _catalog.CreateUnits())
            {
                var prefab = _demonArmyCatalog.FindPrefab(candidate.Id);
                if (prefab == null || prefab.statData == null)
                    throw new InvalidOperationException("Missing demon army prefab/stat data: " + candidate.Id);
            }
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
