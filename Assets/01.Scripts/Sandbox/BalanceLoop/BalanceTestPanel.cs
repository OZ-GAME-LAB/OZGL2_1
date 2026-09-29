using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using OZGL2.Augment;
using OZGL2.Contracts;
using OZGL2.InGame;
using OZGL2.Progression;
using OZGL2.Stage;
using OZGL2.Synergy;
using SandboxGameUI = OZGL2.Sandbox.SandboxGameUI;

namespace OZGL2.BalanceTest
{
    /// <summary>
    /// 밸런스 테스트 InGame 씬 전용 패널(개인 테스트 씬에만 배치, 팀 Builds/InGame 씬에는 없음). 화면 오른쪽에 붙는다 —
    /// 왼쪽 위는 팀 InGame 더미 패널이 쓰는 자리라 겹치지 않게 분리.
    ///  · 라운드가 준비 단계에 들어갈 때 시트의 그 라운드 용사 배율(HP·공격·공속·힐량)을 전투에 반영
    ///  · 마왕 레벨·XP·LP·SP, 시너지(직업 배치 수·발동 단계·조합), 찍은 특성, 고른 증강 표시
    ///  · 배속(일시정지·x1·x2·x4·x8), 용사/마왕군 실시간 생존 수·체력
    ///  · 라운드별 전투 시간과 결과를 화면 로그 + CSV(persistentDataPath/BalanceTest/rounds.csv)로 기록
    /// 실제 InGame 시스템(그리드·전투·보상·증강·결과 화면)은 전혀 건드리지 않고 읽기만 한다. F1 = 접기/펴기.
    /// IMGUI 규칙상 Layout/Repaint 사이에 컨트롤 개수가 바뀌면 안 되므로, 화면에 쓰는 목록은 Update에서 한 프레임에
    /// 한 번(0.25초마다) 스냅샷으로 만들어 두고 OnGUI는 그것만 읽는다.
    /// </summary>
    public sealed class BalanceTestPanel : MonoBehaviour
    {
        private const int MaxLogLines = 14;
        private const float PanelWidth = 340f;

        private InGamePrototypeBootstrap _bootstrap;
        private RealSynergySync _sync;
        private bool _visible = true;
        private float _timeScale = 1f;
        private string _appliedKey;
        private RoundScaling _applied;
        private bool _hasApplied;
        private eStageState _prevState = eStageState.IDLE;
        private float _combatStartTime;
        private int _combatRound;
        private string _combatStageId = string.Empty;
        private readonly List<string> _log = new List<string>();
        private string _csvPath;
        private Vector2 _scroll;

        // ── OnGUI가 읽는 스냅샷(Update에서만 갱신) ──
        private float _nextSnapshot;
        private bool _hasStage;
        private string _stageLine = string.Empty;
        private string _mawangLine = string.Empty, _pointLine = string.Empty;
        private string _synergyCounts = string.Empty;
        private readonly List<string> _synergyLines = new List<string>();
        private readonly List<string> _comboLines = new List<string>();
        private readonly List<string> _traitLines = new List<string>();
        private readonly List<string> _augmentLines = new List<string>();

        private void Awake()
        {
            RealSynergySync.KeepMawangLevelOnRun = true; // InGame 씬에서 바로 시작해도 레벨 유지 (Bootstrap.Start보다 먼저 실행됨)
            _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            _csvPath = Path.Combine(Application.persistentDataPath, "BalanceTest", "rounds.csv");
        }

