using UnityEngine;

/// <summary>
/// 유닛 클릭 시 사거리를 원으로 표시. LineRenderer로 그려서 별도 스프라이트/에셋이 필요 없다.
/// UnitSelectionController가 선택 상태에 따라 SetVisible을 호출한다.
/// 원은 좌우 대칭이라 부모(유닛) 스프라이트가 좌우 반전돼도 별도 보정이 필요 없다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class UnitRangeIndicator : MonoBehaviour
{
    private const int Segments = 48;
    private LineRenderer _line;
    private UnitBase _unit;

    public static UnitRangeIndicator Attach(UnitBase unit)
    {
        var go = new GameObject("RangeIndicator");
        go.transform.SetParent(unit.transform, false);
        var indicator = go.AddComponent<UnitRangeIndicator>();
        indicator.Init(unit);
        return indicator;
    }

    private void Init(UnitBase unit)
    {
        _unit = unit;
        _line = GetComponent<LineRenderer>();
        _line.useWorldSpace = false;
        _line.loop = true;
        _line.positionCount = Segments;
        _line.startWidth = _line.endWidth = 0.04f;
        _line.material = new Material(Shader.Find("Sprites/Default"));
        _line.startColor = _line.endColor = new Color(1f, 1f, 1f, 0.65f);
        _line.sortingOrder = 500;
        gameObject.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (visible && _unit != null && _unit.statData != null)
        {
            DrawCircle(_unit.statData.attackRange);
        }
        gameObject.SetActive(visible);
    }

    private void DrawCircle(float radius)
    {
        for (int i = 0; i < Segments; i++)
        {
            float angle = (float)i / Segments * Mathf.PI * 2f;
            _line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }
}
