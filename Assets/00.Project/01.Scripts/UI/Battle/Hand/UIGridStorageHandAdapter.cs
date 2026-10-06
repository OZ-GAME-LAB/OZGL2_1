using System.Collections.Generic;
using System.Globalization;
using OZGL2.Grid;
using OZGL2.Grid.UI;
using OZGL2.InGame;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// GridManager의 보관함 데이터를 uGUI 손패에 표시하고, 손패 드래그를 기존 배치 명령으로 전달한다.
    /// 보관함의 논리 순서나 생명 주기는 소유하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Battle/Grid Storage Hand Adapter")]
    public sealed class UIGridStorageHandAdapter : MonoBehaviour
    {
        private const string UNIT_ID_PREFIX = "UNIT:";
        private const string BLOCK_ID_PREFIX = "BLOCK:";
        private const string REWARD_ID_PREFIX = "REWARD:";
        private const string LEGACY_TRAY_NAME = "storage-tray";

        [Header("손패")]
        [SerializeField] private UIBattleCardHandView _handView;
        [SerializeField] private UIUnitCatalogSO _unitCatalog;

        [Header("인게임 연결")]
        [SerializeField] private InGamePrototypeBootstrap _bootstrap;
        [SerializeField] private InGamePhasePresentation _phasePresentation;

        [Header("기존 UI Toolkit 보관함")]
        [SerializeField] private UIDocument _legacyDocument;
        [SerializeField] private bool _hideLegacyStorageTray = true;

        private readonly Dictionary<string, StoredIdentity> _identities =
            new Dictionary<string, StoredIdentity>();
        private readonly Dictionary<string, int> _rewardOptionIndices =
            new Dictionary<string, int>();

        private GridRunSession _session;
        private GridManager _manager;
        private VisualElement _legacyTray;
        private StyleEnum<Visibility> _legacyVisibility;
        private bool _hasLegacyVisibilitySnapshot;

        private bool _ownsDrag;
        private int _dragPointerId;
        private string _draggedStableId;
        private string _draggedInstanceId;
        private eGridDragKind _dragKind;
        private Vector2 _dragPointerPosition;
        private bool _isCancellingOwnedDrag;
        private bool _lastViewInteractable;
        private bool _hasLastViewInteractable;
        private bool _isShowingRewardSelection;
        private string _shownRewardRequestId;

        public bool IsRuntimeConnected => _bootstrap != null && _phasePresentation != null && _legacyDocument != null;

        /// <summary>Scene Root가 분리된 실제 InGame 연결을 명시적으로 주입한다.</summary>
        public void ConfigureRuntimeSources(
            InGamePrototypeBootstrap bootstrap,
            InGamePhasePresentation phasePresentation,
            UIDocument legacyDocument)
        {
            if (Application.isPlaying && isActiveAndEnabled)
            {
                if (_bootstrap != null) _bootstrap.Changed -= HandleBootstrapChanged;
                CancelOwnedDrag();
                UnbindManager();
                RestoreLegacyStorageTray();
            }

            _bootstrap = bootstrap;
            _phasePresentation = phasePresentation;
            _legacyDocument = legacyDocument;

            if (Application.isPlaying && isActiveAndEnabled)
            {
                if (_bootstrap != null) _bootstrap.Changed += HandleBootstrapChanged;
                TryHideLegacyStorageTray();
                RefreshSessionBinding();
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeView();

            if (_bootstrap != null) _bootstrap.Changed += HandleBootstrapChanged;

            TryHideLegacyStorageTray();
            RefreshSessionBinding();
        }

        private void OnDisable()
        {
            if (_bootstrap != null) _bootstrap.Changed -= HandleBootstrapChanged;
            UnsubscribeView();

            CancelOwnedDrag();
            UnbindManager();
            RestoreLegacyStorageTray();

            _identities.Clear();
            _rewardOptionIndices.Clear();
            _isShowingRewardSelection = false;
            _shownRewardRequestId = null;
            if (_handView == null) return;
            _handView.SetInteractable(false);
            _handView.Clear();
            _hasLastViewInteractable = false;
        }

        private void LateUpdate()
        {
            if (_ownsDrag && !IsManagerSelectionOwned())
            {
                ClearOwnedDragState();
                _handView?.ResetTransientInteraction();
            }

            HandleOwnedDragKeyboard();

            // 카메라 전환 종료는 Bootstrap Changed를 발생시키지 않으므로 실제 입력 가능 상태만 감시한다.
            UpdateViewInteractability();

            // UIDocument가 OnEnable보다 늦게 VisualTree를 만든 경우에만 보관함 탐색을 재시도한다.
            if (!_hasLegacyVisibilitySnapshot) TryHideLegacyStorageTray();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelOwnedDrag();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) CancelOwnedDrag();
        }

        private void ResolveReferences()
        {
            if (_handView == null) _handView = GetComponentInChildren<UIBattleCardHandView>(true);
            if (_bootstrap == null) _bootstrap = ResolveInHierarchy<InGamePrototypeBootstrap>();
            if (_phasePresentation == null) _phasePresentation = ResolveInHierarchy<InGamePhasePresentation>();
            if (_legacyDocument == null) _legacyDocument = ResolveInHierarchy<UIDocument>();
        }

        private T ResolveInHierarchy<T>() where T : Component
        {
            T component = GetComponentInParent<T>(true);
            if (component != null) return component;

            Transform root = transform.root;
            return root != null ? root.GetComponentInChildren<T>(true) : null;
        }

        private void SubscribeView()
        {
            if (_handView == null) return;
            _handView.CardClicked += HandleCardClicked;
            _handView.CardBeginDrag += HandleCardBeginDrag;
            _handView.CardDragged += HandleCardDragged;
            _handView.CardEndDrag += HandleCardEndDrag;
        }

        private void UnsubscribeView()
        {
            if (_handView == null) return;
            _handView.CardClicked -= HandleCardClicked;
            _handView.CardBeginDrag -= HandleCardBeginDrag;
            _handView.CardDragged -= HandleCardDragged;
            _handView.CardEndDrag -= HandleCardEndDrag;
        }

        private void HandleBootstrapChanged()
        {
            TryHideLegacyStorageTray();
            RefreshSessionBinding();
        }

        private void RefreshSessionBinding()
        {
            GridRunSession nextSession = _bootstrap != null ? _bootstrap.GridSession : null;
            GridManager nextManager = nextSession != null ? nextSession.Grid : null;

            if (ReferenceEquals(nextSession, _session) && ReferenceEquals(nextManager, _manager))
            {
                UpdateViewInteractability();
                return;
            }

            CancelOwnedDrag();
            if (_manager != null)
            {
                _manager.Changed -= HandleManagerChanged;
                _manager.LayoutChanged -= HandleLayoutChanged;
            }

            _session = nextSession;
            _manager = nextManager;

            if (_manager != null)
            {
                _manager.Changed += HandleManagerChanged;
                _manager.LayoutChanged += HandleLayoutChanged;
            }
            RefreshItems();
        }

        private void UnbindManager()
        {
            if (_manager != null)
            {
                _manager.Changed -= HandleManagerChanged;
                _manager.LayoutChanged -= HandleLayoutChanged;
            }
            _manager = null;
            _session = null;
            _isShowingRewardSelection = false;
            _shownRewardRequestId = null;
        }

        private void HandleManagerChanged()
        {
            bool shouldShowRewards = ShouldShowRewardSelection();
            string requestId = shouldShowRewards ? _bootstrap?.Rewards?.Pending?.RequestId : null;
            if (shouldShowRewards != _isShowingRewardSelection || requestId != _shownRewardRequestId)
                RefreshItems();
            else
                UpdateViewInteractability();
        }

        private void HandleLayoutChanged()
        {
            RefreshItems();
        }

        private void RefreshItems()
        {
            _identities.Clear();
            _rewardOptionIndices.Clear();

            if (_manager == null)
            {
                _isShowingRewardSelection = false;
                _shownRewardRequestId = null;
                if (_handView != null)
                {
                    _handView.SetInteractable(false);
                    _handView.Clear();
                    _hasLastViewInteractable = false;
                }
                return;
            }

            if (ShouldShowRewardSelection())
            {
                RefreshRewardItems();
                return;
            }

            _isShowingRewardSelection = false;
            _shownRewardRequestId = null;

            IReadOnlyList<GridStoredItem> storedItems = _manager.GetStoredItems();
            var displayItems = new List<BattleHandCardDisplayData>(storedItems.Count);

            foreach (GridStoredItem storedItem in storedItems)
            {
                BattleHandCardDisplayData displayItem = CreateDisplayItem(storedItem);
                if (displayItem == null) continue;

                displayItems.Add(displayItem);
                _identities[displayItem.Id] = new StoredIdentity(storedItem.Kind, storedItem.InstanceId);
            }

            if (_ownsDrag && !_identities.ContainsKey(_draggedStableId)) CancelOwnedDrag();

            if (_handView != null)
            {
                _handView.SetItems(displayItems);
                _hasLastViewInteractable = false;
            }
            UpdateViewInteractability();
        }

        private bool ShouldShowRewardSelection()
        {
            StageGridRewards rewards = _bootstrap != null ? _bootstrap.Rewards : null;
            return _manager != null && rewards?.Pending != null &&
                _session != null && _session.PendingRewardId == rewards.Pending.RequestId &&
                _manager.Phase == eGridPhase.REWARD && !_manager.HasPendingStorage;
        }

        private void RefreshRewardItems()
        {
            StageGridRewards rewards = _bootstrap.Rewards;
            string requestId = rewards.Pending.RequestId;
            IReadOnlyList<GeneralRewardOption> candidates = rewards.Candidates;
            var displayItems = new List<BattleHandCardDisplayData>(candidates.Count);

            for (int i = 0; i < candidates.Count; i++)
            {
                BattleHandCardDisplayData displayItem = CreateRewardDisplayItem(requestId, i, candidates[i]);
                if (displayItem == null) continue;

                displayItems.Add(displayItem);
                _rewardOptionIndices[displayItem.Id] = i;
            }

            _isShowingRewardSelection = true;
            _shownRewardRequestId = requestId;
            if (_ownsDrag) CancelOwnedDrag();
            if (_handView != null)
            {
                _handView.SetItems(displayItems);
                _hasLastViewInteractable = false;
            }
            UpdateViewInteractability();
        }

        private BattleHandCardDisplayData CreateRewardDisplayItem(
            string requestId,
            int optionIndex,
            GeneralRewardOption option)
        {
            if (option == null) return null;
            string id = string.Concat(REWARD_ID_PREFIX, requestId, ":", optionIndex.ToString(CultureInfo.InvariantCulture));

            if (option.Kind == eGeneralRewardKind.EXPANSION)
            {
                return new BattleHandCardDisplayData(
                    id,
                    eBattleHandCardKind.LAND_SLOT,
                    "배치 영역 확장",
                    areaTitle: "웨이브 보상 · " + (option.ExpansionShape ?? _manager.Definition.Expansion).Cells.Count.ToString(CultureInfo.InvariantCulture) + "칸",
                    footprint: CreateDisplayFootprint(option.ExpansionShape ?? _manager.Definition.Expansion, 0, false));
            }

            UIUnitCatalogSO.Entry catalogEntry = FindUnitCatalogEntry(option.Unit.Id);
            string title = catalogEntry != null && !string.IsNullOrWhiteSpace(catalogEntry.DisplayName)
                ? catalogEntry.DisplayName
                : option.Unit.DisplayName;
            GetUnitStatTexts(option.Unit.Id, option.StarLevel, out string attack, out string defense, out string health);
            return new BattleHandCardDisplayData(
                id,
                eBattleHandCardKind.UNIT,
                title,
                rankText: option.StarLevel.ToString(CultureInfo.InvariantCulture),
                attack: attack,
                defense: defense,
                health: health,
                areaTitle: "웨이브 보상",
                areaDescription: catalogEntry != null ? catalogEntry.Description : string.Empty,
                artwork: catalogEntry != null ? catalogEntry.Portrait : null,
                footprint: CreateDisplayFootprint(option.Block, 0, false));
        }

        private BattleHandCardDisplayData CreateDisplayItem(GridStoredItem storedItem)
        {
            if (storedItem == null) return null;

            if (storedItem.Kind == eGridDragKind.UNIT)
            {
                UnitPlacement unit = _manager.FindUnit(storedItem.InstanceId);
                if (unit == null) return null;

                FootprintDefinition footprint = unit.Definition.GetFootprint(unit.StarLevel);
                UIUnitCatalogSO.Entry catalogEntry = FindUnitCatalogEntry(unit.Definition.Id);
                string title = catalogEntry != null && !string.IsNullOrWhiteSpace(catalogEntry.DisplayName)
                    ? catalogEntry.DisplayName
                    : storedItem.DisplayName;
                string description = catalogEntry != null ? catalogEntry.Description : string.Empty;

                GetUnitStatTexts(unit, out string attack, out string defense, out string health);
                return new BattleHandCardDisplayData(
                    string.Concat(UNIT_ID_PREFIX, storedItem.InstanceId),
                    eBattleHandCardKind.UNIT,
                    title,
                    rankText: unit.StarLevel.ToString(CultureInfo.InvariantCulture),
                    attack: attack,
                    defense: defense,
                    health: health,
                    areaTitle: string.IsNullOrEmpty(description) ? string.Empty : "설명",
                    areaDescription: description,
                    artwork: catalogEntry != null ? catalogEntry.Portrait : null,
                    footprint: CreateDisplayFootprint(footprint, unit.Rotation, unit.IsMirrored));
            }

            if (storedItem.Kind == eGridDragKind.BLOCK)
            {
                BlockPlacement block = _manager.FindBlock(storedItem.InstanceId);
                if (block == null) return null;

                return new BattleHandCardDisplayData(
                    string.Concat(BLOCK_ID_PREFIX, storedItem.InstanceId),
                    eBattleHandCardKind.LAND_SLOT,
                    storedItem.DisplayName,
                    footprint: CreateDisplayFootprint(block.Footprint, block.Rotation, block.IsMirrored));
            }

            return null;
        }

        private UIUnitCatalogSO.Entry FindUnitCatalogEntry(string unitDefinitionId)
        {
            if (_unitCatalog == null || string.IsNullOrWhiteSpace(unitDefinitionId)) return null;

            string entryId = string.Concat("unit.", unitDefinitionId);
            IReadOnlyList<UIUnitCatalogSO.Entry> entries = _unitCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                UIUnitCatalogSO.Entry entry = entries[i];
                if (entry != null && entry.Id == entryId) return entry;
            }
            return null;
        }

        private void GetUnitStatTexts(UnitPlacement unit, out string attack, out string defense, out string health)
        {
            GetUnitStatTexts(unit.Definition.Id, unit.StarLevel, out attack, out defense, out health);
        }

        private void GetUnitStatTexts(
            string unitDefinitionId,
            int starLevel,
            out string attack,
            out string defense,
            out string health)
        {
            attack = string.Empty;
            defense = string.Empty;
            health = string.Empty;

            InGamePrototypeConfigSO config = _bootstrap != null ? _bootstrap.Config : null;
            DemonArmyCatalog armyCatalog = config != null ? config.DemonArmyCatalog : null;
            UnitBase prefab = armyCatalog != null
                ? armyCatalog.FindPrefab(unitDefinitionId, starLevel)
                : null;
            UnitStatData stats = prefab != null ? prefab.statData : null;
            if (stats == null) return;

            attack = stats.attackPower.ToString("0.#", CultureInfo.InvariantCulture);
            defense = string.Concat(
                (stats.defensePercent * 100f).ToString("0.#", CultureInfo.InvariantCulture), "%");
            health = stats.maxHealth.ToString(CultureInfo.InvariantCulture);
        }

        private static Vector2Int[] CreateDisplayFootprint(
            FootprintDefinition footprint, int rotation, bool isMirrored)
        {
            if (footprint == null) return new Vector2Int[0];

            Vector2Int[] cells = footprint.GetCells(Vector2Int.zero, rotation, isMirrored);
            if (cells == null || cells.Length == 0) return new Vector2Int[0];

            int minX = cells[0].x;
            int maxY = cells[0].y;
            for (int i = 1; i < cells.Length; i++)
            {
                minX = Mathf.Min(minX, cells[i].x);
                maxY = Mathf.Max(maxY, cells[i].y);
            }

            var normalized = new Vector2Int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                // Grid의 +Y(위)를 카드 미리보기의 +Y(아래) 좌표로 뒤집어 표시한다.
                normalized[i] = new Vector2Int(cells[i].x - minX, maxY - cells[i].y);
            }
            return normalized;
        }

        private void HandleCardClicked(UIBattleCardHandSlot slot, PointerEventData eventData)
        {
            if (slot == null || eventData == null || eventData.button != PointerEventData.InputButton.Left ||
                !_isShowingRewardSelection || !_rewardOptionIndices.TryGetValue(slot.Id, out int optionIndex)) return;

            StageGridRewards rewards = _bootstrap != null ? _bootstrap.Rewards : null;
            string requestId = rewards?.Pending?.RequestId;
            if (string.IsNullOrEmpty(requestId) || requestId != _shownRewardRequestId ||
                _bootstrap.IsUiInputBlocked) return;

            // TrySelect가 false여도 보관함 초과 처리가 시작됐을 수 있으므로 최신 Grid 상태로 다시 그린다.
            rewards.TrySelect(requestId, optionIndex);
            RefreshItems();
        }

        private void HandleCardBeginDrag(UIBattleCardHandSlot slot, PointerEventData eventData)
        {
            if (slot == null || eventData == null || eventData.button != PointerEventData.InputButton.Left ||
                !CanStartDrag() || !_identities.TryGetValue(slot.Id, out StoredIdentity identity)) return;

            // 기존 UI Toolkit root가 포커스를 쥔 상태라면 선택을 시작하기 전에 해제한다.
            // 선택 후 Blur하면 GridDragInput.OnFocusOut이 새 선택을 취소할 수 있다.
            BlurLegacyPanelFocus();

            _ownsDrag = true;
            _dragPointerId = eventData.pointerId;
            _draggedStableId = slot.Id;
            _draggedInstanceId = identity.InstanceId;
            _dragKind = identity.Kind;

            bool began = identity.Kind == eGridDragKind.UNIT
                ? _manager.BeginUnitDrag(identity.InstanceId)
                : identity.Kind == eGridDragKind.BLOCK && _manager.BeginBlockDrag(identity.InstanceId);

            if (!began)
            {
                ClearOwnedDragState();
                _handView.ResetTransientInteraction();
                UpdateViewInteractability();
                return;
            }

            MoveOwnedPreview(eventData);
            UpdateViewInteractability();
        }

        private void HandleCardDragged(UIBattleCardHandSlot slot, PointerEventData eventData)
        {
            if (!IsOwnedPointer(slot, eventData)) return;
            if (!CanContinueOwnedDrag())
            {
                CancelOwnedDrag();
                return;
            }

            MoveOwnedPreview(eventData);
        }

        private void HandleCardEndDrag(UIBattleCardHandSlot slot, PointerEventData eventData)
        {
            if (!IsOwnedPointer(slot, eventData)) return;
            if (!CanContinueOwnedDrag())
            {
                CancelOwnedDrag();
                return;
            }

            MoveOwnedPreview(eventData);

            Camera eventCamera = eventData.enterEventCamera != null
                ? eventData.enterEventCamera
                : eventData.pressEventCamera;
            UIBattleCardHandSlot targetSlot = null;
            bool isOverSourceSlot = slot.ContainsVisualScreenPoint(eventData.position, eventCamera);
            // 겹침 배치에서는 원래 카드 아래에도 다른 카드의 입력 영역이 있을 수 있다.
            // 시작 카드 위에 그대로 놓은 경우에는 합성 대상을 찾지 않고 단순 보관함 복귀로 처리한다.
            if (!isOverSourceSlot && _handView != null)
                _handView.TryGetSlotUnderScreenPoint(eventData.position, eventCamera, out targetSlot, slot);

            bool isOverHand = isOverSourceSlot || targetSlot != null ||
                (_handView != null && _handView.IsScreenPointInsideDropZone(eventData.position, eventCamera));

            bool completed;
            if (isOverHand)
            {
                completed = TryFuseWithTarget(targetSlot);
                if (!completed && IsManagerSelectionOwned()) completed = _manager.DropToTray();
            }
            else
            {
                completed = _manager.CommitPreview();
            }

            if (!completed && !_manager.HasPendingStorage && IsManagerSelectionOwned()) _manager.CancelDrag();

            ClearOwnedDragState();
            UpdateViewInteractability();
        }

        private bool TryFuseWithTarget(UIBattleCardHandSlot targetSlot)
        {
            if (_dragKind != eGridDragKind.UNIT || targetSlot == null ||
                !_identities.TryGetValue(targetSlot.Id, out StoredIdentity target) ||
                target.Kind != eGridDragKind.UNIT || target.InstanceId == _draggedInstanceId ||
                !_manager.CanFuseUnits(_draggedInstanceId, target.InstanceId)) return false;

            return _manager.TryFuseUnits(_draggedInstanceId, target.InstanceId);
        }

        private bool CanStartDrag()
        {
            return isActiveAndEnabled && _handView != null && _manager != null &&
                _phasePresentation != null && _phasePresentation.Surface != null &&
                _phasePresentation.CanInteract && _manager.Phase == eGridPhase.PREPARATION &&
                !_manager.HasPendingStorage && !_manager.HasSelection;
        }

        private bool CanContinueOwnedDrag()
        {
            return _ownsDrag && _manager != null && _phasePresentation != null &&
                _phasePresentation.Surface != null && _phasePresentation.CanInteract &&
                !_manager.HasPendingStorage && IsManagerSelectionOwned();
        }

        private bool IsOwnedPointer(UIBattleCardHandSlot slot, PointerEventData eventData)
        {
            return _ownsDrag && slot != null && eventData != null &&
                slot.Id == _draggedStableId && eventData.pointerId == _dragPointerId;
        }

        private bool IsManagerSelectionOwned()
        {
            return _ownsDrag && _manager != null && _manager.HasSelection &&
                _manager.DragKind == _dragKind && _manager.SelectedId == _draggedInstanceId;
        }

        private void MoveOwnedPreview(PointerEventData eventData)
        {
            if (!IsManagerSelectionOwned() || eventData == null) return;

            _dragPointerPosition = eventData.position;

            GridWorldInputSurface surface = _phasePresentation != null
                ? _phasePresentation.Surface
                : null;
            if (surface != null) _manager.MovePreview(surface.ScreenToCell(_dragPointerPosition));
        }

        private void HandleOwnedDragKeyboard()
        {
            if (!_ownsDrag) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelOwnedDrag();
                return;
            }

            if (!CanContinueOwnedDrag()) return;

            bool changed = false;
            if (keyboard.rKey.wasPressedThisFrame)
            {
                _manager.RotatePreview();
                changed = true;
            }
            else if (keyboard.fKey.wasPressedThisFrame && !_manager.IsExpansionDrag)
            {
                _manager.MirrorPreview();
                changed = true;
            }

            if (changed && _phasePresentation?.Surface != null)
                _manager.MovePreview(_phasePresentation.Surface.ScreenToCell(_dragPointerPosition));
        }

        private void BlurLegacyPanelFocus()
        {
            FocusController focusController = _legacyDocument?.rootVisualElement?.panel?.focusController;
            focusController?.focusedElement?.Blur();
        }

        private void CancelOwnedDrag()
        {
            if (!_ownsDrag || _isCancellingOwnedDrag) return;

            _isCancellingOwnedDrag = true;
            try
            {
                if (IsManagerSelectionOwned()) _manager.CancelDrag();
                ClearOwnedDragState();
                _handView?.ResetTransientInteraction();
            }
            finally
            {
                _isCancellingOwnedDrag = false;
            }
            UpdateViewInteractability();
        }

        private void ClearOwnedDragState()
        {
            _ownsDrag = false;
            _dragPointerId = 0;
            _draggedStableId = null;
            _draggedInstanceId = null;
            _dragKind = eGridDragKind.NONE;
            _dragPointerPosition = Vector2.zero;
        }

        private void UpdateViewInteractability()
        {
            if (_handView == null) return;

            bool canInteract;
            if (_isShowingRewardSelection)
            {
                StageGridRewards rewards = _bootstrap != null ? _bootstrap.Rewards : null;
                canInteract = isActiveAndEnabled && _manager != null &&
                    rewards?.Pending != null && rewards.Pending.RequestId == _shownRewardRequestId &&
                    _session != null && _session.PendingRewardId == _shownRewardRequestId &&
                    _manager.Phase == eGridPhase.REWARD && !_manager.HasPendingStorage &&
                    !_manager.HasSelection && !_bootstrap.IsUiInputBlocked;
            }
            else
            {
                canInteract = isActiveAndEnabled && _manager != null &&
                    _phasePresentation != null && _phasePresentation.Surface != null &&
                    _phasePresentation.CanInteract && _manager.Phase == eGridPhase.PREPARATION &&
                    !_manager.HasPendingStorage && (!_manager.HasSelection || IsManagerSelectionOwned());
            }

            // View를 잠그면 Slot이 drag flag를 먼저 내리므로, 선택 상태를 그 전에 원자적으로 정리한다.
            if (_ownsDrag && !canInteract && !_isCancellingOwnedDrag)
            {
                CancelOwnedDrag();
                return;
            }

            if (_hasLastViewInteractable && _lastViewInteractable == canInteract) return;

            _lastViewInteractable = canInteract;
            _hasLastViewInteractable = true;
            _handView.SetInteractable(canInteract);
        }

        private void TryHideLegacyStorageTray()
        {
            if (!_hideLegacyStorageTray || _legacyDocument == null || _hasLegacyVisibilitySnapshot ||
                _handView == null || _bootstrap == null || _phasePresentation == null || _manager == null) return;

            VisualElement root = _legacyDocument.rootVisualElement;
            if (root == null) return;

            VisualElement tray = root.Q<VisualElement>(LEGACY_TRAY_NAME);
            if (tray == null) return;

            _legacyTray = tray;
            _legacyVisibility = tray.style.visibility;
            _hasLegacyVisibilitySnapshot = true;
            // worldBound는 기존 GridDragInput의 전장→보관함 드롭 판정에 계속 필요하다.
            // 레이아웃을 제거하지 않고 시각과 포인터 대상만 숨겨 기존 반환 기능을 유지한다.
            tray.style.visibility = Visibility.Hidden;
        }

        private void RestoreLegacyStorageTray()
        {
            if (_hasLegacyVisibilitySnapshot && _legacyTray != null)
                _legacyTray.style.visibility = _legacyVisibility;

            _legacyTray = null;
            _hasLegacyVisibilitySnapshot = false;
        }

        private readonly struct StoredIdentity
        {
            public eGridDragKind Kind { get; }
            public string InstanceId { get; }

            public StoredIdentity(eGridDragKind kind, string instanceId)
            {
                Kind = kind;
                InstanceId = instanceId;
            }
        }
    }
}
