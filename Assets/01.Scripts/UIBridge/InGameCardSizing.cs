using System.Reflection;
using TMPro;
using UnityEngine.UI;
using OZGL2.Stage;
using OZGL2.InGame;
using OZGL2.UIFlow;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 손패 카드를 더 크고 깔끔하게 보여 준다. UI 프리팹은 수정하지 않고, 손패 뷰가 이미 갖고 있는 배치 값(카드 크기·간격·호버)만 실행 중에 바꾼다.
    /// - 평소(배치·보관함): 카드를 키우고 겹침을 줄인다. 마우스를 올리면 부드럽게 커진다.
    /// - 카드 보상 선택 중: 카드가 가려지지 않게 위로 올리고, 겹치지 않게 간격을 벌려 3장이 한눈에 보이게 한다.
    /// 값을 바꾸면 이미 만들어진 카드는 옛 크기이므로, 처음 적용할 때 손패를 한 번 비워 새 크기로 다시 만든다.
    /// </summary>
    public sealed class InGameCardSizing : MonoBehaviour
    {
        [Header("평소")]
        [SerializeField, Min(0.1f)] private float _cardScale = 0.34f;
        [SerializeField, Min(1f)] private float _hoverScale = 1.3f;
        [SerializeField, Min(0f)] private float _hoverRise = 170f;
        [SerializeField] private float _spacing = -16f;
        [SerializeField, Tooltip("0이면 원래 씬 값을 그대로 쓴다")] private float _restingYOffset = 0f;
        [Header("카드 보상 선택 중")]
        [SerializeField, Min(1f), Tooltip("카드 보상 선택 중 손패 전체를 키우는 배율. 화면 가운데에 크게 보인다.")]
        private float _rewardPanelScale = 1.7f;
        [SerializeField, Range(0f, 0.9f), Tooltip("보상 선택 중 뒤 화면을 어둡게 하는 정도")]
        private float _rewardDim = 0.62f;
        [SerializeField] private float _rewardRestingYOffset = 8f;
        [SerializeField, Range(0f, 0.2f), Tooltip("보상 카드를 화면 가운데에서 위로 올리는 정도(화면 높이 비율). 아래 리롤 버튼을 가리지 않게 한다.")]
        private float _rewardRaise = 0.085f;
        [SerializeField, Min(1f)] private float _rewardHoverScale = 1.12f;
        [SerializeField, Min(0f)] private float _rewardHoverRise = 24f;
        [SerializeField] private float _rewardSpacing = 0f;

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly System.Type HandType = typeof(UIBattleCardHandView);
        private static readonly FieldInfo FScale = HandType.GetField("_cardScale", Priv), FHover = HandType.GetField("_hoverScale", Priv),
            FRise = HandType.GetField("_hoverRise", Priv), FSpace = HandType.GetField("_comfortableSpacing", Priv),
            FRest = HandType.GetField("_restingYOffset", Priv), FSize = HandType.GetField("_cardSize", Priv);
        private static readonly MethodInfo MInit = HandType.GetMethod("EnsureInitialized", Priv),
            MCommon = HandType.GetMethod("CalculateCommonCardSize", Priv);

        private static readonly FieldInfo FViewport = HandType.GetField("_viewport", Priv), FBottom = HandType.GetField("_bottomPadding", Priv);

        private UIBattleCardHandView _view;
        private RectTransform _panel;
        private Vector3 _panelScale;
        private Vector2 _panelPos;
        private Canvas _dimCanvas;
        private Image _dim;
        private TMP_Text _dimTitle;
        private float _dimAlpha;
        private InGamePrototypeBootstrap _bootstrap;
        private bool _baseApplied, _rewardMode;
        private float _originalRest;
        private float _next;

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.1f;
            if (FScale == null || FHover == null || FRise == null || FSpace == null || FRest == null || FSize == null || MInit == null || MCommon == null)
            { enabled = false; return; }

            if (_view == null) _view = FindFirstObjectByType<UIBattleCardHandView>(FindObjectsInactive.Include);
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (_view == null) return;

            if (!_baseApplied) ApplyBase();

            bool reward = _bootstrap != null && _bootstrap.Stage != null && _bootstrap.Stage.State == eStageState.GENERAL_REWARD;
            if (reward != _rewardMode)
            {
                _rewardMode = reward;
                ApplyMode();
                if (reward) EnterCenter(); else ExitCenter();
            }
        }

        private void Update()
        {
            // 어두운 막과 안내 글은 부드럽게 나타나고 사라진다
            if (_dim == null && !_rewardMode) return;
            float target = _rewardMode ? _rewardDim : 0f;
            _dimAlpha = Mathf.MoveTowards(_dimAlpha, target, Time.unscaledDeltaTime * 2.5f);
            if (_dim != null)
            {
                _dim.color = new Color(0f, 0f, 0f, _dimAlpha);
                if (_dimTitle != null)
                {
                    float k = _rewardDim > 0f ? _dimAlpha / _rewardDim : 0f;
                    _dimTitle.color = new Color(1f, 0.88f, 0.55f, k);
                }
                if (_dimAlpha <= 0.001f && !_rewardMode) _dimCanvas.enabled = false;
            }
        }

        /// <summary>손패 전체를 키워 화면 가운데로 옮기고, 뒤를 어둡게 한다. 카드가 뷰포트 세로 한가운데 오도록 놓아 계산이 한 번에 끝난다.</summary>
        private void EnterCenter()
        {
            _panel = FViewport != null ? (FViewport.GetValue(_view) as RectTransform)?.parent as RectTransform : null;
            var viewport = FViewport != null ? FViewport.GetValue(_view) as RectTransform : null;
            if (_panel == null || viewport == null) return;
            var canvas = _panel.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _panelScale = _panel.localScale;
            _panelPos = _panel.anchoredPosition;

            // 카드 세로 중심을 뷰포트 한가운데에 맞춘다: 가운데 y = 아래 여백 + 카드 반높이 + 쉬는 위치 보정
            float cardHalf = ((Vector2)FSize.GetValue(_view)).y * 0.5f;
            float bottom = FBottom != null ? (float)FBottom.GetValue(_view) : 0f;
            float rest = viewport.rect.height * 0.5f - bottom - cardHalf;
            FRest.SetValue(_view, rest);
            _view.RefreshLayoutImmediate();

            _panel.localScale = _panelScale * _rewardPanelScale;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
            Vector2 center = (corners[0] + corners[2]) * 0.5f;
            Vector2 screen = new Vector2(Screen.width * 0.5f, Screen.height * (0.5f + _rewardRaise));
            _panel.anchoredPosition = _panelPos + (screen - center) / Mathf.Max(0.01f, canvas.scaleFactor);

        }

        private void ExitCenter()
        {
            if (_panel == null) return;
            _panel.localScale = _panelScale;
            _panel.anchoredPosition = _panelPos;
            _view.RefreshLayoutImmediate();
            _panel = null;
        }

        private void EnsureDim()
        {
            if (_dimCanvas != null) return;
            var go = new GameObject("RewardCardBackdrop", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _dimCanvas = go.AddComponent<Canvas>();
            _dimCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _dimCanvas.sortingOrder = 8; // 손패(10~11) 바로 아래

            var bgGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)bgGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            _dim = bgGo.GetComponent<Image>();
            _dim.color = new Color(0f, 0f, 0f, 0f);
            _dim.raycastTarget = false;

            var tgo = new GameObject("Title", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            var tr = (RectTransform)tgo.transform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(1400f, 120f);
            tr.anchoredPosition = new Vector2(0f, -Screen.height * 0.07f);
            _dimTitle = tgo.AddComponent<TextMeshProUGUI>();
            if (UiFontOverride.Current != null) _dimTitle.font = UiFontOverride.Current;
            else
                foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                    if (font != null && font.HasCharacters("카드를선택하세요", out _, true, true)) { _dimTitle.font = font; break; }
            _dimTitle.fontSize = 56f * Mathf.Max(0.8f, Screen.height / 1080f);
            _dimTitle.alignment = TextAlignmentOptions.Center;
            _dimTitle.text = "마왕군 카드를 하나 고르세요";
            _dimTitle.raycastTarget = false;
            _dimTitle.color = new Color(1f, 0.88f, 0.55f, 0f);
            _dimCanvas.enabled = false;
        }

        private void ApplyBase()
        {
            MInit.Invoke(_view, null);
            _originalRest = (float)FRest.GetValue(_view);
            FScale.SetValue(_view, _cardScale);
            FSize.SetValue(_view, (Vector2)MCommon.Invoke(_view, null));
            ApplyMode();
            // 이미 만들어진 카드는 옛 크기라, 비웠다가 어댑터가 다시 채우게 한다
            _view.Clear();
            var adapter = FindFirstObjectByType<UIGridStorageHandAdapter>(FindObjectsInactive.Include);
            adapter?.GetType().GetMethod("RefreshItems", Priv)?.Invoke(adapter, null);
            _baseApplied = true;
        }

        private void ApplyMode()
        {
            FHover.SetValue(_view, _rewardMode ? _rewardHoverScale : _hoverScale);
            FRise.SetValue(_view, _rewardMode ? _rewardHoverRise : _hoverRise);
            FSpace.SetValue(_view, _rewardMode ? _rewardSpacing : _spacing);
            if (!_rewardMode) FRest.SetValue(_view, Mathf.Approximately(_restingYOffset, 0f) ? _originalRest : _restingYOffset);
            _view.RefreshLayoutImmediate();
        }

        private void OnDestroy()
        {
            if (_dimCanvas != null) Destroy(_dimCanvas.gameObject);
        }
    }
}
