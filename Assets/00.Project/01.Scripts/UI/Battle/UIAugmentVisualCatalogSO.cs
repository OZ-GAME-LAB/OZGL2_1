using System;
using UnityEngine;

namespace OZGL2.UIFlow
{
    // 증강의 효과와 추첨 규칙은 AugmentData/AugmentRun에 두고 표시 리소스만 관리한다.
    [CreateAssetMenu(menuName = "OZGL2/UI/Augment Visual Catalog")]
    public sealed class UIAugmentVisualCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class TierVisual
        {
            [SerializeField, Range(1, 3)] private int _tier = 1;
            [SerializeField] private Sprite _velvetBackground;
            [SerializeField] private Sprite _descriptionPanel;
            [SerializeField] private Sprite _crest;
            [SerializeField] private Color _gradeColor = Color.white;
            [SerializeField] private float _gradeLabelY = -122f;

            public int Tier => _tier;
            public Sprite VelvetBackground => _velvetBackground;
            public Sprite DescriptionPanel => _descriptionPanel;
            public Sprite Crest => _crest;
            public Color GradeColor => _gradeColor;
            // 512x512 문장 원본의 중심을 기준으로 한 라벨 중심 Y다.
            public float GradeLabelY => _gradeLabelY;

            public TierVisual(int tier, Sprite velvetBackground, Sprite descriptionPanel,
                Sprite crest, Color gradeColor, float gradeLabelY)
            {
                _tier = Mathf.Clamp(tier, 1, 3);
                _velvetBackground = velvetBackground;
                _descriptionPanel = descriptionPanel;
                _crest = crest;
                _gradeColor = gradeColor;
                _gradeLabelY = gradeLabelY;
            }
        }

        [Serializable]
        public sealed class IconEntry
        {
            [SerializeField] private string _augmentId;
            [SerializeField] private Sprite _icon;

            public string AugmentId => _augmentId;
            public Sprite Icon => _icon;

            public IconEntry(string augmentId, Sprite icon)
            {
                _augmentId = augmentId;
                _icon = icon;
            }
        }

        [SerializeField] private TierVisual[] _tiers = Array.Empty<TierVisual>();
        [SerializeField] private IconEntry[] _icons = Array.Empty<IconEntry>();
        [SerializeField] private Sprite _defaultIcon;

        public void Configure(TierVisual[] tiers, IconEntry[] icons, Sprite defaultIcon)
        {
            _tiers = tiers ?? Array.Empty<TierVisual>();
            _icons = icons ?? Array.Empty<IconEntry>();
            _defaultIcon = defaultIcon;
        }

        public TierVisual GetTierVisual(int tier)
        {
            if (_tiers == null) return null;
            foreach (TierVisual visual in _tiers)
                if (visual != null && visual.Tier == tier) return visual;
            return null;
        }

        public Sprite GetIcon(string augmentId)
        {
            if (!string.IsNullOrEmpty(augmentId) && _icons != null)
            {
                foreach (IconEntry entry in _icons)
                    if (entry != null && entry.AugmentId == augmentId && entry.Icon != null)
                        return entry.Icon;
            }
            return _defaultIcon;
        }
    }
}
