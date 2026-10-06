using System;
using OZGL2.Grid.UI;
using UnityEngine;

namespace OZGL2.InGame
{
    [CreateAssetMenu(menuName = "OZGL2/InGame/Outdoor Battlefield")]
    public sealed class OutdoorBattlefieldConfigSO : ScriptableObject
    {
        [Serializable]
        public sealed class Decoration
        {
            [SerializeField] private Sprite _sprite;
            [SerializeField] private Vector2 _cellPosition;
            [SerializeField, Min(0.1f)] private float _scale = 1f;
            [SerializeField, Range(0f, 1f)] private float _opacity = 1f;
            public Sprite Sprite => _sprite;
            public Vector2 CellPosition => _cellPosition;
            public float Scale => _scale;
            public float Opacity => _opacity;
        }

        [SerializeField] private Sprite[] _grassVariants;
        [SerializeField] private Sprite[] _sparseGrassVariants;
        [SerializeField, Min(1f)] private float _grassPatchSize = 6f;
        [SerializeField, Range(0f, 1f)] private float _denseGrassThreshold = 0.55f;
        [SerializeField] private Decoration[] _decorations = Array.Empty<Decoration>();
        [Tooltip("연속된 원화를 32x32로 나눈 투명 지면 흔적. 왼쪽 아래부터 행 순서로 연결합니다.")]
        [SerializeField] private Sprite[] _approachTiles = Array.Empty<Sprite>();
        [SerializeField] private Vector2Int _approachOrigin;
        [SerializeField, Min(1)] private int _approachColumns = 1;
        [SerializeField, Range(0f, 1f)] private float _approachOpacity = 1f;
        [SerializeField, Min(1)] private int _coverageMargin = 2;
        [SerializeField, Min(1)] private int _maximumTiles = 6000;
        public Decoration[] Decorations => _decorations;
        public Sprite[] ApproachTiles => _approachTiles;
        public float ApproachOpacity => _approachOpacity;
        public int CoverageMargin => _coverageMargin;
        public int MaximumTiles => _maximumTiles;
        public Sprite GetGrass(Vector2Int cell)
        {
            // 좌표로 고정한 풀 군락을 사용해 카메라 이동으로 타일이 다시 생성돼도 밀도가 바뀌지 않는다.
            float patchSize = Mathf.Max(1f, _grassPatchSize);
            bool hasDenseGrass = Mathf.PerlinNoise(cell.x / patchSize, cell.y / patchSize) >= _denseGrassThreshold;
            var sparse = !hasDenseGrass ? GridTerrainPattern.GetVariant(_sparseGrassVariants, cell) : null;
            return sparse != null ? sparse : GridTerrainPattern.GetVariant(_grassVariants, cell);
        }
        public Vector2Int GetApproachCell(int index)
        {
            int columns = Mathf.Max(1, _approachColumns);
            return _approachOrigin + new Vector2Int(index % columns, index / columns);
        }
    }
}
