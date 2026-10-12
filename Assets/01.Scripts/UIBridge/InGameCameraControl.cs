using OZGL2.InGame;
using OZGL2.Stage;
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
        [Header("전투 자동 구도")]
        [SerializeField, Tooltip("전투 중에 마왕군과 가까이 온 용사들이 화면을 꽉 채우도록 자동으로 다가간다. 휠·끌기를 쓰면 잠시 멈추고, Home 을 누르면 다시 자동")] private bool _combatAutoFrame = true;
        [SerializeField, Range(0.35f, 1f), Tooltip("자동으로 다가갈 수 있는 가장 가까운 거리(전투 기준 구도 대비)")] private float _autoClosest = 0.5f;
        [SerializeField, Min(0f), Tooltip("구도에 함께 담는 가까운 용사 수")] private int _autoHeroCount = 6;
        [SerializeField, Min(0f), Tooltip("담은 유닛 둘레의 여유(월드 단위)")] private float _autoPadding = 2.1f;
        [SerializeField, Min(0f), Tooltip("직접 조작한 뒤 자동 구도가 다시 시작되기까지의 시간(초)")] private float _manualHold = 6f;

        private const string AutoKey = "OZGL2.Camera.Auto";
        private static int _autoOn = -1;

        /// <summary>
        /// 전투 자동 구도 켜짐(배속 막대 옆 「자동」 단추). 끄면 수동 카메라 — 휠·끌기로만 움직인다. 고른 값은 저장된다. 기본 켜짐.
        /// 켜져 있어도 휠·끌기를 쓰면 잠시 사람이 우선한다.
        /// </summary>
        public static bool AutoEnabled
        {
            get { if (_autoOn < 0) _autoOn = PlayerPrefs.GetInt(AutoKey, 1); return _autoOn != 0; }
            set
            {
                _autoOn = value ? 1 : 0;
                PlayerPrefs.SetInt(AutoKey, _autoOn);
                if (value) _resumeAuto = true;   // 켜는 순간 직접 조작 대기시간을 무시하고 바로 자동 구도로 돌아간다
            }
        }

        private static bool _resumeAuto;

        /// <summary>다른 연출(궁극기 등)이 화면을 흔들 때 쓰는 값(월드 단위). 매 프레임 이 값만큼 카메라가 더해서 흔들린다.</summary>
        public static Vector2 Shake;

        private static float _punch;
        private float _prevPunchOffset;

        /// <summary>화면이 순간적으로 amount(거리 비율, 0.03 이면 3%)만큼 다가갔다가 부드럽게 돌아온다. 스킬 시전·마지막 용사 처치 같은 순간에 부른다.</summary>
        public static void Punch(float amount) => _punch = Mathf.Max(_punch, amount);

        private Vector2 _prevShake;
        private InGameCameraTransition _transition;
        private InGamePrototypeBootstrap _bootstrap;
        private Camera _cam;
        private bool _hasBase;
        private Vector2 _baseXY, _targetXY;
        private float _baseDist, _targetDist;
        private Vector3 _lastApplied;
        private bool _dragging;
        private float _manualUntil;
        private Vector2 _autoXY;
        private float _autoDist;
        private bool _autoActive;
        private Vector2 _emaMin, _emaMax, _velXY;
        private float _velDist;

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
            if (_resumeAuto) { _resumeAuto = false; _manualUntil = 0f; _autoActive = false; }
            if (_combatAutoFrame && AutoEnabled) AutoFrame(Time.unscaledDeltaTime);
            else _autoActive = false;

            // 맵 바깥(장식이 없는 빈 땅)이 화면에 보이지 않게 목표를 맵 안으로 끌어온다
            ClampToMap(ref _targetXY, ref _targetDist);

            float t = 1f - Mathf.Exp(-_follow * Time.unscaledDeltaTime);
            float dist = Mathf.Lerp(PlaneZ - pos.z + _prevPunchOffset, _targetDist, t); // 지난 프레임에 더한 펀치는 빼고 계산
            var xy = Vector2.Lerp(new Vector2(pos.x - _prevShake.x, pos.y - _prevShake.y), _targetXY, t); // 지난 프레임에 더한 흔들림은 빼고 계산
            ClampToMap(ref xy, ref dist);
            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 0.35f) * Mathf.Exp(-9f * Time.unscaledDeltaTime);
            float punchOffset = -dist * _punch;   // 거리가 줄어든 만큼 다가가 보인다
            _prevPunchOffset = punchOffset;
            dist += punchOffset;
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

        /// <summary>
        /// 전투 중 카메라를 자동으로 다가가게 한다. 마왕군·마왕과 마왕성에 가까운 용사(그리고 보스)를 한 화면에 담도록 거리와 중심을 정한다.
        /// 자연스럽게 보이도록 이렇게 움직인다:
        /// - 담을 범위(상자)를 매 프레임 그대로 쓰지 않고 시간 평균을 내서, 용사가 죽거나 새로 가까워질 때 화면이 튀지 않는다.
        /// - 용사가 멀리 있을 때는 전투 기준 구도(전체)에 머물다가, 가까이 올수록 천천히 다가간다. 기준 구도보다 멀어지지는 않는다.
        /// - 다가갈 때(확대)는 느긋하게, 물러날 때(축소)는 빠르게 — 용사가 화면 밖으로 나가 버리지 않게 한다.
        /// - 중심을 아주 조금씩 바뀌는 건 무시(불감대)하고, 이동·확대 모두 부드러운 감속 곡선(SmoothDamp)으로 따라간다.
        /// - 게임 배속이 높을수록 카메라도 그만큼 빨리 따라간다(최대 2배).
        /// 손님이 휠·끌기를 쓰면 _manualHold 초 동안 멈추고, 끝나면 그 자리에서부터 부드럽게 자동 구도로 돌아간다.
        /// </summary>
        private void AutoFrame(float dt)
        {
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            bool combat = stage != null && stage.State == eStageState.COMBAT;
            if (!combat || Time.unscaledTime < _manualUntil || _dragging) { _autoActive = false; return; }

            // 지금 담아야 할 범위(상자)를 구한다
            bool any = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            void Add(Vector3 pos)
            {
                var q = new Vector2(pos.x, pos.y);
                if (!any) { min = max = q; any = true; }
                else { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
            }

            if (UnitRegistry.KingWorldPosition.HasValue) Add(UnitRegistry.KingWorldPosition.Value);
            foreach (var u in UnitRegistry.GetUnits(UnitSide.DemonArmy))
                if (u != null && u.currentState != UnitState.Dead) Add(u.transform.position);
            if (!any) return;

            var center = (min + max) * 0.5f;
            var near = new System.Collections.Generic.List<(float d, Vector3 p)>();
            foreach (var u in UnitRegistry.GetUnits(UnitSide.Hero))
            {
                if (u == null || u.currentState == UnitState.Dead || !u.gameObject.activeInHierarchy) continue;
                bool boss = u.statData != null && u.statData.sizeMultiplier > 1.3f;   // 보스는 멀리 있어도 늘 담는다
                if (boss) { Add(u.transform.position); continue; }
                near.Add(((new Vector2(u.transform.position.x, u.transform.position.y) - center).sqrMagnitude, u.transform.position));
            }
            near.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < near.Count && i < _autoHeroCount; i++) Add(near[i].p);

            float speed = Mathf.Clamp(Time.timeScale, 1f, 2f);
            float step = dt * speed;

            if (!_autoActive)
            {
                _autoXY = _targetXY; _autoDist = _targetDist;
                _emaMin = min; _emaMax = max;
                _velXY = Vector2.zero; _velDist = 0f;
                _autoActive = true;
            }

            // 범위를 시간 평균으로 부드럽게(죽거나 새로 가까워지는 용사 때문에 튀지 않게)
            float avg = 1f - Mathf.Exp(-3.2f * step);
            _emaMin = Vector2.Lerp(_emaMin, min, avg);
            _emaMax = Vector2.Lerp(_emaMax, max, avg);
            var ext = (_emaMax - _emaMin) * 0.5f;
            var ctr = (_emaMin + _emaMax) * 0.5f;

            float need = Mathf.Max(ext.y + _autoPadding, (ext.x + _autoPadding) / Mathf.Max(0.1f, _cam.aspect));
            float wantDist = Mathf.Clamp(need / Mathf.Tan(_cam.fieldOfView * Mathf.Deg2Rad * 0.5f), _baseDist * _autoClosest, _baseDist);
            // 화면 위·아래의 UI 띠에 가리지 않도록 중심을 살짝 위로 올린다
            var wantXY = new Vector2(ctr.x, ctr.y + HalfHeight(wantDist) * 0.08f);

            // 불감대: 중심이 조금 바뀌는 건 무시하고, 넘어선 만큼만 따라간다
            var delta = wantXY - _autoXY;
            float dead = HalfHeight(_autoDist) * 0.07f;
            if (delta.magnitude < dead) wantXY = _autoXY;
            else wantXY = _autoXY + delta * (1f - dead / delta.magnitude);

            // 확대(다가감)는 느긋하게, 축소(물러남)는 빠르게
            float zoomTime = wantDist < _autoDist ? 1.25f : 0.45f;
            _autoDist = Mathf.SmoothDamp(_autoDist, wantDist, ref _velDist, zoomTime / speed, _baseDist * 0.8f, dt);
            _autoXY = Vector2.SmoothDamp(_autoXY, wantXY, ref _velXY, 0.8f / speed, HalfHeight(_baseDist) * 1.4f, dt);

            _targetXY = _autoXY;
            _targetDist = _autoDist;
        }

        private float HalfHeight(float dist) => dist * Mathf.Tan(_cam.fieldOfView * Mathf.Deg2Rad * 0.5f);

        /// <summary>
        /// 화면에 보이는 사각형(중심 xy, 거리 dist 에서의 크기)이 맵 범위 안에 머물게 한다.
        /// 맵보다 화면이 크면(너무 멀리 물러났으면) 거리를 맵에 꼭 맞게 줄이고, 아니면 중심을 맵 안쪽으로 밀어 넣는다.
        /// 장식이 아직 만들어지지 않았으면 아무것도 하지 않는다.
        /// </summary>
        private void ClampToMap(ref Vector2 xy, ref float dist)
        {
            if (!InGameBattlefieldDecor.HasMapBounds || _cam == null) return;
            var map = InGameBattlefieldDecor.MapBounds;
            float aspect = Mathf.Max(0.1f, _cam.aspect);
            float tan = Mathf.Tan(_cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
            // 맵 안에 다 들어오는 가장 먼 거리
            float maxDist = Mathf.Min(map.height * 0.5f / tan, map.width * 0.5f / (tan * aspect));
            dist = Mathf.Min(dist, maxDist);
            float halfH = dist * tan, halfW = halfH * aspect;
            xy.x = map.width <= halfW * 2f ? map.center.x : Mathf.Clamp(xy.x, map.xMin + halfW, map.xMax - halfW);
            xy.y = map.height <= halfH * 2f ? map.center.y : Mathf.Clamp(xy.y, map.yMin + halfH, map.yMax - halfH);
        }

        /// <summary>전투 중에 휠·끌기로 직접 카메라를 만지면 「자동」을 끈다(수동 카메라). 단추가 OFF 로 바뀌고, 다시 켜거나 Home 을 누르면 자동으로 돌아간다.</summary>
        private void SwitchToManual()
        {
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage != null && stage.State == eStageState.COMBAT && AutoEnabled) AutoEnabled = false;
        }

        private void HandleInput(Mouse mouse)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.homeKey.wasPressedThisFrame) { _targetXY = _baseXY; _targetDist = _baseDist; _manualUntil = 0f; _autoActive = false; AutoEnabled = true; }   // Home: 원래 구도로, 자동 카메라도 다시 켠다

            Vector2 pointer = mouse.position.ReadValue();
            bool inside = pointer.x >= 0f && pointer.y >= 0f && pointer.x <= Screen.width && pointer.y <= Screen.height;

            // 휠: 커서 아래의 전장 지점이 그대로 커서 아래에 머물도록 확대·축소한다
            float scroll = mouse.scroll.ReadValue().y;
            if (inside && Mathf.Abs(scroll) > 0.01f)
            {
                _manualUntil = Time.unscaledTime + _manualHold;
                SwitchToManual();
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
            if (holding && !_dragging && inside) { _dragging = true; _manualUntil = Time.unscaledTime + _manualHold; SwitchToManual(); }
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
