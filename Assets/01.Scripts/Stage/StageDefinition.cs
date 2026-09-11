using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OZGL2.Stage
{
    /// <summary>외부 SO 또는 더미에서 변환한 실행용 읽기 전용 데이터입니다.</summary>
    public sealed class StageDefinition
    {
        public string StageId { get; }
        public IReadOnlyList<RoundDefinition> Rounds { get; }

        public StageDefinition(string stageId, IEnumerable<RoundDefinition> rounds)
        {
            StageDataValidation.ValidateId(stageId, nameof(stageId));
            if (rounds == null) throw new ArgumentNullException(nameof(rounds));
            var copy = new List<RoundDefinition>(rounds);
            if (copy.Count == 0) throw new ArgumentException("At least one round is required.", nameof(rounds));
            var ids = new HashSet<string>();
            foreach (var round in copy)
            {
                if (round == null || !ids.Add(round.RoundId))
                    throw new ArgumentException("Rounds must be non-null and have unique IDs.", nameof(rounds));
            }
            StageId = stageId;
            Rounds = new ReadOnlyCollection<RoundDefinition>(copy);
        }
    }

    public sealed class RoundDefinition
    {
        public string RoundId { get; }
        public IReadOnlyList<HeroSpawnDefinition> Spawns { get; }
        public bool IsBossRound { get; }
        public string RewardId { get; }
        public AugmentTierWeights AugmentWeights { get; }

        public RoundDefinition(string roundId, IEnumerable<HeroSpawnDefinition> spawns,
            bool isBossRound, string rewardId, AugmentTierWeights augmentWeights)
        {
            StageDataValidation.ValidateId(roundId, nameof(roundId));
            StageDataValidation.ValidateId(rewardId, nameof(rewardId));
            if (spawns == null) throw new ArgumentNullException(nameof(spawns));
            var copy = new List<HeroSpawnDefinition>(spawns);
            if (copy.Count == 0 || copy.Exists(spawn => spawn == null))
                throw new ArgumentException("At least one valid spawn entry is required.", nameof(spawns));
            RoundId = roundId;
            Spawns = new ReadOnlyCollection<HeroSpawnDefinition>(copy);
            IsBossRound = isBossRound;
            RewardId = rewardId;
            AugmentWeights = augmentWeights ?? throw new ArgumentNullException(nameof(augmentWeights));
        }
    }

    public sealed class HeroSpawnDefinition
    {
        public string HeroId { get; }
        public int Count { get; }
        public float IntervalSeconds { get; }

        public HeroSpawnDefinition(string heroId, int count, float intervalSeconds)
        {
            // 외부 밸런스 시트의 식별자를 정규화하면 실제 SO 조회가 끊길 수 있다.
            if (string.IsNullOrWhiteSpace(heroId) || heroId != heroId.Trim())
                throw new ArgumentException("An exact external hero ID is required.", nameof(heroId));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (float.IsNaN(intervalSeconds) || float.IsInfinity(intervalSeconds) || intervalSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            HeroId = heroId;
            Count = count;
            IntervalSeconds = intervalSeconds;
        }
    }

    public sealed class AugmentTierWeights
    {
        public int Silver { get; }
        public int Gold { get; }
        public int Platinum { get; }

        public AugmentTierWeights(int silver, int gold, int platinum)
        {
            if (silver < 0 || gold < 0 || platinum < 0 || (long)silver + gold + platinum == 0)
                throw new ArgumentException("Tier weights must be non-negative with a positive total.");
            Silver = silver;
            Gold = gold;
            Platinum = platinum;
        }
    }

    internal static class StageDataValidation
    {
        internal static void ValidateId(string id, string parameterName)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("ID is required.", parameterName);
            foreach (char character in id)
            {
                if ((character < 'a' || character > 'z') && (character < '0' || character > '9') && character != '_')
                    throw new ArgumentException("IDs use lowercase English letters, digits and underscores.", parameterName);
            }
        }
    }
}
