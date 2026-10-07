using System;
using System.Collections.Generic;
using OZGL2.Grid.Prototype;
using UnityEngine;

namespace OZGL2.Progression
{
    [Serializable] public sealed class RunCellSave { public int x, y; }
    [Serializable] public sealed class RunExpansionSave { public string id, shapeId; public int x, y, rotation; }
    [Serializable] public sealed class RunBlockSave { public string id, contentId; public bool placed, mirrored; public int x, y, rotation; }
    [Serializable] public sealed class RunUnitSave { public string id, unitId; public int star = 1; public bool placed, mirrored; public int x, y, rotation; }

    /// <summary>진행 중인 도전 한 판의 저장본 — 몇 웨이브까지 깼는지, 마왕성 배치(바닥·발판·유닛·성급·보관함), 재화, 고른 증강.</summary>
    [Serializable]
    public sealed class RunSave
    {
        public string stageId;
        public int clearedRounds;
        public int currency;
        /// <summary>보상 단계에서 멈췄는가: 0 아님(배치 단계), 1 일반 보상(카드)을 아직 못 받음, 2 증강을 아직 못 고름.</summary>
        public int rewardStage;
        /// <summary>저장할 때의 배치 영역 크기(처음·최대). 지금 설정과 다르면 이어하지 않는다.</summary>
        public int initX, initY, maxX, maxY;
        public List<RunCellSave> floor = new List<RunCellSave>();
        public List<RunExpansionSave> expansions = new List<RunExpansionSave>();
        public List<RunBlockSave> blocks = new List<RunBlockSave>();
        public List<RunUnitSave> units = new List<RunUnitSave>();
        public List<string> augments = new List<string>();
    }

    /// <summary>
    /// 도전 중인 판을 스테이지(난이도)마다 한 칸씩 PlayerPrefs 에 저장한다. 전투 씬의 InGameRunSaver 가 웨이브를 깨거나 배치를 바꿀 때마다 갱신하므로
    /// 도중에 로비로 나가거나 게임을 꺼도 그 지점부터 이어진다. 패배·클리어로 판이 끝나면 지워지고, 「처음부터」는 SaveGame.StartNew 가 전부 지운다.
    /// 정식 세이브가 생기면 이 클래스만 교체하면 된다.
    /// </summary>
    public static class RunSaveStore
    {
        private const string Prefix = "OZGL2.RunSave.";

        /// <summary>저장본을 마지막으로 바꾼 시각(예: "10-07 21:30"). 없으면 빈 문자열.</summary>
        public static string SavedAt(string stageId) => string.IsNullOrEmpty(stageId) ? string.Empty : PlayerPrefs.GetString(Prefix + stageId + ".at", string.Empty);

        public static bool Has(string stageId) => !string.IsNullOrEmpty(stageId) && PlayerPrefs.HasKey(Prefix + stageId);

        public static RunSave Load(string stageId)
        {
            if (!Has(stageId)) return null;
            try { return JsonUtility.FromJson<RunSave>(PlayerPrefs.GetString(Prefix + stageId)); }
            catch (Exception) { return null; }
        }

