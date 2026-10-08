using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 로비 도감(마왕군·용사)이 열리는 조건을 한곳에서 알려 준다.
    /// - 마왕군: 카드 보상으로 나올 수 있게 해금되면(UnitUnlockStore) 도감도 열린다.
    ///   처음에는 기본 3종(전사·방패병·궁수), 5·10·15웨이브를 클리어하면 마법사·힐러·도적이 하나씩 열린다.
    /// - 용사: 전투에서 그 용사를 처음 만나면(그 웨이브의 전투가 시작되면) 계정에 기록되고 도감이 열린다.
    /// 도감 화면(UILobbyCollectionState)에는 CodexLobbySync 가 이 값을 밀어 넣는다. 저장은 PlayerPrefs, 「처음부터」(SaveGame.StartNew)가 지운다.
    /// </summary>
    public static class CodexStore
    {
        /// <summary>도감에 실린 마왕군 유닛 id(카탈로그 항목은 앞에 "unit." 이 붙는다).</summary>
        public static readonly string[] DemonIds = { "M_WAR_01", "M_ARC_01", "M_MAG_01", "M_SHD_01", "M_HEL_01", "M_ROG_01" };

        /// <summary>도감에 실린 용사 id. 보스는 도감에 없다.</summary>
        public static readonly string[] HeroIds = { "H_WAR_01", "H_ARC_01", "H_MAG_01", "H_SHD_01", "H_HEL_01", "H_ROG_01" };

        private const string HeroKey = "OZGL2.Codex.Hero.";
        private const string EntryPrefix = "unit.";

        public static bool HasSeenHero(string heroId) => !string.IsNullOrEmpty(heroId) && PlayerPrefs.GetInt(HeroKey + heroId, 0) == 1;

        /// <summary>용사를 만났다고 기록한다. 도감에 실린 용사이고 처음이면 true.</summary>
        public static bool MarkHeroSeen(string heroId)
        {
            if (System.Array.IndexOf(HeroIds, heroId) < 0 || HasSeenHero(heroId)) return false;
            PlayerPrefs.SetInt(HeroKey + heroId, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static void SetHeroSeen(string heroId, bool seen)
        {
            if (System.Array.IndexOf(HeroIds, heroId) < 0) return;
            if (seen) PlayerPrefs.SetInt(HeroKey + heroId, 1); else PlayerPrefs.DeleteKey(HeroKey + heroId);
            PlayerPrefs.Save();
        }

        public static int SeenHeroCount
        {
            get
            {
                int n = 0;
                foreach (var id in HeroIds) if (HasSeenHero(id)) n++;
                return n;
            }
        }

        public static void MarkAllHeroes() { foreach (var id in HeroIds) PlayerPrefs.SetInt(HeroKey + id, 1); PlayerPrefs.Save(); }

        public static void ResetAll()
        {
            foreach (var id in HeroIds) PlayerPrefs.DeleteKey(HeroKey + id);
            PlayerPrefs.Save();
        }

        /// <summary>도감 카탈로그 항목 id(예: "unit.M_WAR_01")가 지금 열려야 하는지. 모르는 종류면 false 를 돌려 판단을 맡긴다.</summary>
        public static bool TryIsEntryUnlocked(string entryId, out bool unlocked)
        {
            unlocked = false;
            if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(EntryPrefix)) return false;
            string id = entryId.Substring(EntryPrefix.Length);
            if (id.StartsWith("M_")) { unlocked = UnitUnlockStore.IsUnlocked(id); return true; }
            if (id.StartsWith("H_")) { unlocked = HasSeenHero(id); return true; }
            return false;
        }

        public static string DisplayName(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return "유닛";
            if (unitId.StartsWith("M_")) return UnitUnlockStore.DisplayName(unitId);
            string job = unitId.StartsWith("H_WAR") ? "전사" : unitId.StartsWith("H_ARC") ? "궁수" : unitId.StartsWith("H_MAG") ? "마법사"
                : unitId.StartsWith("H_SHD") ? "방패병" : unitId.StartsWith("H_HEL") ? "힐러" : unitId.StartsWith("H_ROG") ? "도적" : unitId;
            return unitId.StartsWith("H_") ? "용사 " + job : unitId;
        }
    }
}
