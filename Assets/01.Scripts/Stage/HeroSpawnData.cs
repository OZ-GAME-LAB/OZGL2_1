using System;
using UnityEngine;

namespace OZGL2.Stage
{
    [Serializable]
    public sealed class HeroSpawnData
    {
        [SerializeField] private string _heroId;
        [SerializeField, Min(1)] private int _count;
        [SerializeField, Min(0)] private float _intervalSeconds;

        public HeroSpawnDefinition CreateSnapshot()
        {
            return new HeroSpawnDefinition(_heroId, _count, _intervalSeconds);
        }
    }
}
