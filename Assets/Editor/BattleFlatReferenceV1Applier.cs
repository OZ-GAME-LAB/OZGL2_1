using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// InGame 전투 UI의 기존 동작과 참조는 보존하고, 승인된 Flat Reference V1 아트만 적용한다.
/// </summary>
public static class BattleFlatReferenceV1Applier
{
    private const string MENU_PATH = "Tools/OZGL2/Battle/Apply Flat Reference V1";
    private const string PREFAB_MENU_PATH = "Tools/OZGL2/Battle/Apply Flat Reference V1 Shared Prefabs";
    private const string VALIDATE_MENU_PATH = "Tools/OZGL2/Battle/Validate Flat Reference V1";
    private const string INGAME_SCENE = "Assets/00.Scenes/Builds/InGame.unity";
    private const string BATTLE_HUD_PREFAB = "Assets/06.UI/BattleMutedPreview/Prefabs/BattleHud.prefab";
    private const string WAVE_PREVIEW_PREFAB = "Assets/06.UI/BattleMutedPreview/Prefabs/WavePreviewPanel.prefab";
    private const string SYNERGY_TRACKER_PREFAB = "Assets/06.UI/BattleMutedPreview/Prefabs/SynergyTracker.prefab";
    private const string GET_READY_PREFAB = "Assets/02.Prefabs/UI/UI_Panel/Canvas_GetReady.prefab";
    private const string ART_DIRECTORY = "Assets/06.UI/BattleMutedPreview/FlatReference_v1/Sprites/";
    private const string CROSSED_SWORDS = ART_DIRECTORY + "Icon_CrossedSwords_Casual.png";
    private const string HOURGLASS = ART_DIRECTORY + "Icon_Hourglass_Casual.png";
    private const string MENU_ICON = ART_DIRECTORY + "Icon_Menu_Casual.png";
    private const string WAVE_CHEVRON = ART_DIRECTORY + "Icon_WaveChevron_Casual.png";

    private const string TOP_HUD = ART_DIRECTORY + "Frame_TopHud_Flat.png";
    private const string WAVE_PREVIEW = ART_DIRECTORY + "Frame_WavePreview_Flat.png";
    private const string SYNERGY_NAMEPLATE = ART_DIRECTORY + "Frame_SynergyNameplate_Flat.png";
    private const string DIAMOND_LARGE = ART_DIRECTORY + "Frame_DiamondLarge_Flat.png";
    private const string WAVE_TOGGLE = ART_DIRECTORY + "Frame_WaveToggle_Flat.png";
    private const string MENU_DIAMOND = ART_DIRECTORY + "Frame_MenuDiamond_Flat.png";
    private const string LEVEL_TRACK = ART_DIRECTORY + "Bar_LevelTrack_Chevron_Flat.png";
    private const string LEVEL_FILL = ART_DIRECTORY + "Bar_LevelFill_Chevron_Flat.png";
    private const string CURRENCY_DIAMOND = ART_DIRECTORY + "Frame_CurrencyDiamond_Flat.png";
    private const string REROLL_DIAMOND = ART_DIRECTORY + "Frame_RerollDiamond_Flat.png";
    private const string SMALL_DIAMOND = ART_DIRECTORY + "Ornament_Diamond_Flat.png";
    private const string TITLE_DIAMOND = ART_DIRECTORY + "Ornament_WaveTitle_Flat.png";
    private const string UNIT_DIVIDER = ART_DIRECTORY + "Divider_WaveUnit_Flat.png";
    private const string BOTTOM_HUD = ART_DIRECTORY + "Panel_BottomHud_Flat.png";
    private const string START_COMBAT = ART_DIRECTORY + "Frame_StartCombat_Flat.png";
    private const string COST_PLATE = ART_DIRECTORY + "Frame_CostPlate_Flat.png";
    private const string DIVIDER = ART_DIRECTORY + "Divider_Vertical_Flat.png";

    private const string UNDO_LABEL = "전투 UI Flat Reference V1 적용";

    private const float HUD_WIDTH = 1695;
    private const float HUD_HEIGHT = 82;
    private static readonly Color IVORY = new Color32(232, 217, 196, 255);

    private sealed class ArtSet
    {
        public Sprite TopHud;
        public Sprite WavePreview;
        public Sprite SynergyNameplate;
        public Sprite DiamondLarge;
        public Sprite WaveToggle;
        public Sprite MenuDiamond;
        public Sprite LevelTrack;
        public Sprite LevelFill;
        public Sprite BottomHud;
        public Sprite StartCombat;
        public Sprite CostPlate;
        public Sprite Divider;
        public Sprite CrossedSwords;
        public Sprite Hourglass;
        public Sprite MenuIcon;
        public Sprite WaveChevron;
        public Sprite CurrencyDiamond;
        public Sprite RerollDiamond;
        public Sprite SmallDiamond;
        public Sprite TitleDiamond;
        public Sprite UnitDivider;
    }

