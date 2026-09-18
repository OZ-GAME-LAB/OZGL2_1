using System.Collections.Generic;
using UnityEngine;
using OZGL2.Contracts;

namespace OZGL2.Sandbox
{
    /// <summary>
    /// 샌드박스의 적(용사) + 아군(몬스터) 목록을 관리. ITargetProvider(적) 와 IAllyProvider(아군) 겸용.
    /// 실제 씬에서는 세진의 스포너/전투 매니저가 이 두 인터페이스를 구현.
    /// </summary>
    public class SandboxProviders : ITargetProvider, IAllyProvider
    {
        private readonly List<IDamageable> _heroes = new List<IDamageable>();
        private readonly List<IHealable> _monsters = new List<IHealable>();

        public void RegisterHero(IDamageable hero) => _heroes.Add(hero);
        public void RegisterMonster(IHealable monster) => _monsters.Add(monster);

        public void Clear()
        {
            _heroes.Clear();
            _monsters.Clear();
        }

        // ─────────── ITargetProvider (적)

        public IReadOnlyList<IDamageable> All => _heroes;

        public void QueryInRadius(Vector3 center, float radius, List<IDamageable> results)
        {
            float r2 = radius * radius;
            foreach (var h in _heroes)
            {
                if (!h.IsDead && (h.Position - center).sqrMagnitude <= r2)
                {
                    results.Add(h);
                }
            }
        }

        public IDamageable Nearest(Vector3 from)
        {
            IDamageable best = null;
            float bestSqr = float.MaxValue;
            foreach (var h in _heroes)
            {
                if (h.IsDead) continue;
                float d = (h.Position - from).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = h;
                }
            }

            return best;
        }

        // ─────────── IAllyProvider (아군)

        public IReadOnlyList<IHealable> Allies => _monsters;

        public void QueryAlliesInRadius(Vector3 center, float radius, List<IHealable> results)
        {
            float r2 = radius * radius;
            foreach (var m in _monsters)
            {
                if ((m.Position - center).sqrMagnitude <= r2)
                {
                    results.Add(m);
                }
            }
        }

        public int ReviveDead(int count)
        {
            int revived = 0;
            foreach (var m in _monsters)
            {
                if (revived >= count) break;
                if (m.IsDead && m is SandboxUnit unit)
                {
                    unit.ForceRevive();
                    revived++;
                }
            }

            return revived;
        }
    }
}
