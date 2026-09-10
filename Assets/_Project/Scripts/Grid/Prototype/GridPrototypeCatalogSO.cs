using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Grid.Prototype
{
    [CreateAssetMenu(menuName = "OZGL2/Grid/Prototype Catalog")]
    public sealed class GridPrototypeCatalogSO : ScriptableObject
    {
        [SerializeField] private GridSettingsSO _settings;
        [SerializeField] private BlockShapeSO[] _footprints;
        [SerializeField] private GridUnitDataSO[] _units;
        public GridDefinition CreateDefinition() => _settings.CreateSnapshot();
        public IReadOnlyList<FootprintDefinition> CreateBlocks()
        {
            var result = new List<FootprintDefinition>();
            foreach (var footprint in _footprints) result.Add(footprint.CreateSnapshot());
            return result.AsReadOnly();
        }
        public IReadOnlyList<UnitDefinition> CreateUnits()
        {
            var result = new List<UnitDefinition>();
            foreach (var unit in _units) result.Add(unit.CreateSnapshot());
            return result.AsReadOnly();
        }
    }
}
