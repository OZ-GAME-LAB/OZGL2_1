using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.UIFlow
{
    public enum eLobbyDifficulty { EASY, NORMAL, HARD }

    /// <summary>난이도 이름과 설명만 보관한다. 전투 밸런스·선택 상태·저장 데이터는 소유하지 않는다.</summary>
    [CreateAssetMenu(menuName = "OZGL2/UI/Lobby Difficulty Catalog")]
    public sealed class LobbyDifficultyCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private eLobbyDifficulty _difficulty;
            [SerializeField] private string _displayName;
            [SerializeField, TextArea(1, 3)] private string _description;

            public eLobbyDifficulty Difficulty => _difficulty;
            public string DisplayName => _displayName ?? string.Empty;
            public string Description => _description ?? string.Empty;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        public IReadOnlyList<Entry> Entries => _entries;
        public int Count => _entries == null ? 0 : _entries.Length;

        public Entry GetEntry(int index)
        {
            return index >= 0 && index < Count ? _entries[index] : null;
        }

        public int GetIndex(eLobbyDifficulty difficulty)
        {
            for (int i = 0; i < Count; i++)
                if (_entries[i] != null && _entries[i].Difficulty == difficulty) return i;
            return -1;
        }

        public bool TryValidate(out string reason)
        {
            // 난이도 순서를 고정해 카탈로그 재정렬로 좌우 이동의 의미가 뒤집히지 않게 한다.
            if (Count != 3)
            {
                reason = "쉬움 / 보통 / 어려움 항목 3개가 필요합니다.";
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                Entry entry = _entries[i];
                if (entry == null || entry.Difficulty != (eLobbyDifficulty)i || string.IsNullOrWhiteSpace(entry.DisplayName))
                {
                    reason = "카탈로그의 난이도 순서 또는 표시 이름을 확인하세요.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }
}
