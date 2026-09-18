using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using OZGL2.Stage;
using OZGL2.Synergy;

namespace OZGL2.Augment
{
    /// <summary>
    /// 실제 게임용 IStageRewards — 증강 선택만 담당(일반 보상은 StageGridRewards가 직접 처리하므로
    /// SelectGeneralRewardAsync는 이 경로로 호출되지 않는다). 보스 라운드에서 실제 AugmentRun으로
    /// 3장을 뽑아 화면에 띄우고, 플레이어가 고를 때까지 기다린다.
    /// InGamePrototypeBootstrap의 _augmentRewardsOverride 필드에 이 컴포넌트를 연결하면
    /// 기존 Dummy(더미 확인 버튼) 대신 이게 쓰인다.
    /// </summary>
    public class RealAugmentRewards : MonoBehaviour, IStageRewards
    {
        private AugmentRun _augments;
        private List<AugmentData> _pending;
        private TaskCompletionSource<bool> _waiting;

        private void Awake()
        {
            var sync = Object.FindFirstObjectByType<RealSynergySync>();
            _augments = sync != null ? sync.Augments : null;
            if (_augments == null)
            {
                Debug.LogWarning("[RealAugmentRewards] RealSynergySync를 못 찾음 — 증강 연동이 비활성 상태로 동작함(뽑기 자동 스킵).");
            }
        }

        public Task SelectGeneralRewardAsync(RewardRequest request, string rewardId, CancellationToken cancellationToken)
        {
            // 이 경로로는 안 옴 — 일반 보상은 StageGridRewards가 그리드 지급 완료로 직접 판정한다.
            return Task.CompletedTask;
        }

        public async Task SelectAugmentAsync(RewardRequest request, AugmentTierWeights weights, CancellationToken cancellationToken)
        {
            if (_augments == null) return;

            _pending = _augments.Draw3(TierWeightsToStage(weights));
            if (_pending == null || _pending.Count == 0) return;

            _waiting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => _waiting.TrySetCanceled()))
            {
                await _waiting.Task;
            }
        }

        /// <summary>실버/골드/플래티넘 가중치를 AugmentRun.Draw3이 받는 마일스톤 단계로 변환.</summary>
        private static int TierWeightsToStage(AugmentTierWeights weights)
        {
            if (weights.Platinum > 0) return 5;
            if (weights.Gold > 0) return 3;
            return 1;
        }

        private void OnGUI()
        {
            if (_pending == null || _pending.Count == 0) return;

            const float w = 620f, h = 260f;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), GUI.skin.box);
            GUILayout.Label("<b>증강 선택</b>");
            foreach (var d in _pending)
            {
                if (GUILayout.Button($"{d.displayName} ({AugmentData.TierName(d.tier)})\n{d.description}", GUILayout.Height(70f)))
                {
                    _augments.Pick(d);
                    _pending = null;
                    _waiting?.TrySetResult(true);
                    break;
                }
            }
            GUILayout.EndArea();
        }
    }
}
