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

        [Serializable]
        public sealed class CampLayout
        {
            [SerializeField] private string[] _stageIds = Array.Empty<string>();
            [SerializeField, Min(1)] private int _tentCount = 1;
            public int TentCount => _tentCount;
            public bool Matches(string stageId) => !string.IsNullOrEmpty(stageId) &&
                Array.Exists(_stageIds, id => string.Equals(id, stageId, StringComparison.Ordinal));
        }

        [SerializeField] private Sprite[] _grassVariants;
        [SerializeField] private Sprite[] _sparseGrassVariants;
        [SerializeField, Min(1f)] private float _grassPatchSize = 6f;
        [SerializeField, Range(0f, 1f)] private float _denseGrassThreshold = 0.55f;
        [SerializeField] private Decoration[] _decorations = Array.Empty<Decoration>();
        [Header("Difficulty camps (cell coordinates)")]
        [SerializeField] private Decoration _campTent;
        [SerializeField] private Decoration[] _campProps = Array.Empty<Decoration>();
        [SerializeField] private CampLayout[] _campLayouts = Array.Empty<CampLayout>();
        [SerializeField, Min(1)] private int _defaultTentCount = 1;
        [SerializeField] private float _campVerticalOffset = 1.5f;
        [SerializeField, Min(0.1f)] private float _tentSpacing = 4.5f;
        [Tooltip("연속된 원화를 32x32로 나눈 투명 지면 흔적. 왼쪽 아래부터 행 순서로 연결합니다.")]
        [SerializeField] private Sprite[] _approachTiles = Array.Empty<Sprite>();
        [SerializeField] private Vector2Int _approachOrigin;
        [SerializeField, Min(1)] private int _approachColumns = 1;
        [SerializeField, Range(0f, 1f)] private float _approachOpacity = 1f;
        [SerializeField, Min(1)] private int _coverageMargin = 2;
        [SerializeField, Min(1)] private int _maximumTiles = 6000;
        public Decoration[] Decorations => _decorations;
        public Decoration CampTent => _campTent;
        public Decoration[] CampProps => _campProps;
        public Vector2 CampOffset => Vector2.up * _campVerticalOffset;
        public int GetTentCount(string stageId)
        {
            foreach (var layout in _campLayouts)
                if (layout != null && layout.Matches(stageId)) return ValidateTentCount(layout.TentCount);
            return ValidateTentCount(_defaultTentCount);
        }
        public Vector2 GetTentCell(int index, int count)
        {
            ValidateTentCount(count);
            if (_campTent == null || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            if (!float.IsFinite(_tentSpacing) || _tentSpacing <= 0 || !float.IsFinite(_campVerticalOffset))
                throw new InvalidOperationException("Camp spacing and offset must be finite; spacing must be positive.");
            // 중앙 천막을 고정하고 양쪽에 같은 수를 추가한다.
            return _campTent.CellPosition + CampOffset + Vector2.right * ((index - count / 2) * _tentSpacing);
        }
        private static int ValidateTentCount(int count)
        {
            if (count < 1 || count % 2 == 0) throw new InvalidOperationException("Camp tent count must be a positive odd number.");
            return count;
        }
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
