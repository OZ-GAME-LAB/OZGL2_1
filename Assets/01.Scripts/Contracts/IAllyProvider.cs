using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Contracts
{
    /// <summary>아군(몬스터) 목록 조회. 흡혈 의식·축복 오라·광폭화·망자 부활 등 아군 대상 스킬이 사용.</summary>
    public interface IAllyProvider
    {
        IReadOnlyList<IHealable> Allies { get; }

        void QueryAlliesInRadius(Vector3 center, float radius, List<IHealable> results);

        /// <summary>죽은 아군 최대 count 명 부활. 실제 부활한 수 반환.</summary>
        int ReviveDead(int count);
    }
}
