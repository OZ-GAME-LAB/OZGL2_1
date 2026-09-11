using System;
using UnityEngine;

namespace OZGL2.Grid
{
    [CreateAssetMenu(menuName = "OZGL2/Grid/Settings")]
    public sealed class GridSettingsSO : ScriptableObject
    {
        [SerializeField] private Vector2Int _initialSize;
        [SerializeField] private Vector2Int _maximumSize;
        [SerializeField] private string _expansionId;
        [SerializeField] private Vector2Int[] _expansionCells;
        public GridDefinition CreateSnapshot() => new GridDefinition(_initialSize, _maximumSize,
            new FootprintDefinition(_expansionId, "Floor expansion", _expansionCells));
    }
    public sealed class GridDefinition
    {
        public Vector2Int InitialSize { get; }
        public Vector2Int MaximumSize { get; }
        public Vector2Int InitialOrigin { get; }
        public FootprintDefinition Expansion { get; }
        // 셀 중심 좌표계. 마왕은 일반 셀 집합에 추가하지 않는다.
        public Vector2 KingAnchor => new Vector2((MaximumSize.x - 1) / 2f, -1);
        public GridDefinition(Vector2Int initial, Vector2Int maximum, FootprintDefinition expansion)
        {
            if (initial.x <= 0 || initial.y <= 0 || maximum.x < initial.x || maximum.y < initial.y ||
                (maximum.x - initial.x) % 2 != 0) throw new ArgumentException("Invalid centered grid dimensions.");
            InitialSize = initial; MaximumSize = maximum;
            InitialOrigin = new Vector2Int((maximum.x - initial.x) / 2, 0);
            Expansion = expansion ?? throw new ArgumentNullException(nameof(expansion));
        }
        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < MaximumSize.x && cell.y < MaximumSize.y;
    }
}
