using System;
using OZGL2.Stage;
using UnityEditor;
using UnityEngine;

namespace OZGL2.InGame.Editor
{
    public static class StageSelectionVerification
    {
        public static string Result { get; private set; } = "Not run";
        [MenuItem("OZGL2/InGame/Verify Stage Selection Contracts")]
        public static void Run()
        {
            StageCatalogSO copy = null;
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
                var catalog = config.StageCatalog;
                var definitions = catalog.CreateDefinitions();
                Check(definitions.Count == 3 && definitions[0].Rounds.Count == 30 && definitions[1].Rounds.Count == 50 &&
                    definitions[2].Rounds.Count == 100, "Team stage round counts");
                foreach (var stage in definitions)
                {
                    config.Validate(stage);
                    Check(catalog.Resolve(stage.StageId).StageId == stage.StageId, "Exact ID mapping");
                }
                Check(catalog.GetDisplayName(definitions[0].StageId) == "보통" &&
                    catalog.GetDisplayName(definitions[1].StageId) == "어려움" &&
                    catalog.GetDisplayName(definitions[2].StageId) == "헬", "Difficulty display names");
                Check(catalog.GetDisplayName("unknown_stage") == "unknown_stage", "Unconfigured display name fallback");
                Reject(() => catalog.Resolve("unknown_stage"), "Unknown ID");
                Reject(() => catalog.Resolve(""), "Missing ID");
                var missingHero = new StageDefinition("verification_missing_hero", new[]
                {
                    new RoundDefinition("verification_round", new[] { new HeroSpawnDefinition("missing_hero", 1, 0) },
                        false, "verification_reward", new AugmentTierWeights(1, 0, 0))
                });
                Reject(() => config.Validate(missingHero), "Selected stage missing hero pool entry");
                copy = UnityEngine.Object.Instantiate(catalog);
                var serialized = new SerializedObject(copy);
                var stages = serialized.FindProperty("_stages");
                stages.GetArrayElementAtIndex(1).objectReferenceValue = stages.GetArrayElementAtIndex(0).objectReferenceValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Reject(() => copy.CreateDefinitions(), "Duplicate ID");
                stages.GetArrayElementAtIndex(1).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Reject(() => copy.CreateDefinitions(), "Missing SO");

                var session = new StageLaunchSession();
                Check(!session.HasLaunchHistory, "Fresh direct Editor entry");
                var first = new StageLaunchRequest(definitions[0].StageId, InGamePrototypeSetup.SCENE_PATH);
                var second = new StageLaunchRequest(definitions[1].StageId, InGamePrototypeSetup.SCENE_PATH);
                Check(session.TryQueue(first) && !session.TryQueue(second), "Reject duplicate launch");
                Check(!session.Cancel(second.RequestId), "Old/unrelated cancellation cannot clear request");
                Check(session.Consume(InGamePrototypeSetup.SCENE_PATH) == first, "Consume matching request once");
                Check(session.Consume(InGamePrototypeSetup.SCENE_PATH) == null && session.HasLaunchHistory, "No repeated consume or silent fallback");
                Check(session.TryQueue(second), "New selection after previous request");
                Check(!session.Cancel(first.RequestId) && session.Pending == second, "Late cleanup does not erase new selection");
                session.ObserveScene("Assets/Other.unity");
                Check(session.Pending == null && session.HasLaunchHistory, "Wrong scene clears stale request without enabling fallback");
                session.TryQueue(first);
                Reject(() => session.Consume("Assets/Other.unity"), "Wrong destination rejected");
                Check(session.Pending == null, "Wrong request consumed safely");
                session.TryQueue(second); session.EnterSelection();
                Check(session.Pending == null, "Selection reset");
                var source = new SelectedStageSource(definitions[0]);
                Check(ReferenceEquals(source.CreateSnapshot(), definitions[0]), "Selected definition stays immutable across retry");
                var hellRequest = new StageLaunchRequest(definitions[2].StageId, InGamePrototypeSetup.SCENE_PATH);
                Check(session.TryQueue(hellRequest) && session.Consume(InGamePrototypeSetup.SCENE_PATH) == hellRequest, "Hell launch handoff");
                var hellSource = new SelectedStageSource(definitions[2]);
                Check(ReferenceEquals(hellSource.CreateSnapshot(), definitions[2]), "Hell definition retained for retry");
                Result = "PASS: 30/50/100-round mappings, pool compatibility, missing/unknown/duplicate data, duplicate launch, one-time consume, stale cancellation, wrong scene, selection reset, immutable source, hell handoff/retry source";
            }
            catch (Exception exception) { Result = "FAIL: " + exception; }
            finally { if (copy != null) UnityEngine.Object.DestroyImmediate(copy); }
        }
        private static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, message);
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
