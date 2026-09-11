using System.Collections.Generic;

/// <summary>
/// 현재 씬에 살아있는(활성화된) 유닛을 진영별로 추적하는 정적 레지스트리.
/// 그리드/스폰 시스템이 아직 없어서 "누가 어디 있는지"를 유닛 스스로 등록하는 방식으로 대체한다.
/// 실제 그리드 조회 시스템이 붙으면 내부 구현만 바꾸면 되도록 조회 인터페이스만 노출.
/// </summary>
public static class UnitRegistry
{
    private static readonly List<UnitBase> HeroUnits = new List<UnitBase>();
    private static readonly List<UnitBase> DemonArmyUnits = new List<UnitBase>();

    public static void Register(UnitBase unit)
    {
        List<UnitBase> list = GetList(unit.Side);
        if (!list.Contains(unit))
        {
            list.Add(unit);
        }
    }

    public static void Unregister(UnitBase unit)
    {
        GetList(unit.Side).Remove(unit);
    }

    public static IReadOnlyList<UnitBase> GetUnits(UnitSide side)
    {
        return GetList(side);
    }

    /// <summary>
    /// 죽지 않은(Dead 상태가 아닌) 유닛 수. 라운드 승패 판정(Day2 후반/코어루프 연계)에서 쓸 예정.
    /// </summary>
    public static int GetAliveCount(UnitSide side)
    {
        List<UnitBase> list = GetList(side);
        int count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].currentState != UnitState.Dead)
            {
                count++;
            }
        }
        return count;
    }

    private static List<UnitBase> GetList(UnitSide side)
    {
        return side == UnitSide.Hero ? HeroUnits : DemonArmyUnits;
    }
}
