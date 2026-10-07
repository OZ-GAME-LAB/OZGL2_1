using OZGL2.Grid;
using OZGL2.InGame;
using OZGL2.Progression;
using OZGL2.Stage;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 도전 중인 판을 실시간으로 저장한다. 웨이브를 깬 직후(보상을 고르기 전 포함)와 배치(준비) 단계에서 마왕성 배치·성급·재화·증강이 바뀔 때마다
    /// 그 스테이지의 저장 칸을 갱신하므로, 도중에 로비로 나가거나 게임을 꺼도 다음에 같은 난이도로 들어가면 그 웨이브부터 이어진다.
    /// - 보상(카드·증강)을 고르기 전에 나갔다면 이어할 때 그 보상부터 다시 받는다.
    /// - 전투 중에 나가면 그 판은 포기로 본다(전투를 되돌려 같은 웨이브를 다시 도전하는 것을 막는다). 끄려면 _forfeitIfLeftInCombat 을 끈다.
    /// - 패배하거나 끝까지 클리어하면 저장을 지운다(다음 도전은 1웨이브부터, 마왕 레벨은 유지). 1웨이브도 못 깬 판은 저장하지 않는다.
    /// </summary>
    public sealed class InGameRunSaver : MonoBehaviour
    {
        [SerializeField, Tooltip("켜면 전투 중에 나간 판을 포기한 것으로 처리한다(저장 삭제). 끄면 전투 직전 배치부터 이어한다.")] private bool _forfeitIfLeftInCombat = false;

        private InGamePrototypeBootstrap _bootstrap;
        private InGameCurrencyHud _hud;
        private float _next;
        private string _clearedRun;
        private eStageState _lastState = eStageState.IDLE;

        /// <summary>디버그 웨이브 점프처럼 저장본을 직접 써 둔 뒤 씬을 다시 열 때, 씬이 닫히며 옛 상태로 덮어쓰지 않게 막는다(새 씬이 열리면 풀린다).</summary>
        public static bool SuppressSave;

        private void OnEnable()
        {
            SuppressSave = false;
            RunResume.ForfeitOnCombatExit = _forfeitIfLeftInCombat;
        }

        /// <summary>패배해서 1웨이브부터 다시 시작해도 마왕 레벨·경험치는 그대로 이어지게 한다(웨이브·배치·재화만 처음부터). 「처음부터」(SaveGame.StartNew)만 레벨을 지운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void KeepLevelAcrossRuns() => OZGL2.Synergy.RealSynergySync.KeepMawangLevelOnRun = true;

        // 씬을 나가거나 게임이 꺼질 때 마지막 배치를 놓치지 않게 한 번 더 저장한다(0.4초 간격 저장 사이에 바꾼 것 포함)
        private void OnDisable() => FlushNow();
        private void OnApplicationQuit() => FlushNow();

        public void FlushNow()
        {
            if (_bootstrap == null || SuppressSave) return;
            var stage = _bootstrap.Stage;
            var progress = stage != null ? stage.Progress : null;
            if (progress == null) return;
            var state = stage.State;
            if (state == eStageState.PREPARATION || state == eStageState.GENERAL_REWARD || state == eStageState.AUGMENT) Save(progress, state);
        }

        private void Update()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            var progress = stage != null ? stage.Progress : null;
            if (progress == null) return;

            var state = stage.State;
            if (state != _lastState) { _lastState = state; _next = 0f; } // 단계가 바뀐 순간에는 바로 저장한다
            if (state == eStageState.FAILED || state == eStageState.CLEARED)
            {
                if (_clearedRun != progress.RunId) { _clearedRun = progress.RunId; RunSaveStore.Clear(progress.StageId); }
                return;
            }
            if (state == eStageState.COMBAT)
            {
                if (_forfeitIfLeftInCombat) RunSaveStore.MarkCombat(progress.StageId, true);
                return;
            }

            if (Time.unscaledTime < _next || SuppressSave) return;
            _next = Time.unscaledTime + 0.4f;
            Save(progress, state);
        }

        private void Save(StageRunResult progress, eStageState state)
        {
            int rewardStage;
            switch (state)
            {
                case eStageState.PREPARATION: rewardStage = 0; break;
                case eStageState.GENERAL_REWARD:
                case eStageState.WAVE_RESULT: rewardStage = 1; break;
                case eStageState.AUGMENT: rewardStage = 2; break;
                default: return;
            }
            if (progress.ClearedRoundCount < 1) return;
            var session = _bootstrap.GridSession;
            var grid = session != null ? session.Grid : null;
            if (grid == null || grid.Phase == eGridPhase.BATTLE || grid.Phase == eGridPhase.ENDED || grid.HasPendingStorage || grid.HasSelection || grid.RequiresExpansionPlacement) return; // 확장 조각을 아직 못 놓았으면 저장하지 않는다(이어하면 보상부터 다시)
            // 카드를 이미 받은 뒤(상태만 아직 보상)라면 같은 보상을 다시 주지 않도록 저장하지 않는다
            if (rewardStage == 1 && session.PendingRewardId == null) return;
            if (_hud == null) _hud = FindFirstObjectByType<InGameCurrencyHud>();

            var save = new RunSave
            {
                stageId = progress.StageId,
                clearedRounds = progress.ClearedRoundCount,
                currency = _hud != null ? _hud.Balance : 0,
                rewardStage = rewardStage,
            };
            RunSnapshotCodec.Capture(save, grid);
            var sync = OZGL2.Synergy.RealCombatBootstrap.EnsureInitialized();
            RunSnapshotCodec.CaptureAugments(save, sync != null ? sync.Augments : null);
            if (RunSaveStore.Save(save))
                Debug.Log("[이어하기] 저장: " + save.clearedRounds + "웨이브 클리어 · 바닥 " + save.floor.Count + "칸 · 발판 " + save.blocks.Count + " · 유닛 "
                          + save.units.Count + "(배치 " + save.units.FindAll(u => u.placed).Count + ") · 재화 " + save.currency + (save.rewardStage > 0 ? " · 보상 단계 " + save.rewardStage : string.Empty));
        }
    }
}
