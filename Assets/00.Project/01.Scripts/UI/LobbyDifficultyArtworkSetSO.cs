using UnityEngine;

namespace OZGL2.UIFlow
{
    /// <summary>난이도 데이터와 독립적으로 세 장의 아트를 한 번에 교체하는 표시용 세트다.</summary>
    [CreateAssetMenu(menuName = "OZGL2/UI/Lobby Difficulty Artwork Set")]
    public sealed class LobbyDifficultyArtworkSetSO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _easy;
        [SerializeField] private Sprite _normal;
        [SerializeField] private Sprite _hard;

        public string DisplayName => _displayName ?? string.Empty;
        public bool IsComplete => _easy != null && _normal != null && _hard != null;

        public Sprite GetArtwork(eLobbyDifficulty difficulty)
        {
            switch (difficulty)
            {
                case eLobbyDifficulty.EASY: return _easy;
                case eLobbyDifficulty.NORMAL: return _normal;
                case eLobbyDifficulty.HARD: return _hard;
                default: return null;
            }
        }
    }
}
