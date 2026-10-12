using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 오프닝 스토리 컷신을 봤는지 기억한다(계정 저장, PlayerPrefs). 처음 시작할 때 한 번 나오고, 「처음부터」(SaveGame.StartNew)를 하면 다시 나온다.
    /// 디버그 패널의 「스토리 다시 보기」로도 초기화할 수 있다.
    /// </summary>
    public static class StoryStore
    {
        private const string Key = "OZGL2.Story.IntroSeen";

        public static bool Seen => PlayerPrefs.GetInt(Key, 0) == 1;

        public static void MarkSeen()
        {
            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
