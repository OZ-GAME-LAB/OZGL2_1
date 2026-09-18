using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OZGL2.Stage.EditorTools
{
    /// <summary>
    /// 스테이지 스폰 데이터(보통 30R / 어려움 50R) 생성 — 성민 파트 밸런스 담당.
    /// StageDataSO/RoundData/HeroSpawnData는 팀 공용 데이터 구조(코어루프)이고,
    /// 여기서는 그 구조에 들어갈 실제 밸런스 값(직업 도입 순서·물량·보스/증강 라운드)만 채운다.
    /// private [SerializeField] 필드라 SerializedObject로 직접 써넣는다. 재실행하면 기존 것 덮어씀.
    /// </summary>
    public static class StageSpawnDataSetup
    {
        private const string Dir = "Assets/03.ScriptableObjects/Stage/Balance";

        private enum AugTier { Silver, Gold, Platinum }

        // 도입 순서: 전사 → 방패병 → 도적 → 궁수 → (힐러, 어려움만) → 마법사
        private static readonly string[] JobOrderNormal = { "H_WAR_01", "H_SHD_01", "H_ROG_01", "H_ARC_01", "H_MAG_01" };
        private static readonly int[] UnlockRoundNormal = { 1, 6, 11, 16, 21 };

        private static readonly string[] JobOrderHard = { "H_WAR_01", "H_SHD_01", "H_ROG_01", "H_ARC_01", "H_HEL_01", "H_MAG_01" };
        private static readonly int[] UnlockRoundHard = { 1, 6, 11, 16, 21, 26 };

        [MenuItem("OZGL2/Stage/Create Balance Stage Data (보통 30 / 어려움 50)")]
        public static void CreateAll()
        {
            Directory.CreateDirectory(Dir);

            // 그리드는 R1을 항상 기본 유닛 1기로 시작시킨다(코어루프 고정 규칙) — 그래서 R1만 반드시
            // 1기로 이길 수 있는 수준. R2부터는 물량을 크게 늘리고(우르르 몰려오는 느낌), 대신 스폰 시점에
            // StageBalanceSandbox가 인원수만큼 1인당 스탯을 깎아서(_referenceAttackerCount 기준) 총 난이도는
            // 비슷하게 맞춘다 — 물량↑ 개별 세기↓ 트레이드오프.
            // 증강 3회(R10·20·30) — 실버/골드/플래티넘 순
            BuildStage("stage_normal_30", 30, JobOrderNormal, UnlockRoundNormal,
                baseCount: 1, perRound: 1.2f,
                bossRounds: new[] { 10, 20, 30 },
                bossTier: new[] { AugTier.Silver, AugTier.Gold, AugTier.Platinum },
                assetName: "StageNormal30");

            // 증강 5회(R10·20·30·40·50) — 실버·실버·골드·골드·플래티넘
            BuildStage("stage_hard_50", 50, JobOrderHard, UnlockRoundHard,
                baseCount: 1, perRound: 1.5f,
                bossRounds: new[] { 10, 20, 30, 40, 50 },
                bossTier: new[] { AugTier.Silver, AugTier.Silver, AugTier.Gold, AugTier.Gold, AugTier.Platinum },
                assetName: "StageHard50");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OZGL2] 스테이지 밸런스 데이터 생성 완료 → {Dir}");
        }

        private static void BuildStage(string stageId, int roundCount, string[] jobOrder, int[] unlockRound,
            int baseCount, float perRound, int[] bossRounds, AugTier[] bossTier, string assetName)
        {
            // 기존 에셋이 있으면 그 자리에서 값만 갈아끼운다(GUID 유지) — 삭제 후 재생성하면 GUID가
            // 바뀌어서 이미 이 에셋을 참조해둔 InGameConfig_* 사본들의 연결이 끊어진다.
            string path = $"{Dir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageDataSO>(path);
            var so = existing != null ? existing : ScriptableObject.CreateInstance<StageDataSO>();
            var serialized = new SerializedObject(so);
            serialized.FindProperty("_stageId").stringValue = stageId;

            var roundsProp = serialized.FindProperty("_rounds");
            roundsProp.ClearArray();

            for (int r = 1; r <= roundCount; r++)
            {
                roundsProp.InsertArrayElementAtIndex(r - 1);
                var roundEl = roundsProp.GetArrayElementAtIndex(r - 1);
                roundEl.FindPropertyRelative("_roundId").stringValue = $"{stageId}_round_{r:00}";

                int unlockedCount = 1;
                for (int i = 1; i < unlockRound.Length; i++)
                {
                    if (r >= unlockRound[i]) unlockedCount++;
                }
                var activeJobs = jobOrder.Take(unlockedCount).ToArray();

                bool isBoss = bossRounds.Contains(r);
                int totalCount = r == 1 ? 1 : Mathf.RoundToInt(baseCount + r * perRound); // R1은 반드시 기본 유닛 1기로 이길 수 있어야 함
                if (isBoss) totalCount = Mathf.RoundToInt(totalCount * 1.5f);

                var spawnsProp = roundEl.FindPropertyRelative("_spawns");
                spawnsProp.ClearArray();
                for (int i = 0; i < activeJobs.Length; i++)
                {
                    int share = totalCount / activeJobs.Length + (i < totalCount % activeJobs.Length ? 1 : 0);
                    if (share <= 0) continue;
                    spawnsProp.InsertArrayElementAtIndex(spawnsProp.arraySize);
                    var spawnEl = spawnsProp.GetArrayElementAtIndex(spawnsProp.arraySize - 1);
                    spawnEl.FindPropertyRelative("_heroId").stringValue = activeJobs[i];
                    spawnEl.FindPropertyRelative("_count").intValue = share;
                    spawnEl.FindPropertyRelative("_intervalSeconds").floatValue = 0.5f;
                }

                roundEl.FindPropertyRelative("_isBossRound").boolValue = isBoss;
                roundEl.FindPropertyRelative("_rewardId").stringValue = "reward_dummy_general";

                int bossIndex = System.Array.IndexOf(bossRounds, r);
                AugTier tier = bossIndex >= 0 ? bossTier[bossIndex] : AugTier.Silver;
                roundEl.FindPropertyRelative("_silverWeight").intValue = tier == AugTier.Silver ? 100 : 0;
                roundEl.FindPropertyRelative("_goldWeight").intValue = tier == AugTier.Gold ? 100 : 0;
                roundEl.FindPropertyRelative("_platinumWeight").intValue = tier == AugTier.Platinum ? 100 : 0;
            }

            serialized.ApplyModifiedProperties();

            if (existing == null) AssetDatabase.CreateAsset(so, path);
            else EditorUtility.SetDirty(so);
        }
    }
}
