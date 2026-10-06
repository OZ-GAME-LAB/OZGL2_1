using System.Collections.Generic;
using System.Reflection;
using OZGL2.Grid;
using OZGL2.Progression;
using OZGL2.Synergy;
using OZGL2.InGame;
using OZGL2.Stage;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 인게임 재화(불꽃)와 리롤.
    /// - 용사를 처치할 때마다 처치 보상(UnitBase.OnHeroKilled의 보상 값 × _rewardScale)만큼, 라운드를 깰 때 클리어 보너스만큼 재화가 쌓인다.
    /// - 왼쪽 아래 재화 표시가 바로 갱신되고, 얻을 때마다 아이콘이 살짝 커졌다 돌아오며, 쓰러진 자리에서 "+N"이 떠오른다.
    /// - 리롤 버튼: 라운드 보상(유닛 3택 1) 카드를 고르는 중에 재화를 내고 후보 카드를 다시 뽑는다. 비용은 라운드가 올라갈수록 늘어난다. 재화가 모자라거나 보상을 고르는 중이 아니면
    ///   안내 문구가 뜨고 아무 일도 일어나지 않는다. 새 판(재도전 포함)이 시작되면 재화는 시작값으로 돌아간다.
    /// - "이번 웨이브" 패널 아래에, 이번 라운드를 클리어하면 받을 보상(재화 최대치, SP, 유닛 카드)을 미리 보여 준다. SP는 10·20·30라운드 마일스톤에서 받는다.
    /// UI 스크립트와 보상 코드(StageGridRewards)는 수정하지 않고 리플렉션과 공개 API로만 다룬다.
    /// </summary>
    public sealed class InGameCurrencyHud : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SerializeField, Min(0)] private int _startAmount = 0;
        [SerializeField, Min(0f)] private float _rewardScale = 1f;
        [SerializeField, Min(0.1f)] private float _floatSeconds = 0.9f;
        [Header("라운드 클리어 보너스 (전투가 끝나 보상을 고를 때 한 번 지급)")]
        [SerializeField, Min(0)] private int _clearBonusBase = 4;
        [SerializeField, Min(0f)] private float _clearBonusPerRound = 0.5f;
        [Header("리롤 비용 = 기본 + 라운드당 증가 × (현재 라운드 - 1) + 같은 보상 안에서 다시 뽑을 때마다 추가")]
        [Tooltip("1라운드 기준 비용. 용사 처치 수입이 라운드 1에 9, 라운드 10에 약 65, 라운드 20에 약 100(보통 난이도)이라 거기에 맞췄다.")]
        [SerializeField, Min(0)] private int _rerollBase = 12;
        [SerializeField, Min(0f)] private float _rerollPerRound = 4.2f;
        [SerializeField, Min(0)] private int _rerollRepeatStep = 5;

        private sealed class Floating
        {
            public RectTransform Rect;
            public TMP_Text Text;
            public Vector2 Start;
            public float Age;
        }

        private UIBattleMutedPreviewView _view;
        private InGamePrototypeBootstrap _bootstrap;
        private UIGridStorageHandAdapter _handAdapter;
        private object _stage;
        private eStageState _prevState = eStageState.IDLE;
        private RectTransform _pulseTarget;
        private readonly List<Button> _rerollButtons = new List<Button>();
        private TMP_Text _rerollCostLabel;
        private float _pulse;
        private int _balance;
        private int _rerollCount;
        private string _rerollRequestId;
        private bool _pushed;
        private bool _buttonHooked;
        private int _roundKillGain, _roundBonusGain, _roundSpGain, _roundNumberShown;
        [Header("보상 패널 이미지 (이번 웨이브 패널과 같은 프레임·아이콘)")]
        [SerializeField] private Sprite _frameSprite;
        [SerializeField] private Sprite _ornamentSprite;
        [SerializeField] private Sprite _gemIcon;
        [SerializeField] private Sprite _spIcon;
        [SerializeField] private Sprite _cardIcon;
        [SerializeField] private Sprite _augmentIcon;
        [SerializeField] private Sprite _clearIcon;
        [SerializeField] private Sprite _unlockIcon;

        private Canvas _summaryCanvas;
        private RectTransform _summaryContent;
        private RectTransform _summaryBox;
        private Canvas _canvas;
        private TMP_FontAsset _font;
        private readonly List<Floating> _floats = new List<Floating>();

        public int Balance => _balance;
        private int CurrentRound => Mathf.Max(1, _bootstrap?.Stage != null ? _bootstrap.Stage.CurrentRoundNumber : 1);
        private int CurrentRerollCost =>
            Mathf.Max(1, Mathf.RoundToInt(_rerollBase + _rerollPerRound * (CurrentRound - 1))) + _rerollRepeatStep * _rerollCount;

        private void OnEnable()
        {
            _balance = _startAmount;
            _pushed = false;
            UnitBase.OnHeroKilled += OnHeroKilled;
        }

        private void OnDisable()
        {
            UnitBase.OnHeroKilled -= OnHeroKilled;
            foreach (var button in _rerollButtons) if (button != null) button.onClick.RemoveListener(OnRerollClicked);
            _rerollButtons.Clear();
            _buttonHooked = false;
            if (_summaryCanvas != null) Destroy(_summaryCanvas.gameObject);
            _summaryCanvas = null;
            foreach (var portrait in _portraits.Values) if (portrait != null) portrait.Release();
            _portraits.Clear();
            if (_widgetCanvas != null) Destroy(_widgetCanvas.gameObject);
            _widgetCanvas = null;
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            _floats.Clear();
        }

        private void Update()
        {
            if (_view == null) _view = FindFirstObjectByType<UIBattleMutedPreviewView>(FindObjectsInactive.Include);
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (_view == null) return;
            HookRerollButton();

            // 새 판(재도전 포함)이 시작되면 재화를 시작값으로 되돌린다.
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage != null && !ReferenceEquals(stage, _stage))
            {
                _stage = stage;
                _balance = _startAmount;
                _pushed = false;
            }
            TrackClearBonus(stage);
            TrackRewardRequest();
            if (!_pushed) Push();
            RefreshRerollState();
            RefreshSummary(stage);
            RefreshNextWavePreview(stage);
            UpdatePulse();
            RefreshCombatWidget(stage);
            UpdateFloats();
        }

        private void Push()
        {
            if (_view == null) return;
            _view.SetCost(_balance, CurrentRerollCost);
            _pushed = true;
        }

        private void OnHeroKilled(UnitBase hero, int reward)
        {
            int gain = Mathf.Max(1, Mathf.RoundToInt(reward * _rewardScale));
            _balance += gain;
            _roundKillGain += gain;
            Push();
            _pulse = 0.3f;
            if (hero != null) SpawnFloating(WorldToScreen(hero.transform.position + Vector3.up * 0.4f), "+" + gain, new Color(1f, 0.86f, 0.35f));
        }

        // ───────────── 리롤

        /// <summary>
        /// 리롤 UI는 원래 그림(Image)뿐이고 Button 컴포넌트가 없어서 눌러도 아무 일이 없었다.
        /// 이름에 "Reroll"이 들어간 UI 요소마다 Button을 붙이고(레이캐스트 켬) 같은 리롤 동작에 연결한다.
        /// 한 번 누르면 맨 위에 있는 요소 하나만 반응하므로 리롤이 두 번 실행되지는 않는다.
        /// </summary>
        private void HookRerollButton()
        {
            if (!_buttonHooked)
            {
                foreach (var graphic in Resources.FindObjectsOfTypeAll<Graphic>())
                {
                    var go = graphic.gameObject;
                    if (!go.scene.IsValid() || go.name.IndexOf("Reroll", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (graphic.GetComponentInParent<Canvas>(true) == null) continue;
                    graphic.raycastTarget = true;
                    var button = go.GetComponent<Button>();
                    if (button == null)
                    {
                        button = go.AddComponent<Button>();
                        button.transition = Selectable.Transition.None;
                        button.targetGraphic = graphic;
                    }
                    button.onClick.RemoveListener(OnRerollClicked);
                    button.onClick.AddListener(OnRerollClicked);
                    _rerollButtons.Add(button);
                }
                if (_rerollButtons.Count > 0)
                {
                    _buttonHooked = true;
                    Debug.Log("[리롤] 리롤 버튼 " + _rerollButtons.Count + "개를 연결했습니다.");
                }
            }
            if (_rerollCostLabel == null)
                foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    if (text.gameObject.scene.IsValid() && text.gameObject.name == "RerollValue_Text") { _rerollCostLabel = text; break; }
        }

        /// <summary>전투가 끝나 보상 선택으로 넘어가는 순간 라운드 클리어 보너스를 한 번 지급한다(그 보상에서 바로 리롤에 쓸 수 있다).</summary>
        private void TrackClearBonus(StageManager stage)
        {
            if (stage == null) { _prevState = eStageState.IDLE; return; }
            var state = stage.State;
            if (state == _prevState) return;
            if (state == eStageState.COMBAT && _prevState != eStageState.COMBAT)
            {
                _roundKillGain = _roundBonusGain = _roundSpGain = 0; // 새 전투가 시작되면 이번 라운드 기록을 비운다
            }
            if (state == eStageState.GENERAL_REWARD && _prevState == eStageState.COMBAT)
            {
                int round = Mathf.Max(1, stage.CurrentRoundNumber);
                _roundNumberShown = round;
                int bonus = ClearBonus(round);
                var at = _pulseTarget != null ? ScreenCenter(_pulseTarget) : new Vector2(Screen.width * 0.12f, Screen.height * 0.2f);
                if (bonus > 0)
                {
                    _balance += bonus;
                    _roundBonusGain = bonus;
                    _pushed = false;
                    _pulse = 0.3f;
                    SpawnFloating(at, "+" + bonus + "  라운드 클리어 보너스", new Color(0.75f, 1f, 0.8f));
                }
                // 마일스톤 SP(10·20·30라운드…)는 이 순간 바로 지급한다. 같은 마일스톤을 두 번 받지는 않는다.
                _roundSpGain = SkillTreeStore.GrantForRound(round, MilestoneSpBonus());
                if (_roundSpGain > 0) SpawnFloating(at + new Vector2(0f, 46f), "SP +" + _roundSpGain + "  마일스톤 달성!", new Color(0.6f, 1f, 0.65f));
            }
            _prevState = state;
        }

        /// <summary>같은 보상(요청)에서만 리롤 횟수를 세고, 새 보상이 나오면 비용을 처음으로 되돌린다.</summary>
        private void TrackRewardRequest()
        {
            string id = _bootstrap?.Rewards?.Pending?.RequestId;
            if (id == _rerollRequestId) return;
            _rerollRequestId = id;
            _rerollCount = 0;
            _pushed = false;
        }

        private bool IsChoosingReward(out GridRunSession run)
        {
            run = _bootstrap != null ? _bootstrap.GridSession : null;
            var rewards = _bootstrap != null ? _bootstrap.Rewards : null;
            return run != null && rewards?.Pending != null && run.PendingRewardId == rewards.Pending.RequestId &&
                run.Grid != null && run.Grid.Phase == eGridPhase.REWARD && !run.Grid.HasPendingStorage;
        }

        private void RefreshRerollState()
        {
            bool choosing = IsChoosingReward(out _);
            // 비활성으로 막으면 눌렀을 때 안내 문구도 못 띄우므로 버튼은 항상 눌리게 두고, 안 되는 이유는 누른 뒤 안내로 알려 준다.
            if (_rerollCostLabel != null)
            {
                // 보상을 고르는 중인데 재화가 모자라면 비용을 빨갛게 보여 준다.
                Color want = choosing && _balance < CurrentRerollCost ? new Color(1f, 0.45f, 0.4f) : Color.white;
                if (_rerollCostLabel.color != want) _rerollCostLabel.color = want;
            }
        }

        private void OnRerollClicked()
        {
            Vector2 at = _rerollButtons.Count > 0 && _rerollButtons[0] != null ? ScreenCenter(_rerollButtons[0].transform as RectTransform) : new Vector2(Screen.width * 0.2f, Screen.height * 0.12f);
            if (!IsChoosingReward(out var run))
            {
                SpawnFloating(at, "보상을 고를 때만 쓸 수 있어요", new Color(0.8f, 0.85f, 1f));
                return;
            }
            int cost = CurrentRerollCost;
            if (_balance < cost)
            {
                SpawnFloating(at, "재화가 모자라요 (" + cost + " 필요)", new Color(1f, 0.5f, 0.45f));
                return;
            }
            if (!RedrawCandidates(run))
            {
                SpawnFloating(at, "다시 뽑지 못했어요", new Color(1f, 0.5f, 0.45f));
                return;
            }
            _balance -= cost;
            _rerollCount++;
            Push();
            RefreshHandCards();
            SpawnFloating(at, "-" + cost + "  다시 뽑았어요", new Color(0.7f, 0.95f, 1f));
        }

        /// <summary>보상 후보(Candidates)를 같은 규칙(GeneralRewardSource.Draw)으로 새로 뽑아 바꿔 끼운다.</summary>
        private bool RedrawCandidates(GridRunSession run)
        {
            var rewards = _bootstrap.Rewards;
            var source = rewards.GetType().GetField("_source", Private)?.GetValue(rewards) as GeneralRewardSource;
            var setter = rewards.GetType().GetProperty("Candidates")?.GetSetMethod(true);
            if (source == null || setter == null)
            {
                Debug.LogWarning("리롤 실패: 보상 후보를 바꿀 방법을 찾지 못했습니다(StageGridRewards 구조가 바뀌었을 수 있음).", this);
                return false;
            }
            setter.Invoke(rewards, new object[] { source.Draw(run.Grid.CanExpand) });
            return true;
        }

        /// <summary>손패(카드 UI)가 새 후보를 바로 그리도록 갱신을 요청한다.</summary>
        private void RefreshHandCards()
        {
            if (_handAdapter == null) _handAdapter = FindFirstObjectByType<UIGridStorageHandAdapter>(FindObjectsInactive.Include);
            _handAdapter?.GetType().GetMethod("RefreshItems", Private)?.Invoke(_handAdapter, null);
        }

        // ───────────── 얻을 때 재화 아이콘이 톡 커졌다 돌아온다

        private void UpdatePulse()
        {
            if (_pulseTarget == null)
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    if (t.gameObject.scene.IsValid() && t.name == "Currency" && t.GetComponentInParent<Canvas>(true) != null)
                    { _pulseTarget = t as RectTransform; break; }
                if (_pulseTarget == null) return;
            }
            if (_pulse > 0f) _pulse = Mathf.Max(0f, _pulse - Time.unscaledDeltaTime);
            float k = _pulse > 0f ? 1f + 0.18f * Mathf.Sin((_pulse / 0.3f) * Mathf.PI) : 1f;
            _pulseTarget.localScale = new Vector3(k, k, 1f);
        }

        // ───────────── 떠오르는 문구 ("+N", 안내)

        private static Vector2 WorldToScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector3 s = cam.WorldToScreenPoint(world);
            return new Vector2(s.x, s.y);
        }

        private static Vector2 ScreenCenter(RectTransform rect)
        {
            if (rect == null) return Vector2.zero;
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return new Vector2((bl.x + tr.x) * 0.5f, tr.y + 6f); // 버튼 바로 위
        }

        private void SpawnFloating(Vector2 screen, string message, Color color)
        {
            EnsureCanvas();
            var go = new GameObject("Float", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(700f, 60f);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.text = message;
            text.fontSize = 32f * Mathf.Max(0.8f, Screen.height / 1080f);
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(30, 18, 0, 255);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            _floats.Add(new Floating { Rect = rect, Text = text, Start = screen, Age = 0f });
        }

        private void UpdateFloats()
        {
            float rise = 70f * Mathf.Max(0.8f, Screen.height / 1080f);
            for (int i = _floats.Count - 1; i >= 0; i--)
            {
                var f = _floats[i];
                f.Age += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(f.Age / _floatSeconds);
                if (f.Rect == null || t >= 1f)
                {
                    if (f.Rect != null) Destroy(f.Rect.gameObject);
                    _floats.RemoveAt(i);
                    continue;
                }
                f.Rect.anchoredPosition = f.Start + new Vector2(0f, rise * Mathf.SmoothStep(0f, 1f, t));
                var c = f.Text.color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                f.Text.color = c;
            }
        }

        // ───────────── 라운드 보상 요약 (재화 · SP)

        private int ClearBonus(int round) =>
            (_clearBonusBase > 0 || _clearBonusPerRound > 0f)
                ? Mathf.Max(0, Mathf.RoundToInt(_clearBonusBase + _clearBonusPerRound * (round - 1))) : 0;

        /// <summary>이번 라운드를 클리어하면 받게 될 것을 "이번 웨이브" 패널 바로 아래에 미리 보여 준다. 배치(준비) 중에만 보이고,
        /// 전투 중에는 "이번 웨이브" 패널과 함께 숨긴다(전투 화면을 가리지 않게). 보상 선택 중에도 숨긴다.</summary>
        private void RefreshSummary(StageManager stage)
        {
            SetWavePanelsVisible(stage == null || stage.State != eStageState.COMBAT);
            bool show = stage != null && stage.State == eStageState.PREPARATION;
            if (!show)
            {
                if (_summaryCanvas != null && _summaryCanvas.enabled) _summaryCanvas.enabled = false;
                return;
            }
            int round = Mathf.Max(1, stage.CurrentRoundNumber);
            string signature = (stage.Progress != null ? stage.Progress.StageId : "") + "#" + round + "#" + SkillTreeStore.HighestMilestone + "#" + SkillTreeStore.HighestDripStep + "#" + UnitUnlockStore.UnlockedCount;
            if (signature != _previewSignature || _summaryCanvas == null) BuildPreviewText(stage, round, signature);
            if (_summaryCanvas == null || string.IsNullOrEmpty(_previewText)) return;
            PlaceSummaryUnderWavePanel();
            _summaryCanvas.enabled = true;
        }

        private string _previewSignature, _previewText;
        private Dictionary<string, int> _heroReward;

        private void BuildPreviewText(StageManager stage, int round, string signature)
        {
            _previewSignature = signature;
            EnsureSummary();
            int heroCount, killTotal;
            if (!TryGetRoundHeroReward(stage, round, out heroCount, out killTotal)) { _previewText = null; return; }
            bool isBoss = IsBossRound(stage, round);
            bool isFinal = round >= stage.TotalRounds;

            int bonus = ClearBonus(round);
            int sp = PreviewSp(round);
            var rows = new List<RewardRow>();

            rows.Add(new RewardRow(_gemIcon, Color.white, "재화", "최대 +" + (killTotal + bonus),
                "용사 " + heroCount + "명 처치 " + killTotal + " + 클리어 " + bonus, "#F2DC8C"));

            if (sp > 0)
                rows.Add(new RewardRow(_spIcon, new Color(0.6f, 0.9f, 0.6f), "SP", "+" + sp,
                    (round % 10 == 0 ? "마일스톤 보너스 포함" : SkillTreeStore.DripEveryRounds + "라운드마다 +1") + " · 스킬 해금", "#9BE59B"));
            else
            {
                int nextRound = 0;
                for (int r = round + 1; r <= stage.TotalRounds && nextRound == 0; r++)
                    if (SkillTreeStore.PreviewForRound(r, MilestoneSpBonus()) > 0) nextRound = r;
                rows.Add(new RewardRow(_spIcon, new Color(0.6f, 0.82f, 1f), "SP", "이번엔 없음",
                    nextRound > 0 ? "다음 SP: 라운드 " + nextRound : "이번 스테이지엔 더 없음", "#9CD2FF"));
            }

            string unlockId = UnitUnlockStore.PreviewForRound(round);
            if (unlockId != null)
            {
                var unlockRow = new RewardRow(_unlockIcon != null ? _unlockIcon : _cardIcon, new Color(1f, 0.82f, 0.4f), "마왕군 해금",
                    UnitUnlockStore.DisplayName(unlockId), "클리어하면 영구 해금", "#FFD27A");
                unlockRow.portrait = UnitPortrait(unlockId);
                rows.Add(unlockRow);
            }

            if (isFinal)
                rows.Add(new RewardRow(_clearIcon, new Color(0.94f, 0.63f, 0.71f), "스테이지 클리어", "마지막 라운드", "다음 라운드 보상은 없음", "#F0A0B4"));
            else
            {
                if (isBoss)
                    rows.Add(new RewardRow(_augmentIcon, Color.white, "증강", "1개 선택", "보스 처치 보상", "#C9A8FF"));
            }

            _previewText = "rows";
            LayoutSummary(rows);
        }

        private readonly Dictionary<string, RenderTexture> _portraits = new Dictionary<string, RenderTexture>();

        /// <summary>
        /// 유닛 프리팹의 스프라이트 파츠만 복사해 화면 밖에서 한 번 그린 그림을 돌려준다(유닛 스크립트가 실행되거나 전투에 등록되지 않는다).
        /// 그리지 못하면 null — 부르는 쪽이 아이콘으로 대신한다.
        /// </summary>
        private RenderTexture UnitPortrait(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            if (_portraits.TryGetValue(unitId, out var cached) && cached != null) return cached;
            try
            {
                var catalog = _bootstrap != null && _bootstrap.Config != null ? _bootstrap.Config.DemonArmyCatalog : null;
                var prefab = catalog != null ? catalog.FindPrefab(unitId) : null;
                if (prefab == null) return null;

                var root = new GameObject("UnitPortraitRoot");
                root.transform.position = new Vector3(5000f, 5000f, 0f);
                Bounds bounds = default;
                bool any = false;
                Transform pt = prefab.transform;
                Vector3 ps = pt.lossyScale;
                foreach (var r in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (r.sprite == null || r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    bool visible = true;
                    for (var c = r.transform; c != null; c = c.parent) { if (!c.gameObject.activeSelf) { visible = false; break; } if (c == pt) break; }
                    if (!visible) continue;

                    var go = new GameObject(r.name);
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = pt.InverseTransformPoint(r.transform.position);
                    go.transform.localRotation = Quaternion.Inverse(pt.rotation) * r.transform.rotation;
                    Vector3 ls = r.transform.lossyScale;
                    go.transform.localScale = new Vector3(Mathf.Abs(ps.x) > 1e-4f ? ls.x / ps.x : ls.x, Mathf.Abs(ps.y) > 1e-4f ? ls.y / ps.y : ls.y, ls.z);
                    var nr = go.AddComponent<SpriteRenderer>();
                    nr.sprite = r.sprite;
                    nr.color = r.color;
                    nr.flipX = r.flipX; nr.flipY = r.flipY;
                    nr.sharedMaterial = r.sharedMaterial;
                    nr.sortingLayerID = r.sortingLayerID;
                    nr.sortingOrder = r.sortingOrder;
                    if (!any) { bounds = nr.bounds; any = true; } else bounds.Encapsulate(nr.bounds);
                }
                if (!any) { Destroy(root); return null; }

                var camGo = new GameObject("UnitPortraitCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = ~0;
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
                cam.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y) * 0.5f * 1.15f;
                camGo.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
                // 1차: 넓게 그려서 실제로 그림이 있는 부분(알파)의 범위를 구한다. 스프라이트 사각형에는 투명 여백이 많아 그대로 그리면 작게 나온다.
                const int probe = 256;
                float size1 = cam.orthographicSize;
                Vector3 center1 = camGo.transform.position;
                var probeRt = new RenderTexture(probe, probe, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = probeRt;
                cam.Render();
                var prevActive = RenderTexture.active;
                RenderTexture.active = probeRt;
                var readable = new Texture2D(probe, probe, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, probe, probe), 0, 0);
                RenderTexture.active = prevActive;
                var px = readable.GetPixels32();
                Destroy(readable);
                probeRt.Release();

                int minX = probe, minY = probe, maxX = -1, maxY = -1;
                for (int y = 0; y < probe; y++)
                    for (int x = 0; x < probe; x++)
                        if (px[y * probe + x].a > 12)
                        {
                            if (x < minX) minX = x; if (x > maxX) maxX = x;
                            if (y < minY) minY = y; if (y > maxY) maxY = y;
                        }
                if (maxX >= minX)
                {
                    float unit = size1 * 2f / probe;
                    float cx = center1.x - size1 + (minX + maxX + 1) * 0.5f * unit;
                    float cy = center1.y - size1 + (minY + maxY + 1) * 0.5f * unit;
                    float half = Mathf.Max(maxX - minX + 1, maxY - minY + 1) * unit * 0.5f * 1.12f;
                    camGo.transform.position = new Vector3(cx, cy, -10f);
                    cam.orthographicSize = Mathf.Max(0.05f, half);
                }

                var rt = new RenderTexture(192, 192, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                Destroy(camGo);
                Destroy(root);
                _portraits[unitId] = rt;
                return rt;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("유닛 초상을 그리지 못했습니다: " + e.Message);
                return null;
            }
        }

        // ───────────── 마왕군 해금 알림 배너

        private GameObject _bannerGo;
        private Coroutine _bannerRoutine;

        /// <summary>라운드를 클리어해 새 마왕군이 해금됐을 때 화면 위쪽에 잠깐 알려 준다(유닛 그림 + 이름).</summary>
        public void AnnounceUnitUnlock(string unitId)
        {
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            if (_bannerGo != null) Destroy(_bannerGo);
            _bannerRoutine = StartCoroutine(BannerRoutine(unitId));
        }

        private System.Collections.IEnumerator BannerRoutine(string unitId)
        {
            EnsureCanvas();
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            var go = new GameObject("UnitUnlockBanner", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_canvas.transform, false);
            _bannerGo = go;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
            var bg = bgGo.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.03f, 0.05f, 0.94f);
            bg.raycastTarget = false;
            if (_frameSprite != null)
            {
                var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                frameGo.transform.SetParent(go.transform, false);
                var fr = (RectTransform)frameGo.transform;
                fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
                var frame = frameGo.GetComponent<Image>();
                frame.sprite = _frameSprite;
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = 2.6f;
                frame.raycastTarget = false;
            }

            float portraitSize = 92f * s, padX = 34f * s, gap = 16f * s;
            var portrait = UnitPortrait(unitId);
            var title = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            title.transform.SetParent(go.transform, false);
            if (_font != null) title.font = _font;
            title.fontSize = 26f * s;
            title.richText = true;
            title.alignment = TextAlignmentOptions.Left;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.raycastTarget = false;
            title.text = "<b><color=#FFD27A>새 마왕군 해금!</color></b>\n<size=130%><b><color=#F5F2E8>" + UnitUnlockStore.DisplayName(unitId) + "</color></b></size>";
            title.ForceMeshUpdate();
            Vector2 textSize = title.GetPreferredValues();

            float width = padX * 2f + (portrait != null ? portraitSize + gap : 0f) + textSize.x;
            float height = Mathf.Max(portraitSize, textSize.y) + 2f * 26f * s;
            rect.sizeDelta = new Vector2(width, height);
            float x = padX;
            if (portrait != null)
            {
                var pgo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                pgo.transform.SetParent(go.transform, false);
                var pr = (RectTransform)pgo.transform;
                pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f);
                pr.pivot = new Vector2(0f, 0.5f);
                pr.sizeDelta = new Vector2(portraitSize, portraitSize);
                pr.anchoredPosition = new Vector2(x, 0f);
                var raw = pgo.GetComponent<RawImage>();
                raw.texture = portrait;
                raw.raycastTarget = false;
                x += portraitSize + gap;
            }
            var tr = (RectTransform)title.transform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.sizeDelta = textSize;
            tr.anchoredPosition = new Vector2(x, 0f);

            // 위에서 내려오며 나타나고, 잠시 머문 뒤 사라진다
            float top = -Screen.height * 0.12f;
            for (float t0 = 0f; t0 < 0.35f; t0 += Time.unscaledDeltaTime)
            {
                float k = t0 / 0.35f;
                group.alpha = k;
                rect.anchoredPosition = new Vector2(0f, top + (1f - k) * 40f * s);
                yield return null;
            }
            group.alpha = 1f;
            rect.anchoredPosition = new Vector2(0f, top);
            float hold = 0f;
            while (hold < 3f) { hold += Time.unscaledDeltaTime; yield return null; }
            for (float t0 = 0f; t0 < 0.5f; t0 += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t0 / 0.5f;
                yield return null;
            }
            Destroy(go);
            _bannerGo = null;
            _bannerRoutine = null;
        }

        private struct RewardRow
        {
            public Sprite icon; public Color tint; public string title, value, sub, titleColor; public Texture portrait;
            public RewardRow(Sprite icon, Color tint, string title, string value, string sub, string titleColor)
            { this.icon = icon; this.tint = tint; this.title = title; this.value = value; this.sub = sub; this.titleColor = titleColor; portrait = null; }
        }

        /// <summary>보상 행마다 아이콘 + 두 줄 글(제목·값 / 설명)로 배치한다. 프레임은 "이번 웨이브" 패널과 같은 이미지다.</summary>
        private void LayoutSummary(List<RewardRow> rows)
        {
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            float padX = 30f * s, padTop = 26f * s, padBottom = 26f * s, iconSize = 36f * s, gap = 10f * s, rowGap = 8f * s;
            float titleSize = 23f * s, mainSize = 19f * s;

            for (int i = _summaryContent.childCount - 1; i >= 0; i--) Destroy(_summaryContent.GetChild(i).gameObject);

            var title = NewSummaryText("Title", "<b>이번 라운드 보상</b>", titleSize, new Color(0.95f, 0.86f, 0.55f));
            Vector2 titleSizePref = title.GetPreferredValues();

            var items = new List<(RewardRow row, TMP_Text text, Vector2 size)>();
            float maxTextW = 0f;
            foreach (var row in rows)
            {
                string body = "<size=100%><b><color=" + row.titleColor + ">" + row.title + "</color></b>  <color=#F5F2E8><b>" + row.value + "</b></color></size>\n"
                              + "<size=74%><color=#BDB8AE>" + row.sub + "</color></size>";
                var text = NewSummaryText("Row", body, mainSize, Color.white);
                Vector2 size = text.GetPreferredValues();
                maxTextW = Mathf.Max(maxTextW, size.x);
                items.Add((row, text, size));
            }

            float iconCol = iconSize;
            foreach (var it in items) if (it.row.portrait != null) iconCol = iconSize * 2.0f;
            float contentW = padX + iconCol + gap + maxTextW + padX;
            float width = Mathf.Max(_summaryMinWidth, contentW, titleSizePref.x + 2f * padX + 60f * s);
            float y = -padTop;

            // 제목 + 양옆 장식
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = titleSizePref;
            titleRect.anchoredPosition = new Vector2(width * 0.5f, y);
            if (_ornamentSprite != null)
            {
                float o = 20f * s;
                for (int side = -1; side <= 1; side += 2)
                {
                    var orn = NewSummaryImage("Ornament", _ornamentSprite, Color.white, o);
                    var r = (RectTransform)orn.transform;
                    r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                    r.pivot = new Vector2(0.5f, 0.5f);
                    r.anchoredPosition = new Vector2(width * 0.5f + side * (titleSizePref.x * 0.5f + 20f * s), y - titleSizePref.y * 0.5f);
                }
            }
            y -= titleSizePref.y + 10f * s;

            foreach (var (row, text, size) in items)
            {
                float rowH = Mathf.Max(row.portrait != null ? iconCol : iconSize, size.y);
                if (row.portrait != null)
                {
                    // 해금되는 유닛의 모습(스프라이트를 한 번 그려 둔 그림)
                    var goPortrait = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    goPortrait.transform.SetParent(_summaryContent, false);
                    var raw = goPortrait.GetComponent<RawImage>();
                    raw.texture = row.portrait;
                    raw.raycastTarget = false;
                    var pr = (RectTransform)goPortrait.transform;
                    pr.sizeDelta = new Vector2(iconCol, iconCol);
                    pr.anchorMin = pr.anchorMax = new Vector2(0f, 1f);
                    pr.pivot = new Vector2(0f, 0.5f);
                    pr.anchoredPosition = new Vector2(padX, y - rowH * 0.5f);
                }
                else if (row.icon != null)
                {
                    var icon = NewSummaryImage("Icon", row.icon, row.tint, iconSize);
                    var ir = (RectTransform)icon.transform;
                    ir.anchorMin = ir.anchorMax = new Vector2(0f, 1f);
                    ir.pivot = new Vector2(0f, 0.5f);
                    ir.anchoredPosition = new Vector2(padX, y - rowH * 0.5f);
                }
                var tr = (RectTransform)text.transform;
                tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
                tr.pivot = new Vector2(0f, 0.5f);
                tr.sizeDelta = size;
                tr.anchoredPosition = new Vector2(padX + iconCol + gap, y - rowH * 0.5f);
                y -= rowH + rowGap;
            }

            _summaryBox.sizeDelta = new Vector2(width, -y - rowGap + padBottom);
        }

        private TMP_Text NewSummaryText(string name, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_summaryContent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) tmp.font = _font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.richText = true;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.text = text;
            tmp.ForceMeshUpdate();
            return tmp;
        }

        private Image NewSummaryImage(string name, Sprite sprite, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_summaryContent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private float _summaryMinWidth;

        // ───────────── 카드 보상 선택 중에는 "이번 웨이브"가 다음 웨이브 정보를 보여 준다

        private string _nextWaveSignature;
        private float _nextWaveReassert;

        /// <summary>
        /// 라운드를 깨고 카드(·증강)를 고르는 동안에도 라운드 번호는 방금 깬 라운드 그대로라서, "이번 웨이브" 패널과 웨이브 숫자가 지난 웨이브 값에 머물렀다.
        /// 이 구간에는 다음에 싸울 웨이브의 용사 구성과 웨이브 번호를 대신 보여 준다. 배치 단계로 넘어가면 팀 코드가 같은 값을 다시 쓴다.
        /// </summary>
        private void RefreshNextWavePreview(StageManager stage)
        {
            bool reward = stage != null && (stage.State == eStageState.GENERAL_REWARD || stage.State == eStageState.AUGMENT);
            if (!reward || stage.CurrentRoundNumber >= stage.TotalRounds || _view == null) { _nextWaveSignature = null; return; }

            int next = stage.CurrentRoundNumber + 1;
            string signature = (stage.Progress != null ? stage.Progress.StageId : "") + "#" + next;
            if (signature == _nextWaveSignature && Time.unscaledTime < _nextWaveReassert) return;
            _nextWaveSignature = signature;
            _nextWaveReassert = Time.unscaledTime + 0.5f; // 팀 코드가 다른 이유로 값을 되돌려도 곧 다시 맞춘다

            var config = _bootstrap != null ? _bootstrap.Config : null;
            string stageId = stage.Progress != null ? stage.Progress.StageId : null;
            if (config == null || config.StageCatalog == null || string.IsNullOrEmpty(stageId)) return;
            StageDefinition definition;
            try { definition = config.StageCatalog.Resolve(stageId); }
            catch { return; }
            if (definition == null || next > definition.Rounds.Count) return;

            _view.SetWave(next, stage.TotalRounds);
            int slotCount = _view.EnemySlotCount;
            for (int i = 0; i < slotCount; i++) _view.SetEnemy(i, string.Empty, 0);

            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var spawn in definition.Rounds[next - 1].Spawns)
            {
                if (!counts.ContainsKey(spawn.HeroId)) { counts[spawn.HeroId] = 0; order.Add(spawn.HeroId); }
                counts[spawn.HeroId] += spawn.Count;
            }
            int direct = order.Count <= slotCount ? order.Count : Mathf.Max(0, slotCount - 1);
            for (int i = 0; i < direct; i++) _view.SetEnemy(i, HeroDisplayName(config, order[i]), counts[order[i]]);
            if (order.Count > slotCount && slotCount > 0)
            {
                int rest = 0;
                for (int i = direct; i < order.Count; i++) rest += counts[order[i]];
                _view.SetEnemy(slotCount - 1, "기타 " + (order.Count - direct) + "종", rest);
            }
        }

        private static string HeroDisplayName(InGamePrototypeConfigSO config, string heroId)
        {
            var catalog = config.HeroPoolCatalog;
            if (catalog == null) return heroId;
            foreach (var entry in catalog.CreateSnapshot())
            {
                if (entry == null || entry.HeroId != heroId || entry.Prefab == null) continue;
                var unit = entry.Prefab.GetComponentInChildren<UnitBase>(true);
                if (unit != null && unit.statData != null && !string.IsNullOrWhiteSpace(unit.statData.displayName)) return unit.statData.displayName;
            }
            return heroId;
        }

        private bool IsBossRound(StageManager stage, int round)
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

        /// <summary>이번 라운드에 나올 용사 수와, 모두 처치했을 때 받을 재화(처치 보상 합계)를 데이터에서 계산한다.</summary>
        private bool TryGetRoundHeroReward(StageManager stage, int round, out int heroCount, out int killTotal)
        {
            heroCount = killTotal = 0;
            var config = _bootstrap != null ? _bootstrap.Config : null;
            string stageId = stage.Progress != null ? stage.Progress.StageId : null;
            if (config == null || config.StageCatalog == null || string.IsNullOrEmpty(stageId)) return false;
            StageDefinition definition;
            try { definition = config.StageCatalog.Resolve(stageId); }
            catch { return false; }
            if (definition == null || round > definition.Rounds.Count) return false;

            if (_heroReward == null)
            {
                _heroReward = new Dictionary<string, int>();
                if (config.HeroPoolCatalog != null)
                    foreach (var entry in config.HeroPoolCatalog.CreateSnapshot())
                    {
                        var unit = entry.Prefab != null ? entry.Prefab.GetComponent<UnitBase>() : null;
                        int reward = unit != null && unit.statData != null ? unit.statData.killExpReward : entry.Experience;
                        _heroReward[entry.HeroId] = reward;
                    }
            }
            foreach (var spawn in definition.Rounds[round - 1].Spawns)
            {
                int reward = _heroReward.TryGetValue(spawn.HeroId, out var r) ? r : 0;
                heroCount += spawn.Count;
                killTotal += spawn.Count * Mathf.Max(1, Mathf.RoundToInt(reward * _rewardScale)); // 실제 지급(OnHeroKilled)과 같은 계산
            }
            return true;
        }

        /// <summary>이번 라운드를 클리어했을 때 받게 될 마일스톤 SP (이미 받은 마일스톤이면 0).</summary>
        private int PreviewSp(int round) => SkillTreeStore.PreviewForRound(round, MilestoneSpBonus());

        private int MilestoneSpBonus()
        {
            var sync = FindFirstObjectByType<RealSynergySync>();
            var traits = sync != null && sync.Traits != null ? sync.Traits : new TraitTree(Resources.LoadAll<TraitData>("Traits"));
            return traits.BuildModifiers().MilestoneSpBonus;
        }

        private RectTransform _wavePanel;
        private readonly List<CanvasGroup> _waveGroups = new List<CanvasGroup>();
        private bool _waveHidden;
        private float _waveShownAlpha = 1f;
        private float _nextWaveScan;

        /// <summary>"이번 웨이브" 패널(WavePreviewPanel*)을 보이거나 숨긴다. 팀 UI를 건드리지 않게 CanvasGroup 투명도로만 처리한다.</summary>
        private void SetWavePanelsVisible(bool visible)
        {
            if ((_waveGroups.Count == 0 || _waveGroups.Exists(g => g == null)) && Time.unscaledTime >= _nextWaveScan)
            {
                _nextWaveScan = Time.unscaledTime + 1f;
                _waveGroups.Clear();
                foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
                {
                    // 패널 안쪽 CanvasGroup은 펼침/접힘 연출이 알파를 쓰므로 건드리지 않고, 그 바깥 마스크(뷰포트)에 따로 그룹을 둔다.
                    if (!rect.gameObject.scene.IsValid() || rect.name != "WavePreviewViewport") continue;
                    var group = rect.GetComponent<CanvasGroup>();
                    if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
                    _waveGroups.Add(group);
                }
                _waveHidden = false;
            }
            if (visible && !_waveHidden) return;
            if (!visible && !_waveHidden) _waveShownAlpha = _waveGroups.Count > 0 && _waveGroups[0] != null ? Mathf.Max(0.01f, _waveGroups[0].alpha) : 1f;
            _waveHidden = !visible;
            foreach (var group in _waveGroups) // 숨기는 동안은 매 프레임 다시 적용 — 다른 UI 연출이 알파를 되돌려도 숨김이 유지된다
            {
                if (group == null) continue;
                group.alpha = visible ? _waveShownAlpha : 0f;
                group.blocksRaycasts = visible;
                group.interactable = visible;
            }
        }

        /// <summary>
        /// 보상 패널을 "이번 웨이브" 패널 아래 왼쪽 줄에 놓는다. 폭은 가운데 손패 카드(왼쪽 끝)를 가리지 않게 좁게 고정한다.
        /// 웨이브 패널은 이름이 바뀌어도 찾도록 제목 글자("이번 웨이브")로 찾고, 패널 안 그림 중 가장 아래·가장 왼쪽을 기준으로 삼는다.
        /// </summary>
        private void PlaceSummaryUnderWavePanel()
        {
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            if (_wavePanel == null || !_wavePanel.gameObject.activeInHierarchy) _wavePanel = FindWavePanel();

            Vector2 topLeft = new Vector2(Screen.width * 0.02f, Screen.height * 0.58f); // 못 찾았을 때의 대략 위치
            if (_wavePanel != null)
            {
                var canvas = _wavePanel.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
                float left = float.MaxValue, bottom = float.MaxValue;
                var gc = new Vector3[4];
                foreach (var g in _wavePanel.GetComponentsInChildren<Graphic>(false))
                {
                    if (g == null || !g.enabled || g.color.a < 0.05f) continue;
                    g.rectTransform.GetWorldCorners(gc);
                    Vector2 a0 = RectTransformUtility.WorldToScreenPoint(cam, gc[0]), b0 = RectTransformUtility.WorldToScreenPoint(cam, gc[2]);
                    if (Mathf.Abs(b0.x - a0.x) > Screen.width * 0.9f) continue; // 화면 전체를 덮는 막은 제외
                    left = Mathf.Min(left, Mathf.Min(a0.x, b0.x));
                    bottom = Mathf.Min(bottom, Mathf.Min(a0.y, b0.y));
                }
                if (left < float.MaxValue) topLeft = new Vector2(Mathf.Max(8f * s, left), bottom - 28f * s);
            }

            float width = 380f * s;
            if (Mathf.Abs(width - _summaryMinWidth) > 1f)
            {
                _summaryMinWidth = width;
                _previewSignature = null; // 폭이 바뀌면 크기를 다시 계산
            }
            _summaryBox.anchoredPosition = topLeft;
        }

        private RectTransform FindWavePanel()
        {
            foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
                if (rect.gameObject.scene.IsValid() && rect.gameObject.activeInHierarchy && rect.name.StartsWith("WavePreviewPanel")) return rect;

            RectTransform title = null;
            foreach (var t0 in Resources.FindObjectsOfTypeAll<Text>())
                if (t0.gameObject.scene.IsValid() && t0.gameObject.activeInHierarchy && t0.text != null && t0.text.Trim() == "이번 웨이브") { title = t0.rectTransform; break; }
            if (title == null)
                foreach (var t1 in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    if (t1.gameObject.scene.IsValid() && t1.gameObject.activeInHierarchy && t1.text != null && t1.text.Trim() == "이번 웨이브") { title = t1.rectTransform; break; }
            if (title == null) return null;

            // 제목에서 위로 올라가며, 화면에 비해 너무 크지 않은 가장 큰 덩어리를 패널로 본다
            RectTransform best = title;
            var cur = title;
            while (cur.parent is RectTransform parent && parent.GetComponent<Canvas>() == null)
            {
                var corners = new Vector3[4];
                parent.GetWorldCorners(corners);
                if (Mathf.Abs(corners[2].x - corners[0].x) > Screen.width * 0.6f || Mathf.Abs(corners[2].y - corners[0].y) > Screen.height * 0.45f) break;
                best = parent; cur = parent;
            }
            return best;
        }

        private void EnsureSummary()
        {
            if (_summaryCanvas != null) return;
            EnsureCanvas();
            var go = new GameObject("ClearRewardPreview", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _summaryCanvas = go.AddComponent<Canvas>();
            _summaryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _summaryCanvas.sortingOrder = 12; // 손패·드롭 존보다 아래, 시너지 팝업·결과 팝업보다 아래

            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(go.transform, false);
            _summaryBox = (RectTransform)boxGo.transform;
            _summaryBox.anchorMin = _summaryBox.anchorMax = Vector2.zero; // 화면 왼쪽 아래 기준 좌표
            _summaryBox.pivot = new Vector2(0f, 1f);                       // 왼쪽 위 모서리를 기준점으로
            var bg = boxGo.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.03f, 0.05f, 0.94f);
            bg.raycastTarget = false;

            // 프레임 이미지("이번 웨이브" 패널과 같은 9분할 프레임)
            if (_frameSprite != null)
            {
                var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                frameGo.transform.SetParent(boxGo.transform, false);
                var fr = (RectTransform)frameGo.transform;
                fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
                var frame = frameGo.GetComponent<Image>();
                frame.sprite = _frameSprite;
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = 2.6f; // 모서리 장식이 내용을 가리지 않게 테두리를 얇게
                frame.raycastTarget = false;
            }
            else
            {
                var outline = boxGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.55f, 0.1f, 0.12f, 0.95f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(boxGo.transform, false);
            _summaryContent = (RectTransform)contentGo.transform;
            _summaryContent.anchorMin = Vector2.zero; _summaryContent.anchorMax = Vector2.one;
            _summaryContent.offsetMin = _summaryContent.offsetMax = Vector2.zero;
            _summaryCanvas.enabled = false;
        }

        // ───────────── 전투 중 재화 표시 (준비 단계의 재화 아이콘은 전투 화면에서는 사라진다)

        private Canvas _widgetCanvas;
        private RectTransform _widgetBox;
        private TMP_Text _widgetText;
        private float _displayBalance;
        private bool _widgetPlaced;

        /// <summary>
        /// 전투 화면에는 재화 표시가 없어서(준비 화면에만 있다) 용사를 잡아도 쌓이는 게 안 보였다.
        /// 기존 재화 아이콘이 보이지 않는 동안에는 왼쪽 아래에 작은 재화 표시를 띄워 숫자가 실시간으로 올라가게 한다.
        /// </summary>
        private RectTransform _combatClone;
        private TMP_Text _cloneText;
        private bool _cloneFailed;

        /// <summary>
        /// 준비 화면의 재화 아이콘(불꽃 다이아몬드)을 그대로 복제해 전투 화면 쪽에도 같은 모양·같은 자리에 둔다.
        /// 전투 화면 페이지의 자식이라 전투 화면일 때만 보이고, 숫자와 "톡" 커지는 효과는 원본과 같이 갱신한다.
        /// 복제에 실패하면 false를 돌려주고 아래의 작은 임시 표시를 쓴다.
        /// </summary>
        private bool TryRefreshCloneWidget()
        {
            if (_cloneFailed) return false;
            if (_combatClone == null)
            {
                if (_pulseTarget == null) return false;
                var bridge = FindFirstObjectByType<UIInGameBattleBridge>(FindObjectsInactive.Include);
                var combatPage = bridge != null
                    ? typeof(UIInGameBattleBridge).GetField("_combatPage", Private)?.GetValue(bridge) as GameObject : null;
                var parent = combatPage != null ? combatPage.transform as RectTransform : null;
                if (parent == null) { _cloneFailed = true; return false; }

                var copy = Instantiate(_pulseTarget.gameObject, parent, false);
                copy.name = "Currency_Combat";
                copy.SetActive(true);
                // UI 그림·글자 외의 동작 스크립트는 복제본에서 제거(원본과 겹쳐 동작하지 않게)
                foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    var ns = behaviour.GetType().Namespace ?? "";
                    if (!ns.StartsWith("UnityEngine.UI") && !ns.StartsWith("TMPro")) Destroy(behaviour);
                }
                foreach (var graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

                _combatClone = (RectTransform)copy.transform;
                // 원본과 같은 화면 위치·크기에 놓는다(부모가 달라도 월드 기준으로 맞춘다)
                _combatClone.position = _pulseTarget.position;
                _combatClone.rotation = _pulseTarget.rotation;
                Vector3 ps = parent.lossyScale;
                Vector3 os = _pulseTarget.lossyScale;
                _combatClone.localScale = new Vector3(ps.x != 0 ? os.x / ps.x : 1f, ps.y != 0 ? os.y / ps.y : 1f, 1f);
                _cloneBaseScale = _combatClone.localScale;
                _combatClone.SetAsLastSibling();

                foreach (var text in copy.GetComponentsInChildren<TMP_Text>(true))
                    if (_cloneText == null || text.gameObject.name.IndexOf("Value", System.StringComparison.OrdinalIgnoreCase) >= 0) _cloneText = text;
                if (_cloneText == null) { Destroy(copy); _combatClone = null; _cloneFailed = true; return false; }
            }
            _displayBalance = Mathf.MoveTowards(_displayBalance, _balance, Mathf.Max(30f, Mathf.Abs(_balance - _displayBalance) * 6f) * Time.unscaledDeltaTime);
            _cloneText.text = Mathf.RoundToInt(_displayBalance).ToString();
            float k = _pulse > 0f ? 1f + 0.18f * Mathf.Sin((_pulse / 0.3f) * Mathf.PI) : 1f;
            _combatClone.localScale = new Vector3(_cloneBaseScale.x * k, _cloneBaseScale.y * k, 1f);
            return true;
        }

        private Vector3 _cloneBaseScale = Vector3.one;

        private void RefreshCombatWidget(StageManager stage)
        {
            if (TryRefreshCloneWidget())
            {
                if (_widgetCanvas != null && _widgetCanvas.enabled) _widgetCanvas.enabled = false;
                return;
            }
            bool currencyVisible = _pulseTarget != null && _pulseTarget.gameObject.activeInHierarchy;
            bool inRun = stage != null && stage.State != eStageState.IDLE && stage.State != eStageState.CLEARED &&
                         stage.State != eStageState.FAILED && stage.State != eStageState.CANCELLED;
            bool show = inRun && !currencyVisible && stage.State == eStageState.COMBAT;
            if (!show)
            {
                if (_widgetCanvas != null && _widgetCanvas.enabled) _widgetCanvas.enabled = false;
                _displayBalance = _balance;
                return;
            }
            EnsureWidget();
            if (_widgetCanvas == null) return;
            _widgetCanvas.enabled = true;
            PlaceWidget();

            // 숫자가 한 번에 바뀌지 않고 빠르게 올라간다
            _displayBalance = Mathf.MoveTowards(_displayBalance, _balance, Mathf.Max(30f, Mathf.Abs(_balance - _displayBalance) * 6f) * Time.unscaledDeltaTime);
            _widgetText.text = Mathf.RoundToInt(_displayBalance).ToString();
            float k = _pulse > 0f ? 1f + 0.22f * Mathf.Sin((_pulse / 0.3f) * Mathf.PI) : 1f;
            _widgetBox.localScale = new Vector3(k, k, 1f);
        }

        private void PlaceWidget()
        {
            // 왼쪽 아래, 하단 HUD 바로 위. 화면 크기에 맞춰 위치를 잡는다.
            _widgetBox.anchoredPosition = new Vector2(Screen.width * 0.02f, Screen.height * 0.2f);
        }

        private void EnsureWidget()
        {
            if (_widgetCanvas != null) return;
            EnsureCanvas();
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            var go = new GameObject("CombatCurrency", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _widgetCanvas = go.AddComponent<Canvas>();
            _widgetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _widgetCanvas.sortingOrder = 14;

            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(go.transform, false);
            _widgetBox = (RectTransform)boxGo.transform;
            _widgetBox.anchorMin = _widgetBox.anchorMax = Vector2.zero;
            _widgetBox.pivot = new Vector2(0f, 0f);
            _widgetBox.sizeDelta = new Vector2(190f * s, 64f * s);
            var bg = boxGo.GetComponent<Image>();
            bg.color = new Color(0.07f, 0.04f, 0.05f, 0.9f);
            bg.raycastTarget = false;
            var outline = boxGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.7f, 0.35f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            // 불꽃 아이콘(준비 화면과 같은 그림)
            Sprite flame = null;
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                if (sprite != null && sprite.name == "Icon_Flame") { flame = sprite; break; }
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(boxGo.transform, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(38f * s, 0f);
            iconRect.sizeDelta = new Vector2(44f * s, 44f * s);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = flame;
            icon.enabled = flame != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var textGo = new GameObject("Value", typeof(RectTransform));
            textGo.transform.SetParent(boxGo.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(70f * s, 0f);
            textRect.offsetMax = new Vector2(-14f * s, 0f);
            _widgetText = textGo.AddComponent<TextMeshProUGUI>();
            if (_font != null) _widgetText.font = _font;
            _widgetText.fontSize = 34f * s;
            _widgetText.fontStyle = FontStyles.Bold;
            _widgetText.alignment = TextAlignmentOptions.MidlineLeft;
            _widgetText.color = new Color(1f, 0.88f, 0.4f);
            _widgetText.raycastTarget = false;
            _widgetText.textWrappingMode = TextWrappingModes.NoWrap;
            _widgetCanvas.enabled = false;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            var go = new GameObject("CurrencyFloats", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 25; // 손패·드롭 존보다 위, 시너지 팝업(30)·결과 팝업(100)보다 아래
            const string sample = "+-0123456789보상을고를때만쓸수있어요재화가모자라요필요다시뽑았지못했라운드클리어이번보너스달성마일스톤스킬해금에사용다음남음없음최대용사명처치유닛카드장중선택";
            if (UiFontOverride.Current != null) { _font = UiFontOverride.Current; return; } // 씬 전체 폰트(던파 비트체)
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters(sample, out _, true, true)) { _font = font; break; }
        }
    }
}
