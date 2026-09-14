using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Skill
{
    /// <summary>
    /// 실제 게임 씬용 ITargetProvider — UnitRegistry(세진 파트)의 살아있는 용사(Hero)를 스킬 타겟으로 넘긴다.
    /// 샌드박스(SandboxTargetProvider)와는 별개. UnitBase가 IDamageable을 구현하고 있어서 캐스팅만 하면 됨.
    /// </summary>
    public class RealTargetProvider : ITargetProvider
    {
        private readonly List<IDamageable> _buffer = new List<IDamageable>();

        public IReadOnlyList<IDamageable> All
        {
            get
            {
                _buffer.Clear();
                var units = UnitRegistry.GetUnits(UnitSide.Hero);
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i] is IDamageable d && !d.IsDead) _buffer.Add(d);
                }
                return _buffer;
            }
        }

        public void QueryInRadius(Vector3 center, float radius, List<IDamageable> results)
        {
            var units = UnitRegistry.GetUnits(UnitSide.Hero);
            float radiusSqr = radius * radius;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] is IDamageable d && !d.IsDead && (d.Position - center).sqrMagnitude <= radiusSqr)
                {
                    results.Add(d);
                }
            }
        }

        public IDamageable Nearest(Vector3 from)
        {
            var units = UnitRegistry.GetUnits(UnitSide.Hero);
            IDamageable nearest = null;
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] is IDamageable d && !d.IsDead)
                {
                    float sqr = (d.Position - from).sqrMagnitude;
                    if (sqr < nearestSqr)
                    {
                        nearestSqr = sqr;
                        nearest = d;
                    }
                }
            }
            return nearest;
        }
    }
}
