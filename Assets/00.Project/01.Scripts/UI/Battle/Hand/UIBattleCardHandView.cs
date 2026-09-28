using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// 카드 목록의 논리 순서는 유지하면서 부채꼴 겹침 배치와 호버 비주얼만 관리한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class UIBattleCardHandView : MonoBehaviour
    {
        [Header("스크롤 영역")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _dropZone;
        [SerializeField] private RectTransform _content;
        [SerializeField] private Scrollbar _horizontalScrollbar;

        [Header("카드 프리팹")]
        [SerializeField] private GameObject _unitCardPrefab;
        [SerializeField] private GameObject _landSlotCardPrefab;
        [SerializeField] private GameObject _relicCardPrefab;

        [Header("기본 배치")]
        [SerializeField, Min(0.01f)] private float _cardScale = 0.29f;
        [SerializeField] private float _comfortableSpacing = -60f;
        [SerializeField, Min(1f)] private float _minimumRevealWidth = 56f;
        [SerializeField, Min(0f)] private float _bottomPadding = 24f;

        [Header("부채꼴 배치")]
        [SerializeField, Min(0f)] private float _fanArcHeight = 32f;
        [SerializeField, Range(0f, 30f)] private float _maxFanAngle = 8f;

        [Header("호버")]
        [SerializeField, Min(1f)] private float _hoverScale = 1.44f;
        [SerializeField, Min(0f)] private float _hoverRise = 60f;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.15f;

        private readonly List<UIBattleCardHandSlot> _slots = new List<UIBattleCardHandSlot>();
        private readonly Dictionary<string, UIBattleCardHandSlot> _slotsByKey =
            new Dictionary<string, UIBattleCardHandSlot>(StringComparer.Ordinal);
        private readonly HashSet<eBattleHandCardKind> _missingPrefabWarnings =
            new HashSet<eBattleHandCardKind>();

        private UIBattleCardHandSlot _hoveredSlot;
        private Canvas _handCanvas;
        private Vector2 _cardSize = Vector2.one;
        private bool _isInitialized;
        private bool _isApplyingLayout;
        private bool _hasAppliedLayout;
        private bool _isInteractable = true;
        private bool _hasHeightWarning;

        public event Action<UIBattleCardHandSlot, PointerEventData> CardClicked;
        public event Action<UIBattleCardHandSlot, PointerEventData> CardBeginDrag;
        public event Action<UIBattleCardHandSlot, PointerEventData> CardDragged;
        public event Action<UIBattleCardHandSlot, PointerEventData> CardEndDrag;

        public IReadOnlyList<UIBattleCardHandSlot> Slots => _slots;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null) _slots[i].SetInteractable(_isInteractable);
            RefreshLayoutInternal(true);
        }

        private void OnDisable()
        {
            _hoveredSlot = null;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == null) continue;
                _slots[i].SetInteractable(false);
                _slots[i].ResetState(true);
            }
            if (_horizontalScrollbar != null)
            {
                _horizontalScrollbar.interactable = false;
                _horizontalScrollbar.gameObject.SetActive(false);
            }
            RestoreLogicalSiblingOrder();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null) _slots[i].Dispose();
        }

        private void OnValidate()
        {
            _cardScale = Mathf.Max(0.01f, _cardScale);
            _minimumRevealWidth = Mathf.Max(1f, _minimumRevealWidth);
            _bottomPadding = Mathf.Max(0f, _bottomPadding);
            _fanArcHeight = Mathf.Max(0f, _fanArcHeight);
            _maxFanAngle = Mathf.Clamp(_maxFanAngle, 0f, 30f);
            _hoverScale = Mathf.Max(1f, _hoverScale);
            _hoverRise = Mathf.Max(0f, _hoverRise);
            _transitionDuration = Mathf.Max(0f, _transitionDuration);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!_isInitialized || _isApplyingLayout) return;
            RefreshLayoutInternal(!Application.isPlaying || !isActiveAndEnabled);
        }

        public void SetItems(IReadOnlyList<BattleHandCardDisplayData> items)
        {
            EnsureInitialized();

            Dictionary<string, UIBattleCardHandSlot> remaining =
                new Dictionary<string, UIBattleCardHandSlot>(_slotsByKey, StringComparer.Ordinal);
            Dictionary<string, int> idOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            List<UIBattleCardHandSlot> nextSlots = new List<UIBattleCardHandSlot>(items?.Count ?? 0);
            Dictionary<string, UIBattleCardHandSlot> nextByKey =
                new Dictionary<string, UIBattleCardHandSlot>(StringComparer.Ordinal);

            int itemCount = items != null ? items.Count : 0;
            for (int i = 0; i < itemCount; i++)
            {
                BattleHandCardDisplayData data = items[i];
                if (data == null) continue;

                string reuseKey = BuildReuseKey(data.Id, i, idOccurrences);
                remaining.TryGetValue(reuseKey, out UIBattleCardHandSlot slot);
                if (slot != null && slot.Kind != data.Kind)
                {
                    remaining.Remove(reuseKey);
                    DestroySlot(slot);
                    slot = null;
                }

                if (slot == null) slot = CreateSlot(data.Kind, i);
                else remaining.Remove(reuseKey);
                if (slot == null) continue;

                slot.Bind(data, reuseKey, nextSlots.Count);
                ApplyData(slot, data);
                slot.SetInteractable(isActiveAndEnabled && _isInteractable);
                nextSlots.Add(slot);
                nextByKey[reuseKey] = slot;
            }

            foreach (KeyValuePair<string, UIBattleCardHandSlot> pair in remaining)
                DestroySlot(pair.Value);

            if (_hoveredSlot != null && !nextSlots.Contains(_hoveredSlot)) _hoveredSlot = null;

            _slots.Clear();
            _slots.AddRange(nextSlots);
            _slotsByKey.Clear();
            foreach (KeyValuePair<string, UIBattleCardHandSlot> pair in nextByKey)
                _slotsByKey.Add(pair.Key, pair.Value);

            RestoreLogicalSiblingOrder();
            RefreshLayoutInternal(!_hasAppliedLayout || !Application.isPlaying || !isActiveAndEnabled);
        }

        public void Clear()
        {
            SetItems(Array.Empty<BattleHandCardDisplayData>());
        }

        public void RefreshLayoutImmediate()
        {
            EnsureInitialized();
            RefreshLayoutInternal(true);
        }

        public void ResetTransientInteraction()
        {
            _hoveredSlot = null;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null) _slots[i].ResetState(true);
            RestoreLogicalSiblingOrder();
        }

        public void SetInteractable(bool interactable)
        {
            _isInteractable = interactable;
            if (!interactable) SetHoveredSlot(null, true);

            bool effectiveInteractable = interactable && isActiveAndEnabled;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null) _slots[i].SetInteractable(effectiveInteractable);

            if (_horizontalScrollbar != null) _horizontalScrollbar.interactable = effectiveInteractable;
        }

        public bool IsScreenPointInsideViewport(Vector2 screenPoint, Camera eventCamera)
        {
            EnsureInitialized();
            return _viewport != null && RectTransformUtility.RectangleContainsScreenPoint(
                _viewport, screenPoint, eventCamera);
        }

        public bool IsScreenPointInsideDropZone(Vector2 screenPoint, Camera eventCamera)
        {
            EnsureInitialized();
            RectTransform target = _dropZone != null ? _dropZone : _viewport;
            return target != null && RectTransformUtility.RectangleContainsScreenPoint(
                target, screenPoint, eventCamera);
        }

        public bool TryGetSlotUnderScreenPoint(
            Vector2 screenPoint,
            Camera eventCamera,
            out UIBattleCardHandSlot slot,
            UIBattleCardHandSlot excludedSlot = null)
        {
            slot = null;
            if (!IsScreenPointInsideViewport(screenPoint, eventCamera)) return false;

            for (int siblingIndex = _content != null ? _content.childCount - 1 : -1;
                 siblingIndex >= 0;
                 siblingIndex--)
            {
                Transform child = _content.GetChild(siblingIndex);
                if (!child.gameObject.activeInHierarchy ||
                    !child.TryGetComponent(out UIBattleCardHandSlot candidate) ||
                    candidate == excludedSlot ||
                    !_slots.Contains(candidate) ||
                    !candidate.ContainsScreenPoint(screenPoint, eventCamera))
                    continue;

                slot = candidate;
                return true;
            }

            return false;
        }

        internal void HandlePointerEnter(UIBattleCardHandSlot slot)
        {
            if (!isActiveAndEnabled || !_isInteractable || slot == null || !_slots.Contains(slot)) return;
            SetHoveredSlot(slot, false);
        }

        internal void HandlePointerExit(UIBattleCardHandSlot slot)
        {
            if (!isActiveAndEnabled || slot == null || slot != _hoveredSlot || slot.IsDragging) return;
            SetHoveredSlot(null, false);
        }

        internal void HandlePointerClick(UIBattleCardHandSlot slot, PointerEventData eventData)
        {
            if (!isActiveAndEnabled || !_isInteractable || slot == null || !_slots.Contains(slot)) return;
            CardClicked?.Invoke(slot, eventData);
        }

        internal void HandleBeginDrag(UIBattleCardHandSlot slot, PointerEventData eventData)
        {
            if (!isActiveAndEnabled || !_isInteractable || slot == null || !_slots.Contains(slot)) return;
            SetHoveredSlot(slot, false);
            CardBeginDrag?.Invoke(slot, eventData);
        }

        internal void HandleDrag(UIBattleCardHandSlot slot, PointerEventData eventData)
        {
            if (!isActiveAndEnabled || !_isInteractable || slot == null || !_slots.Contains(slot)) return;
            CardDragged?.Invoke(slot, eventData);
        }

        internal void HandleEndDrag(UIBattleCardHandSlot slot, PointerEventData eventData)
        {
            if (!isActiveAndEnabled || slot == null) return;
            CardEndDrag?.Invoke(slot, eventData);
        }

        private void EnsureInitialized()
        {
            if (_isInitialized) return;
            if (_scrollRect == null) TryGetComponent(out _scrollRect);
            if (_handCanvas == null) TryGetComponent(out _handCanvas);
            if (_viewport == null && _scrollRect != null) _viewport = _scrollRect.viewport;
            if (_content == null && _scrollRect != null) _content = _scrollRect.content;
            if (_horizontalScrollbar == null && _scrollRect != null)
                _horizontalScrollbar = _scrollRect.horizontalScrollbar;

            if (_scrollRect != null)
            {
                _scrollRect.viewport = _viewport;
                _scrollRect.content = _content;
                _scrollRect.horizontalScrollbar = _horizontalScrollbar;
                _scrollRect.vertical = false;
            }

            DisableEmptyAreaRaycasts();
            _cardSize = CalculateCommonCardSize();
            _isInitialized = true;
        }

        private void DisableEmptyAreaRaycasts()
        {
            DisableGraphicRaycast(transform as RectTransform);
            DisableGraphicRaycast(_viewport);
            DisableGraphicRaycast(_content);
        }

        private static void DisableGraphicRaycast(RectTransform target)
        {
            if (target != null && target.TryGetComponent(out Graphic graphic))
                graphic.raycastTarget = false;
        }

        private Vector2 CalculateCommonCardSize()
        {
            Vector2 result = Vector2.zero;
            IncludePrefabSize(_unitCardPrefab, ref result);
            IncludePrefabSize(_landSlotCardPrefab, ref result);
            IncludePrefabSize(_relicCardPrefab, ref result);
            return new Vector2(Mathf.Max(1f, result.x), Mathf.Max(1f, result.y));
        }

        private void IncludePrefabSize(GameObject prefab, ref Vector2 result)
        {
            if (prefab == null || !prefab.TryGetComponent(out RectTransform prefabRect)) return;

            Vector2 sourceSize = prefabRect.rect.size;
            if (sourceSize.x <= 0f) sourceSize.x = Mathf.Abs(prefabRect.sizeDelta.x);
            if (sourceSize.y <= 0f) sourceSize.y = Mathf.Abs(prefabRect.sizeDelta.y);
            result.x = Mathf.Max(result.x, Mathf.Abs(sourceSize.x) * _cardScale);
            result.y = Mathf.Max(result.y, Mathf.Abs(sourceSize.y) * _cardScale);
        }

        private UIBattleCardHandSlot CreateSlot(eBattleHandCardKind kind, int sourceIndex)
        {
            if (_content == null) return null;
            GameObject prefab = GetPrefab(kind);
            if (prefab == null)
            {
                if (_missingPrefabWarnings.Add(kind))
                    Debug.LogWarning($"{kind} 손패 카드 Prefab이 연결되지 않아 표시하지 않습니다.", this);
                return null;
            }

            GameObject slotObject = new GameObject(
                $"HandCard_{kind}_{sourceIndex}",
                typeof(RectTransform),
                typeof(UIBattleCardHandSlot));
            RectTransform slotRoot = (RectTransform)slotObject.transform;
            slotRoot.SetParent(_content, false);
            slotRoot.anchorMin = Vector2.zero;
            slotRoot.anchorMax = Vector2.zero;
            slotRoot.pivot = new Vector2(0.5f, 0.5f);
            slotRoot.sizeDelta = _cardSize;

            GameObject hitAreaObject = new GameObject(
                "HitArea",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform hitAreaRoot = (RectTransform)hitAreaObject.transform;
            hitAreaRoot.SetParent(slotRoot, false);
            hitAreaRoot.anchorMin = new Vector2(0.5f, 0.5f);
            hitAreaRoot.anchorMax = new Vector2(0.5f, 0.5f);
            hitAreaRoot.pivot = new Vector2(0.5f, 0.5f);
            hitAreaRoot.sizeDelta = _cardSize;
            Image hitArea = hitAreaObject.GetComponent<Image>();

            GameObject visualObject = new GameObject(
                "VisualRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            RectTransform visualRoot = (RectTransform)visualObject.transform;
            visualRoot.SetParent(slotRoot, false);
            visualRoot.anchorMin = new Vector2(0.5f, 0.5f);
            visualRoot.anchorMax = new Vector2(0.5f, 0.5f);
            visualRoot.pivot = new Vector2(0.5f, 0.5f);
            visualRoot.sizeDelta = _cardSize;
            Canvas visualCanvas = visualObject.GetComponent<Canvas>();
            CanvasGroup visualCanvasGroup = visualObject.GetComponent<CanvasGroup>();

            int hoverVisualSortingOrder = 1;
            if (_handCanvas != null)
            {
                visualCanvas.sortingLayerID = _handCanvas.sortingLayerID;
                hoverVisualSortingOrder = _handCanvas.sortingOrder + 1;
                visualCanvas.sortingOrder = hoverVisualSortingOrder;
            }
            visualCanvas.overrideSorting = false;

            GameObject visualInstance = Instantiate(prefab, visualRoot, false);
            visualInstance.name = prefab.name;
            if (visualInstance.transform is RectTransform cardRect)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.localRotation = Quaternion.identity;
                cardRect.localScale = Vector3.one * _cardScale;
            }

            Graphic[] visualGraphics = visualObject.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < visualGraphics.Length; i++)
                if (visualGraphics[i] != null) visualGraphics[i].raycastTarget = false;

            UIBattlePreparationCardView cardView =
                visualInstance.GetComponentInChildren<UIBattlePreparationCardView>(true);
            UIBattleCardHandSlot slot = slotObject.GetComponent<UIBattleCardHandSlot>();
            slot.Initialize(
                this,
                slotRoot,
                hitAreaRoot,
                visualRoot,
                hitArea,
                visualCanvas,
                visualCanvasGroup,
                cardView,
                _cardSize,
                hoverVisualSortingOrder);
            return slot;
        }

        private GameObject GetPrefab(eBattleHandCardKind kind)
        {
            switch (kind)
            {
                case eBattleHandCardKind.UNIT:
                    return _unitCardPrefab;
                case eBattleHandCardKind.LAND_SLOT:
                    return _landSlotCardPrefab;
                case eBattleHandCardKind.RELIC:
                    return _relicCardPrefab;
                default:
                    return null;
            }
        }

        private static string BuildReuseKey(string id, int index, IDictionary<string, int> occurrences)
        {
            string baseKey = string.IsNullOrWhiteSpace(id) ? $"__INDEX_{index}" : id;
            occurrences.TryGetValue(baseKey, out int occurrence);
            occurrences[baseKey] = occurrence + 1;
            return occurrence == 0 ? baseKey : $"{baseKey}#{occurrence}";
        }

        private static void ApplyData(UIBattleCardHandSlot slot, BattleHandCardDisplayData data)
        {
            UIBattlePreparationCardView cardView = slot.GetCardView();
            if (cardView == null || data == null) return;

            cardView.SetTitle(data.Title);
            cardView.SetRankText(data.RankText);
            cardView.SetTrait(data.TraitTitle, data.TraitDescription);
            cardView.SetSkill(data.SkillTitle, data.SkillDescription);
            cardView.SetStats(data.Attack, data.Defense, data.Health);
            cardView.SetAreaDescription(data.AreaTitle, data.AreaDescription);
            // null은 해당 종류 Prefab의 기본 아트를 그대로 사용한다는 의미다.
            cardView.SetArtwork(data.Artwork != null ? data.Artwork : slot.DefaultArtwork);
            cardView.SetTypeIcon(data.TypeIcon != null ? data.TypeIcon : slot.DefaultTypeIcon);
            cardView.SetFootprint(data.Footprint);
        }

        private void DestroySlot(UIBattleCardHandSlot slot)
        {
            if (slot == null) return;
            if (_hoveredSlot == slot) _hoveredSlot = null;
            slot.Dispose();
            slot.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(slot.gameObject);
            else DestroyImmediate(slot.gameObject);
        }

        private void RefreshLayoutInternal(bool immediate)
        {
            if (_isApplyingLayout || _viewport == null || _content == null) return;
            _isApplyingLayout = true;
            try
            {
                float viewportWidth = Mathf.Max(0f, _viewport.rect.width);
                float previousNormalizedPosition = _scrollRect != null
                    ? _scrollRect.horizontalNormalizedPosition
                    : 0f;
                // 첫/마지막 카드가 회전하거나 확대될 때 Viewport 좌우 마스크에 잘리지 않도록 여백을 둔다.
                float hoverSidePadding = _slots.Count > 0
                    ? _cardSize.x * Mathf.Max(0f, _hoverScale - 1f) * 0.5f
                    : 0f;
                float fanRadians = _maxFanAngle * Mathf.Deg2Rad;
                float rotatedHalfWidth = Mathf.Abs(Mathf.Cos(fanRadians)) * _cardSize.x * 0.5f +
                                         Mathf.Abs(Mathf.Sin(fanRadians)) * _cardSize.y * 0.5f;
                float fanSidePadding = Mathf.Max(0f, rotatedHalfWidth - _cardSize.x * 0.5f);
                float sidePadding = Mathf.Max(hoverSidePadding, fanSidePadding);
                BattleCardHandLayout.Result layout = BattleCardHandLayout.Calculate(
                    Mathf.Max(0f, viewportWidth - sidePadding * 2f),
                    _cardSize.x,
                    _slots.Count,
                    _comfortableSpacing,
                    _minimumRevealWidth);
                float contentWidth = layout.ContentWidth + sidePadding * 2f;
                bool requiresScroll = layout.RequiresScroll || contentWidth > viewportWidth + 0.01f;

                _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
                float viewportHeight = Mathf.Max(0f, _viewport.rect.height);
                float requiredHeight = _bottomPadding * 2f + _hoverRise +
                    _fanArcHeight + _cardSize.y * 0.5f * (1f + _hoverScale);
                _content.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    Mathf.Max(viewportHeight, requiredHeight));

                if (!_hasHeightWarning && viewportHeight > 0f && viewportHeight + 0.01f < requiredHeight)
                {
                    _hasHeightWarning = true;
                    Debug.LogWarning(
                        $"손패 Viewport 높이가 {requiredHeight:0.#} UI 단위보다 작아 호버 카드가 잘릴 수 있습니다.",
                        this);
                }

                float baseY = _bottomPadding + _cardSize.y * 0.5f;
                bool animate = !immediate && Application.isPlaying && isActiveAndEnabled;
                for (int i = 0; i < _slots.Count; i++)
                {
                    UIBattleCardHandSlot slot = _slots[i];
                    if (slot == null) continue;
                    float normalized = _slots.Count <= 1
                        ? 0f
                        : Mathf.Lerp(-1f, 1f, i / (float)(_slots.Count - 1));
                    float fanLift = _fanArcHeight * (1f - normalized * normalized);
                    float fanRotation = -normalized * _maxFanAngle;
                    slot.SetLogicalIndex(i);
                    slot.MoveTo(
                        new Vector2(layout.Positions[i] + sidePadding, baseY + fanLift),
                        fanRotation,
                        _transitionDuration,
                        !animate);
                }

                if (_scrollRect != null)
                {
                    _scrollRect.horizontal = requiresScroll;
                    _scrollRect.vertical = false;
                    _scrollRect.horizontalNormalizedPosition = requiresScroll
                        ? Mathf.Clamp01(previousNormalizedPosition)
                        : 0f;
                }

                if (_horizontalScrollbar != null)
                {
                    bool showScrollbar = isActiveAndEnabled && requiresScroll;
                    _horizontalScrollbar.interactable = showScrollbar && _isInteractable;
                    if (_horizontalScrollbar.gameObject.activeSelf != showScrollbar)
                        _horizontalScrollbar.gameObject.SetActive(showScrollbar);
                }

                _hasAppliedLayout = true;
            }
            finally
            {
                _isApplyingLayout = false;
            }
        }

        private void SetHoveredSlot(UIBattleCardHandSlot nextSlot, bool immediate)
        {
            if (_hoveredSlot == nextSlot) return;
            UIBattleCardHandSlot previous = _hoveredSlot;
            _hoveredSlot = nextSlot;

            if (previous != null)
                previous.SetHoverState(false, _hoverScale, _hoverRise, _transitionDuration, immediate);

            RestoreLogicalSiblingOrder();
            if (_hoveredSlot == null) return;

            _hoveredSlot.SetHoverState(true, _hoverScale, _hoverRise, _transitionDuration, immediate);
        }

        private void RestoreLogicalSiblingOrder()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                UIBattleCardHandSlot slot = _slots[i];
                if (slot != null) slot.transform.SetSiblingIndex(i);
            }
        }
    }
}
