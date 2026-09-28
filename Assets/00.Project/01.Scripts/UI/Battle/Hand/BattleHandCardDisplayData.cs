using System;
using UnityEngine;

namespace OZGL2.UIFlow
{
    public enum eBattleHandCardKind
    {
        UNIT,
        LAND_SLOT,
        RELIC
    }

    /// <summary>
    /// 손패 View에 전달되는 표시 전용 데이터다. 전투 상태나 카드 사용 규칙은 소유하지 않는다.
    /// </summary>
    [Serializable]
    public sealed class BattleHandCardDisplayData
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private eBattleHandCardKind _kind;
        [SerializeField] private string _title = string.Empty;
        [SerializeField] private string _rankText = string.Empty;
        [SerializeField] private string _traitTitle = string.Empty;
        [SerializeField] private string _traitDescription = string.Empty;
        [SerializeField] private string _skillTitle = string.Empty;
        [SerializeField] private string _skillDescription = string.Empty;
        [SerializeField] private string _attack = string.Empty;
        [SerializeField] private string _defense = string.Empty;
        [SerializeField] private string _health = string.Empty;
        [SerializeField] private string _areaTitle = string.Empty;
        [SerializeField] private string _areaDescription = string.Empty;
        [SerializeField] private Sprite _artwork;
        [SerializeField] private Sprite _typeIcon;
        [SerializeField] private Vector2Int[] _footprint = Array.Empty<Vector2Int>();

        public string Id => _id ?? string.Empty;
        public eBattleHandCardKind Kind => _kind;
        public string Title => _title ?? string.Empty;
        public string RankText => _rankText ?? string.Empty;
        public string TraitTitle => _traitTitle ?? string.Empty;
        public string TraitDescription => _traitDescription ?? string.Empty;
        public string SkillTitle => _skillTitle ?? string.Empty;
        public string SkillDescription => _skillDescription ?? string.Empty;
        public string Attack => _attack ?? string.Empty;
        public string Defense => _defense ?? string.Empty;
        public string Health => _health ?? string.Empty;
        public string AreaTitle => _areaTitle ?? string.Empty;
        public string AreaDescription => _areaDescription ?? string.Empty;
        public Sprite Artwork => _artwork;
        public Sprite TypeIcon => _typeIcon;
        public Vector2Int[] Footprint => _footprint ?? Array.Empty<Vector2Int>();

        public BattleHandCardDisplayData(
            string id,
            eBattleHandCardKind kind,
            string title,
            string rankText = "",
            string traitTitle = "",
            string traitDescription = "",
            string skillTitle = "",
            string skillDescription = "",
            string attack = "",
            string defense = "",
            string health = "",
            string areaTitle = "",
            string areaDescription = "",
            Sprite artwork = null,
            Sprite typeIcon = null,
            Vector2Int[] footprint = null)
        {
            _id = id ?? string.Empty;
            _kind = kind;
            _title = title ?? string.Empty;
            _rankText = rankText ?? string.Empty;
            _traitTitle = traitTitle ?? string.Empty;
            _traitDescription = traitDescription ?? string.Empty;
            _skillTitle = skillTitle ?? string.Empty;
            _skillDescription = skillDescription ?? string.Empty;
            _attack = attack ?? string.Empty;
            _defense = defense ?? string.Empty;
            _health = health ?? string.Empty;
            _areaTitle = areaTitle ?? string.Empty;
            _areaDescription = areaDescription ?? string.Empty;
            _artwork = artwork;
            _typeIcon = typeIcon;
            _footprint = footprint != null
                ? (Vector2Int[])footprint.Clone()
                : Array.Empty<Vector2Int>();
        }
    }
}
