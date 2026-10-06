using System;
using System.Collections.Generic;
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
        [SerializeField, Min(2)] private int _storageCapacity = 10;
        public GridDefinition CreateSnapshot() => new GridDefinition(_initialSize, _maximumSize,
            new FootprintDefinition(_expansionId, "Floor expansion", _expansionCells), _storageCapacity);
    }
    public sealed class GridDefinition
    {
        public Vector2Int InitialSize { get; }
        public Vector2Int MaximumSize { get; }
        public Vector2Int InitialOrigin { get; }
        public FootprintDefinition Expansion { get; }
        /// <summary>웨이브 보상으로 나올 수 있는 영역 확장 조각 모양들(기본 조각 포함). 보상을 뽑을 때마다 이 중 하나가 나온다.</summary>
        public IReadOnlyList<FootprintDefinition> ExpansionShapes { get; }
        public int StorageCapacity { get; }
        // 셀 중심 좌표계. 마왕은 일반 셀 집합에 추가하지 않는다.
        public Vector2 KingAnchor => new Vector2((MaximumSize.x - 1) / 2f, -1);
        public GridDefinition(Vector2Int initial, Vector2Int maximum, FootprintDefinition expansion, int storageCapacity = 10)
        {
            if (initial.x <= 0 || initial.y <= 0 || maximum.x < initial.x || maximum.y < initial.y ||
                (maximum.x - initial.x) % 2 != 0) throw new ArgumentException("Invalid centered grid dimensions.");
            if (storageCapacity < 2) throw new ArgumentOutOfRangeException(nameof(storageCapacity));
            InitialSize = initial; MaximumSize = maximum; StorageCapacity = storageCapacity;
            InitialOrigin = new Vector2Int((maximum.x - initial.x) / 2, 0);
            Expansion = expansion ?? throw new ArgumentNullException(nameof(expansion));
            ExpansionShapes = BuildExpansionShapes(Expansion);
        }
        private static IReadOnlyList<FootprintDefinition> BuildExpansionShapes(FootprintDefinition basic)
        {
            var shapes = new List<FootprintDefinition> { basic };
            void Add(string id, string name, params Vector2Int[] cells) { if (id != basic.Id) shapes.Add(new FootprintDefinition(id, name, cells)); }
            Add("floor_line3", "Floor line 3", new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2));
            Add("floor_corner3", "Floor corner 3", new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1));
            Add("floor_square4", "Floor square 4", new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1));
            Add("floor_tee4", "Floor tee 4", new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1));
            Add("floor_l4", "Floor L 4", new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 0));
            return shapes.AsReadOnly();
        }
        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < MaximumSize.x && cell.y < MaximumSize.y;
    }
}
