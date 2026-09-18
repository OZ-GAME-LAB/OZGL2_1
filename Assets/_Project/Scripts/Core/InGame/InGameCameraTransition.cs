using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace OZGL2.InGame
{
    /// <summary>게임 진행을 소유하지 않고 카메라 구도와 취소 가능한 보간만 담당한다.</summary>
    public sealed class InGameCameraTransition : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private InGameCameraConfigSO _config;
        private Coroutine _motion;
        private TaskCompletionSource<bool> _completion;
        public Camera Camera => _camera;
        public InGameCameraConfigSO Config => _config;
        public bool IsMoving => _motion != null;
        public void Validate()
        {
            if (_camera == null || _config == null) throw new System.InvalidOperationException("Camera and framing config are required.");
            _config.Validate();
            if (Quaternion.Angle(_camera.transform.rotation, Quaternion.identity) > 0.01f)
                throw new System.InvalidOperationException("The XY battlefield requires a front-facing camera.");
        }
        public Task FrameAsync(Bounds bounds, bool preparation, bool immediate = false)
        {
            float halfHeight;
            var target = CalculateTarget(bounds, preparation, out halfHeight);
            Cancel();
            if (immediate || _config.TransitionSeconds <= 0)
            {
                _camera.transform.position = target;
                if (_camera.orthographic) _camera.orthographicSize = halfHeight;
                return Task.CompletedTask;
            }
            var completion = new TaskCompletionSource<bool>();
            _completion = completion;
            _motion = StartCoroutine(Move(target, halfHeight, completion));
            return completion.Task;
        }
        public void ValidateFraming(Bounds bounds, bool preparation)
        { CalculateTarget(bounds, preparation, out _); }
        private Vector3 CalculateTarget(Bounds bounds, bool preparation, out float halfHeight)
        {
            Validate();
            var viewport = _config.GetViewport(preparation);
            float padding = preparation ? _config.PreparationPadding : _config.BattlePadding;
            halfHeight = Mathf.Max((bounds.size.y + padding * 2) / viewport.height,
                (bounds.size.x + padding * 2) / (_camera.aspect * viewport.width)) * 0.5f;
            float distance = _camera.orthographic ? Mathf.Max(10, _camera.nearClipPlane + 1) :
                halfHeight / Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            distance = Mathf.Max(distance, _camera.nearClipPlane + 1);
            if (distance >= _camera.farClipPlane) throw new System.InvalidOperationException("Camera far clip is too small for this battlefield.");
            var target = new Vector3(bounds.center.x - (viewport.center.x - 0.5f) * halfHeight * 2 * _camera.aspect,
                bounds.center.y - (viewport.center.y - 0.5f) * halfHeight * 2, bounds.center.z - distance);
            return target;
        }
        private IEnumerator Move(Vector3 target, float size, TaskCompletionSource<bool> completion)
        {
            var start = _camera.transform.position;
            float startSize = _camera.orthographicSize;
            float elapsed = 0;
            while (elapsed < _config.TransitionSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / _config.TransitionSeconds));
                _camera.transform.position = Vector3.Lerp(start, target, t);
                if (_camera.orthographic) _camera.orthographicSize = Mathf.Lerp(startSize, size, t);
            }
            _motion = null; _completion = null;
            completion.TrySetResult(true);
        }
        public void Cancel()
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = null;
            var completion = _completion; _completion = null; completion?.TrySetCanceled();
        }
        private void OnDisable() => Cancel();
    }
}
