using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.UIFlow
{
    // 도감/스킬 해금과 업적 진행의 Play 세션 상태만 공유한다. SO와 실제 저장 데이터는 변경하지 않는다.
    [DisallowMultipleComponent]
    public sealed class UILobbyCollectionState : MonoBehaviour
    {
        [Serializable]
        private sealed class PreviewUnlockEntry
        {
            [SerializeField] private string _entryId;
            [SerializeField] private bool _isUnlocked;

            public string EntryId => _entryId;
            public bool IsUnlocked => _isUnlocked;
        }

        [Serializable]
        private sealed class PreviewProgressEntry
        {
            [SerializeField] private string _entryId;
            [SerializeField, Min(0)] private int _value;

            public string EntryId => _entryId;
            public int Value => _value;
        }

        [SerializeField] private PreviewUnlockEntry[] _initialUnlockOverrides = Array.Empty<PreviewUnlockEntry>();
        [SerializeField] private PreviewProgressEntry[] _initialAchievementProgress = Array.Empty<PreviewProgressEntry>();

        private readonly Dictionary<string, bool> _unlockOverrides = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _achievementProgress = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool _hasInitialized;

        public event Action Changed;

        public bool IsUnlocked(string id, bool defaultUnlocked)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            InitializeIfNeeded();
            return _unlockOverrides.TryGetValue(id, out bool isUnlocked) ? isUnlocked : defaultUnlocked;
        }

        public void SetUnlocked(string id, bool unlocked)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            InitializeIfNeeded();
            if (_unlockOverrides.TryGetValue(id, out bool previous) && previous == unlocked) return;
            _unlockOverrides[id] = unlocked;
            Changed?.Invoke();
        }

        public int GetAchievementProgress(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return 0;
            InitializeIfNeeded();
            return _achievementProgress.TryGetValue(id, out int progress) ? progress : 0;
        }

        public void SetAchievementProgress(string id, int progress)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            InitializeIfNeeded();
            progress = Mathf.Max(0, progress);
            int previous = _achievementProgress.TryGetValue(id, out int value) ? value : 0;
            if (previous == progress) return;
            _achievementProgress[id] = progress;
            Changed?.Invoke();
        }

        // 명시적으로 초기화할 때만 Inspector 초기값으로 돌아가며, 화면을 닫아도 세션 상태는 유지한다.
        public void ResetPreviewState()
        {
            _hasInitialized = false;
            InitializeIfNeeded();
            Changed?.Invoke();
        }

        private void InitializeIfNeeded()
        {
            if (_hasInitialized) return;
            _hasInitialized = true;
            _unlockOverrides.Clear();
            _achievementProgress.Clear();

            if (_initialUnlockOverrides != null)
                foreach (PreviewUnlockEntry entry in _initialUnlockOverrides)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.EntryId))
                        _unlockOverrides[entry.EntryId] = entry.IsUnlocked;

            if (_initialAchievementProgress != null)
                foreach (PreviewProgressEntry entry in _initialAchievementProgress)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.EntryId))
                        _achievementProgress[entry.EntryId] = Mathf.Max(0, entry.Value);
        }
    }
}
