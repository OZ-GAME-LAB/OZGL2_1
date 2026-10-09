using UnityEngine;
using UnityEngine.UI;
using OZGL2.UIFlow;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 하단 스킬 슬롯(원형 아이콘 + 이름)이 아래쪽 바(BottomSkillBackground) 밖으로 삐져나오지 않게 맞춘다.
    /// 슬롯·프레임·이름표의 고정 영역만 측정한다. 움직이는 쿨타임 효과는 배치에 참여하지 않는다.
    /// 슬롯 구성/화면 크기/화면 활성 상태가 바뀔 때 원래 배치에서 다시 계산하고 가로·세로 중심을 함께 보정한다.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    public sealed class InGameSkillBarFit : MonoBehaviour
    {
        private const string ContainerName = "SkillSlots";
        private const string BarName = "BottomSkillBackground";
        private const float LEGACY_LAYOUT_CHECK_INTERVAL = 0.5f;
        private const float LAYOUT_SLACK_PIXELS = 8f;

        [SerializeField, Range(0.6f, 1f), Tooltip("바 높이 중 슬롯이 차지해도 되는 비율")] private float _fillHeight = 0.78f;
        [SerializeField, Range(0.6f, 1f), Tooltip("바 폭 중 슬롯이 차지해도 되는 비율")] private float _fillWidth = 0.96f;
        [SerializeField, Range(-0.15f, 0.15f), Tooltip("바 가운데에서 위(+)/아래(-)로 옮기는 비율(바 높이 기준)")] private float _verticalBias = -0.06f;

        private RectTransform _container, _bar;
        private UIInGameSkillBarController _controller;
        private Vector3 _baseScale;
        private Vector2 _basePosition;
        private int _lastChildCount = -1;
        private Vector2Int _lastScreen;
        private Vector2 _lastContainerSize, _lastBarSize;
        private int _lastLayoutVersion = -1;
        private float _nextLegacyLayoutCheckTime;
        private bool _wasActive;
        private bool _hasBaseLayout;
        private readonly Vector3[] _corners = new Vector3[4];

        private void LateUpdate()
        {
            if (_controller != null && _controller.SlotContainer != _container)
            {
                if (_container != null && _hasBaseLayout)
                {
                    _container.localScale = _baseScale;
                    _container.anchoredPosition = _basePosition;
                }
                _container = null;
                _bar = null;
                _hasBaseLayout = false;
            }

            if (_container == null || _bar == null)
            {
                _controller = FindFirstObjectByType<UIInGameSkillBarController>(FindObjectsInactive.Include);
                _container = _controller != null ? _controller.SlotContainer : null;
                if (_container == null)
                {
                    foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
                        if (rect != null && rect.gameObject.scene.IsValid() &&
                            rect.gameObject.activeInHierarchy && rect.name == ContainerName) { _container = rect; break; }
                }
                if (_container == null) return;
                var canvas = _container.GetComponentInParent<Canvas>();
                if (canvas != null)
                    foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
                        if (rect.name == BarName) { _bar = rect; break; }
                if (_bar == null) return;
                _baseScale = _container.localScale;
                _basePosition = _container.anchoredPosition;
                _hasBaseLayout = true;
                _lastChildCount = -1;
                _wasActive = false;
            }
            if (!_container.gameObject.activeInHierarchy || !_bar.gameObject.activeInHierarchy)
            {
                _wasActive = false;
                return;
            }

            var screen = new Vector2Int(Screen.width, Screen.height);
            UICombatSkillSlotView[] slots = _container.GetComponentsInChildren<UICombatSkillSlotView>(false);
            int version = _controller != null ? _controller.LayoutVersion : 0;
            bool changed = !_wasActive || screen != _lastScreen || slots.Length != _lastChildCount ||
                version != _lastLayoutVersion || _container.rect.size != _lastContainerSize || _bar.rect.size != _lastBarSize;
            _wasActive = true;
            _lastScreen = screen;
            _lastChildCount = slots.Length;
            _lastLayoutVersion = version;
            _lastContainerSize = _container.rect.size;
            _lastBarSize = _bar.rect.size;
            if (slots.Length == 0) return;
            if (changed)
            {
                _nextLegacyLayoutCheckTime = Time.unscaledTime + LEGACY_LAYOUT_CHECK_INTERVAL;
                Solve(slots);
                return;
            }

            // 기존 씬 슬롯은 다른 배치 컴포넌트가 위치를 바꿀 수 있어 주기적으로 확인한다.
            if (_controller != null && _controller.UsesSlotPrefab) return;
            if (Time.unscaledTime < _nextLegacyLayoutCheckTime) return;
            _nextLegacyLayoutCheckTime = Time.unscaledTime + LEGACY_LAYOUT_CHECK_INTERVAL;
            if (!Fits(slots)) Solve(slots);
        }

        private static Vector2 Screen2(RectTransform rt, Vector3 world)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(cam, world);
        }

        /// <summary>배치 경계는 스킬/쿨타임 표시 상태와 무관한 고정 Rect로만 구한다.</summary>
        private bool Measure(UICombatSkillSlotView[] views, out Rect bar, out Rect slots)
        {
            bar = slots = default;
            _bar.GetWorldCorners(_corners);
            Vector2 b0 = Screen2(_bar, _corners[0]), b1 = Screen2(_bar, _corners[2]);
            bar = Rect.MinMaxRect(Mathf.Min(b0.x, b1.x), Mathf.Min(b0.y, b1.y), Mathf.Max(b0.x, b1.x), Mathf.Max(b0.y, b1.y));

            bool any = false;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var view in views)
            {
                RectTransform root = FindSlotRoot(view);
                if (root == null) continue;
                IncludeRect(root, ref minX, ref minY, ref maxX, ref maxY);
                IncludeRect(view.FrameLayoutRect, ref minX, ref minY, ref maxX, ref maxY);
                IncludeRect(view.SkillNameText != null ? view.SkillNameText.rectTransform : null,
                    ref minX, ref minY, ref maxX, ref maxY);
                any = true;
            }
            if (!any) return false;
            slots = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        /// <summary>이름표의 폭이나 위치와 무관하게 슬롯 칸들의 가로 중심을 구한다.</summary>
        private bool MeasureRoots(UICombatSkillSlotView[] views, out Rect roots)
        {
            roots = default;
            bool any = false;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var view in views)
            {
                RectTransform root = FindSlotRoot(view);
                if (root == null) continue;
                IncludeRect(root, ref minX, ref minY, ref maxX, ref maxY);
                any = true;
            }
            if (!any) return false;
            roots = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private RectTransform FindSlotRoot(UICombatSkillSlotView view)
        {
            if (view == null) return null;
            Transform root = view.transform;
            while (root.parent != null && root.parent != _container) root = root.parent;
            return root.parent == _container && root.gameObject.activeInHierarchy ? root as RectTransform : null;
        }

        private bool Fits(UICombatSkillSlotView[] views)
        {
            if (!Measure(views, out var bar, out var slots)) return true;
            if (slots.yMax > bar.yMax + LAYOUT_SLACK_PIXELS || slots.yMin < bar.yMin - LAYOUT_SLACK_PIXELS ||
                slots.xMin < bar.xMin - LAYOUT_SLACK_PIXELS || slots.xMax > bar.xMax + LAYOUT_SLACK_PIXELS)
                return false;
            return !MeasureRoots(views, out var roots) || Mathf.Abs(roots.center.x - bar.center.x) <= LAYOUT_SLACK_PIXELS;
        }

        private void IncludeRect(RectTransform rect, ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            if (rect == null) return;
            rect.GetWorldCorners(_corners);
            for (int i = 0; i < _corners.Length; i++)
            {
                Vector2 p = Screen2(_container, _corners[i]);
                minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
            }
        }

        private void Solve(UICombatSkillSlotView[] views)
        {
            // 처음 값에서 다시 시작해 몇 번을 맞춰도 같은 결과가 나오게 한다
            _container.localScale = _baseScale;
            _container.anchoredPosition = _basePosition;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
            if (!Measure(views, out var bar, out var slots)) return;

            float k = Mathf.Min(1f, bar.height * _fillHeight / Mathf.Max(1f, slots.height), bar.width * _fillWidth / Mathf.Max(1f, slots.width));
            _container.localScale = _baseScale * k;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
            if (!Measure(views, out bar, out slots)) return;

            Vector2 target = bar.center + new Vector2(0f, bar.height * _verticalBias);
            Vector2 currentCenter = slots.center;
            if (MeasureRoots(views, out var roots)) currentCenter.x = roots.center.x;
            var parent = _container.parent as RectTransform;
            if (parent == null) return;
            var canvas = _container.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, currentCenter, camera, out var currentLocal) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, target, camera, out var targetLocal))
                _container.anchoredPosition += targetLocal - currentLocal;
        }
    }
}
