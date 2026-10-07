using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 하단 스킬 슬롯(원형 아이콘 + 이름)이 아래쪽 바(BottomSkillBackground) 밖으로 삐져나오지 않게 맞춘다.
    /// 슬롯 묶음(SkillSlots)을 한 번 재서, 아이콘·이름 전체가 바 안에 들어오도록 크기를 줄이고 바 한가운데로 옮긴다.
    /// 이미 바 안에 들어와 있으면 아무것도 하지 않는다(그래서 슬롯 수가 바뀌거나 화면 크기가 바뀔 때만 다시 맞춘다).
    /// 팀 프리팹은 수정하지 않고 실행 중에 컨테이너의 크기·위치만 조절한다.
    /// </summary>
    public sealed class InGameSkillBarFit : MonoBehaviour
    {
        private const string ContainerName = "SkillSlots";
        private const string BarName = "BottomSkillBackground";

        [SerializeField, Range(0.6f, 1f), Tooltip("바 높이 중 슬롯이 차지해도 되는 비율")] private float _fillHeight = 0.88f;
        [SerializeField, Range(0.6f, 1f), Tooltip("바 폭 중 슬롯이 차지해도 되는 비율")] private float _fillWidth = 0.96f;
        [SerializeField, Range(-0.15f, 0.15f), Tooltip("바 가운데에서 위(+)/아래(-)로 옮기는 비율(바 높이 기준)")] private float _verticalBias = -0.02f;

        private RectTransform _container, _bar;
        private Vector3 _baseScale;
        private Vector2 _basePosition;
        private int _lastChildCount = -1;
        private Vector2Int _lastScreen;
        private float _next;
        private readonly Vector3[] _corners = new Vector3[4];

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.3f;

            if (_container == null || _bar == null)
            {
                foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
                {
                    if (rect == null || !rect.gameObject.scene.IsValid() || !rect.gameObject.activeInHierarchy) continue;
                    if (_container == null && rect.name == ContainerName) _container = rect;
                    else if (_bar == null && rect.name == BarName) _bar = rect;
                }
                if (_container == null || _bar == null) return;
                _baseScale = _container.localScale;
                _basePosition = _container.anchoredPosition;
                _lastChildCount = -1;
            }
            if (!_container.gameObject.activeInHierarchy || !_bar.gameObject.activeInHierarchy) return;

            var screen = new Vector2Int(Screen.width, Screen.height);
            int children = ActiveChildren();
            bool changed = screen != _lastScreen || children != _lastChildCount;
            _lastScreen = screen; _lastChildCount = children;
            if (!changed && Fits(out _, out _)) return;
            Solve();
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

        /// <summary>바(화면 좌표)와 슬롯 묶음(보이는 그림 전부)의 사각형을 구한다.</summary>
        private bool Measure(out Rect bar, out Rect slots)
        {
            bar = slots = default;
            _bar.GetWorldCorners(_corners);
            Vector2 b0 = Screen2(_bar, _corners[0]), b1 = Screen2(_bar, _corners[2]);
            bar = Rect.MinMaxRect(Mathf.Min(b0.x, b1.x), Mathf.Min(b0.y, b1.y), Mathf.Max(b0.x, b1.x), Mathf.Max(b0.y, b1.y));

            bool any = false;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var graphic in _container.GetComponentsInChildren<Graphic>(false))
            {
                if (graphic == null || !graphic.enabled || graphic.color.a < 0.05f) continue;
                // 슬롯 배경으로 쓰는 화면 전체 크기의 막은 제외
                graphic.rectTransform.GetWorldCorners(_corners);
                Vector2 a = Screen2(_container, _corners[0]), c = Screen2(_container, _corners[2]);
                if (Mathf.Abs(c.x - a.x) > Screen.width * 0.9f) continue;
                minX = Mathf.Min(minX, Mathf.Min(a.x, c.x)); maxX = Mathf.Max(maxX, Mathf.Max(a.x, c.x));
                minY = Mathf.Min(minY, Mathf.Min(a.y, c.y)); maxY = Mathf.Max(maxY, Mathf.Max(a.y, c.y));
                any = true;
            }
            if (!any) return false;
            slots = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private bool Fits(out Rect bar, out Rect slots)
        {
            if (!Measure(out bar, out slots)) return true;
            return slots.yMax <= bar.yMax && slots.yMin >= bar.yMin && slots.xMin >= bar.xMin && slots.xMax <= bar.xMax;
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

            // 바 가운데로 옮긴다(가로는 지금 위치 유지)
            float targetY = bar.center.y + bar.height * _verticalBias;
            Vector2 deltaScreen = new Vector2(0f, targetY - slots.center.y);
            var parent = _container.parent as RectTransform;
            // 화면 → 부모 로컬 변환: 오버레이 캔버스는 월드 단위가 화면 픽셀과 같으므로 부모의 크기 비율만 반영한다
            float scale = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.y) : 1f;
            _container.anchoredPosition += new Vector2(deltaScreen.x, deltaScreen.y) / scale;
        }
    }
}
