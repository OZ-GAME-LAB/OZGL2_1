using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.UIFlow
{
    public sealed class UISceneNavigator : MonoBehaviour
    {
        // 빌드 등록 없이 Editor에서만 Scene 이동을 시험하기 위한 연결점이다.
        public static event Action<string> PreviewSceneRequested;
        private bool _isLoading;

        public void LoadScene(string scenePath)
        {
            if (_isLoading || string.IsNullOrWhiteSpace(scenePath)) return;
            try
            {
                if (Application.CanStreamedLevelBeLoaded(scenePath))
                {
                    _isLoading = true;
                    SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
                }
                else if (Application.isEditor && PreviewSceneRequested != null)
                {
                    _isLoading = true;
                    PreviewSceneRequested.Invoke(scenePath);
                }
                else Debug.LogWarning("이동할 Scene을 빌드 Scene 목록에 등록해야 합니다: " + scenePath, this);
            }
            catch (Exception exception)
            {
                if (this != null) _isLoading = false;
                Debug.LogException(exception);
            }
        }
    }
}
