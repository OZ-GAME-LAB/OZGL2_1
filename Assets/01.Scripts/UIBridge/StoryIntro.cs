using System;
using System.Collections;
using System.Collections.Generic;
using OZGL2.Progression;
using OZGL2.Tutorial;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 처음 시작할 때 보여 주는 오프닝 스토리 컷신(배경 그림 6장 + 실제 게임 캐릭터 + 글). 타이틀 화면에서 「시작」을 누르면 로비로 가기 전에 한 번 나온다(StoryStore).
    /// - 마왕·마왕군·용사는 게임 프리팹(StoryCast)을 DemonKingPortrait 방식으로 살아 움직이게 그려 배경 위에 얹는다(그림에 따로 그려 넣지 않는다).
    /// - 배경은 Resources/Story/story_01~06 (도트 480x270)을 점 필터로 크게 늘려 보여 준다. 천천히 확대·이동하고 장면이 바뀔 때 부드럽게 겹쳐 넘어간다.
    /// - 위아래 검은 띠(영화 느낌), 가장자리 어둠, 떠오르는 불씨 알갱이로 분위기를 낸다.
    /// - 글은 한 글자씩 찍히고, 마왕이 말하는 장면은 튜토리얼과 같은 옹알이 말소리가 난다. 클릭·Space·Enter 로 넘기고(글이 찍히는 중이면 먼저 다 보여 줌), Esc 나 「건너뛰기」로 끝낸다.
    /// - 마지막 장면에서 제목 「용사 때문에 레벨업」이 나온 뒤 끝난다.
    /// 팀 UI 에는 손대지 않고 화면 위에 덮는 캔버스를 직접 만든다. 내용을 바꾸려면 아래 Slides 표만 고치면 된다.
    /// </summary>
    public sealed class StoryIntro : MonoBehaviour
    {
        private sealed class Slide
        {
            public string Image, Speaker, Text;
            public float ZoomFrom, ZoomTo;
            public Vector2 PanFrom, PanTo;       // 화면 폭·높이에 대한 비율(-0.5~0.5)
            public SfxId? Sting;
            public bool Title;
            public string Label;                 // 화면 위쪽에 크게 띄우는 글(예: LV 1)
            public bool Zzz;                     // 마왕 머리 위에 「Zzz」를 띄운다
            public bool Alert;                   // 글이 다 찍히면 경보 연출(붉은 번쩍임 + 화면 울림)
            public Actor[] Actors;
        }

        /// <summary>장면에 서는 인물 한 명. Pos 는 화면 가운데 기준 픽셀(1920x1080 기준, 위가 +), Size 는 그림 한 변.</summary>
        private sealed class Actor
        {
            public string Id;
            public Vector2 Pos;
            public float Size;
            public bool FaceRight;               // 프리팹은 기본이 왼쪽을 본다
            public PlayerState State = PlayerState.IDLE;
            public Vector2 Drift;                // 초당 이동(행군 등)
            public bool Roar;                    // 장면이 시작될 때 공격 모션을 한 번
            public float Delay;                  // 나타나는 시점(초)
            public Color Tint = Color.white;     // 멀리 있는 인물을 어둡게
            public float EnterX;                 // 이만큼 왼쪽 밖에서 걸어 들어온다
            public float EnterSeconds = 3.2f;
            public bool Torch;                   // 횃불을 든다(깜빡이는 불빛)
        }

        /// <summary>쳐들어오는 용사 한 명: 왼쪽 밖에서 걸어 들어와 대열에 선다. shade 가 낮을수록 멀리 있는 듯 어둡다.</summary>
        private static Actor March(string id, float x, float y, float size, float shade, float delay, bool torch)
            => new Actor
            {
                Id = id, Pos = new Vector2(x, y), Size = size, FaceRight = true, State = PlayerState.MOVE, Delay = delay,
                Drift = new Vector2(18f, 3f), EnterX = 1500f, Tint = new Color(shade, shade, Mathf.Min(1f, shade + 0.12f), 1f), Torch = torch,
            };

        private static Actor A(string id, float x, float y, float size, bool faceRight = false, PlayerState state = PlayerState.IDLE, float driftX = 0f, float driftY = 0f, bool roar = false, float delay = 0f)
            => new Actor { Id = id, Pos = new Vector2(x, y), Size = size, FaceRight = faceRight, State = state, Drift = new Vector2(driftX, driftY), Roar = roar, Delay = delay };

        private static readonly Slide[] Slides =
        {
            new Slide { Image = "Story/story_01", Text = "세상의 끝, 칠흑 같은 어둠 속에\n오래된 마왕성이 서 있었다.", ZoomFrom = 1.0f, ZoomTo = 1.1f, PanFrom = new Vector2(0f, -0.02f), PanTo = new Vector2(0f, 0.02f) },
            new Slide { Image = "Story/story_02", Text = "그곳의 주인, 마왕은\n수백 년째 낮잠을 자고 있었다.", ZoomFrom = 1.14f, ZoomTo = 1.0f, PanFrom = new Vector2(0f, 0.03f), PanTo = Vector2.zero, Zzz = true,
                Actors = new[] { A("king", 0f, 40f, 540f) } },
            new Slide { Image = "Story/story_03", Text = "그런데 어느 날, 성문 밖에 횃불이 하나둘 켜지기 시작했다.\n……용사들이었다!", ZoomFrom = 1.0f, ZoomTo = 1.12f, PanFrom = new Vector2(0.03f, 0f), PanTo = new Vector2(-0.03f, 0f), Alert = true,
                Actors = new[]
                {
                    // 뒤 줄(작고 어둡다) → 가운데 줄 → 앞 줄(크고 또렷하다) 순으로 쌓아 깊이를 만든다
                    March("h_mag3", -760f, 96f, 220f, 0.50f, 0.15f, false),
                    March("h_rog3", -480f, 104f, 220f, 0.50f, 0.00f, false),
                    March("h_arc3", -200f, 112f, 220f, 0.50f, 0.25f, false),
                    March("h_hel2", 80f, 120f, 220f, 0.50f, 0.10f, false),
                    March("h_shd2", -860f, 44f, 280f, 0.72f, 0.55f, true),
                    March("h_war2", -560f, 52f, 280f, 0.72f, 0.35f, false),
                    March("h_arc2", -260f, 60f, 280f, 0.72f, 0.65f, true),
                    March("h_mag2", 40f, 68f, 280f, 0.72f, 0.45f, false),
                    March("h_shd", -700f, -14f, 350f, 1.00f, 1.00f, true),
                    March("h_war", -380f, -8f, 350f, 1.00f, 0.80f, true),
                    March("h_rog", -60f, -2f, 340f, 1.00f, 1.15f, false),
                    March("h_arc", 260f, 4f, 340f, 1.00f, 0.90f, true),
                    March("h_war3", 560f, 10f, 360f, 1.00f, 0.70f, true),
                } },
            new Slide { Image = "Story/story_04", Speaker = "마왕", Text = "……시끄럽군. 누가 감히 짐의 낮잠을 방해하느냐!", ZoomFrom = 1.0f, ZoomTo = 1.1f, PanFrom = Vector2.zero, PanTo = Vector2.zero, Sting = SfxId.BossAppear,
                Actors = new[] { A("king", 0f, 90f, 900f, roar: true) } },
            new Slide { Image = "Story/story_05", Text = "눈을 뜬 마왕은 곧 깨달았다.\n자신의 레벨이…… 1이라는 것을.", ZoomFrom = 1.06f, ZoomTo = 1.0f, PanFrom = Vector2.zero, PanTo = Vector2.zero, Label = "LV. 1",
                Actors = new[] { A("king", 0f, 10f, 520f) } },
            new Slide { Image = "Story/story_06", Speaker = "마왕", Text = "좋다! 쳐들어오는 용사들이 짐을 더 강하게 만들어 줄 것이다.\n마왕군이여, 성을 지켜라!", ZoomFrom = 1.0f, ZoomTo = 1.08f, PanFrom = new Vector2(0f, -0.02f), PanTo = new Vector2(0f, 0.01f), Sting = SfxId.LevelUp, Title = true,
                Actors = new[]
                {
                    A("d_arc", -800f, -10f, 360f, true, delay: 0.45f),
                    A("d_war", -560f, 0f, 380f, true, delay: 0.3f),
                    A("d_shd", -320f, -5f, 380f, true, delay: 0.15f),
                    A("d_hel", 800f, -10f, 360f, false, delay: 0.45f),
                    A("d_rog", 560f, 0f, 380f, false, delay: 0.3f),
                    A("d_mag", 320f, -5f, 380f, false, delay: 0.15f),
                    A("king", 0f, 70f, 560f, roar: true),
                } },
        };

        private const float CharsPerSecond = 38f;
        private const float SlideSeconds = 40f;    // 한 장면의 확대·이동이 이어지는 시간(넘기기 전까지 천천히 계속)
        private const float FadeSeconds = 0.9f;

        private static readonly Color Gold = new Color(0.95f, 0.76f, 0.34f, 1f);
        private static readonly Color Cream = new Color(1f, 0.95f, 0.84f, 1f);

        private Action _onDone;
        private Canvas _canvas;
        private RectTransform _root, _skipRect, _panel, _namePlate, _arrow, _titleCard;
        private RawImage _imgA, _imgB, _fade;
        private RectTransform _actorsRt, _labelRt;
        private CanvasGroup _actorGroup, _zzzGroup;
        private TMP_Text _label, _zzz;
        private StoryCast _cast;
        private readonly List<(Actor def, DemonKingPortrait portrait, RawImage image)> _actors = new List<(Actor, DemonKingPortrait, RawImage)>();
        private static int _stageCounter;
        private Image _flash;
        private float _alertAt = -10f;
        private bool _alertDone;
        private readonly List<(RectTransform glow, RectTransform core, Image glowImg, Image coreImg, RectTransform follow, Actor def, float phase)> _torches = new List<(RectTransform, RectTransform, Image, Image, RectTransform, Actor, float)>();
        private readonly List<(RectTransform rt, Image img, float born, float drift)> _puffs = new List<(RectTransform, Image, float, float)>();
        private readonly Dictionary<Actor, float> _nextPuff = new Dictionary<Actor, float>();
        private TMP_Text _text, _name, _title, _skipLabel;
        private CanvasGroup _panelGroup, _titleGroup;
        private TMP_FontAsset _font;
        private Sprite _dot, _soft, _vignetteSprite;
        private readonly List<RectTransform> _dots = new List<RectTransform>();
        private readonly List<Image> _dotImages = new List<Image>();
        private readonly List<(RectTransform rt, Image img, float speed, float phase, float sway)> _embers = new List<(RectTransform, Image, float, float, float)>();
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();

        private int _index = -1;
        private float _slideStart;
        private float _typeStart;
        private int _shown;
        private bool _typing, _titleShown, _finishing;
        private float _unlockAt;
        private int _lastVoice;

        /// <summary>스토리를 시작한다. 끝나면(건너뛰기 포함) onDone 이 한 번 불린다. 화면이 완전히 어두워진 상태에서 끝나므로 바로 씬을 넘겨도 된다.</summary>
        public static StoryIntro Play(Action onDone)
        {
            var go = new GameObject("StoryIntro");
            DontDestroyOnLoad(go);
            var story = go.AddComponent<StoryIntro>();
            story._onDone = onDone;
            return story;
        }

        // ───────────── 흐름

        private void Start()
        {
            _font = UiFontOverride.Current != null ? UiFontOverride.Current : FindFont();
            Build();
            _unlockAt = Time.unscaledTime + 0.6f;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            StartCoroutine(Fade(1f, 0f, 1.3f));   // 검은 화면이 걷히는 동안 첫 그림이 이미 그 아래에 깔려 있다
            yield return ShowSlide(0, false);
        }

        private IEnumerator ShowSlide(int index, bool crossfade)
        {
            _index = index;
            var slide = Slides[index];
            _titleShown = false;
            _titleGroup.alpha = 0f;
            _slideStart = Time.unscaledTime;

            // 새 그림을 앞 장(B)에 놓고 서서히 드러낸 뒤 A 로 넘긴다
            _imgB.texture = Load(slide.Image);
            ApplyKenBurns(_imgB.rectTransform, slide, 0f);
            for (int i = 0; i < _dotImages.Count; i++)
                _dotImages[i].color = i == index ? Gold : new Color(Gold.r, Gold.g, Gold.b, 0.28f);

            if (crossfade)
            {
                StartCoroutine(FadeGroup(_actorGroup, 0f, 0.18f));
                yield return CrossfadeText(0f, 0.18f);
            }
            ClearActors();
            _alertDone = false;
            _zzzGroup.alpha = 0f;
            _label.text = slide.Label ?? string.Empty;
            _labelRt.gameObject.SetActive(!string.IsNullOrEmpty(slide.Label));
            BuildActors(slide);
            _actorGroup.alpha = crossfade ? 0f : 1f;
            SetText(slide);
            _crossfading = crossfade;
            float t = 0f;
            if (crossfade)
            {
                for (; t < FadeSeconds; t += Time.unscaledDeltaTime)
                {
                    _imgB.color = new Color(1f, 1f, 1f, t / FadeSeconds);
                    ApplyKenBurns(_imgB.rectTransform, slide, Time.unscaledTime - _slideStart);
                    ApplyKenBurns(_actorsRt, slide, Time.unscaledTime - _slideStart);
                    _actorGroup.alpha = Mathf.SmoothStep(0f, 1f, t / FadeSeconds);
                    yield return null;
                }
            }
            _imgB.color = Color.white;
            _imgA.texture = _imgB.texture;
            ApplyKenBurns(_imgA.rectTransform, slide, Time.unscaledTime - _slideStart);
            _imgB.color = new Color(1f, 1f, 1f, 0f);
            _actorGroup.alpha = 1f;
            _crossfading = false;
            if (slide.Sting.HasValue) Sfx.Play(slide.Sting.Value);
            if (!crossfade) yield return new WaitForSecondsRealtime(0.9f);   // 첫 장면은 화면이 밝아진 뒤에 글이 나온다
            yield return CrossfadeText(1f, 0.25f);
            StartTyping();
        }

        private void Update()
        {
            if (_finishing || _index < 0) return;
            float elapsed = Time.unscaledTime - _slideStart;
            var slide = Slides[_index];
            if (!_crossfading) ApplyKenBurns(_imgA.rectTransform, slide, elapsed);   // 겹쳐 넘어가는 동안 옛 그림은 멈춰 둔다
            if (!_crossfading) ApplyKenBurns(_actorsRt, slide, elapsed);
            UpdateActors(elapsed);
            ApplyAlert();
            AnimateEmbers(Time.unscaledDeltaTime);
            AnimateTyping();
            if (_arrow != null)
            {
                bool done = !_typing && _panelGroup.alpha > 0.9f;
                _arrow.gameObject.SetActive(done);
                if (done) _arrow.anchoredPosition = new Vector2(_arrow.anchoredPosition.x, 22f + Mathf.Sin(Time.unscaledTime * 5f) * 6f);
            }
            HandleInput();
        }

        private void HandleInput()
        {
            if (Time.unscaledTime < _unlockAt) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Finish(); return; }

            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (click && _skipRect != null && RectTransformUtility.RectangleContainsScreenPoint(_skipRect, mouse.position.ReadValue(), null)) { Finish(); return; }
            bool key = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame);
            if (!click && !key) return;
            Advance();
        }

        private void Advance()
        {
            if (_busy) return;
            if (_typing) { CompleteTyping(); return; }
            var slide = Slides[_index];
            if (slide.Title && !_titleShown) { StartCoroutine(ShowTitle()); return; }
            if (_index + 1 < Slides.Length) { StartCoroutine(Next()); return; }
            Finish();
        }

        private bool _busy;
        private bool _crossfading;

        private IEnumerator Next()
        {
            _busy = true;
            yield return ShowSlide(_index + 1, true);
            _busy = false;
        }

        private IEnumerator ShowTitle()
        {
            _busy = true;
            _titleShown = true;
            StartCoroutine(FadeGroup(_actorGroup, 0.22f, 0.6f));
            yield return CrossfadeText(0f, 0.25f);
            for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.1f);
                _titleGroup.alpha = k;
                _titleCard.localScale = Vector3.one * Mathf.Lerp(1.18f, 1f, k);
                yield return null;
            }
            _titleGroup.alpha = 1f;
            _titleCard.localScale = Vector3.one;
            _busy = false;
            // 제목이 뜬 뒤 한 번 더 누르면 끝난다
        }

        private void Finish()
        {
            if (_finishing) return;
            _finishing = true;
            StopAllCoroutines();
            StartCoroutine(FinishRoutine());
        }

        private IEnumerator FinishRoutine()
        {
            yield return Fade(_fade.color.a, 1f, 0.8f);
            ClearActors();
            StoryStore.MarkSeen();
            var done = _onDone;
            _onDone = null;
            done?.Invoke();
            Destroy(gameObject);
        }

        // ───────────── 글 찍기

        private void SetText(Slide slide)
        {
            _text.text = slide.Text;
            _text.maxVisibleCharacters = 0;
            _name.text = slide.Speaker ?? string.Empty;
            _namePlate.gameObject.SetActive(!string.IsNullOrEmpty(slide.Speaker));
            _text.color = string.IsNullOrEmpty(slide.Speaker) ? Cream : new Color(1f, 0.9f, 0.78f, 1f);
        }

        private void StartTyping()
        {
            _typing = true;
            _typeStart = Time.unscaledTime;
            _shown = 0;
            _lastVoice = 0;
            _text.maxVisibleCharacters = 0;
        }

        private void AnimateTyping()
        {
            if (!_typing) return;
            _text.ForceMeshUpdate();
            int total = _text.textInfo.characterCount;
            int want = Mathf.Min(total, Mathf.FloorToInt((Time.unscaledTime - _typeStart) * CharsPerSecond));
            if (want != _shown)
            {
                _shown = want;
                _text.maxVisibleCharacters = want;
                // 마왕이 말하는 장면: 두 글자마다 옹알이 소리
                if (!string.IsNullOrEmpty(Slides[_index].Speaker) && want - _lastVoice >= 2)
                {
                    _lastVoice = want;
                    Sfx.Play(SfxId.TutorialVoice, UnityEngine.Random.Range(0.92f, 1.14f));
                }
            }
            if (want >= total) { _typing = false; OnTypingDone(); }
        }

        private void CompleteTyping()
        {
            _text.maxVisibleCharacters = int.MaxValue;
            _typing = false;
            OnTypingDone();
        }

        private void OnTypingDone()
        {
            if (_alertDone || _index < 0 || !Slides[_index].Alert) return;
            _alertDone = true;
            _alertAt = Time.unscaledTime;
            Sfx.Play(SfxId.BattleStart);
        }

        private IEnumerator CrossfadeText(float to, float seconds)
        {
            float from = _panelGroup.alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _panelGroup.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            _panelGroup.alpha = to;
        }

        // ───────────── 인물(실제 게임 캐릭터)

        private void BuildActors(Slide slide)
        {
            if (slide.Actors == null || _cast == null) return;
            foreach (var def in slide.Actors)
            {
                var prefab = _cast.Get(def.Id);
                if (prefab == null) continue;
                var go = new GameObject("StoryActor_" + def.Id);
                go.transform.SetParent(transform, false);
                var portrait = go.AddComponent<DemonKingPortrait>();
                portrait.Configure(new Vector3(7000f + (_stageCounter++ % 40) * 40f, 7000f, 0f), def.Size > 330f ? 512 : 320);
                portrait.Build(prefab);
                if (!portrait.IsReady) { Destroy(go); continue; }
                portrait.SetShown(true);
                portrait.Play(def.State);
                if (def.Roar) portrait.Gesture();

                var image = NewRaw("Actor_" + def.Id, _actorsRt, portrait.Texture, Color.white);
                image.raycastTarget = false;
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(def.Size, def.Size);
                rt.anchoredPosition = def.Pos;
                image.uvRect = def.FaceRight ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);   // 오른쪽을 보게 좌우 뒤집기
                image.color = new Color(1f, 1f, 1f, def.Delay > 0f ? 0f : 1f);
                _actors.Add((def, portrait, image));

                if (def.Torch)
                {
                    var glow = NewImage("TorchGlow", _actorsRt, _soft, new Color(1f, 0.55f, 0.16f, 0f));
                    glow.raycastTarget = false;
                    glow.rectTransform.sizeDelta = new Vector2(def.Size * 0.95f, def.Size * 0.95f);
                    var core = NewImage("TorchCore", _actorsRt, _soft, new Color(1f, 0.9f, 0.55f, 0f));
                    core.raycastTarget = false;
                    core.rectTransform.sizeDelta = new Vector2(def.Size * 0.22f, def.Size * 0.22f);
                    _torches.Add((glow.rectTransform, core.rectTransform, glow, core, rt, def, UnityEngine.Random.Range(0f, 6.28f)));
                }
            }
        }

        private void ClearActors()
        {
            foreach (var a in _actors)
            {
                if (a.image != null) Destroy(a.image.gameObject);
                if (a.portrait != null) Destroy(a.portrait.gameObject);
            }
            _actors.Clear();
            foreach (var tch in _torches)
            {
                if (tch.glow != null) Destroy(tch.glow.gameObject);
                if (tch.core != null) Destroy(tch.core.gameObject);
            }
            _torches.Clear();
            foreach (var puff in _puffs) if (puff.rt != null) Destroy(puff.rt.gameObject);
            _puffs.Clear();
            _nextPuff.Clear();
        }

        private void UpdateActors(float elapsed)
        {
            foreach (var a in _actors)
            {
                if (a.image == null) continue;
                var rt = a.image.rectTransform;
                float since = Mathf.Max(0f, elapsed - a.def.Delay);
                var pos = a.def.Pos + a.def.Drift * Mathf.Min(since, SlideSeconds);
                bool entering = false;
                if (a.def.EnterX > 0f)
                {
                    float k = Mathf.Clamp01(since / a.def.EnterSeconds);
                    entering = k < 1f;
                    pos.x -= a.def.EnterX * Mathf.Pow(1f - k, 3f);     // 처음엔 빠르게 들어와 대열 앞에서 속도가 줄어든다
                }
                float phase = a.def.Pos.x * 0.013f;
                // 숨 쉬듯(대기) · 발을 디딜 때마다 통통(행군)
                if (a.def.State == PlayerState.IDLE) pos.y += Mathf.Sin(Time.unscaledTime * 2f + phase) * 3f;
                else if (a.def.State == PlayerState.MOVE) pos.y += Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6.5f + phase)) * 7f * (a.def.Size / 340f);
                rt.anchoredPosition = pos;
                float alpha = a.def.Delay > 0f && a.def.EnterX <= 0f ? Mathf.Clamp01((elapsed - a.def.Delay) / 0.4f) : 1f;
                var tint = a.def.Tint;
                a.image.color = new Color(tint.r, tint.g, tint.b, alpha);

                // 걸을 때 발밑에서 먼지가 인다
                if (a.def.State == PlayerState.MOVE && elapsed > a.def.Delay)
                {
                    _nextPuff.TryGetValue(a.def, out var due);
                    if (Time.unscaledTime >= due)
                    {
                        _nextPuff[a.def] = Time.unscaledTime + (entering ? 0.16f : 0.34f) + UnityEngine.Random.Range(0f, 0.12f);
                        SpawnPuff(pos + new Vector2(-a.def.Size * 0.12f, -a.def.Size * 0.4f), a.def.Size, a.def.Tint.r);
                    }
                }
            }
            UpdateTorches();
            UpdatePuffs();
            // 「Zzz」: 마왕 머리 위에서 천천히 떠올랐다 사라진다
            var slide = Slides[_index];
            if (slide.Zzz && _zzz != null)
            {
                float k = Mathf.Repeat(Time.unscaledTime * 0.45f, 1f);
                _zzzGroup.alpha = Mathf.Sin(k * Mathf.PI) * 0.95f;
                _zzz.rectTransform.anchoredPosition = new Vector2(150f + k * 40f, 290f + k * 90f);
                _zzz.fontSize = 60f + k * 40f;
            }
            else if (_zzzGroup != null) _zzzGroup.alpha = 0f;
            // 레벨 표시: 나타날 때 크게 튀어나왔다가 자리 잡는다
            if (_labelRt != null && _labelRt.gameObject.activeSelf)
            {
                float k = Mathf.Clamp01((elapsed - 0.9f) / 0.45f);
                float pop = k < 1f ? 1f + (1f - k) * 0.9f : 1f + Mathf.Sin(Time.unscaledTime * 2.4f) * 0.015f;
                _labelRt.localScale = Vector3.one * pop;
                _label.alpha = k;
            }
        }

        private void SpawnPuff(Vector2 at, float actorSize, float shade)
        {
            var img = NewImage("Dust", _actorsRt, _soft, new Color(0.62f * shade + 0.1f, 0.5f * shade + 0.08f, 0.44f * shade + 0.08f, 0f));
            img.raycastTarget = false;
            img.rectTransform.SetSiblingIndex(0);
            img.rectTransform.anchoredPosition = at;
            img.rectTransform.sizeDelta = Vector2.one * actorSize * 0.2f;
            _puffs.Add((img.rectTransform, img, Time.unscaledTime, UnityEngine.Random.Range(-30f, 30f)));
        }

        private void UpdatePuffs()
        {
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                var (rt, img, born, drift) = _puffs[i];
                float k = (Time.unscaledTime - born) / 1.0f;
                if (rt == null || k >= 1f) { if (rt != null) Destroy(rt.gameObject); _puffs.RemoveAt(i); continue; }
                var c = img.color; c.a = Mathf.Sin(k * Mathf.PI) * 0.34f; img.color = c;
                rt.sizeDelta *= 1f + Time.unscaledDeltaTime * 0.9f;
                rt.anchoredPosition += new Vector2(drift * Time.unscaledDeltaTime, 24f * Time.unscaledDeltaTime);
            }
        }

        /// <summary>횃불 불빛: 든 사람을 따라다니며 불규칙하게 깜빡인다.</summary>
        private void UpdateTorches()
        {
            foreach (var tch in _torches)
            {
                if (tch.follow == null || tch.glow == null) continue;
                float s = tch.def.Size / 340f;
                var at = tch.follow.anchoredPosition + new Vector2(78f * s, 108f * s);   // 몸 앞 오른쪽 위(손 높이)
                float flick = 0.62f + 0.38f * (Mathf.PerlinNoise(Time.unscaledTime * 7f, tch.phase) * 0.7f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 11f + tch.phase)) * 0.3f);
                float fade = tch.follow.GetComponent<RawImage>().color.a;
                tch.glow.anchoredPosition = at; tch.core.anchoredPosition = at;
                tch.glowImg.color = new Color(1f, 0.5f, 0.14f, 0.5f * flick * fade * Mathf.Clamp01(tch.def.Tint.r + 0.2f));
                tch.coreImg.color = new Color(1f, 0.88f, 0.5f, 0.85f * flick * fade);
                tch.glow.localScale = Vector3.one * (0.9f + 0.2f * flick);
            }
        }

        /// <summary>경보: 붉은 번쩍임 + 화면이 쿵 하고 확 커졌다 돌아온다.</summary>
        private void ApplyAlert()
        {
            float k = (Time.unscaledTime - _alertAt) / 0.9f;
            if (k < 0f || k > 1f) { if (_flash != null && _flash.color.a > 0f) _flash.color = new Color(1f, 0.25f, 0.2f, 0f); return; }
            float decay = Mathf.Pow(1f - k, 2f);
            float punch = 1f + 0.05f * decay * Mathf.Cos(k * 18f);
            _imgA.rectTransform.localScale *= punch;
            _actorsRt.localScale *= punch;
            _flash.color = new Color(1f, 0.25f, 0.2f, 0.5f * decay);
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float to, float seconds)
        {
            float from = group.alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            group.alpha = to;
        }

        // ───────────── 연출

        private IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            _fade.color = new Color(0f, 0f, 0f, to);
        }

        private static void ApplyKenBurns(RectTransform rt, Slide slide, float elapsed)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SlideSeconds));
            float zoom = Mathf.Lerp(slide.ZoomFrom, slide.ZoomTo, k);
            Vector2 pan = Vector2.Lerp(slide.PanFrom, slide.PanTo, k);
            rt.localScale = new Vector3(zoom, zoom, 1f);
            rt.anchoredPosition = new Vector2(pan.x * 1920f, pan.y * 1080f);
        }

        private void AnimateEmbers(float dt)
        {
            for (int i = 0; i < _embers.Count; i++)
            {
                var (rt, img, speed, phase, sway) = _embers[i];
                var p = rt.anchoredPosition;
                p.y += speed * dt;
                p.x += Mathf.Sin(Time.unscaledTime * 1.3f + phase) * sway * dt;
                if (p.y > 1100f) { p.y = -20f; p.x = UnityEngine.Random.Range(0f, 1920f); }
                rt.anchoredPosition = p;
                float flick = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f + phase * 2f));
                img.color = new Color(1f, 0.62f + 0.2f * flick, 0.22f, flick * 0.85f);
            }
        }

        // ───────────── 화면 만들기

        private void Build()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>(); // 아래 타이틀 화면의 단추가 클릭을 받지 않도록 이 캔버스가 클릭을 막는다
            _root = (RectTransform)go.transform;

            _dot = MakeDot();
            _soft = MakeSoft();
            _vignetteSprite = MakeVignette();

            var black = NewRaw("Black", _root, null, Color.black);
            Stretch(black.rectTransform);

            // 그림 두 장(앞뒤로 겹쳐 넘기기). 마스크로 확대된 부분이 화면 밖으로 새지 않게 한다
            var picture = NewRect("Picture", _root);
            Stretch(picture);
            picture.gameObject.AddComponent<RectMask2D>();
            _imgA = NewRaw("ImageA", picture, null, Color.white);
            _imgB = NewRaw("ImageB", picture, null, new Color(1f, 1f, 1f, 0f));
            Stretch(_imgA.rectTransform);
            Stretch(_imgB.rectTransform);
            _imgA.raycastTarget = _imgB.raycastTarget = false;

            // 인물 층(그림과 같이 확대·이동한다)
            _cast = Resources.Load<StoryCast>("Story/StoryCast");
            if (_cast == null) Debug.LogWarning("[스토리] Resources/Story/StoryCast 를 찾지 못해 인물 없이 보여 줍니다.");
            _actorsRt = NewRect("Actors", picture);
            Stretch(_actorsRt);
            _actorGroup = _actorsRt.gameObject.AddComponent<CanvasGroup>();
            _actorGroup.blocksRaycasts = false;
            _actorGroup.interactable = false;

            // 마왕이 자는 장면의 「Zzz」
            _zzz = NewText("Zzz", _actorsRt, 70f, new Color(0.8f, 0.88f, 1f), TextAlignmentOptions.Center);
            _zzz.text = "Zzz…";
            _zzz.fontStyle = FontStyles.Bold | FontStyles.Italic;
            _zzz.outlineWidth = 0.22f; _zzz.outlineColor = new Color32(10, 10, 40, 255);
            _zzz.rectTransform.anchorMin = _zzz.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _zzz.rectTransform.sizeDelta = new Vector2(400f, 140f);
            _zzzGroup = _zzz.gameObject.AddComponent<CanvasGroup>();
            _zzzGroup.alpha = 0f;

            // 불씨
            var embers = NewRect("Embers", _root);
            Stretch(embers);
            for (int i = 0; i < 34; i++)
            {
                var img = NewImage("Ember", embers, _dot, new Color(1f, 0.7f, 0.3f, 0.8f));
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
                img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                float s = UnityEngine.Random.Range(3f, 7f);
                img.rectTransform.sizeDelta = new Vector2(s, s);
                img.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(0f, 1920f), UnityEngine.Random.Range(-20f, 1100f));
                img.raycastTarget = false;
                _embers.Add((img.rectTransform, img, UnityEngine.Random.Range(18f, 60f), UnityEngine.Random.Range(0f, 6.28f), UnityEngine.Random.Range(6f, 26f)));
            }

            _flash = NewImage("Flash", _root, null, new Color(1f, 0.25f, 0.2f, 0f));
            Stretch(_flash.rectTransform);
            _flash.raycastTarget = false;

            // 가장자리 어둠 + 위아래 검은 띠(영화 느낌)
            var vignette = NewImage("Vignette", _root, _vignetteSprite, new Color(0f, 0f, 0f, 0.72f));
            Stretch(vignette.rectTransform);
            vignette.raycastTarget = false;
            var bars = NewImage("BarTop", _root, null, new Color(0f, 0f, 0f, 1f));
            bars.rectTransform.anchorMin = new Vector2(0f, 1f); bars.rectTransform.anchorMax = new Vector2(1f, 1f);
            bars.rectTransform.pivot = new Vector2(0.5f, 1f); bars.rectTransform.sizeDelta = new Vector2(0f, 84f);
            bars.raycastTarget = false;
            var barsBottom = NewImage("BarBottom", _root, null, new Color(0f, 0f, 0f, 1f));
            barsBottom.rectTransform.anchorMin = new Vector2(0f, 0f); barsBottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            barsBottom.rectTransform.pivot = new Vector2(0.5f, 0f); barsBottom.rectTransform.sizeDelta = new Vector2(0f, 84f);
            barsBottom.raycastTarget = false;

            // 레벨 장면의 「LV. 1」
            _label = NewText("Label", _root, 190f, new Color(1f, 0.86f, 0.4f), TextAlignmentOptions.Center);
            _label.fontStyle = FontStyles.Bold;
            _label.outlineWidth = 0.3f; _label.outlineColor = new Color32(70, 10, 20, 255);
            _labelRt = _label.rectTransform;
            _labelRt.anchorMin = _labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _labelRt.sizeDelta = new Vector2(900f, 260f);
            _labelRt.anchoredPosition = new Vector2(0f, 330f);
            _labelRt.gameObject.SetActive(false);

            BuildPanel();
            BuildTitleCard();
            BuildHud();

            _fade = NewRaw("Fade", _root, null, Color.black);
            Stretch(_fade.rectTransform);
            _fade.raycastTarget = false;
        }

        private void BuildPanel()
        {
            _panel = NewRect("TextPanel", _root);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.sizeDelta = new Vector2(1560f, 214f);
            _panel.anchoredPosition = new Vector2(0f, 118f);
            _panelGroup = _panel.gameObject.AddComponent<CanvasGroup>();
            _panelGroup.alpha = 0f;

            var style = UIHeraldrySupplement.Style;
            bool hasFrame = style != null && style.FrameSprite != null;
            var bg = NewImage("Bg", _panel, null, hasFrame ? style.BackgroundColor : new Color(0.04f, 0.02f, 0.07f, 0.86f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = false;
            if (hasFrame) UIHeraldrySupplement.AddFrame(_panel);
            else
            {
                var border = bg.gameObject.AddComponent<Outline>();
                border.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.95f);
                border.effectDistance = new Vector2(3f, -3f);
                var inner = NewImage("Inner", _panel, null, new Color(1f, 1f, 1f, 0f));
                inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
                inner.rectTransform.offsetMin = new Vector2(10f, 10f); inner.rectTransform.offsetMax = new Vector2(-10f, -10f);
                var innerLine = inner.gameObject.AddComponent<Outline>();
                innerLine.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
                innerLine.effectDistance = new Vector2(1.5f, -1.5f);
                inner.raycastTarget = false;
            }

            _text = NewText("Text", _panel, 46f, Cream, TextAlignmentOptions.TopLeft);
            UIHeraldrySupplement.ApplyBody(_text);
            _text.rectTransform.anchorMin = Vector2.zero; _text.rectTransform.anchorMax = Vector2.one;
            _text.rectTransform.offsetMin = new Vector2(52f, 24f); _text.rectTransform.offsetMax = new Vector2(-52f, hasFrame ? -44f : -26f);
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.lineSpacing = 12f;

            // 말하는 사람 이름표(마왕일 때만)
            _namePlate = NewRect("NamePlate", _panel);
            _namePlate.anchorMin = _namePlate.anchorMax = new Vector2(0f, 1f);
            _namePlate.pivot = new Vector2(0f, 0.5f);
            bool hasNameplate = style != null && style.NameplateSprite != null;
            _namePlate.sizeDelta = hasNameplate ? new Vector2(240f, 64f) : new Vector2(190f, 58f);
            _namePlate.anchoredPosition = new Vector2(hasNameplate ? 60f : 34f, 0f);
            var plate = NewImage("Bg", _namePlate, hasNameplate ? style.NameplateSprite : null, hasNameplate ? Color.white : new Color(0.45f, 0.08f, 0.16f, 1f));
            Stretch(plate.rectTransform);
            plate.raycastTarget = false;
            if (hasNameplate)
            {
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = style.FrameMultiplier;
                if (style.CrestSprite != null)
                {
                    var crest = UIHeraldrySupplement.CreateImage(_namePlate, "SpeakerCrest", style.CrestSprite);
                    crest.preserveAspect = true;
                    var rt = crest.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(88f, 70f);
                    rt.anchoredPosition = new Vector2(-18f, 0f);
                }
            }
            else
            {
                var plateBorder = plate.gameObject.AddComponent<Outline>();
                plateBorder.effectColor = Gold; plateBorder.effectDistance = new Vector2(3f, -3f);
            }
            _name = NewText("Name", _namePlate, 40f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center);
            UIHeraldrySupplement.ApplyTitle(_name);
            Stretch(_name.rectTransform);
            if (hasNameplate) _name.rectTransform.offsetMin = new Vector2(28f, 0f);
            _name.fontStyle = FontStyles.Bold;

            // 다음으로 넘기라는 화살표
            var arrow = NewText("Arrow", _panel, 40f, Gold, TextAlignmentOptions.Center);
            arrow.text = "▼";
            _arrow = arrow.rectTransform;
            _arrow.anchorMin = _arrow.anchorMax = new Vector2(1f, 0f);
            _arrow.pivot = new Vector2(0.5f, 0f);
            _arrow.sizeDelta = new Vector2(60f, 50f);
            _arrow.anchoredPosition = new Vector2(-50f, 22f);
            _arrow.gameObject.SetActive(false);
        }

        private void BuildTitleCard()
        {
            _titleCard = NewRect("TitleCard", _root);
            _titleCard.anchorMin = _titleCard.anchorMax = new Vector2(0.5f, 0.5f);
            _titleCard.pivot = new Vector2(0.5f, 0.5f);
            _titleCard.sizeDelta = new Vector2(1500f, 700f);
            _titleCard.anchoredPosition = new Vector2(0f, 10f);
            _titleGroup = _titleCard.gameObject.AddComponent<CanvasGroup>();
            _titleGroup.alpha = 0f;
            _titleGroup.blocksRaycasts = false;

            var shade = NewImage("Shade", _titleCard, _soft, new Color(0f, 0f, 0f, 0.72f));
            Stretch(shade.rectTransform);
            shade.rectTransform.offsetMin = new Vector2(-300f, -160f); shade.rectTransform.offsetMax = new Vector2(300f, 160f);
            shade.raycastTarget = false;

            // 타이틀 화면의 게임 로고(투명 배경 그림)를 그대로 쓴다. 로고를 못 찾을 때만 글자로 대신한다.
            var logo = FindTitleLogo();
            if (logo != null)
            {
                var image = NewImage("Logo", _titleCard, logo, Color.white);
                image.preserveAspect = true;
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                image.rectTransform.sizeDelta = new Vector2(1150f, 566f);
                image.raycastTarget = false;
            }
            else
            {
                _title = NewText("Title", _titleCard, 150f, new Color(1f, 0.84f, 0.42f), TextAlignmentOptions.Center);
                _title.text = "용사 때문에 레벨업";
                _title.fontStyle = FontStyles.Bold;
                Stretch(_title.rectTransform);
                _title.outlineWidth = 0.28f;
                _title.outlineColor = new Color32(70, 10, 20, 255);
            }
        }

        /// <summary>타이틀 화면(TitleScreen)에 연결된 로고 그림을 가져온다. 스토리는 타이틀에서 시작되므로 같은 장면에 있다.</summary>
        private static Sprite FindTitleLogo()
        {
            var title = FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
            if (title == null) return null;
            var field = typeof(TitleScreen).GetField("_logo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field != null ? field.GetValue(title) as Sprite : null;
        }

        private void BuildHud()
        {
            // 오른쪽 위 「건너뛰기」
            _skipRect = NewRect("Skip", _root);
            _skipRect.anchorMin = _skipRect.anchorMax = new Vector2(1f, 1f);
            _skipRect.pivot = new Vector2(1f, 1f);
            _skipRect.sizeDelta = new Vector2(250f, 62f);
            _skipRect.anchoredPosition = new Vector2(-34f, -12f);
            var bg = NewImage("Bg", _skipRect, null, new Color(0f, 0f, 0f, 0.0f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = false;
            _skipLabel = NewText("Label", _skipRect, 32f, new Color(0.9f, 0.82f, 0.66f, 0.85f), TextAlignmentOptions.Right);
            _skipLabel.text = "건너뛰기 ▶▶  (Esc)";
            Stretch(_skipLabel.rectTransform);

            // 장면 점
            var dotsRoot = NewRect("Dots", _root);
            dotsRoot.anchorMin = dotsRoot.anchorMax = new Vector2(0.5f, 1f);
            dotsRoot.pivot = new Vector2(0.5f, 1f);
            dotsRoot.sizeDelta = new Vector2(Slides.Length * 34f, 30f);
            dotsRoot.anchoredPosition = new Vector2(0f, -30f);
            for (int i = 0; i < Slides.Length; i++)
            {
                var img = NewImage("Dot", dotsRoot, _dot, new Color(Gold.r, Gold.g, Gold.b, 0.28f));
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                img.rectTransform.sizeDelta = new Vector2(14f, 14f);
                img.rectTransform.anchoredPosition = new Vector2(17f + i * 34f, 0f);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                img.raycastTarget = false;
                _dots.Add(img.rectTransform);
                _dotImages.Add(img);
            }
        }

        // ───────────── 도우미

        private Texture2D Load(string path)
        {
            if (_textures.TryGetValue(path, out var cached) && cached != null) return cached;
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning("[스토리] 그림을 찾지 못했습니다: Resources/" + path);
                tex = Texture2D.blackTexture;
            }
            tex.filterMode = FilterMode.Point; // 도트가 번지지 않게
            tex.wrapMode = TextureWrapMode.Clamp;
            _textures[path] = tex;
            return tex;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
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

        private static RawImage NewRaw(string name, Transform parent, Texture texture, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = texture;
            raw.color = color;
            return raw;
        }

        private TMP_Text NewText(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static TMP_FontAsset FindFont()
        {
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters("마왕성용사때문에레벨업", out _, true, true)) return font;
            return null;
        }

        private static Sprite MakeDot()
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>가운데는 투명하고 가장자리로 갈수록 짙어지는 막(비네트). 색은 Image.color 가 정한다.</summary>
        private static Sprite MakeVignette()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - (s - 1) * 0.5f) / (s * 0.5f), dy = (y - (s - 1) * 0.5f) / (s * 0.5f);
                    float d = Mathf.Sqrt(dx * dx * 0.85f + dy * dy * 1.1f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.6f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>가운데가 짙고 가장자리로 갈수록 투명한 막(제목 글자 뒤 그늘). 색은 Image.color 가 정한다.</summary>
        private static Sprite MakeSoft()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - (s - 1) * 0.5f) / (s * 0.5f), dy = (y - (s - 1) * 0.5f) / (s * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);       // 가운데가 짙고 바깥이 투명
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
