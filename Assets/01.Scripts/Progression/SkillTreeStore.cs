using System;
using System.Collections.Generic;
using OZGL2.Skill;
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
        private const string KeyDrip = "OZGL2.Skill.Drip";
        private const string KeyEquip = "OZGL2.Skill.Equip";
        private const string KeyUnlockPrefix = "OZGL2.Skill.Unlock.";
        public static event Action Changed;

        public static int SkillPoints
        {
            get => PlayerPrefs.GetInt(KeySp, 0);
            set { PlayerPrefs.SetInt(KeySp, Mathf.Max(0, value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        /// <summary>지금까지 도달한 최고 마일스톤 단계 (라운드/10).</summary>
        public static int HighestMilestone
        {
            get => PlayerPrefs.GetInt(KeyMilestone, 0);
            set { PlayerPrefs.SetInt(KeyMilestone, value); PlayerPrefs.Save(); }
        }

        /// <summary>SP를 잘게 나눠 주는 간격(라운드). 이 라운드를 클리어할 때마다 +1.</summary>
        public const int DripEveryRounds = 2;

        /// <summary>지금까지 받은 "잘게 나눠 주는 SP" 단계 수 (라운드 / DripEveryRounds).</summary>
        public static int HighestDripStep
        {
            get => PlayerPrefs.GetInt(KeyDrip, 0);
            set { PlayerPrefs.SetInt(KeyDrip, value); PlayerPrefs.Save(); }
        }

        /// <summary>지금 이 라운드를 클리어하면 받게 될 SP — 잘게 나눠 주는 몫(2라운드마다 +1) + 10단위 마일스톤 몫(단계 수 + 보너스).</summary>
        public static int PreviewForRound(int round, int perMilestoneBonus = 0)
        {
            int granted = 0;
            for (int k = HighestDripStep + 1; k <= round / DripEveryRounds; k++) granted += 1;
            for (int k = HighestMilestone + 1; k <= round / 10; k++) granted += k + Mathf.Max(0, perMilestoneBonus);
            return granted;
        }

        /// <summary>
        /// 라운드 클리어 시 호출. 아직 안 받은 몫을 지급한다: ① DripEveryRounds(2)라운드마다 +1 ② 10단위 마일스톤마다 (단계 수 + perMilestoneBonus).
        /// perMilestoneBonus = 특성 "지배자의 지혜". 같은 몫을 재도전으로 다시 받지는 않는다. 지급한 총 SP 반환.
        /// </summary>
        public static int GrantForRound(int round, int perMilestoneBonus = 0)
        {
            int granted = PreviewForRound(round, perMilestoneBonus);
            if (granted > 0)
            {
                SkillPoints += granted;
                HighestDripStep = Mathf.Max(HighestDripStep, round / DripEveryRounds);
                HighestMilestone = Mathf.Max(HighestMilestone, round / 10);
            }
            return granted;
        }

        public static bool IsUnlocked(string skillId) => PlayerPrefs.GetInt(KeyUnlockPrefix + skillId, 0) == 1;

        public static void SetUnlocked(string skillId, bool value)
        {
            PlayerPrefs.SetInt(KeyUnlockPrefix + skillId, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 확인창의 표시값을 신뢰하지 않고 확정 시 실제 스킬 비용·해금 상태·잔액을 재검사한다.
        // 차감과 해금을 함께 기록한 뒤 한 번만 저장하므로 중복 확인도 추가 SP를 쓰지 않는다.
        public static bool TryUnlock(SkillData data, out string reason)
        {
            reason = string.Empty;
            if (data == null || string.IsNullOrWhiteSpace(data.skillId))
            {
                reason = "현재 해금할 수 없는 스킬입니다.";
                return false;
            }
            if (data.displayName == "화염구" || IsUnlocked(data.skillId))
            {
                reason = "이미 잠금 해제된 스킬입니다.";
                return false;
            }
            int cost = new SkillRuntime(data).UnlockCost;
            int balance = SkillPoints;
            if (balance < cost)
            {
                reason = "보유 SP가 부족합니다.";
                return false;
            }
            PlayerPrefs.SetInt(KeySp, balance - cost);
            PlayerPrefs.SetInt(KeyUnlockPrefix + data.skillId, 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>스킬 세팅에서 한 번이라도 장착을 저장했는가. 저장한 적이 없는 신규 계정만 기본 스킬(화염구)을 자동 장착한다.</summary>
        public static bool HasSavedEquipment => PlayerPrefs.HasKey(KeyEquip);

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
            Changed?.Invoke();
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
            Changed?.Invoke();
        }
    }
}
