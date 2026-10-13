using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OZGL2.Augment;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.Tutorial;
using OZGL2.UIBridge;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 실제 표시 메서드를 임시 Edit Mode 씬에서 호출해 보조 UI를 검사합니다.
/// 계정 저장·보상 확정·업적 알림 처리·스토리/튜토리얼 진행은 호출하지 않습니다.
/// 메뉴는 Scene/Prefab을 저장하지 않으며 Undo 대상 변경을 만들지 않습니다.
/// </summary>
public static class HeraldrySupplementPreviewValidator
{
    private const string OUTPUT = "Builds/HeraldrySupplement_20261013/Preview";
    private const string GAMEPLAY = "Builds/TrailerReview/capture_20261013_161824/frames/frame_01550.jpg";
    private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const int PREVIEW_LAYER = 31;
    private static readonly List<Behaviour> OWNERS = new List<Behaviour>();
    private static readonly List<Object> TEMPORARIES = new List<Object>();
    private static readonly List<object> REPORTS = new List<object>();
    private static Camera _camera;
    private static Canvas _backgroundCanvas;
    private static RawImage _background;
    private static Texture2D _gameplayTexture;
    private static Texture2D _kingTexture;
    private static Vector2 _layoutResolution;

    [MenuItem("Tools/OZGL2/Shared UI/Validate Heraldry Supplement In Temporary Scene")]
    public static void ValidateFromMenu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 실행해 주세요.");
        Scene original = SceneManager.GetActiveScene();
        if (!original.IsValid() || original.isDirty)
            throw new InvalidOperationException("현재 씬의 미저장 변경을 먼저 정리해 주세요. 검증 도구는 원본 씬을 저장하지 않습니다.");
        if (UIHeraldrySupplement.Style == null)
            throw new InvalidOperationException("Build Heraldry Supplement Art 메뉴로 공통 스타일을 먼저 생성해 주세요.");

        string originalPath = original.path;
        int originalSceneCount = SceneManager.sceneCount;
        var originalRandom = UnityEngine.Random.state;
        _layoutResolution = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        Scene test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        OWNERS.Clear(); TEMPORARIES.Clear(); REPORTS.Clear();
        string failure = null;
        bool restored = false;
        try
        {
            SceneManager.SetActiveScene(test);
            Directory.CreateDirectory(OUTPUT);
            _camera = new GameObject("HeraldryPreviewCamera", typeof(Camera)).GetComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color32(26, 24, 29, 255);
            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.cullingMask = 1 << PREVIEW_LAYER;
            _backgroundCanvas = NewCanvas("GameplayContext", null, -2000, false);
            _background = NewRaw(_backgroundCanvas.transform, "RecordedGameplay");
            Stretch(_background.rectTransform);
            _gameplayTexture = LoadTemporaryTexture(GAMEPLAY);
            _background.texture = _gameplayTexture;
            _background.color = _gameplayTexture != null ? Color.white : new Color(0.12f, 0.1f, 0.14f);
            _kingTexture = RenderStaticKing();
            var style = UIHeraldrySupplement.Style;
            REPORTS.Add(new
            {
                name = "SharedStyle", frame = style.FrameSprite != null, crest = style.CrestSprite != null,
                nameplate = style.NameplateSprite != null, medallion = style.MedallionSprite != null,
                tail = style.TailSprite != null, darkButton = style.DarkButtonSprite != null,
                panel = style.PanelSprite != null, dialogue = style.DialogueSprite != null,
                resultPanel = style.ResultPanelSprite != null, resultTrack = style.ResultTrackSprite != null,
                resultFill = style.ResultFillSprite != null,
                redButton = style.RedButtonSprite != null, titleFont = style.TitleFont != null,
                bodyFont = style.BodyFont != null, numberFont = style.NumberFont != null,
                staticKingRendered = _kingTexture != null, gameplayContextLoaded = _gameplayTexture != null
            });

            RunCase("SkillTooltip", PreviewSkills);
            RunCase("AugmentTooltip", PreviewAugments);
            RunCase("AchievementToast", PreviewAchievements);
            RunCase("StorageDiscard12", () => PreviewStorage(12));
            RunCase("StorageDiscard20", () => PreviewStorage(20));
            RunCase("StoryDialogue", PreviewStory);
            RunCase("TutorialBubble", PreviewTutorial);
            RunCase("CombatNumbers", PreviewNumbers);
        }
        catch (Exception ex)
        {
            failure = ex.ToString();
        }
        finally
        {
            foreach (Behaviour owner in OWNERS)
            {
                if (owner == null) continue;
                CollectTemporaryResources(owner);
                SetIfPresent(owner, "_canvas", null);
                SetIfPresent(owner, "_helpCanvas", null);
                if (owner is InGameBattleFx)
                {
                    SetIfPresent(owner, "_root", null);
                    SetIfPresent(owner, "_numberMaterial", null);
                    SetIfPresent(owner, "_numberShadowMaterial", null);
                }
            }
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            if (test.IsValid()) EditorSceneManager.CloseScene(test, true);
            foreach (Object temporary in TEMPORARIES.Distinct().Reverse())
                if (temporary != null && !AssetDatabase.Contains(temporary)) Object.DestroyImmediate(temporary);
            UnityEngine.Random.state = originalRandom;
            restored = original.IsValid() && !original.isDirty && SceneManager.GetActiveScene().path == originalPath && SceneManager.sceneCount == originalSceneCount;
            _camera = null; _backgroundCanvas = null; _background = null; _gameplayTexture = null; _kingTexture = null;
        }

