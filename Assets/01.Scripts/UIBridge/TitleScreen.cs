using System.Collections;
using System.Collections.Generic;
using OZGL2.Progression;
using OZGL2.Tutorial;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 타이틀 화면 — 글자와 캐릭터 없이 가운데 로고만 두고, 해 질 녘의 중세 성을 배경으로 깐다.
    /// 노을 하늘과 떠가는 구름, 멀리 겹친 언덕, 횃불이 일렁이고 깃발이 펄럭이는 돌성, 양옆에 늘어진 문장 깃발,
    /// 금빛 테두리, 피어오르는 불씨. 화면 아무 곳이나 누르거나 Enter·Space 를 누르면 어두워지며 로비로 넘어간다
    /// (로비에서 마왕의 설명이 이어진다). 그림은 모두 실행 중에 코드로 그린다.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] private string _lobbyScene = "Lobby";
        [SerializeField, Tooltip("게임 로고(투명 배경)")] private Sprite _logo;
        [SerializeField, Tooltip("로고 이미지가 없을 때 대신 보이는 글자 제목")] private string _gameTitle = "용사 때문에 레벨업";
        [SerializeField, Min(0.1f)] private float _fadeSeconds = 0.45f;
        [SerializeField, Range(0f, 80f), Tooltip("마우스에 따라 배경이 움직이는 정도(시차)")] private float _parallax = 30f;
        [Header("희수 UI 조각(메뉴·설정 창)")]
        [SerializeField, Tooltip("창·단추 프레임(Frame_WavePreview_Flat, 9분할)")] private Sprite _panelFrame;
        [SerializeField, Tooltip("창 제목 이름표(Frame_SynergyNameplate_Flat, 9분할)")] private Sprite _namePlate;
        [SerializeField, Tooltip("으뜸 단추 붉은 배너(Frame_StartCombat_Flat, 고정 비율)")] private Sprite _startBanner;
        [SerializeField, Tooltip("모서리 마름모(Ornament_Diamond_Flat)")] private Sprite _cornerDiamond;
        [SerializeField, Tooltip("이름표 양옆 장식(Ornament_WaveTitle_Flat)")] private Sprite _titleOrnament;
        [SerializeField, Tooltip("배너 마름모 안 아이콘(Icon_CrossedSwords_Casual)")] private Sprite _swordsIcon;
        [SerializeField, Tooltip("공통 설정창 프리팹. 미연결 시 기존 설정창을 사용합니다.")] private GameObject _settingsPopupPrefab;
        [Header("타이틀 메뉴 문장 디자인")]
        [SerializeField] private Sprite _titlePrimaryButton;
        [SerializeField] private Sprite _titleSecondaryButton;
        [SerializeField] private Sprite _titleSwordsIcon;
        [SerializeField] private Sprite _titleSettingsIcon;
        [SerializeField] private Sprite _titleExitIcon;
        [SerializeField] private Sprite _titleConfirmFrame;
        [SerializeField, Tooltip("처음부터 확인창의 설명 본문 글꼴. 제목과 버튼 글꼴은 유지합니다.")] private TMP_FontAsset _titleConfirmBodyFont;
        [SerializeField, Tooltip("편집 가능한 처음부터 확인창. 미연결 시 기존 코드 생성 방식을 유지합니다.")] private TitleConfirmationView _titleConfirmPrefab;
        [SerializeField, Min(0.1f), Tooltip("9분할 테두리의 표시 배율. 높이가 다른 버튼에서도 동일한 두께를 유지합니다.")] private float _titleBorderScale = 4f;

        private TMP_FontAsset _font;
        private RectTransform _logoRect, _sun, _mountains, _hillsFar, _forestFar, _castleNear, _village, _hillsNear, _forestNear;
        private RectTransform _windmill, _rays, _dragon, _dragonNear, _dragonFar;
        private float _dragonNext;
        private readonly List<(RectTransform rt, float phase)> _flames = new List<(RectTransform, float)>();
        private readonly List<(RectTransform rt, Image img, float phase, float speed)> _sparkles = new List<(RectTransform, Image, float, float)>();
        private readonly List<(RectTransform rt, float phase, float baseX, float baseY, float speed)> _birds = new List<(RectTransform, float, float, float, float)>();
        private readonly List<(RectTransform rt, Image img, Vector2 origin, float phase)> _smoke = new List<(RectTransform, Image, Vector2, float)>();
        private Image _fade, _logoGlow;
        private GameObject _ownedEventSystem;
        private bool _starting;
        private float _introStart;
        private Sprite _dot, _glow, _round, _roundFlat, _roundRing, _chevron;
        private RectTransform _starsRoot;
        private GameObject _settingsPanel, _confirmPanel;
        private UICommonSettingsView _commonSettingsView;
        private TMP_Text _volumeLabel, _tutorialLabel;
        private TMP_Text _fullscreenLabel, _bgmLabel, _sfxLabel;
        private bool _fullscreenOn;
        private readonly List<(RectTransform rt, CanvasGroup group, Vector2 end, float delay)> _intro = new List<(RectTransform, CanvasGroup, Vector2, float)>();
        private float _tutorialLabelResetAt;
        private const string VolumeKey = "OZGL2.Volume";
        private const int CastleW = 320, CastleH = 96, VillageW = 384, VillageH = 64;
        private const float CastleWidth = 1500f, CastleHeight = 460f;
        private readonly List<(RectTransform rt, float speed, float sway, float phase, float baseX, Image img)> _embers = new List<(RectTransform, float, float, float, float, Image)>();
        private readonly List<(Image img, float phase, float speed)> _stars = new List<(Image, float, float)>();
        private readonly List<(RectTransform rt, float speed, float width)> _clouds = new List<(RectTransform, float, float)>();
        private readonly List<(RectTransform rt, Image img, float phase)> _torches = new List<(RectTransform, Image, float)>();
        private readonly List<(RectTransform rt, float phase)> _flags = new List<(RectTransform, float)>();
        private readonly List<(RectTransform rt, float phase)> _banners = new List<(RectTransform, float)>();

        private void Start()
        {
            Time.timeScale = 1f;
            EnsureEventSystem();
            _font = UiFontOverride.Current != null ? UiFontOverride.Current : FindFont();
            BuildUi();
            _introStart = Time.unscaledTime;
        }

        private void Update()
        {
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            AnimateEmbers(t, dt);
            AnimateStars(t);
            AnimateClouds(dt);
            AnimateCastle(t);
            AnimateScenery(t, dt);
            AnimateIntro(t);
            AnimateParallax(dt);

            if (_tutorialLabel != null && _tutorialLabelResetAt > 0f && t > _tutorialLabelResetAt) { _tutorialLabel.text = "다시 보기"; _tutorialLabelResetAt = 0f; }

            var kb = Keyboard.current;
            if (kb == null || _starting) return;
            if ((_settingsPanel != null && _settingsPanel.activeSelf) || (_confirmPanel != null && _confirmPanel.activeSelf))
            {
                if (kb.escapeKey.wasPressedThisFrame) { CloseSettings(); _confirmPanel.SetActive(false); }
                return;
            }
            // Enter·Space: 저장이 있으면 이어하기, 없으면 새로 시작
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                if (SaveGame.HasSave) ContinueGame(); else BeginNewGame();
            }
        }

        /// <summary>이어하기: 저장된 진행 그대로 로비로.</summary>
        private void ContinueGame()
        {
            if (_starting || !SaveGame.HasSave) return;
            StartGame(); // 마지막 접속 시각은 로비 씬이 열릴 때 SaveGame 이 알아서 갱신한다
        }

        /// <summary>처음부터: 저장이 있으면 지워도 되는지 먼저 묻는다. 저장이 없으면 바로 시작.</summary>
        private void NewGameClicked()
        {
            if (_starting) return;
            if (SaveGame.HasSave) _confirmPanel.SetActive(true);
            else BeginNewGame();
        }

        private void BeginNewGame()
        {
            if (_starting) return;
            SaveGame.StartNew();
            StartGame();
        }

        private void OpenSettings()
        {
            if (_starting || _settingsPanel == null) return;
            if (_commonSettingsView == null)
            {
                _fullscreenOn = Screen.fullScreen;
                RefreshFullscreenLabel();
                RefreshAudioLabels();
            }
            _settingsPanel.SetActive(true);
        }

        private void RefreshFullscreenLabel()
        {
            if (_fullscreenLabel == null) return;
            _fullscreenLabel.text = _fullscreenOn ? "켜짐" : "꺼짐";
        }

        private void RefreshAudioLabels()
        {
            if (_bgmLabel != null) _bgmLabel.text = GameAudioSettings.BgmEnabled ? "켜짐" : "꺼짐";
            if (_sfxLabel != null) _sfxLabel.text = GameAudioSettings.SfxEnabled ? "켜짐" : "꺼짐";
        }

        private void ToggleBgm()
        {
            GameAudioSettings.SetBgm(!GameAudioSettings.BgmEnabled);
            RefreshAudioLabels();
        }

        private void ToggleSfx()
        {
            GameAudioSettings.SetSfx(!GameAudioSettings.SfxEnabled);
            RefreshAudioLabels();
        }

        private void CloseSettings()
        {
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        private void QuitGame()
        {
            if (_starting) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetVolume(float value)
        {
            AudioListener.volume = value;
            if (_volumeLabel != null) _volumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
            PlayerPrefs.SetFloat(VolumeKey, value);
        }

        private void ToggleFullscreen()
        {
            _fullscreenOn = !_fullscreenOn; // 에디터에서는 실제 창 모드가 바뀌지 않으므로 스위치는 따로 기억한다
            Screen.fullScreen = _fullscreenOn;
            RefreshFullscreenLabel();
        }

        private void ResetTutorial()
        {
            TutorialStore.ResetAll();
            _tutorialLabel.text = "초기화됨!";
            _tutorialLabelResetAt = Time.unscaledTime + 1.6f;
        }

        // ───────────── 동작

        public void StartGame()
        {
            if (_starting) return;
            _starting = true;
            StartCoroutine(FadeAndLoad());
        }

        private IEnumerator FadeAndLoad()
        {
            for (float t = 0f; t < _fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _fade.color = new Color(0f, 0f, 0f, t / _fadeSeconds);
                yield return null;
            }
            _fade.color = Color.black;
            if (_ownedEventSystem != null) { Destroy(_ownedEventSystem); _ownedEventSystem = null; }
            yield return null; // 이벤트 시스템이 지워진 뒤에 로비를 연다
            // 씬 이름이 바뀌기 전 값(예: Lobby_2)이 저장돼 있어도 로비로 가도록 한다
            string target = Application.CanStreamedLevelBeLoaded(_lobbyScene) ? _lobbyScene : "Lobby";
            SceneManager.LoadScene(target);
        }

        private IEnumerator FadeIn()
        {
            for (float t = 0f; t < _fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _fade.color = new Color(0f, 0f, 0f, 1f - t / _fadeSeconds);
                yield return null;
            }
            _fade.color = Color.clear;
        }

        // ───────────── 화면 만들기

        private void BuildUi()
        {
            var canvasGo = new GameObject("TitleCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            _dot = MakeDot(); _glow = MakeGlow(128); _round = MakeRound(true); _roundFlat = MakeRound(false); _roundRing = MakeRoundRing(2.4f); _chevron = MakeChevron();
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);

            // 1) 노을 하늘: 위는 짙은 남색, 아래로 갈수록 보랏빛을 거쳐 지평선은 호박색
            var sky = NewImage("Sky", root, MakeSky(), Color.white);
            Stretch(sky.rectTransform); sky.raycastTarget = false;

            // 2) 별(하늘 위쪽만)
            _starsRoot = NewRect("Stars", root);
            Stretch(_starsRoot);
            for (int i = 0; i < 46; i++) SpawnStar();

            // 3) 지는 해: 지평선 위에 걸린 따뜻한 빛
            _sun = NewRect("Sun", root);
            _sun.anchorMin = _sun.anchorMax = new Vector2(0.5f, 0f);
            _sun.pivot = new Vector2(0.5f, 0.5f);
            _sun.sizeDelta = new Vector2(1500f, 1500f);
            _sun.anchoredPosition = new Vector2(0f, 430f);
            var sunGlow = NewImage("SunGlow", _sun, _glow, new Color(1f, 0.62f, 0.30f, 0.62f));
            Stretch(sunGlow.rectTransform); sunGlow.raycastTarget = false;
            var sunDisc = NewImage("SunDisc", _sun, _dot, new Color(1f, 0.86f, 0.55f, 0.95f));
            sunDisc.rectTransform.anchorMin = sunDisc.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sunDisc.rectTransform.sizeDelta = new Vector2(280f, 280f); sunDisc.raycastTarget = false;
            // 해에서 사방으로 뻗는 햇살(천천히 돈다)
            _rays = NewRect("Rays", _sun);
            _rays.anchorMin = _rays.anchorMax = new Vector2(0.5f, 0.5f); _rays.sizeDelta = Vector2.zero;
            var raySprite = MakeRay();
            for (int i = 0; i < 12; i++)
            {
                var ray = NewImage("Ray", _rays, raySprite, new Color(1f, 0.84f, 0.52f, i % 2 == 0 ? 0.13f : 0.08f));
                ray.raycastTarget = false;
                var rr = ray.rectTransform;
                rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f); rr.pivot = new Vector2(0.5f, 0f);
                rr.sizeDelta = new Vector2(i % 2 == 0 ? 190f : 120f, 1500f);
                rr.localRotation = Quaternion.Euler(0f, 0f, i * 30f + (i % 3) * 4f);
            }
            _rays.SetSiblingIndex(1); // 해 뒤(빛 번짐 바로 위)

            // 4) 구름: 노을빛을 받아 천천히 흘러간다
            for (int i = 0; i < 7; i++) SpawnCloud(root);

            // 5) 풍경(멀리서 가까이): 눈 덮인 산맥 → 먼 언덕 → 먼 소나무 숲 → 성 → 마을과 풍차 → 가까운 언덕 → 가까운 소나무 숲
            _mountains = MakeLayer(root, "Mountains", MakeMountains(5), 2400f, 640f, 120f);
            _hillsFar = MakeLayer(root, "HillsFar", MakeHills(3, new Color(0.36f, 0.22f, 0.38f), 0.30f, 0.14f), 2300f, 300f);
            _forestFar = MakeLayer(root, "ForestFar", MakeForest(4, new Color(0.24f, 0.15f, 0.30f), 10, 26, 0f), 2300f, 330f, 10f);
            var castleSprite = MakeCastle(23, new Color(0.07f, 0.045f, 0.10f), true, out var torchSpots, out var flagSpots);
            _castleNear = MakeLayer(root, "CastleNear", castleSprite, CastleWidth, CastleHeight, 20f);
            foreach (var spot in torchSpots) SpawnTorch(_castleNear, spot);
            foreach (var spot in flagSpots) SpawnFlag(_castleNear, spot);
            var villageSprite = MakeVillage(17, new Color(0.10f, 0.06f, 0.13f), out var chimneys, out var windmillSpot);
            _village = MakeLayer(root, "Village", villageSprite, 2300f, 300f, -6f);
            foreach (var spot in chimneys) SpawnSmoke(_village, spot);
            SpawnWindmill(_village, windmillSpot);
            _hillsNear = MakeLayer(root, "HillsNear", MakeHills(8, new Color(0.12f, 0.08f, 0.16f), 0.22f, 0.10f), 2300f, 200f, -30f);
            _forestNear = MakeLayer(root, "ForestNear", MakeForest(9, new Color(0.07f, 0.045f, 0.10f), 28, 62, 0.34f), 2300f, 420f, -40f);
            for (int i = 0; i < 6; i++) SpawnBird(root, i);
            SpawnDragon(root);

            // 6) 떠오르는 불씨(횃불에서 날아오르는 느낌)
            for (int i = 0; i < 30; i++) SpawnEmber(root);

            // 7) 양옆에 늘어진 문장 깃발
            SpawnBanner(root, -1f);
            SpawnBanner(root, 1f);

            // 8) 로고(가운데, 통통 튀며 나타난다) + 뒤의 빛
            _logoGlow = NewImage("LogoGlow", root, _glow, new Color(1f, 0.70f, 0.38f, 0.40f));
            _logoGlow.raycastTarget = false;
            RectTransform logoRect;
            if (_logo != null)
            {
                var logoImage = NewImage("Logo", root, _logo, Color.white);
                logoImage.preserveAspect = true; logoImage.raycastTarget = false;
                logoRect = logoImage.rectTransform;
                float aspect = _logo.rect.height / _logo.rect.width;
                const float width = 1060f;
                logoRect.sizeDelta = new Vector2(width, width * aspect);
            }
            else
            {
                var title = NewText("Title", root, 150f, new Color(1f, 0.86f, 0.45f), TextAlignmentOptions.Center);
                title.text = _gameTitle; title.outlineWidth = 0.22f; title.outlineColor = new Color32(70, 14, 24, 255);
                logoRect = title.rectTransform; logoRect.sizeDelta = new Vector2(1250f, 230f);
            }
            logoRect.anchorMin = logoRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            logoRect.anchoredPosition = new Vector2(0f, 225f);
            _logoRect = logoRect;
            var lg = _logoGlow.rectTransform;
            lg.anchorMin = lg.anchorMax = new Vector2(0.5f, 0.5f); lg.pivot = new Vector2(0.5f, 0.5f);
            lg.sizeDelta = logoRect.sizeDelta * 1.25f; lg.anchoredPosition = logoRect.anchoredPosition;
            lg.SetSiblingIndex(logoRect.GetSiblingIndex());
            SpawnSparkles(root, logoRect.anchoredPosition);

            // 9) 로고 아래 메뉴. 문장 아트 미연결 씬은 기존 배너 배치를 유지한다.
            BuildDivider(root, new Vector2(0f, -62f));
            bool useHeraldryButtons = _titlePrimaryButton != null && _titleSecondaryButton != null;
            float menuW = useHeraldryButtons ? 520f : 400f;
            float bannerH = useHeraldryButtons ? 120f : _startBanner != null ? menuW * 194f / 500f : 120f;
            float secondaryW = useHeraldryButtons ? 500f : menuW;
            float secondaryH = useHeraldryButtons ? 82f : 66f;
            float utilityH = useHeraldryButtons ? 78f : 60f;
            float utilityGap = useHeraldryButtons ? 28f : 12f;
            float utilityW = (secondaryW - utilityGap) * 0.5f;
            float primaryY = -62f - (useHeraldryButtons ? 24f : 34f) - bannerH * 0.5f;
            float utilityY;
            if (SaveGame.HasSave)
            {
                // 저장이 있으면: 이어하기(으뜸) + 저장 요약 + 처음부터
                RegisterIntro(ButtonRect(MakeTitleMenuButton(root, "이어하기", new Vector2(0f, primaryY), new Vector2(menuW, bannerH), 50f, true, _titleSwordsIcon, ContinueGame)), 0.45f);
                var summary = NewText("SaveSummary", root, 24f, new Color(0.93f, 0.87f, 0.75f, 0.95f), TextAlignmentOptions.Center);
                summary.text = SaveGame.Summary();
                summary.enableAutoSizing = true; summary.fontSizeMin = 16f; summary.fontSizeMax = 24f;
                AddTextShadow(summary);
                var sm = summary.rectTransform;
                sm.anchorMin = sm.anchorMax = new Vector2(0.5f, 0.5f); sm.pivot = new Vector2(0.5f, 0.5f);
                float summaryY = primaryY - bannerH * 0.5f - (useHeraldryButtons ? 40f : 24f);
                sm.sizeDelta = new Vector2(menuW + 120f, 34f); sm.anchoredPosition = new Vector2(0f, summaryY);
                RegisterIntro(sm, 0.55f);
                float secondY = summaryY - 17f - (useHeraldryButtons ? 16f : 14f) - secondaryH * 0.5f;
                RegisterIntro(ButtonRect(MakeTitleMenuButton(root, "처음부터", new Vector2(0f, secondY), new Vector2(secondaryW, secondaryH), 38f, false, null, NewGameClicked)), 0.62f);
                utilityY = secondY - secondaryH * 0.5f - (useHeraldryButtons ? 20f : 12f) - utilityH * 0.5f;
            }
            else
            {
                // 저장이 없으면(처음 켠 경우): 시작하기 하나
                RegisterIntro(ButtonRect(MakeTitleMenuButton(root, "시작하기", new Vector2(0f, primaryY), new Vector2(menuW, bannerH), 54f, true, _titleSwordsIcon, BeginNewGame)), 0.45f);
                utilityY = primaryY - bannerH * 0.5f - (useHeraldryButtons ? 20f : 12f) - utilityH * 0.5f;
            }
            float utilityX = useHeraldryButtons ? (secondaryW + utilityGap) * 0.25f : menuW * 0.25f + 6f;
            RegisterIntro(ButtonRect(MakeTitleMenuButton(root, "설정", new Vector2(-utilityX, utilityY), new Vector2(utilityW, utilityH), 34f, false, _titleSettingsIcon, OpenSettings)), 0.75f);
            RegisterIntro(ButtonRect(MakeTitleMenuButton(root, "나가기", new Vector2(utilityX, utilityY), new Vector2(utilityW, utilityH), 34f, false, _titleExitIcon, QuitGame)), 0.85f);

            // 10) 돌담 테두리 + 가장자리를 어둡게 하는 비네팅
            BuildFrame(root);
            var vignette = NewImage("Vignette", root, MakeVignette(), Color.white);
            Stretch(vignette.rectTransform); vignette.raycastTarget = false;
            SpawnWallTorch(root, -1f);
            SpawnWallTorch(root, 1f);

            // 11) 설정 창(맨 위) + 화면 전환용 검은 막
            BuildSettings(root);
            BuildConfirm(root);
            _fade = NewImage("Fade", root, null, Color.black);
            Stretch(_fade.rectTransform); _fade.raycastTarget = false;
            StartCoroutine(FadeIn());
        }

        private void BuildFrame(RectTransform root)
        {
            Color gold = new Color(0.96f, 0.76f, 0.38f, 0.85f), goldDim = new Color(0.96f, 0.76f, 0.38f, 0.40f);
            // 돌벽돌 띠(위·아래·왼·오른쪽) + 안쪽 금빛 줄
            var brick = MakeBrick();
            for (int i = 0; i < 4; i++)
            {
                var strip = NewImage("Bricks" + i, root, brick, Color.white);
                strip.type = Image.Type.Tiled; strip.raycastTarget = false;
                var rt = strip.rectTransform;
                switch (i)
                {
                    case 0: rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(0f, 32f); break;
                    case 1: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f); rt.sizeDelta = new Vector2(0f, 32f); break;
                    case 2: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f); rt.sizeDelta = new Vector2(32f, 0f); break;
                    default: rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 0.5f); rt.sizeDelta = new Vector2(32f, 0f); break;
                }
                rt.anchoredPosition = Vector2.zero;
            }
            AddFrameLine(root, "FrameInner", 32f, 4f, gold);
            AddFrameLine(root, "FrameInner2", 40f, 2f, goldDim);
            // 네 모서리의 금빛 마름모 장식
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                var d = NewImage("Corner", root, _dot, gold);
                d.raycastTarget = false;
                var rt = d.rectTransform;
                rt.anchorMin = rt.anchorMax = corner; rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(26f, 26f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                rt.anchoredPosition = new Vector2(corner.x > 0f ? -34f : 34f, corner.y > 0f ? -34f : 34f);
            }
        }

        private void AddFrameLine(RectTransform root, string name, float inset, float thickness, Color color)
        {
            // 위·아래·왼·오른쪽 네 줄
            for (int i = 0; i < 4; i++)
            {
                var img = NewImage(name + i, root, null, color);
                img.raycastTarget = false;
                var rt = img.rectTransform;
                switch (i)
                {
                    case 0: rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f); rt.offsetMin = new Vector2(inset, -inset - thickness); rt.offsetMax = new Vector2(-inset, -inset); break;
                    case 1: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f); rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, inset + thickness); break;
                    case 2: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f); rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(inset + thickness, -inset); break;
                    default: rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 0.5f); rt.offsetMin = new Vector2(-inset - thickness, inset); rt.offsetMax = new Vector2(-inset, -inset); break;
                }
            }
        }

        // ───────────── 단추와 설정 창

        /// <summary>로고 아래 금빛 장식선(가운데 마름모, 양끝 점).</summary>
        private void BuildDivider(RectTransform root, Vector2 pos)
        {
            Color gold = new Color(0.96f, 0.76f, 0.38f, 0.9f);
            void Piece(string name, Sprite sprite, Vector2 offset, Vector2 size, float angle)
            {
                var img = NewImage(name, root, sprite, gold);
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = size; rt.anchoredPosition = pos + offset;
                rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            Piece("DividerLeft", null, new Vector2(-160f, 0f), new Vector2(270f, 3f), 0f);
            Piece("DividerRight", null, new Vector2(160f, 0f), new Vector2(270f, 3f), 0f);
            Piece("DividerGem", _dot, Vector2.zero, new Vector2(24f, 24f), 45f);
            Piece("DividerEndL", _dot, new Vector2(-300f, 0f), new Vector2(9f, 9f), 0f);
            Piece("DividerEndR", _dot, new Vector2(300f, 0f), new Vector2(9f, 9f), 0f);
        }

        private static RectTransform ButtonRect(TMP_Text label) => (RectTransform)label.transform.parent;

        /// <summary>메뉴 요소가 아래에서 떠오르며 차례로 나타나게 등록한다(delay 초 뒤 시작).</summary>
        private void RegisterIntro(RectTransform rt, float delay)
        {
            var group = rt.gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            _intro.Add((rt, group, rt.anchoredPosition, delay));
            rt.anchoredPosition += new Vector2(0f, -46f);
        }

        private static readonly Color Cream = new Color(0.93f, 0.87f, 0.75f), Gold = new Color(1f, 0.84f, 0.48f);

        private static void AddTextShadow(TMP_Text text)
        {
            if (text.GetComponent<Shadow>() != null) return;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>타이틀 메뉴·확인창의 문장 버튼. 아트가 없는 씬과 설정 fallback은 기존 MakeButton을 유지한다.</summary>
        private TMP_Text MakeTitleMenuButton(RectTransform parent, string label, Vector2 pos, Vector2 size, float fontSize,
            bool primary, Sprite iconSprite, UnityEngine.Events.UnityAction onClick)
        {
            if (_titlePrimaryButton == null || _titleSecondaryButton == null)
                return MakeButton(parent, label, pos, size, fontSize, primary, onClick);

            var hit = NewImage("Button_" + label, parent, null, Color.clear);
            var rt = hit.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            Color glowColor = primary ? new Color(1f, 0.5f, 0.34f, 0.14f) : new Color(Cream.r, Cream.g, Cream.b, 0.12f);
            var glow = NewImage("Glow", rt, _glow, glowColor);
            glow.raycastTarget = false;
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(size.x * 1.12f, size.y * 1.5f);

            Color baseColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            var body = NewImage("Body", rt, primary ? _titlePrimaryButton : _titleSecondaryButton, baseColor);
            body.type = Image.Type.Sliced;
            body.pixelsPerUnitMultiplier = Mathf.Max(0.1f, _titleBorderScale);
            body.raycastTarget = false;
            Stretch(body.rectTransform);

            var labelText = NewText("Label", rt, fontSize, Cream, TextAlignmentOptions.Center);
            labelText.text = label;
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = fontSize * 0.5f;
            labelText.fontSizeMax = fontSize;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            AddTextShadow(labelText);
            Stretch(labelText.rectTransform);
            labelText.rectTransform.offsetMin = new Vector2(24f, 8f);
            labelText.rectTransform.offsetMax = new Vector2(-24f, -8f);

            if (iconSprite != null)
            {
                float iconSize = primary ? 60f : 34f;
                float iconInset = primary ? 48f : 32f;
                var icon = NewImage("Icon", rt, iconSprite, Color.white);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
                icon.rectTransform.anchoredPosition = new Vector2(iconInset + iconSize * 0.5f, 0f);
                float textInset = iconInset + iconSize + (primary ? 44f : 16f);
                labelText.rectTransform.offsetMin = new Vector2(textInset, 8f);

                if (primary)
                {
                    var divider = NewImage("IconDivider", rt, null, new Color(Gold.r, Gold.g, Gold.b, 0.72f));
                    divider.raycastTarget = false;
                    divider.rectTransform.anchorMin = divider.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    divider.rectTransform.sizeDelta = new Vector2(1.5f, 54f);
                    divider.rectTransform.anchoredPosition = new Vector2(iconInset + iconSize + 24f, 0f);
                }
            }

            var button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(onClick);
            hit.gameObject.AddComponent<TitleButtonFx>().Setup(body, null, glow, null, labelText,
                baseColor, Color.white, Color.clear, Color.clear, Cream, primary ? Color.white : Gold, glowColor, primary);
            return labelText;
        }

        /// <summary>
        /// 희수의 UI 조각으로 만든 단추. 으뜸 단추는 붉은 배너(왼쪽 마름모에 칼 아이콘, 글자는 오른쪽 띠 안 가운데),
        /// 나머지는 어두운 프레임에 크림색 글자. 글자는 칸 안에서 가운데 정렬되고 길면 자동으로 줄어 칸을 넘지 않는다.
        /// 마우스를 올리면 살짝 커지며 밝아지고 빛이 번진다. danger 는 글자가 붉은 확인용 단추.
        /// </summary>
        private TMP_Text MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 size, float fontSize, bool primary, UnityEngine.Events.UnityAction onClick, bool danger = false)
        {
            bool banner = primary && !danger && _startBanner != null;
            Color text = danger ? new Color(1f, 0.58f, 0.52f) : primary ? Gold : Cream;
            Color hoverText = danger ? new Color(1f, 0.74f, 0.68f) : primary ? Color.white : Gold;
            Color glowColor = primary ? new Color(1f, 0.5f, 0.34f, 0.5f) : new Color(0.93f, 0.87f, 0.75f, 0.30f);

            var hit = NewImage("Button_" + label, parent, null, Color.clear); // 눌리는 영역(보이지 않음)
            var rt = hit.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;

            var glow = NewImage("Glow", rt, _glow, glowColor);
            glow.raycastTarget = false;
            var glr = glow.rectTransform;
            glr.anchorMin = glr.anchorMax = new Vector2(0.5f, 0.5f); glr.sizeDelta = new Vector2(size.x * 1.25f, size.y * 1.9f);

            Sprite sprite = banner ? _startBanner : (_panelFrame != null ? _panelFrame : _roundFlat);
            bool art = banner || _panelFrame != null;
            Color baseColor = art ? new Color(0.86f, 0.86f, 0.86f) : new Color(0.09f, 0.09f, 0.09f, 0.97f);
            Color hoverColor = art ? Color.white : new Color(0.2f, 0.17f, 0.15f, 1f);
            var body = NewImage("Body", rt, sprite, baseColor);
            if (!banner) body.type = Image.Type.Sliced;
            body.raycastTarget = false;
            Stretch(body.rectTransform);

            var labelText = NewText("Label", rt, fontSize, text, TextAlignmentOptions.Center);
            labelText.text = label;
            labelText.enableAutoSizing = true; labelText.fontSizeMin = fontSize * 0.5f; labelText.fontSizeMax = fontSize;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            AddTextShadow(labelText);
            var lr = labelText.rectTransform;
            if (banner)
            {
                // 왼쪽 마름모를 비우고 오른쪽 붉은 띠 안 가운데
                lr.anchorMin = new Vector2(0.37f, 0.08f); lr.anchorMax = new Vector2(0.93f, 0.92f);
                lr.offsetMin = lr.offsetMax = Vector2.zero;
                if (_swordsIcon != null)
                {
                    var icon = NewImage("Icon", rt, _swordsIcon, Color.white);
                    icon.preserveAspect = true; icon.raycastTarget = false;
                    var ir = icon.rectTransform;
                    ir.anchorMin = ir.anchorMax = new Vector2(0.176f, 0.5f); ir.pivot = new Vector2(0.5f, 0.5f);
                    ir.sizeDelta = new Vector2(size.y * 0.4f, size.y * 0.4f);
                    ir.anchoredPosition = Vector2.zero;
                }
            }
            else
            {
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(18f, 6f); lr.offsetMax = new Vector2(-18f, -6f);
            }

            var button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(onClick);
            hit.gameObject.AddComponent<TitleButtonFx>().Setup(body, null, glow, null, labelText, baseColor, hoverColor, Color.clear, Color.clear, text, hoverText, glowColor, primary || danger);
            return labelText;
        }

        /// <summary>
        /// 창 틀(희수의 어두운 프레임): 네 모서리 마름모, 위 가장자리에 걸친 이름표(제목, 양옆 장식), 그 아래 머리줄.
        /// 안쪽 내용은 창 가운데를 (0,0)으로 놓는다. 제목 글자는 이름표 안 가운데에 맞춰 자동으로 줄어든다.
        /// </summary>
        private RectTransform MakeFlatPanel(RectTransform parent, Vector2 size, string title, float plateWidth)
        {
            var sprite = _panelFrame != null ? _panelFrame : _roundFlat;
            var img = NewImage("Panel", parent, sprite, _panelFrame != null ? Color.white : new Color(0.09f, 0.09f, 0.09f, 0.98f));
            img.type = Image.Type.Sliced;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            if (_cornerDiamond != null)
                foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
                {
                    var d = NewImage("Corner", rt, _cornerDiamond, Color.white);
                    d.raycastTarget = false;
                    var dr = d.rectTransform;
                    dr.anchorMin = dr.anchorMax = corner; dr.pivot = new Vector2(0.5f, 0.5f);
                    dr.sizeDelta = new Vector2(34f, 34f);
                    dr.anchoredPosition = Vector2.zero;
                }

            const float plateH = 78f;
            var plate = NewImage("Plate", rt, _namePlate != null ? _namePlate : _roundFlat, _namePlate != null ? Color.white : new Color(0.3f, 0.1f, 0.1f, 1f));
            plate.type = Image.Type.Sliced; plate.raycastTarget = false;
            var pr = plate.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(plateWidth, plateH); pr.anchoredPosition = Vector2.zero;
            var titleText = NewText("PlateText", pr, 44f, Gold, TextAlignmentOptions.Center);
            titleText.text = title;
            titleText.enableAutoSizing = true; titleText.fontSizeMin = 24f; titleText.fontSizeMax = 44f;
            AddTextShadow(titleText);
            Stretch(titleText.rectTransform);
            titleText.rectTransform.offsetMin = new Vector2(20f, 8f); titleText.rectTransform.offsetMax = new Vector2(-20f, -8f);
            if (_titleOrnament != null)
                foreach (float side in new[] { -1f, 1f })
                {
                    var o = NewImage("PlateOrnament", rt, _titleOrnament, Color.white);
                    o.raycastTarget = false;
                    var orr = o.rectTransform;
                    orr.anchorMin = orr.anchorMax = new Vector2(0.5f, 1f); orr.pivot = new Vector2(0.5f, 0.5f);
                    orr.sizeDelta = new Vector2(30f, 30f);
                    orr.anchoredPosition = new Vector2(side * (plateWidth * 0.5f + 34f), 0f);
                }
            var line = NewImage("HeaderLine", rt, null, new Color(Cream.r, Cream.g, Cream.b, 0.5f));
            line.raycastTarget = false;
            var lr = line.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1f); lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(size.x - 120f, 2f); lr.anchoredPosition = new Vector2(0f, -(plateH * 0.5f + 20f));
            return rt;
        }

        private TMP_Text PlaceLabel(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align, Vector2 pos, Vector2 box)
        {
            var label = NewText("Label_" + text, parent, size, color, align);
            label.text = text;
            AddTextShadow(label);
            var r = label.rectTransform;
            bool left = align == TextAlignmentOptions.Left;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(left ? 0f : 0.5f, 0.5f);
            r.sizeDelta = box; r.anchoredPosition = pos;
            return label;
        }

        private void Hairline(RectTransform parent, float y, float width, Color color)
        {
            var line = NewImage("Hairline", parent, null, color);
            line.raycastTarget = false;
            var r = line.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(width, 2f); r.anchoredPosition = new Vector2(0f, y);
        }

        /// <summary>설정 창: 소리 크기 슬라이더, 배경음악·효과음 켜짐/꺼짐, 전체화면 켜짐/꺼짐, 마왕 설명 다시 보기. 줄마다 제목은 왼쪽, 조절은 오른쪽에 같은 선으로 맞춘다.</summary>
        private void BuildSettings(RectTransform root)
        {
            if (_settingsPopupPrefab != null)
            {
                UICommonSettingsView prefabView;
                if (_settingsPopupPrefab.TryGetComponent(out prefabView) && prefabView.IsConfigured)
                {
                    _commonSettingsView = Instantiate(prefabView, root, false);
                    _settingsPanel = _commonSettingsView.gameObject;
                    _settingsPanel.SetActive(false);
                    _commonSettingsView.ConfigureStandalone(CloseSettings);
                    return;
                }

                Debug.LogWarning("공통 설정창의 UICommonSettingsView 연결이 누락되어 기존 설정창을 사용합니다.", this);
            }

            var dim = NewImage("SettingsDim", root, null, new Color(0f, 0f, 0f, 0.66f));
            Stretch(dim.rectTransform);
            _settingsPanel = dim.gameObject;
            var panel = MakeFlatPanel(dim.rectTransform, new Vector2(920f, 840f), "설정", 320f);
            Color line = new Color(Cream.r, Cream.g, Cream.b, 0.22f);
            const float left = -400f, rowW = 780f;

            // 소리 크기
            PlaceLabel(panel, "소리 크기", 42f, Cream, TextAlignmentOptions.Left, new Vector2(left, 240f), new Vector2(260f, 60f));
            var sliderGo = new GameObject("VolumeSlider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            sliderGo.transform.SetParent(panel, false);
            sliderGo.GetComponent<Image>().color = Color.clear; // 누를 수 있는 영역을 넓힌다
            var sr = (RectTransform)sliderGo.transform;
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f); sr.sizeDelta = new Vector2(330f, 56f); sr.anchoredPosition = new Vector2(100f, 240f);
            var bar = NewImage("Bar", sr, null, new Color(Cream.r, Cream.g, Cream.b, 0.28f));
            bar.raycastTarget = false;
            bar.rectTransform.anchorMin = new Vector2(0f, 0.5f); bar.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 6f);
            var fillArea = NewRect("FillArea", sr);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f); fillArea.sizeDelta = new Vector2(0f, 6f);
            var fill = NewImage("Fill", fillArea, null, Gold);
            var handleArea = NewRect("HandleArea", sr);
            Stretch(handleArea); handleArea.offsetMin = new Vector2(20f, 0f); handleArea.offsetMax = new Vector2(-20f, 0f);
            var handle = NewImage("Handle", handleArea, _cornerDiamond != null ? _cornerDiamond : _dot, _cornerDiamond != null ? Color.white : Cream);
            handle.rectTransform.sizeDelta = new Vector2(40f, 40f);
            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            _volumeLabel = PlaceLabel(panel, string.Empty, 40f, Gold, TextAlignmentOptions.Right, new Vector2(340f, 240f), new Vector2(140f, 60f));
            slider.SetValueWithoutNotify(AudioListener.volume);
            _volumeLabel.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
            slider.onValueChanged.AddListener(SetVolume);
            Hairline(panel, 185f, rowW, line);

            // 배경음악 / 효과음
            PlaceLabel(panel, "배경음악", 42f, Cream, TextAlignmentOptions.Left, new Vector2(left, 130f), new Vector2(260f, 60f));
            _bgmLabel = MakeButton(panel, "켜짐", new Vector2(320f, 130f), new Vector2(170f, 62f), 36f, false, ToggleBgm);
            Hairline(panel, 75f, rowW, line);
            PlaceLabel(panel, "효과음", 42f, Cream, TextAlignmentOptions.Left, new Vector2(left, 20f), new Vector2(260f, 60f));
            _sfxLabel = MakeButton(panel, "켜짐", new Vector2(320f, 20f), new Vector2(170f, 62f), 36f, false, ToggleSfx);
            Hairline(panel, -35f, rowW, line);

            // 전체화면
            PlaceLabel(panel, "전체화면", 42f, Cream, TextAlignmentOptions.Left, new Vector2(left, -90f), new Vector2(260f, 60f));
            _fullscreenLabel = MakeButton(panel, "꺼짐", new Vector2(320f, -90f), new Vector2(170f, 62f), 36f, false, ToggleFullscreen);
            Hairline(panel, -145f, rowW, line);

            // 마왕의 설명 다시 보기
            PlaceLabel(panel, "마왕의 설명", 42f, Cream, TextAlignmentOptions.Left, new Vector2(left, -200f), new Vector2(260f, 60f));
            _tutorialLabel = MakeButton(panel, "다시 보기", new Vector2(290f, -200f), new Vector2(230f, 62f), 34f, false, ResetTutorial);
            Hairline(panel, -255f, rowW, line);

            MakeButton(panel, "닫기", new Vector2(0f, -336f), new Vector2(320f, 72f), 40f, true, CloseSettings);
            _settingsPanel.SetActive(false);
        }

        /// <summary>처음부터를 눌렀을 때 뜨는 확인 창: 저장된 진행이 사라진다고 알리고 「삭제하고 시작」/「취소」를 묻는다.</summary>
        private void BuildConfirm(RectTransform root)
        {
            if (_titleConfirmPrefab != null)
            {
                var view = Instantiate(_titleConfirmPrefab, root, false);
                if (view.Bind(BeginNewGame, () => _confirmPanel.SetActive(false)))
                {
                    _confirmPanel = view.gameObject;
                    Stretch((RectTransform)view.transform);
                    _confirmPanel.SetActive(false);
                    return;
                }

                Debug.LogWarning("타이틀 확인창의 버튼 연결이 없어 기존 확인창을 사용합니다.", this);
                Destroy(view.gameObject);
            }

            var dim = NewImage("ConfirmDim", root, null, new Color(0f, 0f, 0f, 0.66f));
            Stretch(dim.rectTransform);
            _confirmPanel = dim.gameObject;
            bool useHeraldryConfirm = _titleConfirmFrame != null && _titlePrimaryButton != null && _titleSecondaryButton != null;
            RectTransform panel;
            if (useHeraldryConfirm)
            {
                var frame = NewImage("Panel", dim.rectTransform, _titleConfirmFrame, Color.white);
                frame.type = Image.Type.Simple;
                panel = frame.rectTransform;
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(920f, 676f);
                var title = PlaceLabel(panel, "처음부터 시작할까요?", 46f, Cream, TextAlignmentOptions.Center,
                    new Vector2(0f, 154f), new Vector2(760f, 76f));
                title.gameObject.name = "Title";
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.enableAutoSizing = true;
                title.fontSizeMin = 36f;
                title.fontSizeMax = 46f;
                Hairline(panel, 100f, 700f, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
            }
            else
            {
                panel = MakeFlatPanel(dim.rectTransform, new Vector2(920f, 480f), "처음부터 시작할까요?", 560f);
            }
            var body = PlaceLabel(panel, "저장된 마왕 레벨, 특성, 스킬과\n마왕군 해금 정보가 모두 삭제됩니다.\n튜토리얼도 처음부터 다시 시작됩니다.", 38f, Cream, TextAlignmentOptions.Center,
                new Vector2(0f, useHeraldryConfirm ? 4f : 20f), new Vector2(780f, 170f));
            body.gameObject.name = "Description";
            if (_titleConfirmBodyFont != null) body.font = _titleConfirmBodyFont;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 8f;
            if (useHeraldryConfirm)
            {
                MakeTitleMenuButton(panel, "삭제하고 시작", new Vector2(-188f, -210f), new Vector2(340f, 88f), 36f, true, null, BeginNewGame);
                MakeTitleMenuButton(panel, "취소", new Vector2(188f, -210f), new Vector2(340f, 88f), 36f, false, null, () => _confirmPanel.SetActive(false));
            }
            else
            {
                MakeButton(panel, "삭제하고 시작", new Vector2(-200f, -150f), new Vector2(340f, 72f), 36f, false, BeginNewGame, true);
                MakeButton(panel, "취소", new Vector2(200f, -150f), new Vector2(280f, 72f), 38f, false, () => _confirmPanel.SetActive(false));
            }
            _confirmPanel.SetActive(false);
        }

        // ───────────── 배경 만들기

        private RectTransform MakeLayer(RectTransform root, string name, Sprite sprite, float width, float height, float y = 0f)
        {
            var img = NewImage(name, root, sprite, Color.white);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, y);
            return rt;
        }

        private void SpawnCloud(RectTransform root)
        {
            var img = NewImage("Cloud", root, _glow, new Color(1f, 0.74f, 0.58f, Random.Range(0.14f, 0.26f)));
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
            float w = Random.Range(520f, 980f);
            rt.sizeDelta = new Vector2(w, w * Random.Range(0.14f, 0.22f));
            rt.anchoredPosition = new Vector2(Random.Range(-200f, 2100f), Random.Range(560f, 940f));
            _clouds.Add((rt, Random.Range(8f, 22f), w));
        }

        private void AnimateClouds(float dt)
        {
            for (int i = 0; i < _clouds.Count; i++)
            {
                var c = _clouds[i];
                var p = c.rt.anchoredPosition;
                p.x += c.speed * dt;
                if (p.x - c.width * 0.5f > 1920f) p.x = -c.width * 0.5f;
                c.rt.anchoredPosition = p;
            }
        }

        private void SpawnStar()
        {
            var img = NewImage("Star", _starsRoot, _dot, new Color(1f, 0.95f, 0.85f, 0f));
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
            float size = Random.Range(3f, 8f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(Random.Range(0f, 1920f), Random.Range(700f, 1060f));
            _stars.Add((img, Random.Range(0f, 6.28f), Random.Range(1.2f, 3.5f)));
        }

        private void AnimateStars(float t)
        {
            for (int i = 0; i < _stars.Count; i++)
            {
                var s = _stars[i];
                float a = 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(t * s.speed + s.phase));
                s.img.color = new Color(1f, 0.95f, 0.85f, a * 0.8f);
            }
        }

        /// <summary>성 위에 놓이는 횃불의 불빛. 그림 좌표(칸)를 화면 좌표로 바꾸어 성 그림의 자식으로 둔다.</summary>
        private void SpawnTorch(RectTransform castle, Vector2 spot)
        {
            var img = NewImage("Torch", castle, _glow, new Color(1f, 0.62f, 0.25f, 0.8f));
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 120f);
            rt.anchoredPosition = CastleToCanvas(castle, spot);
            _torches.Add((rt, img, Random.Range(0f, 6.28f)));
            var core = NewImage("Flame", rt, _dot, new Color(1f, 0.90f, 0.55f, 0.95f));
            core.raycastTarget = false;
            core.rectTransform.sizeDelta = new Vector2(11f, 16f);
        }

        private void SpawnFlag(RectTransform castle, Vector2 spot)
        {
            var img = NewImage("Pennant", castle, MakePennant(), Color.white);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(60f, 34f);
            rt.anchoredPosition = CastleToCanvas(castle, spot);
            _flags.Add((rt, Random.Range(0f, 6.28f)));
        }

        private static Vector2 CastleToCanvas(RectTransform castle, Vector2 px) => SpriteToCanvas(castle, px, CastleW, CastleH);

        /// <summary>그림 칸 좌표를 그 그림 층(아래 가운데가 기준) 안의 화면 좌표로 바꾼다. 그림은 가로로 늘려 그려지므로 가로·세로 배율을 따로 계산한다.</summary>
        private static Vector2 SpriteToCanvas(RectTransform layer, Vector2 px, int spriteW, int spriteH)
        {
            float sx = layer.sizeDelta.x / spriteW, sy = layer.sizeDelta.y / spriteH;
            return new Vector2((px.x - spriteW * 0.5f) * sx, px.y * sy);
        }

        /// <summary>마을 굴뚝 위로 피어오르는 연기(번지는 원 셋이 시간차로 올라가며 커지고 옅어진다).</summary>
        private void SpawnSmoke(RectTransform village, Vector2 spot)
        {
            var origin = SpriteToCanvas(village, spot, VillageW, VillageH);
            for (int i = 0; i < 3; i++)
            {
                var img = NewImage("Smoke", village, _glow, new Color(0.88f, 0.76f, 0.82f, 0f));
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(50f, 50f); rt.anchoredPosition = origin;
                _smoke.Add((rt, img, origin, i / 3f + Random.Range(0f, 0.2f)));
            }
        }

        /// <summary>풍차 날개: 가로·세로 막대 두 개를 겹쳐 중심축 둘레로 천천히 돌린다.</summary>
        private void SpawnWindmill(RectTransform village, Vector2 spot)
        {
            var hub = NewRect("WindmillHub", village);
            hub.anchorMin = hub.anchorMax = new Vector2(0.5f, 0f); hub.pivot = new Vector2(0.5f, 0.5f);
            hub.sizeDelta = new Vector2(10f, 10f);
            hub.anchoredPosition = SpriteToCanvas(village, spot, VillageW, VillageH);
            Color wood = new Color(0.16f, 0.09f, 0.15f);
            foreach (bool horizontal in new[] { true, false })
            {
                var blade = NewImage("Blade", hub, null, wood);
                blade.raycastTarget = false;
                var r = blade.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = horizontal ? new Vector2(150f, 8f) : new Vector2(8f, 150f);
                // 날개 끝의 넓은 천
                var sail = NewImage("Sail", r, null, new Color(0.22f, 0.13f, 0.20f));
                sail.raycastTarget = false;
                var s = sail.rectTransform;
                s.anchorMin = s.anchorMax = new Vector2(0.5f, 0.5f);
                s.sizeDelta = horizontal ? new Vector2(60f, 24f) : new Vector2(24f, 60f);
                s.anchoredPosition = horizontal ? new Vector2(40f, 14f) : new Vector2(14f, 40f);
            }
            var cap = NewImage("HubCap", hub, _dot, wood);
            cap.raycastTarget = false;
            cap.rectTransform.sizeDelta = new Vector2(20f, 20f);
            _windmill = hub;
        }

        /// <summary>하늘을 지나는 용 실루엣: 몸통 하나와 앞뒤 날개 둘(날개는 어깨를 축으로 퍼덕인다). 평소에는 숨어 있다.</summary>
        private void SpawnDragon(RectTransform root)
        {
            Color silhouette = new Color(0.09f, 0.05f, 0.13f, 0.92f);
            _dragon = NewRect("Dragon", root);
            _dragon.anchorMin = _dragon.anchorMax = Vector2.zero; _dragon.pivot = new Vector2(0.5f, 0.5f);
            _dragon.sizeDelta = new Vector2(192f, 84f);
            var wingSprite = MakeWing();
            RectTransform Wing(string name, float alpha, Vector2 pos)
            {
                var img = NewImage(name, _dragon, wingSprite, new Color(silhouette.r, silhouette.g, silhouette.b, alpha));
                img.raycastTarget = false;
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.08f, 0.08f);
                r.sizeDelta = new Vector2(150f, 125f); r.anchoredPosition = pos;
                return r;
            }
            _dragonFar = Wing("WingFar", 0.7f, new Vector2(-24f, 12f));
            var body = NewImage("Body", _dragon, MakeDragonBody(), silhouette);
            body.raycastTarget = false;
            Stretch(body.rectTransform);
            _dragonNear = Wing("WingNear", 0.95f, new Vector2(-6f, 10f));
            _dragonNext = Time.unscaledTime + 5f;
            _dragon.gameObject.SetActive(false);
        }

        /// <summary>화면 양옆 돌벽돌 테두리에 달린 쇠 횃불: 쇠 받침, 세 겹으로 일렁이는 불꽃, 주변을 물들이는 불빛.</summary>
        private void SpawnWallTorch(RectTransform root, float side)
        {
            Vector2 p = new Vector2(side * 900f, -70f);
            Color iron = new Color(0.10f, 0.08f, 0.12f), ironHi = new Color(0.32f, 0.30f, 0.36f);
            void Bar(string name, Vector2 size, Vector2 pos, Color color)
            {
                var img = NewImage(name, root, null, color);
                img.raycastTarget = false;
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = size; r.anchoredPosition = pos;
            }
            var glow = NewImage("TorchLight", root, _glow, new Color(1f, 0.60f, 0.25f, 0.6f));
            glow.raycastTarget = false;
            var gr = glow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f); gr.sizeDelta = new Vector2(520f, 520f); gr.anchoredPosition = p + new Vector2(0f, 80f);
            _torches.Add((gr, glow, Random.Range(0f, 6.28f)));
            Bar("TorchStem", new Vector2(12f, 100f), p + new Vector2(0f, -18f), iron);
            Bar("TorchStemHi", new Vector2(3f, 94f), p + new Vector2(-side * 3f, -18f), ironHi);
            Bar("TorchCup", new Vector2(44f, 16f), p + new Vector2(0f, 36f), iron);
            Bar("TorchRim", new Vector2(54f, 5f), p + new Vector2(0f, 45f), ironHi);
            var flame = MakeFlame();
            var layers = new[]
            {
                (color: new Color(1f, 0.40f, 0.10f, 0.92f), size: new Vector2(56f, 96f)),
                (color: new Color(1f, 0.68f, 0.18f, 0.95f), size: new Vector2(40f, 72f)),
                (color: new Color(1f, 0.95f, 0.72f, 1f), size: new Vector2(22f, 46f)),
            };
            float phase = Random.Range(0f, 6.28f);
            foreach (var layer in layers)
            {
                var img = NewImage("Flame", root, flame, layer.color);
                img.raycastTarget = false;
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0f);
                r.sizeDelta = layer.size; r.anchoredPosition = p + new Vector2(0f, 46f);
                _flames.Add((r, phase));
                phase += 0.7f;
            }
        }

        /// <summary>로고 둘레에서 깜빡이는 별빛.</summary>
        private void SpawnSparkles(RectTransform root, Vector2 center)
        {
            var sprite = MakeSparkle();
            for (int i = 0; i < 12; i++)
            {
                var img = NewImage("Sparkle", root, sprite, new Color(1f, 0.94f, 0.72f, 0f));
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                float size = Random.Range(24f, 56f);
                rt.sizeDelta = new Vector2(size, size);
                float ang = Random.Range(0f, 6.28f), rad = Mathf.Sqrt(Random.value);
                rt.anchoredPosition = center + new Vector2(Mathf.Cos(ang) * rad * 540f, Mathf.Sin(ang) * rad * 230f);
                _sparkles.Add((rt, img, Random.Range(0f, 6.28f), Random.Range(0.6f, 1.6f)));
            }
        }

        private void SpawnBird(RectTransform root, int i)
        {
            var img = NewImage("Bird", root, MakeBird(), new Color(0.10f, 0.06f, 0.14f, 0.9f));
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
            float scale = Random.Range(0.8f, 1.4f);
            rt.sizeDelta = new Vector2(46f * scale, 22f * scale);
            float y = Random.Range(640f, 980f);
            rt.anchoredPosition = new Vector2(Random.Range(0f, 1900f), y);
            _birds.Add((rt, Random.Range(0f, 6.28f), 0f, y, Random.Range(22f, 40f)));
        }

        private void AnimateScenery(float t, float dt)
        {
            for (int i = 0; i < _birds.Count; i++)
            {
                var b = _birds[i];
                var p = b.rt.anchoredPosition;
                p.x += b.speed * dt;
                if (p.x > 2000f) p.x = -60f;
                p.y = b.baseY + Mathf.Sin(t * 0.7f + b.phase) * 16f;
                b.rt.anchoredPosition = p;
                b.rt.localScale = new Vector3(1f, 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 5.5f + b.phase)), 1f);
            }
            for (int i = 0; i < _smoke.Count; i++)
            {
                var s = _smoke[i];
                float k = Mathf.Repeat(t * 0.22f + s.phase, 1f);
                s.rt.anchoredPosition = s.origin + new Vector2(Mathf.Sin(k * 5f + s.phase * 6f) * 10f + k * 28f, k * 150f);
                s.rt.localScale = Vector3.one * (0.5f + k * 1.7f);
                s.img.color = new Color(0.88f, 0.76f, 0.82f, Mathf.Sin(k * Mathf.PI) * 0.30f);
            }
            if (_windmill != null) _windmill.localRotation = Quaternion.Euler(0f, 0f, -t * 22f);
            if (_rays != null) _rays.localRotation = Quaternion.Euler(0f, 0f, t * 2.2f);

            // 하늘을 가로지르는 용: 한참 만에 한 번 나타나 천천히 날아간다
            if (_dragon != null)
            {
                if (!_dragon.gameObject.activeSelf)
                {
                    if (t >= _dragonNext) { _dragon.gameObject.SetActive(true); _dragon.anchoredPosition = new Vector2(-260f, 780f); }
                }
                else
                {
                    var p = _dragon.anchoredPosition;
                    p.x += 85f * dt;
                    p.y = 780f + Mathf.Sin(t * 0.7f) * 34f + p.x * 0.025f;
                    _dragon.anchoredPosition = p;
                    float flap = Mathf.Sin(t * 4.2f);
                    _dragonNear.localRotation = Quaternion.Euler(0f, 0f, 10f + flap * 30f);
                    _dragonFar.localRotation = Quaternion.Euler(0f, 0f, 4f + Mathf.Sin(t * 4.2f + 0.5f) * 30f);
                    _dragon.localRotation = Quaternion.Euler(0f, 0f, 3f + Mathf.Cos(t * 4.2f) * 2f);
                    if (p.x > 2250f) { _dragon.gameObject.SetActive(false); _dragonNext = t + Random.Range(16f, 30f); }
                }
            }

            // 벽 횃불의 불꽃
            for (int i = 0; i < _flames.Count; i++)
            {
                var f = _flames[i];
                float n = 0.88f + 0.18f * Mathf.Sin(t * 11f + f.phase) + 0.10f * Mathf.Sin(t * 17f + f.phase * 2f);
                f.rt.localScale = new Vector3(0.92f + 0.08f * Mathf.Sin(t * 7f + f.phase), n, 1f);
                f.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 5f + f.phase) * 5f);
            }

            // 로고 주변의 반짝임
            float gate = Mathf.Clamp01((t - _introStart - 0.9f) / 0.6f);
            for (int i = 0; i < _sparkles.Count; i++)
            {
                var s = _sparkles[i];
                float a = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * s.speed + s.phase)), 3f);
                s.img.color = new Color(1f, 0.94f, 0.72f, a * gate);
                s.rt.localScale = Vector3.one * (0.55f + 0.6f * a);
                s.rt.localRotation = Quaternion.Euler(0f, 0f, t * 18f + s.phase * 20f);
            }
        }

        private void AnimateCastle(float t)
        {
            for (int i = 0; i < _torches.Count; i++)
            {
                var o = _torches[i];
                float f = 0.75f + 0.25f * Mathf.Sin(t * 9f + o.phase) + 0.1f * Mathf.Sin(t * 23f + o.phase * 2f);
                o.img.color = new Color(1f, 0.62f, 0.25f, 0.62f * f);
                o.rt.localScale = Vector3.one * (0.9f + 0.18f * f);
            }
            for (int i = 0; i < _flags.Count; i++)
            {
                var f = _flags[i];
                float w = Mathf.Sin(t * 4f + f.phase);
                f.rt.localScale = new Vector3(0.82f + 0.18f * w, 1f, 1f);
                f.rt.localRotation = Quaternion.Euler(0f, 0f, w * 4f);
            }
            for (int i = 0; i < _banners.Count; i++)
            {
                var b = _banners[i];
                b.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.1f + b.phase) * 1.8f);
            }
            if (_logoGlow != null) _logoGlow.color = new Color(1f, 0.70f, 0.38f, 0.34f + 0.08f * Mathf.Sin(t * 1.3f));
            if (_sun != null) _sun.localScale = Vector3.one * (1f + Mathf.Sin(t * 0.7f) * 0.025f);
        }

        /// <summary>위에서 늘어진 붉은 문장 깃발(끝이 제비꼬리 모양, 금빛 테두리와 방패 무늬).</summary>
        private void SpawnBanner(RectTransform root, float side)
        {
            var img = NewImage("Banner", root, MakeBanner(), Color.white);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(170f, 520f);
            rt.anchoredPosition = new Vector2(side * 790f, -30f);
            _banners.Add((rt, side > 0f ? 1.7f : 0f));
        }

        private void AnimateIntro(float t)
        {
            if (_logoRect == null) return;
            float since = t - _introStart;
            float k = Mathf.Clamp01((since - 0.1f) / 0.9f);
            float scale = k <= 0f ? 0.001f : EaseOutBack(k);
            float bob = k >= 1f ? Mathf.Sin(t * 1.4f) * 0.012f : 0f;
            _logoRect.localScale = Vector3.one * (scale + bob);
            var img = _logoRect.GetComponent<Image>();
            if (img != null) img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 3f));

            for (int i = 0; i < _intro.Count; i++)
            {
                var it = _intro[i];
                if (it.rt == null) continue;
                float kk = Mathf.Clamp01((since - it.delay) / 0.55f);
                float e = 1f - Mathf.Pow(1f - kk, 3f);
                it.group.alpha = e;
                it.rt.anchoredPosition = Vector2.Lerp(it.end + new Vector2(0f, -46f), it.end, e); // 아래에서 떠오른다
            }
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private void AnimateParallax(float dt)
        {
            var mouse = Mouse.current;
            Vector2 m = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 n = new Vector2(m.x / Mathf.Max(1f, Screen.width) - 0.5f, m.y / Mathf.Max(1f, Screen.height) - 0.5f);
            float k = 1f - Mathf.Exp(-4f * dt);
            Move(_sun, new Vector2(-n.x * _parallax * 0.2f, 430f - n.y * _parallax * 0.15f), k);
            Move(_mountains, new Vector2(-n.x * _parallax * 0.3f, 120f - n.y * _parallax * 0.1f), k);
            Move(_hillsFar, new Vector2(-n.x * _parallax * 0.5f, -n.y * _parallax * 0.15f), k);
            Move(_forestFar, new Vector2(-n.x * _parallax * 0.7f, 10f - n.y * _parallax * 0.2f), k);
            Move(_castleNear, new Vector2(-n.x * _parallax * 0.9f, 20f - n.y * _parallax * 0.25f), k);
            Move(_village, new Vector2(-n.x * _parallax * 1.2f, -6f - n.y * _parallax * 0.3f), k);
            Move(_hillsNear, new Vector2(-n.x * _parallax * 1.5f, -30f - n.y * _parallax * 0.35f), k);
            Move(_forestNear, new Vector2(-n.x * _parallax * 1.9f, -40f - n.y * _parallax * 0.4f), k);
        }

        private static void Move(RectTransform rt, Vector2 target, float k)
        {
            if (rt != null) rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, k);
        }

        private void SpawnEmber(RectTransform root)
        {
            var img = NewImage("Ember", root, _dot, new Color(1f, 0.7f, 0.35f, 0f));
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
            float size = Random.Range(4f, 14f);
            rt.sizeDelta = new Vector2(size, size);
            float x = Random.Range(0f, 1920f);
            rt.anchoredPosition = new Vector2(x, Random.Range(0f, 1080f));
            _embers.Add((rt, Random.Range(28f, 76f), Random.Range(14f, 40f), Random.Range(0f, 6.28f), x, img));
        }

        private void AnimateEmbers(float t, float dt)
        {
            for (int i = 0; i < _embers.Count; i++)
            {
                var e = _embers[i];
                if (e.rt == null) continue;
                var p = e.rt.anchoredPosition;
                p.y += e.speed * dt;
                p.x = e.baseX + Mathf.Sin(t * 0.8f + e.phase) * e.sway;
                if (p.y > 1130f) p.y = -40f;
                e.rt.anchoredPosition = p;
                float life = Mathf.Clamp01(p.y / 1080f);
                float flicker = 0.7f + 0.3f * Mathf.Sin(t * 5f + e.phase * 3f);
                e.img.color = new Color(1f, Mathf.Lerp(0.78f, 0.35f, life), 0.3f, Mathf.Sin(life * Mathf.PI) * 0.6f * flicker);
            }
        }

        // ───────────── 도우미

        /// <summary>씬에 이벤트 시스템이 없으면 만든다. 이 씬에서만 쓰고 로비로 넘어가기 전에 지운다(두 개가 되면 입력이 꼬인다).</summary>
        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            _ownedEventSystem = new GameObject("TitleEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private TMP_FontAsset FindFont()
        {
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters("용사때문에레벨업", out _, true, true)) return font;
            return null;
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

        private TMP_Text NewText(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) tmp.font = _font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ───────────── 코드로 그리는 그림

        private static Sprite MakeSky()
        {
            const int h = 128;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color top = new Color(0.05f, 0.07f, 0.20f), mid = new Color(0.30f, 0.20f, 0.42f), low = new Color(0.78f, 0.38f, 0.42f), horizon = new Color(1f, 0.74f, 0.40f);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1); // 0 = 아래
                Color c = v < 0.30f ? Color.Lerp(horizon, low, v / 0.30f) : v < 0.58f ? Color.Lerp(low, mid, (v - 0.30f) / 0.28f) : Color.Lerp(mid, top, (v - 0.58f) / 0.42f);
                tex.SetPixel(0, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>물결치는 언덕 실루엣(두 파형을 겹쳐 만든다).</summary>
        private static Sprite MakeHills(int seed, Color color, float amp, float baseline)
        {
            const int w = 512, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var rng = new System.Random(seed);
            float p1 = (float)rng.NextDouble() * 6f, p2 = (float)rng.NextDouble() * 6f;
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                float top = baseline + amp * (0.55f * (0.5f + 0.5f * Mathf.Sin(u * 6.28f * 2f + p1)) + 0.45f * (0.5f + 0.5f * Mathf.Sin(u * 6.28f * 5f + p2)));
                int topY = Mathf.RoundToInt(Mathf.Clamp01(top) * h);
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, y <= topY ? color : Color.clear);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        }

        /// <summary>
        /// 중세 돌성 실루엣(도트 느낌, 384x96). 성가퀴 달린 성벽 위로 원뿔 지붕 탑들이 서고, 가운데에는 가장 높은 본성이 있다.
        /// 불 켜진 창(windows)과 횃불·깃발 자리를 함께 돌려준다(그림 좌표).
        /// </summary>
        private static Sprite MakeCastle(int seed, Color color, bool windows, out List<Vector2> torches, out List<Vector2> flags)
        {
            const int w = CastleW, h = CastleH;
            var px = new Color32[w * h];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            Color32 fill = color, glow = new Color32(255, 200, 100, 255);
            Color32 dark = color * 0.55f;
            dark.a = 255;
            var rng = new System.Random(seed);
            var torchList = new List<Vector2>();
            var flagList = new List<Vector2>();

            void Box(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++) px[y * w + x] = c;
            }

            void Crenels(int x0, int x1, int y)
            {
                for (int x = x0; x < x1; x += 4) Box(x, y, Mathf.Min(x + 2, x1), y + 3, fill);
            }

            void Cone(int cx, int baseY, int halfBase, int roofH)
            {
                for (int r = 0; r < roofH; r++)
                {
                    float k = 1f - r / (float)roofH;
                    int half = Mathf.Max(1, Mathf.RoundToInt(k * halfBase));
                    Box(cx - half, baseY + r, cx + half, baseY + r + 1, fill);
                }
            }

            void Tower(int x0, int tw, int th, bool pole)
            {
                Box(x0, 0, x0 + tw, th, fill);
                Box(x0 - 2, th - 2, x0 + tw + 2, th + 1, fill); // 처마
                int roofH = Mathf.RoundToInt(tw * 1.1f);
                Cone(x0 + tw / 2, th + 1, tw / 2 + 3, roofH);
                if (windows)
                {
                    Box(x0 + tw / 2 - 1, th - 14, x0 + tw / 2 + 1, th - 9, glow);
                    if (th > 50) Box(x0 + tw / 2 - 1, th - 28, x0 + tw / 2 + 1, th - 23, glow);
                }
                if (pole)
                {
                    int top = th + 1 + roofH;
                    Box(x0 + tw / 2, top, x0 + tw / 2 + 1, top + 10, fill);
                    flagList.Add(new Vector2(x0 + tw / 2 + 1, top + 8));
                }
            }

            // 성벽: 양쪽 끝까지 이어지는 낮은 성벽 + 성가퀴 + 총안(어두운 틈)
            Box(0, 0, w, 26, fill);
            Crenels(0, w, 26);
            for (int x = 6; x < w - 6; x += 14) Box(x, 14, x + 2, 21, dark);

            // 가운데 본성과 곁탑
            int mid = w / 2;
            Box(mid - 24, 0, mid + 24, 62, fill);
            Crenels(mid - 24, mid + 24, 62);
            Tower(mid - 38, 16, 70, true);
            Tower(mid + 22, 16, 70, true);
            Tower(mid - 10, 20, 82, true);
            if (windows)
            {
                Box(mid - 6, 38, mid - 3, 46, glow); Box(mid + 3, 38, mid + 6, 46, glow);
                torchList.Add(new Vector2(mid - 14, 52)); torchList.Add(new Vector2(mid + 14, 52));
            }
            // 정문(아치)
            Box(mid - 7, 0, mid + 7, 18, dark);
            Box(mid - 5, 18, mid + 5, 21, dark);
            if (windows)
            {
                Box(mid - 5, 0, mid + 5, 14, new Color32(255, 170, 80, 255));
                torchList.Add(new Vector2(mid - 10, 22)); torchList.Add(new Vector2(mid + 10, 22));
            }

            // 좌우의 탑들: 간격을 두고 높이를 달리 세운다
            for (int side = -1; side <= 1; side += 2)
            {
                int x = mid + side * 70;
                while (x > 12 && x < w - 28)
                {
                    int tw = rng.Next(14, 22), th = rng.Next(40, 66);
                    int x0 = side < 0 ? x - tw : x;
                    Tower(x0, tw, th, rng.Next(0, 2) == 0);
                    if (windows && rng.Next(0, 2) == 0) torchList.Add(new Vector2(x0 + (side < 0 ? tw + 3 : -3), 28));
                    x += side * (tw + rng.Next(14, 34));
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            tex.SetPixels32(px);
            tex.Apply();
            torches = torchList;
            flags = flagList;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        }

        /// <summary>오른쪽으로 펄럭이는 삼각 깃발(붉은색, 끝이 갈라짐).</summary>
        private static Sprite MakePennant()
        {
            const int w = 64, h = 36;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float k = x / (float)w;
                    float half = (h * 0.5f) * (1f - k * 0.55f);
                    bool inside = Mathf.Abs(y + 0.5f - h * 0.5f) < half;
                    // 끝의 제비꼬리 홈
                    if (x > w - 14 && Mathf.Abs(y + 0.5f - h * 0.5f) < (x - (w - 14)) * 0.6f) inside = false;
                    tex.SetPixel(x, y, inside ? Color.Lerp(new Color(0.92f, 0.25f, 0.25f), new Color(0.62f, 0.12f, 0.18f), k) : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 100f);
        }

        /// <summary>
        /// 세로로 늘어진 문장 깃발: 붉은 천, 금빛 테두리, 가운데 방패 안에 마왕의 문장(뿔 달린 해골과 왕관), 아래에 금빛 갈매기 줄, 끝은 제비꼬리.
        /// 85x260 칸 기준으로 계산하고 2배 해상도로 그린다.
        /// </summary>
        private static Sprite MakeBanner()
        {
            const int S = 2, uw = 85, uh = 260, w = uw * S, h = uh * S;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color cloth = new Color(0.62f, 0.10f, 0.16f), clothDark = new Color(0.40f, 0.06f, 0.11f), gold = new Color(0.96f, 0.76f, 0.38f), shieldFill = new Color(0.20f, 0.07f, 0.12f);
            float shieldTop = uh * 0.58f + 30f, shieldBottom = uh * 0.58f - 48f, centerX = uw * 0.5f, emblemY = uh * 0.58f - 4f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float ux = (x + 0.5f) / S, uy = (y + 0.5f) / S;
                    float dx = Mathf.Abs(ux - centerX);
                    // 아래쪽 제비꼬리
                    if (uy < 34f && dx < (34f - uy) * 0.9f) { tex.SetPixel(x, y, Color.clear); continue; }
                    // 걸이 막대(맨 위)
                    if (uy >= uh - 12f) { tex.SetPixel(x, y, gold * Mathf.Lerp(0.75f, 1f, ux / uw)); continue; }
                    Color c = Color.Lerp(clothDark, cloth, Mathf.Clamp01(1f - dx / (uw * 0.5f) * 0.6f));
                    // 금빛 테두리
                    if (Mathf.Min(ux, uw - ux) < 4f) c = gold;
                    // 방패 아래 금빛 갈매기 두 줄
                    if (dx < 28f && (Mathf.Abs(uy - (90f - dx * 0.55f)) < 2.2f || Mathf.Abs(uy - (76f - dx * 0.55f)) < 2.2f)) c = gold;
                    // 방패 위 작은 금빛 마름모 셋
                    if (Mathf.Abs(ux - centerX) + Mathf.Abs(uy - 212f) < 6f || Mathf.Abs(dx - 15f) + Mathf.Abs(uy - 212f) < 3f) c = gold;
                    // 가운데 방패: 위는 평평, 아래는 뾰족
                    float sdy = uy - uh * 0.58f;
                    float shieldHalf = sdy >= 0f ? 30f : 30f * (1f + sdy / 48f);
                    bool shield = uy > shieldBottom && uy < shieldTop && dx < shieldHalf;
                    if (shield)
                    {
                        bool shieldEdge = dx > shieldHalf - 3f || uy > shieldTop - 3f;
                        c = shieldEdge ? gold : shieldFill;
                        if (!shieldEdge && DemonEmblem(ux - centerX, uy - emblemY, out var emblem)) c = emblem;
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 200f);
        }

        /// <summary>
        /// 마왕의 문장(가운데가 원점): 큼직하게 휘어 올라간 두 뿔, 세 갈래 왕관, 해골 얼굴, 붉게 타는 눈과 이빨.
        /// 점이 문장에 속하면 색을 돌려준다.
        /// </summary>
        private static bool DemonEmblem(float x, float y, out Color color)
        {
            Color gold = new Color(0.98f, 0.80f, 0.42f) * Mathf.Lerp(0.85f, 1.05f, Mathf.Clamp01((y + 14f) / 40f));
            Color dark = new Color(0.20f, 0.07f, 0.12f), eye = new Color(1f, 0.28f, 0.20f);
            gold.a = 1f;
            color = gold;

            // 두 뿔: 머리 옆에서 시작해 바깥으로 휘어 위로 솟는 호(끝으로 갈수록 가늘어진다)
            float ax = Mathf.Abs(x);
            Vector2 hornCenter = new Vector2(11f, 22f);
            Vector2 v = new Vector2(ax - hornCenter.x, y - hornCenter.y);
            float dist = v.magnitude, a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; // 아래(-90°)에서 시작해 바깥(0°)을 지나 위(+20°)에서 끝난다
            if (a >= -90f && a <= 20f && Mathf.Abs(dist - 14f) < Mathf.Lerp(3.6f, 0.6f, (a + 90f) / 110f)) return true;

            // 왕관: 띠와 세 갈래 뾰족 끝, 가운데 붉은 보석
            if (ax < 7f && y > 9.5f && y < 13f) { if (ax < 1.6f && y > 10.5f && y < 12.5f) color = eye; return true; }
            float[] spikeX = { -5f, 0f, 5f };
            float[] spikeTop = { 19f, 23f, 19f };
            for (int i = 0; i < 3; i++)
                if (y >= 13f && y < spikeTop[i] && Mathf.Abs(x - spikeX[i]) < (spikeTop[i] - y) / (spikeTop[i] - 13f) * 2.6f) return true;

            // 해골 얼굴
            float fx = x / 11f, fy = y / 12f;
            if (fx * fx + fy * fy < 1f)
            {
                // 눈구멍(어둡게) + 붉은 불꽃
                for (int s = -1; s <= 1; s += 2)
                {
                    float ex = (x - s * 5f) / 3.4f, ey = (y - 2f) / 2.6f;
                    if (ex * ex + ey * ey < 1f) { color = (x - s * 5f) * (x - s * 5f) + (y - 2f) * (y - 2f) < 3f ? eye : dark; return true; }
                }
                // 코
                if (y < -1f && y > -5f && ax < (y + 5f) * 0.5f) { color = dark; return true; }
                // 이빨: 입 선과 세로 틈
                if (y < -6f && y > -10.5f && ax < 7f)
                {
                    if (y > -7.2f || Mathf.Repeat(x + 7f, 3f) < 0.8f) { color = dark; return true; }
                }
                return true;
            }
            return false;
        }

        /// <summary>눈 덮인 산맥(512x160): 뾰족한 능선, 노을이 닿은 윗선, 봉우리의 눈.</summary>
        private static Sprite MakeMountains(int seed)
        {
            const int w = 512, h = 160;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color topColor = new Color(0.42f, 0.30f, 0.54f), bottomColor = new Color(0.30f, 0.20f, 0.42f);
            Color rim = new Color(1f, 0.72f, 0.56f), snow = new Color(0.94f, 0.80f, 0.88f);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                float r1 = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(u * 2.4f + seed, 0.5f) - 1f);
                float r2 = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(u * 6.5f + seed * 2f, 3.5f) - 1f);
                float top = 0.28f + 0.64f * (0.65f * r1 + 0.35f * r2);
                int topY = Mathf.Clamp(Mathf.RoundToInt(top * h), 1, h - 1);
                float snowDepth = 24f * (0.4f + 0.6f * Mathf.PerlinNoise(x * 0.09f, seed));
                for (int y = 0; y < h; y++)
                {
                    if (y > topY) { tex.SetPixel(x, y, Color.clear); continue; }
                    Color c = Color.Lerp(bottomColor, topColor, y / (float)h);
                    if (topY > h * 0.58f && y > topY - snowDepth) c = Color.Lerp(c, snow, 0.75f - 0.35f * (topY - y) / snowDepth);
                    if (y > topY - 3) c = Color.Lerp(c, rim, 0.65f);
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        }

        /// <summary>소나무 숲 실루엣(512x128, 도트): 세 단으로 뾰족한 나무가 빽빽이 서 있다. gapFraction 만큼 가운데는 비워 성이 보이게 한다.</summary>
        private static Sprite MakeForest(int seed, Color color, int minH, int maxH, float gapFraction)
        {
            const int w = 512, h = 128, ground = 6;
            var px = new Color32[w * h];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            Color32 fill = color;
            var rng = new System.Random(seed);

            void Row(int y, int x0, int x1)
            {
                if (y < 0 || y >= h) return;
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++) px[y * w + x] = fill;
            }

            for (int y = 0; y < ground; y++) Row(y, 0, w);
            int cx = 3;
            while (cx < w - 3)
            {
                float u = cx / (float)w;
                if (gapFraction > 0f && Mathf.Abs(u - 0.5f) < gapFraction * 0.5f) { cx += 6; continue; }
                int th = rng.Next(minH, maxH + 1);
                float width = th * 0.38f;
                for (int r = 0; r < th; r++)
                {
                    float tier = r * 3f / th, local = tier - Mathf.Floor(tier);
                    float half = width * 0.5f * (1f - r / (float)th) * (0.6f + 0.4f * (1f - local));
                    Row(ground + r, cx - Mathf.RoundToInt(half), cx + Mathf.RoundToInt(half) + 1);
                }
                cx += rng.Next(4, 10);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        }

        /// <summary>
        /// 성 아래 마을(384x64, 도트): 가파른 지붕의 집들이 늘어서고, 굴뚝과 불 켜진 창이 있으며, 왼쪽에는 풍차 탑이 서 있다.
        /// 굴뚝 자리와 풍차 중심축 자리(그림 좌표)를 돌려준다. 가운데는 성문으로 가는 길이라 비워 둔다.
        /// </summary>
        private static Sprite MakeVillage(int seed, Color color, out List<Vector2> chimneys, out Vector2 windmillHub)
        {
            const int w = VillageW, h = VillageH, ground = 5, windX = 40;
            var px = new Color32[w * h];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            Color32 fill = color, glow = new Color32(255, 196, 100, 255);
            var rng = new System.Random(seed);
            var chimneyList = new List<Vector2>();

            void Box(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++) px[y * w + x] = c;
            }

            Box(0, 0, w, ground, fill);

            // 풍차 탑: 아래가 넓은 사다리꼴, 위에 원뿔 지붕
            const int millH = 28;
            for (int r = 0; r < millH; r++)
            {
                int half = Mathf.RoundToInt(Mathf.Lerp(8f, 5f, r / (float)millH));
                Box(windX - half, ground + r, windX + half, ground + r + 1, fill);
            }
            for (int r = 0; r < 8; r++)
            {
                int half = Mathf.Max(1, Mathf.RoundToInt(7f * (1f - r / 8f)));
                Box(windX - half, ground + millH + r, windX + half, ground + millH + r + 1, fill);
            }
            Box(windX - 1, ground + 8, windX + 1, ground + 12, glow);
            windmillHub = new Vector2(windX, ground + millH - 2);

            int x = 4;
            while (x < w - 24)
            {
                if (Mathf.Abs(x - windX) < 20) { x = windX + 20; continue; }
                if (x > 150 && x < 226) { x = 226; continue; } // 성문으로 가는 길
                int hw = rng.Next(12, 21), wallH = rng.Next(9, 15), roofH = rng.Next(9, 15);
                Box(x, ground - 1, x + hw, ground + wallH, fill);
                for (int r = 0; r < roofH; r++)
                {
                    float half = (hw * 0.5f + 2.5f) * (1f - r / (float)roofH);
                    Box(x + hw / 2 - Mathf.RoundToInt(half), ground + wallH + r, x + hw / 2 + Mathf.RoundToInt(half), ground + wallH + r + 1, fill);
                }
                int windows = rng.Next(1, 3);
                for (int i = 0; i < windows; i++)
                    if (rng.Next(0, 4) != 0) Box(x + 3 + i * (hw - 8) , ground + wallH / 2 - 1, x + 5 + i * (hw - 8), ground + wallH / 2 + 3, glow);
                if (rng.Next(0, 2) == 0)
                {
                    int chx = x + hw - 4;
                    int chTop = ground + wallH + roofH / 2 + 3;
                    Box(chx, ground + wallH, chx + 3, chTop, fill);
                    chimneyList.Add(new Vector2(chx + 1.5f, chTop + 1));
                }
                x += hw + rng.Next(3, 10);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            tex.SetPixels32(px);
            tex.Apply();
            chimneys = chimneyList;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        }

        /// <summary>날갯짓하는 새(24x12): 가운데가 낮고 양 끝이 올라간 V 자.</summary>
        private static Sprite MakeBird()
        {
            const int w = 24, h = 12;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float line = 2.5f + Mathf.Abs(x - 11.5f) * 0.55f;
                    float d = Mathf.Abs(y + 0.5f - line);
                    float thick = Mathf.Lerp(2.0f, 0.9f, Mathf.Abs(x - 11.5f) / 11.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(thick - d)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool InPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        private static Sprite PaintPolygons(int w, int h, params Vector2[][] polys)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var offsets = new[] { new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.25f), new Vector2(0.25f, 0.75f), new Vector2(0.75f, 0.75f) };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 네 점을 찍어 가장자리를 부드럽게
                    float cover = 0f;
                    foreach (var o in offsets)
                        foreach (var poly in polys)
                            if (InPolygon(new Vector2(x + o.x, y + o.y), poly)) { cover += 0.25f; break; }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, cover));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>용의 몸통(64x28, 오른쪽을 보고 있다): 긴 꼬리 끝의 창날, 어깨, 목, 뿔 달린 머리.</summary>
        private static Sprite MakeDragonBody()
        {
            var body = new[]
            {
                new Vector2(1f, 11f), new Vector2(10f, 13f), new Vector2(20f, 18f), new Vector2(32f, 20f), new Vector2(42f, 22f), new Vector2(48f, 25f),
                new Vector2(52f, 26f), new Vector2(56f, 22f), new Vector2(62f, 21f), new Vector2(62f, 18f), new Vector2(55f, 17f), new Vector2(49f, 14f),
                new Vector2(42f, 11f), new Vector2(32f, 9f), new Vector2(20f, 9f), new Vector2(10f, 10f),
            };
            var tail = new[] { new Vector2(0f, 11f), new Vector2(5f, 15f), new Vector2(5f, 7f) };
            var horn = new[] { new Vector2(50f, 25f), new Vector2(44f, 27.5f), new Vector2(53f, 26f) };
            return PaintPolygons(64, 28, body, tail, horn);
        }

        /// <summary>용의 날개(48x40): 어깨(왼쪽 아래)에서 뻗는 박쥐 날개, 가장자리는 뼈마디 사이가 오목하다.</summary>
        private static Sprite MakeWing()
        {
            var wing = new[]
            {
                new Vector2(3f, 3f), new Vector2(5f, 20f), new Vector2(12f, 38f), new Vector2(16f, 27f), new Vector2(24f, 39f), new Vector2(29f, 26f),
                new Vector2(38f, 34f), new Vector2(40f, 21f), new Vector2(46f, 18f), new Vector2(44f, 10f), new Vector2(26f, 5f),
            };
            return PaintPolygons(48, 40, wing);
        }

        /// <summary>햇살 한 줄기(32x128): 해 쪽(아래)은 가늘고 멀어질수록 넓어지며 옅어진다.</summary>
        private static Sprite MakeRay()
        {
            const int w = 32, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)h;
                float half = 0.10f + 0.40f * v;
                float fall = Mathf.Pow(1f - v, 1.5f);
                for (int x = 0; x < w; x++)
                {
                    float ux = Mathf.Abs((x + 0.5f) / w - 0.5f);
                    float edge = Mathf.Clamp01(1f - ux / half);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, edge * edge * fall));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        }

        /// <summary>불꽃 한 겹(32x48): 아래는 둥글고 위는 뾰족한 물방울.</summary>
        private static Sprite MakeFlame()
        {
            const int w = 32, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            {
                float u = (y + 0.5f) / h;
                float half = u < 0.35f ? 0.48f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((0.35f - u) / 0.35f, 2f))) : 0.48f * Mathf.Pow(Mathf.Max(0f, 1f - (u - 0.35f) / 0.65f), 1.3f);
                for (int x = 0; x < w; x++)
                {
                    float d = half * w - Mathf.Abs(x + 0.5f - w * 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(d * 1.4f)));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        }

        /// <summary>네 갈래 별빛(32x32).</summary>
        private static Sprite MakeSparkle()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float ux = Mathf.Abs((x + 0.5f) / n * 2f - 1f), uy = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                    float v = Mathf.Pow(ux, 0.55f) + Mathf.Pow(uy, 0.55f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - v), 1.4f) + 0.5f * Mathf.Clamp01(1f - Mathf.Sqrt(ux * ux + uy * uy) * 3.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float Hash(int a, int b)
        {
            unchecked
            {
                int h = a * 374761393 + b * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>둥근 사각형(64x64, 9분할). shaded 면 위아래 가장자리만 살짝 밝고 어두워져 입체감이 난다(가운데는 일정해서 늘려도 띠가 생기지 않는다).</summary>
        private static Sprite MakeRound(bool shaded)
        {
            const int n = 64, radius = 22;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
            {
                float v = 1f;
                if (shaded) v = y < 24 ? Mathf.Lerp(0.80f, 0.90f, y / 24f) : y < 40 ? 0.90f : Mathf.Lerp(0.90f, 1f, (y - 40) / 24f);
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (n - radius), 0f);
                    float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (n - radius), 0f);
                    float dist = radius - Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(v, v, v, Mathf.Clamp01(dist + 0.5f)));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
        }

        /// <summary>둥근 사각형의 가는 테두리(9분할).</summary>
        private static Sprite MakeRoundRing(float thickness)
        {
            const int n = 64, radius = 22;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (n - radius), 0f);
                    float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (n - radius), 0f);
                    float dist = radius - Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(dist + 0.5f) - Mathf.Clamp01(dist - thickness + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
        }

        /// <summary>단추 오른쪽에 미끄러져 나오는 화살표(›).</summary>
        private static Sprite MakeChevron()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Vector2 a = new Vector2(10f, 7f), b = new Vector2(21f, 16f), c = new Vector2(10f, 25f);
            float Seg(Vector2 p, Vector2 s, Vector2 e)
            {
                Vector2 d = e - s;
                float t = Mathf.Clamp01(Vector2.Dot(p - s, d) / d.sqrMagnitude);
                return Vector2.Distance(p, s + d * t);
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(Seg(p, a, b), Seg(p, b, c));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(2.2f - d)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>돌벽돌 타일(64x32): 엇갈려 쌓은 두 줄, 회반죽 틈, 벽돌마다 다른 색.</summary>
        private static Sprite MakeBrick()
        {
            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            Color mortar = new Color(0.07f, 0.05f, 0.07f), stone = new Color(0.30f, 0.26f, 0.29f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int row = y / 16, yy = y % 16;
                    int xs = (x + (row == 1 ? 16 : 0)) % w;
                    int bx = xs % 32, bi = xs / 32;
                    Color c;
                    if (yy < 2 || bx < 2) c = mortar;
                    else
                    {
                        float v = 0.82f + 0.36f * Hash(row * 2 + bi, 7);
                        c = stone * v * (0.96f + 0.08f * Hash(x, y));
                        if (yy == 15) c = Color.Lerp(c, Color.white, 0.12f); // 윗면 하이라이트
                        if (yy == 2) c *= 0.8f;
                    }
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite MakeDot()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((r - 1f - d) * 0.9f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeGlow(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2.0f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeVignette()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / (r * 1.25f);
                    tex.SetPixel(x, y, new Color(0.04f, 0.02f, 0.06f, Mathf.SmoothStep(0f, 0.65f, Mathf.Clamp01(d - 0.35f) / 0.65f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>타이틀 단추의 반응: 올리면 살짝 커지고 바탕·테두리·글자색이 바뀌며 빛이 번지고 화살표가 미끄러져 나온다. 으뜸 단추는 평소에도 숨 쉬듯 빛난다.</summary>
    internal sealed class TitleButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Image _body, _ring, _glow, _chevron;
        private TMP_Text _text;
        private Color _fill, _hoverFill, _ringColor, _hoverRing, _textColor, _hoverText, _glowColor;
        private Vector2 _chevronBase;
        private bool _primary, _hover, _down;
        private float _scale = 1f, _k;

        public void Setup(Image body, Image ring, Image glow, Image chevron, TMP_Text text,
            Color fill, Color hoverFill, Color ringColor, Color hoverRing, Color textColor, Color hoverText, Color glowColor, bool primary)
        {
            _body = body; _ring = ring; _glow = glow; _chevron = chevron; _text = text;
            _fill = fill; _hoverFill = hoverFill; _ringColor = ringColor; _hoverRing = hoverRing;
            _textColor = textColor; _hoverText = hoverText; _glowColor = glowColor; _primary = primary;
            if (chevron != null) _chevronBase = chevron.rectTransform.anchoredPosition;
            Apply(0f, Time.unscaledTime);
        }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        private void OnDisable() { _hover = false; _down = false; _k = 0f; _scale = 1f; transform.localScale = Vector3.one; }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _scale = Mathf.Lerp(_scale, _down ? 0.97f : _hover ? 1.04f : 1f, 1f - Mathf.Exp(-16f * dt));
            transform.localScale = Vector3.one * _scale;
            _k = Mathf.Lerp(_k, _hover ? 1f : 0f, 1f - Mathf.Exp(-12f * dt));
            Apply(_k, Time.unscaledTime);
        }

        private void Apply(float k, float t)
        {
            _body.color = Color.Lerp(_fill, _hoverFill, k);
            if (_ring != null) _ring.color = Color.Lerp(_ringColor, _hoverRing, k);
            var text = Color.Lerp(_textColor, _hoverText, k);
            _text.color = text;
            float pulse = _primary ? 0.55f + 0.25f * Mathf.Sin(t * 2.2f) : 0f;
            _glow.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, _glowColor.a * Mathf.Max(pulse, k));
            if (_chevron != null)
            {
                _chevron.color = new Color(text.r, text.g, text.b, k * 0.95f);
                _chevron.rectTransform.anchoredPosition = _chevronBase + new Vector2(-(1f - k) * 16f + Mathf.Sin(t * 7f) * 2f * k, 0f);
            }
        }
    }
}
