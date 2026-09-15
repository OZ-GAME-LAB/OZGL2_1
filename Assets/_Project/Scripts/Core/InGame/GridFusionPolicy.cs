using OZGL2.Grid;

namespace OZGL2.InGame
{
    /// <summary>팀 합성 규칙을 배치 데이터에 연결한다. Grid는 전투 오브젝트를 참조하지 않는다.</summary>
    public static class GridFusionPolicy
    {
        public static bool CanFuse(UnitPlacement source, UnitPlacement target)
            => FusionRules.CanFuse(source.Definition.Id, source.StarLevel, target.Definition.Id, target.StarLevel);
    }
}
