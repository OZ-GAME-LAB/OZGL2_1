using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using OZGL2.Progression;
using OZGL2.Skill;
using OZGL2.Synergy;

namespace OZGL2.Sandbox
{
    /// <summary>
    /// 개인 루프 테스트용 로비 화면 — 계정 상태(마왕 레벨/LP/SP), 특성·스킬 찍기, "새 게임처럼" 초기화까지.
    /// 실제 팀 Builds/Lobby.unity·희수 UI와 완전히 무관한 내 전용 테스트 씬.
    /// </summary>
    public class SandboxLobbyScreen : MonoBehaviour
    {
        private enum View { Main, Traits, Skills }
        private View _view = View.Main;
        private Vector2 _scroll;
        private RealSynergySync _sync;

        /// <summary>RealCombatBootstrap이 씬 로드 직후 AfterSceneLoad에서 생성하는데, 그게 이 컴포넌트의
        /// Awake보다 늦게 실행될 수 있어서 한 번 캐시하지 않고 필요할 때마다 다시 찾는다.</summary>
        private RealSynergySync Sync => _sync != null ? _sync : (_sync = Object.FindFirstObjectByType<RealSynergySync>());

        private void OnGUI()
        {
            GUI.depth = 0;
            switch (_view)
            {
                case View.Main: DrawMain(); break;
                case View.Traits: DrawTraits(); break;
                case View.Skills: DrawSkills(); break;
            }
        }

        private void DrawMain()
        {
            const float w = 460f, h = 380f;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), SandboxGameUI.Panel);
            GUILayout.Label("마왕성 디펜스", SandboxGameUI.Title);
            GUILayout.Label("로비", SandboxGameUI.Subtitle);
            GUILayout.Space(10f);

            var mawang = MawangXpBridge.Mawang;
            if (mawang != null)
            {
                GUILayout.Label($"마왕 Lv {mawang.Level}   XP {mawang.Xp}/{mawang.XpToNext}", SandboxGameUI.Body);
                GUILayout.Label($"레벨 포인트(LP) {mawang.Points}   스킬 포인트(SP) {SkillTreeStore.SkillPoints}", SandboxGameUI.Body);
            }

            GUILayout.Space(18f);
            if (GUILayout.Button("특성 찍기", SandboxGameUI.SecondaryButton)) _view = View.Traits;
            GUILayout.Space(6f);
            if (GUILayout.Button("스킬 찍기", SandboxGameUI.SecondaryButton)) _view = View.Skills;

            GUILayout.Space(18f);
            if (GUILayout.Button("게임 시작", SandboxGameUI.PrimaryButton))
                SceneManager.LoadScene(SandboxLoopState.StageChoiceScene);

            GUILayout.Space(10f);
            if (GUILayout.Button("새 게임처럼 초기화 (레벨·특성·스킬 전부)", SandboxGameUI.SecondaryButton))
                ResetEverything();