        /// <summary>저장한다. 이전과 같은 내용이면 쓰지 않는다(true = 바뀌어서 저장함).</summary>
        public static bool Save(RunSave save)
        {
            if (save == null || string.IsNullOrEmpty(save.stageId)) return false;
            string json = JsonUtility.ToJson(save);
            string key = Prefix + save.stageId;
            if (PlayerPrefs.GetString(key, string.Empty) == json) { MarkCombat(save.stageId, false); return false; }
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.SetString(key + ".at", System.DateTime.Now.ToString("MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.DeleteKey(CombatKey(save.stageId));
            PlayerPrefs.Save();
            return true;
        }

        public static void Clear(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return;
            PlayerPrefs.DeleteKey(Prefix + stageId);
            PlayerPrefs.DeleteKey(Prefix + stageId + ".at");
            PlayerPrefs.DeleteKey(CombatKey(stageId));
            PlayerPrefs.Save();
        }

        // 전투 중에 나갔는지 기억하는 표시. 전투가 시작되면 켜고, 웨이브를 이겨 새로 저장하면 꺼진다.
        private static string CombatKey(string stageId) => Prefix + stageId + ".combat";
        public static bool WasInCombat(string stageId) => !string.IsNullOrEmpty(stageId) && PlayerPrefs.GetInt(CombatKey(stageId), 0) == 1;
        public static void MarkCombat(string stageId, bool inCombat)
        {
            if (string.IsNullOrEmpty(stageId) || WasInCombat(stageId) == inCombat) return;
            if (inCombat) PlayerPrefs.SetInt(CombatKey(stageId), 1); else PlayerPrefs.DeleteKey(CombatKey(stageId));
            PlayerPrefs.Save();
        }

        public static void ClearAll()
        {
            foreach (var id in StageClearStore.OrderedStageIds) { PlayerPrefs.DeleteKey(Prefix + id); PlayerPrefs.DeleteKey(Prefix + id + ".at"); PlayerPrefs.DeleteKey(CombatKey(id)); }
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// 이번 판이 저장된 판을 이어서 시작하는지 알려 주는 공유 상태. StageManager 가 판을 시작할 때 채우고,
    /// 그리드 세션·재화·증강·기록 쪽이 같은 값을 읽어 이어서 시작한다.
    /// </summary>
    public static class RunResume
    {
        /// <summary>이어하는 판의 저장본(새 판이면 null).</summary>
        public static RunSave Active { get; private set; }

        /// <summary>판이 새로 시작될 때마다 바뀌는 번호. 읽는 쪽이 "이번 판에 이미 반영했는가"를 가려내는 데 쓴다.</summary>
        public static int Token { get; private set; }

        /// <summary>저장본에서 이미 깬 것으로 건너뛴 웨이브 수(업적·기록이 이 웨이브를 다시 세지 않게 한다).</summary>
        public static int FastForwarded => Active != null ? Active.clearedRounds : 0;

        /// <summary>true 면 전투 중에 나간 판을 포기로 처리한다(기본 꺼짐: 전투 직전 배치부터 이어한다).</summary>
        public static bool ForfeitOnCombatExit { get; set; }

        /// <summary>그리드 카탈로그(유닛·발판 정의). 판을 시작하는 부트스트랩이 채운다.</summary>
        public static GridPrototypeCatalogSO Catalog { get; set; }

        /// <summary>이어할 저장본이 있으면 꺼내 활성으로 두고, 없으면 새 판으로 둔다. 1웨이브도 못 깬 저장이나 이미 다 깬 저장은 이어하지 않는다.</summary>
        public static RunSave Begin(string stageId, int totalRounds)
        {
            Token++;
            Active = null;
            // 전투 중에 나갔다면 그 판은 포기한 것으로 보고(전투를 되돌려 같은 웨이브를 다시 도전하는 것을 막는다) 새 판으로 시작한다
            if (RunSaveStore.WasInCombat(stageId))
            {
                if (ForfeitOnCombatExit) { RunSaveStore.Clear(stageId); return null; }
                RunSaveStore.MarkCombat(stageId, false); // 포기 규칙을 끈 상태에서는 남아 있던 표시를 지운다
            }
            var save = RunSaveStore.Load(stageId);
            if (save == null || save.clearedRounds < 1 || save.clearedRounds >= totalRounds) return null;
            // 배치 영역 크기가 바뀐 뒤의 옛 저장(칸 위치가 어긋남)은 버린다
            if (Catalog != null)
            {
                var def = Catalog.CreateDefinition();
                if (save.initX != def.InitialSize.x || save.initY != def.InitialSize.y || save.maxX != def.MaximumSize.x || save.maxY != def.MaximumSize.y)
                {
                    RunSaveStore.Clear(stageId);
                    return null;
                }
            }
            Active = save;
            return save;
        }
    }
}