        private void OnDestroy() => Time.timeScale = 1f; // 씬을 나갈 때 배속이 남지 않게

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) _visible = !_visible;
            Refresh();
            if (Time.unscaledTime >= _nextSnapshot)
            {
                BuildSnapshot();
                _nextSnapshot = Time.unscaledTime + 0.25f;
            }
        }

        /// <summary>이벤트 구독 대신 매 프레임 상태를 읽는다 — 부트스트랩 시작 순서에 상관없이 동작하고, 재도전으로
        /// 런이 새로 시작돼도(런 ID가 바뀜) 1라운드 배율을 다시 넣는다.</summary>
        private void Refresh()
        {
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage == null) return;

            eStageState state = stage.State;
            int round = stage.CurrentRoundNumber;
            string stageId = _bootstrap.SelectedStageId ?? string.Empty;
            string runId = _bootstrap.GridSession != null ? _bootstrap.GridSession.RunId : "-";

            bool playable = state == eStageState.PREPARATION || state == eStageState.COMBAT;
            string key = runId + "|" + stageId + "|" + round;
            if (playable && round > 0 && key != _appliedKey)
            {
                ApplyScaling(stageId, round);
                _appliedKey = key;
            }

            if (state == eStageState.COMBAT && _prevState != eStageState.COMBAT)
            {
                _combatStartTime = Time.time;
                _combatRound = round;
                _combatStageId = stageId;
            }
            else if (_prevState == eStageState.COMBAT && state != eStageState.COMBAT)
            {
                RecordRound(state);
            }
            _prevState = state;
        }

        private void ApplyScaling(string stageId, int round)
        {
            var sync = RealCombatBootstrap.EnsureInitialized();
            if (sync == null) return;
            if (BalanceRoundScaling.TryGet(stageId, round, out var row))
            {
                // 팀 스테이지 시스템(PooledStageBattle.ApplyRoundDifficultyMultiplier)이 이미 라운드별 HP·공격 배율을
                // 용사 스폰 때 곱해 준다 — 시트 값이 "최종 배율"이 되도록 그 값으로 나눠서 넣는다(이중 적용 방지).
                // 공속·힐량 배율은 팀 시스템에 없으므로 그대로 넣는다.
                GetTeamRoundMultipliers(stageId, round, out float teamHp, out float teamAtk);
                sync.SetHeroRoundScaling(row.Hp / teamHp, row.Atk / teamAtk, row.Spd, row.Heal);
                _applied = row;
                _hasApplied = true;
            }
            else
            {
                sync.SetHeroRoundScaling(1f, 1f, 1f, 1f);
                _hasApplied = false;
            }
        }

        /// <summary>이 라운드에 팀 스테이지 시스템이 실제로 곱하는 HP·공격 배율(RoundDefinition에 들어 있는 값). 못 읽으면 1.</summary>
        private float GetTeamHp(StageDefinition def, int round) => round - 1 < def.Rounds.Count ? Mathf.Max(0.01f, def.Rounds[round - 1].HpMultiplier) : 1f;
        private float GetTeamAtk(StageDefinition def, int round) => round - 1 < def.Rounds.Count ? Mathf.Max(0.01f, def.Rounds[round - 1].AttackMultiplier) : 1f;

        private void GetTeamRoundMultipliers(string stageId, int round, out float hp, out float atk)
        {
            hp = 1f; atk = 1f;
            try
            {
                var catalog = _bootstrap != null && _bootstrap.Config != null ? _bootstrap.Config.StageCatalog : null;
                if (catalog == null) return;
                var def = catalog.Resolve(stageId);
                hp = GetTeamHp(def, round);
                atk = GetTeamAtk(def, round);
            }
            catch (System.Exception e) { Debug.LogWarning("[BalanceTest] 팀 라운드 배율을 못 읽음(1로 처리): " + e.Message); }
        }

        private void RecordRound(eStageState endState)
        {
            float seconds = Time.time - _combatStartTime;
            string line = $"R{_combatRound}  {seconds:0.0}초  → {endState}";
            _log.Add(line);
            if (_log.Count > MaxLogLines) _log.RemoveAt(0);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_csvPath));
                bool isNew = !File.Exists(_csvPath);
                var sb = new StringBuilder();
                if (isNew) sb.AppendLine("time,stage,round,combat_seconds,end_state,hero_hp_mult,hero_atk_mult");
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                sb.AppendLine(string.Join(",", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), _combatStageId, _combatRound,
                    seconds.ToString("0.0", inv), endState, _applied.Hp.ToString("0.000", inv), _applied.Atk.ToString("0.000", inv)));
                File.AppendAllText(_csvPath, sb.ToString());
            }
            catch (System.Exception e) { Debug.LogWarning("[BalanceTest] CSV 기록 실패: " + e.Message); }
        }

        // ─────────────────────────────── 스냅샷(마왕·시너지·특성·증강)

        private static readonly SynergyJob[] Jobs = (SynergyJob[])System.Enum.GetValues(typeof(SynergyJob));

        private static string JobName(SynergyJob j) => j switch
        {
            SynergyJob.Warrior => "전사",
            SynergyJob.Shield => "방패병",
            SynergyJob.Archer => "궁수",
            SynergyJob.Mage => "마법사",
            SynergyJob.Rogue => "도적",
            _ => "힐러",
        };

        private static string BranchName(TraitBranch b) => b switch
        {
            TraitBranch.Monster => "몬스터",
            TraitBranch.Hero => "용사 약화",
            TraitBranch.Skill => "스킬",
            _ => "재화·성장",
        };

        private void BuildSnapshot()
        {
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            _hasStage = stage != null;
            _stageLine = stage == null ? string.Empty
                : $"{StageLabel(_bootstrap.SelectedStageId)}   R{stage.CurrentRoundNumber}/{stage.TotalRounds}   [{stage.State}]";

            var mawang = MawangXpBridge.Mawang;
            _mawangLine = mawang == null ? "마왕 정보 없음" : $"마왕 Lv {mawang.Level}   XP {mawang.Xp}/{mawang.XpToNext}";
            _pointLine = mawang == null ? string.Empty : $"레벨 포인트 LP {mawang.Points}   스킬 포인트 SP {SkillTreeStore.SkillPoints}";

            if (_sync == null) _sync = FindFirstObjectByType<RealSynergySync>();
            BuildSynergy(_sync != null ? _sync.Synergy : null);
            BuildTraits(_sync != null ? _sync.Traits : null);
            BuildAugments(_sync != null ? _sync.Augments : null);
        }

        private void BuildSynergy(SynergyTracker tracker)
        {
            _synergyLines.Clear();
            _comboLines.Clear();
            if (tracker == null) { _synergyCounts = "시너지 정보 없음"; return; }

            _synergyCounts = string.Join("  ", Jobs.Select(j => $"{JobName(j)} {tracker.CountOf(j)}"));
            foreach (var d in tracker.Defs.OrderBy(d => (int)d.job))
            {
                int count = tracker.CountOf(d.job), tier = tracker.TierOf(d.job);
                if (tier <= 0) _synergyLines.Add($"· {d.displayName}  {count}/{d.tier1Threshold}  (미발동)");
                else _synergyLines.Add($"★ {d.displayName}  {count}명 · {tier}단계 — {(tier >= 2 ? d.tier2Desc : d.tier1Desc)}");
            }
            foreach (var c in tracker.ComboDefs)
                if (tracker.IsComboActive(c)) _comboLines.Add($"★ {c.displayName} — {c.desc}");
            if (_comboLines.Count == 0) _comboLines.Add("발동 중인 조합 시너지 없음");
        }

        private void BuildTraits(TraitTree tree)
        {
            _traitLines.Clear();
            if (tree == null) { _traitLines.Add("특성 정보 없음"); return; }

            var ranked = tree.Defs.Where(d => tree.RankOf(d.id) > 0).ToList();
            _traitLines.Add($"투자 {tree.AllocatedPoints} LP · 특성 {ranked.Count}개");
            foreach (var group in ranked.GroupBy(d => d.branch).OrderBy(g => (int)g.Key))
                _traitLines.Add($"[{BranchName(group.Key)}] " + string.Join(", ", group.OrderBy(d => (int)d.id).Select(d => $"{d.displayName} {tree.RankOf(d.id)}/{d.maxRank}")));
            if (ranked.Count == 0) _traitLines.Add("찍은 특성 없음");
        }

        private void BuildAugments(AugmentRun run)
        {
            _augmentLines.Clear();
            if (run == null) { _augmentLines.Add("증강 정보 없음"); return; }

            foreach (var a in run.PickedList)
            {
                int stack = run.StackOf(a);
                _augmentLines.Add($"· {a.displayName} ({AugmentData.TierName(a.tier)}){(stack > 1 ? " ×" + stack : string.Empty)} — {a.description}");
            }
            if (_augmentLines.Count == 0) _augmentLines.Add("아직 고른 증강 없음 (보스 라운드 R10마다 선택)");
        }

        // ─────────────────────────────── 화면

        private void OnGUI()
        {
            float x = Screen.width - PanelWidth - 8f;
            if (!_visible)
            {
                if (GUI.Button(new Rect(Screen.width - 98f, 8f, 90f, 24f), "밸런스(F1)")) _visible = true;
                return;
            }

            GUILayout.BeginArea(new Rect(x, 8f, PanelWidth, Screen.height - 16f), SandboxGameUI.Panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("밸런스 테스트", SandboxGameUI.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("접기", SandboxGameUI.SecondaryButton, GUILayout.Width(52), GUILayout.Height(24))) _visible = false;
            GUILayout.EndHorizontal();

            if (!_hasStage)
            {
                GUILayout.Label("InGame 시작 대기 중…", SandboxGameUI.Body);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label(_stageLine, SandboxGameUI.Subtitle);
            GUILayout.Label(_hasApplied
                ? $"용사 배율  HP x{_applied.Hp:0.00}  공격 x{_applied.Atk:0.00}  공속 x{_applied.Spd:0.00}  힐 x{_applied.Heal:0.00}"
                : "용사 배율  (시트 배율 없음 — x1)", SandboxGameUI.Body);

            _scroll = GUILayout.BeginScrollView(_scroll);

            Section("마왕");
            GUILayout.Label(_mawangLine, SandboxGameUI.Body);
            GUILayout.Label(_pointLine, SandboxGameUI.Body);

            Section("전투 현황");
            DrawLive();

            Section("배속");
            GUILayout.BeginHorizontal();
            DrawSpeedButton("II", 0f);
            DrawSpeedButton("x1", 1f);
            DrawSpeedButton("x2", 2f);
            DrawSpeedButton("x4", 4f);
            DrawSpeedButton("x8", 8f);
            GUILayout.EndHorizontal();

            Section("시너지 (직업별 배치 수)");
            GUILayout.Label(_synergyCounts, SandboxGameUI.Body);
            foreach (var line in _synergyLines) GUILayout.Label(line, SandboxGameUI.Body);
            foreach (var line in _comboLines) GUILayout.Label(line, SandboxGameUI.Body);

            Section("특성");
            foreach (var line in _traitLines) GUILayout.Label(line, SandboxGameUI.Body);

            Section("증강");
            foreach (var line in _augmentLines) GUILayout.Label(line, SandboxGameUI.Body);

            Section("라운드 기록 (CSV 자동 저장)");
            for (int i = _log.Count - 1; i >= 0; i--) GUILayout.Label(_log[i], SandboxGameUI.Body);
            if (_log.Count == 0) GUILayout.Label("아직 끝난 전투가 없어요.", SandboxGameUI.Body);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void Section(string title)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, SandboxGameUI.Subtitle);
        }

        private void DrawSpeedButton(string label, float scale)
        {
            bool selected = Mathf.Approximately(_timeScale, scale);
            bool prev = GUI.enabled;
            GUI.enabled = !selected;
            if (GUILayout.Button(label, SandboxGameUI.SecondaryButton, GUILayout.Height(28)))
            {
                _timeScale = scale;
                Time.timeScale = scale;
            }
            GUI.enabled = prev;
        }

        private static void DrawLive()
        {
            GetSideStats(UnitSide.Hero, out int heroAlive, out int heroTotal, out float heroHp, out float heroMaxHp);
            GetSideStats(UnitSide.DemonArmy, out int armyAlive, out int armyTotal, out float armyHp, out float armyMaxHp);
            GUILayout.Label($"용사    생존 {heroAlive}/{heroTotal}   체력 {heroHp:0}/{heroMaxHp:0}", SandboxGameUI.Body);
            GUILayout.Label($"마왕군  생존 {armyAlive}/{armyTotal}   체력 {armyHp:0}/{armyMaxHp:0}", SandboxGameUI.Body);
        }

        private static void GetSideStats(UnitSide side, out int alive, out int total, out float hp, out float maxHp)
        {
            alive = 0; total = 0; hp = 0f; maxHp = 0f;
            var units = UnitRegistry.GetUnits(side);
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || u.statData == null) continue;
                total++;
                maxHp += u.statData.maxHealth * CombatModifierHub.GetHpMult(u.statData.job, u.statData.side);
                if (u.currentState == UnitState.Dead) continue;
                alive++;
                hp += Mathf.Max(0, u.currentHealth);
            }
        }

        private static string StageLabel(string stageId) => stageId switch
        {
            "bal_normal_30" => "보통",
            "bal_hard_50" => "어려움",
            "bal_hell_100" => "헬",
            _ => string.IsNullOrEmpty(stageId) ? "?" : stageId,
        };
    }
}
