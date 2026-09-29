using System;
using System.Collections.Generic;
using UnityEngine;
using OZGL2.InGame;
using OZGL2.Stage;
using OZGL2.Synergy;
using SandboxGameUI = OZGL2.Sandbox.SandboxGameUI;

namespace OZGL2.BalanceTest
{
    /// <summary>
    /// 밸런스 테스트 루프의 난이도 선택 화면(보통·어려움·헬). 화면만 내 것이고, 선택 → 씬 이동은 실제 게임과
    /// 똑같이 StageSelectionController(StageLaunchRuntime 요청함)를 그대로 쓴다 — 그래서 InGame 쪽은
    /// 실제 게임과 완전히 같은 경로로 스테이지를 받아 시작한다.
    /// </summary>
    public sealed class BalanceStageChoiceScreen : MonoBehaviour
    {
        [SerializeField] private StageSelectionController _controller;

        private IReadOnlyList<StageDefinition> _stages;
        private string _error;

        private struct Info { public string Title, Desc, Boss; }

        private static readonly Dictionary<string, Info> Infos = new Dictionary<string, Info>
        {
            { "bal_normal_30", new Info { Title = "보통  ·  30라운드",
                Desc = "온보딩 난이도. 위협 요소를 한 번에 하나씩 익힌다. 클리어하면 어려움 해금.",
                Boss = "보스 R10 · R20 · R30(최종)" } },
            { "bal_hard_50", new Info { Title = "어려움  ·  50라운드",
                Desc = "엔드게임. 3성·시너지 2단계·증강·영구강화 최적화가 필요하다. R21부터 용사 힐러가 등장한다.",
                Boss = "보스 R10 · R20 · R30 · R40 · R50(최종)" } },
            { "bal_hell_100", new Info { Title = "헬  ·  100라운드",
                Desc = "하드코어 엔드게임. 6직업이 R1부터 전부 등장하고, 정예 비중이 30%에서 93%까지 오른다.",
                Boss = "보스 10종 (R10 ~ R100, 최종 R100)" } },
        };

        private bool _barHidden;

        /// <summary>난이도 선택 화면에도 전투용 스킬바가 떠 있으면 안 된다 — 로비에서 이어진 RealSynergySync의
        /// 스킬바를 숨긴다(생성이 늦을 수 있어 준비될 때까지 매 프레임 시도).</summary>
        private void Update()
        {
            if (_barHidden) return;
            var sync = FindFirstObjectByType<RealSynergySync>();
            if (sync == null || sync.SkillManager == null) return;
            sync.StopCombat();
            _barHidden = true;
        }

        private void Start()
        {
            RealSynergySync.KeepMawangLevelOnRun = true; // 밸런스 루프에서는 마왕 레벨이 런을 넘어 유지된다
            try { _stages = _controller != null ? _controller.GetStages() : null; }
            catch (Exception e) { _error = e.Message; }
        }

        private void OnGUI()
        {
            GUI.depth = 0;
            const float w = 560f;
            float h = Mathf.Min(Screen.height - 40f, 620f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), SandboxGameUI.Panel);
            GUILayout.Label("난이도 선택", SandboxGameUI.Title);
            GUILayout.Label("밸런스 테스트 루프 — 진짜 InGame 시스템으로 진행돼요", SandboxGameUI.Subtitle);
            GUILayout.Space(10f);

            bool prev = GUI.enabled;
            GUI.enabled = prev && _controller != null && !_controller.IsLoading;
            if (_stages != null)
            {
                foreach (var stage in _stages)
                {
                    Infos.TryGetValue(stage.StageId, out var info);
                    GUILayout.BeginVertical(SandboxGameUI.CardOpen);
                    GUILayout.Label(string.IsNullOrEmpty(info.Title) ? stage.StageId : info.Title, SandboxGameUI.Subtitle);
                    if (!string.IsNullOrEmpty(info.Desc)) GUILayout.Label(info.Desc, SandboxGameUI.Body);
                    if (!string.IsNullOrEmpty(info.Boss)) GUILayout.Label(info.Boss, SandboxGameUI.Body);
                    GUILayout.Label(ScalingSummary(stage), SandboxGameUI.Body);
                    if (GUILayout.Button("이 난이도로 시작", SandboxGameUI.PrimaryButton))
                        _controller.TryStartStage(stage.StageId);
                    GUILayout.EndVertical();
                    GUILayout.Space(8f);
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("로비로", SandboxGameUI.SecondaryButton)) _controller.TryReturnToLobby();
            GUI.enabled = prev;

            string message = _error ?? (_controller != null ? _controller.Error : "StageSelectionController 연결 안 됨");
            GUILayout.Label(message ?? string.Empty, SandboxGameUI.Body);   // 항상 그려서 클릭 직후에도 컨트롤 개수가 안 바뀌게
            GUILayout.EndArea();
        }

        /// <summary>시트에서 뽑은 시작/끝 라운드 용사 배율을 한 줄로 보여준다(밸런스 확인용).</summary>
        private static string ScalingSummary(StageDefinition stage)
        {
            int last = stage.Rounds.Count;
            if (BalanceRoundScaling.TryGet(stage.StageId, 1, out var first) && BalanceRoundScaling.TryGet(stage.StageId, last, out var end))
                return $"용사 배율  R1  HP x{first.Hp:0.00} 공격 x{first.Atk:0.00}   →   R{last}  HP x{end.Hp:0.00} 공격 x{end.Atk:0.00}";
            return "용사 배율  (시트 데이터 없음)";
        }
    }
}
