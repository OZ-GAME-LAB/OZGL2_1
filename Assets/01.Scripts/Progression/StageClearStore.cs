using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 스테이지 클리어 기록과 난이도 잠금. 보통을 깨야 어려움이, 어려움을 깨야 지옥이 열린다.
    /// 로비 난이도 카드 순서(보통 → 어려움 → 지옥)가 곧 OrderedStageIds 의 순서다.
    /// 디버그 열기(DebugUnlockAll)를 켜면 클리어하지 않아도 전부 열린다(로비 F8 패널에서 켜고 끈다).
    /// 정식 세이브로 바뀌면 이 클래스만 교체하면 된다. 처음부터(SaveGame.StartNew)할 때 같이 지운다.
    /// </summary>
    public static class StageClearStore
    {
        /// <summary>로비 난이도 카드 순서(보통, 어려움, 지옥)에 대응하는 스테이지 ID.</summary>
        public static readonly string[] OrderedStageIds = { "stage_normal_30", "stage_hard_50", "bal_hell_100" };
        public static readonly string[] DifficultyNames = { "보통", "어려움", "지옥" };

        private const string Prefix = "OZGL2.Clear.";
        private const string DebugKey = "OZGL2.Debug.UnlockAllDifficulty";

        public static bool DebugUnlockAll
        {
            get => PlayerPrefs.GetInt(DebugKey, 0) == 1;
            set { PlayerPrefs.SetInt(DebugKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool IsCleared(string stageId) => !string.IsNullOrEmpty(stageId) && PlayerPrefs.GetInt(Prefix + stageId, 0) == 1;

        public static void MarkCleared(string stageId)
        {
            if (string.IsNullOrEmpty(stageId) || IsCleared(stageId)) return;
            PlayerPrefs.SetInt(Prefix + stageId, 1);
            PlayerPrefs.Save();
        }

        public static bool AnyCleared
        {
            get { foreach (var id in OrderedStageIds) if (IsCleared(id)) return true; return false; }
        }

        public static string StageIdOf(int difficultyIndex) =>
            difficultyIndex >= 0 && difficultyIndex < OrderedStageIds.Length ? OrderedStageIds[difficultyIndex] : null;

        /// <summary>난이도 카드 번호(0 보통, 1 어려움, 2 지옥)가 열려 있는가. 첫 난이도는 항상 열려 있다.</summary>
        public static bool IsDifficultyUnlocked(int difficultyIndex)
        {
            if (difficultyIndex <= 0) return true;
            if (DebugUnlockAll) return true;
            return IsCleared(StageIdOf(difficultyIndex - 1));
        }

        /// <summary>잠긴 난이도에 보여 줄 안내(예: "보통을 클리어하면 열립니다").</summary>
        public static string LockedHint(int difficultyIndex) =>
            difficultyIndex > 0 && difficultyIndex < DifficultyNames.Length ? DifficultyNames[difficultyIndex - 1] + "을(를) 클리어하면 열립니다" : string.Empty;

        public static void ResetAll()
        {
            foreach (var id in OrderedStageIds) PlayerPrefs.DeleteKey(Prefix + id);
            PlayerPrefs.Save();
        }
    }
}
