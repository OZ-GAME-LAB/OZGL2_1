using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 전달된 값만 표시한다. 전투 데이터나 선택 상태를 소유하지 않는다.
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Battle Preparation Card View")]
    public sealed class UIBattlePreparationCardView : MonoBehaviour
    {
        [Header("공통 문구")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _rankText;

        [Header("특성 / 보유 스킬")]
        [SerializeField] private Text _traitTitleText;
        [SerializeField] private Text _traitDescriptionText;
        [SerializeField] private Text _skillTitleText;
        [SerializeField] private Text _skillDescriptionText;

        [Header("공격 / 방어 / 체력")]
        [SerializeField] private Text _attackLabelText;
        [SerializeField] private Text _attackValueText;
        [SerializeField] private Text _defenseLabelText;
        [SerializeField] private Text _defenseValueText;
        [SerializeField] private Text _healthLabelText;
        [SerializeField] private Text _healthValueText;

        [Header("영역 설명")]
        [SerializeField] private Text _areaTitleText;
        [SerializeField] private Text _areaDescriptionText;

        [Header("독립 아이콘 / 삽화")]
        [SerializeField] private Image _typeIcon;
        [SerializeField] private Image _artwork;

        [Header("점유 칸 미리보기")]
        [Tooltip("격자가 배치된 RectTransform입니다.")]
        [SerializeField] private RectTransform _footprintGridRoot;
        [Tooltip("표시할 가로/세로 칸 수입니다. 크기를 늘리려면 해당 이름의 격자 자식 오브젝트가 필요합니다.")]
        [SerializeField] private Vector2Int _footprintGridSize = new Vector2Int(5, 5);
        [Tooltip("격자 한 칸의 UI 크기입니다.")]
        [SerializeField, Min(1f)] private float _footprintCellSize = 76f;
        [Tooltip("격자선의 UI 굵기입니다.")]
        [SerializeField, Min(0.5f)] private float _footprintGridLineThickness = 4f;
        [SerializeField, HideInInspector] private Image[] _footprintCells;

        internal Sprite Artwork => _artwork != null ? _artwork.sprite : null;
        internal Sprite TypeIcon => _typeIcon != null ? _typeIcon.sprite : null;

        public void SetTitle(string title) => SetText(_titleText, title);

        public void SetRank(int rank) => SetRankText(rank.ToString(CultureInfo.InvariantCulture));

        public void SetRankText(string rank) => SetText(_rankText, rank);

        public void SetTrait(string title, string description)
        {
            SetText(_traitTitleText, title);
            SetText(_traitDescriptionText, description);
        }

        public void SetSkill(string title, string description)
        {
            SetText(_skillTitleText, title);
            SetText(_skillDescriptionText, description);
        }

        public void SetStats(string attack, string defense, string health)
        {
            SetText(_attackValueText, attack);
            SetText(_defenseValueText, defense);
            SetText(_healthValueText, health);
        }

        public void SetAreaDescription(string title, string description)
        {
            SetText(_areaTitleText, title);
            SetText(_areaDescriptionText, description);
        }

        public void SetArtwork(Sprite artwork) => SetSprite(_artwork, artwork);

        public void SetTypeIcon(Sprite typeIcon) => SetSprite(_typeIcon, typeIcon);

        /// <summary>
        /// Inspector의 격자 설정을 기존 FootprintGrid 자식에 적용한다.
        /// Prefab Mode에서는 전용 Inspector 버튼을 통해 Undo와 함께 호출한다.
        /// </summary>
        public bool ApplyFootprintGridLayout()
        {
            _footprintGridSize.x = Mathf.Max(1, _footprintGridSize.x);
            _footprintGridSize.y = Mathf.Max(1, _footprintGridSize.y);
            _footprintCellSize = Mathf.Max(1f, _footprintCellSize);
            _footprintGridLineThickness = Mathf.Max(0.5f, _footprintGridLineThickness);

            if (_footprintGridRoot == null)
                _footprintGridRoot = transform.Find("FootprintGrid") as RectTransform;
            if (_footprintGridRoot == null)
            {
                Debug.LogWarning("FootprintGrid RectTransform이 없어 격자 레이아웃을 적용하지 못했습니다.", this);
                return false;
            }

            int width = _footprintGridSize.x;
            int height = _footprintGridSize.y;
            for (int column = 0; column <= width; column++)
                if (_footprintGridRoot.Find("ColumnLine_" + column) == null)
                    return WarnMissingGridObject("ColumnLine_" + column);
            for (int row = 0; row <= height; row++)
                if (_footprintGridRoot.Find("RowLine_" + row) == null)
                    return WarnMissingGridObject("RowLine_" + row);
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                    if (_footprintGridRoot.Find("Cell_" + column + "_" + row) == null)
                        return WarnMissingGridObject("Cell_" + column + "_" + row);

            float previousWidth = Mathf.Abs(_footprintGridRoot.rect.width);
            if (previousWidth <= 0f) previousWidth = Mathf.Abs(_footprintGridRoot.sizeDelta.x);
            float centerX = _footprintGridRoot.anchoredPosition.x +
                            (0.5f - _footprintGridRoot.pivot.x) * previousWidth;
            float gridWidth = width * _footprintCellSize;
            float gridHeight = height * _footprintCellSize;
            Vector2 gridPosition = _footprintGridRoot.anchoredPosition;
            gridPosition.x = centerX - (0.5f - _footprintGridRoot.pivot.x) * gridWidth;
            _footprintGridRoot.anchoredPosition = gridPosition;
            _footprintGridRoot.sizeDelta = new Vector2(gridWidth, gridHeight);

            foreach (Transform child in _footprintGridRoot)
            {
                if (TryParseIndex(child.name, "ColumnLine_", out int column))
                    child.gameObject.SetActive(column <= width);
                else if (TryParseIndex(child.name, "RowLine_", out int row))
                    child.gameObject.SetActive(row <= height);
                else if (TryParseCell(child.name, out int cellX, out int cellY) &&
                         (cellX >= width || cellY >= height))
                    child.gameObject.SetActive(false);
            }

            for (int column = 0; column <= width; column++)
            {
                RectTransform line = (RectTransform)_footprintGridRoot.Find("ColumnLine_" + column);
                line.gameObject.SetActive(true);
                SetTopLeftRect(
                    line,
                    column * _footprintCellSize - _footprintGridLineThickness * 0.5f,
                    0f,
                    _footprintGridLineThickness,
                    gridHeight);
            }

            for (int row = 0; row <= height; row++)
            {
                RectTransform line = (RectTransform)_footprintGridRoot.Find("RowLine_" + row);
                line.gameObject.SetActive(true);
                SetTopLeftRect(
                    line,
                    0f,
                    row * _footprintCellSize - _footprintGridLineThickness * 0.5f,
                    gridWidth,
                    _footprintGridLineThickness);
            }

            Image[] cells = new Image[width * height];
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    RectTransform cell =
                        (RectTransform)_footprintGridRoot.Find("Cell_" + column + "_" + row);
                    SetTopLeftRect(
                        cell,
                        column * _footprintCellSize,
                        row * _footprintCellSize,
                        _footprintCellSize,
                        _footprintCellSize);
                    ApplyCellBorderLayout(cell);
                    cells[row * width + column] = cell.GetComponent<Image>();
                }
            }

            _footprintCells = cells;
            return true;
        }

        private void ApplyCellBorderLayout(RectTransform cell)
        {
            const float BORDER_THICKNESS = 2f;
            SetTopLeftRect(cell.Find("TopBorder") as RectTransform,
                0f, 0f, _footprintCellSize, BORDER_THICKNESS);
            SetTopLeftRect(cell.Find("BottomBorder") as RectTransform,
                0f, _footprintCellSize - BORDER_THICKNESS, _footprintCellSize, BORDER_THICKNESS);
            SetTopLeftRect(cell.Find("LeftBorder") as RectTransform,
                0f, BORDER_THICKNESS, BORDER_THICKNESS, _footprintCellSize - BORDER_THICKNESS * 2f);
            SetTopLeftRect(cell.Find("RightBorder") as RectTransform,
                _footprintCellSize - BORDER_THICKNESS,
                BORDER_THICKNESS,
                BORDER_THICKNESS,
                _footprintCellSize - BORDER_THICKNESS * 2f);
        }

        public void SetFootprint(Vector2Int[] cells)
        {
            if (_footprintCells == null) return;

            foreach (Image cell in _footprintCells)
            {
                if (cell != null && cell.gameObject.activeSelf) cell.gameObject.SetActive(false);
            }

            int width = _footprintGridSize.x;
            int height = _footprintGridSize.y;
            if (cells == null || width <= 0 || height <= 0) return;

            foreach (Vector2Int position in cells)
            {
                if (position.x < 0 || position.y < 0 || position.x >= width || position.y >= height) continue;

                // 좌상단이 (0, 0)이며 y는 위에서 아래로 증가한다. 배열 순서는 y * width + x이다.
                long index = (long)position.y * width + position.x;
                if (index >= _footprintCells.Length) continue;

                Image cell = _footprintCells[(int)index];
                // 같은 좌표가 여러 번 전달되어도 활성 상태는 한 번만 바꾼다.
                if (cell != null && !cell.gameObject.activeSelf) cell.gameObject.SetActive(true);
            }
        }

        private static void SetText(Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }

        private static void SetSprite(Image target, Sprite sprite)
        {
            if (target == null) return;
            target.sprite = sprite;
            target.enabled = sprite != null;
        }

        private bool WarnMissingGridObject(string objectName)
        {
            Debug.LogWarning(
                $"FootprintGrid에 {objectName} 오브젝트가 없어 요청한 격자 크기를 적용하지 못했습니다.",
                this);
            return false;
        }

        private static bool TryParseIndex(string value, string prefix, out int index)
        {
            index = -1;
            return value.StartsWith(prefix) && int.TryParse(value.Substring(prefix.Length), out index);
        }

        private static bool TryParseCell(string value, out int column, out int row)
        {
            column = -1;
            row = -1;
            if (!value.StartsWith("Cell_")) return false;
            string[] parts = value.Substring(5).Split('_');
            return parts.Length == 2 && int.TryParse(parts[0], out column) && int.TryParse(parts[1], out row);
        }

        private static void SetTopLeftRect(
            RectTransform target,
            float x,
            float y,
            float width,
            float height)
        {
            if (target == null) return;
            target.anchorMin = target.anchorMax = new Vector2(0f, 1f);
            target.pivot = new Vector2(0f, 1f);
            target.anchoredPosition = new Vector2(x, -y);
            target.sizeDelta = new Vector2(width, height);
        }

        private void OnValidate()
        {
            _footprintGridSize.x = Mathf.Max(1, _footprintGridSize.x);
            _footprintGridSize.y = Mathf.Max(1, _footprintGridSize.y);
            _footprintCellSize = Mathf.Max(1f, _footprintCellSize);
            _footprintGridLineThickness = Mathf.Max(0.5f, _footprintGridLineThickness);
        }
    }
}
