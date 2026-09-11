using OZGL2.Grid.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.Grid.Prototype
{
    /// <summary>실제 보상/전투 연결 전 준비 흐름을 재현하는 더미 화면.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GridPrototypeRunner : MonoBehaviour
    {
        private VisualElement _root;
        private VisualElement _tray;
        private VisualElement _blockTray;
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
            Board = new GridBoardView(Manager); _root.Add(Board.Element);
            _root.Add(new Label("BLOCKS  /  R: rotate · Esc: cancel · Shift + drag: move an occupied block"));
            _blockTray = CreateTray("block-tray", 112); _root.Add(_blockTray);
            _root.Add(new Label("UNIT CARDS  /  Place on a matching empty block · Drag unit marker to move"));
            _tray = CreateTray("unit-tray", 98); _root.Add(_tray);
            _input = new GridDragInput(Manager, Board, _root, _tray, _blockTray);
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
                (Manager.Definition.MaximumSize.x * Manager.Definition.MaximumSize.y) + "  |  DEPLOYED " + Manager.PlacedCount;
            _message.text = Manager.HasSelection ? (Manager.GetPreviewFailure() == ePlacementFailure.NONE ? "Valid placement" : "Cannot place: " + Manager.GetPreviewFailure()) :
                Manager.Phase == eGridPhase.WAITING || (Manager.Phase == eGridPhase.REWARD && Session.PendingRewardId == null) ? "Waiting for preparation permission." :
                Manager.RequiresExpansionPlacement ? "Place the floor reward before starting or skipping." :
                Manager.Phase == eGridPhase.REWARD ? "Round clear: choose a reward to enter the next preparation." :
                Manager.Phase == eGridPhase.BATTLE ? "Battle active: placement is locked." :
                Manager.PlacedCount == 0 ? "Deploy at least one unit. The king does not count." : "Ready. Rearrange units or start the next battle.";
            _blockTray.Clear();
            foreach (var unit in Manager.Blocks)
            {
                if (unit.IsPlaced) continue;
                string id = unit.InstanceId;
                var card = new VisualElement { name = "block-card-" + id };
                card.style.width = 170; card.style.flexShrink = 0; card.style.height = 86; card.style.marginTop = 6; card.style.marginLeft = 6;
                card.style.alignItems = Align.Center; card.style.backgroundColor = new Color(0.16f, 0.22f, 0.32f);
                card.style.opacity = Manager.DragKind == eGridDragKind.BLOCK && Manager.SelectedId == id ? 0.35f : 1;
                var name = new Label(unit.Footprint.DisplayName) { pickingMode = PickingMode.Ignore }; name.style.marginTop = 5; card.Add(name);
                var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = 80; icon.style.height = 44; card.Add(icon);
                foreach (var cell in unit.Footprint.Cells)
                {
                    var square = new VisualElement { pickingMode = PickingMode.Ignore };
                    square.style.position = Position.Absolute; square.style.width = 9; square.style.height = 9;
                    square.style.left = 32 + cell.x * 10; square.style.top = 12 - cell.y * 10;
                    square.style.backgroundColor = cell == Vector2Int.zero ? new Color(1, 0.8f, 0.35f) : new Color(0.4f, 0.7f, 1); icon.Add(square);
                }
                card.RegisterCallback<PointerDownEvent>(evt => _input.BeginBlockCard(id, evt)); _blockTray.Add(card);
            }
            _tray.Clear();
            foreach (var unit in Manager.Units)
            {
                bool isPendingReturn = Manager.IsUnitTemporarilyReturned(unit);
                if (unit.IsPlaced && !isPendingReturn) continue;
                string id = unit.InstanceId;
                var card = new Label(unit.Definition.DisplayName + "\nRequires: " + GetRequiredBlockName(unit.Definition.RequiredBlockId) +
                    (isPendingReturn ? "\nReturn pending" : "")) { name = "card-" + id };
                card.style.width = 170; card.style.flexShrink = 0; card.style.height = 72; card.style.fontSize = 14;
                card.style.marginTop = 6; card.style.marginLeft = 6; card.style.unityTextAlign = TextAnchor.MiddleCenter;
                card.style.backgroundColor = new Color(0.28f, 0.22f, 0.14f);
                card.style.opacity = isPendingReturn || (Manager.DragKind == eGridDragKind.UNIT && Manager.SelectedId == id) ? 0.4f : 1;
                card.RegisterCallback<PointerDownEvent>(evt => _input.BeginCard(id, evt)); _tray.Add(card);
            }
            _expansionCard.style.display = Manager.Phase == eGridPhase.PREPARATION && Manager.RequiresExpansionPlacement ? DisplayStyle.Flex : DisplayStyle.None;
            _start.SetEnabled(Manager.CanBeginBattle); _skip.SetEnabled(Manager.CanSkipPreparation);
            _clear.style.display = _flow != null && Manager.Phase == eGridPhase.BATTLE ? DisplayStyle.Flex : DisplayStyle.None;
            bool hasReward = _flow != null && Manager.Phase == eGridPhase.REWARD && Session.PendingRewardId != null;
            _reward.style.display = hasReward && Manager.CanExpand ? DisplayStyle.Flex : DisplayStyle.None;
            _unitRewardA.style.display = _unitRewardB.style.display = hasReward ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasReward)
            {
                _unitRewardA.text = "Choose " + _flow.Rewards.GetCandidate(Session.CompletedRounds, 0).DisplayName + " + block";
                _unitRewardB.text = "Choose " + _flow.Rewards.GetCandidate(Session.CompletedRounds, 1).DisplayName + " + block";
            }
        }
        private string GetRequiredBlockName(string shapeId)
        {
            foreach (var block in Manager.Blocks) if (block.Footprint.Id == shapeId) return block.Footprint.DisplayName;
            return "Unknown block";
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
