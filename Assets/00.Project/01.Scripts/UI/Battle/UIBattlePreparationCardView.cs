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
        [SerializeField] private Text _categoryText;
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
        [SerializeField] private Vector2Int _footprintGridSize = new Vector2Int(6, 5);
        [SerializeField] private Image[] _footprintCells;

        public void SetTitle(string title) => SetText(_titleText, title);

        public void SetCategory(string category) => SetText(_categoryText, category);

        public void SetRank(int rank) => SetText(_rankText, rank.ToString(CultureInfo.InvariantCulture));

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
    }
}
