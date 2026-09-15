using UnityEngine;

namespace OZGL2.Grid.UI
{
    public enum eGridCellSurface { UNCLAIMED, AVAILABLE, PLATFORM }

    [CreateAssetMenu(menuName = "OZGL2/Grid/Board Theme")]
    public sealed class GridBoardThemeSO : ScriptableObject
    {
        [SerializeField] private Sprite _unclaimed;
        [SerializeField] private Sprite _available;
        [SerializeField] private Sprite _platform;
        [SerializeField, ColorUsage(false, true)] private Color _unclaimedTint = Color.white;
        [SerializeField] private Color _availableTint = Color.white;
        [SerializeField] private Color _platformTint = Color.white;
        [SerializeField] private Color _platformBorder = new Color(0.38f, 0.30f, 0.48f);
        [SerializeField] private Color _unclaimedUIOverlay = new Color(0.72f, 0.68f, 0.58f, 0.35f);
        public Color PlatformBorder => _platformBorder;
        public Color UnclaimedUIOverlay => _unclaimedUIOverlay;
        public Sprite GetSprite(eGridCellSurface surface) => surface == eGridCellSurface.UNCLAIMED ? _unclaimed :
            surface == eGridCellSurface.AVAILABLE ? _available : _platform;
        public Color GetTint(eGridCellSurface surface) => surface == eGridCellSurface.UNCLAIMED ? _unclaimedTint :
            surface == eGridCellSurface.AVAILABLE ? _availableTint : _platformTint;
        public static eGridCellSurface GetSurface(GridManager grid, Vector2Int cell)
            => !grid.HasFloor(cell) ? eGridCellSurface.UNCLAIMED :
                grid.GetBlockAt(cell) == null ? eGridCellSurface.AVAILABLE : eGridCellSurface.PLATFORM;
    }
}
