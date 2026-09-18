using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Skill
{
    /// <summary>
    /// 실제 게임 씬용 IAllyProvider — UnitRegistry(세진 파트)의 살아있는 몬스터(DemonArmy, 아군)를
    /// 힐/버프/부활 스킬 대상으로 넘긴다. UnitBase가 IHealable을 구현하고 있어서 캐스팅만 하면 됨.
    /// </summary>
    public class RealAllyProvider : IAllyProvider
    {
        private readonly List<IHealable> _buffer = new List<IHealable>();

        public IReadOnlyList<IHealable> Allies
        {
            get
            {
                _buffer.Clear();
                var units = UnitRegistry.GetUnits(UnitSide.DemonArmy);
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i] is IHealable h && !h.IsDead) _buffer.Add(h);
                }
                return _buffer;
            }
        }

        public void QueryAlliesInRadius(Vector3 center, float radius, List<IHealable> results)
        {
            var units = UnitRegistry.GetUnits(UnitSide.DemonArmy);
            float radiusSqr = radius * radius;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] is IHealable h && !h.IsDead && (h.Position - center).sqrMagnitude <= radiusSqr)
                {
                    results.Add(h);
                }
            }
        }

        public int ReviveDead(int count)
        {
            var units = UnitRegistry.GetUnits(UnitSide.DemonArmy);
            int revived = 0;
            for (int i = 0; i < units.Count && revived < count; i++)
            {
                if (units[i] != null && units[i].currentState == UnitState.Dead)
                {
                    units[i].Revive();
                    revived++;
                }
            }
            return revived;
        }
    }
}
