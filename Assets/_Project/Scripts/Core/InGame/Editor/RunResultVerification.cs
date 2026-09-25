using System;
using OZGL2.Stage;
using UnityEditor;

namespace OZGL2.InGame.Editor
{
    public static class RunResultVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/InGame/Verify Run Result")]
        public static void Run()
        {
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
                foreach (bool victory in new[] { false, true })
                {
                    var definition = config.Stage.CreateSnapshot();
                    var progress = new StageRunProgress(definition);
                    int count = victory ? definition.Rounds.Count : 1;
                    for (int i = 1; i <= count; i++)
                    {
                        progress.BeginRound(i);
                        progress.RecordRound(i, new RoundResult(victory ? eBattleResult.VICTORY : eBattleResult.DEFEAT, 0));
                    }
                    bool rejected = false;
                    try { new InGameRunResult(progress.CreateSnapshot(), definition.Rounds.Count, 3, 20); }
                    catch (ArgumentException) { rejected = true; }
                    Check(rejected, "Unsettled result rejected");
                    progress.MarkSettled();
                    var result = new InGameRunResult(progress.CreateSnapshot(), definition.Rounds.Count, 3, 20);
                    var selection = new InGameResultSelection();
                    selection.Present(result);
                    Check(result.Progress.IsCleared == victory && result.Level == 3 && result.CurrentLevelXp == 20, "Outcome and display snapshot");
                    Check(!selection.TryBegin("stale") && selection.TryBegin(result.RunId), "Request ownership");
                    Check(!selection.TryBegin(result.RunId), "Duplicate action rejected");
                    selection.Restore("stale");
                    Check(selection.IsBusy, "Stale failure cannot unlock action");
                    selection.Restore(result.RunId);
                    Check(selection.TryBegin(result.RunId), "Navigation failure can retry");
                    selection.Clear();
                    Check(!selection.TryBegin(result.RunId), "Consumed result rejected");
                }
                Result = "PASS: victory/defeat snapshots, unsettled rejection, stale/duplicate actions, failed navigation recovery, consumed result";
            }
            catch (Exception error) { Result = "FAIL: " + error; }
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
