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
        /// <summary>비워두면 예전 개인 루프(JOB_SUNGMIN_StageChoice)로, 값이 있으면 그 씬으로 이동한다 —
        /// 밸런스 테스트 루프(JOB_SUNGMIN_Balance_*)가 같은 로비 화면을 재사용하려고 추가.</summary>
        [SerializeField] private string _stageChoiceScene = string.Empty;

        private enum View { Main, Traits, Skills }
        private View _view = View.Main;

        // IMGUI는 Layout 이벤트와 Repaint/마우스 이벤트에서 GUILayout 컨트롤 개수가 똑같아야 한다(안 그러면
        // "Getting control N's position in a group with only M controls" 오류). 마우스를 올린 노드나 클릭 결과로
        // 설명 패널 구성이 바뀌므로, 그런 값은 그 자리에서 바꾸지 않고 "다음 Layout 이벤트 시작 때" 한 번에 반영한다.
        // (Layout 이벤트에서는 마우스 위치도 믿을 수 없어서 hover 는 Repaint 때만 모은다.)
        private System.Action _pending;
        private string _skillHoverAccum, _skillHoverShown;
        private TraitId? _traitHoverAccum;

        private void BeginGuiFrame()
        {
            if (Event.current.type != EventType.Layout) return;
            _skillHoverShown = _skillHoverAccum;
            _skillHoverAccum = null;
            if (_traitHoverAccum.HasValue) _selectedTrait = _traitHoverAccum;   // 특성은 마지막으로 가리킨 노드를 유지
            if (_pending != null)
            {
                var action = _pending;
                _pending = null;
                action();
            }
        }
        private Vector2 _scroll;
        private RealSynergySync _sync;

        /// <summary>RealCombatBootstrap이 씬 로드 직후 AfterSceneLoad에서 생성하는데, 그게 이 컴포넌트의
        /// Awake보다 늦게 실행될 수 있어서 한 번 캐시하지 않고 필요할 때마다 다시 찾는다.</summary>
        private RealSynergySync Sync => _sync != null ? _sync : (_sync = Object.FindFirstObjectByType<RealSynergySync>());

        private bool _lobbyPrepared;

        /// <summary>로비에서는 전투용 스킬바(화면 아래 스킬창)가 보이면 안 되고, 기본 스킬(화염구)은 항상
        /// 해금·장착 상태여야 한다. RealSynergySync는 씬 로드 직후에 만들어지므로 준비될 때까지 매 프레임 시도한다.</summary>
        private void Update() => PrepareLobby();

        private void PrepareLobby()
        {
            if (_lobbyPrepared) return;
            var sync = Sync;
            var mgr = sync != null ? sync.SkillManager : null;
            if (mgr == null) return;

            sync.StopCombat(); // 스킬바 숨김 + 시전 비활성
            foreach (var skill in mgr.Skills)
            {
                if (!RealSynergySync.IsStarterSkill(skill.Data)) continue;
                if (!skill.IsUnlocked) mgr.SetUnlocked(skill, true);
                if (!skill.IsEquipped) mgr.TryEquip(skill);
            }
            _lobbyPrepared = true;
        }

        private void OnGUI()
        {
            GUI.depth = 0;
            BeginGuiFrame();
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
                SceneManager.LoadScene(string.IsNullOrEmpty(_stageChoiceScene) ? SandboxLoopState.StageChoiceScene : _stageChoiceScene);

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
                    if (RealSynergySync.IsStarterSkill(skill.Data)) continue; // 기본 스킬(화염구)은 초기화해도 해금·장착 유지
                    mgr.Unequip(skill);
                    mgr.SetUnlocked(skill, false);
                }
            }
            SkillTreeStore.Wipe(mgr != null ? mgr.Skills.Select(s => s.Data.skillId) : System.Array.Empty<string>());
        }

        // ─────────────────────────────── 특성 (트리: 마왕 → 갈래 → 라인 A·B·C 체인 → 캡스톤, 선으로 연결)

        private static readonly Dictionary<TraitBranch, Color> BranchColor = new Dictionary<TraitBranch, Color>
        {
            { TraitBranch.Monster, new Color(0.75f, 0.45f, 0.2f) },  // 주황(몬스터)
            { TraitBranch.Hero, new Color(0.35f, 0.65f, 0.35f) },    // 초록(용사 약화)
            { TraitBranch.Skill, new Color(0.55f, 0.35f, 0.75f) },   // 보라(스킬)
            { TraitBranch.Economy, new Color(0.3f, 0.5f, 0.8f) },    // 파랑(재화·성장)
        };

        private static readonly TraitBranch[] BranchOrder = { TraitBranch.Monster, TraitBranch.Hero, TraitBranch.Skill, TraitBranch.Economy };
        private static readonly TraitLine[] LineOrder = { TraitLine.A, TraitLine.B, TraitLine.C };

        private static string BranchName(TraitBranch b) => b switch
        {
            TraitBranch.Monster => "몬스터",
            TraitBranch.Hero => "용사 약화",
            TraitBranch.Skill => "스킬",
            _ => "재화·성장",
        };

        private static string LineName(TraitLine l) => l == TraitLine.Capstone ? "캡스톤" : l + "라인";

        private TraitId? _selectedTrait;   // 마지막으로 마우스를 올린 노드 — 아래 상세 패널에 표시

        // 트리 캔버스 치수(픽셀)
        private const float ColW = 70f, BranchGap = 28f, PadX = 26f;
        private const float KingY = 40f, HubY = 118f, RowY0 = 208f, RowGap = 98f;
        private static readonly float BranchW = ColW * 3f;
        private static readonly float CanvasW = PadX * 2f + BranchW * 4f + BranchGap * 3f;
        private static readonly float CapY = RowY0 + RowGap * 2f + 108f;
        private static readonly float CanvasH = CapY + 84f;

        private static float BranchLeft(int b) => PadX + b * (BranchW + BranchGap);
        private static float ColX(int b, int c) => BranchLeft(b) + ColW * (c + 0.5f);
        private static float HubX(int b) => BranchLeft(b) + BranchW / 2f;
        private static float RowY(int r) => RowY0 + r * RowGap;

        private void DrawTraits()
        {
            var tree = Sync?.Traits;
            var mawang = MawangXpBridge.Mawang;
            if (tree == null || mawang == null)
            {
                DrawUnavailable("특성 트리를 아직 불러오지 못했어 — 잠깐 기다렸다가 다시 눌러줘.");
                return;
            }

            float w = Mathf.Min(1020f, Screen.width - 20f);
            float h = Mathf.Min(Screen.height - 30f, 780f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), SandboxGameUI.Panel);

            GUILayout.BeginHorizontal();
            GUILayout.Label("특성 트리", SandboxGameUI.Title);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"마왕 Lv {mawang.Level}   남은 레벨 포인트 {mawang.Points}p", SandboxGameUI.Subtitle);
            GUILayout.EndHorizontal();
            GUILayout.Label("좌클릭 = +1 (1 LP)   ·   우클릭 = −1 (환급)   ·   앞 노드를 만렙으로 찍어야 다음 노드가 열리고, 캡스톤은 A·B·C 3라인을 전부 만렙 찍어야 열려요.", SandboxGameUI.Body);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(h - 210f));
            var canvas = GUILayoutUtility.GetRect(CanvasW, CanvasH);
            DrawTraitTree(tree, mawang, canvas.position);
            GUILayout.EndScrollView();

            DrawTraitDetail(tree);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 환급 (특성 초기화)", SandboxGameUI.SecondaryButton, GUILayout.Width(220f))) _pending = () => tree.TryResetAndRefund(mawang);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("뒤로", SandboxGameUI.SecondaryButton, GUILayout.Width(120f))) _pending = () => _view = View.Main;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>트리 전체를 (origin 기준) 그린다 — 연결선을 먼저, 그 위에 노드를 얹는다.</summary>
        private void DrawTraitTree(TraitTree tree, MawangLevel mawang, Vector2 origin)
        {
            var lineOff = new Color(0.27f, 0.27f, 0.33f);
            Vector2 P(float x, float y) => new Vector2(origin.x + x, origin.y + y);
            void Elbow(Vector2 from, Vector2 to, float midY, Color c)  // 세로-가로-세로 꺾은선(조직도 스타일)
            {
                float my = origin.y + midY;
                SandboxGameUI.DrawLine(from, new Vector2(from.x, my), c, 3f);
                SandboxGameUI.DrawLine(new Vector2(from.x, my), new Vector2(to.x, my), c, 3f);
                SandboxGameUI.DrawLine(new Vector2(to.x, my), to, c, 3f);
            }

            float kingX = CanvasW / 2f;
            var lineNodes = new TraitData[4, 3][];      // [갈래, 라인] → 노드 3개(체인 순서)
            var caps = new TraitData[4];
            for (int b = 0; b < 4; b++)
            {
                for (int l = 0; l < 3; l++)
                {
                    int bb = b, ll = l;
                    lineNodes[b, l] = tree.Defs.Where(d => d.branch == BranchOrder[bb] && d.line == LineOrder[ll]).OrderBy(d => (int)d.id).ToArray();
                }
                int b2 = b;
                caps[b] = tree.Defs.FirstOrDefault(d => d.branch == BranchOrder[b2] && d.line == TraitLine.Capstone);
            }

            // ── 연결선 ──
            for (int b = 0; b < 4; b++)
            {
                Color color = BranchColor[BranchOrder[b]];
                float hx = HubX(b);
                Elbow(P(kingX, KingY + 30f), P(hx, HubY - 16f), 84f, color);                       // 마왕 → 갈래

                for (int l = 0; l < 3; l++)
                {
                    var nodes = lineNodes[b, l];
                    if (nodes.Length == 0) continue;
                    float cx = ColX(b, l);
                    Elbow(P(hx, HubY + 16f), P(cx, RowY(0) - 30f), 152f, color);                    // 갈래 → 라인 루트
                    for (int r = 0; r + 1 < nodes.Length; r++)                                        // 체인: 앞 노드 만렙이면 켜짐
                        SandboxGameUI.DrawLine(P(cx, RowY(r) + 30f), P(cx, RowY(r + 1) - 30f), tree.IsMaxed(nodes[r].id) ? color : lineOff, 3f);
                    if (caps[b] != null)                                                              // 라인 끝 → 캡스톤
                    {
                        float busY = RowY(2) + 74f;
                        Color c = tree.IsMaxed(nodes[nodes.Length - 1].id) ? color : lineOff;
                        SandboxGameUI.DrawLine(P(cx, RowY(2) + 30f), P(cx, busY), c, 3f);
                        SandboxGameUI.DrawLine(P(cx, busY), P(hx, busY), c, 3f);
                        SandboxGameUI.DrawLine(P(hx, busY), P(hx, CapY - 32f), c, 3f);
                    }
                }
            }

            // ── 마왕(뿌리) + 갈래 이름표 ──
            var kingRect = new Rect(origin.x + kingX - 22f, origin.y + KingY - 22f, 44f, 44f);
            SandboxGameUI.DrawDiamondNode(kingRect, new Color(0.6f, 0.15f, 0.15f), false, string.Empty, null);
            GUI.Label(new Rect(kingRect.center.x - 42f, kingRect.yMax + 4f, 84f, 18f), "마왕", SandboxGameUI.DiamondLabel);
            for (int b = 0; b < 4; b++)
            {
                var hubRect = new Rect(origin.x + HubX(b) - 44f, origin.y + HubY - 15f, 88f, 30f);
                var prevColor = GUI.color;
                GUI.color = BranchColor[BranchOrder[b]];
                GUI.DrawTexture(hubRect, Texture2D.whiteTexture);   // 갈래 색 그대로 채움(색 곱하기 왜곡 없이)
                GUI.color = prevColor;
                GUI.Label(hubRect, BranchName(BranchOrder[b]), SandboxGameUI.DiamondLabel);
            }

            // ── 노드 ──
            for (int b = 0; b < 4; b++)
            {
                Color color = BranchColor[BranchOrder[b]];
                for (int l = 0; l < 3; l++)
                {
                    var nodes = lineNodes[b, l];
                    for (int r = 0; r < nodes.Length; r++)
                        DrawTreeTrait(tree, mawang, nodes[r], new Vector2(origin.x + ColX(b, l), origin.y + RowY(r)), 40f, color);
                }
                if (caps[b] != null)
                    DrawTreeTrait(tree, mawang, caps[b], new Vector2(origin.x + HubX(b), origin.y + CapY), 52f, Lighten(color));
            }
        }

        private void DrawTreeTrait(TraitTree tree, MawangLevel mawang, TraitData d, Vector2 center, float size, Color color)
        {
            bool open = tree.IsOpen(d.id);
            int rank = tree.RankOf(d.id);
            bool maxed = tree.IsMaxed(d.id);
            Color fill = rank > 0 ? color : new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f);
            var rect = new Rect(center.x - size / 2f, center.y - size / 2f, size, size);
            bool hover;
            int click = SandboxGameUI.DrawTreeNode(rect, fill, !open, maxed, d.displayName, maxed ? "MAX" : $"{rank}/{d.maxRank}", out hover);
            if (hover && Event.current.type == EventType.Repaint) _traitHoverAccum = d.id;
            if (click == 1) _pending = () => { if (tree.CanRank(d.id, mawang.Points)) tree.TryRank(d.id, mawang); };
            else if (click == 2) _pending = () => { if (tree.CanUnrank(d.id)) tree.TryUnrank(d.id, mawang); };
        }

        /// <summary>마지막으로 가리킨 노드의 이름·설명·랭크·비용·전제를 보여준다.</summary>
        private void DrawTraitDetail(TraitTree tree)
        {
            GUILayout.BeginVertical(SandboxGameUI.CardOpen, GUILayout.Height(92f));
            var d = _selectedTrait.HasValue ? tree.Def(_selectedTrait.Value) : null;
            if (d == null)
            {
                GUILayout.Label("노드에 마우스를 올리면 설명이 여기에 보여요.", SandboxGameUI.Body);
            }
            else
            {
                int cost = tree.NextCost(d.id);
                string costText = cost < 0 ? "만렙" : $"다음 {cost} LP";
                GUILayout.Label($"{d.displayName}   [{BranchName(d.branch)} · {LineName(d.line)}]   랭크 {tree.RankOf(d.id)}/{d.maxRank}   ({costText})", SandboxGameUI.Subtitle);
                if (!string.IsNullOrEmpty(d.description)) GUILayout.Label(d.description, SandboxGameUI.Body);
                if (d.parents != null && d.parents.Length > 0)
                {
                    var names = d.parents.Select(p => tree.Def(p) != null ? tree.Def(p).displayName : p.ToString());
                    GUILayout.Label("전제(전부 만렙): " + string.Join(", ", names) + (tree.IsOpen(d.id) ? "   ✔ 열림" : "   ✖ 아직 잠김"), SandboxGameUI.Body);
                }
            }
            GUILayout.EndVertical();
        }

        private static Color Lighten(Color c) => new Color(Mathf.Clamp01(c.r * 1.3f), Mathf.Clamp01(c.g * 1.3f), Mathf.Clamp01(c.b * 1.3f), c.a);

        // ─────────────────────────────── 스킬 (카테고리별 가로 줄, 노드를 눌러 선택 → 아래 패널에서 설명 확인·해금·장착)

        private static readonly SkillCategory[] CategoryOrder = { SkillCategory.Damage, SkillCategory.Debuff, SkillCategory.Buff, SkillCategory.Ultimate };

        private static Color CategoryColor(SkillCategory c) => c switch
        {
            SkillCategory.Damage => new Color(0.85f, 0.35f, 0.3f),
            SkillCategory.Debuff => new Color(0.6f, 0.4f, 0.85f),
            SkillCategory.Buff => new Color(0.3f, 0.7f, 0.5f),
            SkillCategory.Ultimate => new Color(0.9f, 0.7f, 0.2f),
            _ => new Color(0.5f, 0.5f, 0.6f),
        };

        private string _selectedSkillId;   // 클릭해서 고른 스킬 — 아래 패널의 버튼(해금·장착)이 이 스킬에 적용된다
        private string _skillMessage = string.Empty;

        private const float SkNodeStep = 96f, SkRowH = 100f, SkHeaderW = 92f, SkPad = 12f, SkCanvasW = 940f;

        private void DrawSkills()
        {
            var mgr = Sync?.SkillManager;
            if (mgr == null)
            {
                DrawUnavailable("스킬 매니저를 아직 불러오지 못했어 — 잠깐 기다렸다가 다시 눌러줘.");
                return;
            }

            float w = Mathf.Min(1000f, Screen.width - 20f);
            float h = Mathf.Min(Screen.height - 30f, 780f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), SandboxGameUI.Panel);

            GUILayout.BeginHorizontal();
            GUILayout.Label("스킬 세팅", SandboxGameUI.Title);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"보유 스킬 포인트 {SkillTreeStore.SkillPoints} SP   ·   장착 {mgr.EquippedCount}/{mgr.EquipCapacity}", SandboxGameUI.Subtitle);
            GUILayout.EndHorizontal();

            DrawEquipSlots(mgr);
            GUILayout.Label("스킬을 클릭하면 아래에 설명이 나와요. 잠긴 스킬은 SP로 해금하고, 우클릭하면 바로 장착/해제해요. 화염구는 기본 스킬이라 항상 장착돼요.", SandboxGameUI.Body);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(h - 300f));
            int perRow = Mathf.Max(1, (int)((SkCanvasW - SkHeaderW - SkPad * 2f) / SkNodeStep));
            var groups = CategoryOrder.Select(c => mgr.Skills.Where(s => s.Data.category == c).OrderBy(s => s.UnlockCost).ThenBy(s => s.Data.displayName).ToList()).ToList();
            int totalLines = groups.Sum(g => Mathf.Max(1, Mathf.CeilToInt(g.Count / (float)perRow)));
            var canvas = GUILayoutUtility.GetRect(SkCanvasW, totalLines * SkRowH + CategoryOrder.Length * 8f);

            float y = canvas.y;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                var group = groups[gi];
                int lines = Mathf.Max(1, Mathf.CeilToInt(group.Count / (float)perRow));
                var cat = CategoryOrder[gi];
                Color color = CategoryColor(cat);

                var band = new Rect(canvas.x, y, SkCanvasW, lines * SkRowH - 4f);
                var prevColor = GUI.color;
                GUI.color = new Color(color.r, color.g, color.b, 0.14f);
                GUI.DrawTexture(band, Texture2D.whiteTexture);                       // 카테고리 배경 띠
                GUI.color = color;
                GUI.DrawTexture(new Rect(band.x, band.y, SkHeaderW - 8f, band.height), Texture2D.whiteTexture);   // 왼쪽 이름표
                GUI.color = prevColor;
                GUI.Label(new Rect(band.x, band.y, SkHeaderW - 8f, band.height), SkillDescriber.CategoryName(cat), SandboxGameUI.CategoryLabel);

                for (int i = 0; i < group.Count; i++)
                {
                    float cx = canvas.x + SkPad + SkHeaderW + SkNodeStep * (i % perRow + 0.5f);
                    float cy = y + (i / perRow) * SkRowH + 32f;
                    DrawSkillNode(mgr, group[i], new Vector2(cx, cy), color);
                }
                y += lines * SkRowH + 8f;
            }
            GUILayout.EndScrollView();

            DrawSkillDetail(mgr);

            if (GUILayout.Button("뒤로", SandboxGameUI.SecondaryButton)) _pending = () => _view = View.Main;
            GUILayout.EndArea();
        }

        /// <summary>장착 슬롯을 칸으로 보여준다(빈 칸 포함) — "몇 개 찼는지"가 한눈에 보이게.</summary>
        private void DrawEquipSlots(SkillManager mgr)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("장착 슬롯", SandboxGameUI.Subtitle, GUILayout.Width(80f));
            var equipped = mgr.EquippedSkills.ToList();
            for (int i = 0; i < mgr.EquipCapacity; i++)
            {
                GUILayout.BeginVertical(SandboxGameUI.CardOpen, GUILayout.Width(128f), GUILayout.Height(30f));
                GUILayout.Label(i < equipped.Count ? equipped[i].Data.displayName : "— 빈 슬롯 —", SandboxGameUI.Body);
                GUILayout.EndVertical();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawSkillNode(SkillManager mgr, SkillRuntime skill, Vector2 center, Color color)
        {
            bool starter = RealSynergySync.IsStarterSkill(skill.Data);
            bool unlocked = skill.IsUnlocked || starter;
            bool equipped = skill.IsEquipped;
            string sub = !unlocked ? $"{skill.UnlockCost} SP" : starter ? "기본" : equipped ? "장착중" : "해금됨";
            Color fill = equipped ? color : new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f);
            bool selected = skill.Data.skillId == _selectedSkillId;

            var rect = new Rect(center.x - 20f, center.y - 20f, 40f, 40f);
            bool hover;
            int click = SandboxGameUI.DrawTreeNode(rect, fill, !unlocked, equipped || selected, skill.Data.displayName, sub, out hover, clickableWhenLocked: true);
            if (hover && Event.current.type == EventType.Repaint) _skillHoverAccum = skill.Data.skillId;
            if (click == 1)
            {
                string id = skill.Data.skillId;
                _pending = () => { _selectedSkillId = id; _skillMessage = string.Empty; };
            }
            else if (click == 2 && unlocked && !starter)
            {
                string id = skill.Data.skillId;
                _pending = () => { _selectedSkillId = id; ToggleEquip(mgr, skill); };
            }
        }

        private void ToggleEquip(SkillManager mgr, SkillRuntime skill)
        {
            if (skill.IsEquipped)
            {
                mgr.Unequip(skill);
                _skillMessage = string.Empty;
            }
            else if (mgr.TryEquip(skill))
            {
                _skillMessage = string.Empty;
            }
            else
            {
                _skillMessage = $"슬롯이 가득 찼어요({mgr.EquippedCount}/{mgr.EquipCapacity}) — 장착 중인 스킬을 먼저 해제해 주세요.";
            }
            SkillTreeStore.SetEquipped(EquippedIds(mgr));
        }

        /// <summary>마우스를 올린 스킬(없으면 클릭해서 고른 스킬)의 설명과 해금·장착 버튼.</summary>
        private void DrawSkillDetail(SkillManager mgr)
        {
            string id = _skillHoverShown ?? _selectedSkillId;
            var skill = id == null ? null : mgr.Skills.FirstOrDefault(s => s.Data.skillId == id);

            GUILayout.BeginVertical(SandboxGameUI.CardOpen, GUILayout.Height(140f));
            if (skill == null)
            {
                GUILayout.Label("스킬을 클릭하면 설명이 여기에 보여요.", SandboxGameUI.Body);
                GUILayout.EndVertical();
                return;
            }

            var d = skill.Data;
            bool starter = RealSynergySync.IsStarterSkill(d);
            bool unlocked = skill.IsUnlocked || starter;
            GUILayout.Label(d.displayName, SandboxGameUI.Title);
            GUILayout.Label(SkillDescriber.Header(d), SandboxGameUI.Subtitle);
            GUILayout.Label(SkillDescriber.Describe(d), SandboxGameUI.Body);
            GUILayout.FlexibleSpace();

            // 버튼은 "클릭해서 고른 스킬"에만 적용한다(마우스가 다른 노드를 스치며 지나가도 엉뚱한 스킬이 해금되지 않게).
            bool actionable = id == _selectedSkillId;
            GUILayout.BeginHorizontal();
            if (starter)
            {
                GUILayout.Label("기본 스킬 — 항상 해금·장착돼 있어요.", SandboxGameUI.Body);
            }
            else if (!unlocked)
            {
                GUI.enabled = actionable && SkillTreeStore.SkillPoints >= skill.UnlockCost;
                if (GUILayout.Button($"해금 ({skill.UnlockCost} SP)", SandboxGameUI.SecondaryButton, GUILayout.Width(160f), GUILayout.Height(28f)))
                {
                    _pending = () =>
                    {
                        SkillTreeStore.SkillPoints -= skill.UnlockCost;
                        SkillTreeStore.SetUnlocked(d.skillId, true);
                        mgr.SetUnlocked(skill, true);
                        _skillMessage = string.Empty;
                    };
                }
                GUI.enabled = true;
                if (SkillTreeStore.SkillPoints < skill.UnlockCost)
                    GUILayout.Label($"SP가 {skill.UnlockCost - SkillTreeStore.SkillPoints} 부족해요.", SandboxGameUI.Body);
            }
            else
            {
                GUI.enabled = actionable;
                if (GUILayout.Button(skill.IsEquipped ? "장착 해제" : "장착", SandboxGameUI.SecondaryButton, GUILayout.Width(160f), GUILayout.Height(28f)))
                    _pending = () => ToggleEquip(mgr, skill);
                GUI.enabled = true;
            }
            GUILayout.Label(_skillMessage ?? string.Empty, SandboxGameUI.Body);   // 항상 그려서 컨트롤 개수가 안 바뀌게
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
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
