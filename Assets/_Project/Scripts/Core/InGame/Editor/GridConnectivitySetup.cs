using System;
using System.Linq;
using OZGL2.Grid.Prototype;
using OZGL2.Grid.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.InGame.Editor
{
    public static class GridConnectivitySetup
    {
        public const string THEME_PATH = "Assets/_Project/Data/Grid/Prototype/SPUMConnectedTheme.asset";
        public const string SPRITES_PATH = "Assets/98.ExternalAssets/00.LocalStaging/00.Packages/SPUM/Ultimate Resource Bundle/Res/Maps/BG/Tile01/TP_Tile01.png";
        [MenuItem("OZGL2/InGame/Apply Connected SPUM Grid")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/00.Scenes/Builds/InGame.unity" || scene.isDirty)
                throw new InvalidOperationException("Open the saved InGame scene in Edit mode first.");
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SPRITES_PATH).OfType<Sprite>().ToArray();
            var unclaimed = sprites.Single(s => s.name == "TP_Tile01_1");
            var available = sprites.Single(s => s.name == "TP_Tile01_144");
            var platform = sprites.Single(s => s.name == "TP_Tile01_290");
            var presentation = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InGameGridPresentation>(true)).Single();
            var theme = AssetDatabase.LoadAssetAtPath<GridBoardThemeSO>(THEME_PATH);
            if (theme == null) { theme = ScriptableObject.CreateInstance<GridBoardThemeSO>(); AssetDatabase.CreateAsset(theme, THEME_PATH); }
            var data = new SerializedObject(theme);
            data.FindProperty("_unclaimed").objectReferenceValue = unclaimed;
            data.FindProperty("_available").objectReferenceValue = available;
            data.FindProperty("_platform").objectReferenceValue = platform;
            data.FindProperty("_unclaimedTint").colorValue = new Color(1f, 1f, 0.92f);
            data.FindProperty("_availableTint").colorValue = new Color(0.38f, 0.44f, 0.45f);
            data.FindProperty("_platformTint").colorValue = new Color(0.50f, 0.37f, 0.55f);
            data.FindProperty("_platformBorder").colorValue = new Color(0.45f, 0.31f, 0.49f);
            data.FindProperty("_availableBorder").colorValue = new Color(0.20f, 0.26f, 0.25f);
            data.FindProperty("_borderWidth").floatValue = 0.0625f;
            data.FindProperty("_unclaimedUIOverlay").colorValue = Color.clear;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var runner in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GridPrototypeRunner>(true)))
            {
                data = new SerializedObject(runner);
                data.FindProperty("_boardTheme").objectReferenceValue = theme;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(runner);
            }
            data = new SerializedObject(presentation);
            data.FindProperty("_theme").objectReferenceValue = theme;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(presentation);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
