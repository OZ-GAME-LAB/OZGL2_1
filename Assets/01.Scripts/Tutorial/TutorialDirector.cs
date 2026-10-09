using System.Collections.Generic;
using System.Reflection;
using OZGL2.Grid;
using OZGL2.InGame;
using OZGL2.Stage;
using OZGL2.UIBridge;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.Tutorial
{
    /// <summary>
    /// 마왕이 직접 설명해 주는 튜토리얼·도움말 시스템(쿠키런의 설명 캐릭터처럼).
    /// - 화면 아래(또는 강조 대상이 아래쪽이면 위)에 살아 움직이는 마왕과 말풍선이 나오고, 글자가 한 글자씩 나타난다. 화면 아무 곳이나 누르면 다음 말.
    /// - 설명할 UI(웨이브 패널·손패·시너지·전투 시작 버튼 등)는 나머지를 어둡게 하고 금빛 틀로 강조한다.
    /// - 게임 상태를 지켜보다가 처음 보는 상황(첫 배치, 첫 전투, 첫 보상 선택, 첫 증강, 첫 보스 웨이브)에서 한 번만 자동으로 나온다.
    /// - 화면 오른쪽 위의 "?" 단추로 언제든 주제를 골라 다시 들을 수 있다.
    /// UI 프리팹·팀 코드는 수정하지 않는다(실행 중에 화면 위에 따로 그린다). 문장은 TutorialLibrary에서 고친다.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class TutorialDirector : MonoBehaviour
    {
        [SerializeField] private GameObject _kingPrefab;
        [SerializeField, Tooltip("마왕이 말풍선 반대쪽을 보고 있으면 켜서 좌우를 뒤집는다")] private bool _flipKing = false;
        [Header("희수 UI 조각(대화창 꾸미기)")]
        [SerializeField, Tooltip("말풍선 프레임(Frame_WavePreview_Flat, 9분할)")] private Sprite _frameSprite;
        [SerializeField, Tooltip("이름표(Frame_SynergyNameplate_Flat, 9분할)")] private Sprite _nameplateSprite;
        [SerializeField, Tooltip("모서리 마름모(Ornament_Diamond_Flat)")] private Sprite _cornerSprite;
        [SerializeField, Tooltip("이름표 양옆 장식(Ornament_WaveTitle_Flat)")] private Sprite _titleOrnamentSprite;
        [SerializeField, Min(10f)] private float _charsPerSecond = 44f;

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FDropZone = typeof(UIBattleCardHandView).GetField("_dropZone", Priv);

        // 흐름
        private InGamePrototypeBootstrap _bootstrap;
        private DemonKingPortrait _portrait;
        private TutorialSequence _seq;
        private int _index;
        private float _typed;
        private int _totalChars;
        private bool _pausedByUs;
        private float _prevTimeScale = 1f;
        private eStageState _lastState = eStageState.IDLE;
        private float _nextScan;
        private float _sceneStart;
        private readonly Dictionary<string, Transform> _popups = new Dictionary<string, Transform>();
        private readonly HashSet<string> _openPopups = new HashSet<string>();
        private bool _lobbyIntroQueued;
        private readonly List<(TutorialSequence seq, float at)> _queue = new List<(TutorialSequence, float)>();

        // 화면 요소
        private Canvas _canvas, _helpCanvas;
        private RectTransform _dialog, _portraitRect, _bubble, _nameTag, _hint, _helpButton, _topicPanel;
        private Image _shadow, _frameImage, _headerLine;
        private RectTransform[] _corners = new RectTransform[4];
        private RectTransform[] _titleOrnaments = new RectTransform[2];
        private RectTransform _tail, _progressFill;
        private float _popStart;
        private Image _blocker, _arrow;
        private RawImage _portraitImage;
        private TMP_Text _body, _counter, _hintText;
        private GameObject _topicCatcher;
        private float _hintTextWidth;
        private TMP_FontAsset _font;
        private bool _dialogAtTop, _dockLeft;
        private bool _arrowActive;
        private Rect _arrowRect;
        private float _stepStartedAt;
        private Vector2 _fitSize = new Vector2(600f, 200f);
        private int _fitScreenHeight;
        private float _showAlpha;
        private readonly Image[] _gates = new Image[4];
        private Rect _gateHole;
        private float _clickedAt = -1f;
        private float _releasedAt = -1f;
        private bool _hadTarget;
        private Vector2 _arrowPos;
        private bool _arrowShown;
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        public bool IsPlaying => _seq != null;

        private static bool IsLobby => SceneManager.GetActiveScene().name.StartsWith("Lobby");
        private string SceneKey => IsLobby ? "lobby" : "ingame";

        private float S => Mathf.Max(0.8f, Screen.height / 1080f);

        private void Awake()
        {
            // 게임 시작 설명(intro)을 아직 안 봤으면 첫 라운드에 기본 마왕군을 미리 깔지 않고 카드로 지급해 직접 놓아 보게 한다.
            // 한 번 본 뒤나 설명을 끈 경우, 로비에서는 원래대로 미리 배치한다.
            StageGridPreparation.InitialAsCard = !IsLobby && !TutorialStore.AutoDisabled && !TutorialStore.Seen(TutorialLibrary.Intro.Id);
        }

        // ───────────── 외부에서 부르기

        /// <summary>설명을 바로 시작한다. 이미 설명 중이면 끝난 뒤 이어서 나온다.</summary>
        public void Play(string sequenceId)
        {
            var seq = TutorialLibrary.Find(sequenceId);
            if (seq != null) Enqueue(seq, 0f);
        }

        // ───────────── 매 프레임

        private void Update()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (_sceneStart <= 0f) _sceneStart = Time.unscaledTime;

            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.2f;
                WatchGame();
            }

            if (_seq == null && _queue.Count > 0 && Time.unscaledTime >= _queue[0].at)
            {
                var next = _queue[0].seq;
                _queue.RemoveAt(0);
                Begin(next);
            }

            if (_seq != null) UpdateDialog();
            UpdateHelp();
        }

        // ───────────── 게임 상태를 보고 설명을 띄운다

        private void WatchGame()
        {
            if (IsLobby) { WatchLobby(); return; }
            // 웨이브 결과창이 떠 있으면(상태가 바뀌지 않아도) 그 설명을 한 번 한다
            if (!TutorialStore.AutoDisabled && !TutorialStore.Seen(TutorialLibrary.WaveResult.Id))
            {
                var results = FindFirstObjectByType<InGameWaveResultPresenter>(FindObjectsInactive.Include);
                if (results != null && results.Pending != null) Enqueue(TutorialLibrary.WaveResult, 0.6f);
            }
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage == null) { _lastState = eStageState.IDLE; return; }
            var state = stage.State;
            if (state == _lastState) return;
            _lastState = state;
            if (TutorialStore.AutoDisabled) return;

            int round = stage.CurrentRoundNumber;
            switch (state)
            {
                case eStageState.PREPARATION:
                    if (round <= 1 && !TutorialStore.Seen(TutorialLibrary.Intro.Id)) Enqueue(TutorialLibrary.Intro, 0.8f);
                    else if (IsBossRound(stage, round) && !TutorialStore.Seen(TutorialLibrary.Boss.Id)) Enqueue(TutorialLibrary.Boss, 0.8f);
                    break;
                case eStageState.COMBAT:
                    if (!TutorialStore.Seen(TutorialLibrary.Battle.Id)) Enqueue(TutorialLibrary.Battle, 1.2f);
                    break;
                case eStageState.GENERAL_REWARD:
                    if (!TutorialStore.Seen(TutorialLibrary.Reward.Id)) Enqueue(TutorialLibrary.Reward, 1.4f);
                    break;
                case eStageState.AUGMENT:
                    if (!TutorialStore.Seen(TutorialLibrary.Augment.Id)) Enqueue(TutorialLibrary.Augment, 0.9f);
                    break;
            }
        }

        /// <summary>로비: 처음 들어오면 로비 소개, 특성·스킬·도감 창이 처음 열리면 그 설명을 한다.</summary>
        private void WatchLobby()
        {
            if (TutorialStore.AutoDisabled) return;
            if (_lobbyIntroQueued || Time.unscaledTime - _sceneStart <= 1.2f) return;
            _lobbyIntroQueued = true;
            if (!TutorialStore.Seen(TutorialLibrary.LobbyTour.Id)) Enqueue(TutorialLibrary.LobbyTour, 0f);
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

        private void Enqueue(TutorialSequence seq, float delay)
        {
            if (seq == null || _seq == seq) return;
            foreach (var q in _queue) if (q.seq == seq) return;
            _queue.Add((seq, Time.unscaledTime + delay));
        }

        // ───────────── 진행

        private int _blipChars, _syllables;

        // 글자마다 높낮이가 다른 음계(펜타토닉 느낌) — 같은 글자는 늘 같은 높이라 말소리처럼 들린다
        private static readonly float[] BabbleSteps = { -4f, -2f, 0f, 2f, 3f, 5f, 7f, 9f };

        /// <summary>
        /// 마왕이 말하는 소리(옹알이): 방금 찍힌 글자들 중 글자·숫자만 세어 세 글자마다 한 음절을 낸다. 공백과 문장부호에서는 쉰다.
        /// 물음표·느낌표 앞 음절은 높게 끝난다. 소리 12종 중 하나가 무작위로 나오고(같은 것이 연달아 나오지 않음) 글자에 따라 높낮이가 바뀐다.
        /// </summary>
        private void SpeakNewChars(int from, int to)
        {
            var info = _body.textInfo;
            for (int i = from; i < to && i < info.characterCount; i++)
            {
                char c = info.characterInfo[i].character;
                if (!char.IsLetterOrDigit(c)) continue;
                if (++_syllables % 3 != 0) continue;
                float semitone = BabbleSteps[(c * 7 + 3) % BabbleSteps.Length] - 3f; // 마왕답게 전체를 낮춘다
                char next = i + 1 < info.characterCount ? info.characterInfo[i + 1].character : ' ';
                if (next == '?' || next == '!') semitone += 3f;
                Sfx.Play(SfxId.TutorialVoice, Mathf.Pow(2f, semitone / 12f));
            }
        }

        private void Begin(TutorialSequence seq)
        {
            EnsureUi();
            if (_canvas == null) return;
            Debug.Log("[튜토리얼] 시작: " + seq.Id + " (" + seq.Title + ")");
            _seq = seq;
            _index = 0;
            _canvas.enabled = true;
            _portrait.SetShown(true);
            if (seq.PauseGame && Time.timeScale > 0f)
            {
                _prevTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _pausedByUs = true;
            }
            ShowStep();
        }

        private void ShowStep()
        {
            var step = _seq.Steps[_index];
            _popStart = Time.unscaledTime; // 말풍선이 톡 튀어나오는 연출 시작
            _body.text = Highlight(step.Text);
            _body.maxVisibleCharacters = 0;
            FitBubble();
            _body.ForceMeshUpdate();
            _totalChars = _body.textInfo.characterCount;
            _typed = 0f;
            _blipChars = 0;
            _syllables = 0;
            _counter.text = (_index + 1) + " / " + _seq.Steps.Length;
            _clickedAt = -1f;
            _releasedAt = -1f;
            _hadTarget = false;
            _stepStartedAt = Time.unscaledTime;
            Debug.Log("[튜토리얼] " + _seq.Id + " " + (_index + 1) + "/" + _seq.Steps.Length + " (" + step.Kind + (string.IsNullOrEmpty(step.Target) ? "" : ", 대상 " + step.Target) + ")");
            if (step.Emphasis) _portrait.Gesture();
        }

        /// <summary>화면을 눌렀을 때: 말하기 단계에서만 다음으로 넘어간다(눌러 보기·창 닫기 단계는 직접 해야 넘어간다).</summary>
        private void Advance()
        {
            if (_seq == null || _seq.Steps[_index].Kind != StepKind.Talk) return;
            if (_typed < _totalChars) { _typed = _totalChars; return; } // 타이핑 중이면 한 번에 다 보여 준다
            NextStep();
        }

        private static void AddShadowTo(TMP_Text text)
        {
            if (text.GetComponent<Shadow>() != null) return;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>작은따옴표로 묶인 말('업적', '뒤로' 등)은 금빛으로 강조한다(따옴표는 빼고).</summary>
        private static string Highlight(string text) =>
            System.Text.RegularExpressions.Regex.Replace(text, "'([^']+)'", "<color=#FFD27A>$1</color>");

        private void NextStep()
        {
            _index++;
            if (_index >= _seq.Steps.Length) End();
            else ShowStep();
        }

        private void End()
        {
            if (_seq != null) TutorialStore.MarkSeen(_seq.Id);
            _seq = null;
            if (_canvas != null) _canvas.enabled = false;
            if (_arrow != null) { _arrow.enabled = false; _arrowShown = false; }
            SetGates(false, default);
            if (_blocker != null) _blocker.raycastTarget = true;
            if (_portrait != null) _portrait.SetShown(false);
            if (_pausedByUs)
            {
                if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = _prevTimeScale;
                _pausedByUs = false;
            }
            _showAlpha = 0f;
        }

        // ───────────── 대화창 갱신

        private void UpdateDialog()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                Advance();
            if (_seq == null) return;
            var current = _seq.Steps[_index];

            // 글자가 한 글자씩 나타난다
            if (_typed < _totalChars)
            {
                _typed = Mathf.Min(_totalChars, _typed + _charsPerSecond * Time.unscaledDeltaTime);
                int shown = Mathf.FloorToInt(_typed);
                _body.maxVisibleCharacters = shown;
                if (shown > _blipChars) { SpeakNewChars(_blipChars, shown); _blipChars = shown; }
            }
            else _body.maxVisibleCharacters = _totalChars;

            _showAlpha = Mathf.MoveTowards(_showAlpha, 1f, Time.unscaledDeltaTime * 4f);
            if (_progressFill != null) _progressFill.anchorMax = new Vector2(Mathf.Clamp01((_index + 1f) / _seq.Steps.Length), 1f);

            // 강조 대상: 화면을 칠하지 않고 화살표로 가리킨다
            var step = _seq.Steps[_index];
            Rect target = default;
            string targetKey = step.Target;
            if (step.Kind == StepKind.WaitPlace && GridSession != null && GridSession.Grid.HasSelection) targetKey = "grid"; // 카드를 끌고 있으면 놓을 칸을 가리킨다
            bool has = !string.IsNullOrEmpty(targetKey) && TryGetTarget(targetKey, out target);
            UpdateArrow(has, target);
            DriveStep(step, has, target);
            if (_seq == null) return;

            // 강조 대상과 화살표를 가리지 않는 모서리에 대화창을 놓는다
            ChoosePlacement(has, target);
            LayoutDialog();

            // 말하는 동안 마왕이 살짝 들썩이고, "다음" 표시는 위아래로 까딱인다
            bool talking = _typed < _totalChars;
            float pulse = talking ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 14f)) : 0f;
            _portraitRect.localScale = Vector3.one * (1f + pulse * 0.012f);
            bool showHint = !talking && current.Kind == StepKind.Talk;
            _hint.gameObject.SetActive(showHint);
            _hintText.gameObject.SetActive(showHint);
            // 삼각형은 안내 글 왼쪽에서 글 쪽(오른쪽)을 가리키며 까딱인다
            _hint.localRotation = Quaternion.Euler(0f, 0f, 90f);
            _hint.anchoredPosition = new Vector2(-(48f * S + _hintTextWidth + 28f * S) + Mathf.Sin(Time.unscaledTime * 6f) * 5f * S, 44f * S);

            var group = _dialog.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = _showAlpha;
        }

        /// <summary>
        /// 단계 종류에 따라 입력을 조절하고 다음으로 넘어갈 때를 판단한다.
        /// - 말하기: 화면 전체가 입력을 받아 어디를 눌러도 다음으로.
        /// - 눌러 보기: 강조한 단추 구역만 비우고 나머지는 막아서, 그 단추만 눌리게 한다. 단추를 눌렀다 떼면(실제 단추가 먼저 반응한 뒤) 잠시 후 다음으로.
        /// - 창 닫기: 아무것도 막지 않고, 지정한 창이 닫히면 다음으로.
        /// </summary>
        private void DriveStep(TutorialStep step, bool hasTarget, Rect target)
        {
            if (step.Kind == StepKind.Talk)
            {
                SetGates(false, default);
                _blocker.raycastTarget = true;
                return;
            }

            _blocker.raycastTarget = false;
            if (step.Kind == StepKind.WaitPlace)
            {
                // 카드를 끌어야 하니 아무것도 막지 않는다. 칸에 마왕군이 놓이면 다음으로.
                SetGates(false, default);
                if (GridSession != null && GridSession.Grid.PlacedCount > 0 && Time.unscaledTime - _stepStartedAt > 0.5f) NextStep();
                return;
            }
            if (step.Kind == StepKind.Click)
            {
                // 눌러도 되는 구역: 칸이 많은 목록은 목록 전체(HoleTarget), 아니면 강조한 칸
                Rect hole = target;
                bool hasHole = hasTarget;
                if (!string.IsNullOrEmpty(step.HoleTarget) && TryGetTarget(step.HoleTarget, out var holeRect)) { hole = holeRect; hasHole = true; }
                if (hasHole) { float pad = 8f * S; SetGates(true, Rect.MinMaxRect(hole.xMin - pad, hole.yMin - pad, hole.xMax + pad, hole.yMax + pad)); }
                else SetGates(false, default);

                if (step.Popup == "@page")
                {
                    // 하단 메뉴 단추: 누르면 업적·특성·스킬·도감 화면으로 넘어가며 로비 메뉴(단추)가 화면에서 사라진다.
                    // 단추가 사라지는 순간 바로 그 화면에서 설명을 시작한다(누른 위치로 판단하지 않는다).
                    if (TryGetOverlay(out var overlay))
                    {
                        // 새 로비: 로비 위에 화면이 올라온다(하단 메뉴는 그대로 있다) → 오버레이가 열렸는지로 판단
                        if (overlay.IsOpen && _clickedAt < 0f) _clickedAt = Time.unscaledTime;
                    }
                    else
                    {
                        if (hasTarget) _hadTarget = true;
                        else if (_hadTarget && _clickedAt < 0f) _clickedAt = Time.unscaledTime;
                    }
                    if (_clickedAt > 0f && Time.unscaledTime - _clickedAt > 0.15f) NextStep();

                    // 새 로비는 화면이 정말 열려야만 넘어간다(열리지 않았는데 로비 위에서 설명하지 않도록). 옛 로비만 시간 안전장치를 둔다.
                    if (!TryGetOverlay(out _))
                    {
                        var m = Mouse.current;
                        if (hasTarget && m != null && m.leftButton.wasReleasedThisFrame && _gateHole.Contains(m.position.ReadValue())) _releasedAt = Time.unscaledTime;
                        if (_releasedAt > 0f && Time.unscaledTime - _releasedAt > 1.2f && hasTarget) NextStep();
                    }
                    return;
                }

                // 그 구역에서 눌렀다 떼면 넘어간다. 지정한 창(예: 특성 설명창)이 열려도 넘어간다.
                var mouse = Mouse.current;
                if (hasHole && mouse != null && mouse.leftButton.wasReleasedThisFrame && _gateHole.Contains(mouse.position.ReadValue()))
                    _clickedAt = Time.unscaledTime;
                if (_clickedAt < 0f && !string.IsNullOrEmpty(step.Popup) && IsPopupOpen(step.Popup)) _clickedAt = Time.unscaledTime;
                if (_clickedAt > 0f && Time.unscaledTime - _clickedAt > 0.3f) NextStep();

                // 눌러 볼 칸을 못 찾으면 튜토리얼이 멈추지 않게 잠시 뒤 넘어간다
                if (!hasHole && Time.unscaledTime - _stepStartedAt > 4f)
                {
                    Debug.LogWarning("[튜토리얼] 눌러 볼 대상을 찾지 못해 넘어갑니다: " + step.Target);
                    NextStep();
                }
                return;
            }

            // 창 닫기: 막지 않는다
            SetGates(false, default);
            bool back = step.Popup == "@page" ? (TryGetOverlay(out var openOverlay) ? !openOverlay.IsOpen : LobbyMenuVisible()) : !IsPopupOpen(step.Popup);
            if (back) NextStep();
        }

        private GridRunSession GridSession => _bootstrap != null ? _bootstrap.GridSession : null;

        private UILobbyOverlayView _overlay;

        /// <summary>새 로비의 화면 관리자(특성·스킬·도감·업적을 로비 위에 올리는 곳). 옛 로비에는 없다.</summary>
        private bool TryGetOverlay(out UILobbyOverlayView overlay)
        {
            if (_overlay == null) _overlay = Object.FindFirstObjectByType<UILobbyOverlayView>(FindObjectsInactive.Include);
            overlay = _overlay;
            return overlay != null;
        }

        /// <summary>로비 메인 화면(하단 메뉴)이 보이는가. 다른 화면(업적·특성·스킬·도감)이 열려 있으면 사라진다.</summary>
        private static bool LobbyMenuVisible()
        {
            foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
                if (rt != null && rt.name == "BottomNavigation" && rt.gameObject.scene.IsValid()) return rt.gameObject.activeInHierarchy;
            return true;
        }

        private readonly Dictionary<string, Transform> _popupCache = new Dictionary<string, Transform>();

        private bool IsPopupOpen(string popupName)
        {
            if (!_popupCache.TryGetValue(popupName, out var tr) || tr == null)
            {
                tr = null;
                foreach (var candidate in Resources.FindObjectsOfTypeAll<Transform>())
                    if (candidate != null && candidate.name == popupName && candidate.gameObject.scene.IsValid()) { tr = candidate; break; }
                if (tr == null) return false;
                _popupCache[popupName] = tr;
            }
            return tr.gameObject.activeInHierarchy;
        }

        /// <summary>강조한 구역만 비우고 사방을 보이지 않는 막으로 덮어, 그 구역의 UI만 눌리게 한다.</summary>
        private void SetGates(bool on, Rect hole)
        {
            if (_gates[0] == null) return;
            if (!on) { foreach (var g in _gates) g.gameObject.SetActive(false); return; }
            _gateHole = hole;
            float w = Screen.width, h = Screen.height;
            float x0 = Mathf.Clamp(hole.xMin, 0f, w), x1 = Mathf.Clamp(hole.xMax, 0f, w);
            float y0 = Mathf.Clamp(hole.yMin, 0f, h), y1 = Mathf.Clamp(hole.yMax, 0f, h);
            Place(_gates[0], 0f, y1, w, h - y1);
            Place(_gates[1], 0f, 0f, w, y0);
            Place(_gates[2], 0f, y0, x0, y1 - y0);
            Place(_gates[3], x1, y0, w - x1, y1 - y0);
            foreach (var g in _gates) g.gameObject.SetActive(true);
        }

        private static void Place(Image img, float x, float y, float w, float h)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        /// <summary>
        /// 강조 대상을 화살표로 가리킨다. 대상이 화면 위쪽이면 아래에서 위로, 아래쪽이면 위에서 아래로, 오른쪽 가장자리면 왼쪽에서 오른쪽으로 가리킨다.
        /// 화살표는 위아래로 까딱이고, 대상이 바뀌면 부드럽게 미끄러져 간다.
        /// </summary>
        private void UpdateArrow(bool has, Rect target)
        {
            if (!has)
            {
                _arrow.enabled = false;
                _arrowShown = false;
                _arrowActive = false;
                return;
            }
            float s = S;
            float length = 150f * s, margin = 12f * s;
            float bob = (Mathf.Sin(Time.unscaledTime * 6.5f) * 0.5f + 0.5f) * 16f * s;
            float w = Screen.width, h = Screen.height;

            Vector2 pos;
            float rotation;
            if (target.center.x > w * 0.82f)
            {
                // 오른쪽 가장자리의 대상은 왼쪽에서 오른쪽을 가리킨다
                pos = new Vector2(target.xMin - length * 0.5f - margin - bob, target.center.y);
                rotation = 90f;
            }
            else if (target.yMax + length + margin < h - 110f * s)
            {
                pos = new Vector2(target.center.x, target.yMax + length * 0.5f + margin + bob); // 위에서 아래로
                rotation = 0f;
            }
            else
            {
                pos = new Vector2(target.center.x, target.yMin - length * 0.5f - margin - bob); // 아래에서 위로
                rotation = 180f;
            }
            pos.x = Mathf.Clamp(pos.x, length * 0.5f, w - length * 0.5f);
            pos.y = Mathf.Clamp(pos.y, length * 0.5f, h - length * 0.5f);

            // 가운데 정렬 기준으로 놓는다(앵커는 화면 왼쪽 아래)
            if (!_arrowShown) { _arrowPos = pos; _arrowShown = true; }
            else _arrowPos = Vector2.Lerp(_arrowPos, pos, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            var rt = _arrow.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(length * 0.62f, length);
            rt.anchoredPosition = _arrowPos;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
            _arrow.enabled = true;
            _arrow.color = new Color(1f, 1f, 1f, _showAlpha);
            float halfW = (rotation == 90f ? length : length * 0.62f) * 0.5f, halfH = (rotation == 90f ? length * 0.62f : length) * 0.5f;
            _arrowRect = Rect.MinMaxRect(_arrowPos.x - halfW, _arrowPos.y - halfH, _arrowPos.x + halfW, _arrowPos.y + halfH);
            _arrowActive = true;
        }

        private static Sprite MakeArrowSprite()
        {
            // 아래를 가리키는 금빛 화살표(어두운 테두리). 한 번만 그린다.
            const int w = 96, h = 148, outline = 5;
            bool[] inside = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = Mathf.Abs(x + 0.5f - w * 0.5f);
                    // y=0이 아래: 아래쪽 60px은 삼각형 머리, 그 위는 몸통
                    bool head = y < 62 && cx <= (y / 62f) * (w * 0.5f - outline - 2f);
                    bool shaft = y >= 56 && y < h - outline - 2 && cx <= 17f;
                    inside[y * w + x] = head || shaft;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color fillTop = new Color(1f, 0.93f, 0.55f), fillBottom = new Color(1f, 0.72f, 0.2f), line = new Color(0.27f, 0.08f, 0.12f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (inside[y * w + x]) { tex.SetPixel(x, y, Color.Lerp(fillBottom, fillTop, y / (float)(h - 1))); continue; }
                    bool near = false;
                    for (int dy = -outline; dy <= outline && !near; dy++)
                        for (int dx = -outline; dx <= outline; dx++)
                        {
                            if (dx * dx + dy * dy > outline * outline) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            if (inside[ny * w + nx]) { near = true; break; }
                        }
                    tex.SetPixel(x, y, near ? line : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 말풍선을 글 전체가 딱 들어가는 크기로 맞춘다. 한 줄에 다 들어가면 그 폭으로, 길면 최대 폭에서 줄바꿈해 그 높이로.
        /// 타이핑하는 동안 상자가 커지지 않도록 처음부터 전체 글 기준으로 잰다. 글은 양쪽 맞춤이라 줄 끝이 가지런하다.
        /// </summary>
        private void FitBubble()
        {
            float s = S;
            _fitScreenHeight = Screen.height;
            float padX = 44f * s, padTop = 84f * s, padBottom = 58f * s; // 아래쪽에는 건너뛰기 단추 자리
            _body.fontSize = 34f * s;
            _body.alignment = TextAlignmentOptions.TopJustified;
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.overflowMode = TextOverflowModes.Overflow;

            float maxW = Mathf.Min(Screen.width * 0.46f, 860f * s);
            float oneLine = _body.GetPreferredValues(_body.text, 100000f, 0f).x;
            float textW = Mathf.Clamp(oneLine, 520f * s, maxW - 2f * padX);
            float textH = _body.GetPreferredValues(_body.text, textW, 0f).y;
            _fitSize = new Vector2(textW + 2f * padX, Mathf.Max(textH + padTop + padBottom, 168f * s));

            var rt = _body.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padBottom);
            rt.offsetMax = new Vector2(-padX, -padTop);
            _counter.fontSize = 20f * s;
        }

        /// <summary>대화창(마왕 + 말풍선)이 차지하는 두 구역. 마왕은 모서리, 말풍선은 마왕 안쪽 옆.</summary>
        private void DialogRects(bool top, bool left, out Rect portrait, out Rect bubble)
        {
            float s = S, w = Screen.width, h = Screen.height;
            float p = 520f * s, margin = 16f * s, yEdge = 24f * s;
            float px = left ? margin : w - margin - p;
            float py = top ? h - yEdge * 0.2f - p : -yEdge * 0.2f;
            portrait = new Rect(px, py, p, p);
            float bw = _fitSize.x, bh = _fitSize.y;
            float bx = left ? margin + p * 0.68f : w - (margin + p * 0.68f) - bw;
            float by = top ? h - (yEdge + 8f * s) - bh : yEdge + 8f * s;
            bubble = new Rect(bx, by, bw, bh);
        }

        private static float OverlapArea(Rect a, Rect b)
        {
            float x = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float y = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return x > 0f && y > 0f ? x * y : 0f;
        }

        /// <summary>
        /// 가리킬 대상(과 화살표)이 있으면, 네 모서리 중 그것을 가장 덜 가리는 곳에 대화창을 놓는다.
        /// 순서는 오른쪽 아래(기본) → 오른쪽 위 → 왼쪽 아래 → 왼쪽 위. 현재 자리가 이미 안 가리면 그대로 둔다(흔들림 방지).
        /// </summary>
        private void ChoosePlacement(bool has, Rect target)
        {
            if (!has) { _dialogAtTop = false; _dockLeft = false; return; }
            float s = S;
            var avoidTarget = Rect.MinMaxRect(target.xMin - 12f * s, target.yMin - 12f * s, target.xMax + 12f * s, target.yMax + 12f * s);
            float Cost(bool top, bool left)
            {
                DialogRects(top, left, out var portrait, out var bubble);
                // 마왕 그림은 가장자리가 비어 있으니 가운데 부분만 센다
                var king = new Rect(portrait.x + portrait.width * 0.18f, portrait.y + portrait.height * 0.04f, portrait.width * 0.64f, portrait.height * 0.9f);
                float cost = OverlapArea(king, avoidTarget) + OverlapArea(bubble, avoidTarget);
                if (_arrowActive) cost += OverlapArea(king, _arrowRect) + OverlapArea(bubble, _arrowRect);
                return cost;
            }
            var candidates = new[] { (false, false), (true, false), (false, true), (true, true) };
            float current = Cost(_dialogAtTop, _dockLeft);
            if (current <= 1f) return;
            float best = float.MaxValue;
            (bool top, bool left) pick = (_dialogAtTop, _dockLeft);
            foreach (var c in candidates)
            {
                float cost = Cost(c.Item1, c.Item2);
                if (cost < best - 1f) { best = cost; pick = c; }
            }
            _dialogAtTop = pick.top;
            _dockLeft = pick.left;
        }

        private void LayoutDialog()
        {
            float s = S;
            if (_fitScreenHeight != Screen.height) FitBubble(); // 해상도가 바뀌면 다시 맞춘다
            float portraitSize = 520f * s, margin = 16f * s;
            float yEdge = 24f * s;

            // 마왕은 모서리, 말풍선은 마왕 안쪽 옆. 왼쪽에 있을 땐 그림을 뒤집어 말풍선 쪽을 보게 한다
            float anchorX = _dockLeft ? 0f : 1f, side = _dockLeft ? 1f : -1f;
            float anchorY = _dialogAtTop ? 1f : 0f;
            foreach (var rt in new[] { _portraitRect, _bubble })
            {
                rt.anchorMin = rt.anchorMax = new Vector2(anchorX, anchorY);
                rt.pivot = new Vector2(anchorX, anchorY);
            }
            _portraitImage.uvRect = (_dockLeft ^ _flipKing) ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            _portraitRect.sizeDelta = new Vector2(portraitSize, portraitSize);
            _portraitRect.anchoredPosition = new Vector2(side * margin, -yEdge * 0.2f);
            _bubble.sizeDelta = _fitSize;
            _bubble.anchoredPosition = new Vector2(side * (margin + portraitSize * 0.68f), _dialogAtTop ? -(yEdge + 8f * s) : yEdge + 8f * s);

            // 프레임의 테두리 두께를 해상도 배율에 맞춘다(그림 한 칸 = 화면 한 칸 × 배율)
            if (_frameImage != null) _frameImage.pixelsPerUnitMultiplier = 1f / s;

            // 머리: 이름(상자 없이 글자만) 아래로 금빛 머리줄
            float nameRowH = 54f * s;
            _nameTag.anchorMin = _nameTag.anchorMax = new Vector2(0f, 1f);
            _nameTag.pivot = new Vector2(0f, 0.5f);
            _nameTag.sizeDelta = new Vector2(220f * s, nameRowH);
            _nameTag.anchoredPosition = new Vector2(46f * s, -(nameRowH * 0.5f + 14f * s));
            foreach (var o in _titleOrnaments) if (o != null) o.gameObject.SetActive(false);
            if (_headerLine != null)
            {
                var hr = _headerLine.rectTransform;
                hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
                hr.offsetMin = new Vector2(30f * s, 0f); hr.offsetMax = new Vector2(-30f * s, 0f);
                hr.sizeDelta = new Vector2(-60f * s, 2f * s);
                hr.anchoredPosition = new Vector2(0f, -(nameRowH + 18f * s));
            }
            for (int i = 0; i < 4; i++)
            {
                var c = _corners[i];
                if (c == null) continue;
                bool right = (i & 1) == 1, top = (i & 2) == 2;
                c.anchorMin = c.anchorMax = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
                c.pivot = new Vector2(0.5f, 0.5f);
                c.sizeDelta = new Vector2(26f * s, 26f * s);
                c.anchoredPosition = new Vector2(right ? -2f * s : 2f * s, top ? -2f * s : 2f * s);
            }

            // 아래 줄 오른쪽: 넘기는 방법 안내(Space · 화면 클릭). 왼쪽의 삼각형이 이 글을 가리킨다
            float rowY = 26f * s + 18f * s;
            var ht = _hintText.rectTransform;
            ht.anchorMin = ht.anchorMax = new Vector2(1f, 0f); ht.pivot = new Vector2(1f, 0.5f);
            ht.sizeDelta = new Vector2(360f * s, 34f * s);
            ht.anchoredPosition = new Vector2(-48f * s, rowY);
            _hintText.fontSize = 22f * s;
            _hintText.alignment = TextAlignmentOptions.MidlineRight;
            _hintTextWidth = _hintText.GetPreferredValues(_hintText.text, 10000f, 0f).x;
            _hint.sizeDelta = new Vector2(26f * s, 22f * s);

            // 말풍선 그림자, 마왕 쪽으로 뾰족한 말꼬리, 나타날 때 살짝 커지는 연출(마왕 쪽 모서리를 기준으로)
            float pad = 46f * s;
            var sr = _shadow.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(anchorX, anchorY);
            sr.pivot = new Vector2(anchorX, anchorY);
            sr.sizeDelta = _fitSize + new Vector2(2f * pad, 2f * pad);
            Vector2 bp = _bubble.anchoredPosition;
            sr.anchoredPosition = new Vector2(bp.x + (anchorX > 0.5f ? pad : -pad), bp.y + (anchorY < 0.5f ? -pad : pad));
            float pop = Mathf.LerpUnclamped(0.9f, 1f, EaseOutBack(Mathf.Clamp01((Time.unscaledTime - _popStart) / 0.3f)));
            _bubble.localScale = Vector3.one * pop;
            sr.localScale = Vector3.one * pop;
            _tail.anchorMin = _tail.anchorMax = new Vector2(_dockLeft ? 0f : 1f, 0.5f);
            _tail.pivot = new Vector2(0.5f, 0.5f);
            _tail.sizeDelta = new Vector2(30f * s, 36f * s);
            _tail.anchoredPosition = new Vector2(side * -1f * 12f * s, 0f);
            _tail.localRotation = Quaternion.Euler(0f, 0f, _dockLeft ? -90f : 90f);

            // 진행 막대(몇 번째 말인지): 말풍선 바로 아래의 얇은 금빛 줄
            var pr = (RectTransform)_progressFill.parent;
            pr.anchorMin = new Vector2(0f, 0f); pr.anchorMax = new Vector2(1f, 0f); pr.pivot = new Vector2(0.5f, 1f);
            pr.offsetMin = new Vector2(30f * s, 0f); pr.offsetMax = new Vector2(-30f * s, 0f);
            pr.sizeDelta = new Vector2(-60f * s, 5f * s);
            pr.anchoredPosition = new Vector2(0f, -4f * s);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private static Sprite MakeGlowSprite()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        // ───────────── 화면 만들기

        private void EnsureUi()
        {
            if (_canvas != null) return;
            if (_font == null) _font = FindFont();

            // 마왕 초상(살아 움직이는 마왕)
            var pgo = new GameObject("TutorialPortrait");
            pgo.transform.SetParent(transform, false);
            _portrait = pgo.AddComponent<DemonKingPortrait>();
            _portrait.Build(_kingPrefab);

            var go = new GameObject("TutorialCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 400; // 인게임 결과창(100)보다 위, 새 로비의 특성·스킬·도감·업적 화면(200~211)보다도 위 — 낮으면 그 화면 뒤에 가려진다
            go.AddComponent<GraphicRaycaster>();

            // 화면 전체를 덮는 입력 막: 어디를 눌러도 다음 말로 넘어가고, 뒤의 UI는 눌리지 않는다
            _blocker = NewImage("Blocker", go.transform, null, new Color(0f, 0f, 0f, 0.003f));
            Stretch(_blocker.rectTransform);
            _blocker.raycastTarget = true;
            var click = _blocker.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
            click.onClick.AddListener(Advance);

            for (int i = 0; i < _gates.Length; i++)
            {
                _gates[i] = NewImage("Gate" + i, go.transform, null, new Color(0f, 0f, 0f, 0.003f));
                _gates[i].raycastTarget = true;
                _gates[i].gameObject.SetActive(false);
            }

            _arrow = NewImage("Arrow", go.transform, MakeArrowSprite(), Color.white);
            _arrow.raycastTarget = false;
            _arrow.enabled = false;

            // 대화창(마왕 + 말풍선)
            var dialogGo = new GameObject("Dialog", typeof(RectTransform), typeof(CanvasGroup));
            dialogGo.transform.SetParent(go.transform, false);
            _dialog = (RectTransform)dialogGo.transform;
            Stretch(_dialog);
            dialogGo.GetComponent<CanvasGroup>().blocksRaycasts = false;

            _portraitImage = new GameObject("King", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            _portraitImage.transform.SetParent(_dialog, false);
            _portraitImage.texture = _portrait.Texture;
            if (_flipKing) _portraitImage.uvRect = new Rect(1f, 0f, -1f, 1f);
            _portraitImage.raycastTarget = false;
            _portraitRect = _portraitImage.rectTransform;

            _shadow = NewImage("BubbleShadow", _dialog, MakeGlowSprite(), new Color(0f, 0f, 0f, 0.6f));
            _shadow.raycastTarget = false;

            var bubbleImage = NewImage("Bubble", _dialog, null, new Color(0.07f, 0.05f, 0.09f, 0.96f));
            bubbleImage.raycastTarget = false;
            _bubble = bubbleImage.rectTransform;
            var frameSprite = _frameSprite != null ? _frameSprite : Spr("Frame_WavePreview_Flat") ?? Spr("Frame_CostPlate");
            if (frameSprite != null)
            {
                _frameImage = NewImage("Frame", _bubble, frameSprite, Color.white);
                _frameImage.type = Image.Type.Sliced;
                _frameImage.raycastTarget = false;
                Stretch(_frameImage.rectTransform);
            }

            _body = NewText("Body", _bubble, 34f, new Color(0.97f, 0.94f, 0.87f), TextAlignmentOptions.TopLeft);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.overflowMode = TextOverflowModes.Overflow;
            _body.lineSpacing = 6f;
            var bodyRt = _body.rectTransform;
            bodyRt.anchorMin = Vector2.zero; bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(40f, 38f); bodyRt.offsetMax = new Vector2(-40f, -52f);

            var plateSprite = _nameplateSprite != null ? _nameplateSprite : Spr("Frame_SynergyNameplate_Flat") ?? Spr("Frame_CostPlate");
            var tagImage = NewImage("NameTag", _bubble, plateSprite, plateSprite == _nameplateSprite || plateSprite == null ? Color.white : new Color(0.45f, 0.1f, 0.14f, 1f));
            if (plateSprite != null) tagImage.type = Image.Type.Sliced;
            tagImage.raycastTarget = false;
            _nameTag = tagImage.rectTransform;
            var nameText = NewText("Name", _nameTag, 34f * S, new Color(1f, 0.9f, 0.55f), TextAlignmentOptions.Center);
            nameText.text = "마왕";
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            AddShadowTo(nameText);
            tagImage.enabled = false; // 이름표 상자는 없애고 글자만 둔다
            nameText.enableAutoSizing = true; nameText.fontSizeMin = 16f * S; nameText.fontSizeMax = 34f * S;
            Stretch(nameText.rectTransform);
            nameText.rectTransform.offsetMin = new Vector2(14f * S, 6f * S); nameText.rectTransform.offsetMax = new Vector2(-14f * S, -6f * S);

            _counter = NewText("Counter", _bubble, 22f, new Color(0.7f, 0.66f, 0.6f), TextAlignmentOptions.Right);
            _counter.gameObject.SetActive(false); // 남은 쪽 수(n / N)는 보이지 않게 한다

            var hintImage = NewImage("Hint", _bubble, MakeTriangle(), new Color(1f, 0.86f, 0.45f, 1f));
            hintImage.raycastTarget = false;
            _hint = hintImage.rectTransform;
            _hint.anchorMin = _hint.anchorMax = new Vector2(1f, 0f);
            _hint.pivot = new Vector2(0.5f, 0.5f);
            _hint.sizeDelta = new Vector2(26f, 22f);
            _hintText = NewText("HintText", _bubble, 22f * S, new Color(0.93f, 0.87f, 0.75f, 0.95f), TextAlignmentOptions.MidlineRight);
            _hintText.text = "Space · 화면 클릭: 다음";
            _hintText.raycastTarget = false;

            // 말풍선 꾸밈(희수 UI 조각): 이름표 아래 금빛 머리줄, 네 모서리 마름모, 이름표 양옆 장식
            var headerImage = NewImage("HeaderLine", _bubble, null, new Color(1f, 0.82f, 0.42f, 0.6f));
            headerImage.raycastTarget = false;
            _headerLine = headerImage;
            var cornerSprite = _cornerSprite != null ? _cornerSprite : Spr("Ornament_Diamond_Flat");
            if (cornerSprite != null)
                for (int i = 0; i < 4; i++)
                {
                    var d = NewImage("Corner" + i, _bubble, cornerSprite, Color.white);
                    d.raycastTarget = false;
                    _corners[i] = d.rectTransform;
                }
            var ornSprite = _titleOrnamentSprite != null ? _titleOrnamentSprite : Spr("Ornament_WaveTitle_Flat");
            if (ornSprite != null)
                for (int i = 0; i < 2; i++)
                {
                    var o = NewImage("NameOrnament" + i, _bubble, ornSprite, Color.white);
                    o.raycastTarget = false;
                    _titleOrnaments[i] = o.rectTransform;
                }

            // 말꼬리(마왕 쪽으로 뾰족)와 진행 막대
            var tailImage = NewImage("Tail", _bubble, MakeTriangle(), new Color(0.07f, 0.05f, 0.09f, 0.96f));
            tailImage.raycastTarget = false;
            _tail = tailImage.rectTransform;
            tailImage.transform.SetAsFirstSibling();
            var barBg = NewImage("Progress", _bubble, null, new Color(1f, 1f, 1f, 0.12f));
            barBg.raycastTarget = false;
            var barFill = NewImage("Fill", barBg.rectTransform, null, new Color(1f, 0.82f, 0.42f, 0.95f));
            barFill.raycastTarget = false;
            _progressFill = barFill.rectTransform;
            _progressFill.anchorMin = new Vector2(0f, 0f); _progressFill.anchorMax = new Vector2(0f, 1f); _progressFill.pivot = new Vector2(0f, 0.5f);
            _progressFill.offsetMin = Vector2.zero; _progressFill.offsetMax = Vector2.zero;

            _canvas.enabled = false;
            EnsureHelp();
        }

        // ───────────── 도움말(?) 단추와 주제 목록

        private void EnsureHelp()
        {
            if (_helpCanvas != null) return;
            var go = new GameObject("TutorialHelp", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _helpCanvas = go.AddComponent<Canvas>();
            _helpCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _helpCanvas.sortingOrder = 40;
            go.AddComponent<GraphicRaycaster>();

            // 주제 목록 바깥을 누르면 닫히게 하는 투명한 막
            var catcher = NewImage("Catcher", go.transform, null, new Color(0f, 0f, 0f, 0.004f));
            Stretch(catcher.rectTransform);
            catcher.raycastTarget = true;
            catcher.gameObject.AddComponent<Button>().onClick.AddListener(() => SetTopicsOpen(false));
            _topicCatcher = catcher.gameObject;
            _topicCatcher.SetActive(false);

            // 로비 난이도 화살표 단추와 같은 모양의 메달: 어두운 원판 + 금빛 이중 테두리 + 네 방향 마름모, 숨 쉬는 빛, 올리면 커진다
            var rootGo = new GameObject("HelpButton", typeof(RectTransform));
            rootGo.transform.SetParent(go.transform, false);
            _helpButton = (RectTransform)rootGo.transform;
            _helpButton.anchorMin = _helpButton.anchorMax = new Vector2(1f, 1f);
            _helpButton.pivot = new Vector2(0.5f, 1f);
            _helpButton.sizeDelta = new Vector2(60f, 60f);
            Color gold = new Color(0.96f, 0.76f, 0.38f, 1f);
            var discSprite = MakeMedallionSprite(0f);
            var ringSprite = MakeMedallionSprite(0.9f);
            _helpGlow = NewImage("Glow", _helpButton, MakeGlowSprite(), new Color(1f, 0.7f, 0.28f, 0.3f));
            _helpGlow.raycastTarget = false;
            _helpGlow.rectTransform.anchorMin = _helpGlow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _helpGlow.rectTransform.sizeDelta = new Vector2(120f, 120f);
            var disc = NewImage("Disc", _helpButton, discSprite, new Color(0.09f, 0.04f, 0.06f, 0.97f));
            Stretch(disc.rectTransform);
            var button = disc.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SetTopicsOpen(_topicPanel == null || !_topicPanel.gameObject.activeSelf));
            _helpHover = disc.gameObject.AddComponent<HelpHover>();
            var ring = NewImage("Ring", _helpButton, ringSprite, gold);
            ring.raycastTarget = false;
            Stretch(ring.rectTransform);
            var innerRing = NewImage("InnerRing", _helpButton, ringSprite, new Color(gold.r, gold.g, gold.b, 0.45f));
            innerRing.raycastTarget = false;
            Stretch(innerRing.rectTransform);
            innerRing.rectTransform.offsetMin = new Vector2(6f, 6f); innerRing.rectTransform.offsetMax = new Vector2(-6f, -6f);
            for (int i = 0; i < 4; i++)
            {
                var tick = NewImage("Tick", _helpButton, _cornerSprite, gold);
                tick.raycastTarget = false;
                var tr = tick.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f); tr.pivot = new Vector2(0.5f, 0.5f);
                tr.sizeDelta = new Vector2(13f, 13f);
                float a = i * Mathf.PI * 0.5f;
                tr.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 30f;
                if (_cornerSprite == null) tr.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            var q = NewText("Q", _helpButton, 36f, new Color(1f, 0.88f, 0.52f), TextAlignmentOptions.Center);
            q.text = "?";
            q.fontStyle = FontStyles.Bold;
            q.outlineWidth = 0.2f; q.outlineColor = new Color32(40, 8, 14, 255);
            Stretch(q.rectTransform);

            BuildTopicPanel(go.transform);
            _helpCanvas.enabled = false;
        }

        private Image _helpGlow;
        private HelpHover _helpHover;
        private float _helpHoverK;

        private sealed class HelpHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            public bool Hover;
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) => Hover = true;
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => Hover = false;
        }

        /// <summary>원판(inner = 0) 또는 속이 빈 고리(inner = 안쪽 반지름 비율)를 그린 스프라이트.</summary>
        private static Sprite MakeMedallionSprite(float inner)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float outer = n * 0.5f - 1f, innerR = outer * inner;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    float a = Mathf.Clamp01(outer - d) * (inner > 0f ? Mathf.Clamp01(d - innerR) : 1f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private void BuildTopicPanel(Transform parent)
        {
            var panel = NewImage("Topics", parent, null, new Color(0.07f, 0.05f, 0.09f, 0.96f));
            _topicPanel = panel.rectTransform;
            _topicPanel.anchorMin = _topicPanel.anchorMax = new Vector2(1f, 1f);
            _topicPanel.pivot = new Vector2(1f, 1f);
            var frameSprite = Spr("Frame_CostPlate");
            if (frameSprite != null)
            {
                var frame = NewImage("Frame", _topicPanel, frameSprite, Color.white);
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = 2.6f;
                frame.raycastTarget = false;
                Stretch(frame.rectTransform);
            }
            var title = NewText("Title", _topicPanel, 28f, new Color(1f, 0.88f, 0.55f), TextAlignmentOptions.Center);
            title.text = "마왕에게 묻기";
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(0f, 60f); tr.anchoredPosition = new Vector2(0f, -22f);

            float y = -88f;
            foreach (var seq in TutorialLibrary.HelpTopics(SceneKey))
            {
                var captured = seq;
                var row = NewImage("Topic", _topicPanel, null, new Color(0.22f, 0.12f, 0.16f, 0.95f));
                var rt = row.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(-56f, 52f); rt.anchoredPosition = new Vector2(0f, y);
                var b = row.gameObject.AddComponent<Button>();
                var colors = b.colors;
                colors.highlightedColor = new Color(1.25f, 1.1f, 1.1f, 1f);
                colors.pressedColor = new Color(0.8f, 0.7f, 0.7f, 1f);
                b.colors = colors;
                b.onClick.AddListener(() => { SetTopicsOpen(false); Enqueue(captured, 0f); });
                var label = NewText("Label", rt, 26f, new Color(0.97f, 0.93f, 0.85f), TextAlignmentOptions.Center);
                label.text = seq.Title;
                Stretch(label.rectTransform);
                y -= 60f;
            }
            _topicPanel.sizeDelta = new Vector2(360f, -y + 30f);
            _topicPanel.gameObject.SetActive(false);
        }

        private void SetTopicsOpen(bool open)
        {
            if (_topicPanel == null) return;
            _topicPanel.gameObject.SetActive(open);
            _topicCatcher.SetActive(open);
        }

        /// <summary>오른쪽 위 「?」 단추(마왕에게 묻기) 표시 여부. 다시 쓰려면 true 로 바꾸고 _seq == null 조건을 함께 둔다.</summary>
        private static readonly bool ShowHelpButton = false;

        private void UpdateHelp()
        {
            if (_helpCanvas == null) return;
            // 오른쪽 위 「?」(마왕에게 묻기)는 로비·인게임 모두 보이지 않게 했다(자동으로 나오는 설명은 그대로)
            bool show = ShowHelpButton;
            if (_helpCanvas.enabled != show) _helpCanvas.enabled = show;
            if (!show) { SetTopicsOpen(false); return; }

            float s = S;
            _helpHoverK = Mathf.MoveTowards(_helpHoverK, _helpHover != null && _helpHover.Hover ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            _helpButton.localScale = Vector3.one * (s * (1f + 0.12f * _helpHoverK));
            if (_helpGlow != null)
            {
                var gc = _helpGlow.color;
                gc.a = Mathf.Lerp(0.16f, 0.32f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f)) + 0.3f * _helpHoverK;
                _helpGlow.color = gc;
            }
            // 설정(메뉴) 단추 바로 아래, 같은 세로 줄(가운데)에 맞춘다. 설정 단추를 못 찾으면 예전 자리(오른쪽 위)를 쓴다.
            Vector2 pos = new Vector2(-24f * s - 27f * s, -108f * s);
            var settings = FindSettingsButton();
            if (settings != null)
            {
                var corners = new Vector3[4];
                settings.GetWorldCorners(corners);
                var canvas = settings.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
                Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 br = RectTransformUtility.WorldToScreenPoint(cam, corners[3]);
                pos = new Vector2((bl.x + br.x) * 0.5f - Screen.width, bl.y - 8f * s - Screen.height);
            }
            _helpButton.anchoredPosition = pos;
            _topicPanel.localScale = Vector3.one * s;
            _topicPanel.anchoredPosition = new Vector2(-24f * s, pos.y - 60f * s - 12f * s);
        }

        private RectTransform _settingsRect;
        private float _nextSettingsFind;

        /// <summary>오른쪽 위 설정(메뉴) 단추. 화면이 바뀌면 사라질 수 있어 1초에 한 번만 다시 찾는다.</summary>
        private RectTransform FindSettingsButton()
        {
            if (_settingsRect != null && _settingsRect.gameObject.activeInHierarchy) return _settingsRect;
            if (Time.unscaledTime < _nextSettingsFind) return null;
            _nextSettingsFind = Time.unscaledTime + 1f;
            _settingsRect = null;
            foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
                if (rect != null && rect.name == "SettingsButton" && rect.gameObject.scene.IsValid() && rect.gameObject.activeInHierarchy) { _settingsRect = rect; break; }
            return _settingsRect;
        }

        // ───────────── 강조할 UI 찾기

        private bool TryGetTarget(string key, out Rect rect)
        {
            rect = default;
            if (key.StartsWith("popup.close:")) return RectOfPopupClose(key.Substring("popup.close:".Length), out rect);
            switch (key)
            {
                case "wave": return RectOfNamed(new[] { "WavePreviewPanel", "WaveText" }, out rect);
                case "start": return RectOfNamed(new[] { "Button_BattleStart" }, out rect);
                case "reroll": return RectOfNamed(new[] { "Button_Reroll" }, out rect);
                case "currency": return RectOfNamed(new[] { "Currency", "Currency_Combat", "CombatCurrency" }, out rect);
                case "xp": return RectOfNamed(new[] { "ExperienceTrack", "ExperienceFillSoul" }, out rect);
                case "augments": return RectOfNamed(new[] { "AugmentListToggle" }, out rect);
                case "synergy": return RectOfNamed(new[] { "SynergyTracker_InGame", "Synergy_0", "Synergy_1", "Synergy_2", "Synergy_3" }, out rect, true);
                case "skills": return RectOfSkillSlots(out rect);
                case "hand": return RectOfHand(out rect);
                case "grid": return RectOfGrid(out rect);
                case "king": return RectOfKing(out rect);
                case "level": return RectOfNamed(new[] { "LevelFrame", "Level" }, out rect);
                case "nav": return RectOfNamed(new[] { "BottomNavigation" }, out rect);
                case "stage": return RectOfNamed(new[] { "CurrentStage" }, out rect);
                case "startbtn": return RectOfNamed(new[] { "StartButton" }, out rect);
                case "traittree": return RectOfNamed(new[] { "TraitTreeScroll" }, out rect);
                case "skillpoints": return RectOfNamed(new[] { "SkillPoints" }, out rect);
                case "slots": return RectOfNamed(new[] { "Slot_1", "Slot_2", "Slot_3" }, out rect, true);
                case "codexlist": return RectOfNamed(new[] { "UnitScroll" }, out rect);
                case "reward": return RectOfUnder("ClearRewardPreview", new[] { "Box" }, out rect);
                case "speed": return RectOfUnder("SpeedControl", new[] { "Bar" }, out rect);
                case "result.title": return RectOfUnder("Popup_WaveResult", new[] { "Outcome" }, out rect);
                case "result.xp": return RectOfUnder("Popup_WaveResult", new[] { "Experience", "XpTrack" }, out rect);
                case "result.confirm": return RectOfUnder("Popup_WaveResult", new[] { "Confirm" }, out rect);
                case "codex.stars": return RectOfPicked(n => n == "Stars", "Viewport", out rect);
                case "codex.next": return RectOfPicked(n => n == "NextAppearance", "Viewport", out rect);
                case "ach.count": return RectOfUnder("Canvas_Achievements", new[] { "CompletedBadge", "CompletedCount" }, out rect);
                case "ach.list": return RectOfUnder("Canvas_Achievements", new[] { "Viewport" }, out rect);
                case "codex.factions": return RectOfUnder("Canvas_UnitCodex", new[] { "Faction_0", "Faction_1" }, out rect);
                case "codex.list": return RectOfUnder("Canvas_UnitCodex", new[] { "Viewport" }, out rect);
                case "codex.count": return RectOfUnder("Canvas_UnitCodex", new[] { "DiscoveredCount" }, out rect);
                case "trait.points": return RectOfNamed(new[] { "PointsFrame", "Points" }, out rect);
                case "trait.tree": return RectOfNamed(new[] { "TraitTreeViewport", "TraitTreeScroll" }, out rect);
                case "trait.node": return RectOfPicked(n => n.StartsWith("Trait_") && n.EndsWith("_A1"), "TraitTreeViewport", out rect);
                case "trait.detail": return RectOfNamed(new[] { "SelectedTraitDetail" }, out rect);
                case "trait.recenter": return RectOfNamed(new[] { "Recenter" }, out rect);
                case "trait.reset": return RectOfNamed(new[] { "ResetTraits" }, out rect);
                case "skill.level": return RectOfNamed(new[] { "AccountStatus" }, out rect);
                case "skill.sp": return RectOfNamed(new[] { "RemainingSp" }, out rect);
                case "skill.equipped": return RectOfNamed(new[] { "EquippedTitle", "EquippedSlot_0", "EquippedSlot_1", "EquippedSlot_2" }, out rect, true);
                case "skill.category": return RectOfNamed(new[] { "Category_0", "Category_1", "Category_2", "Category_3" }, out rect, true);
                case "skill.grid": return RectOfNamed(new[] { "OwnedSkillsPanel" }, out rect);
                case "skill.card": return RectOfPicked(n => n.StartsWith("SkillCard_"), "SkillGrid", out rect);
                case "skill.detail": return RectOfNamed(new[] { "SkillDetailPanel" }, out rect);
                case "skill.buttons": return RectOfNamed(new[] { "Equip", "Unequip" }, out rect, true);
                case "skill.save": return RectOfNamed(new[] { "Save" }, out rect);
                case "btn.achievement": return RectOfNamed(new[] { "ReservedButton" }, out rect);
                case "btn.traits": return RectOfNamed(new[] { "TraitsButton" }, out rect);
                case "btn.skills": return RectOfNamed(new[] { "SkillsButton" }, out rect);
                case "btn.codex": return RectOfNamed(new[] { "CodexButton" }, out rect);
            }
            return false;
        }

        private static Rect ScreenRectOf(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static bool Union(ref Rect acc, ref bool any, Rect r)
        {
            if (r.width < 1f || r.height < 1f) return false;
            acc = any ? Rect.MinMaxRect(Mathf.Min(acc.xMin, r.xMin), Mathf.Min(acc.yMin, r.yMin), Mathf.Max(acc.xMax, r.xMax), Mathf.Max(acc.yMax, r.yMax)) : r;
            any = true;
            return true;
        }

        private static bool RectOfNamed(string[] names, out Rect rect, bool unionAll = false)
        {
            rect = default;
            bool any = false;
            foreach (var name in names)
            {
                foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
                {
                    if (rt == null || rt.name != name || !rt.gameObject.scene.IsValid() || !rt.gameObject.activeInHierarchy) continue;
                    Union(ref rect, ref any, ScreenRectOf(rt));
                    if (!unionAll) return any;
                }
                if (any && !unionAll) return true;
            }
            return any;
        }

        /// <summary>열려 있는 화면(screen) 안에서 이름이 같은 요소를 찾는다. 화면마다 'Viewport' 같은 이름이 겹쳐서 화면 이름으로 범위를 좁힌다.</summary>
        private static bool RectOfUnder(string screen, string[] names, out Rect rect)
        {
            rect = default;
            bool any = false;
            foreach (var root in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (root == null || root.name != screen || !root.gameObject.scene.IsValid() || !root.gameObject.activeInHierarchy) continue;
                foreach (var rt in root.GetComponentsInChildren<RectTransform>(false))
                    foreach (var n in names)
                        if (rt.name == n) Union(ref rect, ref any, ScreenRectOf(rt));
                if (any) return true;
            }
            return any;
        }

        /// <summary>
        /// 조건에 맞는 칸 중 하나를 고른다(특성 칸·스킬 카드처럼 같은 모양이 여러 개일 때).
        /// 뷰포트 안에 보이는 것 가운데 뷰포트 왼쪽 위에 가장 가까운 것 — 순서가 매 프레임 같아야 화살표가 흔들리지 않는다.
        /// </summary>
        private static bool RectOfPicked(System.Func<string, bool> match, string viewportName, out Rect rect)
        {
            rect = default;
            bool hasView = RectOfNamed(new[] { viewportName }, out var view);
            Vector2 corner = hasView ? new Vector2(view.xMin, view.yMax) : new Vector2(0f, Screen.height);
            float best = float.MaxValue;
            bool found = false;
            foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (rt == null || !rt.gameObject.scene.IsValid() || !rt.gameObject.activeInHierarchy || !match(rt.name)) continue;
                var r = ScreenRectOf(rt);
                if (r.width < 1f || r.height < 1f) continue;
                if (hasView && !view.Contains(r.center)) continue;
                float d = (r.center - corner).sqrMagnitude;
                if (d < best) { best = d; rect = r; found = true; }
            }
            return found;
        }

        /// <summary>열려 있는 창 안의 닫기 단추(CloseButton)를 찾는다.</summary>
        private static bool RectOfPopupClose(string popupName, out Rect rect)
        {
            rect = default;
            if (popupName == "@page")
            {
                // 화면 전체 페이지의 '뒤로' 단추 — 옛 로비는 CloseButton, 새 로비(특성·스킬·도감·업적)는 Back 이라는 이름이다
                foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
                    if (rt != null && (rt.name == "CloseButton" || rt.name == "Back") && rt.gameObject.scene.IsValid() && rt.gameObject.activeInHierarchy)
                    {
                        var r = ScreenRectOf(rt);
                        if (r.width > 1f) { rect = r; return true; }
                    }
                return false;
            }
            foreach (var popup in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (popup == null || popup.name != popupName || !popup.gameObject.scene.IsValid() || !popup.gameObject.activeInHierarchy) continue;
                foreach (var rt in popup.GetComponentsInChildren<RectTransform>(false))
                    if (rt.name == "CloseButton") { rect = ScreenRectOf(rt); return rect.width > 1f; }
            }
            return false;
        }

        private static bool RectOfSkillSlots(out Rect rect)
        {
            rect = default;
            bool any = false;
            foreach (var view in Object.FindObjectsByType<UICombatSkillSlotView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var rt = view.transform as RectTransform;
                if (rt != null) Union(ref rect, ref any, ScreenRectOf(rt));
                foreach (var g in view.GetComponentsInChildren<Graphic>(false))
                    if (g.enabled && g.color.a > 0.05f && !(g is TMP_Text)) Union(ref rect, ref any, ScreenRectOf(g.rectTransform));
            }
            return any;
        }

        private static bool RectOfHand(out Rect rect)
        {
            rect = default;
            var view = Object.FindFirstObjectByType<UIBattleCardHandView>(FindObjectsInactive.Exclude);
            var drop = view != null && FDropZone != null ? FDropZone.GetValue(view) as RectTransform : null;
            if (drop == null) return false;
            var r = ScreenRectOf(drop);
            float extra = 190f * Mathf.Max(0.8f, Screen.height / 1080f); // 카드가 손패 영역 위로 솟아 있다
            rect = Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, r.yMax + extra);
            return true;
        }

        private static bool RectOfGrid(out Rect rect)
        {
            rect = default;
            var cam = Camera.main;
            var first = GameObject.Find("Surface_0_0");
            if (cam == null || first == null) return false;
            Bounds b = default;
            bool any = false;
            foreach (var t in first.transform.parent.GetComponentsInChildren<Transform>())
            {
                if (!t.name.StartsWith("Surface_")) continue;
                if (!any) { b = new Bounds(t.position, Vector3.zero); any = true; } else b.Encapsulate(t.position);
            }
            if (!any) return false;
            Vector3 lo = cam.WorldToScreenPoint(b.min - new Vector3(0.5f, 0.5f, 0f));
            Vector3 hi = cam.WorldToScreenPoint(b.max + new Vector3(0.5f, 0.5f, 0f));
            rect = Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y), Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));
            return true;
        }

        private static bool RectOfKing(out Rect rect)
        {
            rect = default;
            var cam = Camera.main;
            var king = GameObject.Find("DemonKing");
            if (cam == null || king == null) return false;
            Vector3 p = cam.WorldToScreenPoint(king.transform.position);
            float half = 90f * Mathf.Max(0.8f, Screen.height / 1080f);
            rect = Rect.MinMaxRect(p.x - half, p.y - half * 0.6f, p.x + half, p.y + half * 1.4f);
            return true;
        }

        // ───────────── 만들기 도우미

        private Sprite Spr(string name)
        {
            if (_sprites.TryGetValue(name, out var cached) && cached != null) return cached;
            foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>())
                if (sp != null && sp.name == name) { _sprites[name] = sp; return sp; }
            return null;
        }

        private TMP_FontAsset FindFont()
        {
            if (UiFontOverride.Current != null) return UiFontOverride.Current;
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters("마왕크하하용사배치전투시작", out _, true, true)) return font;
            return null;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        private TMP_Text NewText(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) tmp.font = _font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.richText = true;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Sprite MakeTriangle()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float half = Mathf.Lerp(0f, n * 0.5f - 1f, y / (float)(n - 1)); // 아래를 가리키는 삼각형
                    float a = Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - n * 0.5f) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private void OnDestroy()
        {
            if (_pausedByUs && Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = _prevTimeScale;
        }
    }
}
