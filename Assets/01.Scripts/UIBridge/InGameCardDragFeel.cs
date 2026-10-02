using System.Reflection;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 카드를 끌어 전장에 배치할 때의 조작감을 다듬는다.
    /// 1) 끄는 동안 카드가 부드럽게 작아지고 살짝 투명해지며, 마우스 오른쪽 옆을 따라다닌다 — 크게 펼쳐진 카드가 전장 칸을 가리지 않는다.
    ///    카드를 마우스 바로 밑에 붙이면 안 된다. 놓을 때 게임이 "마우스가 시작 카드 위인가"를 보고 보관함 복귀로 처리하기 때문이다.
    ///    그래서 카드는 마우스에 닿지 않게 옆(_sideGap만큼 떨어진 곳)에 두고, 빨리 움직여도 카드가 마우스를 덮지 않게 빠르게 따라붙는다.
    /// 2) 손패 드롭 영역(이 영역 안에서 놓으면 "보관함으로 되돌림"으로 처리됨)이 화면 아래 약 1/3로 너무 높아서,
    ///    전장 아래쪽 칸에 놓아도 보관함 복귀로 처리되던 문제가 있었다. 실제 카드가 보이는 높이만큼으로 줄인다.
    /// UI 스크립트는 수정하지 않고 리플렉션으로만 읽고 쓴다.
    /// </summary>
    public sealed class InGameCardDragFeel : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Header("카드 드래그")]
        [SerializeField, Range(0.3f, 1f)] private float _dragScale = 0.5f;
        [SerializeField, Range(0.3f, 1f)] private float _dragAlpha = 0.9f;
        [Tooltip("카드가 작아지고 따라붙는 빠르기. 클수록 즉각적이다.")]
        [SerializeField, Min(5f)] private float _followSpeed = 45f;
        [Tooltip("마우스와 카드 사이 간격(픽셀). 너무 줄이면 빠르게 움직일 때 카드가 마우스를 덮어 배치가 보관으로 처리될 수 있다.")]
        [SerializeField, Min(8f)] private float _sideGap = 44f;
        [Header("손패 드롭 영역")]
        [Tooltip("손패에서 '보관함 복귀'로 처리되는 영역의 높이(캔버스 단위). 원래는 350이라 전장 아래쪽 칸까지 덮었다. 0이면 건드리지 않는다.")]
        [SerializeField, Min(0f)] private float _dropZoneHeight = 190f;

        private UIBattleCardHandView _hand;
        private bool _zoneFit;
        private FieldInfo _visualRootField;
        private UIBattleCardHandSlot _lastDragged;

        private void Update()
        {
            if (_hand == null) _hand = FindFirstObjectByType<UIBattleCardHandView>(FindObjectsInactive.Include);
            if (_hand == null) return;
            if (!_zoneFit && _dropZoneHeight > 0f) FitDropZone();
        }

        private void LateUpdate()
        {
            if (_hand == null) return;
            UIBattleCardHandSlot dragging = null;
            foreach (var slot in _hand.Slots)
                if (slot != null && slot.IsDragging) { dragging = slot; break; }

            if (dragging != null)
            {
                ApplyDragLook(dragging);
                _lastDragged = dragging;
            }
            else if (_lastDragged != null)
            {
                RestoreAlpha(_lastDragged); // 크기·위치는 슬롯 자체 애니메이션이 원래 자리로 부드럽게 돌려놓는다
                _lastDragged = null;
            }
        }

        private void FitDropZone()
        {
            var dropRect = typeof(UIBattleCardHandView).GetField("_dropZone", Private)?.GetValue(_hand) as RectTransform;
            if (dropRect != null)
            {
                var size = dropRect.sizeDelta;
                if (size.y > _dropZoneHeight) dropRect.sizeDelta = new Vector2(size.x, _dropZoneHeight);
            }
            _zoneFit = true;
        }

        private RectTransform VisualRoot(UIBattleCardHandSlot slot)
        {
            if (_visualRootField == null) _visualRootField = typeof(UIBattleCardHandSlot).GetField("_visualRoot", Private);
            return _visualRootField?.GetValue(slot) as RectTransform;
        }

        private void ApplyDragLook(UIBattleCardHandSlot slot)
        {
            var visual = VisualRoot(slot);
            if (visual == null) return;
            float follow = 1f - Mathf.Exp(-_followSpeed * Time.unscaledDeltaTime); // 프레임 시간과 무관하게 같은 빠르기

            // 크기·투명도는 부드럽게 바뀐다(딱 끊기지 않게)
            visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * _dragScale, follow);
            var group = visual.GetComponent<CanvasGroup>();
            if (group == null) group = visual.gameObject.AddComponent<CanvasGroup>();
            group.alpha = Mathf.Lerp(group.alpha, _dragAlpha, follow);

            if (Mouse.current == null) return;
            var parent = visual.parent as RectTransform;
            var canvas = visual.GetComponentInParent<Canvas>();
            if (parent == null || canvas == null) return;
            var root = canvas.rootCanvas;
            Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

            // 카드 폭의 절반 + 간격만큼 마우스 오른쪽 옆에 둔다 → 마우스가 카드 위에 올라가지 않는다.
            var corners = new Vector3[4];
            visual.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            float halfWidth = Mathf.Abs(b.x - a.x) * 0.5f;
            float halfHeight = Mathf.Abs(b.y - a.y) * 0.5f;
            Vector2 pointer = Mouse.current.position.ReadValue();
            Vector2 target = pointer + new Vector2(halfWidth + _sideGap, halfHeight * 0.3f);
            if (target.x + halfWidth > Screen.width) target.x = pointer.x - halfWidth - _sideGap; // 화면 오른쪽 끝이면 왼쪽 옆으로

            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, target, cam, out var world)) return;
            visual.position = Vector3.Lerp(visual.position, world, follow); // 손패에서 마우스 옆까지 부드럽게 날아온다
        }

        private void RestoreAlpha(UIBattleCardHandSlot slot)
        {
            var visual = VisualRoot(slot);
            if (visual == null) return;
            var group = visual.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
        }
    }
}
