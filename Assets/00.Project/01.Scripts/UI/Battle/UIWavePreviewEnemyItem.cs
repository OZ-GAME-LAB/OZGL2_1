using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>웨이브의 한 유닛 종류에 대한 아이콘, 이름, 합산 수량만 표시한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIWavePreviewEnemyItem : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _countText;

        public void SetEnemy(string displayName, int count, Sprite portrait)
        {
            if (_nameText != null) _nameText.text = displayName ?? string.Empty;
            if (_countText != null) _countText.text = "×" + count;
            if (_icon == null) return;
            _icon.sprite = portrait;
            _icon.enabled = portrait != null;
            _icon.preserveAspect = true;
        }
    }
}
