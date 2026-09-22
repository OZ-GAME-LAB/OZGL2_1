using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>실제 UI는 같은 제공자의 Candidates/RequestId/TrySelect를 사용한다.</summary>
    [RequireComponent(typeof(InGameAugmentRewards))]
    public sealed class InGameAugmentDummyView : MonoBehaviour
    {
        private InGameAugmentRewards _provider;
        private void Awake() => _provider = GetComponent<InGameAugmentRewards>();
        private void OnGUI()
        {
            if (_provider == null || !_provider.IsPending) return;
            var candidates = _provider.Candidates;
            string requestId = _provider.RequestId;
            float width = Mathf.Min(620f, Screen.width - 20f);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, (Screen.height - 300f) / 2f, width, 300f), GUI.skin.box);
            GUILayout.Label("증강 선택 · 하나를 선택하면 다음 준비 단계로 진행합니다.");
            foreach (var candidate in candidates)
            {
                if (GUILayout.Button(candidate.displayName + " (" + OZGL2.Augment.AugmentData.TierName(candidate.tier) + ")\n" + candidate.description, GUILayout.Height(70f)))
                {
                    _provider.TrySelect(requestId, candidate);
                    break;
                }
            }
            GUILayout.EndArea();
        }
    }
}
