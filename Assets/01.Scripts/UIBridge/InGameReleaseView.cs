using System.Collections.Generic;
using System.Reflection;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.InGame;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 인게임 씬을 "개발용 화면" 없이 실제 게임처럼 보이게 하는 정리 컴포넌트. InGame 씬에만 배치한다.
    /// - 개발용 IMGUI 오버레이(InGameDummyView)를 끄고, 그 안에 있던 그리드 연결·페이지 전환만 그대로 이어받는다.
    /// - 그리드 프로토타입 UI의 영문 디버그 요소(제목, 상태, 안내문, 개발용 버튼)를 숨긴다. 보관함 초과 정리, 층 확장 안내, 카메라 복구 같은
    ///   실제로 필요한 요소는 그대로 둔다.
    /// 팀 파일(InGameDummyView, GridPrototypeRunner)은 수정하지 않고 리플렉션으로만 읽는다.
    /// </summary>
    public sealed class InGameReleaseView : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] DebugFieldNames =
            { "_status", "_message", "_start", "_skip", "_clear", "_reward", "_unitRewardA", "_unitRewardB" };

        private InGameDummyView _dummyView;
        private InGamePrototypeBootstrap _bootstrap;
        private GridPrototypeRunner _gridView;
        private UIPageGroup _pages;
        private GameObject _battlePage;
        private readonly List<VisualElement> _hidden = new List<VisualElement>();
        private bool _took;
        private bool _subscribed;

        private void Update()
        {
            if (!_took) TryTakeOver();
            if (_took && !_subscribed && _bootstrap != null)
            {
                _bootstrap.Changed += RefreshPages;
                _subscribed = true;
                RefreshPages();
            }
        }

        private void LateUpdate()
        {
            HideDebugElements();
            HideUnusedCurrencyUi();
        }

        [Header("아직 쓰이지 않는 UI")]
        [Tooltip("재화와 리롤은 InGameCurrencyHud가 실제 값으로 연결하므로 기본은 숨기지 않는다. 켜면 리롤 관련 UI를 숨긴다.")]
        [SerializeField] private bool _hideUnusedCurrencyUi = false;
        private float _nextCurrencyScan;
        private int _hiddenCurrencyCount;

        private void HideUnusedCurrencyUi()
        {
            if (!_hideUnusedCurrencyUi || Time.unscaledTime < _nextCurrencyScan) return;
            _nextCurrencyScan = Time.unscaledTime + (_hiddenCurrencyCount > 0 ? 2f : 0.5f);
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (!t.gameObject.scene.IsValid() || !t.gameObject.activeSelf) continue;
                if (t.name.IndexOf("Reroll", System.StringComparison.OrdinalIgnoreCase) < 0 || t.GetComponentInParent<Canvas>(true) == null) continue;
                t.gameObject.SetActive(false);
                _hiddenCurrencyCount++;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && _bootstrap != null) _bootstrap.Changed -= RefreshPages;
        }

        private void TryTakeOver()
        {
            _dummyView = FindFirstObjectByType<InGameDummyView>(FindObjectsInactive.Include);
            if (_dummyView == null) return;
            _bootstrap = Field<InGamePrototypeBootstrap>(_dummyView, "_bootstrap");
            _gridView = Field<GridPrototypeRunner>(_dummyView, "_gridView");
            _pages = Field<UIPageGroup>(_dummyView, "_pages");
            _battlePage = Field<GameObject>(_dummyView, "_battlePage");
            if (_bootstrap == null || _gridView == null || _pages == null)
            {
                Debug.LogWarning("개발용 화면 정리 실패: InGameDummyView의 연결을 읽지 못했습니다. 기존 화면을 그대로 둡니다.", this);
                enabled = false;
                return;
            }
            _dummyView.enabled = false; // OnGUI 오버레이 제거. 그리드 바인딩과 페이지 전환은 아래 RefreshPages가 이어받는다.
            // 증강 선택은 실제 UI(UIAugmentSelectionView)가 같은 제공자를 쓰므로 개발용 IMGUI 선택창은 끈다.
            var augmentDummy = FindFirstObjectByType<InGameAugmentDummyView>(FindObjectsInactive.Include);
            if (augmentDummy != null) augmentDummy.enabled = false;
            _took = true;
        }

        /// <summary>InGameDummyView.Refresh와 같은 규칙 — 준비 단계(또는 보관 대기)에는 그리드를, 아니면 전투 페이지를 보인다.</summary>
        private void RefreshPages()
        {
            if (_gridView == null || _pages == null || _bootstrap == null) return;
            var session = _bootstrap.GridSession;
            if (_gridView.Session != session || (session != null && session.IsEnded))
            {
                _gridView.Unbind();
                if (session != null && !session.IsEnded) _gridView.Bind(session);
            }
            bool showGrid = session != null && !session.IsEnded &&
                (session.Grid.Phase == eGridPhase.PREPARATION || session.Grid.HasPendingStorage);
            _pages.ShowPage(showGrid ? _gridView.gameObject : _battlePage);
        }

        private void HideDebugElements()
        {
            if (_gridView == null) return;
            if (_hidden.Count == 0 || _hidden[0] == null || _hidden[0].panel == null) CollectDebugElements();
            foreach (var element in _hidden)
                if (element != null) element.style.display = DisplayStyle.None; // 그리드 쪽이 매 갱신마다 다시 켜므로 매 프레임 끈다.
        }

        private void CollectDebugElements()
        {
            _hidden.Clear();
            foreach (var name in DebugFieldNames)
                if (_gridView.GetType().GetField(name, Private)?.GetValue(_gridView) is VisualElement element) _hidden.Add(element);
            if (_gridView.GetType().GetField("_root", Private)?.GetValue(_gridView) is VisualElement root)
                foreach (var label in root.Query<Label>().ToList())
                    if (label.text != null && (label.text.StartsWith("CASTLE GRID") || label.text.StartsWith("STORAGE / ")))
                        _hidden.Add(label);
        }

        private static T Field<T>(object owner, string name) where T : class =>
            owner.GetType().GetField(name, Private)?.GetValue(owner) as T;
    }
}
