using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.UIFlow
{
    // 업적의 고정 표시 정보만 보관한다. 현재 진행도와 달성 여부는 런타임 상태에서 계산한다.
    [CreateAssetMenu(menuName = "OZGL2/UI/Achievement Catalog")]
    public sealed class UIAchievementCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _id;
            [SerializeField] private string _displayName;
            [SerializeField, TextArea(2, 6)] private string _description = string.Empty;
            [SerializeField] private Sprite _icon;
            [SerializeField, Min(1)] private int _target = 1;

            public string Id => _id;
            public string DisplayName => _displayName ?? string.Empty;
            public string Description => _description ?? string.Empty;
            public Sprite Icon => _icon;
            public int Target => Mathf.Max(1, _target);
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        public IReadOnlyList<Entry> Entries => _entries ?? Array.Empty<Entry>();
    }
}
