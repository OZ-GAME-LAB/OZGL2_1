using System.Collections.Generic;
using System.Linq;
using System.Text;
using OZGL2.Progression;
using OZGL2.Skill;
using OZGL2.Synergy;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비 UI ↔ 실제 시스템 연동 확인용 디버그 패널 (에디터·개발 빌드 전용, F8로 접기/펴기).
    /// 마왕 레벨·LP·SP·스킬 해금/장착을 직접 바꿔 보고, 화면(UI)과 저장값이 일치하는지 한눈에 확인한다.
    /// 값을 바꾼 뒤 특성/스킬 화면은 다시 열면 반영된다(레벨 HUD는 즉시).
    /// IMGUI라 컨트롤 개수가 Layout/Repaint에서 같아야 한다 — 목록 길이가 바뀌는 값은 Update에서 스냅샷으로만 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyDebugPanel : MonoBehaviour
    {
        private const float Width = 340f;
        private const float RefreshInterval = 0.25f;

        private bool _open; // 기본은 숨김 — 화면에는 아무것도 그리지 않고, F8을 눌렀을 때만 열린다
        private Vector2 _scroll;
        private float _nextRefresh;

        private SkillData[] _skills;
        private TraitData[] _traitDefs;
        private readonly HashSet<string> _equipped = new HashSet<string>();
        private string _mawangLine = string.Empty;
        private string _traitText = string.Empty;
        private string _checkText = string.Empty;
        private string _message = string.Empty;

        private static MawangLevel Mawang => MawangXpBridge.Mawang;

        private void Awake()
        {
            _skills = Resources.LoadAll<SkillData>("Skills").Where(s => s != null)
                .OrderBy(s => s.tier).ThenBy(s => s.displayName).ToArray();
            _traitDefs = Resources.LoadAll<TraitData>("Traits");
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame) _open = !_open;
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Snapshot();
        }

        private TraitTree Traits()
        {
            var sync = FindFirstObjectByType<RealSynergySync>();
            return sync != null && sync.Traits != null ? sync.Traits : new TraitTree(_traitDefs);
        }

        private void Snapshot()
        {
            _equipped.Clear();
            foreach (var id in SkillTreeStore.GetEquipped()) _equipped.Add(id);

            var m = Mawang;
            _mawangLine = m == null ? "MawangLevel 없음" :
                "Lv " + m.Level + "   XP " + m.Xp + " / " + m.XpToNext + "   LP " + m.Points;

            var tree = Traits();
            var sb = new StringBuilder();
            foreach (var d in _traitDefs)
            {
                int rank = tree.RankOf(d.id);
                if (rank > 0) sb.Append(d.displayName).Append(' ').Append(rank).Append('/').Append(d.maxRank).Append("   ");
            }
            int slots = 3 + tree.BuildModifiers().ExtraSkillSlots;
            _traitText = (sb.Length == 0 ? "찍은 특성 없음" : sb.ToString()) +
                         "\n배분 LP " + tree.AllocatedPoints + "   장착 슬롯 용량 " + slots;

            _checkText = BuildChecks(tree);
        }

        private string BuildChecks(TraitTree tree)
        {
            var sb = new StringBuilder();
            var m = Mawang;

            var hud = FindFirstObjectByType<UILobbyLevelHudView>(FindObjectsInactive.Include);
            if (hud == null) sb.AppendLine("레벨 HUD: 씬에 없음");
            else if (m == null) sb.AppendLine("레벨 HUD: 마왕 없음");
            else
            {
                bool ok = hud.DisplayLevel == m.Level &&
                          Mathf.Abs(hud.Progress - UILobbyLevelHudView.CalculateProgress(m.Xp, m.XpToNext)) < 0.01f;
                sb.AppendLine("레벨 HUD 일치: " + Mark(ok) + "  (화면 Lv " + hud.DisplayLevel + ")");
            }

            var traitUi = FindFirstObjectByType<UITraitProgressionController>(FindObjectsInactive.Include);
            if (traitUi == null) sb.AppendLine("특성 UI: 씬에 없음");
            else if (traitUi.Tree == null) sb.AppendLine("특성 UI: 화면을 한 번 열어야 확인됨");
            else
            {
                bool sameAccount = traitUi.Account == m;
                bool sameTree = traitUi.Tree == tree;
                sb.AppendLine("특성 UI 마왕 모델 공유: " + Mark(sameAccount));
                sb.AppendLine("특성 UI 트리 공유: " + Mark(sameTree) + (FindFirstObjectByType<RealSynergySync>() == null ? " (전투 시스템 없음 → 로비 단독 트리)" : ""));
            }

            var loadout = FindFirstObjectByType<LobbySkillLoadoutBinder>(FindObjectsInactive.Include);
            if (loadout == null || !loadout.IsReady) sb.AppendLine("스킬창: 화면을 한 번 열어야 확인됨");
            else
            {
                var screen = loadout.ScreenEquippedRealIds();
                var stored = new HashSet<string>(SkillTreeStore.GetEquipped());
                // 화염구는 저장 목록에 없어도 항상 자동 장착이라 비교에서 뺀다.
                screen.RemoveAll(IsStarterId);
                stored.RemoveWhere(IsStarterId);
                bool same = stored.SetEquals(screen);
                sb.AppendLine("스킬창 장착 = 저장 장착: " + Mark(same) + "  (화면: " + (screen.Count == 0 ? "-" : string.Join(",", screen)) + ")");
            }
            return sb.ToString().TrimEnd();
        }

        private bool IsStarterId(string id)
        {
            foreach (var s in _skills)
                if (s.skillId == id) return RealSynergySync.IsStarterSkill(s);
            return false;
        }

        private static string Mark(bool ok) => ok ? "OK" : "불일치";

        private void OnGUI()
        {
            GUI.depth = -100;
            if (!_open) return;

            float height = Mathf.Min(Screen.height - 16f, 720f);
            GUILayout.BeginArea(new Rect(8, 8, Width, height), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("로비 연동 디버그 (F8 접기)");
            if (GUILayout.Button("접기", GUILayout.Width(50))) _open = false;
            GUILayout.EndHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawMawang();
            DrawSkillPoints();
            DrawSkills();
            DrawTraits();
            DrawCodex();
            DrawChecks();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawMawang()
        {
            GUILayout.Label("■ 마왕");
            GUILayout.Label(_mawangLine);
            var m = Mawang;
            GUI.enabled = m != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("XP +100")) { m.AddXp(100); Snapshot(); }
            if (GUILayout.Button("XP +1000")) { m.AddXp(1000); Snapshot(); }
            if (GUILayout.Button("LP +5")) { m.Refund(5); Snapshot(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Lv·XP 리셋(런 시작)")) { m.ResetForNewRun(); Snapshot(); }
            if (GUILayout.Button("마왕 전체 초기화")) { m.ClearSaved(); Snapshot(); }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        private void DrawSkillPoints()
        {
            GUILayout.Label("■ 스킬 포인트   SP " + SkillTreeStore.SkillPoints);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SP +5")) SkillTreeStore.SkillPoints += 5;
            if (GUILayout.Button("SP 0")) SkillTreeStore.SkillPoints = 0;
            if (GUILayout.Button("마왕군 해금 초기화")) UnitUnlockStore.ResetAll();
            if (GUILayout.Button("튜토리얼 초기화")) OZGL2.Tutorial.TutorialStore.ResetAll();
            if (GUILayout.Button(StageClearStore.DebugUnlockAll ? "난이도 잠금 복원" : "난이도 전부 열기")) StageClearStore.DebugUnlockAll = !StageClearStore.DebugUnlockAll;
            if (GUILayout.Button("클리어 기록 삭제")) StageClearStore.ResetAll();
            if (GUILayout.Button("스킬 저장 전부 삭제"))
            {
                SkillTreeStore.Wipe(_skills.Select(s => s.skillId));
                ResyncSkillWindows();
                Snapshot();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSkills()
        {
            GUILayout.Label("■ 스킬 해금 / 장착 (저장값)");
            foreach (var s in _skills)
            {
                bool starter = RealSynergySync.IsStarterSkill(s);
                bool unlocked = starter || SkillTreeStore.IsUnlocked(s.skillId);
                bool equipped = _equipped.Contains(s.skillId) || (starter && !SkillTreeStore.HasSavedEquipment);
                GUILayout.BeginHorizontal();
                GUILayout.Label("T" + s.tier + " " + s.displayName, GUILayout.Width(130));
                GUI.enabled = !starter;
                if (GUILayout.Button(unlocked ? "해금 ✔" : "잠김", GUILayout.Width(70)))
                {
                    SkillTreeStore.SetUnlocked(s.skillId, !unlocked);
                    if (unlocked) RemoveFromEquipped(s.skillId); // 잠그면 장착도 해제 (게임 규칙과 동일)
                    ResyncSkillWindows();
                    Snapshot();
                }
                GUI.enabled = unlocked;
                if (GUILayout.Button(equipped ? "장착 ✔" : "장착", GUILayout.Width(70)))
                {
                    ToggleEquipped(s.skillId, !equipped);
                    Snapshot();
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("스킬창은 다시 열면 저장 장착이 반영된다.");
        }

        private void DrawTraits()
        {
            GUILayout.Label("■ 특성 (저장값)");
            GUILayout.Label(_traitText);
            if (GUILayout.Button("특성 전체 초기화 (LP 환급)"))
            {
                var m = Mawang;
                _message = m != null && Traits().TryResetAndRefund(m) ? "특성 초기화, LP 환급됨" : "초기화할 특성 없음";
                Snapshot();
            }
        }

        /// <summary>도감 연동 확인용: 마왕군 해금·용사 발견을 켜고 끄면 도감 카드가 바로 열리고 닫힌다(0.4초 안에 반영).</summary>
        private void DrawCodex()
        {
            GUILayout.Label("■ 도감 (마왕군 해금 / 용사 발견)  — 발견 용사 " + CodexStore.SeenHeroCount + " / " + CodexStore.HeroIds.Length);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("마왕군 전부 해금")) UnitUnlockStore.UnlockAll();
            if (GUILayout.Button("마왕군 해금 초기화")) UnitUnlockStore.ResetAll();
            if (GUILayout.Button("용사 전부 등록")) CodexStore.MarkAllHeroes();
            if (GUILayout.Button("용사 등록 초기화")) CodexStore.ResetAll();
            GUILayout.EndHorizontal();
            foreach (var id in CodexStore.DemonIds)
            {
                bool scheduled = false;
                foreach (var e in UnitUnlockStore.Schedule) if (e.UnitId == id) scheduled = true;
                bool on = UnitUnlockStore.IsUnlocked(id);
                GUILayout.BeginHorizontal();
                GUILayout.Label((on ? "● " : "○ ") + "마왕군 " + UnitUnlockStore.DisplayName(id) + (scheduled ? "" : " (기본)"), GUILayout.Width(190f));
                GUI.enabled = scheduled;
                if (GUILayout.Button(on ? "잠그기" : "해금", GUILayout.Width(70f))) UnitUnlockStore.SetUnlocked(id, !on);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            foreach (var id in CodexStore.HeroIds)
            {
                bool on = CodexStore.HasSeenHero(id);
                GUILayout.BeginHorizontal();
                GUILayout.Label((on ? "● " : "○ ") + CodexStore.DisplayName(id), GUILayout.Width(190f));
                if (GUILayout.Button(on ? "지우기" : "등록", GUILayout.Width(70f))) CodexStore.SetHeroSeen(id, !on);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawChecks()
        {
            GUILayout.Label("■ UI ↔ 저장 일치 확인");
            GUILayout.Label(_checkText);
            GUILayout.Label(_message); // 항상 그려서 컨트롤 개수를 고정한다.
        }

        private void RemoveFromEquipped(string skillId)
        {
            var list = SkillTreeStore.GetEquipped();
            if (list.Remove(skillId)) SkillTreeStore.SetEquipped(list);
        }

        private void ToggleEquipped(string skillId, bool equip)
        {
            var list = SkillTreeStore.GetEquipped();
            if (equip)
            {
                int capacity = 3 + Traits().BuildModifiers().ExtraSkillSlots;
                if (list.Contains(skillId)) return;
                if (list.Count + 1 > capacity) { _message = "장착 용량 초과 (" + capacity + "칸)"; return; }
                list.Add(skillId);
                _message = string.Empty;
            }
            else list.Remove(skillId);
            SkillTreeStore.SetEquipped(list);
        }

        private static void ResyncSkillWindows()
        {
            foreach (var binder in FindObjectsByType<LobbySkillLoadoutBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                binder.ResyncUnlocks();
        }
    }
}
