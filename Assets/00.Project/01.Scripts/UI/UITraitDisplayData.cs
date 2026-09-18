using UnityEngine;

namespace OZGL2.UIFlow
{
    // 게임 데이터/저장 데이터를 소유하지 않는 UI 표시용 스냅샷이다.
    public readonly struct UITraitDisplayData
    {
        public string TraitId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public string Description { get; }
        public int CurrentRank { get; }
        public int MaxRank { get; }
        public bool IsUnlocked { get; }
        public bool CanUpgrade { get; }
        public string CurrentEffect { get; }
        public string NextEffect { get; }
        public string UnavailableReason { get; }

        public UITraitDisplayData(string traitId, string displayName, string category, string description,
            int currentRank, int maxRank, bool isUnlocked, bool canUpgrade,
            string currentEffect, string nextEffect, string unavailableReason)
        {
            TraitId = traitId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            MaxRank = Mathf.Max(1, maxRank);
            CurrentRank = Mathf.Clamp(currentRank, 0, MaxRank);
            IsUnlocked = isUnlocked;
            CanUpgrade = canUpgrade && isUnlocked && CurrentRank < MaxRank && !string.IsNullOrWhiteSpace(traitId);
            CurrentEffect = currentEffect ?? string.Empty;
            NextEffect = nextEffect ?? string.Empty;
            UnavailableReason = unavailableReason ?? string.Empty;
        }
    }
}
