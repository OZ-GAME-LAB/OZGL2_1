using UnityEngine;

namespace OZGL2.InGame
{
    [CreateAssetMenu(menuName = "OZGL2/InGame/Camera Framing")]
    public sealed class InGameCameraConfigSO : ScriptableObject
    {
        [SerializeField, Min(0)] private float _transitionSeconds = 0.55f;
        [SerializeField, Min(0)] private float _preparationPadding = 0.5f;
        [SerializeField, Min(0)] private float _battlePadding = 4f;
        [SerializeField] private Rect _preparationViewport = new Rect(0.08f, 0.34f, 0.84f, 0.52f);
        [SerializeField] private Rect _battleViewport = new Rect(0.06f, 0.14f, 0.88f, 0.76f);
        public float TransitionSeconds => _transitionSeconds;
        public float PreparationPadding => _preparationPadding;
        public float BattlePadding => _battlePadding;
        public Rect GetViewport(bool preparation) => preparation ? _preparationViewport : _battleViewport;
        public void Validate()
        {
            if (!float.IsFinite(_transitionSeconds) || _transitionSeconds < 0 ||
                !float.IsFinite(_preparationPadding) || _preparationPadding < 0 ||
                !float.IsFinite(_battlePadding) || _battlePadding < 0) throw new System.InvalidOperationException("Invalid camera framing settings.");
            foreach (var rect in new[] { _preparationViewport, _battleViewport })
                if (!float.IsFinite(rect.x) || !float.IsFinite(rect.y) || !float.IsFinite(rect.width) || !float.IsFinite(rect.height) ||
                    rect.width <= 0 || rect.height <= 0 || rect.xMin < 0 || rect.yMin < 0 || rect.xMax > 1 || rect.yMax > 1)
                    throw new System.InvalidOperationException("Camera safe viewport must be inside 0..1.");
        }
    }
}
