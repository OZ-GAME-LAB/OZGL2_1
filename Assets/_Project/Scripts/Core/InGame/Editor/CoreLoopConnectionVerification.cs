using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Stage;
using OZGL2.Synergy;
using UnityEditor;
using UnityEngine;

namespace OZGL2.InGame.Editor
{
    public static class CoreLoopConnectionVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/InGame/Verify Core Connections (Empty Play Scene)")]
        public static async void Run()
        {
            Result = "Running";
            try
            {
                Check(Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<InGamePrototypeBootstrap>() == null, "Use empty Play scene");
                await VerifyConnections();
                await VerifyBattle();
                VerifySynergy();
                Result = "PASS: prepare/enable ordering, duplicate cleanup, cleanup failure isolation, partial start, cancellation, cleanup before pool return, simultaneous defeat, visual-independent fusion counts and unsubscribe.";
            }
            catch (Exception error) { Result = "FAIL: " + error; }
        }

        private static async Task VerifyConnections()
        {
            var first = new Probe(); var second = new Probe();
            var context = new InGameCombatContext("run_one", 1, Vector3.one);
            var connection = new InGameCombatConnection(new[] { first, second, first }, () => context);
            Check(connection.ParticipantCount == 2, "Deduplicate participants");
            first.OnPrepare = () => Check(!second.IsEnabled, "No early enable");
            await connection.BeginRoundAsync(CancellationToken.None);
            Check(first.IsEnabled && second.IsEnabled && first.Context == context, "Prepared context then enable");
            first.IsCleanupFailure = true;
            try { await connection.EndRoundAsync(); throw new Exception("Expected cleanup failure"); }
            catch (AggregateException) { }
            try { await connection.EndRoundAsync(); } catch (AggregateException) { }
            Check(first.CleanupCount == 1 && second.CleanupCount == 1 && !second.IsEnabled, "All cleaned exactly once despite failure");
            first.IsCleanupFailure = false;
            first.IsPrepareFailure = true;
            try { await connection.BeginRoundAsync(CancellationToken.None); throw new Exception("Expected prepare failure"); }
            catch (InvalidOperationException) { }
            await connection.EndRoundAsync();
            Check(!first.IsEnabled && !second.IsEnabled && second.CleanupCount == 2, "Partial start cleanup");
            first.IsPrepareFailure = false;
            context = new InGameCombatContext("run_two", 2, Vector3.right);
            await connection.BeginRoundAsync(CancellationToken.None);
            Check(first.Context == context, "Fresh context each battle");
            await connection.EndRoundAsync();
        }

