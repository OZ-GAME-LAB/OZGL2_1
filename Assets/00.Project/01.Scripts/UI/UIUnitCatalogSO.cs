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
            [SerializeField, Tooltip("선택 사항. 마왕군은 1~3성 순서, 인간군은 외형 순서. 비워두면 기존 Portrait를 사용한다.")]
            private Sprite[] _portraitVariants = Array.Empty<Sprite>();
            [SerializeField] private eUnitCodexFaction _faction;
            [SerializeField] private bool _defaultUnlocked;
            [SerializeField, Tooltip("기본 스탯 원본입니다. 도감은 읽기만 하며 전투 보정이나 저장값을 적용하지 않습니다.")]
            private UnitStatData _baseStats;

            public string Id => _id ?? string.Empty;
            public string DisplayName => _displayName ?? string.Empty;
            public string Description => _description ?? string.Empty;
            public Sprite Portrait => _portrait;
            public IReadOnlyList<Sprite> PortraitVariants => _portraitVariants ?? Array.Empty<Sprite>();
            public int AppearanceCount => _portraitVariants != null && _portraitVariants.Length > 0 ? _portraitVariants.Length : 1;
            public eUnitCodexFaction Faction => _faction;
            public bool DefaultUnlocked => _defaultUnlocked;
            public UnitStatData BaseStats => _baseStats;

            public Sprite GetPortrait(int appearanceIndex)
            {
                if (_portraitVariants == null || appearanceIndex < 0 || appearanceIndex >= _portraitVariants.Length)
                    return _portrait;
                return _portraitVariants[appearanceIndex] != null ? _portraitVariants[appearanceIndex] : _portrait;
            }
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        public IReadOnlyList<Entry> Entries => _entries;
    }
}
