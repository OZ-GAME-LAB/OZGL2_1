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

        // 원래 시트 공식(HP=1.068^(R-1), 공격=1.045^(R-1))은 지수 성장이라, 그리드 칸 수 상한(8x5=40칸)에
        // 묶여 사실상 선형으로만 느는 유닛 성장 속도를 라운드가 갈수록 압도해버렸다(보스2부터 스킬·특성을
        // 끝까지 투자해도 못 깨는 수준). 유닛 성장(그리드 보상+합성)만으로 1~5번 보스를 전부 클리어 가능한
        // 선에서 역산한 완만한 비율로 낮춤 — 특성/스킬은 이제 "필수"가 아니라 여유분(안전마진)으로 작용한다.
        private const float RoundHpMultiplierRate = 1.036f;
        private const float RoundAttackMultiplierRate = 1.024f;

        /// <summary>
        /// roundNumber(1부터 시작하는 실제 라운드 번호)를 받아 라운드별 HP/공격 배율을 적용한 스냅샷을 만든다.
        /// 기본값 1이면 배율 없음(테스트 호출 호환용).
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
            float hpMultiplier = Mathf.Pow(RoundHpMultiplierRate, exponent);
            float attackMultiplier = Mathf.Pow(RoundAttackMultiplierRate, exponent);
            return new RoundDefinition(_roundId, spawns, _isBossRound, _rewardId,
                new AugmentTierWeights(_silverWeight, _goldWeight, _platinumWeight),
                hpMultiplier, attackMultiplier);
        }
    }
}
