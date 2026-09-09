using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Stage
{
    [CreateAssetMenu(fileName = "StageData", menuName = "OZGL2/Stage/Stage Data")]
    public sealed class StageDataSO : ScriptableObject, IStageDataSource
    {
        [SerializeField] private string _stageId;
        [SerializeField] private List<RoundData> _rounds = new List<RoundData>();

        public StageDefinition CreateSnapshot()
        {
            if (_rounds == null) throw new InvalidOperationException("Round list is missing.");
            var rounds = new List<RoundDefinition>(_rounds.Count);
            foreach (var round in _rounds)
            {
                if (round == null) throw new InvalidOperationException("Round entry is missing.");
                rounds.Add(round.CreateSnapshot());
            }
            return new StageDefinition(_stageId, rounds);
        }
    }
}
