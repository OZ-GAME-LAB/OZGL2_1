using System;
using UnityEngine;

namespace OZGL2.Stage
{
    [CreateAssetMenu(menuName = "OZGL2/Stage/Hero Pool Catalog")]
    public sealed class HeroPoolCatalogSO : ScriptableObject
    {
        [SerializeField] private HeroPoolEntry[] _entries = Array.Empty<HeroPoolEntry>();
        [SerializeField, Min(1)] private int _dummyDefenderCount = 1;
        public int DummyDefenderCount => _dummyDefenderCount;
        public HeroPoolEntry[] CreateSnapshot()
        {
            var entries = new HeroPoolEntry[_entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                var entry = _entries[index];
                entries[index] = new HeroPoolEntry(entry.HeroId, entry.Prefab, entry.InitialCapacity, entry.GrowthCount, entry.Experience);
            }
            return entries;
        }
    }
    [Serializable]
    public sealed class HeroPoolEntry
    {
        [SerializeField] private string _heroId;
        [SerializeField] private PooledHero _prefab;
        [SerializeField, Min(0)] private int _initialCapacity;
        [SerializeField, Min(1)] private int _growthCount = 1;
        [SerializeField, Min(0)] private int _experience;
        public string HeroId => _heroId;
        public PooledHero Prefab => _prefab;
        public int InitialCapacity => _initialCapacity;
        public int GrowthCount => _growthCount;
        public int Experience => _experience;
        public HeroPoolEntry(string heroId, PooledHero prefab, int initialCapacity, int growthCount, int experience)
        {
            _heroId = heroId; _prefab = prefab; _initialCapacity = initialCapacity;
            _growthCount = growthCount; _experience = experience;
        }
    }
}
