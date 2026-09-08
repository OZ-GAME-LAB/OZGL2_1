using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Contracts
{
    /// <summary>
    /// 전장의 적(용사) 목록을 조회하는 창구. 스킬·시너지 시스템이 "누구를 때릴지"를 여기서 얻는다.
    /// 샌드박스: SandboxTargetProvider(큐브 목록). 실제 씬: 세진의 스포너/전투 매니저가 구현.
    /// </summary>
    public interface ITargetProvider
    {
        IReadOnlyList<IDamageable> All { get; }

        /// <summary>center 반경 radius 안의 살아있는 대상을 results 에 채운다(호출 전 Clear 는 호출자 책임).</summary>
        void QueryInRadius(Vector3 center, float radius, List<IDamageable> results);

        /// <summary>from 에서 가장 가까운 살아있는 대상. 없으면 null.</summary>
        IDamageable Nearest(Vector3 from);
    }
}