    [MenuItem(MENU_PATH)]
    public static void Apply()
    {
        Scene scene = ValidateEditorState();
        PreflightSharedPrefabs();
        PreflightScene(scene);
        string[] buttonEventsBefore = CaptureSceneButtonEvents(scene);
        string[] originalIconsBefore = CaptureOriginalActionIcons(scene);
        ArtSet art = ImportAndLoadArt();

        ApplyBattleHudPrefab(art);
        ApplyWavePreviewPrefab(art);
        ApplySynergyTrackerPrefab(art);
        ApplyGetReadyPrefab(art);

        // Prefab 저장 뒤 Scene 인스턴스가 갱신되도록 동기화한 다음, Scene 직접 장식만 수정한다.
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Undo.IncrementCurrentGroup();
        int sceneUndoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_LABEL);
        try
        {
            ApplySceneBottomHud(scene, art);
            string[] buttonEventsAfter = CaptureSceneButtonEvents(scene);
            if (!buttonEventsBefore.SequenceEqual(buttonEventsAfter))
                throw new InvalidOperationException("기존 Button 이벤트 구성이 변경되어 저장을 중단했습니다. Git Diff에서 Prefab을 확인해 주세요.");
            if (!originalIconsBefore.SequenceEqual(CaptureOriginalActionIcons(scene)))
                throw new InvalidOperationException("화염/리롤/전투 시작 원본 아이콘의 Sprite 또는 색상이 변경되어 저장을 중단했습니다.");

            ValidateAppliedState(scene, art);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("InGame 씬 저장에 실패했습니다.");
        }
        catch
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(sceneUndoGroup);
            throw;
        }

        Undo.CollapseUndoOperations(sceneUndoGroup);
        Debug.Log("Flat Reference V1 적용 완료: 단색 통합 프레임/장식, BattleHud/WavePreview/Synergy/GetReady Prefab 및 InGame 정렬. 기존 Button 이벤트, 수치 갱신, 하단 원본 아이콘을 검증했습니다.");
    }

    [MenuItem(VALIDATE_MENU_PATH)]
    public static void Validate()
    {
        Scene scene = ValidateEditorState();
        ArtSet art = LoadArt();
        ValidateAppliedState(scene, art);
        Debug.Log("Flat Reference V1 검증 완료: 필수 Sprite, Prefab 연결, InGame 하단 장식 연결이 모두 정상입니다.");
    }

    [MenuItem(PREFAB_MENU_PATH)]
    public static void ApplySharedPrefabsOnly()
    {
        ValidatePrefabEditorState();
        PreflightSharedPrefabs();
        ArtSet art = ImportAndLoadArt();
        ApplyBattleHudPrefab(art);
        ApplyWavePreviewPrefab(art);
        ApplySynergyTrackerPrefab(art);
        ApplyGetReadyPrefab(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Flat Reference V1 공용 Prefab 재적용 완료. 현재 Scene의 미저장 상태는 변경하거나 저장하지 않았습니다.");
    }

    private static Scene ValidateEditorState()
    {
        ValidatePrefabEditorState();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != INGAME_SCENE)
            throw new InvalidOperationException("InGame 씬을 연 상태에서만 실행할 수 있습니다: " + INGAME_SCENE);
        if (scene.isDirty)
            throw new InvalidOperationException("현재 씬의 미저장 변경을 먼저 저장하거나 보존해 주세요. 기존 변경을 덮어쓰지 않았습니다.");
        return scene;
    }

    private static void ValidatePrefabEditorState()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Unity Edit Mode에서 컴파일이 끝난 뒤 실행해 주세요.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Prefab Stage를 닫은 뒤 실행해 주세요.");
    }

    private static ArtSet ImportAndLoadArt()
    {
        PreflightArtAssets();
        ConfigureImporter(TOP_HUD, new Vector4(55, 14, 55, 14));
        ConfigureImporter(WAVE_PREVIEW, new Vector4(35, 18, 35, 18));
        ConfigureImporter(SYNERGY_NAMEPLATE, new Vector4(12, 12, 12, 12));
        ConfigureImporter(DIAMOND_LARGE, Vector4.zero);
        ConfigureImporter(WAVE_TOGGLE, Vector4.zero);
        ConfigureImporter(MENU_DIAMOND, Vector4.zero);
        ConfigureImporter(LEVEL_TRACK, Vector4.zero);
        ConfigureImporter(LEVEL_FILL, Vector4.zero);
        ConfigureImporter(BOTTOM_HUD, new Vector4(0, 12, 0, 48));
        ConfigureImporter(START_COMBAT, Vector4.zero);
        ConfigureImporter(COST_PLATE, Vector4.zero);
        ConfigureImporter(DIVIDER, Vector4.zero);
        foreach (string path in AdditionalArtPaths())
            ConfigureImporter(path, Vector4.zero);

        return LoadArt();
    }

    private static ArtSet LoadArt()
    {
        return new ArtSet
        {
            TopHud = Load<Sprite>(TOP_HUD),
            WavePreview = Load<Sprite>(WAVE_PREVIEW),
            SynergyNameplate = Load<Sprite>(SYNERGY_NAMEPLATE),
            DiamondLarge = Load<Sprite>(DIAMOND_LARGE),
            WaveToggle = Load<Sprite>(WAVE_TOGGLE),
            MenuDiamond = Load<Sprite>(MENU_DIAMOND),
            LevelTrack = Load<Sprite>(LEVEL_TRACK),
            LevelFill = Load<Sprite>(LEVEL_FILL),
            BottomHud = Load<Sprite>(BOTTOM_HUD),
            StartCombat = Load<Sprite>(START_COMBAT),
            CostPlate = Load<Sprite>(COST_PLATE),
            Divider = Load<Sprite>(DIVIDER),
            CrossedSwords = Load<Sprite>(CROSSED_SWORDS),
            Hourglass = Load<Sprite>(HOURGLASS),
            MenuIcon = Load<Sprite>(MENU_ICON),
            WaveChevron = Load<Sprite>(WAVE_CHEVRON),
            CurrencyDiamond = Load<Sprite>(CURRENCY_DIAMOND),
            RerollDiamond = Load<Sprite>(REROLL_DIAMOND),
            SmallDiamond = Load<Sprite>(SMALL_DIAMOND),
            TitleDiamond = Load<Sprite>(TITLE_DIAMOND),
            UnitDivider = Load<Sprite>(UNIT_DIVIDER)
        };
    }

    private static string[] AdditionalArtPaths()
    {
        return new[] { CROSSED_SWORDS, HOURGLASS, MENU_ICON, WAVE_CHEVRON,
            CURRENCY_DIAMOND, REROLL_DIAMOND, SMALL_DIAMOND, TITLE_DIAMOND, UNIT_DIVIDER };
    }

    private static void PreflightArtAssets()
    {
        string[] texturePaths =
        {
            TOP_HUD,
            WAVE_PREVIEW,
            SYNERGY_NAMEPLATE,
            DIAMOND_LARGE,
            WAVE_TOGGLE,
            MENU_DIAMOND,
            LEVEL_TRACK,
            LEVEL_FILL,
            BOTTOM_HUD,
            START_COMBAT,
            COST_PLATE,
            DIVIDER
        };

        foreach (string path in texturePaths.Concat(AdditionalArtPaths()))
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter))
                throw new InvalidOperationException("필수 UI Texture 또는 TextureImporter가 없습니다: " + path);
        }

        // 신규 PNG는 아직 Texture로 임포트됐을 수 있으므로 Sprite 검사는 ImportAndLoadArt 이후에 한다.
    }

    private static void PreflightSharedPrefabs()
    {
        PreflightPrefab(BATTLE_HUD_PREFAB, root =>
        {
            NeedRect(root);
            NeedRect(root, "Body");
            NeedRect(root, "Body/Body");
            NeedImage(root, "Body/Body");
            NeedImage(root, "Body/Border");
            NeedImage(root, "WaveIcon");
            RectTransform toggle = NeedRect(root, "WaveToggleButton");
            NeedImage(root, "WaveToggleButton");
            NeedComponent<Button>(toggle);
            NeedRect(toggle, "Arrow");
            NeedImage(toggle, "Arrow");
            NeedImage(root, "Divider_Wave");
            NeedImage(root, "Divider_Level");
            Need(root, "Divider_Menu");
            NeedRect(root, "LevelText");
            NeedImage(root, "ExperienceTrack");
            NeedImage(root, "ExperienceFill");
            NeedRect(root, "ExperienceTicks");
            NeedRect(root, "TimerIcon");
            NeedImage(root, "TimerIcon");
            NeedRect(root, "WaveText");
            NeedRect(root, "RemainingTimeText");
        });

        PreflightPrefab(WAVE_PREVIEW_PREFAB, root =>
        {
            NeedImage(root, "Frame/Border");
            Need(root, "HeaderShade");
            Need(root, "HeaderLine");
            NeedImage(root, "HeaderLine");
            NeedImage(root, "Enemy_1/Separator");
            NeedImage(root, "Enemy_2/Separator");
            NeedComponent<Text>(Need(root, "Title"));
        });

        PreflightPrefab(SYNERGY_TRACKER_PREFAB, root =>
        {
            NeedImage(root, "Frame");
            NeedImage(root, "Nameplate/Body");
            NeedRect(root, "Nameplate");
            NeedRect(root, "Icon");
            NeedComponent<Text>(Need(root, "Name"));
            NeedRect(root, "Thresholds");
        });

        PreflightPrefab(GET_READY_PREFAB, root =>
        {
            NeedRect(root, "BattleHUD");
            NeedRect(root, "BattleHUD/Body");
            NeedRect(root, "BattleHUD/Body/Body");
            NeedRect(root, "BattleHUD/Body/Border");
            NeedRect(root, "BattleHUD/ExperienceTicks");
            RectTransform settings = NeedRect(root, "SettingsButton");
            NeedImage(root, "SettingsButton");
            NeedComponent<Button>(settings);
            NeedRect(settings, "Icon");
            NeedImage(settings, "Icon");
            NeedRect(root, "WavePreviewViewport");
            NeedRect(root, "WavePreviewViewport/WavePreview");
            NeedRect(root, "SynergyTrackers");
        });
    }

    private static void PreflightPrefab(string path, Action<Transform> inspect)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
            throw new InvalidOperationException("필수 Prefab을 불러올 수 없습니다: " + path);

        try
        {
            inspect(root.transform);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PreflightScene(Scene scene)
    {
        string[] imagePaths =
        {
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/BottomNobleBackground/ReferenceLayersV4/Hud_Backplate",
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Currency/Panel_CurrentAmount",
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Reroll/Button_Reroll",
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Reroll/RerollValue_Frame",
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/StartCombatButton/Button_BattleStart"
        };

        foreach (string path in imagePaths)
        {
            Transform target = NeedSceneTransform(scene, path);
            NeedRect(target);
            NeedComponent<Image>(target);
        }
        Transform preparation = NeedSceneTransform(scene, "BattleInGameUI/UI_BattleScreens/Canvas_Preparation");
        foreach (string path in new[] { "Currency/Icon", "Currency/Value_SDF", "Reroll/Icon",
            "Reroll/RerollValue_Icon", "Reroll/RerollValue_Text", "StartCombatButton/Icon", "StartCombatButton/Title" })
            NeedRect(preparation, path);
        NeedComponent<TMPro.TMP_Text>(Need(preparation, "Reroll/RerollValue_Text"));
        CaptureOriginalActionIcons(scene);
    }

    private static void ConfigureImporter(string path, Vector4 border)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("TextureImporter를 불러올 수 없습니다: " + path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.spriteBorder = border;
        importer.spritePixelsPerUnit = 100;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static void ApplyBattleHudPrefab(ArtSet art)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BATTLE_HUD_PREFAB);
        try
        {
            string[] eventsBefore = CaptureButtonEvents(root.transform);
            ConfigureBattleHudLayout(root.transform, art);

            EnsureButtonEventsUnchanged(root.transform, eventsBefore, BATTLE_HUD_PREFAB);
            SavePrefab(root, BATTLE_HUD_PREFAB);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureBattleHudLayout(Transform root, ArtSet art)
    {
        NeedRect(root).sizeDelta = new Vector2(HUD_WIDTH, HUD_HEIGHT);
        PlaceTopLeft(NeedRect(root, "Body"), 0, 0, HUD_WIDTH, HUD_HEIGHT);
        SetActiveIfPresent(root, "Body/Body", false);
        foreach (string path in new[] { "Legacy_Wave", "Legacy_Level", "Legacy_ExperienceTrack",
            "Legacy_ExperienceFill", "Legacy_ElapsedTime", "Divider_Menu" })
            SetActiveIfPresent(root, path, false);
        Image border = NeedImage(root, "Body/Border");
        PlaceTopLeft(border.rectTransform, 0, 0, HUD_WIDTH, HUD_HEIGHT);
        ConfigureImage(border, art.TopHud, Image.Type.Sliced, false, false);
        PlaceDecoration(root, "Flat_EndLeft", art.SmallDiamond, 0, 24, 34, 34);
        PlaceDecoration(root, "Flat_EndRight", art.SmallDiamond, HUD_WIDTH - 34, 24, 34, 34);

        Image waveIcon = NeedImage(root, "WaveIcon");
        PlaceTopLeft(waveIcon.rectTransform, 50, 15, 52, 52);
        ConfigureImage(waveIcon, art.CrossedSwords, Image.Type.Simple, true, false);
        PlaceTopLeft(NeedRect(root, "WaveText"), 164, 10, 280, 60);
        RectTransform toggle = NeedRect(root, "WaveToggleButton");
        PlaceTopLeft(toggle, 453, 14, 54, 54);
        Image toggleImage = NeedImage(root, "WaveToggleButton");
        ConfigureImage(toggleImage, art.WaveToggle, Image.Type.Simple, true, true);
        NeedComponent<Button>(toggle).targetGraphic = toggleImage;
        Image arrow = NeedImage(toggle, "Arrow");
        CenterInParent(arrow.rectTransform, 26, 26);
        ConfigureImage(arrow, art.WaveChevron, Image.Type.Simple, true, false);
        ConfigureDivider(root, "Divider_Wave", art.Divider, 552);
        ConfigureDivider(root, "Divider_Level", art.Divider, 1346);
        PlaceTopLeft(NeedRect(root, "LevelText"), 594, 10, 137, 60);

        Image track = NeedImage(root, "ExperienceTrack");
        PlaceTopLeft(track.rectTransform, 750, 23, 570, 36);
        ConfigureImage(track, art.LevelTrack, Image.Type.Simple, false, false);
        Image fill = NeedImage(root, "ExperienceFill");
        PlaceTopLeft(fill.rectTransform, 754, 27, 562, 28);
        float amount = fill.fillAmount;
        ConfigureImage(fill, art.LevelFill, Image.Type.Filled, false, false);
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillClockwise = true;
        fill.fillAmount = amount;
        fill.transform.SetSiblingIndex(track.transform.GetSiblingIndex() + 1);
        RectTransform ticks = NeedRect(root, "ExperienceTicks");
        PlaceTopLeft(ticks, 750, 23, 570, 36);
        for (int i = 0; i < ticks.childCount; i++)
            ticks.GetChild(i).gameObject.SetActive(false);
        // 통합 Track의 외곽선을 침범하지 않는 여백으로 Fill을 배치한다. 별도 테두리 이미지는 필요 없다.
        Transform oldLineOverlay = root.Find("Flat_LevelLines");
        if (oldLineOverlay != null) UnityEngine.Object.DestroyImmediate(oldLineOverlay.gameObject);
        Image timer = NeedImage(root, "TimerIcon");
        PlaceTopLeft(timer.rectTransform, 1396, 16, 44, 50);
        ConfigureImage(timer, art.Hourglass, Image.Type.Simple, true, false);
        PlaceTopLeft(NeedRect(root, "RemainingTimeText"), 1485, 10, 180, 60);
    }

    private static void ApplyWavePreviewPrefab(ArtSet art)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WAVE_PREVIEW_PREFAB);
        try
        {
            string[] eventsBefore = CaptureButtonEvents(root.transform);
            ConfigureWavePreviewLayout(root.transform, art);

            EnsureButtonEventsUnchanged(root.transform, eventsBefore, WAVE_PREVIEW_PREFAB);
            SavePrefab(root, WAVE_PREVIEW_PREFAB);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureWavePreviewLayout(Transform root, ArtSet art)
    {
        NeedRect(root).sizeDelta = new Vector2(640, 180);
        PlaceTopLeft(NeedRect(root, "Frame"), 0, 0, 640, 180);
        Image border = NeedImage(root, "Frame/Border");
        PlaceTopLeft(border.rectTransform, 0, 0, 640, 180);
        ConfigureImage(border, art.WavePreview, Image.Type.Sliced, false, false);
        SetActiveIfPresent(root, "HeaderShade", false);
        Image line = NeedImage(root, "HeaderLine");
        line.gameObject.SetActive(true);
        PlaceTopLeft(line.rectTransform, 30, 48, 580, 2);
        ConfigureImage(line, null, Image.Type.Simple, false, false);
        line.color = IVORY;
        Text title = NeedComponent<Text>(Need(root, "Title"));
        PlaceTopLeft(title.rectTransform, 212, 6, 216, 36);
        title.fontSize = 25;
        title.color = Color.white;
        title.raycastTarget = false;
        PlaceDecoration(root, "Flat_TitleLeft", art.TitleDiamond, 196, 17, 16, 16);
        PlaceDecoration(root, "Flat_TitleRight", art.TitleDiamond, 428, 17, 16, 16);
        PlaceDecoration(root, "Flat_CornerTL", art.SmallDiamond, 0, 30, 26, 26);
        PlaceDecoration(root, "Flat_CornerTR", art.SmallDiamond, 614, 30, 26, 26);
        PlaceDecoration(root, "Flat_CornerBL", art.SmallDiamond, 0, 134, 26, 26);
        PlaceDecoration(root, "Flat_CornerBR", art.SmallDiamond, 614, 134, 26, 26);
        foreach (string path in new[] { "Enemy_1/Separator", "Enemy_2/Separator" })
        {
            Image separator = NeedImage(root, path);
            separator.gameObject.SetActive(true);
            PlaceTopLeft(separator.rectTransform, -6, 5, 8, 96);
            ConfigureImage(separator, art.UnitDivider, Image.Type.Simple, false, false);
        }
    }

    private static void ApplySynergyTrackerPrefab(ArtSet art)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SYNERGY_TRACKER_PREFAB);
        try
        {
            string[] eventsBefore = CaptureButtonEvents(root.transform);
            ConfigureSynergyLayout(root.transform, art);

            EnsureButtonEventsUnchanged(root.transform, eventsBefore, SYNERGY_TRACKER_PREFAB);
            SavePrefab(root, SYNERGY_TRACKER_PREFAB);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureSynergyLayout(Transform root, ArtSet art)
    {
        NeedRect(root).sizeDelta = new Vector2(312, 124);
        PlaceTopLeft(NeedRect(root, "Nameplate"), 64, 12, 246, 110);
        Image plate = NeedImage(root, "Nameplate/Body");
        PlaceTopLeft(plate.rectTransform, 0, 0, 246, 110);
        ConfigureImage(plate, art.SynergyNameplate, Image.Type.Sliced, false, false);
        plate.pixelsPerUnitMultiplier = 2;
        Image frame = NeedImage(root, "Frame");
        Color categoryTint = frame.color;
        PlaceTopLeft(frame.rectTransform, 0, 0, 124, 124);
        ConfigureImage(frame, art.DiamondLarge, Image.Type.Simple, true, true);
        frame.color = categoryTint;
        Button button = root.GetComponent<Button>();
        if (button != null) button.targetGraphic = frame;
        foreach (string path in new[] { "Body", "Nameplate/Border", "Nameplate/ReferenceTop",
            "Nameplate/ReferenceBottom", "Nameplate/ReferenceRight", "Nameplate/ReferenceLeft" })
            SetActiveIfPresent(root, path, false);
        PlaceTopLeft(NeedRect(root, "Icon"), 32, 28, 60, 68);
        NeedImage(root, "Icon").color = Color.white;
        Text name = NeedComponent<Text>(Need(root, "Name"));
        PlaceTopLeft(name.rectTransform, 136, 24, 162, 36);
        name.fontSize = 25;
        PlaceTopLeft(NeedRect(root, "Thresholds"), 136, 68, 170, 40);
    }

    private static void ApplyGetReadyPrefab(ArtSet art)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GET_READY_PREFAB);
        try
        {
            string[] eventsBefore = CaptureButtonEvents(root.transform);

            RectTransform hud = NeedRect(root.transform, "BattleHUD");
            PlaceTopLeft(hud, 34, 26, HUD_WIDTH, HUD_HEIGHT);
            ConfigureBattleHudLayout(hud, art);
            PlaceTopLeft(NeedRect(root.transform, "WavePreviewViewport"), 42, 136, 640, 180);
            Transform preview = Need(root.transform, "WavePreviewViewport/WavePreview");
            PlaceTopLeft(NeedRect(preview), 0, 0, 640, 180);
            ConfigureWavePreviewLayout(preview, art);
            RectTransform trackers = NeedRect(root.transform, "SynergyTrackers");
            PlaceTopLeft(trackers, 1580, 194, 312, 532);
            for (int i = 0; i < trackers.childCount; i++)
            {
                Transform row = trackers.GetChild(i);
                PlaceTopLeft(NeedRect(row), 0, i * 136, 312, 124);
                ConfigureSynergyLayout(row, art);
            }

            RectTransform settings = NeedRect(root.transform, "SettingsButton");
            PlaceTopLeft(settings, 1770, 11, 112, 112);
            Image settingsImage = NeedImage(root.transform, "SettingsButton");
            ConfigureImage(settingsImage, art.MenuDiamond, Image.Type.Simple, true, true);
            Button settingsButton = NeedComponent<Button>(settings);
            settingsButton.targetGraphic = settingsImage;

            RectTransform menuIcon = NeedRect(settings, "Icon");
            CenterInParent(menuIcon, 48, 42);
            Image iconImage = NeedImage(settings, "Icon");
            ConfigureImage(iconImage, art.MenuIcon, Image.Type.Simple, true, false);

            EnsureButtonEventsUnchanged(root.transform, eventsBefore, GET_READY_PREFAB);
            SavePrefab(root, GET_READY_PREFAB);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplySceneBottomHud(Scene scene, ArtSet art)
    {
        Transform backplate = NeedSceneTransform(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/BottomNobleBackground/ReferenceLayersV4/Hud_Backplate");
        Transform currentAmount = NeedSceneTransform(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Currency/Panel_CurrentAmount");
        Transform reroll = NeedSceneTransform(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Reroll/Button_Reroll");
        Transform rerollCost = NeedSceneTransform(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Reroll/RerollValue_Frame");
        Transform startCombat = NeedSceneTransform(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/StartCombatButton/Button_BattleStart");

        Transform preparation = NeedSceneTransform(scene, "BattleInGameUI/UI_BattleScreens/Canvas_Preparation");
        Undo.RegisterFullObjectHierarchyUndo(preparation.gameObject, UNDO_LABEL);

        RectTransform backplateRect = NeedRect(backplate);
        backplateRect.anchorMin = new Vector2(0, 0);
        backplateRect.anchorMax = new Vector2(1, 0);
        backplateRect.pivot = new Vector2(0.5f, 0);
        backplateRect.anchoredPosition = Vector2.zero;
        backplateRect.sizeDelta = new Vector2(0, 220);
        backplateRect.localScale = Vector3.one;
        backplateRect.localRotation = Quaternion.identity;
        Image backplateImage = NeedComponent<Image>(backplate);
        ConfigureImage(backplateImage, art.BottomHud, Image.Type.Sliced, false, false);
        backplateImage.pixelsPerUnitMultiplier = 2;

        CenterAt(NeedRect(currentAmount), 0, 0, 270, 270);
        CenterAt(NeedRect(reroll), 0, 6, 180, 180);
        CenterAt(NeedRect(rerollCost), 0, -67, 150, 46);
        CenterAt(NeedRect(startCombat), 0, 5, 500, 194);
        ConfigureImage(NeedComponent<Image>(currentAmount), art.CurrencyDiamond, Image.Type.Simple, true, false);
        ConfigureImage(NeedComponent<Image>(reroll), art.RerollDiamond, Image.Type.Simple, true, false);
        ConfigureImage(NeedComponent<Image>(rerollCost), art.CostPlate, Image.Type.Simple, true, false);
        ConfigureImage(NeedComponent<Image>(startCombat), art.StartCombat, Image.Type.Simple, true, false);

        // 사용자 요청에 따라 아래 원본 Image의 Sprite/색상/Importer는 건드리지 않고 위치만 맞춘다.
        CenterAt(NeedRect(preparation, "Currency/Icon"), 0, 25, 82, 96);
        CenterAt(NeedRect(preparation, "Currency/Value_SDF"), 0, -62, 140, 58);
        CenterAt(NeedRect(preparation, "Reroll/Icon"), 0, 6, 70, 70);
        CenterAt(NeedRect(preparation, "Reroll/RerollValue_Icon"), -42, -67, 22, 22);
        CenterAt(NeedRect(preparation, "Reroll/RerollValue_Text"), 22, -67, 90, 36);
        TMPro.TMP_Text costText = NeedComponent<TMPro.TMP_Text>(Need(preparation, "Reroll/RerollValue_Text"));
        costText.fontSize = 32;
        costText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        costText.alignment = TMPro.TextAlignmentOptions.Center;
        CenterAt(NeedRect(preparation, "StartCombatButton/Icon"), -155, 5, 72, 72);
        CenterAt(NeedRect(preparation, "StartCombatButton/Title"), 95, 5, 230, 64);
    }

    private static void ValidateAppliedState(Scene scene, ArtSet art)
    {
        ValidateImporterSettings();
        ValidatePrefabSprite(BATTLE_HUD_PREFAB, "Body/Border", art.TopHud);
        ValidatePrefabSprite(BATTLE_HUD_PREFAB, "WaveToggleButton", art.WaveToggle);
        ValidatePrefabSprite(BATTLE_HUD_PREFAB, "ExperienceTrack", art.LevelTrack);
        ValidatePrefabSprite(BATTLE_HUD_PREFAB, "ExperienceFill", art.LevelFill);
        ValidatePrefabSprite(BATTLE_HUD_PREFAB, "WaveIcon", art.CrossedSwords);
        ValidatePrefabSprite(BATTLE_HUD_PREFAB, "TimerIcon", art.Hourglass);
        ValidatePrefabSprite(WAVE_PREVIEW_PREFAB, "Frame/Border", art.WavePreview);
        ValidatePrefabSprite(SYNERGY_TRACKER_PREFAB, "Frame", art.DiamondLarge);
        ValidatePrefabSprite(SYNERGY_TRACKER_PREFAB, "Nameplate/Body", art.SynergyNameplate);
        ValidatePrefabSprite(GET_READY_PREFAB, "SettingsButton", art.MenuDiamond);

        Image fill = LoadPrefabImage(BATTLE_HUD_PREFAB, "ExperienceFill");
        if (fill.type != Image.Type.Filled || fill.fillMethod != Image.FillMethod.Horizontal)
            throw new InvalidOperationException("ExperienceFill의 Filled/Horizontal 갱신 구조가 유지되지 않았습니다.");

        Image settings = LoadPrefabImage(GET_READY_PREFAB, "SettingsButton");
        Button settingsButton = settings.GetComponent<Button>();
        if (settingsButton == null || settingsButton.targetGraphic != settings)
            throw new InvalidOperationException("SettingsButton의 클릭 대상 연결이 올바르지 않습니다.");

        ValidateSceneSprite(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/BottomNobleBackground/ReferenceLayersV4/Hud_Backplate", art.BottomHud);
        ValidateSceneSprite(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Currency/Panel_CurrentAmount", art.CurrencyDiamond);
        ValidateSceneSprite(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Reroll/Button_Reroll", art.RerollDiamond);
        ValidateSceneSprite(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Reroll/RerollValue_Frame", art.CostPlate);
        ValidateSceneSprite(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/StartCombatButton/Button_BattleStart", art.StartCombat);
        CaptureOriginalActionIcons(scene);
        GameObject hud = Load<GameObject>(BATTLE_HUD_PREFAB);
        RectTransform trackRect = NeedRect(hud.transform, "ExperienceTrack");
        RectTransform fillRect = NeedRect(hud.transform, "ExperienceFill");
        if (fill.transform.GetSiblingIndex() <= trackRect.GetSiblingIndex() ||
            fillRect.sizeDelta.x >= trackRect.sizeDelta.x || fillRect.sizeDelta.y >= trackRect.sizeDelta.y)
            throw new InvalidOperationException("레벨 바 Fill의 계층 또는 테두리 안쪽 여백이 올바르지 않습니다.");
        RectTransform settingsRect = settings.rectTransform;
        if (!Mathf.Approximately(settingsRect.sizeDelta.x, settingsRect.sizeDelta.y))
            throw new InvalidOperationException("메뉴 마름모의 가로/세로 크기가 일치하지 않습니다.");
        foreach (Sprite diamond in new[] { art.DiamondLarge, art.CurrencyDiamond, art.RerollDiamond,
            art.WaveToggle, art.MenuDiamond, art.SmallDiamond })
            if (!Mathf.Approximately(diamond.rect.width, diamond.rect.height))
                throw new InvalidOperationException("정사각형이 아닌 마름모 원본입니다: " + diamond.name);
    }

    private static void ValidateImporterSettings()
    {
        ValidateImporter(TOP_HUD, new Vector4(55, 14, 55, 14));
        ValidateImporter(WAVE_PREVIEW, new Vector4(35, 18, 35, 18));
        ValidateImporter(SYNERGY_NAMEPLATE, new Vector4(12, 12, 12, 12));
        ValidateImporter(DIAMOND_LARGE, Vector4.zero);
        ValidateImporter(WAVE_TOGGLE, Vector4.zero);
        ValidateImporter(MENU_DIAMOND, Vector4.zero);
        ValidateImporter(LEVEL_TRACK, Vector4.zero);
        ValidateImporter(LEVEL_FILL, Vector4.zero);
        ValidateImporter(BOTTOM_HUD, new Vector4(0, 12, 0, 48));
        ValidateImporter(START_COMBAT, Vector4.zero);
        ValidateImporter(COST_PLATE, Vector4.zero);
        ValidateImporter(DIVIDER, Vector4.zero);
        foreach (string path in AdditionalArtPaths())
            ValidateImporter(path, Vector4.zero);
    }

    private static void ValidateImporter(string path, Vector4 expectedBorder)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("TextureImporter를 불러올 수 없습니다: " + path);

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool isValid = importer.textureType == TextureImporterType.Sprite &&
                       importer.spriteImportMode == SpriteImportMode.Single &&
                       importer.spriteBorder == expectedBorder &&
                       settings.spriteAlignment == (int)SpriteAlignment.Center &&
                       settings.spritePivot == new Vector2(0.5f, 0.5f) &&
                       settings.spriteMeshType == SpriteMeshType.FullRect &&
                       Mathf.Approximately(importer.spritePixelsPerUnit, 100) &&
                       importer.alphaSource == TextureImporterAlphaSource.FromInput &&
                       importer.alphaIsTransparency &&
                       !importer.mipmapEnabled &&
                       importer.filterMode == FilterMode.Bilinear &&
                       importer.textureCompression == TextureImporterCompression.Uncompressed &&
                       !importer.crunchedCompression &&
                       importer.wrapMode == TextureWrapMode.Clamp &&
                       importer.npotScale == TextureImporterNPOTScale.None &&
                       importer.maxTextureSize == 2048;

        if (!isValid)
            throw new InvalidOperationException("UI Sprite Import 설정 불일치: " + path);
    }

    private static void ValidatePrefabSprite(string prefabPath, string imagePath, Sprite expected)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Image image = NeedImage(root.transform, imagePath);
            if (image.sprite != expected)
                throw new InvalidOperationException(prefabPath + "의 Sprite 연결 불일치: " + imagePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Image LoadPrefabImage(string prefabPath, string imagePath)
    {
        GameObject prefab = Load<GameObject>(prefabPath);
        return NeedImage(prefab.transform, imagePath);
    }

    private static void ValidateSceneSprite(Scene scene, string path, Sprite expected)
    {
        Image image = NeedComponent<Image>(NeedSceneTransform(scene, path));
        if (image.sprite != expected)
            throw new InvalidOperationException("InGame Scene Sprite 연결 불일치: " + path);
    }

    private static void ConfigureDivider(Transform owner, string path, Sprite sprite, float x)
    {
        Image divider = NeedImage(owner, path);
        PlaceTopLeft(divider.rectTransform, x, 18, 4, 46);
        ConfigureImage(divider, sprite, Image.Type.Simple, false, false);
    }

    private static void PlaceDecoration(Transform parent, string name, Sprite sprite,
        float x, float y, float width, float height, bool preserveAspect = true)
    {
        Transform target = parent.Find(name);
        if (target == null)
        {
            GameObject decoration = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            decoration.transform.SetParent(parent, false);
            target = decoration.transform;
        }
        Image image = NeedComponent<Image>(target);
        target.gameObject.SetActive(true);
        PlaceTopLeft(image.rectTransform, x, y, width, height);
        ConfigureImage(image, sprite, Image.Type.Simple, preserveAspect, false);
        target.SetAsLastSibling();
    }

    private static void ConfigureImage(Image image, Sprite sprite, Image.Type type, bool preserveAspect, bool raycastTarget)
    {
        image.enabled = true;
        image.overrideSprite = null;
        image.sprite = sprite;
        image.type = type;
        image.preserveAspect = preserveAspect;
        image.color = Color.white;
        image.material = null;
        image.raycastTarget = raycastTarget;
    }

    private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void CenterInParent(RectTransform rect, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void CenterAt(RectTransform rect, float x, float y, float width, float height)
    {
        CenterInParent(rect, width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }

    private static string[] CaptureOriginalActionIcons(Scene scene)
    {
        Transform root = NeedSceneTransform(scene, "BattleInGameUI/UI_BattleScreens/Canvas_Preparation");
        string[] paths = { "Currency/Icon", "Reroll/Icon", "Reroll/RerollValue_Icon", "StartCombatButton/Icon" };
        string[] expectedPaths = { "Assets/06.UI/BattleMutedPreview/LowerLeftReference/Icon_Flame.png",
            "Assets/06.UI/BattleMutedPreview/LowerLeftReference/Icon_Reroll.png",
            "Assets/06.UI/BattleMutedPreview/LowerLeftReference/Icon_Flame.png",
            "Assets/06.UI/BattleMutedPreview/Sprites/Icon_CrossedSwords.png" };
        List<string> result = new List<string>();
        for (int i = 0; i < paths.Length; i++)
        {
            Image icon = NeedImage(root, paths[i]);
            string assetPath = AssetDatabase.GetAssetPath(icon.sprite);
            if (assetPath != expectedPaths[i])
                throw new InvalidOperationException("원본 하단 아이콘 연결을 먼저 확인해 주세요: " + paths[i]);
            result.Add(paths[i] + "|" + GetObjectIdentity(icon.sprite) + "|" + icon.color + "|" +
                EditorJsonUtility.ToJson(AssetImporter.GetAtPath(assetPath)));
        }
        return result.ToArray();
    }

    private static void SetActiveIfPresent(Transform owner, string path, bool active)
    {
        Transform target = owner.Find(path);
        if (target != null)
            target.gameObject.SetActive(active);
    }

    private static Transform NeedSceneTransform(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        GameObject root = scene.GetRootGameObjects().SingleOrDefault(item => item.name == parts[0]);
        if (root == null)
            throw new InvalidOperationException("필수 Scene 루트 없음: " + parts[0]);
        string childPath = string.Join("/", parts.Skip(1));
        Transform result = root.transform.Find(childPath);
        if (result == null)
            throw new InvalidOperationException("필수 Scene 오브젝트 없음: " + path);
        return result;
    }

    private static Transform Need(Transform owner, string path)
    {
        Transform result = owner.Find(path);
        if (result == null)
            throw new InvalidOperationException("필수 UI 오브젝트 없음: " + owner.name + "/" + path);
        return result;
    }

    private static RectTransform NeedRect(Transform owner, string path)
    {
        return NeedRect(Need(owner, path));
    }

    private static RectTransform NeedRect(Transform target)
    {
        RectTransform result = target as RectTransform;
        if (result == null)
            throw new InvalidOperationException("RectTransform이 없습니다: " + target.name);
        return result;
    }

    private static Image NeedImage(Transform owner, string path)
    {
        return NeedComponent<Image>(Need(owner, path));
    }

    private static T NeedComponent<T>(Transform target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
            throw new InvalidOperationException(typeof(T).Name + " 컴포넌트가 없습니다: " + target.name);
        return component;
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException("필수 Asset을 불러올 수 없습니다: " + path);
        return asset;
    }

    private static void SavePrefab(GameObject root, string path)
    {
        bool saved;
        PrefabUtility.SaveAsPrefabAsset(root, path, out saved);
        if (!saved)
            throw new IOException("Prefab 저장 실패: " + path);
    }

    private static string[] CaptureSceneButtonEvents(Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Button>(true))
            .SelectMany(CaptureButtonEvent)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] CaptureButtonEvents(Transform root)
    {
        return root.GetComponentsInChildren<Button>(true)
            .SelectMany(CaptureButtonEvent)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> CaptureButtonEvent(Button button)
    {
        string path = AnimationUtility.CalculateTransformPath(button.transform, null);
        yield return string.Join("|", path, "button-state", button.interactable.ToString(),
            button.transition.ToString(), GetObjectIdentity(button.targetGraphic),
            button.targetGraphic == null ? "no-graphic" : button.targetGraphic.raycastTarget.ToString());
        SerializedObject serializedButton = new SerializedObject(button);
        SerializedProperty calls = serializedButton.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        if (calls == null)
        {
            yield return path + "|count=" + button.onClick.GetPersistentEventCount();
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                UnityEngine.Object target = button.onClick.GetPersistentTarget(i);
                yield return string.Join("|",
                    path,
                    i.ToString(CultureInfo.InvariantCulture),
                    button.onClick.GetPersistentMethodName(i),
                    button.onClick.GetPersistentListenerState(i).ToString(),
                    GetObjectIdentity(target));
            }

            yield break;
        }

        yield return path + "|count=" + calls.arraySize;
        for (int i = 0; i < calls.arraySize; i++)
        {
            SerializedProperty call = calls.GetArrayElementAtIndex(i);
            SerializedProperty arguments = call.FindPropertyRelative("m_Arguments");
            yield return string.Join("|",
                path,
                i.ToString(CultureInfo.InvariantCulture),
                Encode(call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue),
                GetObjectIdentity(call.FindPropertyRelative("m_Target").objectReferenceValue),
                Encode(call.FindPropertyRelative("m_MethodName").stringValue),
                call.FindPropertyRelative("m_Mode").intValue.ToString(CultureInfo.InvariantCulture),
                GetObjectIdentity(arguments.FindPropertyRelative("m_ObjectArgument").objectReferenceValue),
                Encode(arguments.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue),
                arguments.FindPropertyRelative("m_IntArgument").intValue.ToString(CultureInfo.InvariantCulture),
                arguments.FindPropertyRelative("m_FloatArgument").floatValue.ToString("R", CultureInfo.InvariantCulture),
                Encode(arguments.FindPropertyRelative("m_StringArgument").stringValue),
                arguments.FindPropertyRelative("m_BoolArgument").boolValue ? "1" : "0",
                call.FindPropertyRelative("m_CallState").intValue.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static string GetObjectIdentity(UnityEngine.Object target)
    {
        if (target == null)
            return "null";

        GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(target);
        return target.GetType().FullName + ":" + globalId;
    }

    private static string Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    private static void EnsureButtonEventsUnchanged(Transform root, string[] before, string context)
    {
        string[] after = CaptureButtonEvents(root);
        if (!before.SequenceEqual(after))
            throw new InvalidOperationException("기존 Button 이벤트가 변경되었습니다: " + context);
    }
}
