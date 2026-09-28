using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// UI Toolkit만 사용하는 인게임 씬에서도 uGUI 손패가 입력을 받을 수 있도록
    /// EventSystem이 없을 때에만 런타임 인스턴스를 보완한다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Battle/Hand Event System Installer")]
    public sealed class BattleHandEventSystemInstaller : MonoBehaviour
    {
        private GameObject _ownedEventSystemObject;

        private void Awake()
        {
            EnsureEventSystem();
        }

        private void OnEnable()
        {
            EnsureEventSystem();
        }

        private void EnsureEventSystem()
        {
            if (!Application.isPlaying || _ownedEventSystemObject != null) return;
            if (EventSystem.current != null ||
                FindFirstObjectByType<EventSystem>(FindObjectsInactive.Exclude) != null) return;

            var eventSystemObject = new GameObject("BattleHand_RuntimeEventSystem");
            if (gameObject.scene.IsValid())
                SceneManager.MoveGameObjectToScene(eventSystemObject, gameObject.scene);
            eventSystemObject.AddComponent<EventSystem>();

            InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            _ownedEventSystemObject = eventSystemObject;
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying || _ownedEventSystemObject == null) return;
            Destroy(_ownedEventSystemObject);
            _ownedEventSystemObject = null;
        }
    }
}
