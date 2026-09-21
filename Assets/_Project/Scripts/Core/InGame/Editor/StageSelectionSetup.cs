using System;
using System.Collections.Generic;
using OZGL2.Stage;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.InGame.Editor
{
    public static class StageSelectionSetup
    {
        public const string SCENE_PATH = "Assets/00.Scenes/Builds/StageChoice.unity";
        public const string CATALOG_PATH = "Assets/_Project/Data/InGame/StageCatalog.asset";
        public const string PREFAB_PATH = "Assets/_Project/Prefabs/InGame/StageSelectionPrototype.prefab";
        private const string LOBBY_PATH = "Assets/00.Scenes/Builds/Lobby.unity";
        private const string BALANCE_PATH = "Assets/03.ScriptableObjects/Stage/Balance/";

        [MenuItem("OZGL2/InGame/Set Up Stage Selection")]
        public static void SetUp()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before setup.");
            string previous = SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(previous)) throw new InvalidOperationException("Open a saved scene before setup.");
            var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(InGamePrototypeSetup.CONFIG_PATH);
            if (config == null) throw new InvalidOperationException("Missing InGame config.");
            var normal = AssetDatabase.LoadAssetAtPath<StageDataSO>(BALANCE_PATH + "StageNormal30.asset");
            var hard = AssetDatabase.LoadAssetAtPath<StageDataSO>(BALANCE_PATH + "StageHard50.asset");
            if (normal == null || hard == null) throw new InvalidOperationException("Missing team balance stage assets.");
            config.Validate(normal.CreateSnapshot()); config.Validate(hard.CreateSnapshot());

            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalogSO>(CATALOG_PATH);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
            }
            var catalogData = new SerializedObject(catalog);
            var stages = catalogData.FindProperty("_stages"); stages.arraySize = 2;
            stages.GetArrayElementAtIndex(0).objectReferenceValue = normal;
            stages.GetArrayElementAtIndex(1).objectReferenceValue = hard;
            catalogData.ApplyModifiedPropertiesWithoutUndo();
            catalog.CreateDefinitions();
            var settings = new SerializedObject(config);
            settings.FindProperty("_stageCatalog").objectReferenceValue = catalog;
            settings.FindProperty("_allowEditorDirectStart").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                var scene = EditorSceneManager.OpenScene(SCENE_PATH);
                var controller = UnityEngine.Object.FindFirstObjectByType<StageSelectionController>();
                GameObject root = controller != null ? controller.gameObject : new GameObject("StageSelectionPrototype");
                var navigator = root.GetComponent<UISceneNavigator>() ?? root.AddComponent<UISceneNavigator>();
                controller = root.GetComponent<StageSelectionController>() ?? root.AddComponent<StageSelectionController>();
                var view = root.GetComponent<StageSelectionDummyView>() ?? root.AddComponent<StageSelectionDummyView>();
                var data = new SerializedObject(controller);
                data.FindProperty("_inGameConfig").objectReferenceValue = config;
                data.FindProperty("_navigator").objectReferenceValue = navigator;
                data.FindProperty("_inGameScenePath").stringValue = InGamePrototypeSetup.SCENE_PATH;
                data.FindProperty("_lobbyScenePath").stringValue = LOBBY_PATH;
                data.ApplyModifiedPropertiesWithoutUndo();
                data = new SerializedObject(view);
                data.FindProperty("_controller").objectReferenceValue = controller;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PREFAB_PATH, InteractionMode.AutomatedAction);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                var existing = scenes.Find(item => item.path == SCENE_PATH);
                if (existing == null) scenes.Add(new EditorBuildSettingsScene(SCENE_PATH, true));
                else existing.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.OpenScene(previous); }
        }
    }
}
