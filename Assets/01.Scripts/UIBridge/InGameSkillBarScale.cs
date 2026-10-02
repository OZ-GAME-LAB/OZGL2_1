using System.Reflection;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 하단 스킬 슬롯의 크기만 줄인다. 위치는 원래 자리 그대로다.
    /// 슬롯 하나하나를 "아래쪽 가운데"를 기준으로 축소하므로 바닥에서 뜨거나 옆으로 밀리지 않고, 슬롯 사이 간격만 줄어든 폭만큼 보정한다.
    /// 컨테이너(레이아웃 그룹)는 건드리지 않는다. _scale 1이면 원래 크기, 0.7이면 70% 크기.
    /// </summary>
    public sealed class InGameSkillBarScale : MonoBehaviour
    {
        [SerializeField, Range(0.3f, 1.2f)] private float _scale = 0.7f;
        private RectTransform _container;
        private HorizontalLayoutGroup _layout;
        private float _originalSpacing;
        private bool _hasOriginalSpacing;
        private float _nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.5f;
            if (_container == null && !FindContainer()) return;

            bool changed = false;
            float width = 0f;
            foreach (var view in FindObjectsByType<UICombatSkillSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var root = DirectChild(view.transform, _container);
                if (root == null) continue;
                if (width <= 0f) width = root.rect.width;
                if (Mathf.Abs(root.localScale.x - _scale) < 0.001f && Mathf.Approximately(root.pivot.y, 0f)) continue;
                root.pivot = new Vector2(root.pivot.x, 0f); // 아래쪽 가운데가 제자리에 남도록
                root.localScale = new Vector3(_scale, _scale, 1f);
                changed = true;
            }

            if (_layout != null && width > 0f)
            {
                // 줄어든 만큼 슬롯 사이가 벌어지므로 간격을 같은 폭만큼 줄인다.
                if (!_hasOriginalSpacing || _layout.spacing > _originalSpacing + 0.01f)
                {
                    _originalSpacing = _layout.spacing; // 컨트롤러가 간격을 다시 쓰면 새 기준으로 삼는다
                    _hasOriginalSpacing = true;
                }
                float wanted = _originalSpacing - (1f - _scale) * width;
                if (Mathf.Abs(_layout.spacing - wanted) > 0.01f) { _layout.spacing = wanted; changed = true; }
            }
            if (changed) LayoutRebuilder.MarkLayoutForRebuild(_container);
        }

        private bool FindContainer()
        {
            var controller = FindFirstObjectByType<UIInGameSkillBarController>(FindObjectsInactive.Include);
            if (controller == null) return false;
            _container = typeof(UIInGameSkillBarController)
                .GetField("_slotContainer", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(controller) as RectTransform;
            if (_container == null) { enabled = false; return false; }
            _layout = _container.GetComponent<HorizontalLayoutGroup>();
            return true;
        }

        private static RectTransform DirectChild(Transform node, RectTransform container)
        {
            while (node != null && node.parent != container) node = node.parent;
            return node as RectTransform;
        }

        private void OnDestroy()
        {
            if (_container == null) return;
            foreach (var view in FindObjectsByType<UICombatSkillSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var root = DirectChild(view.transform, _container);
                if (root != null) root.localScale = Vector3.one;
            }
            if (_layout != null && _hasOriginalSpacing) _layout.spacing = _originalSpacing;
        }
    }
}
