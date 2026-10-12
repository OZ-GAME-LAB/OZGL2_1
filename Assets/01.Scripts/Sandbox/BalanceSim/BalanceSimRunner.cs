using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using OZGL2.Augment;
using OZGL2.Grid;
using OZGL2.InGame;
using OZGL2.Stage;

namespace OZGL2.BalanceSim
{
    /// <summary>
    /// 밸런스 자동 시뮬레이터(방식 A). 실제 InGame 코어루프·전투 코드를 그대로 쓰고, 사람 대신 봇이 플레이한다.
    ///  · 준비: 영역 확장 놓기 → 합성 → 발판 깔기 → 유닛 배치(근접은 앞줄, 원거리·힐러는 뒷줄) → 전투 시작
    ///  · 보상: 일반 보상(유닛/영역 확장)과 증강을 정책대로 선택
    ///  · 결과: 라운드별 승패·전투 시간·생존 아군·성급 구성·바닥 크기를 CSV로 기록하고 난이도별 라운드 승률 요약을 만든다
    /// 보통 30 / 어려움 50을 plan 순서대로, 스테이지마다 runs번씩 반복한다(스테이지 진입은 실제 선택 경로(StageLaunchRequest)와 같다).
    /// 시간은 Time.captureDeltaTime으로 한 프레임=stepSeconds*timeScale 게임 초로 고정해서, 실제 시간보다 빠르게 돌려도 프레임 시간에 결과가 흔들리지 않는다.
    /// ※ 스킬 시전·특성 선택은 아직 넣지 않았다(1단계: 유닛·성급·보상·증강). 마왕 레벨/특성/해금 같은 저장(PlayerPrefs) 값은 현재 상태 그대로 쓰이고
    ///   시뮬 중 경험치·클리어 기록이 쌓이므로, 개인 저장이 중요하면 먼저 백업하거나 새 저장으로 돌릴 것.
    /// 사용: JOB_SEJIN_BalanceSim 씬을 열고 Play. 끝나면 콘솔과 persistentDataPath/BalanceSim/ 에 결과 경로가 출력된다.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class BalanceSimRunner : MonoBehaviour
    {
        [Serializable]
        public struct StagePlan
        {
            public string stageId;
            [Min(1)] public int runs;
            public StagePlan(string stageId, int runs) { this.stageId = stageId; this.runs = runs; }
        }

        [Header("시뮬레이션 계획 (순서대로, 스테이지마다 runs번)")]
        [SerializeField] private StagePlan[] plan = { new StagePlan("stage_normal_30", 5), new StagePlan("stage_hard_50", 5) };

        [Header("시간")]
        [Tooltip("한 프레임이 흘리는 게임 시간(초). 작을수록 정밀하고 느리다.")]
        [SerializeField, Min(0.005f)] private float stepSeconds = 0.04f;
        [Tooltip("프레임 시간 배율. 실효 스텝 = stepSeconds × timeScale. 1이 가장 정밀하다.")]
        [SerializeField, Min(0.1f)] private float timeScale = 1f;

        [Header("안전장치 / 정책")]
        [SerializeField] private float maxStateGameSeconds = 400f;
        [SerializeField] private int seed = 12345;
        [SerializeField] private bool muteAudio = true;
        [SerializeField] private bool quitEditorWhenDone = true;

        private static BalanceSimRunner _instance;

        // ── 실행 상태(씬을 다시 불러도 유지: 이 오브젝트가 DontDestroyOnLoad) ──
        private int _planIndex, _runIndex;
        private bool _finished;
        private string _outputDir;
        private readonly List<RoundRec> _allRounds = new List<RoundRec>();
        private readonly List<RunRec> _allRuns = new List<RunRec>();

        // ── 현재 런 상태 ──
        private InGamePrototypeBootstrap _bootstrap;
        private eStageState _lastState = eStageState.IDLE;
        private float _stateEnteredAt;
        private float _runStartedAt;
        private System.Random _rng;
        private readonly Dictionary<int, RoundRec> _roundRecs = new Dictionary<int, RoundRec>();
        private int _arrangedRound = -1, _prepAttempts;
        private float _nextPrepTime;
        private string _rewardHandledFor, _augmentHandledFor;
        private RoundRec _combat;
        private float _combatStart, _nextCombatSample, _combatStartHpSum;
        private bool _combatSnapshotTaken;
        private float _loadAt = -1f;

        private sealed class RoundRec
        {
            public string stage; public int run, round; public bool boss;
            public string outcome = "?";
            public float seconds;
            public int heroCount, allyStart, allyEnd, star1, star2, star3, floor;
            public float allyHpPct;
        }

        private sealed class RunRec
        {
            public string stage; public int run, totalRounds, clearedRounds; public string status; public float gameSeconds;
        }

        // ───────────────────────────────────────────── 수명

        private void Awake()
        {
            PatchBootstrap();

            if (_instance != null && _instance != this)
            {
                Destroy(gameObject); // 씬을 다시 불러올 때 생기는 복제본 — 지속 인스턴스가 이어서 진행한다
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _outputDir = Path.Combine(Application.persistentDataPath, "BalanceSim", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(_outputDir);
            ApplyTimeSettings();
            if (muteAudio) Sfx.Muted = true;
            BeginRun();
            Debug.Log($"[BalanceSim] 시작 — 출력 폴더: {_outputDir}");
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            _instance = null;
        }

        private void ApplyTimeSettings()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = stepSeconds;
            Time.timeScale = timeScale;
        }

        /// <summary>
        /// 장면의 웨이브 결과 확인창(사람이 눌러야 넘어가는 UI)은 시뮬에서 쓸 수 없으니 연결을 끊는다 —
        /// 연결 안 된 씬은 이 단계를 건너뛰는 것이 부트스트랩의 원래 동작이다. private 필드라 리플렉션으로 개발용 하니스에서만 건드린다.
        /// </summary>
        private static void PatchBootstrap()
        {
            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (bootstrap == null) return;

            FieldInfo field = typeof(InGamePrototypeBootstrap).GetField("_waveResultPresenter", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(bootstrap, null);
            else Debug.LogWarning("[BalanceSim] _waveResultPresenter 필드를 찾지 못했습니다(이름이 바뀌었는지 확인).");
        }

        // ───────────────────────────────────────────── 런 관리

        private string CurrentStageId => plan[_planIndex].stageId;

        private void BeginRun()
        {
            _bootstrap = null;
            _lastState = eStageState.IDLE;
            _stateEnteredAt = Time.time;
            _runStartedAt = Time.time;
            _rng = new System.Random(seed + _planIndex * 1000 + _runIndex);
            _roundRecs.Clear();
            _arrangedRound = -1; _prepAttempts = 0; _nextPrepTime = 0f;
            _rewardHandledFor = null; _augmentHandledFor = null;
            _combat = null;

            // 실제 스테이지 선택과 같은 경로로 이번 런의 스테이지를 요청한다(부트스트랩이 Start에서 소비).
            var session = StageLaunchRuntime.Session;
            if (session.Pending != null) session.Cancel(session.Pending.RequestId);
            session.TryQueue(new StageLaunchRequest(CurrentStageId, SceneManager.GetActiveScene().path));
        }

        private void FinishRun(string status, InGameRunResult result)
        {
            var run = new RunRec
            {
                stage = CurrentStageId, run = _runIndex + 1, status = status,
                gameSeconds = Time.time - _runStartedAt,
            };

            if (result != null)
            {
                run.totalRounds = result.TotalRounds;
                run.clearedRounds = result.Progress.ClearedRoundCount;
                foreach (RoundProgress progress in result.Progress.Rounds)
                {
                    if (!_roundRecs.TryGetValue(progress.RoundNumber, out RoundRec rec))
                    {
                        rec = new RoundRec { stage = CurrentStageId, run = _runIndex + 1, round = progress.RoundNumber };
                        _roundRecs[progress.RoundNumber] = rec;
                    }
                    rec.outcome = progress.Outcome.ToString();
                }
            }
            else if (_bootstrap != null && _bootstrap.Stage != null)
            {
                run.totalRounds = _bootstrap.Stage.TotalRounds;
                run.clearedRounds = Mathf.Max(0, _bootstrap.Stage.CurrentRoundNumber - 1);
            }

            var ordered = new List<RoundRec>(_roundRecs.Values);
            ordered.Sort((a, b) => a.round.CompareTo(b.round));
            _allRounds.AddRange(ordered);
            _allRuns.Add(run);
            Debug.Log($"[BalanceSim] {run.stage} {run.run}/{plan[_planIndex].runs}: {status} — 클리어 {run.clearedRounds}/{run.totalRounds}라운드, 게임 시간 {run.gameSeconds:0}s");
            WriteCsvFiles();

            _runIndex++;
            if (_runIndex >= plan[_planIndex].runs)
            {
                _planIndex++;
                _runIndex = 0;
            }

            if (_planIndex >= plan.Length)
            {
                AllDone();
                return;
            }

            BeginRun();
            _loadAt = Time.time + 0.2f; // 한 프레임 쉬고 씬을 다시 불러 깨끗한 상태로 다음 런을 시작한다
        }

        private void AllDone()
        {
            _finished = true;
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            WriteCsvFiles();
            Debug.Log("[BalanceSim] 전부 끝났습니다. 결과: " + _outputDir);
#if UNITY_EDITOR
            if (quitEditorWhenDone) UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ───────────────────────────────────────────── 매 프레임

        private void Update()
        {
            if (_finished) return;

            if (_loadAt > 0f)
            {
                if (Time.time >= _loadAt)
                {
                    _loadAt = -1f;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().path);
                }
                return;
            }

            if (_bootstrap == null)
            {
                _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
                if (_bootstrap == null) return;
            }

            if (_bootstrap.Error != null && _bootstrap.Result == null)
            {
                Debug.LogWarning("[BalanceSim] 부트스트랩 오류: " + _bootstrap.Error);
                FinishRun("ERROR", null);
                return;
            }

            if (_bootstrap.Result != null)
            {
                FinishRun(_bootstrap.Result.Progress.Status.ToString(), _bootstrap.Result);
                return;
            }

            StageManager stage = _bootstrap.Stage;
            if (stage == null) return;

            eStageState state = stage.State;
            if (state != _lastState)
            {
                if (_lastState == eStageState.COMBAT) EndCombat();
                _lastState = state;
                _stateEnteredAt = Time.time;
                if (state == eStageState.COMBAT) BeginCombat();
            }

            if (Time.time - _stateEnteredAt > maxStateGameSeconds || Time.time - _runStartedAt > maxStateGameSeconds * 12f)
            {
                Debug.LogWarning($"[BalanceSim] 상태 {state}에서 너무 오래 멈춰 있어 이번 런을 중단합니다(라운드 {stage.CurrentRoundNumber}).");
                FinishRun("TIMEOUT_" + state, null);
                return;
            }

            switch (state)
            {
                case eStageState.PREPARATION: HandlePreparation(stage); break;
                case eStageState.COMBAT: SampleCombat(); break;
                case eStageState.GENERAL_REWARD: HandleGeneralReward(); break;
                case eStageState.AUGMENT: HandleAugment(); break;
            }
        }

        // ───────────────────────────────────────────── 전투 기록

        private void BeginCombat()
        {
            StageManager stage = _bootstrap.Stage;
            var round = _bootstrap.CurrentRoundDefinition;
            var rec = new RoundRec { stage = CurrentStageId, run = _runIndex + 1, round = stage.CurrentRoundNumber };
            if (round != null)
            {
                rec.boss = round.IsBossRound;
                foreach (var spawn in round.Spawns) rec.heroCount += spawn.Count;
            }

            GridManager grid = _bootstrap.GridSession?.Grid;
            rec.floor = grid != null ? grid.FloorCells.Count : 0;

            // 아군은 전투 시작 직후(COMBAT 상태 진입 뒤)에 배치되므로 성급 구성·HP 합은 첫 샘플(0.5초 뒤)에 잡는다.
            _combat = rec;
            _combatStart = Time.time;
            _combatStartHpSum = 1f;
            _combatSnapshotTaken = false;
            _nextCombatSample = Time.time + 0.5f;
        }

        private void SampleCombat()
        {
            if (_combat == null || Time.time < _nextCombatSample) return;
            _nextCombatSample = Time.time + 0.25f;

            int alive = 0;
            float hp = 0f, maxHp = 0f;
            int s1 = 0, s2 = 0, s3 = 0;
            foreach (UnitBase unit in UnitRegistry.GetUnits(UnitSide.DemonArmy))
            {
                if (unit == null || unit.currentState == UnitState.Dead || unit.statData == null) continue;
                alive++;
                hp += Mathf.Max(0, unit.currentHealth);
                maxHp += unit.statData.maxHealth;
                if (unit.statData.starLevel >= 3) s3++;
                else if (unit.statData.starLevel == 2) s2++;
                else s1++;
            }

            if (!_combatSnapshotTaken)
            {
                if (alive == 0) return; // 아직 배치 전 — 다음 샘플에서 다시
                _combatSnapshotTaken = true;
                _combat.allyStart = alive;
                _combat.star1 = s1; _combat.star2 = s2; _combat.star3 = s3;
                _combatStartHpSum = Mathf.Max(1f, maxHp);
            }

            _combat.allyEnd = alive;
            _combat.allyHpPct = 100f * hp / _combatStartHpSum;
        }

        private void EndCombat()
        {
            if (_combat == null) return;
            _combat.seconds = Time.time - _combatStart;
            _roundRecs[_combat.round] = _combat;
            _combat = null;
        }

        // ───────────────────────────────────────────── 준비 단계(배치·합성·확장)

        private void HandlePreparation(StageManager stage)
        {
            GridRunSession session = _bootstrap.GridSession;
            if (session == null || session.Grid.Phase != eGridPhase.PREPARATION || Time.time < _nextPrepTime) return;

            int round = stage.CurrentRoundNumber;
            if (_arrangedRound != round)
            {
                _arrangedRound = round;
                _prepAttempts = 0;
            }

            _nextPrepTime = Time.time + 0.15f;
            if (++_prepAttempts > 30)
            {
                Debug.LogWarning($"[BalanceSim] 라운드 {round} 준비를 끝내지 못했습니다(배치 불가/보관함 문제).");
                FinishRun("STUCK_PREPARATION", null);
                return;
            }

            try
            {
                ArrangeGrid(session, session.Grid);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[BalanceSim] 배치 중 예외: " + exception);
            }

            if (session.Grid.CanBeginBattle) _bootstrap.CommitBattleStart();
        }

        private void ArrangeGrid(GridRunSession session, GridManager grid)
        {
            ResolvePendingStorage(session, grid);

            for (int guard = 0; grid.RequiresExpansionPlacement && guard < 4; guard++)
            {
                if (!PlaceExpansion(grid)) break;
            }

            FuseAll(grid);
            PlaceBlocks(grid);
            PlaceUnits(grid);
        }

        /// <summary>보관함이 넘쳐 폐기를 요구하면: 발판(남는 경우가 많다)부터, 그다음 1성 유닛을 버린다.</summary>
        private void ResolvePendingStorage(GridRunSession session, GridManager grid)
        {
            for (int guard = 0; grid.HasPendingStorage && guard < 4; guard++)
            {
                GridStorageRequest request = grid.PendingStorage;
                var discard = new List<GridStoredItem>();
                foreach (GridStoredItem item in request.DiscardCandidates)
                {
                    if (item.Kind == eGridDragKind.BLOCK && discard.Count < request.RequiredDiscardCount) discard.Add(item);
                }
                foreach (GridStoredItem item in request.DiscardCandidates)
                {
                    if (item.Kind == eGridDragKind.UNIT && discard.Count < request.RequiredDiscardCount) discard.Add(item);
                }

                if (!session.TryConfirmStorage(session.RunId, request.RequestId, discard)) break;
            }
        }

        private bool PlaceExpansion(GridManager grid)
        {
            FootprintDefinition shape = grid.PendingExpansionShape;
            var floor = new HashSet<Vector2Int>(grid.FloorCells);
            Vector2Int size = grid.Definition.MaximumSize;
            Vector2 center = new Vector2((size.x - 1) * 0.5f, (size.y - 1) * 0.5f);

            bool found = false;
            float bestScore = float.MaxValue;
            Vector2Int bestAnchor = default;
            int bestRotation = 0;

            for (int rotation = 0; rotation < 4; rotation++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    for (int y = 0; y < size.y; y++)
                    {
                        var anchor = new Vector2Int(x, y);
                        Vector2Int[] cells = shape.GetCells(anchor, rotation);
                        if (GridPlacementRules.ValidateExpansion(grid.Definition, floor, cells) != ePlacementFailure.NONE) continue;

                        float score = 0f; // 중앙에 가깝게 — 바닥이 한쪽으로만 길어지지 않게 compact하게 자란다
                        foreach (Vector2Int cell in cells) score += Mathf.Abs(cell.x - center.x) + Mathf.Abs(cell.y - center.y);
                        if (score < bestScore) { bestScore = score; bestAnchor = anchor; bestRotation = rotation; found = true; }
                    }
                }
            }

            if (!found || !grid.BeginExpansionDrag()) return false;

            for (int r = 0; r < bestRotation; r++) grid.RotatePreview();
            grid.MovePreview(bestAnchor);
            if (grid.CommitPreview()) return true;

            grid.CancelDrag();
            return false;
        }

        private static void FuseAll(GridManager grid)
        {
            for (int guard = 0; guard < 64; guard++)
            {
                UnitPlacement source = null, target = null;
                var seen = new Dictionary<string, UnitPlacement>();
                foreach (UnitPlacement unit in grid.Units)
                {
                    if (unit.StarLevel >= FusionRules.MaxStar) continue;
                    string key = unit.Definition.Id + "#" + unit.StarLevel;
                    if (seen.TryGetValue(key, out UnitPlacement other))
                    {
                        // 배치된 쪽을 남겨서 자리를 유지한다
                        target = other.IsPlaced || !unit.IsPlaced ? other : unit;
                        source = ReferenceEquals(target, other) ? unit : other;
                        break;
                    }
                    seen[key] = unit;
                }

                if (source == null || !grid.TryFuseUnits(source.InstanceId, target.InstanceId)) return;
            }
        }

        private static void PlaceBlocks(GridManager grid)
        {
            foreach (BlockPlacement block in new List<BlockPlacement>(grid.Blocks))
            {
                if (block.IsPlaced) continue;

                var floor = new HashSet<Vector2Int>(grid.FloorCells);
                Vector2Int size = grid.Definition.MaximumSize;
                Vector2 center = new Vector2((size.x - 1) * 0.5f, (size.y - 1) * 0.5f);
                bool found = false;
                float bestScore = float.MaxValue;
                Vector2Int bestAnchor = default;
                int bestRotation = 0;
                bool bestMirror = false;
                var tried = new HashSet<string>();

                for (int rotation = 0; rotation < 4; rotation++)
                {
                    for (int mirror = 0; mirror < 2; mirror++)
                    {
                        Vector2Int[] offsets = block.Footprint.GetCells(Vector2Int.zero, rotation, mirror == 1);
                        if (!tried.Add(ShapeKey(offsets))) continue;

                        for (int x = 0; x < size.x; x++)
                        {
                            for (int y = 0; y < size.y; y++)
                            {
                                var anchor = new Vector2Int(x, y);
                                Vector2Int[] cells = Offset(offsets, anchor);
                                if (GridPlacementRules.ValidateBlock(grid.Definition, floor, grid.Blocks, block.InstanceId, cells) != ePlacementFailure.NONE) continue;

                                float score = 0f;
                                foreach (Vector2Int cell in cells) score += Mathf.Abs(cell.x - center.x) + Mathf.Abs(cell.y - center.y);
                                if (score < bestScore) { bestScore = score; bestAnchor = anchor; bestRotation = rotation; bestMirror = mirror == 1; found = true; }
                            }
                        }
                    }
                }

                if (!found || !grid.BeginBlockDrag(block.InstanceId)) continue;
                SetPreviewOrientation(grid, bestRotation, bestMirror);
                grid.MovePreview(bestAnchor);
                if (!grid.CommitPreview()) grid.CancelDrag();
            }
        }

        private static void PlaceUnits(GridManager grid)
        {
            var invalid = new HashSet<Vector2Int>(grid.GetInvalidCells());
            var todo = new List<UnitPlacement>();
            foreach (UnitPlacement unit in grid.Units)
            {
                bool needs = !unit.IsPlaced;
                if (!needs && invalid.Count > 0)
                {
                    foreach (Vector2Int cell in unit.GetCells())
                    {
                        if (invalid.Contains(cell)) { needs = true; break; }
                    }
                }
                if (needs) todo.Add(unit);
            }

            // 큰 성급부터(자리를 많이 차지하니 먼저), 같으면 근접 먼저
            todo.Sort((a, b) =>
            {
                int byStar = b.StarLevel.CompareTo(a.StarLevel);
                return byStar != 0 ? byStar : RoleFrontness(b.Definition.Id).CompareTo(RoleFrontness(a.Definition.Id));
            });

            foreach (UnitPlacement unit in todo)
            {
                UnitPlacement current = grid.FindUnit(unit.InstanceId);
                if (current != null) PlaceUnit(grid, current);
            }
        }

        private static void PlaceUnit(GridManager grid, UnitPlacement unit)
        {
            FootprintDefinition shape = unit.Definition.GetFootprint(unit.StarLevel);
            Vector2Int size = grid.Definition.MaximumSize;
            float frontness = RoleFrontness(unit.Definition.Id); // +면 앞줄(높은 y), -면 뒷줄
            bool found = false;
            float bestScore = float.MaxValue;
            Vector2Int bestAnchor = default;
            int bestRotation = 0;
            bool bestMirror = false;
            var tried = new HashSet<string>();

            for (int rotation = 0; rotation < 4; rotation++)
            {
                for (int mirror = 0; mirror < 2; mirror++)
                {
                    Vector2Int[] offsets = shape.GetCells(Vector2Int.zero, rotation, mirror == 1);
                    if (!tried.Add(ShapeKey(offsets))) continue;

                    for (int x = 0; x < size.x; x++)
                    {
                        for (int y = 0; y < size.y; y++)
                        {
                            var anchor = new Vector2Int(x, y);
                            Vector2Int[] cells = Offset(offsets, anchor);
                            if (GridPlacementRules.ValidateUnit(grid.Definition, grid.Blocks, grid.Units, unit.InstanceId, cells) != ePlacementFailure.NONE) continue;

                            float meanY = 0f, meanX = 0f;
                            foreach (Vector2Int cell in cells) { meanY += cell.y; meanX += cell.x; }
                            meanY /= cells.Length; meanX /= cells.Length;
                            float score = -frontness * meanY + 0.1f * Mathf.Abs(meanX - (size.x - 1) * 0.5f);
                            if (score < bestScore) { bestScore = score; bestAnchor = anchor; bestRotation = rotation; bestMirror = mirror == 1; found = true; }
                        }
                    }
                }
            }

            if (!found || !grid.BeginUnitDrag(unit.InstanceId)) return;
            SetPreviewOrientation(grid, bestRotation, bestMirror);
            grid.MovePreview(bestAnchor);
            if (!grid.CommitPreview()) grid.CancelDrag();
        }

        /// <summary>미리보기를 (회전 0, 반전 없음)으로 되돌린 뒤 원하는 방향을 만든다. 저장 규격은 "반전 후 회전"이다.</summary>
        private static void SetPreviewOrientation(GridManager grid, int rotation, bool mirrored)
        {
            if (grid.PreviewIsMirrored) grid.MirrorPreview();
            for (int guard = 0; grid.PreviewRotation != 0 && guard < 4; guard++) grid.RotatePreview();
            if (mirrored) grid.MirrorPreview();
            for (int r = 0; r < rotation; r++) grid.RotatePreview();
        }

        private static Vector2Int[] Offset(Vector2Int[] offsets, Vector2Int anchor)
        {
            var cells = new Vector2Int[offsets.Length];
            for (int i = 0; i < offsets.Length; i++) cells[i] = offsets[i] + anchor;
            return cells;
        }

        private static string ShapeKey(Vector2Int[] cells)
        {
            var sorted = new List<Vector2Int>(cells);
            sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return string.Join(";", sorted);
        }

        /// <summary>앞줄 선호도: 방패병 > 전사 > 도적 > (중간) 궁수 > 마법사 > 힐러(가장 뒤).</summary>
        private static float RoleFrontness(string unitId)
        {
            if (unitId.Contains("SHD")) return 3f;
            if (unitId.Contains("WAR")) return 2f;
            if (unitId.Contains("ROG")) return 1f;
            if (unitId.Contains("ARC")) return -1f;
            if (unitId.Contains("MAG")) return -2f;
            if (unitId.Contains("HEL")) return -3f;
            return 0f;
        }

        // ───────────────────────────────────────────── 보상·증강

        private void HandleGeneralReward()
        {
            StageGridRewards rewards = _bootstrap.Rewards;
            if (rewards == null || rewards.Pending == null) return;

            string requestId = rewards.Pending.RequestId;
            if (_rewardHandledFor == requestId) return;

            GridRunSession session = _bootstrap.GridSession;
            if (session == null) return;

            int option = ChooseReward(session.Grid, rewards.Candidates);
            if (rewards.TrySelect(requestId, option))
            {
                _rewardHandledFor = requestId;
                return;
            }

            // 보관함이 가득 차 폐기 확인이 필요한 경우를 처리하고 다음 프레임에 다시 시도한다.
            ResolvePendingStorage(session, session.Grid);
        }

        /// <summary>일반 보상 선택 정책: 자리가 모자라면 영역 확장, 아니면 합성을 완성해 주는/없는 직업의 유닛을 우선.</summary>
        private int ChooseReward(GridManager grid, IReadOnlyList<GeneralRewardOption> candidates)
        {
            int unitCells = 0;
            var ownedByStar = new Dictionary<string, int>();
            var ownedById = new Dictionary<string, int>();
            foreach (UnitPlacement unit in grid.Units)
            {
                unitCells += unit.Definition.GetFootprint(unit.StarLevel).Cells.Count;
                string key = unit.Definition.Id + "#" + unit.StarLevel;
                ownedByStar[key] = ownedByStar.TryGetValue(key, out int n) ? n + 1 : 1;
                ownedById[unit.Definition.Id] = ownedById.TryGetValue(unit.Definition.Id, out int m) ? m + 1 : 1;
            }

            int floor = grid.FloorCells.Count;
            int maxCells = grid.Definition.MaximumSize.x * grid.Definition.MaximumSize.y;
            int expansionIndex = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Kind == eGeneralRewardKind.EXPANSION) expansionIndex = i;
            }

            // 유닛이 차지하는 칸이 바닥에 거의 찼으면 확장(바닥이 이미 꽉 차 있으면 유닛)
            if (expansionIndex >= 0 && floor < maxCells && unitCells + 3 >= floor) return expansionIndex;

            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Kind != eGeneralRewardKind.UNIT) continue;

