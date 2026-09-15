using OZGL2.Grid.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.Prototype
{
    /// <summary>실제 보상/전투 연결 전 준비 흐름을 재현하는 더미 화면.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GridPrototypeRunner : MonoBehaviour
    {
        [SerializeField] private GridBoardThemeSO _boardTheme;
        private VisualElement _root;
        private VisualElement _tray;
        private VisualElement _storageOverlay;
        private string _storageRequestId;
        private readonly System.Collections.Generic.HashSet<GridStoredItem> _discard = new System.Collections.Generic.HashSet<GridStoredItem>();
        private Label _status;
        private Label _message;
        private Button _start;
        private Button _skip;
        private Button _clear;
        private Button _reward;
        private Button _unitRewardA;
        private Button _unitRewardB;
        private VisualElement _expansionCard;
        private GridDragInput _input;
        private GridPrototypeFlow _flow;
        private bool _isBound;
        public GridRunSession Session { get; private set; }
        public GridManager Manager => Session?.Grid;
        public GridBoardView Board { get; private set; }
        public GridPrototypeFlow PrototypeFlow => _flow;
        public string BindingError { get; private set; }
        private void OnEnable()
        {
            if (Session != null && Session.IsEnded) ShowUnavailable("Grid run ended.");
            else if (Session != null) Attach();
        }
        private void Start()
        {
            if (Session == null) ShowUnavailable("Grid session is not connected. Bind a session before using this screen.");
        }
        public void Bind(GridRunSession session, GridPrototypeFlow flow = null)
        {
            if (session == null || session.IsEnded) throw new System.ArgumentException("Active session required.");
            if (Session != null && !Session.IsEnded && !ReferenceEquals(Session, session))
                throw new System.InvalidOperationException("Unbind the current session before replacing it.");
            if (flow != null && !ReferenceEquals(flow.Session, session)) throw new System.ArgumentException("Flow belongs to another session.");
            Detach(); Session = session;
            _flow = flow; BindingError = null;
            if (isActiveAndEnabled) Attach();
        }
        public void Unbind() { Detach(); Session = null; _flow = null; }
        private void ShowUnavailable(string message)
        {
            BindingError = message;
            _root = GetComponent<UIDocument>().rootVisualElement;
            _root.Clear(); _root.Add(new Label(message));
        }
        private void Attach()
        {
            if (_isBound || Session.IsEnded) return;
            Build(); Manager.Changed += Render; Render();
            _isBound = true;
        }
        private void Build()
        {
            _root = GetComponent<UIDocument>().rootVisualElement; _root.Clear();
            _root.style.backgroundColor = new Color(0.035f, 0.055f, 0.09f);
            _root.style.alignItems = Align.Center; _root.style.justifyContent = Justify.Center;
            _root.style.color = new Color(0.87f, 0.92f, 1); _root.style.fontSize = 18;
            var title = new Label("CASTLE GRID  /  PREPARATION PROTOTYPE"); title.style.fontSize = 26; _root.Add(title);
            _status = new Label(); _status.style.marginTop = 10; _root.Add(_status);
            _message = new Label(); _message.style.marginTop = 8; _message.style.height = 28; _root.Add(_message);
            Board = new GridBoardView(Manager, _boardTheme); _root.Add(Board.Element);
            _root.Add(new Label("STORAGE / Units first · R: rotate · Esc: cancel · Drag here to return"));
            _tray = CreateTray("storage-tray", 116); _root.Add(_tray);
            _input = new GridDragInput(Manager, Board, _root, _tray);
            _expansionCard = new Label("FLOOR +2  —  Drag this required reward onto the dotted area") { name = "expansion-card" };
            _expansionCard.style.height = 44; _expansionCard.style.width = 780;
            _expansionCard.style.unityTextAlign = TextAnchor.MiddleCenter;
            _expansionCard.style.backgroundColor = new Color(0.14f, 0.35f, 0.28f);
            _expansionCard.style.marginTop = 10;
            _expansionCard.RegisterCallback<PointerDownEvent>(_input.BeginExpansion); _root.Add(_expansionCard);
            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row; actions.style.marginTop = 14; _root.Add(actions);
            _start = AddButton(actions, "Start battle", () => Session.TryBeginBattle(Session.RunId, Session.NextRound));
            _skip = AddButton(actions, "Skip preparation", () => Session.TryBeginBattle(Session.RunId, Session.NextRound, true));
            _clear = AddButton(actions, "Simulate round clear", () => _flow?.TryFinishBattle());
            _reward = AddButton(actions, "Choose floor +2", () => _flow?.TryChooseExpansion());
            _unitRewardA = AddButton(actions, "Unit A", () => _flow?.TryChooseUnit(0));
            _unitRewardB = AddButton(actions, "Unit B", () => _flow?.TryChooseUnit(1));
            _storageOverlay = new VisualElement { name = "storage-resolution" };
            _storageOverlay.style.position = Position.Absolute;
            _storageOverlay.style.left = _storageOverlay.style.right = _storageOverlay.style.top = _storageOverlay.style.bottom = 0;
            _storageOverlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.98f);
            _storageOverlay.style.alignItems = Align.Center; _storageOverlay.style.justifyContent = Justify.Center;
            _root.Add(_storageOverlay);
        }
        private static VisualElement CreateTray(string name, float height)
        {
            var tray = new ScrollView(ScrollViewMode.Horizontal) { name = name };
            tray.contentContainer.style.flexDirection = FlexDirection.Row;
            tray.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            tray.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            tray.style.width = 1100; tray.style.height = height; tray.style.marginTop = 8;
            tray.style.backgroundColor = new Color(0.07f, 0.10f, 0.15f); return tray;
        }
        private static Button AddButton(VisualElement parent, string text, System.Action action)
        {
            var button = new Button(action) { text = text }; button.style.height = 40; button.style.marginLeft = 4; button.style.marginRight = 4;
            parent.Add(button); return button;
        }
        private void Render()
        {
            if (Session.IsEnded) { Detach(); ShowUnavailable("Grid run ended."); return; }
            Board.Render();
            _status.text = Manager.Phase + "  |  FLOOR " + Manager.FloorCells.Count + "/" +
                (Manager.Definition.MaximumSize.x * Manager.Definition.MaximumSize.y) + "  |  DEPLOYED " + Manager.PlacedCount + "  |  STORAGE " + Manager.StoredCount + "/" + Manager.Definition.StorageCapacity;
            _message.text = Manager.HasSelection ? (Manager.CanFusePreview ? "Fuse: same unit + same star" : Manager.GetPreviewFailure() == ePlacementFailure.NONE ? "Valid placement" : "Cannot place: " + Manager.GetPreviewFailure()) :
                Manager.Phase == eGridPhase.WAITING || (Manager.Phase == eGridPhase.REWARD && Session.PendingRewardId == null) ? "Waiting for preparation permission." :
                Manager.RequiresExpansionPlacement ? "Place the floor reward before starting or skipping." :
                Manager.Phase == eGridPhase.REWARD ? "Round clear: choose a reward to enter the next preparation." :
                Manager.Phase == eGridPhase.BATTLE ? "Battle active: placement is locked." :
                Manager.PlacedCount == 0 ? "Deploy at least one unit. The king does not count." : "Ready. Rearrange units or start the next battle.";
            // 드래그 중 카드의 레이아웃과 드롭 대상을 유지한다.
            if (!Manager.HasSelection)
            {
                _tray.Clear();
                foreach (var item in Manager.GetStoredItems())
                {
                    var shape = item.Kind == eGridDragKind.UNIT ? Manager.FindUnit(item.InstanceId).Definition.Footprint : Manager.FindBlock(item.InstanceId).Footprint;
                    var card = CreateStorageCard(item, shape);
                    if (item.Kind == eGridDragKind.UNIT)
                    {
                        card.userData = item.InstanceId;
                        card.Q<Label>().text += " / " + Manager.FindUnit(item.InstanceId).StarLevel + "★";
                    }
                    card.style.opacity = Manager.DragKind == item.Kind && Manager.SelectedId == item.InstanceId ? 0.4f : 1;
                    card.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (item.Kind == eGridDragKind.UNIT) _input.BeginCard(item.InstanceId, evt);
                        else _input.BeginBlockCard(item.InstanceId, evt);
                    });
                    _tray.Add(card);
                }
            }
            foreach (var card in _tray.Children())
                card.style.opacity = card.name == "card-" + Manager.SelectedId || card.name == "block-card-" + Manager.SelectedId ? 0.4f : 1;
            _expansionCard.style.display = Manager.Phase == eGridPhase.PREPARATION && Manager.RequiresExpansionPlacement ? DisplayStyle.Flex : DisplayStyle.None;
            _start.SetEnabled(Manager.CanBeginBattle); _skip.SetEnabled(Manager.CanSkipPreparation);
            _clear.style.display = _flow != null && Manager.Phase == eGridPhase.BATTLE ? DisplayStyle.Flex : DisplayStyle.None;
            bool hasReward = !Manager.HasPendingStorage && _flow != null && Manager.Phase == eGridPhase.REWARD && Session.PendingRewardId != null;
            _reward.style.display = hasReward && Manager.CanExpand ? DisplayStyle.Flex : DisplayStyle.None;
            _unitRewardA.style.display = _unitRewardB.style.display = hasReward ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasReward)
            {
                _unitRewardA.text = "Choose " + _flow.Rewards.GetCandidate(Session.CompletedRounds, 0).DisplayName + " + block";
                _unitRewardB.text = "Choose " + _flow.Rewards.GetCandidate(Session.CompletedRounds, 1).DisplayName + " + block";
            }
            RenderStorageRequest();
        }
        private static VisualElement CreateStorageCard(GridStoredItem item, FootprintDefinition shape)
        {
            var card = new VisualElement { name = (item.Kind == eGridDragKind.UNIT ? "card-" : "block-card-") + item.InstanceId };
            card.style.width = 170; card.style.flexShrink = 0; card.style.height = 86; card.style.marginTop = 6; card.style.marginLeft = 6;
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = item.Kind == eGridDragKind.UNIT ? new Color(0.28f, 0.22f, 0.14f) : new Color(0.16f, 0.22f, 0.32f);
            var label = new Label(item.Kind + " / " + item.DisplayName) { pickingMode = PickingMode.Ignore }; label.style.fontSize = 13; card.Add(label);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = 80; icon.style.height = 50; card.Add(icon);
            foreach (var cell in shape.Cells)
            {
                var square = new VisualElement { pickingMode = PickingMode.Ignore };
                square.style.position = Position.Absolute; square.style.width = 9; square.style.height = 9;
                square.style.left = 32 + cell.x * 10; square.style.top = 20 - cell.y * 10;
                square.style.backgroundColor = cell == Vector2Int.zero ? new Color(1, 0.8f, 0.35f) : new Color(0.4f, 0.7f, 1); icon.Add(square);
            }
            return card;
        }
        private void RenderStorageRequest()
        {
            var request = Manager.PendingStorage;
            _storageOverlay.style.display = request == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (request == null) { _storageRequestId = null; _discard.Clear(); return; }
            var oldChoices = _storageOverlay.Q<ScrollView>("discard-candidates");
            var scrollOffset = oldChoices != null && _storageRequestId == request.RequestId ? oldChoices.scrollOffset : Vector2.zero;
            if (_storageRequestId != request.RequestId) { _storageRequestId = request.RequestId; _discard.Clear(); }
            _storageOverlay.Clear();
            _storageOverlay.Add(new Label("STORAGE FULL / Select " + request.RequiredDiscardCount + " existing items to discard"));
            var incoming = new System.Collections.Generic.List<string>();
            foreach (var item in request.Incoming) incoming.Add(item.Kind + ": " + item.DisplayName);
            _storageOverlay.Add(new Label("Incoming: " + string.Join(", ", incoming)));
            var choices = CreateTray("discard-candidates", 100); _storageOverlay.Add(choices);
            foreach (var item in request.DiscardCandidates)
            {
                var toggle = new Toggle(item.Kind + " / " + item.DisplayName) { name = "discard-" + item.Kind + "-" + item.InstanceId };
                toggle.style.width = 190; toggle.style.height = 72; toggle.style.flexShrink = 0;
                toggle.labelElement.style.whiteSpace = WhiteSpace.Normal; toggle.labelElement.style.fontSize = 14;
                toggle.tooltip = item.Kind + " / " + item.DisplayName;
                toggle.SetValueWithoutNotify(_discard.Contains(item));
                toggle.RegisterValueChangedCallback(evt => { if (evt.newValue) _discard.Add(item); else _discard.Remove(item); RenderStorageRequest(); });
                choices.Add(toggle);
            }
            choices.schedule.Execute(() => ((ScrollView)choices).scrollOffset = scrollOffset);
            _storageOverlay.Add(new Label("Selected " + _discard.Count + "/" + request.RequiredDiscardCount + " · Items are deleted only on confirmation."));
            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row; _storageOverlay.Add(actions);
            var confirm = AddButton(actions, "Discard and receive", () =>
            {
                var selected = new System.Collections.Generic.List<GridStoredItem>(_discard);
                if (_flow != null) _flow.TryConfirmStorage(request.RequestId, selected);
                else Session.TryConfirmStorage(Session.RunId, request.RequestId, selected);
            });
            confirm.name = "confirm-storage"; confirm.SetEnabled(_discard.Count == request.RequiredDiscardCount);
            var cancel = AddButton(actions, "Cancel", () => Session.TryCancelStorage(Session.RunId, request.RequestId)); cancel.name = "cancel-storage";
        }
        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) _input?.Cancel(); }
        private void OnDisable()
        { Detach(); }
        private void Detach()
        {
            if (Manager != null) Manager.Changed -= Render;
            _input?.Dispose(); _input = null; _root?.Clear();
            _isBound = false;
        }
    }
}
