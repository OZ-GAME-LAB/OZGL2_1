using System.Collections.Generic;
using OZGL2.Grid;
using OZGL2.InGame;
using OZGL2.Progression;
using OZGL2.Stage;
using UnityEngine;

/// <summary>
/// 전투 흐름(전투 시작·보스 등장·승패·레벨업) 효과음 트리거. 코어루프/씬을 건드리지 않고
/// InGamePrototypeBootstrap의 읽기 전용 상태만 지켜보다가 바뀌는 순간에 Sfx를 울린다.
/// 씬 배치 없이 자동으로 붙는다(MawangXpBridge와 같은 패턴).
/// 유닛 단위 소리(공격·피격·사망·회복)는 UnitBase 쪽에서 직접 호출한다.
/// </summary>
internal sealed class BattleSfxWatcher : MonoBehaviour
{
    private const float BootstrapSearchInterval = 0.5f;

    private static BattleSfxWatcher _instance;

    private InGamePrototypeBootstrap _bootstrap;
    private float _nextSearchTime;
    private bool _inBattleScene;
    private eStageState _lastState = eStageState.IDLE;
    private string _lastResultRunId;
    private MawangLevel _subscribedMawang;
    private GridManager _grid;
    private ePlacementFailure _lastFailure;
    private readonly Dictionary<string, PieceState> _pieces = new Dictionary<string, PieceState>();

    private struct PieceState
    {
        public bool IsPlaced;
        public Vector2Int Anchor;
        public int Star;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (_instance != null) return;

        var go = new GameObject("BattleSfxWatcher");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<BattleSfxWatcher>();
    }

    private void OnDestroy()
    {
        if (_subscribedMawang != null) _subscribedMawang.LeveledUp -= OnLeveledUp;
        UnbindGrid();
    }

    private void Update()
    {
        SubscribeLevelUp();

        if (_bootstrap == null)
        {
            // 전투씬을 벗어났으면(부트스트랩이 사라짐) BGM을 끈다.
            if (_inBattleScene)
            {
                _inBattleScene = false;
                Sfx.StopBgm();
                UnbindGrid();
            }

            if (Time.unscaledTime < _nextSearchTime) return;
            _nextSearchTime = Time.unscaledTime + BootstrapSearchInterval;
            _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (_bootstrap == null)
            {
                _lastState = eStageState.IDLE;
                return;
            }
        }

        // 전투씬 진입 — 전투 BGM 시작(재시도로 같은 씬에 머무는 동안은 끊기지 않고 이어진다).
        if (!_inBattleScene)
        {
            _inBattleScene = true;
            Sfx.PlayBattleBgm();
        }

        BindGrid(_bootstrap.GridSession != null ? _bootstrap.GridSession.Grid : null);

        eStageState state = _bootstrap.Stage != null ? _bootstrap.Stage.State : eStageState.IDLE;
        if (state != _lastState)
        {
            if (state == eStageState.COMBAT)
            {
                Sfx.Play(SfxId.BattleStart);
                RoundDefinition round = _bootstrap.CurrentRoundDefinition;
                if (round != null && round.IsBossRound) Sfx.Play(SfxId.BossAppear);
            }
            else if (_lastState == eStageState.COMBAT &&
                     (state == eStageState.WAVE_RESULT || state == eStageState.GENERAL_REWARD))
            {
                // 전투 승리 직후에만 웨이브 클리어음 — 웨이브 결과창(WAVE_RESULT)이 있으면 그리로, 없으면 바로
                // 일반 보상으로 넘어간다. 패배/최종 정산(SETTLING)은 승패 효과음이 따로 담당.
                Sfx.Play(SfxId.RoundClear);
            }

            // 보상/증강 선택 창이 열릴 때와, 고르고 나서 다음 단계로 넘어갈 때.
            bool isReward = IsRewardState(state);
            bool wasReward = IsRewardState(_lastState);
            // 전투 직후 곧바로 열리는 경우는 웨이브 클리어음이 이미 났으니 겹치지 않게 생략.
            if (isReward && !wasReward && _lastState != eStageState.COMBAT) Sfx.Play(SfxId.RewardOpen);
            else if (wasReward && !isReward && state != eStageState.IDLE) Sfx.Play(SfxId.RewardPick);

            _lastState = state;
        }

        InGameRunResult result = _bootstrap.Result;
        if (result == null)
        {
            _lastResultRunId = null;
        }
        else if (result.RunId != _lastResultRunId)
        {
            _lastResultRunId = result.RunId;
            Sfx.Play(result.Progress.IsCleared ? SfxId.Victory : SfxId.Defeat);
        }
    }

