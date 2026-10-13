using System.Collections.Generic;
using OZGL2.Grid;
using OZGL2.InGame;
using OZGL2.Stage;
using OZGL2.UIBridge;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>카드 보상의 전장 보기와 허용 입력만 관리합니다. 후보, 보상 지급, 단계는 변경하지 않습니다.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class UICardRewardPresentation : MonoBehaviour, ICanvasRaycastFilter
    {
        private const int SORTING_ORDER = 50;
        private const float TRANSITION_SECONDS = 0.22f;
        private static UICardRewardPresentation _current;

        private InGamePrototypeBootstrap _bootstrap;
        private UIBattleCardHandView _hand;
        private InGameCurrencyHud _currency;
        private Canvas _canvas;
        private Button _toggle;
        private TMP_Text _label;
        private TMP_Text _hint;
        private string _requestId;
        private bool _isCollapsed;
        private float _collapseProgress;
        private readonly Dictionary<Button, Navigation> _savedNavigation = new Dictionary<Button, Navigation>();

        public static bool IsRewardActive => _current != null && _current.isActiveAndEnabled && _current.HasPendingReward();
        public static bool IsCollapsed => IsRewardActive && _current._isCollapsed;
        public static bool CanSelectReward => !IsRewardActive ||
            (!_current._isCollapsed && _current._collapseProgress <= 0.001f && !_current.IsPopupBlocking);
        public static bool CanInspectSynergy => !IsRewardActive ||
            (_current._isCollapsed && _current._collapseProgress >= 0.999f && !_current.IsPopupBlocking);
        public float CollapseProgress => _collapseProgress;
        private bool IsPopupBlocking => _bootstrap != null && _bootstrap.IsUiInputBlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCurrent() => _current = null;

        public void Configure(InGamePrototypeBootstrap bootstrap, UIBattleCardHandView hand)
        {
            _bootstrap = bootstrap;
            _hand = hand;
            _current = this;
        }

        private bool HasPendingReward()
        {
            var session = _bootstrap != null ? _bootstrap.GridSession : null;
            var rewards = _bootstrap != null ? _bootstrap.Rewards : null;
            var grid = session != null ? session.Grid : null;
            return _bootstrap != null && _bootstrap.Stage != null &&
                _bootstrap.Stage.State == eStageState.GENERAL_REWARD && rewards?.Pending != null &&
                session != null && session.PendingRewardId == rewards.Pending.RequestId && grid != null &&
                grid.Phase == eGridPhase.REWARD && !grid.HasPendingStorage && !grid.RequiresExpansionPlacement;
        }

        private void Update()
        {
            if (!HasPendingReward())
            {
                _isCollapsed = false;
                _collapseProgress = 0f;
                _requestId = null;
                if (_canvas != null) _canvas.enabled = false;
                RestoreNavigation();
                return;
            }

            SynchronizeRequest();
            EnsureUi();
            _canvas.enabled = true;
            if (_currency == null) _currency = FindFirstObjectByType<InGameCurrencyHud>();
            _collapseProgress = Mathf.MoveTowards(_collapseProgress, _isCollapsed ? 1f : 0f,
                Time.unscaledDeltaTime / TRANSITION_SECONDS);
            _toggle.interactable = !IsPopupBlocking && (_currency == null || !_currency.IsRerollInProgress);
            _label.text = _isCollapsed ? "보상 보기" : "전장 보기";
            _hint.gameObject.SetActive(_isCollapsed);
            RestrictNavigation();

            // 보상 전에 선택되어 있던 배경 버튼이 Submit 키로 실행되지 않게 합니다.
            if (!IsPopupBlocking && EventSystem.current != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && !IsAllowedSelection(selected.transform))
                    EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void ToggleCollapsed()
        {
            if (!HasPendingReward() || IsPopupBlocking || (_currency != null && _currency.IsRerollInProgress)) return;
            // 새 요청이 들어온 첫 프레임의 클릭도 다음 Update에서 초기화되지 않게 합니다.
            SynchronizeRequest();
            EnsureUi();
            _isCollapsed = !_isCollapsed;
            _hand?.ResetTransientInteraction();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_toggle.gameObject);
        }

        private void SynchronizeRequest()
        {
            string requestId = _bootstrap.Rewards.Pending.RequestId;
            if (_requestId == requestId) return;
            _requestId = requestId;
            _isCollapsed = false;
            _collapseProgress = 0f;
            _hand?.ResetTransientInteraction();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!HasPendingReward()) return false;
            if (_toggle != null && Contains(_toggle.transform as RectTransform, screenPoint)) return true;
            if (!CanSelectReward) return true;
            Camera handCamera = CanvasCamera(_hand != null ? _hand.transform : null);
            if (_hand != null && _hand.TryGetSlotUnderScreenPoint(screenPoint, handCamera, out _)) return false;
            if (_currency != null)
                foreach (Button button in _currency.RerollButtons)
                    if (button != null && button.isActiveAndEnabled && Contains(button.transform as RectTransform, screenPoint))
                        return false;
            return true;
        }

        private bool IsAllowedSelection(Transform selected)
        {
            if (_toggle != null && (selected == _toggle.transform || selected.IsChildOf(_toggle.transform))) return true;
            if (!CanSelectReward) return false;
            if (_hand != null && selected.IsChildOf(_hand.transform)) return true;
            if (_currency != null)
                foreach (Button button in _currency.RerollButtons)
                    if (button != null && (selected == button.transform || selected.IsChildOf(button.transform))) return true;
            return false;
        }

        private void RestrictNavigation()
        {
            if (_currency == null) return;
            foreach (Button button in _currency.RerollButtons)
            {
                if (button == null || _savedNavigation.ContainsKey(button)) continue;
                _savedNavigation.Add(button, button.navigation);
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
            }
        }

        private void RestoreNavigation()
        {
            foreach (var pair in _savedNavigation)
                if (pair.Key != null) pair.Key.navigation = pair.Value;
            _savedNavigation.Clear();
        }

        private static Camera CanvasCamera(Transform target)
        {
            Canvas canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
            return canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera : null;
        }

        private static bool Contains(RectTransform target, Vector2 screenPoint) => target != null &&
            target.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(target, screenPoint, CanvasCamera(target));

        private void EnsureUi()
        {
            if (_canvas != null) return;
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SORTING_ORDER;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            Image blocker = gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = true;

            var buttonObject = new GameObject("ToggleBattlefieldButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 60f);
            rect.sizeDelta = new Vector2(280f, 74f);
            Image image = buttonObject.GetComponent<Image>();
            var style = UIHeraldrySupplement.Style;
            image.sprite = style != null ? style.DarkButtonSprite : null;
            image.color = image.sprite != null ? Color.white : new Color32(45, 24, 26, 255);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            _toggle = buttonObject.GetComponent<Button>();
            _toggle.targetGraphic = image;
            _toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            _toggle.onClick.AddListener(ToggleCollapsed);
            _label = CreateLabel(buttonObject.transform, "Label", 30f);
            _label.rectTransform.anchorMin = Vector2.zero;
            _label.rectTransform.anchorMax = Vector2.one;
            _label.rectTransform.offsetMin = new Vector2(20f, 4f);
            _label.rectTransform.offsetMax = new Vector2(-20f, -4f);
            _hint = CreateLabel(transform, "InspectionHint", 22f);
            _hint.text = "시너지에 마우스를 올려 효과를 확인하세요";
            _hint.rectTransform.anchorMin = _hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _hint.rectTransform.anchoredPosition = new Vector2(0f, 118f);
            _hint.rectTransform.sizeDelta = new Vector2(760f, 40f);
        }

        private static TMP_Text CreateLabel(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(FontUnifyIgnore));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            UIHeraldrySupplement.ApplyBody(label);
            label.fontSize = size;
            label.color = new Color32(245, 231, 193, 255);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private void OnDisable()
        {
            RestoreNavigation();
            if (_canvas != null) _canvas.enabled = false;
            if (_current == this) _current = null;
        }

        private void OnDestroy()
        {
            if (_toggle != null) _toggle.onClick.RemoveListener(ToggleCollapsed);
        }
    }
}
