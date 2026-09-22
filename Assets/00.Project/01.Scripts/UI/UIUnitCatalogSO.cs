using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.UIFlow
{
    public enum eUnitCodexFaction { DEMON, HERO }

    // 전투 스탯과 분리한 도감의 고정 표시 정보다. 해금 변경은 UILobbyCollectionState가 보관한다.
    [CreateAssetMenu(menuName = "OZGL2/UI/Unit Codex Catalog")]
    public sealed class UIUnitCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField, Tooltip("기존 유닛 ID 앞에 unit.을 붙인 고유 키 (예: unit.M_WAR_01)")]
            private string _id;
            [SerializeField] private string _displayName;
            [SerializeField, TextArea(2, 6)] private string _description;
            [SerializeField] private Sprite _portrait;
            [SerializeField] private eUnitCodexFaction _faction;
            [SerializeField] private bool _defaultUnlocked;

            public string Id => _id ?? string.Empty;
            public string DisplayName => _displayName ?? string.Empty;
            public string Description => _description ?? string.Empty;
            public Sprite Portrait => _portrait;
            public eUnitCodexFaction Faction => _faction;
            public bool DefaultUnlocked => _defaultUnlocked;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        public IReadOnlyList<Entry> Entries => _entries;
    }
}
