using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.UI
{
    public interface IGridBoardSurface
    {
        VisualElement Element { get; }
        Vector2Int PanelToCell(Vector2 position);
        void Render();
    }
}