        var summary = BuildSummary(restored, failure);
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            mode = "EditModeActualDisplayMethods",
            layoutScreen = new { width = _layoutResolution.x, height = _layoutResolution.y },
            note = "1920×1080와 1280×720 PNG는 현재 Game View에서 계산된 배치의 렌더 크기입니다. 실제 해상도별 배치 검증은 Game View 크기를 바꾸고 다시 실행해야 합니다.",
            originalScene = originalPath,
            originalSceneRestored = restored,
            savedSceneOrPrefab = false,
            gameProgressInvoked = false,
            fatalError = failure,
            summary,
            checks = REPORTS
        }, Newtonsoft.Json.Formatting.Indented);
        Directory.CreateDirectory(OUTPUT);
        File.WriteAllText(OUTPUT + "/validation.json", json);
        if (!restored) Debug.LogError("보조 UI 검증 뒤 원본 Editor 상태가 달라졌습니다. 자동 저장·덮어쓰기를 하지 않았습니다.");
        return json;
    }

    private static void RunCase(string name, Action action)
    {
        try { action(); }
        catch (Exception ex) { REPORTS.Add(new { name, failed = true, error = ex.ToString() }); }
        finally
        {
            foreach (Behaviour owner in OWNERS)
                if (owner != null) owner.gameObject.SetActive(false);
            if (_background != null) _background.texture = _gameplayTexture;
        }
    }

    private static void PreviewSkills()
    {
        var catalog = Require<UISkillPreviewCatalogSO>("Assets/06.UI/LobbyMutedPreview/Skills_v2/SkillRosterCatalog.asset");
        var entry = catalog.Entries.First(e => e.DisplayName == "화염구");
        var owner = NewOwner<InGameSkillTooltip>("SkillTooltip_Preview");
        Invoke(owner, "Build");
        Invoke(owner, "Fill", entry);
        Rect slot = new Rect(_layoutResolution.x * 0.12f, _layoutResolution.y * 0.12f, _layoutResolution.x * 0.075f, _layoutResolution.y * 0.14f);
        Invoke(owner, "Place", slot);
        CaptureBoth("02_SkillTooltip", Get<Canvas>(owner, "_canvas"), Get<RectTransform>(owner, "_panel"));

        var longer = JsonUtility.FromJson<UISkillPreviewCatalogSO.Entry>(JsonUtility.ToJson(entry));
        Set(longer, "_displayName", "긴 스킬 이름과 여러 줄 제목 표시 확인");
        Set(longer, "_description", entry.Description + " 이 문장은 검증용이며 실제 스킬을 변경하지 않습니다. 대상 범위와 지속 효과를 여러 줄로 안내하는 상황에서도 본문이 프레임 안에 들어오는지 확인합니다.");
        Invoke(owner, "Fill", longer);
        Invoke(owner, "Place", slot);
        CaptureBoth("02_SkillTooltip_Long", Get<Canvas>(owner, "_canvas"), Get<RectTransform>(owner, "_panel"));
    }

    private static void PreviewAugments()
    {
        AugmentData source = AssetDatabase.FindAssets("t:AugmentData").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<AugmentData>).First(a => a != null);
        var data = Object.Instantiate(source);
        TEMPORARIES.Add(data);
        data.displayName = "처형 재충전";
        data.description = "용사 처치 시 모든 스킬 쿨탐 -0.3초";
        data.tier = 2;
        var owner = NewOwner<InGameAugmentHud>("AugmentTooltip_Preview");
        Invoke(owner, "EnsureCanvas");
        var canvas = Get<Canvas>(owner, "_canvas");
        var cell = new GameObject("PreviewHoverCell", typeof(RectTransform)).GetComponent<RectTransform>();
        cell.SetParent(canvas.transform, false);
        cell.anchorMin = cell.anchorMax = Vector2.zero;
        cell.anchoredPosition = new Vector2(_layoutResolution.x * 0.1f, _layoutResolution.y * 0.55f);
        cell.sizeDelta = new Vector2(60f, 60f);
        Type entryType = typeof(InGameAugmentHud).GetNestedType("Entry", BindingFlags.NonPublic);
        object entry = Activator.CreateInstance(entryType, true);
        Set(entry, "data", data); Set(entry, "id", data.name); Set(entry, "count", 1);
        Canvas.ForceUpdateCanvases();
        Invoke(owner, "ShowTip", entry, cell);
        CaptureBoth("03_AugmentTooltip", canvas, Get<RectTransform>(owner, "_tip"));
        data.displayName = "아주 긴 증강 이름과 중첩 수치 표시 확인";
        data.description += " 이 문장은 검증용입니다. 여러 조건과 중첩 효과를 안내하는 설명이 늘어나도 제목과 등급, 본문이 서로 겹치지 않고 설명창 높이가 늘어나는지 확인합니다.";
        Set(entry, "count", 12);
        Invoke(owner, "ShowTip", entry, cell);
        CaptureBoth("03_AugmentTooltip_Long", canvas, Get<RectTransform>(owner, "_tip"));
    }

    private static void PreviewAchievements()
    {
        var catalog = Require<UIAchievementCatalogSO>("Assets/06.UI/LobbyMutedPreview/Heraldry_Achievements_v1/AchievementCatalog.asset");
        var entry = catalog.Entries.First();
        var owner = NewOwner<AchievementToast>("AchievementToast_Preview");
        Invoke(owner, "EnsureUi");
        Get<TMP_Text>(owner, "_title").text = "업적 달성!";
        Get<TMP_Text>(owner, "_name").text = "첫 방어";
        Get<TMP_Text>(owner, "_desc").text = "첫 전투에서 승리하기";
        Get<Image>(owner, "_icon").sprite = entry.Icon;
        Invoke(owner, "LayoutContent");
        // Begin은 알림 기록과 소리를 처리하므로 표시 위치 계산만 호출합니다.
        Set(owner, "_shownAt", Time.unscaledTime - 0.5f);
        Invoke(owner, "Animate");
        CaptureBoth("04_AchievementToast", Get<Canvas>(owner, "_canvas"), Get<RectTransform>(owner, "_panel"));
        Get<TMP_Text>(owner, "_name").text = "마왕성을 지키는 오랜 수호자";
        Get<TMP_Text>(owner, "_desc").text = "여러 줄의 긴 업적 설명이 표시될 때도 아이콘과 본문의 높이가 맞는지 확인합니다.";
        Invoke(owner, "LayoutContent");
        Invoke(owner, "Animate");
        CaptureBoth("04_AchievementToast_Long", Get<Canvas>(owner, "_canvas"), Get<RectTransform>(owner, "_panel"));
    }

    private static void PreviewStorage(int count)
    {
        var catalogPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:GridPrototypeCatalogSO").First());
        var catalog = Require<GridPrototypeCatalogSO>(catalogPath);
        using (var session = new GridRunSession("heraldry-preview-display-only", catalog.CreateDefinition()))
        {
            var definitions = catalog.CreateUnits();
            int realCount = Mathf.Min(count, session.Grid.Definition.StorageCapacity);
            for (int i = 0; i < realCount; i++) session.Grid.AddUnit("preview-unit-" + i, definitions[i % definitions.Count]);
            var choices = session.Grid.Units.Select(u => new GridStoredItem(eGridDragKind.UNIT, u.InstanceId, u.Definition.DisplayName)).ToList();
            // 게임 보관함 용량은 그대로 두고 표시 전용 후보만 더해 스크롤을 검사합니다.
            // Grid에 없는 ID는 실제 Describe의 DisplayName fallback으로 표시됩니다.
            for (int i = choices.Count; i < count; i++)
                choices.Add(new GridStoredItem(eGridDragKind.UNIT, "preview-display-only-" + i, definitions[i % definitions.Count].DisplayName + "  ★1"));
            var incoming = new List<GridStoredItem> { new GridStoredItem(eGridDragKind.UNIT, "preview-incoming", "전사") };
            ConstructorInfo constructor = typeof(GridStorageRequest).GetConstructors(FLAGS).Single();
            var request = (GridStorageRequest)constructor.Invoke(new object[] { eGridStorageOperation.REWARD, 1, incoming, choices, new List<BlockPlacement>(), new List<UnitPlacement>(), null });
            var owner = NewOwner<InGameStorageDiscard>("StorageDiscard_" + count + "_Preview");
            Invoke(owner, "EnsureUi");
            Invoke(owner, "Rebuild", session, request);
            Invoke(owner, "Refresh", request);
            Canvas canvas = Get<Canvas>(owner, "_canvas");
            RectTransform panel = Get<RectTransform>(owner, "_panel");
            ScrollRect scroll = Get<ScrollRect>(owner, "_scroll");
            LayoutRebuilder.ForceRebuildLayoutImmediate(Get<RectTransform>(owner, "_list"));
            Canvas.ForceUpdateCanvases();
            CaptureBoth("01_StorageDiscard_" + count, canvas, panel);
            bool initiallyDisabled = !Get<Button>(owner, "_confirm").interactable;
            // 선택 집합에 검증용 항목만 넣습니다. Toggle/Confirm/Cancel과 실제 게임 세션은 호출하지 않습니다.
            Get<HashSet<GridStoredItem>>(owner, "_picked").Add(choices.First());
            Invoke(owner, "Refresh", request);
            bool selectedEnabled = Get<Button>(owner, "_confirm").interactable;
            float viewportHeight = scroll.viewport.rect.height;
            float contentHeight = scroll.content.rect.height;
            float before = scroll.content.anchoredPosition.y;
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            float after = scroll.content.anchoredPosition.y;
            CaptureBoth("01_StorageDiscard_" + count + "_SelectedBottom", canvas, panel);
            REPORTS.Add(new
            {
                name = "StorageInteractionFixture" + count, candidateCount = choices.Count,
                actualUnitCount = realCount, displayOnlyCandidateCount = count - realCount,
                initiallyDisabled, selectedEnabled, viewportHeight, contentHeight,
                clipsCandidates = scroll.viewport.GetComponent<RectMask2D>() != null,
                scrollNeeded = contentHeight > viewportHeight, scrollMoved = Mathf.Abs(after - before) > 0.1f,
                confirmOutsideViewport = !RectOverlaps(scroll.viewport, (RectTransform)Get<Button>(owner, "_confirm").transform)
            });
        }
    }

    private static void PreviewStory()
    {
        var owner = NewOwner<StoryIntro>("StoryDialogue_Preview");
        Canvas canvas = NewCanvas("StoryCanvas", owner.transform, 100, true);
        Set(owner, "_canvas", canvas); Set(owner, "_root", (RectTransform)canvas.transform);
        Set(owner, "_font", UIHeraldrySupplement.Style.BodyFont);
        _background.texture = Require<Texture2D>("Assets/Resources/Story/story_04.png");
        RawImage king = NewRaw(canvas.transform, "StaticKingContext");
        king.texture = _kingTexture;
        king.enabled = _kingTexture != null;
        king.rectTransform.anchorMin = king.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        king.rectTransform.sizeDelta = new Vector2(900f, 900f);
        king.rectTransform.anchoredPosition = new Vector2(0f, 90f);
        Invoke(owner, "BuildPanel");
        Get<CanvasGroup>(owner, "_panelGroup").alpha = 1f;
        Get<TMP_Text>(owner, "_name").text = "마왕";
        Get<TMP_Text>(owner, "_text").text = "……시끄럽군. 누가 감히 짐의 낮잠을 방해하느냐!";
        Get<RectTransform>(owner, "_arrow").gameObject.SetActive(true);
        CaptureBoth("05_StoryDialogue", canvas, Get<RectTransform>(owner, "_panel"));
        Get<TMP_Text>(owner, "_text").text = "좋다! 쳐들어오는 용사들이 짐을 더 강하게 만들어 줄 것이다.\n마왕군이여, 성을 지켜라!";
        CaptureBoth("05_StoryDialogue_Long", canvas, Get<RectTransform>(owner, "_panel"));

        // 복사한 대사 목록이 아니라 실제 StoryIntro의 6개 원문을 읽어서 검사합니다.
        var slides = (Array)typeof(StoryIntro).GetField("Slides", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        TMP_Text body = Get<TMP_Text>(owner, "_text");
        TMP_Text speakerLabel = Get<TMP_Text>(owner, "_name");
        for (int i = 0; i < slides.Length; i++)
        {
            object slide = slides.GetValue(i);
            string speaker = Get<string>(slide, "Speaker");
            body.text = Get<string>(slide, "Text");
            speakerLabel.text = speaker ?? string.Empty;
            Get<RectTransform>(owner, "_namePlate").gameObject.SetActive(!string.IsNullOrEmpty(speaker));
            Canvas.ForceUpdateCanvases();
            body.ForceMeshUpdate(true);
            speakerLabel.ForceMeshUpdate(true);
            REPORTS.Add(new
            {
                name = "StorySlideFixture" + (i + 1), slideNumber = i + 1, text = body.text,
                speaker, bodyOverflow = body.isTextOverflowing,
                speakerOverflow = !string.IsNullOrEmpty(speaker) && speakerLabel.isTextOverflowing,
                missingFont = body.font == null || (!string.IsNullOrEmpty(speaker) && speakerLabel.font == null),
                bodyHeight = body.rectTransform.rect.height, preferredHeight = body.preferredHeight
            });
        }
    }

    private static object BuildSummary(bool restored, string fatalError)
    {
        // 서로 다른 화면 보고서의 실패를 한곳에 모아 부분 실패가 전체 성공처럼 보이지 않게 합니다.
        var reports = Newtonsoft.Json.Linq.JArray.FromObject(REPORTS);
        var captures = reports.Where(r => r["filename"] != null).ToArray();
        var storage = reports.Where(r => ((string)r["name"] ?? string.Empty).StartsWith("StorageInteractionFixture", StringComparison.Ordinal)).ToArray();
        var story = reports.Where(r => ((string)r["name"] ?? string.Empty).StartsWith("StorySlideFixture", StringComparison.Ordinal)).ToArray();
        var number = reports.FirstOrDefault(r => (string)r["name"] == "NumberFontFixture");
        var shared = reports.FirstOrDefault(r => (string)r["name"] == "SharedStyle");
        string[] styleFields =
        {
            "frame", "crest", "nameplate", "medallion", "tail", "panel", "dialogue", "resultPanel", "resultTrack", "resultFill",
            "darkButton", "redButton", "titleFont", "bodyFont", "numberFont"
        };
        int failedCaseCount = reports.Count(r => (bool?)r["failed"] == true);
        int textOverflowCount = captures.Count(r => (bool?)r["textOverflow"] == true);
        int missingFontCount = reports.Count(r => (bool?)r["missingFont"] == true);
        int missingFrameCount = captures.Count(r => (bool?)r["missingFrame"] == true);
        int invalidFrameSliceCount = captures.Count(r => (bool?)r["invalidFrameSlice"] == true);
        int missingDecorationSpriteCount = captures.Count(r => (bool?)r["missingDecorationSprite"] == true);
        int decorationBlocksPointerCount = captures.Count(r => (bool?)r["decorationBlocksPointer"] == true);
        int storageFixtureFailureCount = storage.Count(r => (bool?)r["initiallyDisabled"] != true || (bool?)r["selectedEnabled"] != true ||
            (bool?)r["clipsCandidates"] != true || (bool?)r["confirmOutsideViewport"] != true || ((bool?)r["scrollNeeded"] == true && (bool?)r["scrollMoved"] != true));
        int storySlideOverflowCount = story.Count(r => (bool?)r["bodyOverflow"] == true || (bool?)r["speakerOverflow"] == true);
        int numberFixtureFailureCount = number == null || (int?)number["sdfTextCount"] != 8 || (int?)number["legacyTextCount"] != 0 ? 1 : 0;
        int missingStyleReferenceCount = shared == null ? styleFields.Length : styleFields.Count(f => (bool?)shared[f] != true);
        int issueCount = failedCaseCount + textOverflowCount + missingFontCount + missingFrameCount + invalidFrameSliceCount +
            missingDecorationSpriteCount + decorationBlocksPointerCount + storageFixtureFailureCount + storySlideOverflowCount + numberFixtureFailureCount + missingStyleReferenceCount;
        bool coverageComplete = captures.Length >= 30 && storage.Length == 2 && story.Length == 6;
        return new
        {
            passed = restored && fatalError == null && coverageComplete && issueCount == 0,
            coverageComplete, capturedImageCount = captures.Length, storageFixtureCount = storage.Length,
            storySlideCheckedCount = story.Length, failedCaseCount, textOverflowCount, missingFontCount,
            missingFrameCount, invalidFrameSliceCount, missingDecorationSpriteCount, decorationBlocksPointerCount,
            storageFixtureFailureCount, storySlideOverflowCount, numberFixtureFailureCount, missingStyleReferenceCount, issueCount
        };
    }

    private static void PreviewTutorial()
    {
        var owner = NewOwner<TutorialDirector>("TutorialBubble_Preview");
        Set(owner, "_font", UIHeraldrySupplement.Style.BodyFont);
        // Runtime 초상화의 Destroy/애니메이션 처리를 Edit Mode에서 실행하지 않습니다.
        // UI 생성에는 null prefab을 사용하고 별도 정적 SpriteRenderer 렌더만 연결합니다.
        Invoke(owner, "EnsureUi");
        Get<Canvas>(owner, "_helpCanvas").enabled = false;
        RawImage portrait = Get<RawImage>(owner, "_portraitImage");
        portrait.texture = _kingTexture;
        portrait.enabled = _kingTexture != null;
        Get<TMP_Text>(owner, "_body").text = "직접 해 보거라! 카드를 끌어다 칸 위에 놓아 보거라.";
        Set(owner, "_dockLeft", true); Set(owner, "_dialogAtTop", false);
        Set(owner, "_popStart", Time.unscaledTime - 1f);
        Invoke(owner, "FitBubble"); Invoke(owner, "LayoutDialog");
        Get<RectTransform>(owner, "_progressFill").anchorMax = new Vector2(0.4f, 1f);
        CaptureBoth("06_TutorialBubble", Get<Canvas>(owner, "_canvas"), Get<RectTransform>(owner, "_bubble"));
        Get<TMP_Text>(owner, "_body").text += " 이 문장은 검증용이다. 여러 줄로 설명해도 이름판, 본문, 다음 안내가 겹치지 않는지 살펴보거라.";
        Invoke(owner, "FitBubble"); Invoke(owner, "LayoutDialog");
        CaptureBoth("06_TutorialBubble_Long", Get<Canvas>(owner, "_canvas"), Get<RectTransform>(owner, "_bubble"));
    }

    private static void PreviewNumbers()
    {
        var owner = NewOwner<InGameBattleFx>("CombatNumbers_Preview");
        Invoke(owner, "Number", new Vector3(-1.8f, 0.1f, 0f), "53", Color.white, 1f, false);
        Invoke(owner, "Number", new Vector3(0f, 0.55f, 0f), "153!", new Color(1f, 0.82f, 0.25f), 1.5f, true);
        Invoke(owner, "Number", new Vector3(1.4f, -0.1f, 0f), "+12", new Color(0.5f, 1f, 0.6f), 0.8f, false);
        Invoke(owner, "Number", new Vector3(2.8f, 0.5f, 0f), "-24", new Color(1f, 0.38f, 0.32f), 0.85f, false);
        foreach (object item in Get<IList>(owner, "_active"))
        {
            var tick = Get<Delegate>(item, "tick");
            if (tick != null) tick.DynamicInvoke(item, 0.25f);
        }
        Transform root = Get<Transform>(owner, "_root");
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
        CaptureBoth("07_CombatNumbers", null, null, root);
        REPORTS.Add(new
        {
            name = "NumberFontFixture", sdfTextCount = root.GetComponentsInChildren<TextMeshPro>(true).Length,
            legacyTextCount = root.GetComponentsInChildren<TextMesh>(true).Length,
            numberFont = Get<TMP_FontAsset>(owner, "_numberFont") != null ? Get<TMP_FontAsset>(owner, "_numberFont").name : null
        });
    }

    private static void CaptureBoth(string name, Canvas canvas, RectTransform panel, Transform worldRoot = null)
    {
        if (canvas != null) ConfigureCanvas(canvas);
        if (worldRoot != null) SetLayer(worldRoot);
        try
        {
            Capture(name, 1920, 1080, canvas, panel, worldRoot);
            Capture(name, 1280, 720, canvas, panel, worldRoot);
        }
        finally
        {
            // 다음 실제 배치 메서드가 Overlay의 화면 좌표로 계산되도록 돌려놓습니다.
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.scaleFactor = 1f;
                canvas.enabled = false;
            }
        }
    }

    private static void Capture(string name, int width, int height, Canvas canvas, RectTransform panel, Transform worldRoot)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            _camera.targetTexture = target;
            ApplyRenderScale(_backgroundCanvas, width, height);
            if (canvas != null) ApplyRenderScale(canvas, width, height);
            Canvas.ForceUpdateCanvases();
            Transform inspect = panel != null ? panel : worldRoot;
            TMP_Text[] texts = inspect != null ? inspect.GetComponentsInChildren<TMP_Text>(true).Where(t => t.gameObject.activeInHierarchy).ToArray() : Array.Empty<TMP_Text>();
            foreach (TMP_Text text in texts) text.ForceMeshUpdate(true);
            _camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            string filename = name + "_" + width + "x" + height + ".png";
            File.WriteAllBytes(OUTPUT + "/" + filename, image.EncodeToPNG());
            var style = UIHeraldrySupplement.Style;
            Image panelBackground = panel != null ? panel.GetComponent<Image>() : null;
            bool hasCompletePanel = panelBackground != null && style != null && style.PanelSprite != null &&
                panelBackground.sprite == style.PanelSprite;
            Image[] decorations = inspect != null ? inspect.GetComponentsInChildren<Image>(true)
                .Where(i => IsDecoration(i) || (hasCompletePanel && i == panelBackground)).ToArray() : Array.Empty<Image>();
            UIHeraldryDialogueImage[] dialogues = decorations.OfType<UIHeraldryDialogueImage>().ToArray();
            int completeDialogueCount = style != null && style.DialogueSprite != null
                ? dialogues.Count(i => i.sprite == style.DialogueSprite) : 0;
            int frameCount = decorations.Count(i => i.name == "HeraldryFrame");
            REPORTS.Add(new
            {
                name, filename, outputWidth = width, outputHeight = height,
                panelWidth = panel != null ? panel.rect.width : 0f, panelHeight = panel != null ? panel.rect.height : 0f,
                textOverflow = panel != null && texts.Any(t => t.isTextOverflowing), missingFont = texts.Any(t => t.font == null),
                frameCount, completePanelCount = hasCompletePanel ? 1 : 0, completeDialogueCount,
                // 배경과 테두리를 합친 그림은 별도의 HeraldryFrame 자식이 없어도 정상입니다.
                missingFrame = panel != null && frameCount == 0 && !hasCompletePanel && completeDialogueCount == 0,
                invalidFrameSlice = decorations.Where(i => i.name == "HeraldryFrame").Any(i => i.type != Image.Type.Sliced || i.sprite == null || i.sprite.border == Vector4.zero) ||
                    (hasCompletePanel && (panelBackground.type != Image.Type.Sliced || panelBackground.sprite.border == Vector4.zero)) ||
                    dialogues.Any(i => i.sprite == null || i.sprite.border == Vector4.zero),
                missingDecorationSprite = decorations.Any(i => i.sprite == null), decorationBlocksPointer = decorations.Any(i => i.raycastTarget),
                decorations = decorations.Select(i => new { name = i.name, sprite = i.sprite != null ? i.sprite.name : null, type = i.type.ToString(), border = i.sprite != null ? i.sprite.border.ToString() : null, raycastTarget = i.raycastTarget }).ToArray(),
                texts = texts.Select(t => new { name = t.name, text = t.text, font = t.font != null ? t.font.name : null, size = t.fontSize, width = t.rectTransform.rect.width, height = t.rectTransform.rect.height, preferredHeight = t.preferredHeight, overflow = t.isTextOverflowing, visibleCharacters = t.textInfo.characterInfo.Take(t.textInfo.characterCount).Count(c => c.isVisible) }).ToArray()
            });
        }
        finally
        {
            _camera.targetTexture = null; RenderTexture.active = previous;
            Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
        }
    }

    private static bool IsDecoration(Image image)
    {
        return image is UIHeraldryDialogueImage || image.name == "HeraldryFrame" || image.name == "HeraldryCrest" || image.name == "SpeakerCrest" || image.name == "IconFrame" || image.name == "NameTag" || image.name == "Tail" || (image.transform.parent != null && image.transform.parent.name == "NamePlate");
    }

    private static T NewOwner<T>(string name) where T : Behaviour
    {
        var go = new GameObject(name);
        go.SetActive(false);
        T owner = go.AddComponent<T>();
        owner.enabled = false;
        OWNERS.Add(owner);
        go.SetActive(true);
        return owner;
    }

    private static Canvas NewCanvas(string name, Transform parent, int sortingOrder, bool reference1080)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = reference1080 ? new Vector2(1920f, 1080f) : _layoutResolution;
        scaler.matchWidthOrHeight = 0.5f;
        ConfigureCanvas(canvas);
        return canvas;
    }

    private static void ConfigureCanvas(Canvas canvas)
    {
        canvas.enabled = true;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _camera;
        canvas.planeDistance = 1f;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _layoutResolution;
            scaler.matchWidthOrHeight = 0.5f;
        }
        // 동기식 Camera.Render 안에서는 CanvasScaler.Update를 기다릴 수 없어 렌더 배율만 직접 적용합니다.
        scaler.enabled = false;
        SetLayer(canvas.transform);
    }

    private static void ApplyRenderScale(Canvas canvas, int width, int height)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Vector2 reference = scaler != null ? scaler.referenceResolution : _layoutResolution;
        float match = scaler != null ? scaler.matchWidthOrHeight : 0.5f;
        canvas.scaleFactor = Mathf.Pow(width / Mathf.Max(1f, reference.x), 1f - match) * Mathf.Pow(height / Mathf.Max(1f, reference.y), match);
    }

    private static RawImage NewRaw(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        go.layer = PREVIEW_LAYER;
        RawImage image = go.GetComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    private static Texture2D RenderStaticKing()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02.Prefabs/DemonArmy_Unit/DemonKing.prefab");
        if (prefab == null) return null;
        var stage = new GameObject("StaticKingPreview");
        stage.transform.position = new Vector3(6000f, 6000f, 0f);
        Camera camera = null;
        RenderTexture target = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            CloneSpriteHierarchy(prefab.transform, stage.transform);
            var renderers = stage.GetComponentsInChildren<SpriteRenderer>().Where(r => r.sprite != null && r.enabled).ToArray();
            if (renderers.Length == 0) return null;
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            camera = new GameObject("StaticKingCamera", typeof(Camera)).GetComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear; camera.cullingMask = 1 << PREVIEW_LAYER;
            camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y) * 0.6f;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
            target = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            var texture = new Texture2D(768, 768, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            TEMPORARIES.Add(texture);
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); texture.Apply();
            return texture;
        }
        finally
        {
            RenderTexture.active = previous;
            if (camera != null) { camera.targetTexture = null; Object.DestroyImmediate(camera.gameObject); }
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            Object.DestroyImmediate(stage);
        }
    }

    private static void CloneSpriteHierarchy(Transform source, Transform parent)
    {
        var go = new GameObject(source.name) { layer = PREVIEW_LAYER };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = source.localPosition;
        go.transform.localRotation = source.localRotation;
        go.transform.localScale = source.localScale;
        SpriteRenderer renderer = source.GetComponent<SpriteRenderer>();
        if (renderer != null) EditorUtility.CopySerialized(renderer, go.AddComponent<SpriteRenderer>());
        foreach (Transform child in source) CloneSpriteHierarchy(child, go.transform);
        go.SetActive(source.gameObject.activeSelf);
    }

    private static void CollectTemporaryResources(Behaviour owner)
    {
        foreach (FieldInfo field in owner.GetType().GetFields(FLAGS))
        {
            Object value = field.GetValue(owner) as Object;
            if (value == null || AssetDatabase.Contains(value)) continue;
            if (value is Sprite sprite) { TEMPORARIES.Add(sprite); TEMPORARIES.Add(sprite.texture); }
            else if (value is Material || value is Texture2D) TEMPORARIES.Add(value);
        }
        foreach (Image image in owner.GetComponentsInChildren<Image>(true))
            if (image.sprite != null && !AssetDatabase.Contains(image.sprite))
            { TEMPORARIES.Add(image.sprite); TEMPORARIES.Add(image.sprite.texture); }
    }

    private static Texture2D LoadTemporaryTexture(string path)
    {
        if (!File.Exists(path)) return null;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        TEMPORARIES.Add(texture);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) return null;
        return texture;
    }

    private static bool RectOverlaps(RectTransform a, RectTransform b)
    {
        var ac = new Vector3[4]; var bc = new Vector3[4];
        a.GetWorldCorners(ac); b.GetWorldCorners(bc);
        return Rect.MinMaxRect(ac[0].x, ac[0].y, ac[2].x, ac[2].y).Overlaps(Rect.MinMaxRect(bc[0].x, bc[0].y, bc[2].x, bc[2].y));
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetLayer(Transform root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PREVIEW_LAYER;
    }

    private static T Require<T>(string path) where T : Object
    {
        T result = AssetDatabase.LoadAssetAtPath<T>(path);
        if (result == null) throw new InvalidOperationException("미리보기에 필요한 에셋이 없습니다: " + path);
        return result;
    }

    private static T Get<T>(object owner, string field)
    {
        FieldInfo info = owner.GetType().GetField(field, FLAGS);
        if (info == null) throw new MissingFieldException(owner.GetType().Name, field);
        return (T)info.GetValue(owner);
    }

    private static void Set(object owner, string field, object value)
    {
        FieldInfo info = owner.GetType().GetField(field, FLAGS);
        if (info == null) throw new MissingFieldException(owner.GetType().Name, field);
        info.SetValue(owner, value);
    }

    private static void SetIfPresent(object owner, string field, object value)
    {
        owner.GetType().GetField(field, FLAGS)?.SetValue(owner, value);
    }

    private static void Invoke(object owner, string method, params object[] args)
    {
        MethodInfo info = owner.GetType().GetMethod(method, FLAGS);
        if (info == null) throw new MissingMethodException(owner.GetType().Name, method);
        info.Invoke(owner, args);
    }
}
