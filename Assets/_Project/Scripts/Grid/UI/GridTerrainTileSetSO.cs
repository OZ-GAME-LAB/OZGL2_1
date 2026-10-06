using UnityEngine;

namespace OZGL2.Grid.UI
{
    [CreateAssetMenu(menuName = "OZGL2/Grid/Terrain Tile Set")]
    public sealed class GridTerrainTileSetSO : ScriptableObject
    {
        [SerializeField] private Sprite[] _earthVariants;
        [SerializeField] private Sprite[] _earthEdges;
        [SerializeField] private Sprite[] _corruptedVariants;
        [SerializeField] private Sprite[] _corruptionEdges;
        [SerializeField] private Color _preparationGuide = new Color(0.8f, 0.75f, 0.58f, 0.18f);
        [SerializeField, Range(0f, 0.1f)] private float _guideWidth = 0.015625f;
        [SerializeField] private Color _hoverBorder = new Color(0.95f, 0.88f, 0.65f, 0.85f);
        [SerializeField, Range(0f, 0.1f)] private float _hoverBorderWidth = 0.03125f;
        [SerializeField] private Color _occupiedFill = new Color(1f, 0.12f, 0.18f, 0.055f);
        [SerializeField] private Color _occupiedBorder = new Color(1f, 0.22f, 0.25f, 0.5f);
        [SerializeField, Range(0f, 0.1f)] private float _occupiedBorderWidth = 0.015625f;
        public Color PreparationGuide => _preparationGuide;
        public float GuideWidth => _guideWidth;
        public Color HoverBorder => _hoverBorder;
        public float HoverBorderWidth => _hoverBorderWidth;
        public Color OccupiedFill => _occupiedFill;
        public Color OccupiedBorder => _occupiedBorder;
        public float OccupiedBorderWidth => _occupiedBorderWidth;

        public Sprite GetGround(GridManager grid, Vector2Int cell)
        {
            if (!grid.HasFloor(cell)) return null;
            int mask = GridTerrainPattern.GetMask(cell, grid.HasFloor);
            return mask == GridTerrainPattern.FULL_MASK ? GridTerrainPattern.GetVariant(_earthVariants, cell)
                : GridTerrainPattern.GetConnected(_earthEdges, mask);
        }

        public Sprite GetCorruption(GridManager grid, Vector2Int cell)
        {
            if (grid.GetBlockAt(cell) == null) return null;
            int mask = GridTerrainPattern.GetMask(cell, neighbor => grid.GetBlockAt(neighbor) != null);
            return mask == GridTerrainPattern.FULL_MASK ? GridTerrainPattern.GetVariant(_corruptedVariants, cell)
                : GridTerrainPattern.GetConnected(_corruptionEdges, mask);
        }
    }
}
