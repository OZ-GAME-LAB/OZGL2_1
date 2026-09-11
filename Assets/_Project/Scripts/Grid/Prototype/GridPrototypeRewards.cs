using System;
using System.Collections.Generic;

namespace OZGL2.Grid.Prototype
{
    /// <summary>확률 밸런싱 대신 카탈로그를 순환하는 더미 후보 제공자. 실제 보상 시스템으로 교체한다.</summary>
    public sealed class GridPrototypeRewards
    {
        private readonly IReadOnlyList<UnitDefinition> _units;
        private readonly Dictionary<string, FootprintDefinition> _shapes = new Dictionary<string, FootprintDefinition>();
        public GridPrototypeRewards(GridPrototypeCatalogSO catalog)
        {
            _units = catalog.CreateUnits();
            if (_units.Count < 2) throw new ArgumentException("Two reward candidates are required.");
            foreach (var shape in catalog.CreateBlocks()) _shapes.Add(shape.Id, shape);
            foreach (var unit in _units)
                if (!_shapes.ContainsKey(unit.RequiredBlockId)) throw new ArgumentException("Unknown required block: " + unit.RequiredBlockId);
        }
        public UnitDefinition GetCandidate(int completedRounds, int option)
        {
            if (completedRounds < 1 || option < 0 || option > 1) throw new ArgumentOutOfRangeException();
            return _units[((completedRounds - 1) % _units.Count + option) % _units.Count];
        }
        public bool TryChoose(GridRunSession session, int option)
        {
            if (session.PendingRewardId == null) return false;
            var unit = GetCandidate(session.CompletedRounds, option);
            return session.TryChooseUnit(session.RunId, session.PendingRewardId, unit, _shapes[unit.RequiredBlockId]);
        }
    }
}
