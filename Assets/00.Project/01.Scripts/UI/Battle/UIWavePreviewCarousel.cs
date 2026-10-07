using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using OZGL2.InGame;
using OZGL2.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>전체 웨이브 목록을 읽고, 고정된 표시 칸 안에서 한 종류씩 이동한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UIWavePreviewCarousel : MonoBehaviour
    {
        private const float SOURCE_RETRY_INTERVAL = 0.5f;

        [Serializable]
        private sealed class PortraitOverride
        {
            [SerializeField] private string _heroId;
            [SerializeField] private Sprite _portrait;
            public string HeroId => _heroId;
            public Sprite Portrait => _portrait;
        }

        private sealed class EnemyEntry
        {
            public readonly string HeroId;
            public readonly string DisplayName;
            public readonly Sprite Portrait;
            public int Count;

            public EnemyEntry(string heroId, string displayName, Sprite portrait)
            {
                HeroId = heroId;
                DisplayName = displayName;
                Portrait = portrait;
            }
        }

        [Header("표시 전용 영역")]
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _content;
        [SerializeField] private UIWavePreviewEnemyItem _itemTemplate;
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private RectTransform[] _separators = Array.Empty<RectTransform>();
        [SerializeField, Min(1)] private int _visibleCount = 3;

        [Header("기존 HUD 참조는 보존하고 표시만 숨긴다")]
        [SerializeField] private GameObject[] _legacyRows = Array.Empty<GameObject>();
        [SerializeField] private GameObject _designTimePreview;

        [Header("읽기 전용 데이터 연결")]
        [SerializeField] private InGamePrototypeBootstrap _bootstrap;
        [SerializeField] private UIUnitCatalogSO _unitCatalog;
        [SerializeField] private PortraitOverride[] _portraitOverrides = Array.Empty<PortraitOverride>();

        [Header("한 칸 이동")]
        [SerializeField, Min(0f)] private float _duration = 0.2f;
        [SerializeField] private Ease _ease = Ease.OutCubic;

        private readonly List<EnemyEntry> _entries = new List<EnemyEntry>();
        private readonly List<UIWavePreviewEnemyItem> _items = new List<UIWavePreviewEnemyItem>();
        private readonly Dictionary<string, UIUnitCatalogSO.Entry> _catalogById =
            new Dictionary<string, UIUnitCatalogSO.Entry>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroPoolEntry> _heroesById =
            new Dictionary<string, HeroPoolEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _portraitsById =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly List<UIPopupController> _popupControllers = new List<UIPopupController>();

        private InGamePrototypeBootstrap _subscribedBootstrap;
        private HeroPoolCatalogSO _heroCatalog;
        private UIWavePreviewDisclosure _disclosure;
        private StageManager _shownStage;
        private string _shownRunId;
        private int _shownRoundNumber;
        private int _firstVisibleIndex;
        private float _itemWidth;
        private Vector2 _contentOrigin;
        private bool _hasInitializedLayout;
        private bool _hasResolvedSceneUi;
        private bool _hasWaveSnapshot;
        private Coroutine _sourceRetry;
        private Tween _transition;

        public int FirstVisibleIndex => _firstVisibleIndex;
        public int EntryCount => _entries.Count;
        public int VisibleCount => Mathf.Min(Mathf.Max(1, _visibleCount), _entries.Count);

        private void OnEnable()
        {
            HideLegacyPresentation();
            BuildPortraitLookup();
            InitializeLayout();
            if (_previousButton != null) _previousButton.onClick.AddListener(MovePrevious);
            if (_nextButton != null) _nextButton.onClick.AddListener(MoveNext);
            ResolveSceneReferences();
            SubscribeToBootstrap();
            RefreshWave();
            EnsureSourceRetry();
        }

        private void OnDisable()
        {
            if (_previousButton != null) _previousButton.onClick.RemoveListener(MovePrevious);
            if (_nextButton != null) _nextButton.onClick.RemoveListener(MoveNext);
            UnsubscribeFromBootstrap();
            if (_sourceRetry != null) StopCoroutine(_sourceRetry);
            _sourceRetry = null;
            StopTransition();
            ApplyContentPosition();
        }

        private void OnDestroy() => StopTransition();

        private void OnValidate()
        {
            _visibleCount = Mathf.Max(1, _visibleCount);
            _duration = Mathf.Max(0f, _duration);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            // 참조/CanvasGroup 조회는 실제 방향키 입력이 있을 때만 수행한다.
            bool previous = keyboard.leftArrowKey.wasPressedThisFrame;
            bool next = keyboard.rightArrowKey.wasPressedThisFrame;
            if (previous == next) return;
            if (previous) MovePrevious();
            else MoveNext();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!_hasInitializedLayout) return;
            StopTransition();
            ArrangeItems();
            ApplyContentPosition();
            RefreshNavigation();
        }

        public void MovePrevious() => MoveBy(-1);
        public void MoveNext() => MoveBy(1);

        private void MoveBy(int direction)
        {
            if (!CanAcceptNavigation() || _entries.Count <= _visibleCount || _content == null) return;
            int nextIndex = Mathf.Clamp(_firstVisibleIndex + direction, 0, _entries.Count - _visibleCount);
            if (nextIndex == _firstVisibleIndex) return;
            _firstVisibleIndex = nextIndex;
            Vector2 target = CalculateContentPosition();
            if (_duration <= 0f || Mathf.Approximately(_itemWidth, 0f))
            {
                _content.anchoredPosition = target;
                RefreshNavigation();
                return;
            }
            _transition = _content.DOAnchorPos(target, _duration).SetEase(_ease).SetUpdate(true)
                .OnComplete(() =>
                {
                    _transition = null;
                    RefreshNavigation();
                });
            RefreshNavigation();
        }

        private bool CanAcceptNavigation()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy || _bootstrap == null ||
                _bootstrap.IsUiInputBlocked || (_transition != null && _transition.IsActive())) return false;
            if (_disclosure != null && (!_disclosure.IsExpanded || _disclosure.IsTransitioning)) return false;
            foreach (UIPopupController controller in _popupControllers)
                if (controller != null && controller.isActiveAndEnabled && controller.OpenCount > 0) return false;

            // 외부 HUD가 런타임에 CanvasGroup을 추가할 수 있으므로 입력 시 상위 상태를 읽는다.
            foreach (CanvasGroup group in GetComponentsInParent<CanvasGroup>(true))
                if (group != null && group.enabled &&
                    (group.alpha <= 0.01f || !group.interactable || !group.blocksRaycasts)) return false;

            GameObject selection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selection == null ||
                (selection.GetComponentInParent<TMP_InputField>() == null &&
                 selection.GetComponentInParent<InputField>() == null);
        }

        private void HideLegacyPresentation()
        {
            if (_legacyRows != null)
                foreach (GameObject row in _legacyRows)
                    if (row != null) row.SetActive(false);
            if (_designTimePreview != null) _designTimePreview.SetActive(false);
            if (_itemTemplate != null) _itemTemplate.gameObject.SetActive(false);
        }

        private IEnumerator RetrySource()
        {
            var wait = new WaitForSecondsRealtime(SOURCE_RETRY_INTERVAL);
            while (_bootstrap == null || (_bootstrap.Stage != null && !_hasWaveSnapshot))
            {
                yield return wait;
                ResolveSceneReferences();
                SubscribeToBootstrap();
                RefreshWave();
            }
            _sourceRetry = null;
        }

        private void EnsureSourceRetry()
        {
            if (_sourceRetry == null && isActiveAndEnabled &&
                (_bootstrap == null || (_bootstrap.Stage != null && !_hasWaveSnapshot)))
                _sourceRetry = StartCoroutine(RetrySource());
        }

        private void ResolveSceneReferences()
        {
            if (_disclosure == null) _disclosure = GetComponentInParent<UIWavePreviewDisclosure>(true);
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;
            if (_bootstrap != null && _hasResolvedSceneUi) return;
            _popupControllers.Clear();
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (_bootstrap == null) _bootstrap = root.GetComponentInChildren<InGamePrototypeBootstrap>(true);
                _popupControllers.AddRange(root.GetComponentsInChildren<UIPopupController>(true));
            }
            _hasResolvedSceneUi = true;
        }

        private void SubscribeToBootstrap()
        {
            if (_subscribedBootstrap == _bootstrap) return;
            UnsubscribeFromBootstrap();
            _subscribedBootstrap = _bootstrap;
            if (_subscribedBootstrap != null) _subscribedBootstrap.Changed += RefreshWave;
        }

        private void UnsubscribeFromBootstrap()
        {
            if (_subscribedBootstrap != null) _subscribedBootstrap.Changed -= RefreshWave;
            _subscribedBootstrap = null;
        }

        private void RefreshWave()
        {
            StageManager stage = _bootstrap != null ? _bootstrap.Stage : null;
            int roundNumber = stage != null ? stage.CurrentRoundNumber : 0;
            if (stage != null && (stage.State == eStageState.GENERAL_REWARD || stage.State == eStageState.AUGMENT))
                roundNumber = roundNumber < stage.TotalRounds ? roundNumber + 1 : 0;
            string runId = stage != null && stage.Progress != null ? stage.Progress.RunId : null;
            if (_hasWaveSnapshot && _shownStage == stage && _shownRunId == runId && _shownRoundNumber == roundNumber)
            {
                RefreshNavigation();
                return;
            }

            _shownStage = stage;
            _shownRunId = runId;
            _shownRoundNumber = roundNumber;
            _firstVisibleIndex = 0;
            StopTransition();
            _entries.Clear();
            StageDefinition definition = stage != null && roundNumber > 0 ? _bootstrap.SelectedStageDefinition : null;
            // 초기화 중 데이터가 비어 있으면 완료된 캐시로 취급하지 않고 다시 읽는다.
            _hasWaveSnapshot = roundNumber <= 0 || (definition != null && roundNumber <= definition.Rounds.Count);
            if (definition != null && roundNumber <= definition.Rounds.Count)
            {
                BuildHeroLookup();
                var rowsById = new Dictionary<string, EnemyEntry>(StringComparer.Ordinal);
                foreach (HeroSpawnDefinition spawn in definition.Rounds[roundNumber - 1].Spawns)
                {
                    if (spawn == null) continue;
                    if (!rowsById.TryGetValue(spawn.HeroId, out EnemyEntry row))
                    {
                        row = CreateEntry(spawn.HeroId);
                        rowsById.Add(spawn.HeroId, row);
                        _entries.Add(row);
                    }
                    row.Count += spawn.Count;
                }
            }
            RefreshItems();
            EnsureSourceRetry();
        }

        private void BuildPortraitLookup()
        {
            _catalogById.Clear();
            _portraitsById.Clear();
            if (_unitCatalog != null && _unitCatalog.Entries != null)
                foreach (UIUnitCatalogSO.Entry entry in _unitCatalog.Entries)
                    if (entry != null && entry.Faction == eUnitCodexFaction.HERO)
                        _catalogById[entry.Id] = entry;
            if (_portraitOverrides == null) return;
            foreach (PortraitOverride entry in _portraitOverrides)
                if (entry != null && !string.IsNullOrEmpty(entry.HeroId) && entry.Portrait != null)
                    _portraitsById[entry.HeroId] = entry.Portrait;
        }

        private void BuildHeroLookup()
        {
            HeroPoolCatalogSO catalog = _bootstrap != null && _bootstrap.Config != null
                ? _bootstrap.Config.HeroPoolCatalog : null;
            if (_heroCatalog == catalog) return;
            _heroCatalog = catalog;
            _heroesById.Clear();
            if (catalog == null) return;
            foreach (HeroPoolEntry entry in catalog.CreateSnapshot())
                if (entry != null && !string.IsNullOrEmpty(entry.HeroId) && !_heroesById.ContainsKey(entry.HeroId))
                    _heroesById.Add(entry.HeroId, entry);
        }

        private EnemyEntry CreateEntry(string heroId)
        {
            _catalogById.TryGetValue("unit." + heroId, out UIUnitCatalogSO.Entry visual);
            string displayName = visual != null && !string.IsNullOrWhiteSpace(visual.DisplayName)
                ? visual.DisplayName : heroId;
            if (_heroesById.TryGetValue(heroId, out HeroPoolEntry hero) && hero.Prefab != null)
            {
                UnitBase unit = hero.Prefab.GetComponentInChildren<UnitBase>(true);
                if (unit != null && unit.statData != null && !string.IsNullOrWhiteSpace(unit.statData.displayName))
                    displayName = unit.statData.displayName;
            }
            Sprite portrait = visual != null ? visual.Portrait : null;
            if (_portraitsById.TryGetValue(heroId, out Sprite overridePortrait)) portrait = overridePortrait;
            return new EnemyEntry(heroId, displayName, portrait);
        }

        private void InitializeLayout()
        {
            if (_hasInitializedLayout || _content == null) return;
            _contentOrigin = _content.anchoredPosition;
            _hasInitializedLayout = true;
        }

        private void RefreshItems()
        {
            InitializeLayout();
            if (_content == null || _itemTemplate == null)
            {
                RefreshNavigation();
                return;
            }
            while (_items.Count < _entries.Count)
            {
                UIWavePreviewEnemyItem item = Instantiate(_itemTemplate, _content);
                item.name = "EnemyItem_" + _items.Count;
                _items.Add(item);
            }
            for (int i = 0; i < _items.Count; i++)
            {
                UIWavePreviewEnemyItem item = _items[i];
                if (item == null) continue;
                bool hasEntry = i < _entries.Count;
                item.gameObject.SetActive(hasEntry);
                if (hasEntry) item.SetEnemy(_entries[i].DisplayName, _entries[i].Count, _entries[i].Portrait);
            }
            ArrangeItems();
            ApplyContentPosition();
            RefreshNavigation();
        }

        private void ArrangeItems()
        {
            if (_viewport == null || _content == null) return;
            _itemWidth = _viewport.rect.width / Mathf.Max(1, _visibleCount);
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Max(_visibleCount, _entries.Count) * _itemWidth);
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null || !_items[i].TryGetComponent(out RectTransform rect)) continue;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _itemWidth);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _viewport.rect.height);
                rect.anchoredPosition = new Vector2(i * _itemWidth, 0f);
            }
        }

        private void ApplyContentPosition()
        {
            if (_content != null && _hasInitializedLayout)
                _content.anchoredPosition = CalculateContentPosition();
        }

        private Vector2 CalculateContentPosition()
        {
            if (_entries.Count == 0) return _contentOrigin;
            int capacity = Mathf.Max(1, _visibleCount);
            float centerOffset = _entries.Count < capacity ? (capacity - _entries.Count) * _itemWidth * 0.5f : 0f;
            return _contentOrigin + Vector2.right * centerOffset - Vector2.right * (_firstVisibleIndex * _itemWidth);
        }

        private void RefreshSeparators()
        {
            if (_separators == null) return;
            int capacity = Mathf.Max(1, _visibleCount);
            int displayedCount = Mathf.Min(capacity, _entries.Count);
            float centerOffset = (capacity - displayedCount) * _itemWidth * 0.5f;
            for (int i = 0; i < _separators.Length; i++)
            {
                RectTransform separator = _separators[i];
                if (separator == null) continue;
                bool isVisible = i < displayedCount - 1;
                separator.gameObject.SetActive(isVisible);
                if (!isVisible) continue;
                Vector2 position = separator.anchoredPosition;
                position.x = centerOffset + (i + 1) * _itemWidth - separator.rect.width * 0.5f;
                separator.anchoredPosition = position;
            }
        }

        private void RefreshNavigation()
        {
            RefreshSeparators();
            bool hasOverflow = _entries.Count > _visibleCount;
            bool isMoving = _transition != null && _transition.IsActive();
            if (_previousButton != null)
            {
                _previousButton.gameObject.SetActive(hasOverflow);
                _previousButton.interactable = !isMoving && _firstVisibleIndex > 0;
            }
            if (_nextButton != null)
            {
                _nextButton.gameObject.SetActive(hasOverflow);
                _nextButton.interactable = !isMoving && _firstVisibleIndex < _entries.Count - _visibleCount;
            }
        }

        private void StopTransition()
        {
            Tween previous = _transition;
            _transition = null;
            if (previous != null && previous.IsActive()) previous.Kill(false);
        }
    }
}
