using System;
using UnityEngine;

namespace OZGL2.UIFlow
{
    /// <summary>실제 배치 모양과 분리한, 확장 카드의 표시 전용 이미지 목록이다.</summary>
    [CreateAssetMenu(fileName = "UIExpansionCardVisualCatalog", menuName = "OZGL2/UI/Expansion Card Visual Catalog")]
    public sealed class UIExpansionCardVisualCatalogSO : ScriptableObject
    {
        public const string RESOURCE_PATH = "UIExpansionCardVisualCatalog";

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _shapeId;
            [SerializeField] private Sprite _artwork;

            public string ShapeId => _shapeId ?? string.Empty;
            public Sprite Artwork => _artwork;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        [SerializeField] private Sprite _fallbackArtwork;

        public static UIExpansionCardVisualCatalogSO LoadDefault()
        {
            return Resources.Load<UIExpansionCardVisualCatalogSO>(RESOURCE_PATH);
        }

        public Sprite GetArtwork(string shapeId)
        {
            if (!string.IsNullOrWhiteSpace(shapeId) && _entries != null)
            {
                foreach (Entry entry in _entries)
                {
                    if (entry != null && entry.ShapeId == shapeId)
                        return entry.Artwork != null ? entry.Artwork : _fallbackArtwork;
                }
            }

            return _fallbackArtwork;
        }
    }
}
