using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Stage.Prototype;
using UnityEditor;
using UnityEngine;

namespace OZGL2.Stage.Editor
{
    public static class StageCleanupVerification
    {
        public static string LastResult { get; private set; } = "Not run";
        [MenuItem("OZGL2/Stage/Verify Cleanup Failures (Play)")]
        public static async void StartChecks()
        {
            LastResult = "Running";
            try
            {
                await VerifyAsync();
                await VerifyDeathFailures();
                LastResult = "PASS: failed reset discarded, all component hooks attempted, spawn failure discarded, cancel and callback failure reclaim all leases, pool/host disposal continues, token disposed, repeated shutdown safe.";
            }
            catch (Exception exception) { LastResult = "FAIL: " + exception; }
        }
        private static async Task VerifyAsync()
        {
            Require(Application.isPlaying, "Play mode required");
            var root = new GameObject("Cleanup verification pool");
            var template = new GameObject("Cleanup verification hero");
            template.SetActive(false);
            var prefab = template.AddComponent<PooledHero>();
            var templateProbe = template.AddComponent<CleanupFailureProbe>();
            template.AddComponent<PoolResetProbe>();
            HeroPool pool = null;
            Task<RoundResult> run = null;
            using (var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                try
                {
                    pool = CreatePool(prefab, root.transform);
                    Action<HeroLease> death = lease => { };
                    Action<HeroLease> ready = lease => pool.Return(lease);
                    var faulty = pool.Rent("test_hero", Vector3.zero, death, ready);
                    var probe = faulty.Hero.GetComponent<CleanupFailureProbe>();
                    probe.CanThrowOnReturn = true;
                    var laterHook = faulty.Hero.GetComponent<PoolResetProbe>();
                    try { pool.Return(faulty); throw new Exception("Expected reset failure missing"); }
                    catch (AggregateException) { }
                    Require(probe.ReturnAttempts == 1 && laterHook.ReturnCount == 1, "Remaining reset hooks must run");
                    Require(pool.ActiveCount == 0 && pool.CreatedCount == 1 && !faulty.Hero.gameObject.activeSelf, "Faulty instance removed and disabled");
                    var healthy = pool.Rent("test_hero", Vector3.zero, death, ready);
                    Require(healthy.Hero != faulty.Hero, "Failed instance cannot be rented again");
                    pool.Return(healthy);
                    pool.Dispose();

                    templateProbe.CanThrowOnSpawn = true;
                    pool = CreatePool(prefab, root.transform);
                    try { pool.Rent("test_hero", Vector3.zero, death, ready); throw new Exception("Expected spawn failure missing"); }
                    catch (AggregateException) { }
                    Require(pool.ActiveCount == 0 && pool.CreatedCount == 1, "Spawn-failed actor removed even if return succeeds");
                    pool.Dispose();
                    templateProbe.CanThrowOnSpawn = false;

                    pool = CreatePool(prefab, root.transform);
                    var battle = new PooledStageBattle(pool, new DummyDefenders(1), Vector3.zero);
                    var round = new RoundDefinition("round_test", new[] { new HeroSpawnDefinition("test_hero", 2, 0) }, false,
                        "reward_test", new AugmentTierWeights(1, 0, 0));
                    run = battle.RunRoundAsync(round, lifetime.Token);
                    var active = root.GetComponentsInChildren<PooledHero>().Where(hero => hero.IsLeased).ToArray();
                    Require(active.Length == 2, "Two active leases");
                    active[0].GetComponent<CleanupFailureProbe>().CanThrowOnReturn = true;
                    lifetime.Cancel();
                    try { await run; throw new Exception("Expected cleanup failure missing"); }
                    catch (AggregateException exception)
                    {
                        Require(exception.Flatten().InnerExceptions.Any(error => error is OperationCanceledException), "Preserve original cancellation");
                    }
                    Require(pool.ActiveCount == 0 && battle.UnreturnedHeroCount == 0 && battle.AliveHeroCount == 0,
                        "A failed return must not prevent other returns");
                    Require(active[1].GetComponent<PoolResetProbe>().ReturnCount == 1 && battle.CleanupError != null, "Later hero cleaned and error reported");
                    pool.Dispose();

                    pool = CreatePool(prefab, root.transform);
                    battle = new PooledStageBattle(pool, new DummyDefenders(1), Vector3.zero);
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        run = battle.RunRoundAsync(round, timeout.Token);
                        active = root.GetComponentsInChildren<PooledHero>().Where(hero => hero.IsLeased).ToArray();
                        var dying = active[0];
                        dying.GetComponent<CleanupFailureProbe>().CanThrowOnReturn = true;
                        Require(dying.TryReportDeath(dying.LeaseId), "Death callback collected return failure");
                        try { await run; throw new Exception("Callback cleanup failure not observed"); }
                        catch (AggregateException) { }
                        Require(pool.ActiveCount == 0 && battle.CleanupError != null && battle.EarnedExperience == 7, "Callback failure ends battle and cleans survivors");
                    }
                    pool.Dispose();

                    pool = CreatePool(prefab, root.transform);
                    var one = pool.Rent("test_hero", Vector3.zero, death, ready);
                    var two = pool.Rent("test_hero", Vector3.zero, death, ready);
                    one.Hero.GetComponent<CleanupFailureProbe>().CanThrowOnReturn = true;
                    var lastProbe = two.Hero.GetComponent<PoolResetProbe>();
                    try { pool.Dispose(); throw new Exception("Expected disposal failure missing"); }
                    catch (AggregateException) { }
                    Require(lastProbe.ReturnCount == 1 && pool.ActiveCount == 0 && pool.CreatedCount == 0, "Pool disposal processes every actor");
                    pool.Dispose();
                }
                finally
                {
                    lifetime.Cancel();
                    try { if (run != null) await run; } catch (Exception) { }
                    try { pool?.Dispose(); }
                    finally { UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(template); }
                }
            }

