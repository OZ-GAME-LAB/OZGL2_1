using System;
using System.Globalization;
using System.Linq;
using OZGL2.Skill;
using OZGL2.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.Progression
{
    /// <summary>
    /// 저장 파일의 "겉"을 맡는 얇은 층 — 이어하기가 가능한지 알려 주고(HasSave), 처음부터 시작할 때 모든 진행을 지운다(StartNew).
    /// 실제 진행 상태(마왕 레벨·LP, 특성 랭크, 스킬 SP·해금·장착, 마왕군 해금, 튜토리얼 본 기록)는
    /// 각 저장소(MawangLevel · TraitTree · SkillTreeStore · UnitUnlockStore · TutorialStore)가 PlayerPrefs 에 자동 저장하고,
    /// 이 클래스는 그 저장소들을 모아서 지우거나 요약만 한다. 정식 세이브로 바뀌어도 저장소만 교체하면 된다.
    ///
    /// 이어하기 판정: 시작만 눌러 본 것으로는 저장이 생기지 않는다. 한 번이라도 진행(경험치·포인트·특성·스킬·해금)이 쌓여야
    /// 다음 실행부터 타이틀이 「이어하기」로 바뀌고, 그 전까지는 「시작하기」다.
    /// </summary>
    public static class SaveGame
    {
        private const string KeySavedAt = "OZGL2.Save.SavedAt";
        private static bool _leftTitle;

        /// <summary>이어할 진행이 있는가(= 타이틀에서 「이어하기」를 보여 줄지).</summary>
        public static bool HasSave
        {
            get
            {
                var mawang = MawangXpBridge.Mawang;
                if (mawang != null && (mawang.Level > 1 || mawang.Xp > 0 || mawang.Points > 0)) return true;
                if (SkillTreeStore.SkillPoints > 0 || SkillTreeStore.HighestMilestone > 0 || SkillTreeStore.HighestDripStep > 0
                    || SkillTreeStore.HasSavedEquipment || UnitUnlockStore.UnlockedCount > 0) return true;
                if (Resources.LoadAll<SkillData>("Skills").Any(s => s != null && SkillTreeStore.IsUnlocked(s.skillId))) return true;
                return new TraitTree(Resources.LoadAll<TraitData>("Traits")).AllocatedPoints > 0;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            _leftTitle = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        // 타이틀을 벗어나 실제로 플레이한 뒤에만 "마지막 접속"을 갱신한다(타이틀만 켰다 끈 것은 기록하지 않는다)
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.StartsWith("Title")) return;
            _leftTitle = true;
            Touch();
        }

        private static void OnQuitting()
        {
            if (_leftTitle) Touch();
        }

        /// <summary>진행이 있으면 마지막 접속 시각을 지금으로 남긴다.</summary>
        public static void Touch()
        {
            if (!HasSave) return;
            PlayerPrefs.SetString(KeySavedAt, DateTime.Now.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        /// <summary>이어하기 아래에 보여 줄 한 줄 요약.</summary>
        public static string Summary()
        {
            var mawang = MawangXpBridge.Mawang;
            string line = (mawang != null ? "마왕 Lv " + mawang.Level + "  ·  LP " + mawang.Points : "마왕 Lv 1")
                          + "  ·  SP " + SkillTreeStore.SkillPoints;
            string when = PlayerPrefs.GetString(KeySavedAt, string.Empty);
            return string.IsNullOrEmpty(when) ? line : line + "  ·  마지막 접속 " + when;
        }

        /// <summary>처음부터 — 저장된 진행을 전부 지운다(튜토리얼도 처음부터 다시 나온다).</summary>
        public static void StartNew()
        {
            // 이미 메모리에 올라온 마왕 레벨은 직접 비워야 한다. 키만 지우면 다음 저장 때 옛 값이 되살아난다.
            var live = MawangXpBridge.Mawang;
            if (live != null) live.ClearSaved();
            else new MawangLevel().ClearSaved();

            SkillTreeStore.Wipe(Resources.LoadAll<SkillData>("Skills").Where(s => s != null).Select(s => s.skillId));
            new TraitTree(Resources.LoadAll<TraitData>("Traits")).ResetAll();
            UnitUnlockStore.ResetAll();
            TutorialStore.ResetAll();

            PlayerPrefs.DeleteKey(KeySavedAt);
            PlayerPrefs.Save();
        }
    }
}
