using System;
using System.Collections.Generic;
using OZGL2.Grid;
using OZGL2.Synergy;

namespace OZGL2.InGame
{
    /// <summary>표시 수명과 무관하게 배치 수를 팀 시너지의 공개 API에 전달한다.</summary>
    public sealed class InGameSynergyConnection : IDisposable
    {
        private readonly DemonArmyCatalog _catalog;
        private readonly RealSynergySync _target;
        private GridRunSession _session;
        public bool IsConnected => _target != null;

        public InGameSynergyConnection(DemonArmyCatalog catalog, RealSynergySync target)
        { _catalog = catalog; _target = target; }

        public void Bind(GridRunSession session)
        {
            if (session != null && session.IsEnded) session = null;
            if (_session == session) return;
            if (_session != null) _session.Grid.LayoutChanged -= SyncCounts;
            _session = session;
            if (_session != null) _session.Grid.LayoutChanged += SyncCounts;
            SyncCounts();
        }

        private void SyncCounts()
        {
            if (_target == null) return;
            var counts = new Dictionary<SynergyJob, int>();
            if (_session != null)
                foreach (var unit in _session.Grid.Units)
                {
                    if (!unit.IsPlaced) continue;
                    var prefab = _catalog.FindPrefab(unit.Definition.Id);
                    if (prefab == null || prefab.statData == null)
                        throw new InvalidOperationException("Missing synergy unit data: " + unit.Definition.Id);
                    var job = prefab.statData.job;
                    counts.TryGetValue(job, out int count);
                    counts[job] = count + 1;
                }
            foreach (SynergyJob job in Enum.GetValues(typeof(SynergyJob)))
                _target.SetCount(job, counts.TryGetValue(job, out int count) ? count : 0);
        }

        public void Dispose() => Bind(null);
    }
}