            var host = new GameObject("Cleanup verification host").AddComponent<StageRunHost>();
            var services = new ManualStageServices();
            var manager = new StageManager(services, services, services, services, new MemoryStageProgressStore(), services);
            var broken = new DisposableProbe(true);
            var good = new DisposableProbe(false);
            host.OwnResource(broken); host.OwnResource(good);
            var data = AssetDatabase.LoadAssetAtPath<StageDataSO>("Assets/03.ScriptableObjects/Stage/Dummy/DummyStageNormal.asset");
            host.StartRun(manager, data);
            var field = typeof(StageRunHost).GetField("_lifetime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tokenSource = (CancellationTokenSource)field.GetValue(host);
            try
            {
                var shutdown = host.ShutdownAsync();
                Require(ReferenceEquals(shutdown, host.ShutdownAsync()), "Same shutdown task");
                try { await shutdown; throw new Exception("Expected host cleanup failure missing"); }
                catch (AggregateException) { }
                Require(host.IsShutdownComplete && host.CleanupError != null && broken.Count == 1 && good.Count == 1,
                    "Host must process all resources and retain error");
                Require(manager.State == eStageState.CANCELLED && !manager.IsRunning, "Run stopped before disposal");
                bool isDisposed = false;
                try { var token = tokenSource.Token; }
                catch (ObjectDisposedException) { isDisposed = true; }
                Require(isDisposed && field.GetValue(host) == null, "Cancellation source disposed and released");
                try { await host.ShutdownAsync(); } catch (AggregateException) { }
                Require(broken.Count == 1 && good.Count == 1, "Repeated shutdown must not redispose");
            }
            finally { UnityEngine.Object.Destroy(host.gameObject); }
        }
        private static async Task VerifyDeathFailures()
        {
            var root = new GameObject("Death failure verification");
            var template = new GameObject("Death failure template"); template.SetActive(false);
            var prefab = template.AddComponent<PooledHero>(); template.AddComponent<CleanupFailureProbe>();
            HeroPool pool = null;
            try
            {
                for (int mode = 0; mode < 2; mode++)
                {
                    pool = CreatePool(prefab, root.transform);
                    var battle = new PooledStageBattle(pool, new DummyDefenders(1), Vector3.zero);
                    var round = new RoundDefinition("round_test", new[] { new HeroSpawnDefinition("test_hero", 2, 0) }, false,
                        "reward_test", new AugmentTierWeights(1, 0, 0));
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        var run = battle.RunRoundAsync(round, timeout.Token);
                        var hero = root.GetComponentsInChildren<PooledHero>().First(item => item.IsLeased);
                        var probe = hero.GetComponent<CleanupFailureProbe>();
                        probe.CanThrowOnDeath = true; probe.CanThrowOnReturn = mode == 1;
                        long id = hero.LeaseId;
                        Require(hero.TryReportDeath(id), "First death accepted despite failed presentation");
                        Require(!hero.TryReportDeath(id) && !hero.TryCompleteDeath(id), "Duplicate callbacks blocked");
                        Require(pool.ActiveCount == 1, "Failed presentation immediately attempts return");
                        try { await run; throw new Exception("Presentation failure incorrectly completed battle"); }
                        catch (AggregateException exception)
                        {
                            var messages = exception.Flatten().InnerExceptions.Select(error => error.Message).ToArray();
                            Require(messages.Any(message => message.Contains("death presentation")), "Original presentation error retained");
                            if (mode == 1) Require(messages.Any(message => message.Contains("return reset")), "Return failure retained alongside presentation error");
                        }
                        Require(pool.ActiveCount == 0 && battle.UnreturnedHeroCount == 0 && battle.AliveHeroCount == 0,
                            "Battle failure cleans all remaining leases");
                    }
                    pool.Dispose();
                }
                pool = CreatePool(prefab, root.transform);
                HeroLease replacement = default; Exception received = null;
                var first = pool.Rent("test_hero", Vector3.zero, lease => { }, lease =>
                {
                    pool.Return(lease);
                    replacement = pool.Rent("test_hero", Vector3.zero, item => { }, item => pool.Return(item));
                }, (lease, error) => received = error);
                first.Hero.GetComponent<CleanupFailureProbe>().CanThrowAfterDeathReturn = true;
                first.Hero.TryReportDeath(first.LeaseId);
                Require(received != null && replacement.Hero == first.Hero && replacement.LeaseId != first.LeaseId &&
                    replacement.Hero.IsLeased && pool.ActiveCount == 1, "Late failure cannot return new lease and reaches original fault handler");
                Require(!first.Hero.TryCompleteDeath(first.LeaseId), "Old completion rejected after reuse");
                pool.Return(replacement);
            }
            finally { pool?.Dispose(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(template); }
        }
        private static HeroPool CreatePool(PooledHero prefab, Transform root)
            => new HeroPool(new[] { new HeroPoolEntry("test_hero", prefab, 2, 1, 7) }, root);
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private sealed class DisposableProbe : IDisposable
        {
            private readonly bool _canThrow;
            internal int Count;
            internal DisposableProbe(bool canThrow) { _canThrow = canThrow; }
            public void Dispose() { Count++; if (_canThrow) throw new InvalidOperationException("Injected resource disposal failure"); }
        }
    }
}
