using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Stage
{
    [CreateAssetMenu(fileName = "StageData", menuName = "OZGL2/Stage/Stage Data")]
    public sealed class StageDataSO : ScriptableObject, IStageDataSource
    {
        [SerializeField] private string _stageId;
        [SerializeField, Tooltip("UI 표시용 이름. 내부 ID와 저장 데이터에는 영향을 주지 않습니다.")]
        private string _displayName;
        [SerializeField] private List<RoundData> _rounds = new List<RoundData>();

        [Header("난이도별 라운드 배율 (보통/어려움을 이 비율 차이로 구분)")]
        [Tooltip("라운드 R의 HP배율 = 이 값^(R-1). 1에 가까울수록 완만, 클수록 라운드가 갈수록 가파르게 세짐.")]
        [SerializeField] private float _hpMultiplierRate = RoundData.DefaultHpMultiplierRate;
        [SerializeField] private float _attackMultiplierRate = RoundData.DefaultAttackMultiplierRate;
        [Tooltip("테스트용: 배율 계산에만 더해지는 라운드 번호. 예) 49면 첫 라운드가 50라운드 배율로 나온다. 일반 스테이지는 0.")]
        [SerializeField, Min(0)] private int _roundMultiplierOffset;
        public string StageId => _stageId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _stageId : _displayName.Trim();

        public StageDefinition CreateSnapshot()
        {
            if (_rounds == null) throw new InvalidOperationException("Round list is missing.");
            var rounds = new List<RoundDefinition>(_rounds.Count);
            for (int i = 0; i < _rounds.Count; i++)
            {
                var round = _rounds[i];
                if (round == null) throw new InvalidOperationException("Round entry is missing.");
                rounds.Add(round.CreateSnapshot(i + 1 + _roundMultiplierOffset, _hpMultiplierRate, _attackMultiplierRate));
            }
            return new StageDefinition(_stageId, rounds);
        }
    }
}
