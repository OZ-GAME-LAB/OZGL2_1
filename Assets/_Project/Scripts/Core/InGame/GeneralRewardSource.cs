using System;
using System.Collections.Generic;
using OZGL2.Grid;

namespace OZGL2.InGame
{
    public interface IUnitRewardUnlocks
    {
        bool IsUnlocked(string unitId);
    }

    public enum eGeneralRewardKind { UNIT, EXPANSION }

    public sealed class GeneralRewardOption
    {
        public eGeneralRewardKind Kind { get; }
        public UnitDefinition Unit { get; }
        public FootprintDefinition Block { get; }
        public int StarLevel => 1;
        public GeneralRewardOption(UnitDefinition unit, FootprintDefinition block)
        {
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            Block = block ?? throw new ArgumentNullException(nameof(block));
            if (unit.RewardBlockId != block.Id) throw new ArgumentException("Reward block mismatch: " + unit.Id);
            Kind = eGeneralRewardKind.UNIT;
        }
        private GeneralRewardOption() { Kind = eGeneralRewardKind.EXPANSION; }
        public static GeneralRewardOption CreateExpansion() => new GeneralRewardOption();
    }

    public sealed class UnitRewardEntry
    {
        public GeneralRewardOption Option { get; }
        public float Weight { get; }
        public bool IsInitiallyUnlocked { get; }
        public UnitRewardEntry(UnitDefinition unit, FootprintDefinition block, float weight, bool isInitiallyUnlocked)
        {
            if (!float.IsFinite(weight) || weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));
            Option = new GeneralRewardOption(unit, block);
            Weight = weight;
            IsInitiallyUnlocked = isInitiallyUnlocked;
        }
    }

    /// <summary>계정 해금 저장소가 연결되기 전 사용하는 명시적 기본 해금 목록.</summary>
    public sealed class DefaultUnitRewardUnlocks : IUnitRewardUnlocks
    {
        private readonly HashSet<string> _ids = new HashSet<string>();
        public DefaultUnitRewardUnlocks(IEnumerable<UnitRewardEntry> entries)
        { foreach (var entry in entries) if (entry.IsInitiallyUnlocked) _ids.Add(entry.Option.Unit.Id); }
        public bool IsUnlocked(string unitId) => _ids.Contains(unitId);
    }

    public sealed class GeneralRewardSource
    {
        public const int OPTION_COUNT = 3;
        private readonly List<UnitRewardEntry> _entries;
        private readonly IUnitRewardUnlocks _unlocks;
        private readonly Random _random;
        public GeneralRewardSource(IEnumerable<UnitRewardEntry> entries, IUnitRewardUnlocks unlocks, Random random = null)
        {
            _entries = new List<UnitRewardEntry>(entries ?? throw new ArgumentNullException(nameof(entries)));
            _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            _random = random ?? new Random();
            var ids = new HashSet<string>();
            foreach (var entry in _entries)
                if (entry == null || !ids.Add(entry.Option.Unit.Id)) throw new ArgumentException("Null or duplicate reward unit.");
            ValidateAvailable();
        }
        public void ValidateAvailable()
        {
            if (GetEligible().Count == 0) throw new InvalidOperationException("No unlocked unit with positive reward weight.");
        }
        private List<UnitRewardEntry> GetEligible()
            => _entries.FindAll(entry => entry.Weight > 0 && _unlocks.IsUnlocked(entry.Option.Unit.Id));

        public IReadOnlyList<GeneralRewardOption> Draw(bool canExpand)
        {
            var eligible = GetEligible();
            if (eligible.Count == 0) throw new InvalidOperationException("No unlocked unit with positive reward weight.");
            var remaining = new List<UnitRewardEntry>(eligible);
            var result = new List<GeneralRewardOption>();
            int unitCount = canExpand ? OPTION_COUNT - 1 : OPTION_COUNT;
            for (int i = 0; i < unitCount; i++)
            {
                // 해금 종류를 모두 제시한 뒤에만 중복을 허용한다.
                if (remaining.Count == 0) remaining.AddRange(eligible);
                double total = 0;
                foreach (var entry in remaining) total += entry.Weight;
                double roll = _random.NextDouble() * total;
                int selected = remaining.Count - 1;
                for (int j = 0; j < remaining.Count; j++)
                {
                    roll -= remaining[j].Weight;
                    if (roll < 0) { selected = j; break; }
                }
                result.Add(remaining[selected].Option);
                remaining.RemoveAt(selected);
            }
            if (canExpand) result.Add(GeneralRewardOption.CreateExpansion());
            return result.AsReadOnly();
        }
    }
}
