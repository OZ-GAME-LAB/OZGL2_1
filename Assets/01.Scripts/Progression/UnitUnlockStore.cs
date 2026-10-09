using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 마왕군 유닛 해금 — 처음에는 기본 3종(전사·방패병·궁수)만 카드 보상 후보에 나오고, 정해진 라운드를 클리어하면 나머지가 하나씩 영구 해금된다.
    /// 해금한 유닛은 계정에 저장되어(PlayerPrefs) 다음 판에도 유지되고, 이후 카드 3장 중 1장 보상 후보에 섞여 나온다.
    ///
    /// 해금 일정(30라운드 기준, 5라운드마다 1종):
    ///   라운드 5 클리어 → 마법사 (원거리 조합 시너지 시작)
    ///   라운드 10 클리어 → 힐러 (지속력 — 마법사와 결속 조합)
    ///   라운드 15 클리어 → 도적 (후반 화력)
    /// 일정을 바꾸려면 아래 Schedule 표만 고치면 된다.
    /// </summary>
    public static class UnitUnlockStore
    {
        public readonly struct Entry
        {
            public readonly int Round;
            public readonly string UnitId;
            public Entry(int round, string unitId) { Round = round; UnitId = unitId; }
        }

        /// <summary>처음부터 쓸 수 있는 기본 유닛.</summary>
        public static readonly string[] BaseUnlocked = { "M_WAR_01", "M_SHD_01", "M_ARC_01" };

        /// <summary>(해금되는 라운드, 유닛 id).</summary>
        public static readonly Entry[] Schedule =
        {
            new Entry(5, "M_MAG_01"),
            new Entry(10, "M_HEL_01"),
            new Entry(15, "M_ROG_01"),
        };

        private const string KeyPrefix = "OZGL2.Unit.Unlock.";

        public static bool IsUnlocked(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return false;
            foreach (var id in BaseUnlocked) if (id == unitId) return true;
            // 일정에 없는 유닛(새로 추가된 것 등)은 막지 않는다
            bool scheduled = false;
            foreach (var e in Schedule) if (e.UnitId == unitId) { scheduled = true; break; }
            if (!scheduled) return true;
            return PlayerPrefs.GetInt(KeyPrefix + unitId, 0) == 1;
        }

        /// <summary>이번 판에서 clearedRounds만큼 클리어한 시점에 이미 해금됐다고 볼 수 있는가(저장된 해금 + 일정상 이번에 해금되는 것).</summary>
        public static bool IsUnlockedAtRound(string unitId, int clearedRounds)
        {
            if (IsUnlocked(unitId)) return true;
            foreach (var e in Schedule) if (e.UnitId == unitId && e.Round <= clearedRounds) return true;
            return false;
        }

        /// <summary>clearedRounds 클리어 기준으로 새로 해금되는 유닛을 저장하고 id 목록을 돌려준다(이미 해금된 것은 제외).</summary>
        public static List<string> UnlockForRound(int clearedRounds)
        {
            var result = new List<string>();
            foreach (var e in Schedule)
            {
                if (e.Round > clearedRounds || PlayerPrefs.GetInt(KeyPrefix + e.UnitId, 0) == 1) continue;
                PlayerPrefs.SetInt(KeyPrefix + e.UnitId, 1);
                result.Add(e.UnitId);
            }
            if (result.Count > 0) PlayerPrefs.Save();
            return result;
        }

        /// <summary>이 라운드를 클리어하면 새로 해금될 유닛 id. 없으면 null.</summary>
        public static string PreviewForRound(int round)
        {
            foreach (var e in Schedule)
                if (e.Round <= round && PlayerPrefs.GetInt(KeyPrefix + e.UnitId, 0) != 1) return e.UnitId;
            return null;
        }

        /// <summary>저장된 해금 수 (보상 패널 갱신 판단용).</summary>
        public static int UnlockedCount
        {
            get
            {
                int n = 0;
                foreach (var e in Schedule) if (PlayerPrefs.GetInt(KeyPrefix + e.UnitId, 0) == 1) n++;
                return n;
            }
        }

        /// <summary>디버그용: 일정에 있는 유닛(마법사·힐러·도적)의 해금을 켜거나 끈다. 기본 유닛은 항상 해금이라 바꾸지 않는다.</summary>
        public static void SetUnlocked(string unitId, bool unlocked)
        {
            foreach (var e in Schedule)
                if (e.UnitId == unitId)
                {
                    if (unlocked) PlayerPrefs.SetInt(KeyPrefix + unitId, 1); else PlayerPrefs.DeleteKey(KeyPrefix + unitId);
                    PlayerPrefs.Save();
                    return;
                }
        }

        public static void UnlockAll()
        {
            foreach (var e in Schedule) PlayerPrefs.SetInt(KeyPrefix + e.UnitId, 1);
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            foreach (var e in Schedule) PlayerPrefs.DeleteKey(KeyPrefix + e.UnitId);
            PlayerPrefs.Save();
        }

        public static string DisplayName(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return "새 유닛";
            if (unitId.StartsWith("M_WAR")) return "전사";
            if (unitId.StartsWith("M_SHD")) return "방패병";
            if (unitId.StartsWith("M_ARC")) return "궁수";
            if (unitId.StartsWith("M_MAG")) return "마법사";
            if (unitId.StartsWith("M_ROG")) return "도적";
            if (unitId.StartsWith("M_HEL")) return "힐러";
            return unitId;
        }
    }
}
