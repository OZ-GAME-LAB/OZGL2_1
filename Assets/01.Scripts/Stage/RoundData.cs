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
        // 끝까지 투자해도 못 깨는 수준). 유닛 성장(그리드 보상+합성)만으로 보스를 전부 클리어 가능한 선에서
        // 역산한 완만한 비율이 기본값 — 특성/스킬은 이제 "필수"가 아니라 여유분(안전마진)으로 작용한다.
        // 스테이지(StageDataSO)마다 다른 비율을 줄 수 있어서, 보통/어려움 난이도를 "라운드 배율" 자체로
        // 구분할 수 있다 — CreateSnapshot 호출부(StageDataSO)가 난이도별 값을 넘겨준다.
        public const float DefaultHpMultiplierRate = 1.036f;
        public const float DefaultAttackMultiplierRate = 1.024f;

        /// <summary>
        /// roundNumber(1부터 시작하는 실제 라운드 번호)와 스테이지별 HP/공격 배율 비율을 받아 스냅샷을 만든다.
        /// 기본값(파라미터 생략)은 보통 난이도 기준값 — 테스트 호출 호환용.
        /// </summary>
        public RoundDefinition CreateSnapshot(int roundNumber = 1,
            float hpMultiplierRate = DefaultHpMultiplierRate, float attackMultiplierRate = DefaultAttackMultiplierRate)
        {
            if (_spawns == null) throw new InvalidOperationException("Spawn list is missing.");
            var spawns = new List<HeroSpawnDefinition>(_spawns.Count);
            foreach (var spawn in _spawns)
            {
                if (spawn == null) throw new InvalidOperationException("Spawn entry is missing.");
                spawns.Add(spawn.CreateSnapshot());
            }
            int exponent = Mathf.Max(0, roundNumber - 1);
            float hpMultiplier = Mathf.Pow(hpMultiplierRate, exponent);
            float attackMultiplier = Mathf.Pow(attackMultiplierRate, exponent);
            return new RoundDefinition(_roundId, spawns, _isBossRound, _rewardId,
                new AugmentTierWeights(_silverWeight, _goldWeight, _platinumWeight),
                hpMultiplier, attackMultiplier);
        }
    }
}
