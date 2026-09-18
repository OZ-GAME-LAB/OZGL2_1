using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    [DisallowMultipleComponent]
    public sealed class UITraitTooltipView : MonoBehaviour
    {
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _categoryText;
        [SerializeField] private Text _descriptionText;
        [SerializeField] private Text _rankText;
        [SerializeField] private Text _currentEffectText;
        [SerializeField] private Text _nextEffectText;

        public void ShowTrait(UITraitDisplayData data)
        {
            if (_titleText != null) _titleText.text = data.DisplayName;
            if (_categoryText != null) _categoryText.text = data.Category;
            if (_descriptionText != null) _descriptionText.text = data.Description;
            if (_rankText != null) _rankText.text = $"{data.CurrentRank}/{data.MaxRank}";
            if (_currentEffectText != null) _currentEffectText.text = $"현재: {data.CurrentEffect}";
            if (_nextEffectText != null)
            {
                if (!data.IsUnlocked) _nextEffectText.text = $"잠금: {data.UnavailableReason}\n다음: {data.NextEffect}";
                else if (data.CurrentRank >= data.MaxRank) _nextEffectText.text = "최대 단계";
                else _nextEffectText.text = $"다음: {data.NextEffect}" +
                    (data.CanUpgrade ? string.Empty : $"\n{data.UnavailableReason}");
            }
        }
    }
}