                string id = candidates[i].Unit.Id;
                float score = 0f;
                if (ownedByStar.TryGetValue(id + "#1", out int one) && one % 2 == 1) score += 3f;      // 1성 짝을 맞춰 2성으로
                if (ownedByStar.TryGetValue(id + "#2", out int two) && two % 2 == 1) score += 2f;      // 2성 짝을 맞춰 3성으로
                if (!ownedById.ContainsKey(id)) score += 1.5f;                                           // 아직 없는 직업
                score += (float)_rng.NextDouble() * 0.1f;
                if (score > bestScore) { bestScore = score; best = i; }
            }

            return best >= 0 ? best : Mathf.Max(0, expansionIndex);
        }

        private void HandleAugment()
        {
            var provider = FindFirstObjectByType<InGameAugmentRewards>();
            if (provider == null || !provider.IsPending) return;

            string requestId = provider.RequestId;
            if (_augmentHandledFor == requestId) return;

            IReadOnlyList<AugmentData> candidates = provider.Candidates;
            if (candidates.Count == 0) return;

            // 증강 정책: 무작위(시드 고정). 나중에 "전투 빌드 우선" 같은 정책을 갈아끼울 수 있다.
            AugmentData pick = candidates[_rng.Next(candidates.Count)];
            if (provider.TrySelect(requestId, pick)) _augmentHandledFor = requestId;
        }

        // ───────────────────────────────────────────── 결과 기록

        private void WriteCsvFiles()
        {
            if (string.IsNullOrEmpty(_outputDir)) return;

            var rounds = new StringBuilder();
            rounds.AppendLine("stage,run,round,boss,outcome,seconds,heroCount,allyStart,allyEnd,allyHpPct,star1,star2,star3,floorCells");
            foreach (RoundRec r in _allRounds)
            {
                rounds.AppendLine(string.Join(",", r.stage, r.run, r.round, r.boss ? 1 : 0, r.outcome,
                    F(r.seconds), r.heroCount, r.allyStart, r.allyEnd, F(r.allyHpPct), r.star1, r.star2, r.star3, r.floor));
            }
            File.WriteAllText(Path.Combine(_outputDir, "rounds.csv"), rounds.ToString(), new UTF8Encoding(true));

            var runs = new StringBuilder();
            runs.AppendLine("stage,run,status,clearedRounds,totalRounds,gameSeconds");
            foreach (RunRec r in _allRuns)
                runs.AppendLine(string.Join(",", r.stage, r.run, r.status, r.clearedRounds, r.totalRounds, F(r.gameSeconds)));
            File.WriteAllText(Path.Combine(_outputDir, "runs.csv"), runs.ToString(), new UTF8Encoding(true));

            var summary = new StringBuilder();
            summary.AppendLine("stage,round,boss,attempts,wins,winRate,avgSeconds,avgAllyHpPctOnWin,avgAllyUnits,avgStar2,avgStar3,avgFloorCells");
            var keys = new SortedDictionary<string, SortedDictionary<int, List<RoundRec>>>(StringComparer.Ordinal);
            foreach (RoundRec r in _allRounds)
            {
                if (r.outcome != "VICTORY" && r.outcome != "DEFEAT") continue;
                if (!keys.TryGetValue(r.stage, out var byRound)) keys[r.stage] = byRound = new SortedDictionary<int, List<RoundRec>>();
                if (!byRound.TryGetValue(r.round, out var list)) byRound[r.round] = list = new List<RoundRec>();
                list.Add(r);
            }

            foreach (var stagePair in keys)
            {
                foreach (var roundPair in stagePair.Value)
                {
                    List<RoundRec> list = roundPair.Value;
                    int wins = 0; float secs = 0, hpWin = 0, units = 0, s2 = 0, s3 = 0, floor = 0; bool boss = false;
                    foreach (RoundRec r in list)
                    {
                        boss |= r.boss; secs += r.seconds; units += r.allyStart; s2 += r.star2; s3 += r.star3; floor += r.floor;
                        if (r.outcome == "VICTORY") { wins++; hpWin += r.allyHpPct; }
                    }
                    int n = list.Count;
                    summary.AppendLine(string.Join(",", stagePair.Key, roundPair.Key, boss ? 1 : 0, n, wins, F((float)wins / n),
                        F(secs / n), F(wins > 0 ? hpWin / wins : 0f), F(units / n), F(s2 / n), F(s3 / n), F(floor / n)));
                }
            }
            File.WriteAllText(Path.Combine(_outputDir, "summary.csv"), summary.ToString(), new UTF8Encoding(true));
        }

        private static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        // ───────────────────────────────────────────── 화면 표시

        private void OnGUI()
        {
            if (_finished)
            {
                GUI.Label(new Rect(10, 10, 700, 24), "[BalanceSim] 완료 — " + _outputDir);
                return;
            }

            string stateText = _bootstrap != null && _bootstrap.Stage != null
                ? $"{_bootstrap.Stage.State} R{_bootstrap.Stage.CurrentRoundNumber}/{_bootstrap.Stage.TotalRounds}"
                : "대기";
            string stageText = _planIndex < plan.Length ? $"{plan[_planIndex].stageId} {_runIndex + 1}/{plan[_planIndex].runs}" : "-";
            GUI.Label(new Rect(10, 10, 700, 24), $"[BalanceSim] {stageText}  {stateText}  게임시간 {Time.time:0}s  FPS {1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime):0}");
        }
    }
}
