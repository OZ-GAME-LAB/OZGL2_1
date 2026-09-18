using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.Sandbox.EditorTools
{
    /// <summary>
    /// 개인 루프 테스트용 씬 3개(로비 → 스테이지 선택 → 인게임)를 만들어서 전용 폴더에 정리한다.
    /// 팀 실제 씬(Builds/Lobby 등)은 전혀 안 건드리고, 기존 JOB_SUNGMIN.unity도 같이 이 폴더로 옮겨서
    /// 성민 개인 테스트 씬을 한곳에 모은다.
    /// </summary>
    public static class SandboxLoopSceneSetup
    {
        private const string ScenesRoot = "Assets/00.Scenes";
        private const string PersonalFolder = "Assets/00.Scenes/JOB_SUNGMIN";
        private const string MainJobScenePath = "Assets/00.Scenes/JOB_SUNGMIN.unity";

        [MenuItem("OZGL2/Sandbox/Create Personal Loop Scenes (Lobby-Stage-InGame)")]
        public static void CreateAll()
        {
            var current = EditorSceneManager.GetActiveScene();
            if (current.isDirty)
            {
                Debug.LogError("[OZGL2] 현재 씬에 저장 안 된 변경사항이 있어 — Ctrl+S로 저장부터 하고 다시 실행해줘. " +
                    "(작업 중인 씬이 덮어써지는 걸 막기 위해 저장 전엔 진행 안 함)");
                return;
            }
            string originalPath = current.path;

            EnsureFolder();

            CreateLobbyScene();
            CreateStageChoiceScene();
            CreateInGameScene();

            string movedMainScenePath = MoveMainJobSceneIfNeeded();
            RegisterInBuildSettings();

            string reopenPath = (originalPath == MainJobScenePath && movedMainScenePath != null) ? movedMainScenePath : originalPath;
            if (!string.IsNullOrEmpty(reopenPath) && File.Exists(reopenPath)) EditorSceneManager.OpenScene(reopenPath);

            Debug.Log($"[OZGL2] 개인 테스트 씬을 {PersonalFolder}/ 폴더로 정리 완료 — " +
                $"{SandboxLoopState.LobbyScene} / {SandboxLoopState.StageChoiceScene} / {SandboxLoopState.InGameScene}" +
                (movedMainScenePath != null ? " / JOB_SUNGMIN" : "") + ".");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(PersonalFolder)) AssetDatabase.CreateFolder(ScenesRoot, "JOB_SUNGMIN");
        }

        /// <summary>기존 JOB_SUNGMIN.unity(00.Scenes 바로 아래)를 새 폴더로 옮긴다. 이미 옮겨져 있으면 그대로 둠.</summary>
        private static string MoveMainJobSceneIfNeeded()
        {
            if (!File.Exists(MainJobScenePath)) return null;
            string dest = $"{PersonalFolder}/JOB_SUNGMIN.unity";
            if (File.Exists(dest)) return dest;
            string error = AssetDatabase.MoveAsset(MainJobScenePath, dest);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[OZGL2] JOB_SUNGMIN.unity 이동 실패: {error}");
                return null;
            }
            return dest;
        }

        private static void CreateLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(new Color(0.15f, 0.18f, 0.25f));
            new GameObject("LobbyScreen").AddComponent<SandboxLobbyScreen>();
            SaveScene(scene, SandboxLoopState.LobbyScene);
        }

        private static void CreateStageChoiceScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(new Color(0.2f, 0.15f, 0.15f));
            new GameObject("StageChoiceScreen").AddComponent<SandboxStageChoiceScreen>();
            SaveScene(scene, SandboxLoopState.StageChoiceScene);
        }

        private static void CreateInGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(new Color(0.35f, 0.45f, 0.6f));
            new GameObject("StageBalanceSandbox").AddComponent<StageBalanceSandbox>();
            SaveScene(scene, SandboxLoopState.InGameScene);
        }

        private static void AddCamera(Color bg)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = bg;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void SaveScene(Scene scene, string sceneName)
        {
            string newPath = $"{PersonalFolder}/{sceneName}.unity";
            EditorSceneManager.SaveScene(scene, newPath);

            // 예전(폴더 정리 전) 버전이 00.Scenes 바로 아래 남아있으면 정리
            string oldLoosePath = $"{ScenesRoot}/{sceneName}.unity";
            if (oldLoosePath != newPath && File.Exists(oldLoosePath)) AssetDatabase.DeleteAsset(oldLoosePath);
        }

        private static void RegisterInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            // 예전 루즈 경로로 등록된 항목이 있으면 제거하고 새 폴더 경로로 재등록
            scenes.RemoveAll(s => s.path.StartsWith($"{ScenesRoot}/JOB_SUNGMIN_") && !s.path.StartsWith(PersonalFolder + "/"));

            AddIfMissing(scenes, SandboxLoopState.LobbyScene);
            AddIfMissing(scenes, SandboxLoopState.StageChoiceScene);
            AddIfMissing(scenes, SandboxLoopState.InGameScene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddIfMissing(List<EditorBuildSettingsScene> scenes, string sceneName)
        {
            string path = $"{PersonalFolder}/{sceneName}.unity";
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
    }
}