    private static bool IsRewardState(eStageState state)
        => state == eStageState.GENERAL_REWARD || state == eStageState.AUGMENT;

    // ── 그리드(배치·합성) ──────────────────────────────────────────────
    // GridManager는 건드리지 않고 LayoutChanged/Changed를 읽기만 한다. 변경 전후 스냅샷을 비교해서
    // 무슨 일이 있었는지(새 기물·합성·배치·되돌리기) 판별한다.

    private void BindGrid(GridManager grid)
    {
        if (grid == _grid) return;

        UnbindGrid();
        _grid = grid;
        _pieces.Clear();
        _lastFailure = ePlacementFailure.NONE;
        if (_grid == null) return;

        SnapshotPieces();
        _grid.LayoutChanged += OnGridLayoutChanged;
        _grid.Changed += OnGridChanged;
    }

    private void UnbindGrid()
    {
        if (_grid == null) return;

        _grid.LayoutChanged -= OnGridLayoutChanged;
        _grid.Changed -= OnGridChanged;
        _grid = null;
    }

    private void SnapshotPieces()
    {
        _pieces.Clear();
        foreach (UnitPlacement unit in _grid.Units)
        {
            _pieces[unit.InstanceId] = new PieceState { IsPlaced = unit.IsPlaced, Anchor = unit.Anchor, Star = unit.StarLevel };
        }
        foreach (BlockPlacement block in _grid.Blocks)
        {
            _pieces[block.InstanceId] = new PieceState { IsPlaced = block.IsPlaced, Anchor = block.Anchor, Star = 0 };
        }
    }

    private void OnGridLayoutChanged()
    {
        if (_grid == null) return;

        // 준비 단계의 플레이어 조작만 소리를 낸다(전투 시작/종료 때 자동 정리되는 배치는 무시).
        bool audible = _grid.Phase == eGridPhase.PREPARATION || _grid.Phase == eGridPhase.REWARD;
        bool gained = false, fused = false, placed = false, returned = false;

        foreach (UnitPlacement unit in _grid.Units)
        {
            Classify(unit.InstanceId, unit.IsPlaced, unit.Anchor, unit.StarLevel, ref gained, ref fused, ref placed, ref returned);
        }
        foreach (BlockPlacement block in _grid.Blocks)
        {
            Classify(block.InstanceId, block.IsPlaced, block.Anchor, 0, ref gained, ref fused, ref placed, ref returned);
        }

        SnapshotPieces();
        if (!audible) return;

        if (fused) Sfx.Play(SfxId.Fusion);
        else if (placed) Sfx.Play(SfxId.UnitPlace);
        else if (returned) Sfx.Play(SfxId.UnitReturn);
        else if (gained) Sfx.Play(SfxId.UnitGain);
    }

    private void Classify(string id, bool isPlaced, Vector2Int anchor, int star,
        ref bool gained, ref bool fused, ref bool placed, ref bool returned)
    {
        if (!_pieces.TryGetValue(id, out PieceState before))
        {
            gained = true;
            return;
        }

        if (star > before.Star && before.Star > 0) fused = true;
        else if (isPlaced && (!before.IsPlaced || before.Anchor != anchor)) placed = true;
        else if (!isPlaced && before.IsPlaced) returned = true;
    }

    // 놓기 실패(자리 없음·겹침 등) — 실패 사유가 새로 생길 때 한 번만 거부음.
    private void OnGridChanged()
    {
        if (_grid == null) return;

        ePlacementFailure failure = _grid.LastDropFailure;
        if (failure == _lastFailure) return;

        _lastFailure = failure;
        if (failure == ePlacementFailure.NONE || failure == ePlacementFailure.NOT_PREPARING ||
            failure == ePlacementFailure.NO_SELECTION || failure == ePlacementFailure.OUTSIDE_BOUNDS)
        {
            return;
        }

        Sfx.Play(SfxId.UiError);
    }

    private void SubscribeLevelUp()
    {
        MawangLevel mawang = MawangXpBridge.Mawang;
        if (mawang == null || mawang == _subscribedMawang) return;

        if (_subscribedMawang != null) _subscribedMawang.LeveledUp -= OnLeveledUp;
        mawang.LeveledUp += OnLeveledUp;
        _subscribedMawang = mawang;
    }

    private static void OnLeveledUp(int newLevel) => Sfx.Play(SfxId.LevelUp);
}
