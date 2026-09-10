using UnityEngine;

namespace OZGL2.Grid
{
    [CreateAssetMenu(menuName = "OZGL2/Grid/Unit Placement Data")]
    public sealed class GridUnitDataSO : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private string _requiredBlockId;
        public UnitDefinition CreateSnapshot() => new UnitDefinition(_id, _displayName, _requiredBlockId);
    }
}
