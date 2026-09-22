using System;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Stage;
using OZGL2.Stage.Prototype;
using OZGL2.Synergy;
using UnityEditor;
using UnityEngine;

namespace OZGL2.InGame.Editor
{
    public static class InGameAugmentPlayVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/InGame/Verify Augment Integration (Empty Play Scene)")]
        public static async void Run()
        {
            Result = "Running";
            GameObject root = null;
            InGameGridSession session = null;
            Task task = null;
            using (var token = new CancellationTokenSource(TimeSpan.FromSeconds(25)))
            {
                try
                {
                    Check(Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<InGamePrototypeBootstrap>() == null, "Use empty Play scene");
                    root = new GameObject("AugmentIntegrationVerification");
                    var sync = root.AddComponent<RealSynergySync>();
                    sync.BeginRun();
                    var provider = root.AddComponent<InGameAugmentRewards>();
                    root.AddComponent<InGameAugmentDummyView>();
                    provider.Bind(sync);
                    var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
                    session = new InGameGridSession(config.Catalog.CreateDefinition(), new DummyRewardLedger());
                    var prep = new StageGridPreparation(session, config.Catalog.CreateInitialUnit(), config.Catalog.CreateInitialBlock(), config.InitialAnchor);
                    var dummy = new ManualStageServices();
                    var rewards = new StageGridRewards(session, new GridPrototypeRewards(config.Catalog), provider);
                    var store = new MemoryStageProgressStore();
                    var stage = new StageManager(dummy, rewards, prep, session, store, new Lobby());
                    var spawns = new[] { new HeroSpawnDefinition("H_WAR_01", 1, 0f) };
                    var weights = new AugmentTierWeights(100, 0, 0);
                    var source = new SelectedStageSource(new StageDefinition("augment_verification", new[] {
                        new RoundDefinition("r1", spawns, true, "reward_1", weights),
                        new RoundDefinition("r2", spawns, true, "reward_2", weights) }));
                    task = stage.RunAsync(source, token.Token);
                    await Until(() => dummy.PendingRequest == eDummyRequest.BATTLE, token.Token);
                    dummy.CompleteBattle(dummy.RequestId, eBattleResult.VICTORY);
                    await Until(() => rewards.Pending != null, token.Token);
                    Check(rewards.TryChooseUnit(rewards.Pending.RequestId, 0), "General reward accepted");
                    await Until(() => provider.IsPending, token.Token);
                    Check(stage.State == eStageState.AUGMENT && provider.Candidates.Count > 0, "Real selection blocks progression");
                    foreach (var item in provider.Candidates) Check(InGameAugmentAvailability.CanOffer(item), "Only connected effects offered");
                    string id = provider.RequestId;
                    var selected = provider.Candidates[0];
                    Check(provider.TrySelect(id, selected) && !provider.TrySelect(id, selected), "One successful application");
                    await Until(() => session.Session.Grid.Phase == eGridPhase.PREPARATION, token.Token);
                    Check(sync.Augments.StackOf(selected) == 1 && !provider.IsPending, "Effect retained into preparation");
                    Check(session.Session.TryBeginBattle(session.Session.RunId, 2, true), "Next round available");
                    await Until(() => dummy.PendingRequest == eDummyRequest.BATTLE, token.Token);
                    dummy.CompleteBattle(dummy.RequestId, eBattleResult.VICTORY);
                    await Until(() => task.IsCompleted, token.Token);
                    await task;
                    Check(stage.State == eStageState.CLEARED && !provider.IsPending, "Terminal round does not offer augment");
                    sync.BeginRun(); provider.Bind(sync);
                    Check(sync.Augments.PickedCount == 0, "New run resets modifiers");
                    var request = new RewardRequest(Guid.NewGuid().ToString("N"), 10, eRewardKind.AUGMENT);
                    var waiting = provider.SelectAugmentAsync(request, weights, token.Token);
                    provider.enabled = false;
                    await ExpectCancelled(waiting);
                    Check(!provider.IsPending, "Disable cancels and hides");
                    provider.enabled = true; provider.Bind(sync);
                    waiting = provider.SelectAugmentAsync(request, weights, token.Token);
                    UnityEngine.Object.Destroy(root); root = null;
                    await ExpectCancelled(waiting);
                    Result = "PASS: real provider, connected candidates, general reward -> augment -> preparation -> next battle, one-time effect, terminal skip, run reset, disable/destroy cancellation";
                }
                catch (Exception exception) { Result = "FAIL: " + exception; }
                finally
                {
                    token.Cancel();
                    if (task != null) { try { await task; } catch { } }
                    session?.Dispose();
                    if (root != null) UnityEngine.Object.Destroy(root);
                }
            }
        }
        private static async Task Until(Func<bool> condition, CancellationToken token)
        { while (!condition()) { token.ThrowIfCancellationRequested(); await Task.Delay(16, token); } }
        private static async Task ExpectCancelled(Task task)
        { try { await task; } catch (OperationCanceledException) { return; } throw new InvalidOperationException("Expected cancellation"); }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class Lobby : IStageLobby
        {
            public Task ReturnAsync(StageRunResult result, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Check(result.IsSettled, "Settlement before lobby"); return Task.CompletedTask; }
        }
    }
}