            GUILayout.EndArea();
        }

        /// <summary>진짜 신규 계정처럼 — 마왕 레벨/XP/LP, 특성 랭크, 스킬 해금/장착 전부 초기화.</summary>
        private void ResetEverything()
        {
            MawangXpBridge.Mawang?.ClearSaved();
            Sync?.Traits?.ResetAll();

            var mgr = Sync?.SkillManager;
            if (mgr != null)
            {
                foreach (var skill in mgr.Skills)
                {
                    mgr.Unequip(skill);
                    mgr.SetUnlocked(skill, false);
                }
            }
            SkillTreeStore.Wipe(mgr != null ? mgr.Skills.Select(s => s.Data.skillId) : System.Array.Empty<string>());
        }

        // ─────────────────────────────── 특성 (다이아몬드 노드, 갈래별 색 구분)

        private static readonly Dictionary<TraitBranch, Color> BranchColor = new Dictionary<TraitBranch, Color>
        {
            { TraitBranch.Monster, new Color(0.75f, 0.45f, 0.2f) },  // 주황(몬스터)
            { TraitBranch.Hero, new Color(0.35f, 0.65f, 0.35f) },    // 초록(용사 약화)
            { TraitBranch.Skill, new Color(0.55f, 0.35f, 0.75f) },   // 보라(스킬)
            { TraitBranch.Economy, new Color(0.3f, 0.5f, 0.8f) },    // 파랑(재화·성장)
        };

        private void DrawTraits()
        {
            var tree = Sync?.Traits;
            var mawang = MawangXpBridge.Mawang;
            if (tree == null || mawang == null)
            {
                DrawUnavailable("특성 트리를 아직 불러오지 못했어 — 잠깐 기다렸다가 다시 눌러줘.");
                return;
            }

            const float w = 980f;
            float h = Mathf.Min(Screen.height - 40f, 680f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), SandboxGameUI.Panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("특성", SandboxGameUI.Title);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"현재 마왕 레벨: {mawang.Level}   남은 레벨 포인트: {mawang.Points}p", SandboxGameUI.Subtitle);
            GUILayout.EndHorizontal();

            // 중앙 "마왕" 다이아몬드 배너
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var kingRect = GUILayoutUtility.GetRect(56f, 56f, GUILayout.Width(56f), GUILayout.Height(56f));
            SandboxGameUI.DrawDiamondNode(kingRect, new Color(0.6f, 0.15f, 0.15f), false, "마왕", null);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(22f);

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.BeginHorizontal();
            DrawTraitBranch(tree, mawang, TraitBranch.Monster, "몬스터");
            DrawTraitBranch(tree, mawang, TraitBranch.Hero, "용사 약화");
            DrawTraitBranch(tree, mawang, TraitBranch.Skill, "스킬");
            DrawTraitBranch(tree, mawang, TraitBranch.Economy, "재화·성장");
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            if (GUILayout.Button("뒤로", SandboxGameUI.SecondaryButton)) _view = View.Main;
            GUILayout.EndArea();
        }

        private void DrawTraitBranch(TraitTree tree, MawangLevel mawang, TraitBranch branch, string title)
        {
            Color color = BranchColor[branch];
            GUILayout.BeginVertical(GUILayout.Width(230f));
            GUILayout.Label(title, SandboxGameUI.Subtitle);
            GUILayout.Space(6f);
            DrawTraitLine(tree, mawang, branch, TraitLine.A, color);
            DrawTraitLine(tree, mawang, branch, TraitLine.B, color);
            DrawTraitLine(tree, mawang, branch, TraitLine.C, color);
            var cap = tree.Defs.FirstOrDefault(d => d.branch == branch && d.line == TraitLine.Capstone);
            if (cap != null) DrawTraitDiamond(tree, mawang, cap, Lighten(color));
            GUILayout.EndVertical();
        }

        private void DrawTraitLine(TraitTree tree, MawangLevel mawang, TraitBranch branch, TraitLine line, Color color)
        {
            var nodes = tree.Defs.Where(d => d.branch == branch && d.line == line).OrderBy(d => (int)d.id).ToList();
            foreach (var d in nodes) DrawTraitDiamond(tree, mawang, d, color);
            GUILayout.Space(10f);
        }

        private void DrawTraitDiamond(TraitTree tree, MawangLevel mawang, TraitData d, Color color)
        {
            bool open = tree.IsOpen(d.id);
            int rank = tree.RankOf(d.id);
            int cost = tree.NextCost(d.id);
            string sub = cost < 0 ? "MAX" : $"{rank}/{d.maxRank}";

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var rect = GUILayoutUtility.GetRect(52f, 52f, GUILayout.Width(52f), GUILayout.Height(52f));
            bool clicked = SandboxGameUI.DrawDiamondNode(rect, color, !open, d.displayName, sub);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(20f);

            if (open && cost >= 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.enabled = tree.CanRank(d.id, mawang.Points);
                if (GUILayout.Button($"+1 ({cost} LP)", SandboxGameUI.SecondaryButton, GUILayout.Width(100f), GUILayout.Height(26f)) || clicked)
                    tree.TryRank(d.id, mawang);
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private static Color Lighten(Color c) => new Color(Mathf.Clamp01(c.r * 1.3f), Mathf.Clamp01(c.g * 1.3f), Mathf.Clamp01(c.b * 1.3f), c.a);

        // ─────────────────────────────── 스킬 (카테고리별 컬럼, SP 낮은 순 정렬, 다이아몬드 노드)

        private static string CategoryName(SkillCategory c) => c switch
        {
            SkillCategory.Damage => "딜",
            SkillCategory.Debuff => "디버프",
            SkillCategory.Buff => "버프",
            SkillCategory.Ultimate => "궁극기",
            _ => c.ToString(),
        };

        private void DrawSkills()
        {
            var mgr = Sync?.SkillManager;
            if (mgr == null)
            {
                DrawUnavailable("스킬 매니저를 아직 불러오지 못했어 — 잠깐 기다렸다가 다시 눌러줘.");
                return;
            }

            const float w = 780f;
            float h = Mathf.Min(Screen.height - 40f, 660f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), SandboxGameUI.Panel);
            GUILayout.Label("스킬 세팅", SandboxGameUI.Title);
            GUILayout.Label($"가진 SP 포인트: {SkillTreeStore.SkillPoints}p   장착 {mgr.EquippedCount}/{mgr.EquipCapacity}", SandboxGameUI.Subtitle);
            GUILayout.Space(10f);

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.BeginHorizontal();
            foreach (var group in mgr.Skills.GroupBy(s => s.Data.category).OrderBy(g => (int)g.Key))
            {
                GUILayout.BeginVertical(GUILayout.Width(180f));
                GUILayout.Label(CategoryName(group.Key), SandboxGameUI.CategoryHeader, GUILayout.Height(26f));
                GUILayout.Space(8f);
                foreach (var skill in group.OrderBy(s => s.UnlockCost))
                {
                    DrawSkillDiamond(mgr, skill);
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            if (GUILayout.Button("뒤로", SandboxGameUI.SecondaryButton)) _view = View.Main;
            GUILayout.EndArea();
        }

        private void DrawSkillDiamond(SkillManager mgr, SkillRuntime skill)
        {
            bool locked = !skill.IsUnlocked;
            Color fill = skill.IsEquipped ? new Color(0.2f, 0.55f, 0.95f) : new Color(0.25f, 0.28f, 0.36f);
            string sub = locked ? $"{skill.UnlockCost} SP" : (skill.IsEquipped ? "장착됨" : "탭하여 장착");

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var rect = GUILayoutUtility.GetRect(56f, 56f, GUILayout.Width(56f), GUILayout.Height(56f));
            bool clicked = SandboxGameUI.DrawDiamondNode(rect, fill, locked, skill.Data.displayName, sub);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(20f);

            if (locked)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.enabled = SkillTreeStore.SkillPoints >= skill.UnlockCost;
                if (GUILayout.Button($"해금 ({skill.UnlockCost}SP)", SandboxGameUI.SecondaryButton, GUILayout.Width(110f), GUILayout.Height(26f)))
                {
                    SkillTreeStore.SkillPoints -= skill.UnlockCost;
                    SkillTreeStore.SetUnlocked(skill.Data.skillId, true);
                    mgr.SetUnlocked(skill, true);
                }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else if (clicked)
            {
                if (skill.IsEquipped) mgr.Unequip(skill);
                else mgr.TryEquip(skill);
                SkillTreeStore.SetEquipped(EquippedIds(mgr));
            }
            GUILayout.Space(10f);
        }

        private static List<string> EquippedIds(SkillManager mgr)
        {
            var list = new List<string>();
            foreach (var s in mgr.EquippedSkills) list.Add(s.Data.skillId);
            return list;
        }

        private static void DrawUnavailable(string message)
        {
            const float w = 420f, h = 140f;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), SandboxGameUI.Panel);
            GUILayout.Label(message, SandboxGameUI.Body);
            GUILayout.EndArea();
        }
    }
}
