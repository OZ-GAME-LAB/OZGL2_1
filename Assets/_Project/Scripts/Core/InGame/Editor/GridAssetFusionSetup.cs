using System;
using System.Linq;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Grid.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.InGame.Editor
{
    public static class GridAssetFusionSetup
    {
        public const string THEME_PATH = "Assets/_Project/Data/Grid/Prototype/RFCastleTheme.asset";
        public const string SPRITES_PATH = "Assets/98.ExternalAssets/RF Castle/Sliced/mainlevbuild.png";
        [MenuItem("OZGL2/InGame/Apply Grid Art and Fusion")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/00.Scenes/Builds/InGame.unity" || scene.isDirty)
                throw new InvalidOperationException("Open the saved InGame scene in Edit mode first.");
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SPRITES_PATH).OfType<Sprite>().ToArray();
            var unclaimed = sprites.Single(s => s.name == "mainlevbuild_465");
            var available = sprites.Single(s => s.name == "mainlevbuild_459");
            var platform = sprites.Single(s => s.name == "mainlevbuild_925");
            var bootstrap = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InGamePrototypeBootstrap>(true)).Single();
            var theme = AssetDatabase.LoadAssetAtPath<GridBoardThemeSO>(THEME_PATH);
            if (theme == null) { theme = ScriptableObject.CreateInstance<GridBoardThemeSO>(); AssetDatabase.CreateAsset(theme, THEME_PATH); }
            var data = new SerializedObject(theme);
            data.FindProperty("_unclaimed").objectReferenceValue = unclaimed;
            data.FindProperty("_available").objectReferenceValue = available;
            data.FindProperty("_platform").objectReferenceValue = platform;
            data.FindProperty("_unclaimedTint").colorValue = new Color(2.5f, 2.4f, 2.2f, 1);
            data.FindProperty("_availableTint").colorValue = new Color(0.85f, 0.9f, 0.95f, 1);
            data.FindProperty("_platformTint").colorValue = new Color(0.9f, 0.75f, 1f, 1);
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var runner in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GridPrototypeRunner>(true)))
            {
                data = new SerializedObject(runner); data.FindProperty("_boardTheme").objectReferenceValue = theme;
                data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(runner);
            }
            var presentation = bootstrap.GetComponent<InGameGridPresentation>();
            if (presentation == null) presentation = bootstrap.gameObject.AddComponent<InGameGridPresentation>();
            data = new SerializedObject(presentation);
            data.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            data.FindProperty("_theme").objectReferenceValue = theme;
            data.ApplyModifiedPropertiesWithoutUndo();
            var single = AssetDatabase.LoadAssetAtPath<BlockShapeSO>("Assets/_Project/Data/Grid/Prototype/single.asset");
            foreach (var guid in AssetDatabase.FindAssets("t:GridUnitDataSO", new[] { "Assets/_Project/Data/Grid/Prototype", "Assets/03.ScriptableObjects/GridUnits" }))
            {
                var unit = AssetDatabase.LoadAssetAtPath<GridUnitDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                data = new SerializedObject(unit);
                if (data.FindProperty("_footprint").objectReferenceValue == single) continue;
                data.FindProperty("_footprint").objectReferenceValue = single;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
