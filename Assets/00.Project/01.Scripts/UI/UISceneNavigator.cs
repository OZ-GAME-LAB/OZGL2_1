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
            if (!TryLoadScene(scenePath, out var error)) Debug.LogWarning(error, this);
        }

        /// <summary>이동 요청 접수 여부를 반환한다. 기존 UnityEvent용 LoadScene은 유지한다.</summary>
        public bool TryLoadScene(string scenePath, out string error)
        {
            error = null;
            if (_isLoading) { error = "Scene transition is already in progress."; return false; }
            if (string.IsNullOrWhiteSpace(scenePath)) { error = "Scene path is required."; return false; }
            try
            {
                if (Application.CanStreamedLevelBeLoaded(scenePath))
                {
                    _isLoading = true;
                    var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
                    if (operation == null) throw new InvalidOperationException("Scene loading did not start.");
                }
                else if (Application.isEditor && PreviewSceneRequested != null)
                {
                    _isLoading = true;
                    PreviewSceneRequested.Invoke(scenePath);
                }
                else { error = "이동할 Scene을 빌드 Scene 목록에 등록해야 합니다: " + scenePath; return false; }
                return true;
            }
            catch (Exception exception)
            {
                if (this != null) _isLoading = false;
                error = exception.Message;
                return false;
            }
        }
    }
}
