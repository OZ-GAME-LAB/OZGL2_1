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

        /// <summary>
        /// roundNumber(1부터 시작하는 실제 라운드 번호)를 받아 밸런스 시트의 HP배율=1.068^(R-1),
        /// 공격배율=1.045^(R-1) 공식을 그대로 적용한 스냅샷을 만든다. 기본값 1이면 배율 없음(테스트 호출 호환용).
        /// </summary>
        public RoundDefinition CreateSnapshot(int roundNumber = 1)
        {
            if (_spawns == null) throw new InvalidOperationException("Spawn list is missing.");
            var spawns = new List<HeroSpawnDefinition>(_spawns.Count);
            foreach (var spawn in _spawns)
            {
                if (spawn == null) throw new InvalidOperationException("Spawn entry is missing.");
                spawns.Add(spawn.CreateSnapshot());
            }
            int exponent = Mathf.Max(0, roundNumber - 1);
            float hpMultiplier = Mathf.Pow(1.068f, exponent);
            float attackMultiplier = Mathf.Pow(1.045f, exponent);
            return new RoundDefinition(_roundId, spawns, _isBossRound, _rewardId,
                new AugmentTierWeights(_silverWeight, _goldWeight, _platinumWeight),
                hpMultiplier, attackMultiplier);
        }
    }
}
