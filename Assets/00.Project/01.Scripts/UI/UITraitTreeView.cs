using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.UIFlow
{
    [DisallowMultipleComponent]
    public sealed class UITraitTreeView : MonoBehaviour
    {
        [Serializable]
        private sealed class TraitPreview
        {
            [SerializeField] private string _traitId;
            [SerializeField] private UITraitNodeView _node;
            [SerializeField] private string _displayName = "특성 이름";
            [SerializeField] private string _category = "미리보기";
            [TextArea(2, 5)] [SerializeField] private string _description;
            [Tooltip("배열의 각 항목은 1단계부터의 표시 문구입니다. 실제 효과 계산에는 사용하지 않습니다.")]
            [SerializeField] private string[] _rankEffects = { "1단계 효과 (예시)", "2단계 효과 (예시)", "3단계 효과 (예시)" };
            [Min(0)] [SerializeField] private int _initialRank;
            [SerializeField] private bool _isUnlocked = true;
            [SerializeField] private string _lockedReason = "선행 특성이 필요합니다.";

            public string TraitId => _traitId;
            public UITraitNodeView Node => _node;
            public int InitialRank => _initialRank;

            public UITraitDisplayData CreateDisplay(int rank)
            {
                int maxRank = Mathf.Max(1, _rankEffects != null ? _rankEffects.Length : 0);
                rank = Mathf.Clamp(rank, 0, maxRank);
                string current = rank == 0 ? "아직 습득하지 않았습니다." : GetEffect(rank - 1);
                string next = rank < maxRank ? GetEffect(rank) : string.Empty;
                return new UITraitDisplayData(_traitId, _displayName, _category, _description,
                    rank, maxRank, _isUnlocked, _isUnlocked, current, next, _lockedReason);
            }

            private string GetEffect(int index)
            {
                return _rankEffects != null && index >= 0 && index < _rankEffects.Length
                    ? _rankEffects[index] : "효과 미등록";
            }
        }

        [Header("실제 게임 시스템과 무관한 미리보기")]
        [Tooltip("끄면 클릭은 UpgradeRequested만 전달합니다. 실제 시스템에서 SetTraitDisplay로 갱신하세요.")]
        [SerializeField] private bool _usePreviewData = true;
        [SerializeField] private TraitPreview[] _traits = Array.Empty<TraitPreview>();

        private readonly Dictionary<string, int> _previewRanks = new Dictionary<string, int>();
        private bool _hasInitialized;
        // 실제 연동 시 비용/선행 조건/저장 처리는 이 이벤트를 받는 쪽에서 검증한다.
        public event Action<string> UpgradeRequested;

        public void ApplyPreviewData()
        {
            _previewRanks.Clear();
            _hasInitialized = true;
            if (_traits == null) return;
            foreach (TraitPreview trait in _traits)
            {
                if (!IsValid(trait)) continue;
                if (_previewRanks.ContainsKey(trait.TraitId))
                {
                    Debug.LogWarning($"중복 특성 ID: {trait.TraitId}", this);
                    continue;
                }
                UITraitDisplayData data = trait.CreateDisplay(trait.InitialRank);
                _previewRanks.Add(trait.TraitId, data.CurrentRank);
                trait.Node.ShowTrait(data);
            }
        }

        public bool SetTraitDisplay(UITraitDisplayData data)
        {
            if (string.IsNullOrWhiteSpace(data.TraitId) || _traits == null) return false;
            foreach (TraitPreview trait in _traits)
            {
                if (!IsValid(trait) || trait.TraitId != data.TraitId) continue;
                trait.Node.ShowTrait(data);
                return true;
            }
            return false;
        }

        private void OnEnable()
        {
            if (_traits != null)
                foreach (TraitPreview trait in _traits)
                {
                    if (!IsValid(trait)) continue;
                    trait.Node.UpgradeRequested -= RequestUpgrade;
                    trait.Node.UpgradeRequested += RequestUpgrade;
                }
            // 팝업을 다시 열어도 미리보기 단계는 유지한다. Play Mode 종료 시에만 초기화된다.
            if (_usePreviewData && !_hasInitialized) ApplyPreviewData();
        }

        private void OnDisable()
        {
            if (_traits == null) return;
            foreach (TraitPreview trait in _traits)
                if (IsValid(trait)) trait.Node.UpgradeRequested -= RequestUpgrade;
        }

        private void RequestUpgrade(UITraitNodeView node)
        {
            if (!isActiveAndEnabled || node == null || !node.CanReceiveInput || !node.Data.CanUpgrade) return;
            if (!_usePreviewData)
            {
                UpgradeRequested?.Invoke(node.Data.TraitId);
                return;
            }
            if (_traits == null) return;
            foreach (TraitPreview trait in _traits)
            {
                if (!IsValid(trait) || trait.Node != node) continue;
                int rank = _previewRanks.TryGetValue(trait.TraitId, out int savedRank) ? savedRank : node.Data.CurrentRank;
                UITraitDisplayData data = trait.CreateDisplay(rank + 1);
                _previewRanks[trait.TraitId] = data.CurrentRank;
                node.ShowTrait(data);
                return;
            }
        }

        private static bool IsValid(TraitPreview trait)
        {
            return trait != null && trait.Node != null && !string.IsNullOrWhiteSpace(trait.TraitId);
        }
    }
}
