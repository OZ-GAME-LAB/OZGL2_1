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
        public UnitBase prefab;
    }

    public List<Entry> entries = new List<Entry>();

    public UnitBase FindPrefab(string unitId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].unitId == unitId)
            {
                return entries[i].prefab;
            }
        }

        return null;
    }
}