        private static async Task VerifyBattle()
        {
            var root = new GameObject("CoreConnectionVerification");
            var template = new GameObject("HeroTemplate"); template.SetActive(false);
            var prefab = template.AddComponent<PooledHero>();
            try
            {
                using (var pool = new HeroPool(new[] { new HeroPoolEntry("test_hero", prefab, 1, 1, 0) }, root.transform))
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var defenders = new Defenders();
                    var participant = new Probe();
                    participant.OnPrepare = () => Check(defenders.IsPrepared && pool.ActiveCount == 0, "Prepare after deployment before spawn");
                    var connection = new InGameCombatConnection(new[] { participant }, () => new InGameCombatContext("test_run", 1, Vector3.zero));
                    var battle = new PooledStageBattle(pool, defenders, Vector3.zero, connection);
                    var round = new RoundDefinition("round_test", new[] { new HeroSpawnDefinition("test_hero", 1, 0) }, false, "reward_test", new AugmentTierWeights(1, 0, 0));
                    var run = battle.RunRoundAsync(round, timeout.Token);
                    Check(battle.DefeatOneHero(), "Hero dies"); defenders.AliveCount = 0;
                    var result = await run;
                    Check(result.Outcome == eBattleResult.DEFEAT && battle.Outcome == eRoundOutcome.SIMULTANEOUS, "Simultaneous defeat instead of exception");
                    participant.OnCleanup = () => Check(pool.ActiveCount == 1 && !participant.IsEnabled, "Disable and cleanup before pool return");
                    using (var cancel = new CancellationTokenSource())
                    {
                        run = battle.RunRoundAsync(round, cancel.Token);
                        cancel.Cancel();
                        try { await run; throw new Exception("Expected cancellation"); }
                        catch (OperationCanceledException) { }
                    }
                    Check(pool.ActiveCount == 0 && participant.CleanupCount == 2, "Cancelled battle returns all heroes");
                    participant.IsCleanupFailure = true;
                    run = battle.RunRoundAsync(round, timeout.Token);
                    defenders.AliveCount = 0;
                    try { await run; throw new Exception("Expected cleanup failure"); }
                    catch (AggregateException) { }
                    Check(pool.ActiveCount == 0 && battle.UnreturnedHeroCount == 0, "Cleanup failure still returns pool");
                }
            }
            finally { UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(template); }
        }

        private static void VerifySynergy()
        {
            var target = UnityEngine.Object.FindFirstObjectByType<RealSynergySync>();
            Check(target != null, "Real synergy service exists");
            var saved = Enum.GetValues(typeof(SynergyJob)).Cast<SynergyJob>().ToDictionary(job => job, job => target.Synergy.CountOf(job));
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
                using (var session = new GridRunSession("connection_check", config.Catalog.CreateDefinition(), GridFusionPolicy.CanFuse))
                using (var connection = new InGameSynergyConnection(config.DemonArmyCatalog, target))
                {
                    var grid = session.Grid;
                    var unit = config.Catalog.CreateInitialUnit();
                    var job = config.DemonArmyCatalog.FindPrefab(unit.Id).statData.job;
                    grid.AddBlock("floor", "floor", new FootprintDefinition("line", "Line", new[] { Vector2Int.zero, Vector2Int.right }));
                    grid.AddUnit("a", unit); grid.AddUnit("b", unit);
                    session.TryAllowPreparation(session.RunId, 1, false);
                    connection.Bind(session);
                    var origin = grid.Definition.InitialOrigin;
                    grid.BeginBlockDrag("floor"); grid.MovePreview(origin); Check(grid.CommitPreview(), "Floor");
                    GridFusionVerification.Place(grid, "a", origin);
                    GridFusionVerification.Place(grid, "b", origin + Vector2Int.right);
                    Check(target.Synergy.CountOf(job) == 2, "Two deployed units without presentation");
                    Check(grid.TryFuseUnits("a", "b") && target.Synergy.CountOf(job) == 1, "Fusion updates count");
                    connection.Dispose();
                    Check(target.Synergy.CountOf(job) == 0, "Clear on disconnect");
                    grid.AddUnit("c", unit); GridFusionVerification.Place(grid, "c", origin);
                    Check(target.Synergy.CountOf(job) == 0, "No subscription after disconnect");
                }
            }
            finally { foreach (var pair in saved) target.SetCount(pair.Key, pair.Value); }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private sealed class Defenders : IStageDefenders
        {
            public int AliveCount { get; set; }
            public bool IsPrepared { get; private set; }
            public Task PrepareRoundAsync(CancellationToken token)
            { token.ThrowIfCancellationRequested(); IsPrepared = true; AliveCount = 1; return Task.CompletedTask; }
        }
        private sealed class Probe : IInGameCombatParticipant
        {
            public bool IsEnabled, IsCleanupFailure, IsPrepareFailure;
            public int CleanupCount;
            public InGameCombatContext Context;
            public Action OnPrepare, OnCleanup;
            public void SetCombatEnabled(bool value) => IsEnabled = value;
            public Task PrepareAsync(InGameCombatContext context, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Context = context; OnPrepare?.Invoke(); if (IsPrepareFailure) throw new InvalidOperationException("Injected prepare failure"); return Task.CompletedTask; }
            public Task CleanupAsync(InGameCombatContext context)
            { CleanupCount++; OnCleanup?.Invoke(); if (IsCleanupFailure) throw new InvalidOperationException("Injected cleanup failure"); return Task.CompletedTask; }
        }
    }
}
