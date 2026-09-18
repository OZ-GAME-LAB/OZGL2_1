using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Grid.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OZGL2.InGame.Editor
{
    /// <summary>빈 임시 씬의 Play 모드에서 실행. 용사 처치나 계정 저장 없이 실제 배치 어댑터를 검증한다.</summary>
    public static class GridFusionPlayVerification
    {
        public static string Result { get; private set; } = "Not run";
        private static GridRunSession _session;
        private static RealDefenders _defenders;
        private static GridWorldBoardView _world;
        private static GameObject _root;
        private static bool _hasBackgroundOverride;
        private static bool _previousBackground;
        public static GridPrototypeRunner Runner { get; private set; }
        [MenuItem("OZGL2/InGame/Verify Grid Fusion (Empty Play Scene)")]
        public static async void Run()
        {
            Result = "Running";
            try { await Verify(); Result = "PASS: real UI board/tray/cross fusion, invalid drop, star persistence, world tiles, deployment removal/move, original SO preserved."; }
            catch (Exception exception) { Result = "FAIL: " + exception; }
        }
        private static async Task Verify()
        {
            Check(Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<InGamePrototypeBootstrap>() == null, "Use empty Play scene");
            Cleanup();
            _previousBackground = Application.runInBackground;
            _hasBackgroundOverride = true;
            Application.runInBackground = true;
            var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
            var theme = AssetDatabase.LoadAssetAtPath<GridBoardThemeSO>(GridConnectivitySetup.THEME_PATH);
            _root = new GameObject("GridFusionVerification");
            _session = new GridRunSession("fusion_play", config.Catalog.CreateDefinition(), GridFusionPolicy.CanFuse);
            var grid = _session.Grid;
            var basic = config.Catalog.CreateInitialUnit();
            var shape = config.Catalog.CreateInitialBlock();
            var origin = grid.Definition.InitialOrigin;
            grid.AddBlock("platform", "platform", new FootprintDefinition("wide", "Wide", new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one }));
            for (int i = 0; i < 4; i++) grid.AddUnit("unit" + i, basic);
            _session.TryAllowPreparation(_session.RunId, 1, false);
            grid.BeginBlockDrag("platform"); grid.MovePreview(origin); Check(grid.CommitPreview(), "Initial block");
            GridFusionVerification.Place(grid, "unit0", origin);
            GridFusionVerification.Place(grid, "unit1", origin + Vector2Int.right);
            var mapping = new GridWorldMapping(config.GridWorldOrigin, Vector3.right * config.CellWorldSize, Vector3.up * config.CellWorldSize);
            _world = new GridWorldBoardView(grid, theme, mapping, config.CellWorldSize, _root.transform);
            _defenders = new RealDefenders(() => _session, config.DemonArmyCatalog, _root.transform, config.GridWorldOrigin, config.CellWorldSize);
            Check(_session.TryBeginBattle(_session.RunId, 1), "First deployment");
            await _defenders.PrepareRoundAsync(CancellationToken.None);
            Check(_defenders.AliveCount == 2, "Two real defenders");
            NextPreparation(basic, shape, "reward1");
            using var preparationView = new GridWorldPreparationView(grid, mapping, config.CellWorldSize,
                (id, star) => config.DemonArmyCatalog.FindPrefab(id, star).gameObject, _root.transform);
            preparationView.SetVisible(true);
            var ui = new GameObject("FusionPreparationUI"); ui.transform.SetParent(_root.transform);
            ui.SetActive(false);
            ui.AddComponent<UIDocument>().panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/Data/Grid/Prototype/GridPanelSettings.asset");
            Runner = ui.AddComponent<GridPrototypeRunner>();
            var data = new SerializedObject(Runner); data.FindProperty("_boardTheme").objectReferenceValue = theme; data.ApplyModifiedPropertiesWithoutUndo();
            Runner.Bind(_session); ui.SetActive(true);
            await Frame(); await Frame();
            var panel = Runner.GetComponent<UIDocument>().rootVisualElement;
            Down(Runner.Board.Element, Runner.Board.CellToPanel(origin)); await Frame();
            Move(panel, Runner.Board.CellToPanel(origin + Vector2Int.right)); await Frame();
            Check(grid.CanFusePreview, "Board fusion preview");
            Check(Runner.Board.GhostLayer.Q("ghost-cell").resolvedStyle.backgroundColor == GridBoardView.VALID_COLOR, "Green fusion ghost");
            Up(panel, Runner.Board.CellToPanel(origin + Vector2Int.right)); await Frame();
            Check(grid.FindUnit("unit0") == null && grid.FindUnit("unit1").StarLevel == 2, "Pointer board fusion");
            CheckPreviewModel(config.DemonArmyCatalog.FindPrefab(basic.Id, 2), "unit1");
            var sourceCard = panel.Q<VisualElement>("card-unit2"); Down(sourceCard, sourceCard.worldBound.center); await Frame();
            var targetPoint = panel.Q<VisualElement>("card-unit3").worldBound.center;
            Move(panel, targetPoint); await Frame();
            targetPoint = panel.Q<VisualElement>("card-unit3").worldBound.center;
            Up(panel, targetPoint); await Frame();
            Check(grid.FindUnit("unit2") == null && grid.FindUnit("unit3").StarLevel == 2, "Pointer tray fusion");
            sourceCard = panel.Q<VisualElement>("card-unit3"); Down(sourceCard, sourceCard.worldBound.center); await Frame();
            Up(panel, Runner.Board.CellToPanel(origin + Vector2Int.right)); await Frame();
            Check(grid.FindUnit("unit1").StarLevel == 3 && grid.FindUnit("unit3") == null, "Pointer tray-to-board fusion");
            Check(_root.GetComponentsInChildren<TextMesh>().Single().text == "★3", "World star label updates after fusion and removes consumed labels");
            CheckPreviewModel(config.DemonArmyCatalog.FindPrefab(basic.Id, 3), "unit1");
            Down(Runner.Board.Element, Runner.Board.CellToPanel(origin + Vector2Int.right)); await Frame();
            Move(panel, Runner.Board.CellToPanel(new Vector2Int(-1, 0))); await Frame();
            Check(grid.GetPreviewFailure() != ePlacementFailure.NONE, "Outside is invalid");
            Up(panel, Runner.Board.CellToPanel(new Vector2Int(-1, 0))); await Frame();
            Check(grid.FindUnit("unit1").Anchor == origin + Vector2Int.right, "Invalid drop retains placement");
            GridFusionVerification.Place(grid, "unit1", origin + Vector2Int.up);
            var asset = config.DemonArmyCatalog.FindPrefab(basic.Id).statData;
            int originalStar = asset.starLevel;
            int originalHp = asset.maxHealth;
            Check(_session.TryBeginBattle(_session.RunId, 2), "Second deployment");
            await _defenders.PrepareRoundAsync(CancellationToken.None); await Frame();
            var actual = _root.GetComponentsInChildren<UnitBase>().Single();
            Check(_defenders.AliveCount == 1 && actual.statData.starLevel == 3, "Consumed defender removed; star applied");
            Check(actual.name == config.DemonArmyCatalog.FindPrefab(basic.Id, 3).name + "(Clone)", "Three-star combat prefab selected");
            Check(Vector3.Distance(actual.transform.position, mapping.GetWorldPosition(origin + Vector2Int.up)) < 0.001f, "Existing defender moved");
            Check(asset.starLevel == originalStar && asset.maxHealth == originalHp && actual.statData != asset, "Shared SO untouched");
            Check(_root.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.StartsWith("Cell_")) == 40, "Forty real asset cells");
            int retainedHealth = actual.currentHealth = Math.Max(1, actual.currentHealth - 1);
            await _defenders.PrepareRoundAsync(CancellationToken.None);
            Check(_root.GetComponentsInChildren<UnitBase>().Single() == actual && actual.currentHealth == retainedHealth, "Unchanged model retains instance and health");
            NextPreparation(basic, shape, "reward2");
            // 검증 종료 후 준비 화면을 남겨 바닥 상태와 성급 표시를 육안 확인한다.
            await Frame();
        }
        private static void CheckPreviewModel(UnitBase prefab, string instanceId)
        {
            var actor = _root.GetComponentsInChildren<Transform>().Single(t => t.name == "Preview_" + instanceId);
            var expected = prefab.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r.enabled && r.sprite != null && r.GetComponentsInParent<Transform>(true)
                    .TakeWhile(t => t != prefab.transform).All(t => t.gameObject.activeSelf))
                .Select(r => r.sprite).ToArray();
            var actual = actor.GetComponentsInChildren<SpriteRenderer>().Select(r => r.sprite).ToArray();
            Check(expected.Length > 0 && expected.SequenceEqual(actual), "Preparation uses selected star model sprites");
            Check(actor.GetComponentsInChildren<UnitBase>().Length == 0, "Preview has no combat components");
        }
        private static void NextPreparation(UnitDefinition unit, FootprintDefinition block, string reward)
        {
            int round = _session.NextRound;
            Check(_session.TryFinishBattle(_session.RunId, round, reward), "Finish battle");
            Check(_session.TryChooseUnit(_session.RunId, reward, unit, block), "Grant reward");
            Check(_session.TryAllowPreparation(_session.RunId, round + 1, true), "Next preparation");
        }
        public static void Cleanup()
        {
            if (Runner != null) Runner.Unbind();
            _world?.Dispose(); _world = null;
            _defenders?.Dispose(); _defenders = null;
            _session?.Dispose(); _session = null;
            if (_root != null) UnityEngine.Object.Destroy(_root);
            Runner = null;
            if (_hasBackgroundOverride) Application.runInBackground = _previousBackground;
            _hasBackgroundOverride = false;
        }
        private static Task Frame() => Task.Delay(70);
        private static void Check(bool condition, string message) => GridFusionVerification.Require(condition, message);
        private static void Down(VisualElement target, Vector2 point)
        {
            using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = point, button = 0 }))
            { evt.target = target; target.SendEvent(evt); }
        }
        private static void Move(VisualElement target, Vector2 point)
        {
            using (var evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, mousePosition = point, button = 0 }))
            { evt.target = target; target.SendEvent(evt); }
        }
        private static void Up(VisualElement target, Vector2 point)
        {
            using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 }))
            { evt.target = target; target.SendEvent(evt); }
        }
    }
}
