using UnityEngine;

namespace OZGL2.UIFlow
{
    // 표시 규칙만 공유한다. 스킬 효과/장착/해금 상태는 저장하지 않는다.
    [CreateAssetMenu(menuName = "OZGL2/UI/Skill Category Style")]
    public sealed class UISkillCategoryStyleSO : ScriptableObject
    {
        private static readonly int OUTLINE_COLOR = Shader.PropertyToID("_OutlineColor");
        [SerializeField] private Material _damageIcon;
        [SerializeField] private Material _buffIcon;
        [SerializeField] private Material _debuffIcon;
        [SerializeField] private Material _slotTintMaterial;
        [SerializeField] private Material _emptyFrameMaterial;
        [SerializeField] private Sprite _damageFrame;
        [SerializeField] private Sprite _buffFrame;
        [SerializeField] private Sprite _debuffFrame;
        // x/y = 원본 슬롯 대비 표시 크기, z/w = 슬롯 크기 대비 중심 보정.
        [SerializeField] private Vector4 _damageFrameLayout = new Vector4(1, 1, 0, 0);
        [SerializeField] private Vector4 _buffFrameLayout = new Vector4(1, 1, 0, 0);
        [SerializeField] private Vector4 _debuffFrameLayout = new Vector4(1, 1, 0, 0);
        [SerializeField, Range(0f, 0.4f)] private float _slotOpacity = 0.14f;

        public Material SlotTintMaterial => _slotTintMaterial;
        public Material EmptyFrameMaterial => _emptyFrameMaterial;

        public Vector4 GetEquippedFrameLayout(eSkillPreviewCategory category)
        {
            switch (category)
            {
                case eSkillPreviewCategory.DAMAGE: return _damageFrameLayout;
                case eSkillPreviewCategory.BUFF: return _buffFrameLayout;
                case eSkillPreviewCategory.DEBUFF: return _debuffFrameLayout;
                default: return new Vector4(1, 1, 0, 0);
            }
        }

        public Sprite GetEquippedFrame(eSkillPreviewCategory category)
        {
            switch (category)
            {
                case eSkillPreviewCategory.DAMAGE: return _damageFrame;
                case eSkillPreviewCategory.BUFF: return _buffFrame;
                case eSkillPreviewCategory.DEBUFF: return _debuffFrame;
                default: return null;
            }
        }

        public Material GetIconMaterial(eSkillPreviewCategory category)
        {
            switch (category)
            {
                case eSkillPreviewCategory.DAMAGE: return _damageIcon;
                case eSkillPreviewCategory.BUFF: return _buffIcon;
                case eSkillPreviewCategory.DEBUFF: return _debuffIcon;
                default: return null;
            }
        }

        public Color GetSlotColor(eSkillPreviewCategory category)
        {
            Material material = GetIconMaterial(category);
            if (material == null || !material.HasProperty(OUTLINE_COLOR)) return Color.clear;
            Color color = material.GetColor(OUTLINE_COLOR);
            color.a = _slotOpacity;
            return color;
        }
    }
}
