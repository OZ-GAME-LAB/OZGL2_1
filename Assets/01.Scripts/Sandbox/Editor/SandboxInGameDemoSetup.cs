using System.IO;
using UnityEditor;
using UnityEngine;
using OZGL2.InGame;
using OZGL2.Stage;
using OZGL2.Augment;

namespace OZGL2.Sandbox.EditorTools
{
    /// <summary>
    /// InGame 데모(그리드+실전투+보상까지 전체 루프, 코어루프 팀 프리팹)를 현재 열린 씬(JOB_SUNGMIN 등)에
    /// 그대로 가져와서 내 스테이지 밸런스 데이터(StageNormal30/StageHard50)로 실제 플레이 테스트한다.
    /// InGame.unity와 공용 InGamePrototypeConfig.asset은 전혀 안 건드림 — 프리팹 "인스턴스"에만
    /// Config를 내 전용 복사본으로 오버라이드(프리팹 원본·다른 씬에는 영향 없음).
    /// </summary>
    public static class SandboxInGameDemoSetup
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/InGame/InGamePrototype.prefab";
        private const string OriginalConfigPath = "Assets/_Project/Data/InGame/InGamePrototypeConfig.asset";
        private const string MyConfigDir = "Assets/03.ScriptableObjects/Stage/Balance";

        [MenuItem("OZGL2/Sandbox/Add InGame Demo (보통 30R)")]
        public static void AddDemoNormal() => AddDemo("StageNormal30", "InGameConfig_Normal30");

        [MenuItem("OZGL2/Sandbox/Add InGame Demo (어려움 50R)")]
        public static void AddDemoHard() => AddDemo("StageHard50", "InGameConfig_Hard50");

        private static void AddDemo(string stageAssetName, string configAssetName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[OZGL2] InGame 프리팹을 못 찾음: {PrefabPath}");
                return;
            }

            var config = BuildConfigCopy(stageAssetName, configAssetName);
            if (config == null) return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add InGame Demo");

            var bootstrap = instance.GetComponentInChildren<InGamePrototypeBootstrap>(true);
            if (bootstrap == null)
            {
                Debug.LogError("[OZGL2] InGamePrototypeBootstrap을 프리팹 인스턴스에서 못 찾음.");
                return;
            }

            // 실제 증강 연동 — 보스 라운드에서 Dummy 확인 버튼 대신 진짜 3장 뽑기가 뜨게 한다.
            var augmentRewards = instance.AddComponent<RealAugmentRewards>();

            var instanceSo = new SerializedObject(bootstrap);
            instanceSo.FindProperty("_config").objectReferenceValue = config;
            instanceSo.FindProperty("_augmentRewardsOverride").objectReferenceValue = augmentRewards;
            instanceSo.ApplyModifiedProperties();

            // "INGAME / PROTOTYPE CORE LOOP" 개발용 더미 텍스트만 끔 — 그리드 바인딩/페이지 전환
            // 로직(Refresh)은 그대로 살아있어서 라운드 끝나면 준비 화면이 정상적으로 뜬다.
            var dummyView = instance.GetComponentInChildren<InGameDummyView>(true);
            if (dummyView != null)
            {
                var dvSo = new SerializedObject(dummyView);
                dvSo.FindProperty("_showDummyOverlay").boolValue = false;
                dvSo.ApplyModifiedProperties();
            }

            // 내 샌드박스(SkillSandbox)가 따로 스폰하던 테스트용 큐브 유닛은 꺼서 화면 안 겹치게 함
            // — 디버그 패널(마왕 레벨·스킬·특성 등)은 그대로 살아있음, 스폰만 0으로.
            var sandbox = Object.FindFirstObjectByType<OZGL2.Sandbox.SkillSandbox>();
            if (sandbox != null)
            {
                var sbSo = new SerializedObject(sandbox);
                sbSo.FindProperty("_heroCount").intValue = 0;
                sbSo.FindProperty("_monsterCount").intValue = 0;
                sbSo.ApplyModifiedProperties();
            }

            Debug.Log($"[OZGL2] InGame 데모 추가 완료 — Stage: {stageAssetName}. 현재 씬 저장 후 Play 하면 됨.");
        }

        /// <summary>공용 InGamePrototypeConfig을 복사해서 내 전용 Stage만 갈아끼운 사본을 만든다(원본은 그대로 유지).</summary>
        private static InGamePrototypeConfigSO BuildConfigCopy(string stageAssetName, string configAssetName)
        {
            Directory.CreateDirectory(MyConfigDir);
            string destPath = $"{MyConfigDir}/{configAssetName}.asset";

            if (AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(destPath) == null)
            {
                if (!AssetDatabase.CopyAsset(OriginalConfigPath, destPath))
                {
                    Debug.LogError($"[OZGL2] Config 복사 실패: {OriginalConfigPath} → {destPath}");
                    return null;
                }
            }

            var stage = AssetDatabase.LoadAssetAtPath<StageDataSO>($"{MyConfigDir}/{stageAssetName}.asset");
            if (stage == null)
            {
                Debug.LogError($"[OZGL2] 스테이지 데이터를 못 찾음: {MyConfigDir}/{stageAssetName}.asset — 먼저 'OZGL2/Stage/Create Balance Stage Data'부터 실행해줘.");
                return null;
            }

            var config = AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(destPath);
            var configSo = new SerializedObject(config);
            configSo.FindProperty("_stage").objectReferenceValue = stage;
            configSo.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return config;
        }
    }
}
