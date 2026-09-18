using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// unitId(M_WAR_01 등) → DemonArmy_Unit 프리팹 매핑. 그리드에서 확정된 배치(unitId 기준)를
/// 실제 UnitBase 프리팹으로 스폰할 때 RealDefenders가 이 카탈로그로 조회한다.
/// </summary>
[CreateAssetMenu(fileName = "New DemonArmy Catalog", menuName = "MajokDefense/DemonArmy Catalog")]
public class DemonArmyCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string unitId;
        [Tooltip("1성 기본 모델 — 그리드 배치 시 최초 스폰에 사용")]
        public UnitBase prefab;
        [Tooltip("합성 2성 모델 (비워두면 1성 모델로 대체)")]
        public UnitBase prefabStar2;
        [Tooltip("합성 3성 모델 (비워두면 1성 모델로 대체)")]
        public UnitBase prefabStar3;

        public UnitBase GetPrefab(int starLevel)
        {
            switch (starLevel)
            {
                case 2: return prefabStar2 != null ? prefabStar2 : prefab;
                case 3: return prefabStar3 != null ? prefabStar3 : prefab;
                default: return prefab;
            }
        }
    }

    public List<Entry> entries = new List<Entry>();

    /// <summary>1성 기본 모델 조회 (그리드 배치 최초 스폰용, 기존 호출부 호환).</summary>
    public UnitBase FindPrefab(string unitId) => FindPrefab(unitId, 1);

    /// <summary>성급별 모델 조회. 합성(5.2절)으로 성급이 오른 유닛의 비주얼 교체에 사용.</summary>
    public UnitBase FindPrefab(string unitId, int starLevel)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].unitId == unitId)
            {
                return entries[i].GetPrefab(starLevel);
            }
        }

        return null;
    }
}
