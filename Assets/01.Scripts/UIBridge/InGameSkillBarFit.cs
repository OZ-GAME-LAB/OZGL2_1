using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 하단 스킬 슬롯(원형 아이콘 + 이름)이 아래쪽 바(BottomSkillBackground) 밖으로 삐져나오지 않게 맞춘다.
    /// 슬롯 묶음(SkillSlots)을 한 번 재서, 아이콘·이름 전체가 바 안에 들어오도록 크기를 줄이고 바 한가운데(가로·세로)로 옮긴다.
    /// 슬롯이 1~5개 어느 쪽이든 칸 간격은 레이아웃이 똑같이 유지하고, 묶음의 가운데가 항상 바의 가운데에 오게 한다.
    /// 슬롯 수가 바뀌거나 화면 크기가 바뀌거나 가운데에서 벗어났을 때만 다시 맞춘다.
    /// 팀 프리팹은 수정하지 않고 실행 중에 컨테이너의 크기·위치만 조절한다.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    public sealed class InGameSkillBarFit : MonoBehaviour
    {
        private const string ContainerName = "SkillSlots";
        private const string BarName = "BottomSkillBackground";

        [SerializeField, Range(0.6f, 1f), Tooltip("바 높이 중 슬롯이 차지해도 되는 비율")] private float _fillHeight = 0.78f;
        [SerializeField, Range(0.6f, 1f), Tooltip("바 폭 중 슬롯이 차지해도 되는 비율")] private float _fillWidth = 0.96f;
        [SerializeField, Range(-0.15f, 0.15f), Tooltip("바 가운데에서 위(+)/아래(-)로 옮기는 비율(바 높이 기준)")] private float _verticalBias = -0.06f;

        private RectTransform _container, _bar;
        private Vector3 _baseScale;
        private Vector2 _basePosition;
        private int _lastChildCount = -1;
        private Vector2Int _lastScreen;
        private float _next;
        private readonly Vector3[] _corners = new Vector3[4];

        private bool _wasActive;

        /// <summary>
        /// 전투 화면이 켜지는 그 프레임에 바로 맞춘다(렌더링 전에). 예전에는 0.3초마다 확인해서, 전투로 넘어간 뒤 잠깐 맞추기 전 크기로 보이다가
        /// 줄어들며 자리를 잡는 게 "꿈틀거림"으로 보였다. 이제는 화면·슬롯 수가 바뀐 프레임에 한 번에 맞추고, 그 뒤에는 어긋났을 때만 다시 맞춘다.
        /// 다른 컴포넌트가 슬롯을 켜고 끈 뒤에 실행되도록 실행 순서를 뒤로 둔다.
        /// </summary>
        private void LateUpdate()
        {
            bool poll = Time.unscaledTime >= _next;
            if (poll) _next = Time.unscaledTime + 0.5f;

            if (_container == null || _bar == null)
            {
                if (!poll) return;
                foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
                {
                    if (rect == null || !rect.gameObject.scene.IsValid()) continue; // 꺼져 있는 전투 화면 안의 것도 찾는다
                    if (_container == null && rect.name == ContainerName) _container = rect;
                    else if (_bar == null && rect.name == BarName) _bar = rect;
                }
                if (_container == null || _bar == null) return;
                _baseScale = _container.localScale;
                _basePosition = _container.anchoredPosition;
                _lastChildCount = -1;
            }

            bool active = _container.gameObject.activeInHierarchy && _bar.gameObject.activeInHierarchy;
            if (!active) { _wasActive = false; return; }

            var screen = new Vector2Int(Screen.width, Screen.height);
            int children = ActiveChildren();
            bool changed = !_wasActive || screen != _lastScreen || children != _lastChildCount;
            _wasActive = true;
            _lastScreen = screen; _lastChildCount = children;
            if (changed) { Solve(); return; }
            if (poll && !Fits(out _, out _)) Solve();
        }

        private int ActiveChildren()
        {
            int n = 0;
            foreach (Transform child in _container) if (child.gameObject.activeSelf) n++;
            return n;
        }

        private static Vector2 Screen2(RectTransform rt, Vector3 world)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(cam, world);
        }

        /// <summary>바(화면 좌표)와 슬롯 묶음(움직이지 않는 그림)의 사각형을 구한다.</summary>
        private bool Measure(out Rect bar, out Rect slots)
        {
            bar = slots = default;
            _bar.GetWorldCorners(_corners);
            Vector2 b0 = Screen2(_bar, _corners[0]), b1 = Screen2(_bar, _corners[2]);
            bar = Rect.MinMaxRect(Mathf.Min(b0.x, b1.x), Mathf.Min(b0.y, b1.y), Mathf.Max(b0.x, b1.x), Mathf.Max(b0.y, b1.y));

            bool any = false;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            // 웨이브 중에 스킬 위치가 바뀌던 문제: 예전에는 슬롯 안의 보이는 그림을 전부 쟀는데, 쿨타임·사용 연출 그림이 커졌다 줄었다 하면서
            // 크기가 달라져 다시 맞추기가 일어났다. 이제는 움직이지 않는 것(슬롯 칸 자체, 판·테두리 그림, 이름 글자)만 잰다.
            foreach (Transform child in _container)
            {
                if (!child.gameObject.activeSelf) continue;
                Include(child as RectTransform, ref minX, ref minY, ref maxX, ref maxY, ref any);
            }
            foreach (var graphic in _container.GetComponentsInChildren<Graphic>(false))
            {
                if (graphic == null || !graphic.enabled || graphic.color.a < 0.05f) continue;
                if (IsEffect(graphic.name)) continue;
                if (!(graphic is Text) && !(graphic is TMP_Text) && graphic.name != "Plate" && graphic.name != "FrameArt") continue;
                Include(graphic.rectTransform, ref minX, ref minY, ref maxX, ref maxY, ref any);
            }
            if (!any) return false;
            slots = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private void Include(RectTransform rect, ref float minX, ref float minY, ref float maxX, ref float maxY, ref bool any)
        {
            if (rect == null) return;
            rect.GetWorldCorners(_corners);
            Vector2 a = Screen2(_container, _corners[0]), c = Screen2(_container, _corners[2]);
            if (Mathf.Abs(c.x - a.x) > Screen.width * 0.9f) return; // 슬롯 배경으로 쓰는 화면 전체 크기의 막은 제외
            minX = Mathf.Min(minX, Mathf.Min(a.x, c.x)); maxX = Mathf.Max(maxX, Mathf.Max(a.x, c.x));
            minY = Mathf.Min(minY, Mathf.Min(a.y, c.y)); maxY = Mathf.Max(maxY, Mathf.Max(a.y, c.y));
            any = true;
        }

        /// <summary>켜져 있는 슬롯 칸(원형 아이콘 자리)만의 화면 사각형. 이름 글자 길이에 흔들리지 않는 가로 가운데를 구하는 데 쓴다.</summary>
        private bool MeasureRoots(out Rect roots)
        {
            roots = default;
            bool any = false;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (Transform child in _container)
            {
                if (!child.gameObject.activeSelf) continue;
                Include(child as RectTransform, ref minX, ref minY, ref maxX, ref maxY, ref any);
            }
            if (!any) return false;
            roots = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static bool IsEffect(string name) => name.StartsWith("Ready") || name == "CooldownDim" || name == "CooldownHand" || name == "CooldownSpark";

        private bool Fits(out Rect bar, out Rect slots)
        {
            if (!Measure(out bar, out slots)) return true;
            const float slack = 8f; // 어긋남이 작으면 다시 맞추지 않는다(웨이브 중 미세한 흔들림 방지)
            if (!(slots.yMax <= bar.yMax + slack && slots.yMin >= bar.yMin - slack && slots.xMin >= bar.xMin - slack && slots.xMax <= bar.xMax + slack)) return false;
            // 슬롯 묶음이 바의 가로 가운데에서 벗어나도 다시 맞춘다(1~5개 어느 때든 항상 가운데 정렬)
            return !MeasureRoots(out var roots) || Mathf.Abs(roots.center.x - bar.center.x) <= slack;
        }

        private void Solve()
        {
            // 처음 값에서 다시 시작해 몇 번을 맞춰도 같은 결과가 나오게 한다
            _container.localScale = _baseScale;
            _container.anchoredPosition = _basePosition;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
            if (!Measure(out var bar, out var slots)) return;

            float k = Mathf.Min(1f, bar.height * _fillHeight / Mathf.Max(1f, slots.height), bar.width * _fillWidth / Mathf.Max(1f, slots.width));
            _container.localScale = _baseScale * k;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
            if (!Measure(out bar, out slots)) return;

            // 바 가운데로 옮긴다. 세로는 바 높이 기준 약간 아래, 가로는 슬롯 칸들의 가운데를 바의 가운데에 맞춘다.
            // (묶음은 위쪽 왼쪽을 기준으로 줄어들기 때문에, 가로를 맞추지 않으면 줄어든 만큼 왼쪽으로 쏠린다)
            float targetY = bar.center.y + bar.height * _verticalBias;
            float centerX = MeasureRoots(out var roots) ? roots.center.x : slots.center.x;
            Vector2 deltaScreen = new Vector2(bar.center.x - centerX, targetY - slots.center.y);
            var parent = _container.parent as RectTransform;
            // 화면 → 부모 로컬 변환: 오버레이 캔버스는 월드 단위가 화면 픽셀과 같으므로 부모의 크기 비율만 반영한다
            float scale = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.y) : 1f;
            _container.anchoredPosition += new Vector2(deltaScreen.x, deltaScreen.y) / scale;
        }
    }
}
