using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 유닛을 클릭하면 사거리 표시를 토글한다. 씬에 직접 배치할 필요 없이 게임 시작 시 자동 생성된다.
/// 같은 유닛을 다시 클릭하면 선택 해제, 다른 곳을 클릭하면 선택 해제, 다른 유닛을 클릭하면 전환.
/// </summary>
public class UnitSelectionController : MonoBehaviour
{
    private static UnitSelectionController _instance;
    private UnitBase _selected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        var go = new GameObject("UnitSelectionController");
        _instance = go.AddComponent<UnitSelectionController>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        // 선택된 유닛이 죽거나 사라지면 표시도 같이 정리한다.
        // 배치 화면처럼 전투 유닛이 숨겨진 상태(forceRenderingOff)가 되면 선택도 같이 풀어준다.
        if (_selected != null &&
            (_selected.currentState == UnitState.Dead || !_selected.gameObject.activeInHierarchy || !IsRendered(_selected)))
        {
            Deselect();
        }

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        // 원근 카메라에선 ScreenToWorldPoint(z=0)이 카메라 위치 자체를 돌려줘서 마우스 위치와 무관해진다.
        // 카메라 종류와 상관없이 동작하도록 마우스 레이를 z=0 평면(2D 유닛이 있는 평면)과 교차시켜 구한다.
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane unitPlane = new Plane(Vector3.forward, Vector3.zero);
        if (!unitPlane.Raycast(ray, out float enter))
        {
            Deselect();
            return;
        }

        Vector3 worldPoint = ray.GetPoint(enter);
        // 겹친 콜라이더 중 실제로 화면에 그려지는 유닛만 후보로 삼는다.
        UnitBase unit = null;
        foreach (Collider2D hit in Physics2D.OverlapPointAll(worldPoint))
        {
            UnitBase candidate = hit.GetComponentInParent<UnitBase>();
            if (candidate != null && IsRendered(candidate))
            {
                unit = candidate;
                break;
            }
        }

        if (unit == _selected)
        {
            Deselect(); // 같은 유닛 재클릭 = 선택 해제
            return;
        }

        Deselect();
        if (unit != null && unit.currentState != UnitState.Dead)
        {
            _selected = unit;
            unit.SetRangeIndicatorVisible(true);
        }
    }

    /// <summary>
    /// 콜라이더는 살아있지만 화면에선 숨겨진 유닛(배치 화면에서 InGamePhasePresentation이
    /// forceRenderingOff로 숨긴 전투 유닛 등)은 클릭 대상에서 제외하기 위한 판정.
    /// </summary>
    private static bool IsRendered(UnitBase unit)
    {
        foreach (var renderer in unit.GetComponentsInChildren<SpriteRenderer>())
        {
            if (renderer.enabled && !renderer.forceRenderingOff)
            {
                return true;
            }
        }

        return false;
    }

    private void Deselect()
    {
        if (_selected != null)
        {
            _selected.SetRangeIndicatorVisible(false);
        }
        _selected = null;
    }
}
