using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.UIFlow
{
    public enum eSkillPreviewCategory { DAMAGE, BUFF, DEBUFF }

    // 아트 시안용 고정 표시 데이터만 보관한다. 해금/장착/저장 상태는 SO에 기록하지 않는다.
    [CreateAssetMenu(menuName = "OZGL2/UI/Skill Preview Catalog")]
    public sealed class UISkillPreviewCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _id;
            [SerializeField] private string _displayName;
            [SerializeField] private eSkillPreviewCategory _category;
            [SerializeField] private Sprite _icon;
            [SerializeField] private bool _isArcane;
            [SerializeField] private string _effectLabel = "피해";
            [SerializeField] private string _effectValue = "120";
            [SerializeField] private string _cooldown = "8초";
            public string Id => _id;
            public string DisplayName => _displayName;
            public eSkillPreviewCategory Category => _category;
            public Sprite Icon => _icon;
            public bool IsArcane => _isArcane;
            public string EffectLabel => _effectLabel;
            public string EffectValue => _effectValue;
            public string Cooldown => _cooldown;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        public IReadOnlyList<Entry> Entries => _entries;
    }
}
