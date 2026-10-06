using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 발견 여부와 표시용 성급/외형만 관리한다. 선택은 실제 유닛의 성급이나 저장 데이터를 변경하지 않는다.
    [DisallowMultipleComponent]
    public sealed class UIUnitCodexCardView : MonoBehaviour
    {
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Image _lockIcon;
        [SerializeField] private Image _frame;
        [SerializeField] private Color _lockedColor = new Color32(100, 98, 94, 255);
        [SerializeField] private Color _lockedStarColor = new Color32(115, 112, 108, 255);
        [SerializeField] private Button _previousAppearanceButton;
        [SerializeField] private Button _nextAppearanceButton;
        [SerializeField] private Image[] _starImages = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text _appearanceText;

        private const int MAX_DEMON_APPEARANCES = 3;
        private UIUnitCatalogSO.Entry _entry;
        private Material _silhouetteMaterial;
        private Sprite _lockSprite;
        private int _appearanceIndex;

        public string EntryId { get; private set; } = string.Empty;
        public bool IsUnlocked { get; private set; }
        public int AppearanceIndex => IsUnlocked ? _appearanceIndex : 0;
        public int AppearanceCount => _entry == null ? 0 : _entry.Faction == eUnitCodexFaction.DEMON
            ? Mathf.Min(MAX_DEMON_APPEARANCES, _entry.AppearanceCount) : _entry.AppearanceCount;

        public void ShowUnit(UIUnitCatalogSO.Entry entry, bool isUnlocked, Material silhouetteMaterial, Sprite lockSprite)
        {
            bool hasEntry = entry != null && !string.IsNullOrWhiteSpace(entry.Id);
            string entryId = hasEntry ? entry.Id : string.Empty;
            if (EntryId != entryId) _appearanceIndex = 0;
            EntryId = entryId;
            _entry = hasEntry ? entry : null;
            _silhouetteMaterial = silhouetteMaterial;
            _lockSprite = lockSprite;
            IsUnlocked = hasEntry && isUnlocked;
            _appearanceIndex = Mathf.Clamp(_appearanceIndex, 0, Mathf.Max(0, AppearanceCount - 1));
            RefreshVisuals();
        }

        public void PreviousAppearance() => ChangeAppearance(-1);

        public void NextAppearance() => ChangeAppearance(1);

        private void ChangeAppearance(int direction)
        {
            if (!IsUnlocked || _entry == null) return;
            int nextIndex = Mathf.Clamp(_appearanceIndex + direction, 0, Mathf.Max(0, AppearanceCount - 1));
            if (nextIndex == _appearanceIndex) return;
            _appearanceIndex = nextIndex;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            bool hasEntry = _entry != null;

            if (_portrait != null)
            {
                // 미발견 상태는 기존 기본 초상의 실루엣을 유지하고, 해금 상태에서만 외형을 전환한다.
                _portrait.sprite = !hasEntry ? null : IsUnlocked ? _entry.GetPortrait(AppearanceIndex) : _entry.Portrait;
                _portrait.material = IsUnlocked ? null : _silhouetteMaterial;
                _portrait.color = IsUnlocked ? Color.white : _lockedColor;
                // 실루엣 Material이 누락된 상태에서는 원본 색상으로 미발견 유닛을 노출하지 않는다.
                _portrait.enabled = _portrait.sprite != null && (IsUnlocked || _silhouetteMaterial != null);
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
            }

            if (_nameText != null)
            {
                _nameText.text = !hasEntry ? string.Empty : IsUnlocked ? _entry.DisplayName : "미발견";
                _nameText.raycastTarget = false;
            }

            if (_lockIcon != null)
            {
                _lockIcon.sprite = _lockSprite;
                _lockIcon.enabled = hasEntry && !IsUnlocked && _lockSprite != null;
                _lockIcon.raycastTarget = false;
            }

            if (_frame != null) _frame.raycastTarget = false;
            RefreshAppearanceControls(hasEntry);
        }

        private void RefreshAppearanceControls(bool hasEntry)
        {
            if (_previousAppearanceButton != null)
                _previousAppearanceButton.interactable = IsUnlocked && _appearanceIndex > 0;
            if (_nextAppearanceButton != null)
                _nextAppearanceButton.interactable = IsUnlocked && _appearanceIndex < AppearanceCount - 1;

            bool isDemon = hasEntry && _entry.Faction == eUnitCodexFaction.DEMON;
            if (_starImages != null)
                for (int index = 0; index < _starImages.Length; index++)
                {
                    Image star = _starImages[index];
                    if (star == null) continue;
                    star.gameObject.SetActive(isDemon && index <= AppearanceIndex && index < MAX_DEMON_APPEARANCES);
                    star.color = IsUnlocked ? Color.white : _lockedStarColor;
                    star.raycastTarget = false;
                }

            if (_appearanceText != null)
            {
                _appearanceText.gameObject.SetActive(hasEntry && !isDemon);
                _appearanceText.text = !hasEntry ? string.Empty : IsUnlocked
                    ? $"외형 {AppearanceIndex + 1} / {AppearanceCount}" : "미발견";
                _appearanceText.raycastTarget = false;
            }
        }
    }
}
