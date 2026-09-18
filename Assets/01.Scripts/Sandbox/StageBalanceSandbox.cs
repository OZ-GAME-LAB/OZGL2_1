using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using OZGL2.Stage;
using OZGL2.Contracts;
using OZGL2.Progression;
using OZGL2.Grid;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OZGL2.Sandbox
{
    /// <summary>
    /// 그리드·InGame 프로토타입 씬 없이, 내 스테이지 밸런스 데이터(StageDataSO)만 가지고 실제 전투
    /// (UnitBase + 진짜 UnitStatData + CombatModifierHub 배율)로 라운드를 진행하는 밸런스 테스트 도구.
    /// 준비(보상 선택) → 전투 시작 → 결과 → 다음 준비, 실제 게임 루프와 같은 흐름으로 수동 진행한다.
    /// 발판·칸 확장·배치 로직은 그리드팀 공용 실제 시스템(OZGL2.Grid.GridManager + GridSettings.asset)을
    /// 그대로 사용한다 — 코드는 안 건드리고 그대로 호출만 함. 준비 화면은 체스판처럼 칸이 전부 보이고
    /// (배치 가능/불가능/이미 찬 칸이 색으로 구분됨), 보상으로 받은 유닛은 보관함에 먼저 들어갔다가
    /// 마우스 드래그로 원하는 칸에 배치한다(실제 게임의 블록 드래그 배치와 같은 흐름).
    /// 캐릭터·발판 시각화만 스프라이트 대신 단색 네모로 단순화(아트 문제와 무관하게 순수 밸런스만 확인).
    /// </summary>
    public class StageBalanceSandbox : MonoBehaviour
    {
        private enum Phase { Idle, Preparation, Battle, Ended }

        [SerializeField] private StageDataSO _stage;
        [SerializeField] private GridSettingsSO _gridSettings;

        [Header("아군(마왕군) — 1라운드는 이 유닛 1기로 시작(그리드팀 고정 규칙과 동일)")]
        [SerializeField] private string _startingDefenderId = "M_WAR_01";

        [Header("배치 시각화 스케일 — 칸 좌표(GridManager) 1칸당 월드 간격")]
        [SerializeField] private float _unitVisualSize = 1f;
        [SerializeField] private float _heroSpacing = 1.2f;
        [SerializeField] private float _heroY = 4f;
        [SerializeField] private float _defenderSpacing = 1.2f;
        [SerializeField] private float _defenderY = -3f;
        [Tooltip("전체가 다 보이도록 카메라를 자동으로 줌아웃해서 맞출지 — 위에서 넓게 내려다보는 느낌.")]
        [SerializeField] private bool _autoFitCamera = true;
        [SerializeField] private float _cameraMargin = 1.2f;

        [Header("물량 vs 개별 세기 트레이드오프")]
        [Tooltip("라운드당 '총 전투력 예산' 성장 완만도 — 이 값 × 라운드수가 예산. 예산을 실제 스폰 인원수로 나눠서 1인당 스탯을 정한다(인원 많을수록 개별은 약해지되, 예산 자체는 라운드가 갈수록 계속 커짐).")]
        [SerializeField] private float _powerBudgetPerRound = 0.45f;
        [SerializeField] private float _bossBudgetMultiplier = 1.5f;

        private static readonly string[] AllMonsterIds = { "M_WAR_01", "M_SHD_01", "M_ARC_01", "M_MAG_01", "M_ROG_01", "M_HEL_01" };
        private static readonly Dictionary<string, string> DisplayName = new Dictionary<string, string>
        {
            { "M_WAR_01", "전사" }, { "M_SHD_01", "방패병" }, { "M_ARC_01", "궁수" },
            { "M_MAG_01", "마법사" }, { "M_ROG_01", "도적" }, { "M_HEL_01", "힐러" },
        };

        // 마왕군 유닛은 전부 1칸짜리로 단순화(실제 그리드 시스템은 모양 자유— 밸런스 테스트엔 불필요).
        private static readonly FootprintDefinition UnitFootprint = new FootprintDefinition("sandbox_unit_1x1", "1칸", new[] { Vector2Int.zero });

        private const string ExpandOption = "EXPAND";
        private static readonly Color ExpandCardColor = new Color(0.3f, 0.75f, 0.4f);

        // 체스판처럼 칸마다 상태(잠김/배치가능/이미 참/확장 가능)를 색으로 구분 — 짝수/홀수 칸은 살짝
        // 다른 톤을 줘서 체스판 느낌을 낸다. 드래그 중인 칸은 이 색 대신 초록(가능)/빨강(불가능)으로 덮어씀.
        private static readonly Color LockedColorA = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color LockedColorB = new Color(0.09f, 0.09f, 0.11f);
        private static readonly Color PlaceableColorA = new Color(0.45f, 0.37f, 0.27f);
        private static readonly Color PlaceableColorB = new Color(0.4f, 0.32f, 0.24f);
        private static readonly Color OccupiedColorA = new Color(0.32f, 0.27f, 0.32f);
        private static readonly Color OccupiedColorB = new Color(0.28f, 0.23f, 0.28f);
        private static readonly Color ExpansionFrontierColor = new Color(0.3f, 0.75f, 0.9f, 0.95f);
        private static readonly Color DragValidColor = new Color(0.3f, 0.85f, 0.4f, 0.9f);
        private static readonly Color DragInvalidColor = new Color(0.9f, 0.25f, 0.25f, 0.9f);
        private static readonly Color GridLineColor = new Color(0.05f, 0.05f, 0.06f);
        private static readonly Color TraySlotColor = new Color(0.2f, 0.2f, 0.26f);

        private readonly List<GameObject> _floorTiles = new List<GameObject>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _cellFill = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<string, GameObject> _visualByUnitId = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _trayVisualByUnitId = new Dictionary<string, GameObject>();
        private readonly List<GameObject> _traySlotTiles = new List<GameObject>();
        private readonly Dictionary<string, string> _blockIdByUnitId = new Dictionary<string, string>();

        private Phase _phase = Phase.Idle;
        private StageDefinition _def;
        private GridManager _grid; // 실제 그리드팀 배치 시스템 — 발판/블록/유닛/확장 전부 이걸로 처리
        private int _roundIndex;
        private string[] _candidateOptions;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<(UnitBase unit, Transform bar)> _healthBars = new List<(UnitBase, Transform)>();
        private readonly List<string> _log = new List<string>();
        private Vector2 _logScroll;
        private GameObject _kingMarker;

        // 드래그 앤 드랍 상태 — 블록+유닛을 한 쌍으로 같이 옮긴다(한 칸=몬스터 한 마리인 단순 모델이라).
        // 보드 위 칸에서 시작하면 재배치, 보관함 슬롯에서 시작하면 최초 배치(둘 다 같은 커밋 로직 사용).
        private bool _dragging;
        private string _dragUnitId;
        private GameObject _dragVisual;
        private Vector2Int _hoverCell;

        private static readonly Color DemonArmyBorder = new Color(0.25f, 0.5f, 0.95f); // 마왕군(아군) 테두리 — 파랑
        private static readonly Color HeroBorder = new Color(0.95f, 0.2f, 0.2f); // 용사(적) 테두리 — 빨강
        private static readonly Color KingColor = new Color(0.95f, 0.85f, 0.2f); // 마왕 — 금색

        private static readonly Dictionary<string, Color> JobColor = new Dictionary<string, Color>
        {
            { "WAR", new Color(0.82f, 0.30f, 0.18f) },
            { "SHD", new Color(0.4f, 0.55f, 0.85f) },
            { "ARC", new Color(0.35f, 0.7f, 0.35f) },
            { "MAG", new Color(0.6f, 0.35f, 0.85f) },
            { "ROG", new Color(0.85f, 0.75f, 0.2f) },
            { "HEL", new Color(0.9f, 0.5f, 0.75f) },
        };

        /// <summary>Stage가 인스펙터에 없으면 스테이지 선택 화면에서 고른 걸로 자동 로드하고 바로 시작한다.</summary>
        private void Start()
        {
            if (_stage == null)
            {
                _stage = LoadStageByName(SandboxLoopState.ChosenStageAssetName);
            }
            if (_stage != null) StartTest();
        }

        private void Update()
        {
            RefreshHealthBars();

            if (_phase == Phase.Preparation) HandleGridInput();

            if (_phase != Phase.Battle) return;

            int aliveHeroes = UnitRegistry.GetAliveCount(global::UnitSide.Hero);
            int aliveMonsters = UnitRegistry.GetAliveCount(global::UnitSide.DemonArmy);

            if (aliveMonsters <= 0)
            {
                Log($"R{_roundIndex + 1} 패배 — 마왕군 전멸 (배치 {_grid.PlacedCount}기). 여기서 밸런스가 깨짐.");
                _phase = Phase.Ended;
                return;
            }

            if (aliveHeroes <= 0)
            {
                Log($"R{_roundIndex + 1} 클리어 (배치 {_grid.PlacedCount}기)");
                int granted = SkillTreeStore.GrantForRound(_roundIndex + 1);
                if (granted > 0) Log($"  마일스톤 도달 — SP +{granted} (총 {SkillTreeStore.SkillPoints})");
                _roundIndex++;
                ClearSpawned();
                if (_def == null || _roundIndex >= _def.Rounds.Count)
                {
                    Log("전체 스테이지 클리어!");
                    _phase = Phase.Ended;
                    return;
                }
                // 보상 카드를 고르기 전까지는 그리드 실제 Phase를 REWARD로 유지한다(실제 게임과 동일한
                // 순서: 보상 확정 → 그 다음에 배치 가능한 PREPARATION으로 전환).
                _grid.TryFinishBattle();
                _candidateOptions = RollCandidates();
                _phase = Phase.Preparation;
                SpawnDefendersIdle();
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f; // 씬 나갈 때 배속 원복 — 다른 씬까지 빨라진 채로 넘어가면 안 되니까
        }

        private void OnGUI()
        {
            DrawSpeedControls();

            GUILayout.BeginArea(new Rect(Screen.width - 360f, 10f, 340f, Mathf.Min(Screen.height - 20f, 580f)), SandboxGameUI.Panel);
            GUILayout.Label(_stage != null ? _stage.name : "(Stage 미지정)", SandboxGameUI.Title);
            GUILayout.Label("파란 테두리=마왕군, 빨간 테두리=용사, 금색 다이아몬드=마왕", SandboxGameUI.Body);
            GUILayout.Space(6f);

            switch (_phase)
            {
                case Phase.Idle:
                    if (GUILayout.Button("전투 시작", SandboxGameUI.PrimaryButton)) StartTest();
                    break;

                case Phase.Preparation:
                    GUILayout.Label($"준비 — R{_roundIndex + 1} / {_def.Rounds.Count}", SandboxGameUI.Subtitle);
                    GUILayout.Label($"발판 {_grid.PlacedCount}/{_grid.FloorCells.Count}칸, 보관함 {_grid.StoredCount}/{_grid.Definition.StorageCapacity}: " + RosterSummary(), SandboxGameUI.Body);
                    if (_grid.RequiresExpansionPlacement)
                        GUILayout.Label("칸 확장 배치 중 — 하늘색으로 표시된 칸을 클릭해서 새 칸을 놓아줘", SandboxGameUI.Body);
                    else
                        GUILayout.Label("보관함/체스판의 마왕군을 드래그해서 배치·재배치할 수 있어(초록=가능, 빨강=불가)", SandboxGameUI.Body);
                    if (_candidateOptions != null)
                    {
                        GUILayout.Label("선택지 카드 (유닛 영입 / 칸 확장)", SandboxGameUI.Body);
                        GUILayout.BeginHorizontal();
                        foreach (var opt in _candidateOptions)
                        {
                            bool isExpand = opt == ExpandOption;
                            GUILayout.BeginVertical(SandboxGameUI.CardOpen, GUILayout.Width(90f));
                            GUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();
                            var cardRect = GUILayoutUtility.GetRect(44f, 44f, GUILayout.Width(44f), GUILayout.Height(44f));
                            string label = isExpand ? "칸 확장" : Name(opt);
                            string sub = isExpand ? "그리드 확장" : null;
                            Color color = isExpand ? ExpandCardColor : ColorForId(opt);
                            bool picked = SandboxGameUI.DrawDiamondNode(cardRect, color, false, label, sub);
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                            GUILayout.Space(18f);
                            if (picked) ResolveCardPick(opt, isExpand);
                            GUILayout.EndVertical();
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.Space(6f);
                    if (GUILayout.Button("전투 시작", SandboxGameUI.PrimaryButton)) BeginBattle();
                    if (GUILayout.Button("중단", SandboxGameUI.SecondaryButton)) StopTest();
                    break;

                case Phase.Battle:
                    GUILayout.Label($"전투 중 — R{_roundIndex + 1} / {_def.Rounds.Count}, 배치 {_grid.PlacedCount}기", SandboxGameUI.Subtitle);
                    if (GUILayout.Button("포기 (준비 화면으로)", SandboxGameUI.SecondaryButton)) AbandonBattle();
                    break;

                case Phase.Ended:
                    if (GUILayout.Button("다시 시작", SandboxGameUI.PrimaryButton)) StartTest();
                    if (GUILayout.Button("로비로 돌아가기", SandboxGameUI.SecondaryButton))
                        SceneManager.LoadScene(SandboxLoopState.LobbyScene);
                    break;
            }

            GUILayout.Space(6f);
            _logScroll = GUILayout.BeginScrollView(_logScroll);
            for (int i = _log.Count - 1; i >= 0; i--) GUILayout.Label(_log[i]);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>카드 선택 처리 — 유닛은 보관함으로만 들어가고(배치는 드래그로 직접), 확장은 실제
        /// TryAcceptExpansionReward로 "배치 대기" 상태만 만든다(실제 자리 선택은 하늘색 칸 클릭으로).</summary>
        private void ResolveCardPick(string opt, bool isExpand)
        {
            try
            {
                if (isExpand)
                {
                    if (_grid.TryAcceptExpansionReward())
                        Log("칸 확장 카드 선택 — 하늘색 칸을 클릭해서 새 칸을 놓아줘.");
                    else
                        Log("확장 불가 — 이미 최대 크기임.");
                }
                else
                {
                    GrantUnitToStorage(opt);
                }
            }
            catch (System.InvalidOperationException e)
            {
                // AddBlock/AddUnit은 보관함이 꽉 찼거나 그리드 상태가 안 맞으면 예외를 던진다 — 여기서
                // 잡아서 로그로 보여줘야 카드가 화면에 멈춰있는 채로 원인도 모르게 조용히 멈추지 않는다.
                Log($"카드 적용 실패 — {e.Message}");
            }
            _candidateOptions = null;
            _grid.TryAllowPreparation(canSkip: false); // 보상 확정 → 배치 가능한 PREPARATION으로 전환
            SpawnDefendersIdle();
        }

        private void DrawSpeedControls()
        {
            GUILayout.BeginArea(new Rect(10f, Screen.height - 60f, 240f, 50f), SandboxGameUI.Panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("배속", SandboxGameUI.Body, GUILayout.Width(30f));
            DrawSpeedButton(1f, "1x");
            DrawSpeedButton(2f, "2x");
            DrawSpeedButton(3f, "3x");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawSpeedButton(float scale, string label)
        {
            bool active = Mathf.Approximately(Time.timeScale, scale);
            if (GUILayout.Button(label, active ? SandboxGameUI.PrimaryButton : SandboxGameUI.SecondaryButton, GUILayout.Width(45f)))
                Time.timeScale = scale;
        }

        private void StartTest()
        {
            if (_stage == null)
            {
                Log("Stage가 지정 안 됨 — 인스펙터에서 StageNormal30/StageHard50 넣어줘.");
                return;
            }
            if (_gridSettings == null) _gridSettings = LoadGridSettings();
            if (_gridSettings == null)
            {
                Log("GridSettings 에셋을 못 찾음 (Assets/_Project/Data/Grid/Prototype/GridSettings.asset).");
                return;
            }

            _def = _stage.CreateSnapshot();
            _roundIndex = 0;
            _log.Clear();
            ClearSpawned();
            CancelDragState();
            _blockIdByUnitId.Clear();

            // 그리드팀 실제 배치 시스템(GridManager)을 그대로 새로 만들어서 씀 — 초기 바닥·최대 크기·
            // 확장 모양(floor_domino) 전부 GridSettings.asset에 있는 진짜 값 그대로.
            _grid = new GridManager(_gridSettings.CreateSnapshot());
            _grid.TryAllowPreparation(canSkip: false);
            BuildBoardTiles();
            BuildTraySlotTiles();
            ConfigureCamera();
            GrantAndPlaceStartingUnit(_startingDefenderId); // 시작 유닛은 보관함 없이 바로 배치(R1 즉시 테스트 가능하게)

            _candidateOptions = null;
            EnsureKingMarker();
            _phase = Phase.Preparation;
            SpawnDefendersIdle();
        }

        /// <summary>준비 화면 전체(체스판+보관함+용사 스폰 지점)가 다 보이도록 카메라를 자동으로 줌아웃한다
        /// — 위에서 넓게 내려다보는 느낌을 원해서 추가.</summary>
        private void ConfigureCamera()
        {
            if (!_autoFitCamera) return;
            var cam = Camera.main;
            if (cam == null) return;

            var max = _grid.Definition.MaximumSize;
            float boardHalfWidth = max.x * _defenderSpacing * 0.5f;
            float top = _heroY + _heroSpacing * 1.5f;
            float bottom = TraySlotPosition(0).y - _defenderSpacing;
            float verticalHalf = (top - bottom) * 0.5f + _cameraMargin;
            float horizontalHalf = boardHalfWidth + _cameraMargin;

            cam.orthographic = true;
            float sizeForWidth = horizontalHalf / Mathf.Max(cam.aspect, 0.1f);
            cam.orthographicSize = Mathf.Max(verticalHalf, sizeForWidth);
            var pos = cam.transform.position;
            cam.transform.position = new Vector3(0f, (top + bottom) * 0.5f, pos.z);
        }

        /// <summary>마왕 위치 표시 — 그리드팀 실제 KingAnchor 좌표를 그대로 써서 한 번만 생성.</summary>
        private void EnsureKingMarker()
        {
            if (_kingMarker != null) return;
            _kingMarker = new GameObject("King (마왕)");
            _kingMarker.transform.position = CellToWorld(_grid.Definition.KingAnchor);
            _kingMarker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            _kingMarker.transform.localScale = Vector3.one * (_unitVisualSize * 1.4f);
            var sr = _kingMarker.AddComponent<SpriteRenderer>();
            sr.sprite = GetSquareSprite();
            sr.color = KingColor;
            sr.sortingOrder = 5;

            UnitRegistry.KingWorldPosition = _kingMarker.transform.position;
        }

        private void StopTest()
        {
            _phase = Phase.Idle;
            ClearSpawned();
            ClearFloor();
            CancelDragState();
        }

        /// <summary>전투 중 "포기" — 패배 처리 없이 같은 라운드 준비 화면으로 되돌아간다(로스터·칸수는 유지).</summary>
        private void AbandonBattle()
        {
            Log($"R{_roundIndex + 1} 포기 — 준비 화면으로 복귀");
            _grid.TryFinishBattle();
            _grid.TryAllowPreparation(canSkip: false);
            _phase = Phase.Preparation;
            SpawnDefendersIdle();
        }

        private string[] RollCandidates()
        {
            bool fullFloor = FreeFloorCell() == null;
            bool canExpand = _grid.CanExpand;
            // 발판이 이미 꽉 찼으면 확장 카드만(더 확장이 안 되면 유닛 카드로 대체), 아니면 유닛 2장 + 확장 카드 1장.
            if (fullFloor)
                return canExpand ? new[] { ExpandOption, ExpandOption, ExpandOption } : new[] { RandomMonsterId(), RandomMonsterId(), RandomMonsterId() };
            return canExpand ? new[] { RandomMonsterId(), RandomMonsterId(), ExpandOption } : new[] { RandomMonsterId(), RandomMonsterId(), RandomMonsterId() };
        }

        private void BeginBattle()
        {
            CancelDragState();
            if (!_grid.TryBeginBattle(() => { }))
            {
                if (_grid.RequiresExpansionPlacement) Log("전투 시작 불가 — 확장 위치를 먼저 골라야 함(하늘색 칸 클릭).");
                else if (_grid.PlacedCount <= 0) Log("전투 시작 불가 — 배치된 유닛이 없음(보관함에서 드래그해서 배치해줘).");
                else Log("전투 시작 실패.");
                return;
            }
            _phase = Phase.Battle;
            ClearSpawned();
            if (_def == null || _roundIndex >= _def.Rounds.Count) return;
            var round = _def.Rounds[_roundIndex];

            SpawnDefenderRow();

            int totalAttackers = 0;
            foreach (var spawn in round.Spawns) totalAttackers += spawn.Count;

            // 라운드가 갈수록 계속 커지는 "총 전투력 예산"을 실제 스폰 인원수로 나눠서 1인당 스탯을 정한다.
            // 예산 자체는 계속 성장하니 물량이 많아져도 전체 난이도는 계속 올라가고, 다만 인원이 많으면
            // 1인당은 상대적으로 약해진다(물량↑ 개별↓, 총 난이도는 라운드에 비례해 계속 상승).
            float budget = 1f + (_roundIndex + 1) * _powerBudgetPerRound;
            if (round.IsBossRound) budget *= _bossBudgetMultiplier;
            float powerScale = totalAttackers > 0 ? Mathf.Clamp01(budget / totalAttackers) : 1f;

            // 용사는 실제로 마왕군이 서 있는 칸을 향해 걸어가야 사거리 안에 들어온다 — 가장 앞줄(왕에서
            // 가장 먼 row, 즉 용사와 가장 가까운 칸)에 배치된 유닛을 1차 목표로 삼는다.
            Vector3 defenderTarget = FrontlineDefenderPosition();

            int placed = 0;
            foreach (var spawn in round.Spawns)
            {
                for (int i = 0; i < spawn.Count; i++)
                {
                    float x = (placed - (totalAttackers - 1) * 0.5f) * _heroSpacing;
                    placed++;
                    SpawnUnit(spawn.HeroId, new Vector3(x, _heroY, 0f), moveToward: defenderTarget, powerScale);
                }
            }
        }

        /// <summary>준비 화면에서 현재 배치·보관 중인 마왕군을 가만히 세워서 보여준다(전투는 안 붙음).</summary>
        private void SpawnDefendersIdle()
        {
            ClearSpawned();
            SpawnDefenderRow();
            RefreshTrayVisuals();
        }

        /// <summary>
        /// 실제 GridManager에 배치돼 있는 유닛(_grid.Units, IsPlaced)만큼 그린다. 발판(체스판 칸)은
        /// _spawned가 아니라 별도 리스트라 전투 중 유닛이 죽어도 안 사라진다.
        /// </summary>
        private void SpawnDefenderRow()
        {
            _visualByUnitId.Clear();
            foreach (var unit in _grid.Units)
            {
                if (!unit.IsPlaced) continue;
                var go = SpawnUnit(unit.Definition.Id, CellToWorld(unit.Anchor), moveToward: null);
                _visualByUnitId[unit.InstanceId] = go;
            }
            RefreshBoardColors();
        }

        /// <summary>보관함(아직 배치 안 된 유닛)을 화면 아래 별도 줄에 그린다 — 여기서 드래그해서 체스판에 배치.</summary>
        private void RefreshTrayVisuals()
        {
            foreach (var go in _trayVisualByUnitId.Values) if (go != null) Destroy(go);
            _trayVisualByUnitId.Clear();

            int index = 0;
            foreach (var unit in _grid.Units)
            {
                if (unit.IsPlaced) continue;
                var go = SpawnUnit(unit.Definition.Id, TraySlotPosition(index), moveToward: null);
                _trayVisualByUnitId[unit.InstanceId] = go;
                index++;
            }
        }

        /// <summary>보관함 슬롯 배경(빈 칸 표시용)을 저장 용량만큼 미리 그려둔다.</summary>
        private void BuildTraySlotTiles()
        {
            foreach (var t in _traySlotTiles) if (t != null) Destroy(t);
            _traySlotTiles.Clear();

            int capacity = _grid.Definition.StorageCapacity;
            float size = _defenderSpacing * 0.8f;
            for (int i = 0; i < capacity; i++)
            {
                var go = new GameObject($"tray_slot_{i}");
                go.transform.position = TraySlotPosition(i);
                go.transform.localScale = Vector3.one * size;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GetSquareSprite();
                sr.color = TraySlotColor;
                sr.sortingOrder = 0;
                _traySlotTiles.Add(go);
            }
        }

        /// <summary>보관함 i번째 슬롯의 월드 위치 — 왕보다 한 줄 더 아래, 가로로 나열.</summary>
        private Vector3 TraySlotPosition(int index)
        {
            int capacity = _grid?.Definition.StorageCapacity ?? 10;
            float startX = -(_defenderSpacing * (capacity - 1) * 0.5f);
            float kingY = _defenderY + _grid.Definition.KingAnchor.y * _defenderSpacing;
            float y = kingY - _defenderSpacing * 1.8f;
            return new Vector3(startX + index * _defenderSpacing, y, 0f);
        }

        /// <summary>
        /// 그리드팀 최대 크기(Definition.MaximumSize) 전체를 체스판처럼 칸 단위로 한 번만 만든다 — 아직
        /// 확장 안 된 칸도 자리는 보이되 잠긴 색으로, 확장되면 RefreshBoardColors가 색만 바꿔서 반영한다.
        /// </summary>
        private void BuildBoardTiles()
        {
            ClearFloor();
            var max = _grid.Definition.MaximumSize;

            // 칸 사이에 틈(그리드 라인)이 보이게 칸 크기를 간격보다 살짝 작게 둔다 — 체스판처럼 칸이 나뉘어 보임.
            float outlineSize = _defenderSpacing * 0.95f;
            float fillSize = _defenderSpacing * 0.86f;

            for (int y = 0; y < max.y; y++)
            {
                for (int x = 0; x < max.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Vector3 pos = CellToWorld(cell);

                    var outline = new GameObject($"cell_outline_{x}_{y}");
                    outline.transform.position = pos;
                    outline.transform.localScale = Vector3.one * outlineSize;
                    var outlineSr = outline.AddComponent<SpriteRenderer>();
                    outlineSr.sprite = GetSquareSprite();
                    outlineSr.color = GridLineColor;
                    outlineSr.sortingOrder = 0;
                    _floorTiles.Add(outline);

                    var fill = new GameObject($"cell_fill_{x}_{y}");
                    fill.transform.position = pos;
                    fill.transform.localScale = Vector3.one * fillSize;
                    var fillSr = fill.AddComponent<SpriteRenderer>();
                    fillSr.sprite = GetSquareSprite();
                    fillSr.sortingOrder = 1;
                    _floorTiles.Add(fill);

                    _cellFill[cell] = fillSr;
                }
            }
            RefreshBoardColors();
        }

        /// <summary>칸마다 잠김/배치가능/이미 참/확장가능 상태를 색으로 다시 칠한다. 드래그 중이면 마우스가
        /// 올라간 칸만 초록(가능)/빨강(불가)으로 덮어써서 지금 여기 놓을 수 있는지 바로 보이게 한다.</summary>
        private void RefreshBoardColors()
        {
            if (_grid == null) return;
            var max = _grid.Definition.MaximumSize;
            HashSet<Vector2Int> frontier = _grid.RequiresExpansionPlacement
                ? new HashSet<Vector2Int>(_grid.GetExpansionFrontier())
                : null;

            for (int y = 0; y < max.y; y++)
            {
                for (int x = 0; x < max.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_cellFill.TryGetValue(cell, out var sr) || sr == null) continue;

                    bool checker = (x + y) % 2 == 0;
                    bool hasFloor = _grid.HasFloor(cell);
                    bool hasBlock = hasFloor && _grid.GetBlockAt(cell) != null;

                    Color color = !hasFloor ? (checker ? LockedColorA : LockedColorB)
                        : hasBlock ? (checker ? OccupiedColorA : OccupiedColorB)
                        : (checker ? PlaceableColorA : PlaceableColorB);

                    if (frontier != null && frontier.Contains(cell)) color = ExpansionFrontierColor;

                    if (_dragging && _hoverCell == cell)
                        color = _grid.GetPreviewFailure() == ePlacementFailure.NONE ? DragValidColor : DragInvalidColor;

                    sr.color = color;
                }
            }
        }

        private void ClearFloor()
        {
            foreach (var t in _floorTiles) if (t != null) Destroy(t);
            _floorTiles.Clear();
            _cellFill.Clear();
        }

        /// <summary>
        /// 준비 화면 입력 — (1) 확장 배치 대기 중이면 클릭한 칸으로 확장 시도, (2) 아니면 보드/보관함의
        /// 마왕군(블록+유닛 한 쌍)을 마우스로 드래그해서 배치·재배치한다. 실제 GridManager의
        /// BeginBlockDrag/MovePreview/GetPreviewFailure/CommitPreview/CancelDrag를 그대로 써서 검증하고,
        /// "드래그로 자리를 고르는" 입력 처리만 여기서 마우스로 직접 한다(팀 UI는 UI Toolkit 기반이라
        /// 그대로 못 가져다 씀).
        /// </summary>
        private void HandleGridInput()
        {
            if (Mouse.current == null || _grid == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (IsMouseOverUi()) return;
                Vector3 mouseWorld = GetMouseWorldPosition();

                if (_grid.RequiresExpansionPlacement)
                {
                    var clickedCell = WorldToCell(mouseWorld);
                    if (!TryExpandAt(clickedCell)) Log("여기엔 확장 못함 — 하늘색으로 표시된 칸을 클릭해줘.");
                    RefreshBoardColors();
                    return;
                }

                string trayUnitId = HitTestTray(mouseWorld);
                if (trayUnitId != null)
                {
                    StartDrag(trayUnitId, _blockIdByUnitId[trayUnitId], _trayVisualByUnitId[trayUnitId], WorldToCell(mouseWorld));
                    return;
                }

                var cell = WorldToCell(mouseWorld);
                var block = _grid.HasFloor(cell) ? _grid.GetBlockAt(cell) : null;
                if (block == null) return;
                var units = _grid.GetUnitsOnBlock(block.InstanceId);
                string unitId = units.Count > 0 ? units[0].InstanceId : null;
                _visualByUnitId.TryGetValue(unitId, out var visual);
                StartDrag(unitId, block.InstanceId, visual, cell);
            }
            else if (Mouse.current.leftButton.isPressed && _dragging)
            {
                Vector3 mouseWorld = GetMouseWorldPosition();
                if (_dragVisual != null) _dragVisual.transform.position = new Vector3(mouseWorld.x, mouseWorld.y, 0f);
                _hoverCell = WorldToCell(mouseWorld);
                _grid.MovePreview(_hoverCell);
                RefreshBoardColors();
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame && _dragging)
            {
                var cell = WorldToCell(GetMouseWorldPosition());
                _grid.MovePreview(cell);
                if (_grid.GetPreviewFailure() == ePlacementFailure.NONE)
                {
                    _grid.CommitPreview(); // 블록 배치/이동 — 팀 규칙상 원래 자리에 있던 유닛은 자동으로 내려감(ReturnUnitsFromBlock)
                    if (_dragUnitId != null && _grid.BeginUnitDrag(_dragUnitId))
                    {
                        _grid.MovePreview(cell);
                        if (_grid.GetPreviewFailure() == ePlacementFailure.NONE) _grid.CommitPreview();
                        else _grid.CancelDrag();
                    }
                }
                else
                {
                    _grid.CancelDrag();
                }
                _dragging = false;
                _dragUnitId = null;
                _dragVisual = null;
                SpawnDefendersIdle(); // 새 위치/보관함 상태대로 시각화 다시 그림
            }
        }

        /// <summary>보드 칸 또는 보관함 슬롯, 어느 쪽에서 시작했든 같은 방식으로 드래그를 시작한다.</summary>
        private void StartDrag(string unitId, string blockId, GameObject visual, Vector2Int startCell)
        {
            if (blockId == null || !_grid.BeginBlockDrag(blockId)) return;
            _dragging = true;
            _dragUnitId = unitId;
            _dragVisual = visual;
            _hoverCell = startCell;
        }

        /// <summary>마우스 월드 위치가 보관함 슬롯 위인지 확인 — 맞으면 그 슬롯의 유닛 InstanceId 반환.</summary>
        private string HitTestTray(Vector3 mouseWorld)
        {
            float half = _defenderSpacing * 0.5f;
            int index = 0;
            foreach (var unit in _grid.Units)
            {
                if (unit.IsPlaced) continue;
                Vector3 slotPos = TraySlotPosition(index);
                if (Mathf.Abs(mouseWorld.x - slotPos.x) <= half && Mathf.Abs(mouseWorld.y - slotPos.y) <= half)
                    return unit.InstanceId;
                index++;
            }
            return null;
        }

        /// <summary>드래그 중 상태를 강제로 취소 — 전투 시작/중단 등으로 준비 화면을 벗어날 때 씀.</summary>
        private void CancelDragState()
        {
            if (!_dragging) return;
            _grid?.CancelDrag();
            _dragging = false;
            _dragUnitId = null;
            _dragVisual = null;
        }

        /// <summary>마우스가 오른쪽 카드/로그 패널이나 배속 버튼 위에 있으면 그리드 드래그 시작을 막는다
        /// (그 위에서 클릭했는데 밑에 있는 칸이 드래그로 잡혀버리는 걸 방지).</summary>
        private bool IsMouseOverUi()
        {
            Vector2 screen = Mouse.current.position.ReadValue();
            var guiPoint = new Vector2(screen.x, Screen.height - screen.y); // IMGUI는 위가 0이라 y 뒤집기
            var panelRect = new Rect(Screen.width - 360f, 10f, 340f, Mathf.Min(Screen.height - 20f, 580f));
            var speedRect = new Rect(10f, Screen.height - 60f, 240f, 50f);
            return panelRect.Contains(guiPoint) || speedRect.Contains(guiPoint);
        }

        /// <summary>그리드 칸 좌표(Vector2Int/Vector2) → 월드 위치. 그리드팀 실제 좌표계 그대로(KingAnchor가
        /// (가로중앙, -1)이므로 x는 최대폭 중앙 기준, y는 row가 커질수록(왕에서 멀어질수록=용사 쪽) 위로.</summary>
        private Vector3 CellToWorld(Vector2 cell)
        {
            var max = _grid.Definition.MaximumSize;
            float x = (cell.x - (max.x - 1) * 0.5f) * _defenderSpacing;
            float y = _defenderY + cell.y * _defenderSpacing;
            return new Vector3(x, y, 0f);
        }

        /// <summary>CellToWorld의 역변환 — 마우스 월드 위치에서 가장 가까운 칸 좌표를 구한다.</summary>
        private Vector2Int WorldToCell(Vector3 world)
        {
            var max = _grid.Definition.MaximumSize;
            float cellX = world.x / _defenderSpacing + (max.x - 1) * 0.5f;
            float cellY = (world.y - _defenderY) / _defenderSpacing;
            return new Vector2Int(Mathf.RoundToInt(cellX), Mathf.RoundToInt(cellY));
        }

        private static Vector3 GetMouseWorldPosition()
        {
            Vector2 screenPos2D = Mouse.current.position.ReadValue();
            Vector3 screenPos = new Vector3(screenPos2D.x, screenPos2D.y, -Camera.main.transform.position.z);
            return Camera.main.ScreenToWorldPoint(screenPos);
        }

        /// <summary>블록이 아직 없는(=비어있는) 바닥 칸 하나 — 없으면 null(발판이 꽉 찼다는 뜻).</summary>
        private Vector2Int? FreeFloorCell()
        {
            foreach (var cell in _grid.FloorCells)
                if (_grid.GetBlockAt(cell) == null) return cell;
            return null;
        }

        /// <summary>왕에서 가장 먼(=용사와 가장 가까운) row에 배치된 유닛 위치 — 없으면 기본 위치.</summary>
        private Vector3 FrontlineDefenderPosition()
        {
            Vector3 fallback = new Vector3(0f, _defenderY, 0f);
            int bestRow = int.MinValue;
            Vector3 best = fallback;
            bool found = false;
            foreach (var unit in _grid.Units)
            {
                if (!unit.IsPlaced) continue;
                if (unit.Anchor.y > bestRow)
                {
                    bestRow = unit.Anchor.y;
                    best = CellToWorld(unit.Anchor);
                    found = true;
                }
            }
            return found ? best : fallback;
        }

        /// <summary>시작 유닛은 예외적으로 보관함을 거치지 않고 바로 배치한다 — R1을 그리드팀 고정 규칙대로
        /// "기본 유닛 1기로 즉시 테스트 가능"하게 유지하기 위함(보상으로 받는 유닛과는 다름).</summary>
        private void GrantAndPlaceStartingUnit(string heroId)
        {
            var cell = FreeFloorCell();
            if (cell == null) return;
            var (blockId, unitId) = CreateStoredUnit(heroId);
            if (blockId == null) return;

            if (!_grid.BeginBlockDrag(blockId)) return;
            _grid.MovePreview(cell.Value);
            if (_grid.GetPreviewFailure() != ePlacementFailure.NONE) { _grid.CancelDrag(); return; }
            _grid.CommitPreview();

            if (!_grid.BeginUnitDrag(unitId)) return;
            _grid.MovePreview(cell.Value);
            if (_grid.GetPreviewFailure() != ePlacementFailure.NONE) { _grid.CancelDrag(); return; }
            _grid.CommitPreview();
        }

        /// <summary>보상으로 유닛 하나를 보관함에 추가만 한다 — 배치는 플레이어가 직접 드래그로.</summary>
        private void GrantUnitToStorage(string heroId)
        {
            // 몬스터 1마리 = 블록 1개 + 유닛 1개 = 보관함 2칸 소모. 1칸만 보고 통과시키면 AddBlock은
            // 성공하고 AddUnit이 던져서(보관함 꽉 참) 블록만 덩그러니 남는 버그가 생겨서 2칸으로 계산.
            if (_grid.StoredCount + 2 > _grid.Definition.StorageCapacity)
            {
                Log($"{Name(heroId)} 보관함이 꽉 참 — 먼저 배치해줘.");
                return;
            }
            CreateStoredUnit(heroId);
        }

        /// <summary>1칸짜리 블록+유닛 쌍을 만들어 보관함(미배치 상태)에 추가한다.</summary>
        private (string blockId, string unitId) CreateStoredUnit(string heroId)
        {
            string uid = System.Guid.NewGuid().ToString("N");
            string blockId = "block_" + uid;
            string unitId = "unit_" + uid;
            _grid.AddBlock(blockId, heroId, UnitFootprint);
            _grid.AddUnit(unitId, new UnitDefinition(heroId, Name(heroId), UnitFootprint));
            _blockIdByUnitId[unitId] = blockId;
            return (blockId, unitId);
        }

        /// <summary>
        /// 클릭한 칸을 포함하는, 실제 GridPlacementRules.ValidateExpansion을 통과하는 확장 배치를 찾아서
        /// 커밋한다 — 검증 로직 자체는 그리드팀 것 그대로, "드래그로 자리를 고르는" 부분만 클릭 한 번으로
        /// 자동화(회전은 유효한 걸 자동으로 찾음).
        /// </summary>
        private bool TryExpandAt(Vector2Int clickedCell)
        {
            var def = _grid.Definition;
            var floor = new HashSet<Vector2Int>(_grid.FloorCells);
            for (int y = 0; y < def.MaximumSize.y; y++)
            {
                for (int x = 0; x < def.MaximumSize.x; x++)
                {
                    for (int rotation = 0; rotation < 4; rotation++)
                    {
                        var cells = def.Expansion.GetCells(new Vector2Int(x, y), rotation);
                        if (System.Array.IndexOf(cells, clickedCell) < 0) continue;
                        if (GridPlacementRules.ValidateExpansion(def, floor, cells) != ePlacementFailure.NONE) continue;

                        _grid.BeginExpansionDrag();
                        for (int r = 0; r < rotation; r++) _grid.RotatePreview();
                        _grid.MovePreview(new Vector2Int(x, y));
                        if (_grid.GetPreviewFailure() == ePlacementFailure.NONE)
                        {
                            _grid.CommitPreview();
                            return true;
                        }
                        _grid.CancelDrag();
                    }
                }
            }
            return false;
        }

        private string RosterSummary()
        {
            var names = new List<string>();
            foreach (var unit in _grid.Units)
                names.Add(unit.IsPlaced ? Name(unit.Definition.Id) : $"({Name(unit.Definition.Id)})");
            return string.Join(", ", names);
        }

        private GameObject SpawnUnit(string heroId, Vector3 pos, Vector3? moveToward, float powerScale = 1f)
        {
            var baseStat = LoadStatData(heroId);
            if (baseStat == null)
            {
                Debug.LogError($"[StageBalanceSandbox] UnitStatData를 못 찾음: {heroId}");
                return null;
            }

            // 물량 트레이드오프로 스탯을 깎아야 하면 원본 에셋은 안 건드리고 런타임 복제본만 조정.
            UnitStatData stat = baseStat;
            if (!Mathf.Approximately(powerScale, 1f))
            {
                stat = ScriptableObject.Instantiate(baseStat);
                stat.maxHealth = Mathf.Max(1, Mathf.RoundToInt(baseStat.maxHealth * powerScale));
                stat.attackPower = baseStat.attackPower * powerScale;
            }

            var go = new GameObject(heroId);
            go.transform.position = pos;

            // UnitBase.Awake()가 statData를 바로 참조하는데, GameObject가 활성 상태에서 AddComponent하면
            // Awake가 그 자리에서 즉시 실행돼버려서(다음 줄에서 statData를 넣기도 전에) 초기화가 비어버린다.
            // 비활성 상태로 만들어두고 값 다 채운 다음 활성화해야 Awake가 올바른 시점에 실행된다.
            go.SetActive(false);

            // 진영 테두리 — 마왕군(아군)은 파랑, 용사(적)는 빨강. 같은 직업이라 색이 겹쳐도 편이 구분되게.
            var borderGo = new GameObject("border");
            borderGo.transform.SetParent(go.transform, false);
            borderGo.transform.localScale = Vector3.one * (_unitVisualSize * 1.3f);
            var borderSr = borderGo.AddComponent<SpriteRenderer>();
            borderSr.sprite = GetSquareSprite();
            borderSr.color = stat.side == global::UnitSide.DemonArmy ? DemonArmyBorder : HeroBorder;
            borderSr.sortingOrder = 9;

            var jobGo = new GameObject("job");
            jobGo.transform.SetParent(go.transform, false);
            jobGo.transform.localScale = Vector3.one * _unitVisualSize;
            var sr = jobGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetSquareSprite();
            sr.color = ColorForId(heroId);
            sr.sortingOrder = 10;

            // 체력바 — 이게 없으면 전투가 실제로 진행 중이어도 색깔 네모가 안 움직여서 "멈춘 것"처럼 보인다.
            var barBg = new GameObject("hpBarBg");
            barBg.transform.SetParent(go.transform, false);
            barBg.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            barBg.transform.localScale = new Vector3(1.1f, 0.16f, 1f);
            var barBgSr = barBg.AddComponent<SpriteRenderer>();
            barBgSr.sprite = GetSquareSprite();
            barBgSr.color = new Color(0.08f, 0.08f, 0.08f);
            barBgSr.sortingOrder = 11;

            var bar = new GameObject("hpBar");
            bar.transform.SetParent(go.transform, false);
            bar.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            bar.transform.localScale = new Vector3(1f, 0.12f, 1f);
            var barSr = bar.AddComponent<SpriteRenderer>();
            barSr.sprite = GetSquareSprite();
            barSr.color = new Color(0.25f, 0.9f, 0.35f);
            barSr.sortingOrder = 12;

            var unit = go.AddComponent<UnitBase>();
            unit.statData = stat;
            go.SetActive(true);

            _healthBars.Add((unit, bar.transform));

            if (moveToward.HasValue) unit.SetMoveTarget(moveToward.Value);

            float atkMult = CombatModifierHub.GetAttackMult(stat.job, stat.side);
            float hpMult = CombatModifierHub.GetHpMult(stat.job, stat.side);
            float spdMult = CombatModifierHub.GetAttackSpeedMult(stat.job, stat.side);
            Log($"  {heroId} 스폰 — HP {Mathf.RoundToInt(stat.maxHealth * hpMult)}(x{hpMult:0.00}) " +
                $"ATK {stat.attackPower * atkMult:0.0}(x{atkMult:0.00}) 방어 {stat.defensePercent:0.00} SPDx{spdMult:0.00}");

            _spawned.Add(go);
            return go;
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            _healthBars.Clear();
        }

        /// <summary>매 프레임 살아있는 유닛의 체력바 폭을 currentHealth 비율로 갱신 — 전투 진행 여부를 눈으로 확인 가능.</summary>
        private void RefreshHealthBars()
        {
            for (int i = _healthBars.Count - 1; i >= 0; i--)
            {
                var (unit, bar) = _healthBars[i];
                if (unit == null || bar == null)
                {
                    _healthBars.RemoveAt(i);
                    continue;
                }
                float maxHp = unit.statData != null ? unit.statData.maxHealth : 1;
                float ratio = maxHp > 0f ? Mathf.Clamp01(unit.currentHealth / maxHp) : 0f;
                var scale = bar.localScale;
                bar.localScale = new Vector3(ratio, scale.y, scale.z);
            }
        }

        private void Log(string message)
        {
            _log.Add(message);
            Debug.Log($"[StageBalanceSandbox] {message}");
        }

        private static string RandomMonsterId() => AllMonsterIds[Random.Range(0, AllMonsterIds.Length)];

        private static string Name(string id) => DisplayName.TryGetValue(id, out var n) ? n : id;

        private static Color ColorForId(string heroId)
        {
            foreach (var pair in JobColor)
            {
                if (heroId.Contains(pair.Key)) return pair.Value;
            }
            return Color.gray;
        }

        private static Sprite _squareSprite;
        private static Sprite GetSquareSprite()
        {
            if (_squareSprite != null) return _squareSprite;
            var tex = Texture2D.whiteTexture;
            // pixelsPerUnit = 텍스처 폭으로 맞춰서 scale=1일 때 정확히 1 월드 유닛 크기가 되게 함
            // (whiteTexture는 원본이 아주 작아서 고정 PPU를 쓰면 점처럼 작게 보임).
            _squareSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            return _squareSprite;
        }

        private static UnitStatData LoadStatData(string id)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<UnitStatData>($"Assets/03.ScriptableObjects/UnitStats/{id}.asset");
#else
            return null;
#endif
        }

        private static StageDataSO LoadStageByName(string name)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<StageDataSO>($"Assets/03.ScriptableObjects/Stage/Balance/{name}.asset");
#else
            return null;
#endif
        }

        private static GridSettingsSO LoadGridSettings()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GridSettingsSO>("Assets/_Project/Data/Grid/Prototype/GridSettings.asset");
#else
            return null;
#endif
        }
    }
}
