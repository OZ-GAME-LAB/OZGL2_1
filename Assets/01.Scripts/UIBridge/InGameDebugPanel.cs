using OZGL2.Augment;
using OZGL2.InGame;
using OZGL2.Progression;
using OZGL2.Stage;
using OZGL2.Synergy;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전투 씬 디버그 패널(에디터·개발 빌드 전용, F8로 접기/펴기). 웨이브를 바로 넘겨서 보상·증강 화면을 빨리 확인하는 용도다.
    /// - 「이번 웨이브 클리어」: 준비 중이면 전투를 시작하고, 전투 중이면 남은 용사를 모두 쓰러뜨려 그 웨이브를 이긴 것으로 끝낸다.
    /// - 「자동 진행」: 준비가 끝나 있으면 전투를 자동으로 시작하고 용사를 계속 쓰러뜨려 웨이브를 연달아 넘긴다. 보상(카드)은 직접 고른다.
    /// - 「증강까지 자동 진행」: 위 자동 진행을 켜 두고, 보스 웨이브를 깨서 증강 선택 단계가 열리면 자동으로 멈춘다.
    /// - 「웨이브 점프」: 입력한 웨이브의 준비 단계로 바로 간다. 지금의 배치·재화·증강을 그대로 가지고 이어하기 경로로 장면을 다시 열기 때문에 보상 화면 등을 건너뛴다(1을 넣으면 처음부터).
    /// - 「증강 직접 얻기」: 증강 목록에서 원하는 것을 바로 얻는다(실제 전투 효과가 즉시 적용된다).
    /// 용사를 쓰러뜨릴 때는 처치 보상·경험치·업적 기록이 정상 흐름과 똑같이 쌓인다(그래서 손으로 해 본 것과 같은 결과가 나온다).
    /// IMGUI라 컨트롤 개수가 Layout/Repaint에서 같아야 한다 — 표시 문자열은 Update에서만 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InGameDebugPanel : MonoBehaviour
    {
        private const float Width = 330f;

        private bool _open;
        private bool _killWave;          // 이번 전투를 바로 끝낸다
        private bool _auto;              // 전투 시작 + 클리어를 자동으로 반복
        private bool _stopAtAugment;     // 증강 단계가 열리면 자동 진행을 끈다
        private float _nextAction;
        private string _jumpText = "10";
        private Vector2 _augScroll;
        private AugmentRun _augRun;
        private string _line = string.Empty;
        private string _message = string.Empty;
        private InGamePrototypeBootstrap _bootstrap;
        private eStageState _lastState = eStageState.IDLE;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame) _open = !_open;
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage == null) { _line = "전투 준비 중…"; return; }

            if (_augRun == null)
            {
                var sync = RealCombatBootstrap.EnsureInitialized();
                _augRun = sync != null ? sync.Augments : null;
            }

            var state = stage.State;
            if (state != _lastState)
            {
                if (state != eStageState.COMBAT) _killWave = false;
                if (state == eStageState.AUGMENT && _stopAtAugment)
                {
                    _auto = false; _stopAtAugment = false;
                    _message = "증강 선택 단계에 도착했어요. 자동 진행을 껐습니다.";
                }
                _lastState = state;
            }
            _line = "웨이브 " + stage.CurrentRoundNumber + " / " + stage.TotalRounds + "   상태: " + state + (IsBoss(stage, stage.CurrentRoundNumber) ? "   (보스)" : string.Empty);

            if (Time.unscaledTime < _nextAction) return;
            if (state == eStageState.PREPARATION && _auto)
            {
                _nextAction = Time.unscaledTime + 0.8f;
                var grid = _bootstrap.GridSession != null ? _bootstrap.GridSession.Grid : null;
                if (grid != null && grid.CanBeginBattle && !_bootstrap.TryBeginBattle()) _message = "전투를 시작하지 못했어요(카메라 이동 중이거나 배치가 필요해요).";
            }
            else if (state == eStageState.COMBAT && (_killWave || _auto))
            {
                _nextAction = Time.unscaledTime + 0.15f;
                KillAllHeroes();
            }
        }

        private void KillAllHeroes()
        {
            var heroes = UnitRegistry.GetUnits(UnitSide.Hero);
            for (int i = heroes.Count - 1; i >= 0; i--)
            {
                var hero = heroes[i];
                if (hero != null && hero.currentState != UnitState.Dead) hero.TakeDamage(999999);
            }
        }

        private bool IsBoss(StageManager stage, int round)
        {
            var config = _bootstrap != null ? _bootstrap.Config : null;
            string stageId = stage.Progress != null ? stage.Progress.StageId : null;
            if (config == null || config.StageCatalog == null || string.IsNullOrEmpty(stageId)) return false;
            try
            {
                var definition = config.StageCatalog.Resolve(stageId);
                return definition != null && round >= 1 && round <= definition.Rounds.Count && definition.Rounds[round - 1].IsBossRound;
            }
            catch { return false; }
        }

        private void ClearThisWave()
        {
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage == null) return;
            if (stage.State == eStageState.PREPARATION)
            {
                if (_bootstrap.TryBeginBattle()) { _killWave = true; _message = "전투를 시작하고 바로 클리어합니다."; }
                else _message = "전투를 시작하지 못했어요. 유닛을 한 기 이상 배치했는지 확인하세요.";
            }
            else if (stage.State == eStageState.COMBAT) { _killWave = true; _message = "남은 용사를 모두 쓰러뜨립니다."; }
            else _message = "지금은 클리어할 전투가 없어요(" + stage.State + ").";
        }

        /// <summary>입력한 웨이브 직전까지 깬 것으로 저장본을 써 두고, 같은 스테이지를 이어하기 경로로 다시 연다.</summary>
        private void JumpToWave(int wave)
        {
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            var progress = stage != null ? stage.Progress : null;
            var grid = _bootstrap != null && _bootstrap.GridSession != null ? _bootstrap.GridSession.Grid : null;
            if (progress == null || grid == null) { _message = "지금은 점프할 수 없어요(전투 준비 중)."; return; }
            if (wave < 1 || wave > stage.TotalRounds) { _message = "웨이브는 1 ~ " + stage.TotalRounds + " 사이로 넣어 주세요."; return; }

            string stageId = progress.StageId;
            if (wave > 1) UnitUnlockStore.UnlockForRound(wave - 1); // 건너뛴 5·10·15웨이브의 유닛 해금
            if (wave == 1) RunSaveStore.Clear(stageId);
            else
            {
                var hud = FindFirstObjectByType<InGameCurrencyHud>();
                var save = new RunSave { stageId = stageId, clearedRounds = wave - 1, currency = hud != null ? hud.Balance : 0, rewardStage = 0 };
                RunSnapshotCodec.Capture(save, grid);
                RunSnapshotCodec.CaptureAugments(save, _augRun);
                RunSaveStore.Save(save);
            }

            var session = StageLaunchRuntime.Session;
            if (session.Pending != null) session.Cancel(session.Pending.RequestId);
            string scenePath = gameObject.scene.path;
            if (!session.TryQueue(new StageLaunchRequest(stageId, scenePath))) { _message = "점프 요청을 만들지 못했어요."; return; }
            InGameRunSaver.SuppressSave = true;
            SceneManager.LoadScene(scenePath);
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_open) return;
            GUILayout.BeginArea(new Rect(8f, 8f, Width, Mathf.Min(Screen.height - 16f, 640f)), GUI.skin.box);
            GUILayout.Label("전투 디버그 (F8)");
            GUILayout.Label(_line);
            if (GUILayout.Button("이번 웨이브 클리어", GUILayout.Height(34f))) ClearThisWave();
            _auto = GUILayout.Toggle(_auto, " 자동 진행 (전투 시작 + 클리어 반복)");
            if (GUILayout.Button("증강 선택까지 자동 진행", GUILayout.Height(28f)))
            {
                _auto = true; _stopAtAugment = true;
                _message = "보스 웨이브를 깨고 증강 선택이 열릴 때까지 자동으로 진행합니다. 보상 카드는 직접 고르세요.";
            }
            GUILayout.Space(4f);
            GUILayout.Label("웨이브 점프 (지금 배치·재화·증강을 가지고 이동)");
            GUILayout.BeginHorizontal();
            _jumpText = GUILayout.TextField(_jumpText, 3, GUILayout.Width(60f), GUILayout.Height(26f));
            if (GUILayout.Button("이 웨이브로 이동", GUILayout.Height(26f)))
            {
                if (int.TryParse(_jumpText, out int wave)) JumpToWave(wave);
                else _message = "웨이브 번호를 숫자로 넣어 주세요.";
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_message)) GUILayout.Label(_message);

            if (_augRun != null)
            {
                GUILayout.Space(4f);
                GUILayout.Label("증강 직접 얻기 (" + _augRun.PickedCount + " / " + _augRun.TotalCount + ")");
                if (GUILayout.Button("증강 전부 초기화", GUILayout.Height(22f))) _augRun.ResetRun();
                _augScroll = GUILayout.BeginScrollView(_augScroll, GUILayout.Height(250f));
                for (int tier = 1; tier <= 3; tier++)
                {
                    GUILayout.Label("── " + AugmentData.TierName(tier) + " ──");
                    foreach (var d in _augRun.Pool)
                    {
                        if (d == null || d.tier != tier) continue;
                        bool maxed = _augRun.IsMaxed(d);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label((maxed ? "✔ " : string.Empty) + d.displayName, GUILayout.Width(210f));
                        GUI.enabled = !maxed;
                        if (GUILayout.Button("얻기", GUILayout.Width(60f))) _augRun.Pick(d);
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
#endif
        }
    }
}
