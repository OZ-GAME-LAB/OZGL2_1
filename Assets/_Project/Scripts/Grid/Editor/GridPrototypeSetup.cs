using System;
using OZGL2.Grid.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OZGL2.Grid.Editor
{
    public static class GridPrototypeSetup
    {
        public const string DATA_PATH = "Assets/_Project/Data/Grid/Prototype";
        public const string PREFAB_PATH = "Assets/_Project/Prefabs/Grid/GridPrototype.prefab";
        public const string SCENE_PATH = "Assets/00.Scenes/JOB_KIMGUN.unity";
        [MenuItem("OZGL2/Grid/Set Up JOB_KIMGUN Prototype")]
        public static void SetUp()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before setup.");
            if (SceneManager.GetActiveScene().path != SCENE_PATH) throw new InvalidOperationException("Open JOB_KIMGUN first.");
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.GetComponent<GridPrototypeRunner>() != null) throw new InvalidOperationException("Grid prototype already exists; no duplicate created.");
            EnsureFolder(DATA_PATH); EnsureFolder("Assets/_Project/Prefabs/Grid");
            var shapes = new[]
            {
                CreateShape("single", "Single", new[] { Vector2Int.zero }),
                CreateShape("line_two", "Line 2", new[] { Vector2Int.zero, Vector2Int.up }),
                CreateShape("line_three", "Line 3", new[] { Vector2Int.down, Vector2Int.zero, Vector2Int.up }),
                CreateShape("corner_three", "Corner 3", new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.down }),
                CreateShape("corner_four", "Corner 4", new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.down, Vector2Int.down * 2 }),
                CreateShape("tee_four", "Tee 4", new[] { Vector2Int.zero, Vector2Int.left, Vector2Int.right, Vector2Int.down })
            };
            var settings = LoadOrCreate<GridSettingsSO>(DATA_PATH + "/GridSettings.asset");
            var data = new SerializedObject(settings);
            data.FindProperty("_initialSize").vector2IntValue = new Vector2Int(4, 3);
            data.FindProperty("_maximumSize").vector2IntValue = new Vector2Int(8, 5);
            data.FindProperty("_expansionId").stringValue = "floor_domino";
            SetCells(data.FindProperty("_expansionCells"), new[] { Vector2Int.zero, Vector2Int.up });
            data.ApplyModifiedPropertiesWithoutUndo();
            var catalog = LoadOrCreate<GridPrototypeCatalogSO>(DATA_PATH + "/GridPrototypeCatalog.asset");
            data = new SerializedObject(catalog); data.FindProperty("_settings").objectReferenceValue = settings;
            var list = data.FindProperty("_footprints"); list.arraySize = shapes.Length;
            for (int i = 0; i < shapes.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = shapes[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            var panel = LoadOrCreate<PanelSettings>(DATA_PATH + "/GridPanelSettings.asset");
            UpgradeSeparatedUnits();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panel.match = 1;
            EditorUtility.SetDirty(panel);
            var go = new GameObject("GridPrototype");
            go.AddComponent<UIDocument>().panelSettings = panel;
            var runner = go.AddComponent<GridPrototypeRunner>();
            ConfigureBootstrap(go, catalog);
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, PREFAB_PATH, InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        [MenuItem("OZGL2/Grid/Update Explicit Bootstrap")]
        public static void UpgradeBootstrap()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != SCENE_PATH)
                throw new InvalidOperationException("Open JOB_KIMGUN in Edit mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<GridPrototypeCatalogSO>(DATA_PATH + "/GridPrototypeCatalog.asset");
            var prefab = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
            try
            {
                ConfigureBootstrap(prefab, catalog);
                PrefabUtility.SaveAsPrefabAsset(prefab, PREFAB_PATH);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.GetComponent<GridPrototypeRunner>() != null) ConfigureBootstrap(root, catalog);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }
        private static void ConfigureBootstrap(GameObject go, GridPrototypeCatalogSO catalog)
        {
            var bootstrap = go.GetComponent<GridPrototypeBootstrap>();
            if (bootstrap == null) bootstrap = go.AddComponent<GridPrototypeBootstrap>();
            var data = new SerializedObject(bootstrap);
            data.FindProperty("_catalog").objectReferenceValue = catalog;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        [MenuItem("OZGL2/Grid/Update Separate Unit Data")]
        public static void UpgradeSeparatedUnits()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before data update.");
            var catalog = AssetDatabase.LoadAssetAtPath<GridPrototypeCatalogSO>(DATA_PATH + "/GridPrototypeCatalog.asset");
            var source = catalog.CreateBlocks();
            var data = new SerializedObject(catalog);
            var units = data.FindProperty("_units"); units.arraySize = source.Count;
            for (int i = 0; i < source.Count; i++)
            {
                var shape = source[i];
                var unit = LoadOrCreate<GridUnitDataSO>(DATA_PATH + "/unit_" + shape.Id + ".asset");
                var unitData = new SerializedObject(unit);
                unitData.FindProperty("_id").stringValue = "dummy_unit_" + shape.Id;
                unitData.FindProperty("_displayName").stringValue = shape.Id == "single" ? "Basic" : shape.Id == "corner_three" ? "Mage" : "Dummy " + shape.DisplayName;
                unitData.FindProperty("_requiredBlockId").stringValue = shape.Id;
                unitData.ApplyModifiedPropertiesWithoutUndo();
                units.GetArrayElementAtIndex(i).objectReferenceValue = unit;
            }
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
        }
        private static BlockShapeSO CreateShape(string id, string label, Vector2Int[] cells)
        {
            var result = LoadOrCreate<BlockShapeSO>(DATA_PATH + "/" + id + ".asset");
            var serialized = new SerializedObject(result); serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = label; SetCells(serialized.FindProperty("_cells"), cells);
            serialized.ApplyModifiedPropertiesWithoutUndo(); return result;
        }
        private static void SetCells(SerializedProperty property, Vector2Int[] cells)
        {
            property.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++) property.GetArrayElementAtIndex(i).vector2IntValue = cells[i];
        }
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/'); string parent = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(parent + "/" + segments[i])) AssetDatabase.CreateFolder(parent, segments[i]);
                parent += "/" + segments[i];
            }
        }
    }
}
