using System;
using System.Collections.Generic;
using OZGL2.Stage.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OZGL2.Stage.Editor
{
    public static class StagePoolPrototypeSetup
    {
        private const string PREFAB_PATH = "Assets/_Project/Prefabs/Stage/DummyHero.prefab";
        private const string CATALOG_PATH = "Assets/03.ScriptableObjects/Stage/Dummy/DummyHeroPoolCatalog.asset";
        [MenuItem("OZGL2/Stage/Set Up Pool Prototype In JOB_KIMGUN")]
        public static void Configure()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/00.Scenes/JOB_KIMGUN.unity")
                throw new InvalidOperationException("Open JOB_KIMGUN in Edit mode first.");
            StagePrototypeRunner runner = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var candidate = root.GetComponent<StagePrototypeRunner>();
                if (candidate != null) runner = candidate;
            }
            if (runner == null) throw new InvalidOperationException("StagePrototypeRunner is missing.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                EnsureFolder("Assets/_Project/Prefabs/Stage");
                var temporary = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                temporary.name = "DummyHero";
                temporary.AddComponent<PooledHero>();
                try { prefab = PrefabUtility.SaveAsPrefabAsset(temporary, PREFAB_PATH); }
                finally { UnityEngine.Object.DestroyImmediate(temporary); }
            }
            var catalog = AssetDatabase.LoadAssetAtPath<HeroPoolCatalogSO>(CATALOG_PATH);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HeroPoolCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
                var ids = new SortedSet<string>();
                foreach (var name in new[] { "DummyStageNormal", "DummyStageHard" })
                {
                    var data = AssetDatabase.LoadAssetAtPath<StageDataSO>("Assets/03.ScriptableObjects/Stage/Dummy/" + name + ".asset");
                    foreach (var round in data.CreateSnapshot().Rounds)
                        foreach (var spawn in round.Spawns) ids.Add(spawn.HeroId);
                }
                var serialized = new SerializedObject(catalog);
                var entries = serialized.FindProperty("_entries");
                entries.arraySize = ids.Count;
                int index = 0;
                foreach (string id in ids)
                {
                    var entry = entries.GetArrayElementAtIndex(index++);
                    entry.FindPropertyRelative("_heroId").stringValue = id;
                    entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab.GetComponent<PooledHero>();
                    // 미확정 밸런스와 분리된 프로토타입 설정값. 생성 이후 Inspector에서 조정한다.
                    entry.FindPropertyRelative("_initialCapacity").intValue = 8;
                    entry.FindPropertyRelative("_growthCount").intValue = 4;
                    entry.FindPropertyRelative("_experience").intValue = 1;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var runnerData = new SerializedObject(runner);
            runnerData.FindProperty("_heroPoolCatalog").objectReferenceValue = catalog;
            runnerData.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
