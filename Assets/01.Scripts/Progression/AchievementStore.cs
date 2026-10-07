using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 업적 진행도의 계정 영구 기록(PlayerPrefs). 로비 업적 화면의 카탈로그 id(AchievementCatalog.asset)와 1:1로 대응한다.
    /// 전투에서 쌓는 값(AchievementTracker)과 로비에서 읽어 가는 값(AchievementLobbySync)이 이 저장소를 거친다.
    /// 정식 세이브가 생기면 이 클래스만 교체하면 된다. 「처음부터」는 SaveGame.StartNew 가 ResetAll 로 비운다.
    /// </summary>
    public static class AchievementStore
    {
        private const string Prefix = "OZGL2.Ach.";

        // 카운터 이름(저장 키)
        public const string Wins = "wins";           // 승리한 전투(웨이브) 수
        public const string Battles = "battles";     // 끝낸 전투(웨이브) 수 — 승패 무관
        public const string Gates = "gates";         // 돌파한 관문(보스 웨이브) 수
        public const string Kills = "kills";         // 처치한 용사 수
        public const string Giants = "giants";       // 처치한 보스급 용사 수
        public const string Growth = "growth";       // 합성으로 성급을 올린 횟수
        public const string Tuning = "tuning";       // 특성·스킬로 힘을 키운 단계(지금까지의 최고치)
        public const string Slots = "slots";         // 스킬 장착 슬롯을 채운 개수(지금까지의 최고치)

        private static readonly string[] All = { Wins, Battles, Gates, Kills, Giants, Growth, Tuning, Slots };

        public static int Get(string counter) => PlayerPrefs.GetInt(Prefix + counter, 0);

        public static void Add(string counter, int amount = 1)
        {
            if (amount <= 0) return;
            PlayerPrefs.SetInt(Prefix + counter, Get(counter) + amount);
        }

        /// <summary>줄어들지 않는 값(최고치)을 갱신한다. 되돌려도 달성한 업적은 사라지지 않는다.</summary>
        public static bool RaiseTo(string counter, int value)
        {
            if (value <= Get(counter)) return false;
            PlayerPrefs.SetInt(Prefix + counter, value);
            return true;
        }

        public static void Save() => PlayerPrefs.Save();

        // 달성 팝업을 이미 보여 준 업적 id 목록(쉼표로 이어 한 줄로 저장)
        private const string NotifiedKey = Prefix + "notified";

        public static bool IsNotified(string achievementId) =>
            ("," + PlayerPrefs.GetString(NotifiedKey, string.Empty) + ",").Contains("," + achievementId + ",");

        public static void MarkNotified(string achievementId)
        {
            if (IsNotified(achievementId)) return;
            string list = PlayerPrefs.GetString(NotifiedKey, string.Empty);
            PlayerPrefs.SetString(NotifiedKey, string.IsNullOrEmpty(list) ? achievementId : list + "," + achievementId);
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            foreach (var counter in All) PlayerPrefs.DeleteKey(Prefix + counter);
            PlayerPrefs.DeleteKey(NotifiedKey);
            PlayerPrefs.Save();
        }

        /// <summary>카탈로그의 업적 id 에 해당하는 현재 진행도. 모르는 id 는 0.</summary>
        public static int ProgressOf(string achievementId)
        {
            switch (achievementId)
            {
                case "first_defense": return Get(Wins);
                case "first_gate": return Get(Gates);
                case "veteran": return Get(Battles);
                case "hero_hunter_1":
                case "hero_hunter_2": return Get(Kills);
                case "giant_fall": return Get(Giants);
                case "growth": return Get(Growth);
                case "tuning": return Get(Tuning);
                case "ready": return Get(Slots);
                default: return 0;
            }
        }
    }
}
