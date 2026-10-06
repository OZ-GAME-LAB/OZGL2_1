using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 모든 씬의 버튼/토글에 호버·클릭음을 자동으로 입힌다. 버튼마다 컴포넌트를 붙이거나 씬을 수정할 필요 없이
/// 마우스 아래의 최상단 UI를 읽기 전용으로 레이캐스트해서, 그 위 Selectable(Button/Toggle/Dropdown)이
/// 바뀌면 호버음, 눌리면 클릭음(닫기·취소·뒤로 류 이름이면 취소음)을 낸다. BattleSfxWatcher와 같은 자동 부착 패턴.
/// 안 들리게 하고 싶은 버튼이 있으면 그 오브젝트(또는 부모)에 UiSfxMute를 붙인다.
/// </summary>
internal sealed class UiSfxWatcher : MonoBehaviour
{
    private static readonly string[] CancelKeywords =
    {
        "close", "cancel", "back", "exit", "decline", "return", "닫기", "취소", "뒤로", "나가기", "돌아가기",
    };

    private static UiSfxWatcher _instance;

    private readonly List<RaycastResult> _hits = new List<RaycastResult>(16);
    private PointerEventData _pointer;
    private EventSystem _pointerSystem;
    private Selectable _hovered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (_instance != null) return;

        var go = new GameObject("UiSfxWatcher");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<UiSfxWatcher>();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        EventSystem eventSystem = EventSystem.current;
        if (mouse == null || eventSystem == null)
        {
            _hovered = null;
            return;
        }

        bool pressed = mouse.leftButton.wasPressedThisFrame;
        bool moved = mouse.delta.ReadValue().sqrMagnitude > 0.01f;
        if (!pressed && !moved) return;

        Selectable under = FindSelectableUnderPointer(eventSystem, mouse.position.ReadValue());

        if (under != _hovered)
        {
            _hovered = under;
            if (under != null && !pressed) Sfx.Play(SfxId.UiHover);
        }

        if (pressed && under != null)
        {
            Sfx.Play(IsCancelLike(under) ? SfxId.UiCancel : SfxId.UiClick);
        }
    }

    private Selectable FindSelectableUnderPointer(EventSystem eventSystem, Vector2 screenPosition)
    {
        if (_pointer == null || _pointerSystem != eventSystem)
        {
            _pointer = new PointerEventData(eventSystem);
            _pointerSystem = eventSystem;
        }

        _pointer.position = screenPosition;
        _hits.Clear();
        eventSystem.RaycastAll(_pointer, _hits);

        // 가장 위에 걸린 UI 하나만 본다 — 팝업 뒤의 버튼이 소리 나지 않게(위의 비-버튼 UI가 가리면 null).
        for (int i = 0; i < _hits.Count; i++)
        {
            GameObject target = _hits[i].gameObject;
            if (target == null) continue;

            if (target.GetComponentInParent<UiSfxMute>() != null) return null;

            Selectable selectable = target.GetComponentInParent<Selectable>();
            if (selectable == null || !selectable.IsInteractable()) return null;
            if (selectable is Slider || selectable is Scrollbar || selectable is InputField || selectable is TMP_InputField) return null;
            return selectable;
        }

        return null;
    }

    private static bool IsCancelLike(Selectable selectable)
    {
        if (ContainsKeyword(selectable.gameObject.name)) return true;

        TMP_Text label = selectable.GetComponentInChildren<TMP_Text>(true);
        return label != null && ContainsKeyword(label.text);
    }

    private static bool ContainsKeyword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // "Background", "Feedback"처럼 back이 들어갔을 뿐인 이름이 취소로 오인되지 않게 먼저 걷어낸다.
        string lower = text.ToLowerInvariant().Replace("background", string.Empty).Replace("feedback", string.Empty);
        for (int i = 0; i < CancelKeywords.Length; i++)
        {
            if (lower.Contains(CancelKeywords[i])) return true;
        }

        return false;
    }
}
