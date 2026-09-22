using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 한 유닛의 발견 여부만 표시한다. 잠금 실루엣은 원본 Sprite의 알파를 그대로 사용한다.
    [DisallowMultipleComponent]
    public sealed class UIUnitCodexCardView : MonoBehaviour
    {
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Image _lockIcon;
        [SerializeField] private Image _frame;
        [SerializeField] private Color _lockedColor = new Color32(100, 98, 94, 255);

        public string EntryId { get; private set; } = string.Empty;
        public bool IsUnlocked { get; private set; }

        public void ShowUnit(UIUnitCatalogSO.Entry entry, bool isUnlocked, Material silhouetteMaterial, Sprite lockSprite)
        {
            bool hasEntry = entry != null && !string.IsNullOrWhiteSpace(entry.Id);
            EntryId = hasEntry ? entry.Id : string.Empty;
            IsUnlocked = hasEntry && isUnlocked;

            if (_portrait != null)
            {
                _portrait.sprite = hasEntry ? entry.Portrait : null;
                _portrait.material = IsUnlocked ? null : silhouetteMaterial;
                _portrait.color = IsUnlocked ? Color.white : _lockedColor;
                // 실루엣 Material이 누락된 상태에서는 원본 색상으로 미발견 유닛을 노출하지 않는다.
                _portrait.enabled = _portrait.sprite != null && (IsUnlocked || silhouetteMaterial != null);
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
            }

            if (_nameText != null)
            {
                _nameText.text = !hasEntry ? string.Empty : IsUnlocked ? entry.DisplayName : "미발견";
                _nameText.raycastTarget = false;
            }

            if (_lockIcon != null)
            {
                _lockIcon.sprite = lockSprite;
                _lockIcon.enabled = hasEntry && !IsUnlocked && lockSprite != null;
                _lockIcon.raycastTarget = false;
            }

            if (_frame != null) _frame.raycastTarget = false;
        }
    }
}
