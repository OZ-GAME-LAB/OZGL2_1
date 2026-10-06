using System.Collections.Generic;
using System.Reflection;
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
        private Image _blocker, _arrow;
        private RawImage _portraitImage;
        private TMP_Text _body, _counter, _skipLabel;
        private GameObject _skipButton, _topicCatcher;
        private TMP_FontAsset _font;
        private bool _dialogAtTop;
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
            _body.text = step.Text;
            _body.maxVisibleCharacters = 0;
            FitBubble();
            _body.ForceMeshUpdate();
            _totalChars = _body.textInfo.characterCount;
            _typed = 0f;
            _counter.text = (_index + 1) + " / " + _seq.Steps.Length;
            _clickedAt = -1f;
            _releasedAt = -1f;
            _hadTarget = false;
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
                _body.maxVisibleCharacters = Mathf.FloorToInt(_typed);
            }
            else _body.maxVisibleCharacters = _totalChars;

            _showAlpha = Mathf.MoveTowards(_showAlpha, 1f, Time.unscaledDeltaTime * 4f);

            // 강조 대상: 화면을 칠하지 않고 화살표로 가리킨다
            var step = _seq.Steps[_index];
            Rect target = default;
            bool has = !string.IsNullOrEmpty(step.Target) && TryGetTarget(step.Target, out target);
            UpdateArrow(has, target);
            DriveStep(step, has, target);
            if (_seq == null) return;

            // 강조 대상이 화면 아래쪽이면 대화창을 위로 옮긴다
            if (has) _dialogAtTop = target.center.y < Screen.height * 0.5f;
            else _dialogAtTop = false;
            LayoutDialog();

            // 말하는 동안 마왕이 살짝 들썩이고, "다음" 표시는 위아래로 까딱인다
            bool talking = _typed < _totalChars;
            float pulse = talking ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 14f)) : 0f;
            _portraitRect.localScale = Vector3.one * (1f + pulse * 0.012f);
            _hint.gameObject.SetActive(!talking && current.Kind == StepKind.Talk);
            _hint.anchoredPosition = new Vector2(-34f * S, 30f * S + Mathf.Sin(Time.unscaledTime * 6f) * 5f * S);

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
            if (step.Kind == StepKind.Click)
            {
                if (hasTarget) { float pad = 8f * S; SetGates(true, Rect.MinMaxRect(target.xMin - pad, target.yMin - pad, target.xMax + pad, target.yMax + pad)); }
                else SetGates(false, default);

                if (step.Popup == "@page")
                {
                    // 하단 메뉴 단추: 누르면 업적·특성·스킬·도감 화면으로 넘어가며 로비 메뉴(단추)가 화면에서 사라진다.
                    // 단추가 사라지는 순간 바로 그 화면에서 설명을 시작한다(누른 위치로 판단하지 않는다).
                    if (hasTarget) _hadTarget = true;
                    else if (_hadTarget && _clickedAt < 0f) _clickedAt = Time.unscaledTime;
                    if (_clickedAt > 0f && Time.unscaledTime - _clickedAt > 0.15f) NextStep();

                    // 눌렀는데도 화면이 바뀌지 않는 단추라면 잠시 뒤 그냥 넘어간다(튜토리얼이 멈추지 않게)
                    var m = Mouse.current;
                    if (hasTarget && m != null && m.leftButton.wasReleasedThisFrame && _gateHole.Contains(m.position.ReadValue())) _releasedAt = Time.unscaledTime;
                    if (_releasedAt > 0f && Time.unscaledTime - _releasedAt > 1.2f && hasTarget) NextStep();
                    return;
                }

                // 창이 열리지 않는 단추(업적): 단추 구역에서 눌렀다 떼면 넘어간다
                var mouse = Mouse.current;
                if (hasTarget && mouse != null && mouse.leftButton.wasReleasedThisFrame && _gateHole.Contains(mouse.position.ReadValue()))
                    _clickedAt = Time.unscaledTime;
                if (_clickedAt > 0f && Time.unscaledTime - _clickedAt > 0.25f) NextStep();
                return;
            }

            // 창 닫기: 막지 않는다
            SetGates(false, default);
            bool back = step.Popup == "@page" ? LobbyMenuVisible() : !IsPopupOpen(step.Popup);
            if (back) NextStep();
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
            float padX = 44f * s, padTop = 62f * s, padBottom = 40f * s;
            _body.fontSize = 34f * s;
            _body.alignment = TextAlignmentOptions.TopJustified;
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.overflowMode = TextOverflowModes.Overflow;

            float maxW = Mathf.Min(Screen.width * 0.46f, 860f * s);
            float oneLine = _body.GetPreferredValues(_body.text, 100000f, 0f).x;
            float textW = Mathf.Clamp(oneLine, 420f * s, maxW - 2f * padX);
            float textH = _body.GetPreferredValues(_body.text, textW, 0f).y;
            _fitSize = new Vector2(textW + 2f * padX, Mathf.Max(textH + padTop + padBottom, 168f * s));

            var rt = _body.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padBottom);
            rt.offsetMax = new Vector2(-padX, -padTop);
            _counter.fontSize = 22f * s;
        }

        private void LayoutDialog()
        {
            float s = S;
            if (_fitScreenHeight != Screen.height) FitBubble(); // 해상도가 바뀌면 다시 맞춘다
            float portraitSize = 520f * s, margin = 16f * s;
            float yEdge = 24f * s;

            // 마왕은 화면 오른쪽, 말풍선은 마왕 왼쪽
            float anchorY = _dialogAtTop ? 1f : 0f;
            foreach (var rt in new[] { _portraitRect, _bubble })
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, anchorY);
                rt.pivot = new Vector2(1f, anchorY);
            }
            _portraitRect.sizeDelta = new Vector2(portraitSize, portraitSize);
            _portraitRect.anchoredPosition = new Vector2(-margin, -yEdge * 0.2f);
            _bubble.sizeDelta = _fitSize;
            _bubble.anchoredPosition = new Vector2(-(margin + portraitSize * 0.68f), _dialogAtTop ? -(yEdge + 8f * s) : yEdge + 8f * s);

            // 이름표는 말풍선 왼쪽 위 모서리에 걸쳐 놓는다
            _nameTag.anchorMin = _nameTag.anchorMax = new Vector2(0f, 1f);
            _nameTag.pivot = new Vector2(0f, 0.5f);
            _nameTag.sizeDelta = new Vector2(150f * s, 54f * s);
            _nameTag.anchoredPosition = new Vector2(30f * s, 0f);

            // 쪽 번호는 오른쪽 위, "다음" 표시는 오른쪽 아래(상자 안쪽)
            var cr = _counter.rectTransform;
            cr.sizeDelta = new Vector2(160f * s, 34f * s);
            cr.anchoredPosition = new Vector2(-34f * s, -16f * s);
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
            _canvas.sortingOrder = 150; // 로비 팝업·인게임 결과창(100)보다 위
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

            var bubbleImage = NewImage("Bubble", _dialog, null, new Color(0.07f, 0.05f, 0.09f, 0.96f));
            bubbleImage.raycastTarget = false;
            _bubble = bubbleImage.rectTransform;
            var frameSprite = Spr("Frame_CostPlate");
            if (frameSprite != null)
            {
                var frame = NewImage("Frame", _bubble, frameSprite, Color.white);
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = 2.4f;
                frame.raycastTarget = false;
                Stretch(frame.rectTransform);
            }

            _body = NewText("Body", _bubble, 34f, new Color(0.97f, 0.94f, 0.87f), TextAlignmentOptions.TopLeft);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.overflowMode = TextOverflowModes.Overflow;
            _body.lineSpacing = 6f;
            var bodyRt = _body.rectTransform;
            bodyRt.anchorMin = Vector2.zero; bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(40f, 38f); bodyRt.offsetMax = new Vector2(-40f, -52f);

            var tagImage = NewImage("NameTag", _bubble, Spr("Frame_CostPlate"), new Color(0.45f, 0.1f, 0.14f, 1f));
            if (Spr("Frame_CostPlate") != null) { tagImage.type = Image.Type.Sliced; tagImage.pixelsPerUnitMultiplier = 3f; }
            tagImage.raycastTarget = false;
            _nameTag = tagImage.rectTransform;
            var nameText = NewText("Name", _nameTag, 30f, new Color(1f, 0.9f, 0.55f), TextAlignmentOptions.Center);
            nameText.text = "마왕";
            Stretch(nameText.rectTransform);

            _counter = NewText("Counter", _bubble, 22f, new Color(0.7f, 0.66f, 0.6f), TextAlignmentOptions.Right);
            var cr = _counter.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f); cr.pivot = new Vector2(1f, 1f);
            cr.sizeDelta = new Vector2(160f, 34f); cr.anchoredPosition = new Vector2(-34f, -16f);

            var hintImage = NewImage("Hint", _bubble, MakeTriangle(), new Color(1f, 0.86f, 0.45f, 1f));
            hintImage.raycastTarget = false;
            _hint = hintImage.rectTransform;
            _hint.anchorMin = _hint.anchorMax = new Vector2(1f, 0f);
            _hint.pivot = new Vector2(0.5f, 0.5f);
            _hint.sizeDelta = new Vector2(34f, 28f);

            // 건너뛰기
            var skip = NewImage("Skip", go.transform, Spr("Frame_CostPlate"), new Color(0.2f, 0.12f, 0.16f, 0.9f));
            if (Spr("Frame_CostPlate") != null) { skip.type = Image.Type.Sliced; skip.pixelsPerUnitMultiplier = 3.4f; }
            var skipRt = skip.rectTransform;
            skipRt.anchorMin = skipRt.anchorMax = new Vector2(1f, 1f); skipRt.pivot = new Vector2(1f, 1f);
            skipRt.sizeDelta = new Vector2(170f, 56f); skipRt.anchoredPosition = new Vector2(-30f, -120f);
            var skipButton = skip.gameObject.AddComponent<Button>();
            skipButton.onClick.AddListener(End);
            _skipLabel = NewText("Label", skipRt, 24f, new Color(0.95f, 0.9f, 0.8f), TextAlignmentOptions.Center);
            _skipLabel.text = "건너뛰기";
            Stretch(_skipLabel.rectTransform);
            _skipButton = skip.gameObject;
            skipRt.localScale = Vector3.one * S;

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

            var diamond = Spr("Frame_DiamondRed");
            var button = NewImage("HelpButton", go.transform, diamond, Color.white);
            if (diamond == null) button.color = new Color(0.55f, 0.12f, 0.16f, 1f);
            button.preserveAspect = true;
            _helpButton = button.rectTransform;
            _helpButton.anchorMin = _helpButton.anchorMax = new Vector2(1f, 1f);
            _helpButton.pivot = new Vector2(1f, 1f);
            _helpButton.sizeDelta = new Vector2(78f, 78f);
            button.gameObject.AddComponent<Button>().onClick.AddListener(() => SetTopicsOpen(_topicPanel == null || !_topicPanel.gameObject.activeSelf));
            var q = NewText("Q", _helpButton, 40f, new Color(1f, 0.93f, 0.7f), TextAlignmentOptions.Center);
            q.text = "?";
            Stretch(q.rectTransform);

            BuildTopicPanel(go.transform);
            _helpCanvas.enabled = false;
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

        private void UpdateHelp()
        {
            if (_helpCanvas == null) return;
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            bool inGame = stage != null && (stage.State == eStageState.PREPARATION || stage.State == eStageState.COMBAT ||
                                            stage.State == eStageState.GENERAL_REWARD || stage.State == eStageState.AUGMENT);
            bool show = (inGame || IsLobby) && _seq == null;
            if (_helpCanvas.enabled != show) _helpCanvas.enabled = show;
            if (!show) { SetTopicsOpen(false); return; }

            float s = S;
            _helpButton.localScale = Vector3.one * s;
            _helpButton.anchoredPosition = new Vector2(-24f * s, -108f * s); // 상단 바·시너지 목록 사이
            _topicPanel.localScale = Vector3.one * s;
            _topicPanel.anchoredPosition = new Vector2(-24f * s, -108f * s - 84f * s);
        }

        // ───────────── 강조할 UI 찾기

        private bool TryGetTarget(string key, out Rect rect)
        {
            rect = default;
            if (key.StartsWith("popup.close:")) return RectOfPopupClose(key.Substring("popup.close:".Length), out rect);
            switch (key)
            {
                case "wave": return RectOfNamed(new[] { "WavePreviewPanel" }, out rect);
                case "start": return RectOfNamed(new[] { "Button_BattleStart" }, out rect);
                case "reroll": return RectOfNamed(new[] { "Button_Reroll" }, out rect);
                case "currency": return RectOfNamed(new[] { "Currency", "CombatCurrency" }, out rect);
                case "xp": return RectOfNamed(new[] { "ExperienceTrack", "ExperienceFillSoul" }, out rect);
                case "synergy": return RectOfNamed(new[] { "Synergy_0", "Synergy_1", "Synergy_2", "Synergy_3" }, out rect, true);
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

        /// <summary>열려 있는 창 안의 닫기 단추(CloseButton)를 찾는다.</summary>
        private static bool RectOfPopupClose(string popupName, out Rect rect)
        {
            rect = default;
            if (popupName == "@page")
            {
                // 화면 전체 페이지의 '뒤로' 단추 — 이름이 CloseButton인 것 중 지금 보이는 것
                foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
                    if (rt != null && rt.name == "CloseButton" && rt.gameObject.scene.IsValid() && rt.gameObject.activeInHierarchy)
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
