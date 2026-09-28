using System;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace OZGL2.Editor.UIFlow
{
    // 레이아웃 생성 도구가 아니다. Build Settings를 바꾸지 않는 Editor 전용 이동 연결이다.
    [InitializeOnLoad]
    public static class UIFlowPreviewSceneLoader
    {
        private const string SCENE_ROOT = "Assets/00.Scenes/UI_Flow/";

        static UIFlowPreviewSceneLoader()
        {
            UISceneNavigator.PreviewSceneRequested -= LoadPreviewScene;
            UISceneNavigator.PreviewSceneRequested += LoadPreviewScene;
            AssemblyReloadEvents.beforeAssemblyReload += RemoveSubscription;
        }

        private static void LoadPreviewScene(string scenePath)
        {
            if (!EditorApplication.isPlaying || !scenePath.StartsWith(SCENE_ROOT, StringComparison.Ordinal) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new InvalidOperationException("UI_Flow 미리보기 Scene 경로를 확인하세요: " + scenePath);
            EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
        }

        private static void RemoveSubscription()
        {
            UISceneNavigator.PreviewSceneRequested -= LoadPreviewScene;
            AssemblyReloadEvents.beforeAssemblyReload -= RemoveSubscription;
        }
    }
}
