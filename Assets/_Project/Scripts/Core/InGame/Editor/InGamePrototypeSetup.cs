using System;
using System.Collections.Generic;
using OZGL2.Grid.Prototype;
using OZGL2.Stage;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OZGL2.InGame.Editor
{
    public static class InGamePrototypeSetup
    {
        public const string SCENE_PATH = "Assets/00.Scenes/Builds/InGame.unity";
        public const string CONFIG_PATH = "Assets/_Project/Data/InGame/InGamePrototypeConfig.asset";
        private const string LOBBY_PATH = "Assets/00.Scenes/Builds/Lobby.unity";
        private const string GRID_DATA_PATH = "Assets/_Project/Data/Grid/Prototype/";
        [MenuItem("OZGL2/InGame/Set Up Prototype")]
        public static void SetUp()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != SCENE_PATH || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Open the saved InGame scene in Edit mode first.");
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.GetComponent<InGamePrototypeBootstrap>() != null)
                    throw new InvalidOperationException("InGame prototype already exists.");
            EnsureFolder("Assets/_Project/Data/InGame");
            EnsureFolder("Assets/_Project/Prefabs/InGame");
            var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(CONFIG_PATH);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<InGamePrototypeConfigSO>();
                AssetDatabase.CreateAsset(config, CONFIG_PATH);
                var data = new SerializedObject(config);
                data.FindProperty("_stage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StageDataSO>("Assets/03.ScriptableObjects/Stage/Dummy/DummyStageHard.asset");
                data.FindProperty("_catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GridPrototypeCatalogSO>(GRID_DATA_PATH + "GridPrototypeCatalog.asset");
                var definition = ((GridPrototypeCatalogSO)data.FindProperty("_catalog").objectReferenceValue).CreateDefinition();
                data.FindProperty("_initialAnchor").vector2IntValue = definition.InitialOrigin + new Vector2Int((definition.InitialSize.x - 1) / 2, 0);
                data.FindProperty("_lobbyScenePath").stringValue = LOBBY_PATH;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            config.Validate();
            var go = new GameObject("InGamePrototype");
            var bootstrap = go.AddComponent<InGamePrototypeBootstrap>();
            var navigator = go.AddComponent<UISceneNavigator>();
            var pages = go.AddComponent<UIPageGroup>();
            var view = go.AddComponent<InGameDummyView>();
            var battle = new GameObject("DummyBattlePage"); battle.transform.SetParent(go.transform, false);
            var grid = new GameObject("PreparationPage"); grid.transform.SetParent(go.transform, false);
            grid.SetActive(false);
            grid.AddComponent<UIDocument>().panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(GRID_DATA_PATH + "GridPanelSettings.asset");
            var runner = grid.AddComponent<GridPrototypeRunner>();
            SetReference(bootstrap, "_config", config); SetReference(bootstrap, "_navigator", navigator);
            SetReference(view, "_bootstrap", bootstrap); SetReference(view, "_gridView", runner);
            SetReference(view, "_pages", pages); SetReference(view, "_battlePage", battle);
            var group = new SerializedObject(pages);
            var list = group.FindProperty("_pages"); list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = battle;
            list.GetArrayElementAtIndex(1).objectReferenceValue = grid;
            group.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, "Assets/_Project/Prefabs/InGame/InGamePrototype.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string path in new[] { SCENE_PATH, LOBBY_PATH })
            {
                var existing = scenes.Find(scene => scene.path == path);
                if (existing == null) scenes.Add(new EditorBuildSettingsScene(path, true));
                else existing.enabled = true;
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
        private static void SetReference(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            if (value == null) throw new InvalidOperationException("Missing InGame reference: " + property);
            var data = new SerializedObject(target); data.FindProperty(property).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
