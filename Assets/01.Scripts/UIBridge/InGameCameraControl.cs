using OZGL2.InGame;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 화면을 손님이 직접 보게 해 준다 — 마우스 휠로 확대·축소(커서 쪽으로 다가감), 오른쪽(또는 가운데) 버튼을 누른 채 끌어서 화면 이동, Home 키로 원래 구도.
    /// 기본 구도는 팀의 InGameCameraTransition 이 단계(배치·전투)마다 잡아 주므로, 그 보간이 끝난 자리를 "기준"으로 삼고
    /// 그 위에서 거리·위치만 바꾼다. 단계가 바뀌어 카메라가 새 구도로 움직이면 확대·이동은 자연스럽게 기준으로 돌아간다.
    /// 카메라는 정면 원근(perspective) 카메라라서 확대 = 전장 평면에 가까워지는 것이다(팀 카메라 코드는 수정하지 않는다).
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class InGameCameraControl : MonoBehaviour
    {
        [SerializeField, Range(0.2f, 1f), Tooltip("가장 확대했을 때의 거리(기준 거리 대비)")] private float _closestScale = 0.4f;
        [SerializeField, Min(1f), Tooltip("가장 축소했을 때의 거리(기준 거리 대비)")] private float _farthestScale = 1.15f;
        [SerializeField, Range(0.02f, 0.4f), Tooltip("휠 한 칸에 변하는 거리 비율")] private float _wheelStep = 0.28f;
        [SerializeField, Min(1f), Tooltip("클수록 확대·이동이 빨리 따라온다")] private float _follow = 18f;
        [SerializeField, Range(0.2f, 1.5f), Tooltip("기준 화면 크기 대비 화면을 옮길 수 있는 범위")] private float _panRange = 0.4f;
        [SerializeField, Range(0.1f, 1f), Tooltip("끌기 감도. 1 이면 마우스가 움직인 만큼 맵이 그대로 따라온다")] private float _panSpeed = 0.4f;

        /// <summary>다른 연출(궁극기 등)이 화면을 흔들 때 쓰는 값(월드 단위). 매 프레임 이 값만큼 카메라가 더해서 흔들린다.</summary>
        public static Vector2 Shake;

        private Vector2 _prevShake;
        private InGameCameraTransition _transition;
        private InGamePrototypeBootstrap _bootstrap;
        private Camera _cam;
        private bool _hasBase;
        private Vector2 _baseXY, _targetXY;
        private float _baseDist, _targetDist;
        private Vector3 _lastApplied;
        private bool _dragging;

        private float PlaneZ => _bootstrap != null && _bootstrap.Config != null ? _bootstrap.Config.GridWorldOrigin.z : 0f;

        private void LateUpdate()
        {
            if (_transition == null) _transition = FindFirstObjectByType<InGameCameraTransition>();
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (_transition == null) return;
            _cam = _transition.Camera;
            if (_cam == null || _cam.orthographic) return;

            // 단계 전환 보간 중에는 손대지 않고, 끝난 자리를 새 기준으로 삼는다
            if (_transition.IsMoving) { _hasBase = false; _dragging = false; return; }
            var pos = _cam.transform.position;
            if (!_hasBase || (pos - _lastApplied).sqrMagnitude > 1e-6f) Rebase(pos);

            var mouse = Mouse.current;
            if (mouse != null) HandleInput(mouse);

            float t = 1f - Mathf.Exp(-_follow * Time.unscaledDeltaTime);
            float dist = Mathf.Lerp(PlaneZ - pos.z, _targetDist, t);
            var xy = Vector2.Lerp(new Vector2(pos.x - _prevShake.x, pos.y - _prevShake.y), _targetXY, t); // 지난 프레임에 더한 흔들림은 빼고 계산
            _cam.transform.position = _lastApplied = new Vector3(xy.x + Shake.x, xy.y + Shake.y, PlaneZ - dist);
            _prevShake = Shake;
        }

        private void Rebase(Vector3 pos)
        {
            _hasBase = true;
            _baseXY = _targetXY = new Vector2(pos.x, pos.y);
            _baseDist = _targetDist = Mathf.Max(0.5f, PlaneZ - pos.z);
            _lastApplied = pos;
        }

        private float HalfHeight(float dist) => dist * Mathf.Tan(_cam.fieldOfView * Mathf.Deg2Rad * 0.5f);

        private void HandleInput(Mouse mouse)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.homeKey.wasPressedThisFrame) { _targetXY = _baseXY; _targetDist = _baseDist; }

            Vector2 pointer = mouse.position.ReadValue();
            bool inside = pointer.x >= 0f && pointer.y >= 0f && pointer.x <= Screen.width && pointer.y <= Screen.height;

            // 휠: 커서 아래의 전장 지점이 그대로 커서 아래에 머물도록 확대·축소한다
            float scroll = mouse.scroll.ReadValue().y;
            if (inside && Mathf.Abs(scroll) > 0.01f)
            {
                // 장치에 따라 휠 한 칸이 1 또는 120 으로 들어온다 — 어느 쪽이든 한 칸은 한 칸으로 센다
                float notches = Mathf.Sign(scroll) * Mathf.Clamp(Mathf.Abs(scroll) >= 1f ? Mathf.Max(1f, Mathf.Abs(scroll) / 120f) : Mathf.Abs(scroll), 0f, 3f);
                float newDist = Mathf.Clamp(_targetDist * Mathf.Pow(1f - _wheelStep, notches), _baseDist * _closestScale, _baseDist * _farthestScale);
                Vector2 ndc = new Vector2(pointer.x / Screen.width * 2f - 1f, pointer.y / Screen.height * 2f - 1f);
                float aspect = _cam.aspect;
                float h0 = HalfHeight(_targetDist), h1 = HalfHeight(newDist);
                Vector2 world = _targetXY + new Vector2(ndc.x * h0 * aspect, ndc.y * h0);
                _targetXY = world - new Vector2(ndc.x * h1 * aspect, ndc.y * h1);
                _targetDist = newDist;
            }

            // 오른쪽·가운데 버튼 드래그: 잡은 전장이 커서를 따라오게 옮긴다
            bool holding = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            if (holding && !_dragging && inside) _dragging = true;
            if (!holding) _dragging = false;
            if (_dragging)
            {
                Vector2 delta = mouse.delta.ReadValue();
                float perPixel = 2f * HalfHeight(_targetDist) / Mathf.Max(1, Screen.height);
                _targetXY -= delta * perPixel * _panSpeed;
            }

            // 기준 구도에서 너무 멀리 벗어나지 않게 막는다
            float baseHalf = HalfHeight(_baseDist);
            float rx = baseHalf * _cam.aspect * _panRange, ry = baseHalf * _panRange;
            _targetXY = new Vector2(Mathf.Clamp(_targetXY.x, _baseXY.x - rx, _baseXY.x + rx), Mathf.Clamp(_targetXY.y, _baseXY.y - ry, _baseXY.y + ry));
        }
    }
}
