using UnityEngine;

namespace OZGL2.Grid
{
    [CreateAssetMenu(menuName = "OZGL2/Grid/Unit Placement Data")]
    public sealed class GridUnitDataSO : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [UnityEngine.Serialization.FormerlySerializedAs("_requiredBlockId")]
        [SerializeField] private string _rewardBlockId;
        [SerializeField] private BlockShapeSO _footprint;
        public UnitDefinition CreateSnapshot() => new UnitDefinition(_id, _displayName,
            _footprint != null ? _footprint.CreateSnapshot() : throw new System.InvalidOperationException("Unit footprint must be assigned: " + _id), _rewardBlockId);
    }
}
