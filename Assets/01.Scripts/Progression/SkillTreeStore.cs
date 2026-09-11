using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Progression
{
    /// <summary>
    /// 스킬 트리의 계정 영구 상태 — SP(스킬 포인트) · 라운드 마일스톤 · 스킬별 봉인 해제 · 장착 목록.
    /// 프로토는 PlayerPrefs. 정식 세이브가 생기면 이 클래스만 교체하면 된다.
    ///
    /// SP 획득 = 라운드 마일스톤: R10 도달 → +1, R20 → +2, R30 → +3 … (누적).
    /// 보통(30R) 완주 = 6 SP / 어려움(50R) = 15 SP.
    /// </summary>
    public static class SkillTreeStore
    {
        private const string KeySp = "OZGL2.Skill.SP";
        private const string KeyMilestone = "OZGL2.Skill.Milestone";
        private const string KeyEquip = "OZGL2.Skill.Equip";
        private const string KeyUnlockPrefix = "OZGL2.Skill.Unlock.";

        public static int SkillPoints
        {
            get => PlayerPrefs.GetInt(KeySp, 0);
            set { PlayerPrefs.SetInt(KeySp, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>지금까지 도달한 최고 마일스톤 단계 (라운드/10).</summary>
        public static int HighestMilestone
        {
            get => PlayerPrefs.GetInt(KeyMilestone, 0);
            set { PlayerPrefs.SetInt(KeyMilestone, value); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// 라운드 도달 시 호출. 아직 안 받은 10단위 마일스톤마다 (단계 수 + perMilestoneBonus)만큼 SP 지급.
        /// perMilestoneBonus = 특성 "지배자의 지혜". 지급한 총 SP 반환.
        /// </summary>
        public static int GrantForRound(int round, int perMilestoneBonus = 0)
        {
            int stage = round / 10;
            int granted = 0;
            for (int k = HighestMilestone + 1; k <= stage; k++)
            {
                granted += k + Mathf.Max(0, perMilestoneBonus); // R10:+1, R20:+2, R30:+3 … (+보너스)
            }

            if (granted > 0)
            {
                SkillPoints += granted;
                HighestMilestone = stage;
            }

            return granted;
        }

        public static bool IsUnlocked(string skillId) => PlayerPrefs.GetInt(KeyUnlockPrefix + skillId, 0) == 1;

        public static void SetUnlocked(string skillId, bool value)
        {
            PlayerPrefs.SetInt(KeyUnlockPrefix + skillId, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>장착된 스킬 id 목록 (쉼표 구분).</summary>
        public static List<string> GetEquipped()
        {
            var raw = PlayerPrefs.GetString(KeyEquip, string.Empty);
            var list = new List<string>();
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (var part in raw.Split(','))
            {
                if (!string.IsNullOrEmpty(part)) list.Add(part);
            }
            return list;
        }

        public static void SetEquipped(IEnumerable<string> ids)
        {
            PlayerPrefs.SetString(KeyEquip, string.Join(",", ids));
            PlayerPrefs.Save();
        }

        /// <summary>전체 초기화 (샌드박스 디버그용). ids = 씬에 등장하는 모든 스킬 id.</summary>
        public static void Wipe(IEnumerable<string> ids)
        {
            PlayerPrefs.DeleteKey(KeySp);
            PlayerPrefs.DeleteKey(KeyMilestone);
            PlayerPrefs.DeleteKey(KeyEquip);
            foreach (var id in ids)
            {
                PlayerPrefs.DeleteKey(KeyUnlockPrefix + id);
            }
            PlayerPrefs.Save();
        }
    }
}
