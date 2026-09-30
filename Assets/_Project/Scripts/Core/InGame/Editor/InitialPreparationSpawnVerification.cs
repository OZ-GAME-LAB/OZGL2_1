using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Stage;
using OZGL2.Stage.Prototype;
using UnityEditor;
using UnityEngine;

namespace OZGL2.InGame.Editor
{
    public static class InitialPreparationSpawnVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/InGame/Verify Initial Preparation and Spawn (Empty Play Scene)")]
        public static async void Run()
        {
            Result = "Running";
            GameObject root = null;
            var background = Application.runInBackground;
            try
            {
                Require(Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<InGamePrototypeBootstrap>() == null, "Use empty Play scene");
                Application.runInBackground = true;
                var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
                config.Validate();
                var points = config.CreateHeroSpawnPositions();
                Require(points.Length == 3 && points[0].x < points[1].x && points[1].x < points[2].x, "Distinct left/center/right");
                Require(points.All(p => p.y > config.GridWorldOrigin.y + (config.Catalog.CreateDefinition().MaximumSize.y - 0.5f) * config.CellWorldSize), "All spawns above maximum grid");
                using (var session = new InGameGridSession(config.Catalog.CreateDefinition(), new DummyRewardLedger()))
                using (var cancel = new CancellationTokenSource())
                {
                    await session.BeginAsync(new StageRunContext("initial_qa", "stage_qa"), cancel.Token);
                    // Stage request is constructed through the real StageManager in InGameVerification.
                    StageGridPreparation.PlaceInitial(session.Session, config.Catalog.CreateInitialUnit(), config.Catalog.CreateInitialBlock(), config.InitialAnchor);
                    Require(session.Session.Grid.Phase == eGridPhase.PREPARATION && session.Session.Deployment == null, "Initial preparation has no captured battle");
                    var grid = session.Session.Grid;
                    Require(grid.BeginUnitDrag(grid.Units[0].InstanceId) && grid.DropToTray(), "Return initial unit");
                    Require(!session.Session.TryBeginBattle(session.Session.RunId, 1), "Empty deployment blocks battle");
                    Require(grid.BeginUnitDrag(grid.Units[0].InstanceId), "Redeploy initial unit");
                    grid.MovePreview(config.InitialAnchor);
                    Require(grid.CommitPreview() && session.Session.TryBeginBattle(session.Session.RunId, 1), "Explicit start captures deployment");
                }
                root = new GameObject("SpawnDistributionVerification");
                var source = new GameObject("InactiveHero"); source.transform.SetParent(root.transform); source.SetActive(false);
                var prefab = source.AddComponent<PooledHero>();
                using (var pool = new HeroPool(new[] { new HeroPoolEntry("a", prefab, 0, 1, 1), new HeroPoolEntry("b", prefab, 0, 1, 1) }, root.transform))
                {
                    var battle = new PooledStageBattle(pool, new Defenders(), Vector3.zero, null, points);
                    // Constructor takes a snapshot; callers cannot alter an active battle's positions.
                    var expected = points.ToArray(); points[0] = Vector3.one * 999;
                    foreach (int count in new[] { 1, 2, 3, 8 })
                    using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        var entries = count == 1 ? new[] { new HeroSpawnDefinition("a", 1, 0) } :
                            new[] { new HeroSpawnDefinition("a", 1, 0), new HeroSpawnDefinition("b", count - 1, 0) };
                        var round = new RoundDefinition("spawn_qa", entries, false, "reward", new AugmentTierWeights(1, 0, 0));
                        var task = battle.RunRoundAsync(round, cancel.Token);
                        try
                        {
                            while (!battle.IsSpawningComplete) { cancel.Token.ThrowIfCancellationRequested(); await Task.Delay(10); }
                            var heroes = root.GetComponentsInChildren<PooledHero>().Where(h => h.IsLeased).OrderBy(h => h.LeaseId).ToArray();
                            Require(heroes.Length == count, "Spawn count");
                            for (int i = 0; i < count; i++) Require(heroes[i].transform.position == expected[i % 3], "Balanced sequence continues across hero kinds and resets each round");
                        }
                        finally { cancel.Cancel(); try { await task; } catch (OperationCanceledException) { } }
                        Require(pool.ActiveCount == 0 && battle.UnreturnedHeroCount == 0, "Cancellation returns all spawned units");
                    }
                }
                Result = "PASS: first preparation/manual start/empty deployment gate, upper three positions, 1/2/3/8 heroes across types, per-round reset, immutable positions, pool cleanup";
            }
            catch (Exception error) { Result = "FAIL: " + error; }
            finally { if (root != null) UnityEngine.Object.Destroy(root); Application.runInBackground = background; }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class Defenders : IStageDefenders
        {
            public int AliveCount => 1;
            public Task PrepareRoundAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        }
    }
}
