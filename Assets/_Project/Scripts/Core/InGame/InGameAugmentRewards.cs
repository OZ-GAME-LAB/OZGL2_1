using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Augment;
using OZGL2.Stage;
using OZGL2.Synergy;
using UnityEngine;

namespace OZGL2.InGame
{
    public sealed class InGameAugmentRewards : MonoBehaviour, IStageRewards
    {
        private InGameAugmentSelection _selection;
        public bool IsPending => _selection != null && _selection.IsPending;
        public string RequestId => _selection?.RequestId;
        public IReadOnlyList<AugmentData> Candidates => _selection?.Candidates ?? Array.Empty<AugmentData>();
        public string Error => _selection?.Error;

        public void Bind(RealSynergySync sync)
        {
            CancelSelection();
            if (sync == null || sync.Augments == null || sync.SkillManager == null)
                throw new InvalidOperationException("Initialized synergy service is required for augment selection.");
            var owner = sync.SkillManager;
            _selection = new InGameAugmentSelection(sync.Augments, () => sync != null && ReferenceEquals(owner, sync.SkillManager), InGameAugmentAvailability.CanOffer);
        }

        public Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken token)
        {
            if (!isActiveAndEnabled || _selection == null) throw new InvalidOperationException("Augment provider is not active or bound.");
            return _selection.SelectAsync(request, weights, token);
        }
        public bool TrySelect(string requestId, AugmentData candidate) => _selection != null && _selection.TrySelect(requestId, candidate);
        public Task SelectGeneralRewardAsync(RewardRequest request, string rewardId, CancellationToken token)
            => throw new InvalidOperationException("General rewards must use StageGridRewards.");
        public void CancelSelection() { _selection?.Dispose(); _selection = null; }
        private void OnDisable() => CancelSelection();
        private void OnDestroy() => CancelSelection();
    }
}
