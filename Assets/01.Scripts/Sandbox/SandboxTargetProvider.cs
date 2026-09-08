using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Sandbox
{
    /// <summary>ITargetProvider 의 샌드박스 구현. 등록된 큐브 목록으로 조회에 답한다.</summary>
    public class SandboxTargetProvider : ITargetProvider
    {
        private readonly List<IDamageable> _all = new List<IDamageable>();

        public IReadOnlyList<IDamageable> All => _all;

        public void Register(IDamageable target)
        {
            _all.Add(target);
        }

        public void Clear()
        {
            _all.Clear();
        }

        public void QueryInRadius(Vector3 center, float radius, List<IDamageable> results)
        {
            float radiusSqr = radius * radius;
            foreach (var target in _all)
            {
                if (!target.IsDead && (target.Position - center).sqrMagnitude <= radiusSqr)
                {
                    results.Add(target);
                }
            }
        }

        public IDamageable Nearest(Vector3 from)
        {
            IDamageable best = null;
            float bestSqr = float.MaxValue;

            foreach (var target in _all)
            {
                if (target.IsDead)
                {
                    continue;
                }

                float sqr = (target.Position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = target;
                }
            }

            return best;
        }
    }
}
