using UnityEngine;

namespace OZGL2.Tutorial
{
    /// <summary>튜토리얼을 이미 본 기록(계정 저장). 처음 한 번만 자동으로 나오게 한다.</summary>
    public static class TutorialStore
    {
        private const string SeenPrefix = "OZGL2.Tutorial.Seen.";
        private const string KeyDisabled = "OZGL2.Tutorial.Disabled";

        public static bool Seen(string id) => PlayerPrefs.GetInt(SeenPrefix + id, 0) == 1;

        public static void MarkSeen(string id)
        {
            PlayerPrefs.SetInt(SeenPrefix + id, 1);
            PlayerPrefs.Save();
        }

        /// <summary>true면 자동 설명을 모두 끈다(도움말(?)에서 직접 보는 것은 가능).</summary>
        public static bool AutoDisabled
        {
            get => PlayerPrefs.GetInt(KeyDisabled, 0) == 1;
            set { PlayerPrefs.SetInt(KeyDisabled, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void ResetAll()
        {
            foreach (var s in TutorialLibrary.All) PlayerPrefs.DeleteKey(SeenPrefix + s.Id);
            PlayerPrefs.DeleteKey(KeyDisabled);
            PlayerPrefs.Save();
        }
    }
}
