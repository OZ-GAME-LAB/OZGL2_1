using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OZGL2.Augment;
using OZGL2.Stage;
using UnityEditor;
using UnityEngine;

namespace OZGL2.InGame.Editor
{
    public static class InGameAugmentVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/InGame/Verify Augment Selection")]
        public static async void Run()
        {
            Result = "Running";
            var data = new List<AugmentData>();
            try
            {
                for (int i = 0; i < 9; i++)
                {
                    var item = ScriptableObject.CreateInstance<AugmentData>();
                    item.augmentId = "verification_" + i; item.tier = i / 3 + 1;
                    item.effect = AugmentEffect.MonAttack; item.value = 0.1f;
                    data.Add(item);
                }
                var run = new AugmentRun(data);
                var mixed = run.Draw3(50, 50, 0, null);
                Check(mixed.Count == 3 && new HashSet<AugmentData>(mixed).Count == 3, "Unique mixed candidates");
                foreach (var item in mixed) Check(item.tier != 3, "Zero-weight tier excluded");
                foreach (var item in run.Draw3(0, 0, 100, null)) Check(item.tier == 3, "Platinum mapping");
                foreach (var item in run.Draw3(1)) Check(item.tier == 1, "Legacy API preserved");
                bool current = true;
                using (var selection = new InGameAugmentSelection(run, () => current))
                {
                    var request = Request(Guid.NewGuid().ToString("N"), 10);
                    var task = selection.SelectAsync(request, new AugmentTierWeights(100, 0, 0), CancellationToken.None);
                    Check(selection.IsPending && !task.IsCompleted, "Wait for player");
                    await ExpectFailure(selection.SelectAsync(request, new AugmentTierWeights(100, 0, 0), CancellationToken.None));
                    Check(!selection.TrySelect("stale", data[0]) && !selection.TrySelect(request.RequestId, data[8]), "Stale and unoffered clicks rejected");
                    Check(selection.TrySelect(request.RequestId, selection.Candidates[0]), "Apply candidate");
                    Check(!selection.TrySelect(request.RequestId, data[0]), "Double click rejected");
                    await task;
                    await selection.SelectAsync(request, new AugmentTierWeights(100, 0, 0), CancellationToken.None);
                    Check(run.PickedCount == 1 && Math.Abs(run.BuildModifiers().MonsterAttackMult - 1.1f) < 0.001f, "Apply once and modifiers updated");
                    using (var cancelled = new CancellationTokenSource())
                    {
                        var next = Request(request.RunId, 20);
                        task = selection.SelectAsync(next, new AugmentTierWeights(100, 0, 0), cancelled.Token);
                        cancelled.Cancel();
                        Check(!selection.IsPending && selection.Candidates.Count == 0, "Cancellation immediately hides candidates");
                        await ExpectCancelled(task);
                        task = selection.SelectAsync(next, new AugmentTierWeights(0, 100, 0), CancellationToken.None);
                        Check(selection.IsPending, "New request survives old cancellation");
                        current = false;
                        Check(!selection.TrySelect(next.RequestId, selection.Candidates[0]), "Old service ownership rejected");
                        await ExpectCancelled(task);
                    }
                }
                using (var empty = new InGameAugmentSelection(new AugmentRun(data), () => true, _ => false))
                    await ExpectFailure(empty.SelectAsync(Request(Guid.NewGuid().ToString("N"), 10), new AugmentTierWeights(1, 0, 0), CancellationToken.None));
                var failingRun = new AugmentRun(data);
                failingRun.Changed += () => throw new InvalidOperationException("Verification application failure");
                using (var failing = new InGameAugmentSelection(failingRun, () => true))
                {
                    var request = Request(Guid.NewGuid().ToString("N"), 10);
                    var task = failing.SelectAsync(request, new AugmentTierWeights(1, 0, 0), CancellationToken.None);
                    Check(!failing.TrySelect(request.RequestId, failing.Candidates[0]), "Effect failure does not report success");
                    await ExpectFailure(task);
                    Check(!failing.IsPending && failing.Error != null, "Failure closes selection");
                }
                run.ResetRun();
                Check(run.PickedCount == 0, "New-run reset");
                Result = "PASS: legacy/mixed tier draw, unique candidates, wait, duplicate/stale/unoffered inputs, one-time apply, modifiers, cancellation, stale ownership, empty pool, apply failure, reset";
            }
            catch (Exception exception) { Result = "FAIL: " + exception; }
            finally { foreach (var item in data) UnityEngine.Object.DestroyImmediate(item); }
        }
        private static RewardRequest Request(string runId, int round) => new RewardRequest(runId, round, eRewardKind.AUGMENT);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static async Task ExpectFailure(Task task)
        {
            try { await task; } catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("Expected failure");
        }
        private static async Task ExpectCancelled(Task task)
        {
            try { await task; } catch (OperationCanceledException) { return; }
            throw new InvalidOperationException("Expected cancellation");
        }
    }
}
