using System;
using System.Linq;
using OZGL2.Grid.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.InGame.Editor
{
    public static class InGameCameraSetup
    {
        public const string CONFIG_PATH = "Assets/_Project/Data/InGame/InGameCameraConfig.asset";
        [MenuItem("OZGL2/InGame/Apply World Preparation and Camera")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/00.Scenes/Builds/InGame.unity" || scene.isDirty)
                throw new InvalidOperationException("Open the saved InGame scene in Edit mode.");
            var roots = scene.GetRootGameObjects();
            var bootstrap = roots.SelectMany(r => r.GetComponentsInChildren<InGamePrototypeBootstrap>(true)).Single();
            var runner = roots.SelectMany(r => r.GetComponentsInChildren<GridPrototypeRunner>(true)).Single();
            var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
            var config = AssetDatabase.LoadAssetAtPath<InGameCameraConfigSO>(CONFIG_PATH);
            if (config == null) { config = ScriptableObject.CreateInstance<InGameCameraConfigSO>(); AssetDatabase.CreateAsset(config, CONFIG_PATH); }
            var transition = bootstrap.GetComponent<InGameCameraTransition>();
            if (transition == null) transition = bootstrap.gameObject.AddComponent<InGameCameraTransition>();
            var presentation = bootstrap.GetComponent<InGamePhasePresentation>();
            if (presentation == null) presentation = bootstrap.gameObject.AddComponent<InGamePhasePresentation>();
            Set(transition, "_camera", camera); Set(transition, "_config", config);
            Set(presentation, "_bootstrap", bootstrap); Set(presentation, "_runner", runner); Set(presentation, "_camera", transition);
            Set(bootstrap, "_phasePresentation", presentation);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(property).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
