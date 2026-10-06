using System;
using System.Collections.Generic;
using OZGL2.Augment;
using OZGL2.InGame;
using OZGL2.Progression;
using OZGL2.Stage;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>
    /// 전투 UI를 InGamePrototype의 읽기 모델과 명령에 연결한다.
    /// UI는 전투/보상 데이터를 소유하지 않으며, 미리보기 전용 값은 실제 값이 있는 범위에서만 덮어쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIInGameBattleBridge : MonoBehaviour
    {
        [Header("전투 화면")]
        [SerializeField] private UIPageGroup _pageGroup;
        [SerializeField] private GameObject _preparationPage;
        [SerializeField] private GameObject _combatPage;
        [SerializeField] private UIBattleMutedPreviewView _hudView;
        [SerializeField] private Button _startBattleButton;

        [Header("팝업")]
        [SerializeField] private UIPopupController _popupController;
        [SerializeField] private UIAugmentSelectionView _augmentView;
        [SerializeField] private UIPopupPanel _augmentPopup;
        [SerializeField] private UIBattleResultView _victoryView;
        [SerializeField] private UIPopupPanel _victoryPopup;
        [SerializeField] private UIBattleResultView _defeatView;
        [SerializeField] private UIPopupPanel _defeatPopup;
        [SerializeField] private UISceneNavigator _navigator;
        [SerializeField] private string _lobbyScenePath = "Assets/00.Scenes/Builds/Lobby.unity";

        [Header("InGame 연결 (비어 있으면 부모에서 자동 탐색)")]
        [SerializeField] private InGamePrototypeBootstrap _bootstrap;
        [SerializeField] private InGamePhasePresentation _phasePresentation;
        [SerializeField] private InGameAugmentRewards _augmentProvider;

        private string _shownAugmentRequestId;
        private string _shownResultRunId;
        private eStageState _shownStageState = eStageState.IDLE;
        private bool _hasShownStageState;
        private bool _hasSubscribedXp;
        private string _trackedRunId;
        private bool _hasStartedRunTimer;
        private bool _hasFrozenRunTimer;
        private double _runTimerStartedAt;
        private float _elapsedRunSeconds;
        private int _shownElapsedSecond = -1;
        private int _runStartLevel = 1;
        private int _killCount;
        private int _maximumDeploymentCount;
        private string _shownEnemyStageId;
        private int _shownEnemyRound;
        private string _remainingEnemyRunId;
        private int _remainingEnemyRound;
        private int _remainingEnemyTotal;
        private readonly HashSet<long> _countedHeroDeaths = new HashSet<long>();

        public void Configure(
            UIPageGroup pageGroup,
            GameObject preparationPage,
            GameObject combatPage,
            UIBattleMutedPreviewView hudView,
            Button startBattleButton,
            UIPopupController popupController,
            UIAugmentSelectionView augmentView,
            UIPopupPanel augmentPopup,
            UIBattleResultView victoryView,
            UIPopupPanel victoryPopup,
            UIBattleResultView defeatView,
            UIPopupPanel defeatPopup,
            UISceneNavigator navigator,
            string lobbyScenePath)
        {
            _pageGroup = pageGroup;
            _preparationPage = preparationPage;
            _combatPage = combatPage;
            _hudView = hudView;
            _startBattleButton = startBattleButton;
            _popupController = popupController;
            _augmentView = augmentView;
            _augmentPopup = augmentPopup;
            _victoryView = victoryView;
            _victoryPopup = victoryPopup;
            _defeatView = defeatView;
            _defeatPopup = defeatPopup;
            _navigator = navigator;
            _lobbyScenePath = lobbyScenePath;
        }

        /// <summary>Scene에 분리 배치된 UI와 InGame Runtime 소스를 명시적으로 연결한다.</summary>
        public void ConfigureRuntimeSources(
            InGamePrototypeBootstrap bootstrap,
            InGamePhasePresentation phasePresentation,
            InGameAugmentRewards augmentProvider)
        {
            bool isRuntimeActive = Application.isPlaying && isActiveAndEnabled;
            bool isInputBlocked = _popupController != null && _popupController.OpenCount > 0;
            if (isRuntimeActive)
            {
                if (_bootstrap != null) _bootstrap.Changed -= Refresh;
                _phasePresentation?.SetExternalInputBlocked(false);
                _bootstrap?.SetUiInputBlocked(false);
            }

            _bootstrap = bootstrap;
            _phasePresentation = phasePresentation;
            _augmentProvider = augmentProvider;

            if (isRuntimeActive)
            {
                if (_bootstrap != null) _bootstrap.Changed += Refresh;
                SetInputBlocked(isInputBlocked);
                Refresh();
            }
        }

        /// <summary>Button의 영구 이벤트에서 호출하는 전투 시작 진입점.</summary>
        public void RequestBattle()
        {
            ResolveRuntimeReferences();
            if (_bootstrap == null || !_bootstrap.TryBeginBattle()) Refresh();
        }

        public bool RetryResult()
        {
            InGameRunResult result = _bootstrap != null ? _bootstrap.Result : null;
            if (result == null || !_bootstrap.TryRetryResult(result.RunId)) return false;
            return true;
        }

        public bool ReturnToStageSelection()
        {
            InGameRunResult result = _bootstrap != null ? _bootstrap.Result : null;
            return result != null && _bootstrap.TryReturnToStageSelection(result.RunId);
        }

        public bool ReturnToLobby()
        {
            InGameRunResult result = _bootstrap != null ? _bootstrap.Result : null;
            return result != null && _bootstrap.TryReturnToLobby(result.RunId);
        }

        private void Awake()
        {
            ResolveRuntimeReferences();
            ConfigureResultHandlers();
        }

        private void OnEnable()
        {
            ResolveRuntimeReferences();
            if (_bootstrap != null) _bootstrap.Changed += Refresh;
            if (_popupController != null) _popupController.OpenCountChanged += HandlePopupCountChanged;
            if (_augmentView != null) _augmentView.ConfigureSelectionHandler(TrySelectAugment);
            UnitBase.OnHeroKilled += HandleHeroKilled;
            ConfigureResultHandlers();
            SubscribeXp();
            HandlePopupCountChanged(_popupController != null ? _popupController.OpenCount : 0);
            Refresh();
        }

        private void OnDisable()
        {
            if (_bootstrap != null) _bootstrap.Changed -= Refresh;
            if (_popupController != null) _popupController.OpenCountChanged -= HandlePopupCountChanged;
            if (_augmentView != null) _augmentView.ConfigureSelectionHandler(null);
            UnitBase.OnHeroKilled -= HandleHeroKilled;
            UnsubscribeXp();
            SetInputBlocked(false);
            _shownAugmentRequestId = null;
            _shownResultRunId = null;
            _hasShownStageState = false;
        }

        private void LateUpdate()
        {
            // 카메라 전환 종료와 증강 대기 시작은 Bootstrap Changed와 같은 프레임에 오지 않을 수 있다.
            RefreshTimer();
            RefreshStartButton();
            RefreshAugmentPopup();
            if (!_hasSubscribedXp) SubscribeXp();
        }

        private void ResolveRuntimeReferences()
        {
            if (_bootstrap == null) _bootstrap = GetComponentInParent<InGamePrototypeBootstrap>(true);
            if (_phasePresentation == null) _phasePresentation = GetComponentInParent<InGamePhasePresentation>(true);
            if (_augmentProvider == null) _augmentProvider = GetComponentInParent<InGameAugmentRewards>(true);
        }

        private void Refresh()
        {
            ResolveRuntimeReferences();
            RefreshRunIdentity();
            RefreshPage();
            RefreshHud();
            RefreshEnemyPreview();
            RefreshRemainingEnemyCount();
            RefreshTimer();
            RefreshStartButton();
            RefreshAugmentPopup();
            RefreshResultPopup();
        }

        private void RefreshRunIdentity()
        {
            string runId = _bootstrap?.GridSession?.RunId ?? _bootstrap?.Result?.RunId;
            if (string.IsNullOrEmpty(runId) || runId == _trackedRunId) return;

            _trackedRunId = runId;
            _hasStartedRunTimer = false;
            _hasFrozenRunTimer = false;
            _elapsedRunSeconds = 0f;
            _shownElapsedSecond = -1;
            _killCount = 0;
            _maximumDeploymentCount = 0;
            _shownEnemyStageId = null;
            _shownEnemyRound = 0;
            _runStartLevel = MawangXpBridge.Mawang?.Level ?? 1;
            RefreshElapsedTimeDisplay(true);
        }

        private void RefreshPage()
        {
            if (_pageGroup == null || _preparationPage == null || _combatPage == null) return;

            eStageState state = _bootstrap != null && _bootstrap.Stage != null
                ? _bootstrap.Stage.State
                : eStageState.IDLE;
            if (_hasShownStageState && _shownStageState == state) return;

            _shownStageState = state;
            _hasShownStageState = true;
            bool showPreparation = state == eStageState.IDLE ||
                state == eStageState.INITIALIZING ||
                state == eStageState.PREPARATION ||
                state == eStageState.GENERAL_REWARD ||
                state == eStageState.AUGMENT;
            _pageGroup.ShowPage(showPreparation ? _preparationPage : _combatPage);
        }

        private void RefreshHud()
        {
            if (_hudView == null) return;

            if (_bootstrap != null && _bootstrap.Stage != null)
                _hudView.SetWave(_bootstrap.Stage.CurrentRoundNumber, _bootstrap.Stage.TotalRounds);

            MawangLevel mawang = MawangXpBridge.Mawang;
            if (mawang == null) return;
            float normalizedXp = mawang.XpToNext > 0 ? (float)mawang.Xp / mawang.XpToNext : 0f;
            _hudView.SetLevelExperience(mawang.Level, normalizedXp);
        }

        private void RefreshEnemyPreview()
        {
            if (_hudView == null || _bootstrap == null) return;
            string stageId = _bootstrap.SelectedStageId;
            int roundNumber = _bootstrap.Stage != null ? _bootstrap.Stage.CurrentRoundNumber : 0;
            if (_shownEnemyStageId == stageId && _shownEnemyRound == roundNumber) return;

            _shownEnemyStageId = stageId;
            _shownEnemyRound = roundNumber;
            int slotCount = _hudView.EnemySlotCount;
            for (int i = 0; i < slotCount; i++) _hudView.SetEnemy(i, string.Empty, 0);

            RoundDefinition round = _bootstrap.CurrentRoundDefinition;
            if (round == null || slotCount == 0) return;

            var rows = new List<EnemyRow>();
            var rowById = new Dictionary<string, EnemyRow>(StringComparer.Ordinal);
            foreach (HeroSpawnDefinition spawn in round.Spawns)
            {
                if (!rowById.TryGetValue(spawn.HeroId, out EnemyRow row))
                {
                    row = new EnemyRow(ResolveHeroDisplayName(spawn.HeroId));
                    rowById.Add(spawn.HeroId, row);
                    rows.Add(row);
                }
                row.Count += spawn.Count;
            }

            int directCount = rows.Count <= slotCount ? rows.Count : Mathf.Max(0, slotCount - 1);
            for (int i = 0; i < directCount; i++)
                _hudView.SetEnemy(i, rows[i].DisplayName, rows[i].Count);

            if (rows.Count > slotCount)
            {
                int remainingCount = 0;
                for (int i = directCount; i < rows.Count; i++) remainingCount += rows[i].Count;
                _hudView.SetEnemy(slotCount - 1, "기타 " + (rows.Count - directCount) + "종", remainingCount);
            }
        }

        private string ResolveHeroDisplayName(string heroId)
        {
            HeroPoolCatalogSO catalog = _bootstrap?.Config?.HeroPoolCatalog;
            if (catalog == null) return heroId;

            foreach (HeroPoolEntry entry in catalog.CreateSnapshot())
            {
                if (entry == null || entry.HeroId != heroId || entry.Prefab == null) continue;
                UnitBase unit = entry.Prefab.GetComponentInChildren<UnitBase>(true);
                if (unit != null && unit.statData != null && !string.IsNullOrWhiteSpace(unit.statData.displayName))
                    return unit.statData.displayName;
            }
            return heroId;
        }

        private void RefreshRemainingEnemyCount()
        {
            string runId = _bootstrap?.GridSession?.RunId;
            int roundNumber = _bootstrap?.Stage?.CurrentRoundNumber ?? 0;
            RoundDefinition round = _bootstrap?.CurrentRoundDefinition;
            if (string.IsNullOrEmpty(runId) || roundNumber <= 0 || round == null)
            {
                _remainingEnemyRunId = null;
                _remainingEnemyRound = 0;
                _remainingEnemyTotal = 0;
                _countedHeroDeaths.Clear();
                _hudView?.SetRemainingEnemyCount(0);
                return;
            }

            if (_remainingEnemyRunId != runId || _remainingEnemyRound != roundNumber)
            {
                _remainingEnemyRunId = runId;
                _remainingEnemyRound = roundNumber;
                _remainingEnemyTotal = 0;
                _countedHeroDeaths.Clear();
                foreach (HeroSpawnDefinition spawn in round.Spawns)
                {
                    if (spawn == null) continue;
                    _remainingEnemyTotal = (int)Math.Min(int.MaxValue,
                        (long)_remainingEnemyTotal + Math.Max(0, spawn.Count));
                }
            }

            // 아직 스폰되지 않은 용사도 포함한다. 승패 집계 밖의 보스 증원은 제외한다.
            _hudView?.SetRemainingEnemyCount(Mathf.Max(0, _remainingEnemyTotal - _countedHeroDeaths.Count));
        }

        private void RefreshTimer()
        {
            if (_hudView == null) return;
            RefreshRunIdentity();

            eStageState state = _bootstrap != null && _bootstrap.Stage != null
                ? _bootstrap.Stage.State
                : eStageState.IDLE;
            if (!_hasStartedRunTimer && state == eStageState.COMBAT)
            {
                _hasStartedRunTimer = true;
                _runTimerStartedAt = Time.realtimeSinceStartupAsDouble;
                _runStartLevel = MawangXpBridge.Mawang?.Level ?? _runStartLevel;
                _elapsedRunSeconds = 0f;
            }

            if (_hasStartedRunTimer && !_hasFrozenRunTimer)
            {
                _elapsedRunSeconds = (float)Math.Max(0d,
                    Time.realtimeSinceStartupAsDouble - _runTimerStartedAt);
                if (IsFinalSettlementState(state)) _hasFrozenRunTimer = true;
            }

            if (state == eStageState.COMBAT && _bootstrap?.GridSession?.Deployment != null)
                _maximumDeploymentCount = Mathf.Max(
                    _maximumDeploymentCount,
                    _bootstrap.GridSession.Deployment.Units.Count);

            RefreshElapsedTimeDisplay(false);
        }

        private void RefreshStartButton()
        {
            if (_startBattleButton == null) return;

            bool canStart = _bootstrap != null &&
                _bootstrap.Stage != null &&
                _bootstrap.Stage.State == eStageState.PREPARATION &&
                _bootstrap.GridSession != null &&
                _bootstrap.GridSession.Grid.CanBeginBattle &&
                (_phasePresentation == null || _phasePresentation.CanInteract);
            _startBattleButton.interactable = canStart;
        }

        private void RefreshAugmentPopup()
        {
            if (_augmentProvider == null || _augmentView == null ||
                _augmentPopup == null || _popupController == null) return;

            if (!_augmentProvider.IsPending)
            {
                _shownAugmentRequestId = null;
                if (_popupController.IsTopPopup(_augmentPopup))
                    _popupController.CloseConfirmedPopup();
                return;
            }

            string requestId = _augmentProvider.RequestId;
            if (_shownAugmentRequestId == requestId) return;

            _shownAugmentRequestId = requestId;
            _augmentView.SetChoices(_augmentProvider.Candidates);
            _popupController.OpenPopup(_augmentPopup);
        }

        private bool TrySelectAugment(AugmentData data)
        {
            if (_augmentProvider == null || data == null || !_augmentProvider.IsPending) return false;
            string requestId = _augmentProvider.RequestId;
            if (string.IsNullOrEmpty(requestId) || requestId != _shownAugmentRequestId) return false;
            return _augmentProvider.TrySelect(requestId, data);
        }

        private void RefreshResultPopup()
        {
            InGameRunResult result = _bootstrap != null ? _bootstrap.Result : null;
            if (result == null)
            {
                _shownResultRunId = null;
                // 더미 UI 등 다른 진입점에서 재도전해도 결과 상태 해제와 화면을 동기화한다.
                _popupController?.CloseResolvedPopup(_victoryPopup);
                _popupController?.CloseResolvedPopup(_defeatPopup);
                return;
            }
            if (_shownResultRunId == result.RunId || _popupController == null) return;

            RefreshTimer();
            _shownResultRunId = result.RunId;
            bool isVictory = result.Progress.IsCleared;
            UIBattleResultView view = isVictory ? _victoryView : _defeatView;
            UIPopupPanel popup = isVictory ? _victoryPopup : _defeatPopup;
            if (view == null || popup == null) return;

            MawangLevel mawang = MawangXpBridge.Mawang;
            int xpToNext = mawang != null ? mawang.XpToNext : 1;
            float normalizedXp = xpToNext > 0 ? (float)result.CurrentLevelXp / xpToNext : 0f;
            int earnedXp = result.Progress.EarnedExperience > int.MaxValue
                ? int.MaxValue
                : (int)result.Progress.EarnedExperience;
            view.SetState(isVictory ? eBattleResultState.VICTORY : eBattleResultState.DEFEAT);
            view.SetData(new BattleResultDisplayData(
                _bootstrap.Config?.StageCatalog?.GetDisplayName(result.Progress.StageId) ?? result.Progress.StageId,
                _elapsedRunSeconds,
                _killCount,
                _maximumDeploymentCount,
                earnedXp,
                result.Level,
                normalizedXp,
                result.Level > _runStartLevel));
            _popupController.OpenPopup(popup);
        }

        private void RefreshElapsedTimeDisplay(bool force)
        {
            if (_hudView == null) return;
            int elapsedSecond = Mathf.FloorToInt(Mathf.Max(0f, _elapsedRunSeconds));
            if (!force && elapsedSecond == _shownElapsedSecond) return;
            _shownElapsedSecond = elapsedSecond;
            _hudView.SetElapsedTime(_elapsedRunSeconds);
        }

        private static bool IsFinalSettlementState(eStageState state) =>
            state == eStageState.SETTLING ||
            state == eStageState.RETURNING_TO_LOBBY ||
            state == eStageState.CLEARED ||
            state == eStageState.FAILED;

        private void ConfigureResultHandlers()
        {
            if (_victoryView != null) _victoryView.ConfigureReturnToLobby(ReturnToLobby);
            if (_defeatView != null) _defeatView.ConfigureReturnToLobby(ReturnToLobby);
        }

        private void HandlePopupCountChanged(int openCount) => SetInputBlocked(openCount > 0);

        private void SetInputBlocked(bool isBlocked)
        {
            _phasePresentation?.SetExternalInputBlocked(isBlocked);
            _bootstrap?.SetUiInputBlocked(isBlocked);
        }

        private void HandleHeroKilled(UnitBase hero, int reward)
        {
            if (_hasStartedRunTimer && !_hasFrozenRunTimer) _killCount++;

            if (hero == null || _bootstrap == null || _bootstrap.Stage == null ||
                _bootstrap.Stage.State != eStageState.COMBAT || hero.Side != UnitSide.Hero ||
                !hero.transform.IsChildOf(_bootstrap.transform) ||
                !hero.TryGetComponent(out PooledHero pooledHero) || !pooledHero.IsLeased) return;

            RefreshRemainingEnemyCount();
            // OnHeroKilled는 풀의 사망 처리보다 먼저 발생하므로 IsDead로 검사하지 않는다.
            if (_remainingEnemyRound > 0 && _countedHeroDeaths.Add(pooledHero.LeaseId))
                _hudView?.SetRemainingEnemyCount(Mathf.Max(0, _remainingEnemyTotal - _countedHeroDeaths.Count));
        }

        private void SubscribeXp()
        {
            MawangLevel mawang = MawangXpBridge.Mawang;
            if (_hasSubscribedXp || mawang == null) return;
            mawang.XpChanged += RefreshHud;
            _hasSubscribedXp = true;
        }

        private void UnsubscribeXp()
        {
            MawangLevel mawang = MawangXpBridge.Mawang;
            if (_hasSubscribedXp && mawang != null) mawang.XpChanged -= RefreshHud;
            _hasSubscribedXp = false;
        }

        private sealed class EnemyRow
        {
            public string DisplayName { get; }
            public int Count { get; set; }

            public EnemyRow(string displayName)
            {
                DisplayName = displayName;
            }
        }
    }
}
