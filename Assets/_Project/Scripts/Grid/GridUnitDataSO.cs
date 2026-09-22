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
        [Tooltip("2성 발판. 비워두면 1성 발판을 그대로 쓴다.")]
        [SerializeField] private BlockShapeSO _footprintStar2;
        [Tooltip("3성 발판. 비워두면 1성 발판을 그대로 쓴다.")]
        [SerializeField] private BlockShapeSO _footprintStar3;
        public UnitDefinition CreateSnapshot() => new UnitDefinition(_id, _displayName,
            _footprint != null ? _footprint.CreateSnapshot() : throw new System.InvalidOperationException("Unit footprint must be assigned: " + _id),
            _rewardBlockId,
            _footprintStar2 != null ? _footprintStar2.CreateSnapshot() : null,
            _footprintStar3 != null ? _footprintStar3.CreateSnapshot() : null);
    }
}
