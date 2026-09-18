using UnityEngine;

namespace OZGL2.Grid
{
    [CreateAssetMenu(menuName = "OZGL2/Grid/Block Shape")]
    public sealed class BlockShapeSO : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private Vector2Int[] _cells;
        public FootprintDefinition CreateSnapshot() => new FootprintDefinition(_id, _displayName, _cells);
    }
}
