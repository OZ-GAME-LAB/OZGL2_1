using System.Reflection;
using OZGL2.Progression;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비 도감(UIUnitCodexView)을 실제 해금·발견 기록에 연결한다. 도감은 UILobbyCollectionState 의 "열림 표시"만 보는데
    /// 예전에는 그 값을 아무도 채워 주지 않아서, 마왕군을 해금하거나 용사를 만나도 도감은 카탈로그의 기본값 그대로였다.
    /// - 마왕군: UnitUnlockStore(카드 보상 해금)와 같은 기준으로 열린다.
    /// - 용사: 전투에서 만난 용사(CodexStore)가 열린다.
    /// 팀 파일(도감 화면·상태)은 건드리지 않고, 0.4초마다 값을 비교해 바뀐 것만 UILobbyCollectionState.SetUnlocked 로 알린다(화면은 그때 알아서 갱신).
    /// 로비 씬이 열리면 자동으로 붙는다.
    /// </summary>
    public sealed class CodexLobbySync : MonoBehaviour
    {
        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FCatalog = typeof(UIUnitCodexView).GetField("_catalog", Priv);
        private static readonly FieldInfo FState = typeof(UIUnitCodexView).GetField("_state", Priv);

        private UIUnitCodexView _view;
        private float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        private static void Install()
        {
            if (FindFirstObjectByType<UIUnitCodexView>(FindObjectsInactive.Include) == null) return;
            if (FindFirstObjectByType<CodexLobbySync>() != null) return;
            new GameObject("CodexLobbySync").AddComponent<CodexLobbySync>();
        }

        private void OnEnable() => _next = 0f;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.4f;
            Sync();
        }

        private void Sync()
        {
            if (_view == null) _view = FindFirstObjectByType<UIUnitCodexView>(FindObjectsInactive.Include);
            if (_view == null || FCatalog == null || FState == null) return;
            var catalog = FCatalog.GetValue(_view) as UIUnitCatalogSO;
            var state = FState.GetValue(_view) as UILobbyCollectionState ?? FindFirstObjectByType<UILobbyCollectionState>(FindObjectsInactive.Include);
            if (catalog == null || state == null) return;
            foreach (var entry in catalog.Entries)
                if (entry != null && CodexStore.TryIsEntryUnlocked(entry.Id, out bool unlocked))
                    state.SetUnlocked(entry.Id, unlocked);
        }
    }
}
