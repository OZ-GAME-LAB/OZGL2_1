using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Stage
{
    [Serializable]
    public sealed class RoundData
    {
        [SerializeField] private string _roundId;
        [SerializeField] private List<HeroSpawnData> _spawns = new List<HeroSpawnData>();
        [SerializeField] private bool _isBossRound;
        [SerializeField] private string _rewardId;
        [SerializeField, Min(0)] private int _silverWeight;
        [SerializeField, Min(0)] private int _goldWeight;
        [SerializeField, Min(0)] private int _platinumWeight;

        public RoundDefinition CreateSnapshot()
        {
            if (_spawns == null) throw new InvalidOperationException("Spawn list is missing.");
            var spawns = new List<HeroSpawnDefinition>(_spawns.Count);
            foreach (var spawn in _spawns)
            {
                if (spawn == null) throw new InvalidOperationException("Spawn entry is missing.");
                spawns.Add(spawn.CreateSnapshot());
            }
            return new RoundDefinition(_roundId, spawns, _isBossRound, _rewardId,
                new AugmentTierWeights(_silverWeight, _goldWeight, _platinumWeight));
        }
    }
}
