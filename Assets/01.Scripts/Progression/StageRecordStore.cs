using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 난이도(스테이지)별 최고 기록 — 가장 멀리 깬 웨이브와, 끝까지 클리어했을 때의 가장 빠른 시간.
    /// 로비 난이도 카드에 마우스를 올리면 펼쳐지는 기록 패널이 이 값을 보여 준다(LobbyRecordBridge).
    /// 웨이브를 하나 깰 때마다 바로 저장하므로 도중에 로비로 나가거나 게임을 꺼도 그때까지의 기록이 남는다.
    /// 정식 세이브가 생기면 이 클래스만 교체하면 된다. 「처음부터」는 SaveGame.StartNew 가 ResetAll 로 비운다.
    /// </summary>
    public static class StageRecordStore
    {
        private const string Prefix = "OZGL2.Rec.";

        public static int BestWave(string stageId) => string.IsNullOrEmpty(stageId) ? 0 : PlayerPrefs.GetInt(Prefix + stageId + ".wave", 0);

        /// <summary>클리어한 적이 있으면 가장 빠른 클리어 시간(초), 없으면 null.</summary>
        public static float? BestClearSeconds(string stageId)
        {
            string key = Prefix + stageId + ".time";
            return !string.IsNullOrEmpty(stageId) && PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : (float?)null;
        }

        /// <summary>이번 도전의 결과를 올린다. 더 좋은 기록일 때만 바뀌고, 바뀌었으면 true.</summary>
        public static bool Submit(string stageId, int clearedWaves, bool cleared, float seconds)
        {
            if (string.IsNullOrEmpty(stageId)) return false;
            bool changed = false;
            if (clearedWaves > BestWave(stageId))
            {
                PlayerPrefs.SetInt(Prefix + stageId + ".wave", clearedWaves);
                changed = true;
            }
            if (cleared && seconds > 0f)
            {
                float? best = BestClearSeconds(stageId);
                if (!best.HasValue || seconds < best.Value)
                {
                    PlayerPrefs.SetFloat(Prefix + stageId + ".time", seconds);
                    changed = true;
                }
            }
            if (changed) PlayerPrefs.Save();
            return changed;
        }

        public static void ResetAll()
        {
            foreach (var id in StageClearStore.OrderedStageIds)
            {
                PlayerPrefs.DeleteKey(Prefix + id + ".wave");
                PlayerPrefs.DeleteKey(Prefix + id + ".time");
            }
            PlayerPrefs.Save();
        }
    }
}
