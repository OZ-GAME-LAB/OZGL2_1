using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 입력받은 진행도만 표시한다. 업적 집계, 저장, 보상 지급은 이 카드의 책임이 아니다.
    [DisallowMultipleComponent]
    public sealed class UIAchievementCardView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _completedText;
        [SerializeField] private Image _progressFill;
        [SerializeField] private Image _frame;
        [SerializeField, Tooltip("선택 연결: 칸별 진행도 이미지입니다. 구분선 이미지는 연결하지 않습니다.")]
        private Image[] _segments;
        [SerializeField] private Color _inProgressColor = new Color32(140, 76, 73, 255);
        [SerializeField] private Color _completedColor = new Color32(220, 204, 172, 255);

        public string EntryId { get; private set; } = string.Empty;
        public int CurrentProgress { get; private set; }
        public int Target { get; private set; } = 1;
        public bool IsCompleted { get; private set; }
        public float ProgressRatio => (float)CurrentProgress / Target;

        public void Bind(UIAchievementCatalogSO.Entry entry, int progress)
        {
            bool hasEntry = entry != null && !string.IsNullOrWhiteSpace(entry.Id);
            EntryId = hasEntry ? entry.Id : string.Empty;
            Target = hasEntry ? entry.Target : 1;
            CurrentProgress = hasEntry ? Mathf.Clamp(progress, 0, Target) : 0;
            IsCompleted = hasEntry && CurrentProgress >= Target;
            Color stateColor = IsCompleted ? _completedColor : _inProgressColor;

            if (_icon != null)
            {
                _icon.sprite = hasEntry ? entry.Icon : null;
                _icon.enabled = _icon.sprite != null;
                _icon.preserveAspect = true;
                _icon.raycastTarget = false;
            }

            SetText(_nameText, hasEntry ? entry.DisplayName : string.Empty);
            SetText(_descriptionText, hasEntry ? entry.Description : string.Empty);
            SetText(_progressText, hasEntry ? $"{CurrentProgress} / {Target}" : string.Empty);
            SetText(_completedText, !hasEntry ? string.Empty : IsCompleted ? "달성" : "진행 중");
            if (_completedText != null) _completedText.color = stateColor;

            SetFill(_progressFill, ProgressRatio, stateColor);
            if (_frame != null)
            {
                _frame.color = stateColor;
                _frame.raycastTarget = false;
            }

            if (_segments != null)
                for (int index = 0; index < _segments.Length; index++)
                    SetFill(_segments[index], Mathf.Clamp01(ProgressRatio * _segments.Length - index), stateColor);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label == null) return;
            label.text = value;
            label.raycastTarget = false;
        }

        private static void SetFill(Image image, float amount, Color color)
        {
            if (image == null) return;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = Mathf.Clamp01(amount);
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
