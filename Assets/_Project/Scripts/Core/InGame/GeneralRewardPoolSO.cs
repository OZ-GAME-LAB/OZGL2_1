using System;
using System.Collections.Generic;
using OZGL2.Grid;
using UnityEngine;

namespace OZGL2.InGame
{
    [CreateAssetMenu(menuName = "OZGL2/Rewards/General Reward Pool")]
    public sealed class GeneralRewardPoolSO : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private GridUnitDataSO _unit;
            [SerializeField] private BlockShapeSO _block;
            [SerializeField, Min(0)] private float _weight = 1;
            [SerializeField] private bool _isInitiallyUnlocked;
            public UnitRewardEntry CreateSnapshot()
            {
                if (_unit == null || _block == null) throw new InvalidOperationException("Reward unit and block references are required.");
                return new UnitRewardEntry(_unit.CreateSnapshot(), _block.CreateSnapshot(), _weight, _isInitiallyUnlocked);
            }
        }
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        public IReadOnlyList<UnitRewardEntry> CreateEntries()
        {
            var result = new List<UnitRewardEntry>();
            foreach (var entry in _entries)
                result.Add(entry != null ? entry.CreateSnapshot() : throw new InvalidOperationException("Null reward entry."));
            return result.AsReadOnly();
        }
        public GeneralRewardSource CreateSource(IUnitRewardUnlocks unlocks = null, System.Random random = null)
        {
            var entries = CreateEntries();
            return new GeneralRewardSource(entries, unlocks ?? new DefaultUnitRewardUnlocks(entries), random);
        }
    }
}
