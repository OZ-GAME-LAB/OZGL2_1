using System;
using System.Collections.Generic;
using OZGL2.Stage;
using UnityEngine;

namespace OZGL2.InGame
{
    [CreateAssetMenu(menuName = "OZGL2/InGame/Stage Catalog")]
    public sealed class StageCatalogSO : ScriptableObject
    {
        [SerializeField] private StageDataSO[] _stages = Array.Empty<StageDataSO>();

        public IReadOnlyList<StageDefinition> CreateDefinitions()
        {
            if (_stages == null || _stages.Length == 0) throw new InvalidOperationException("Stage catalog is empty.");
            var result = new List<StageDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in _stages)
            {
                if (source == null) throw new InvalidOperationException("Stage catalog contains a missing asset.");
                var stage = source.CreateSnapshot();
                if (!ids.Add(stage.StageId)) throw new InvalidOperationException("Duplicate stage ID: " + stage.StageId);
                result.Add(stage);
            }
            return result.AsReadOnly();
        }

        public StageDefinition Resolve(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId)) throw new ArgumentException("Stage ID is required.");
            foreach (var stage in CreateDefinitions())
                if (stage.StageId == stageId) return stage;
            throw new InvalidOperationException("Unknown stage ID: " + stageId);
        }
    }
}
