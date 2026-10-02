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
    private const string CROSSED_SWORDS = "Assets/06.UI/BattleMutedPreview/Sprites/Icon_CrossedSwords.png";

    private const string TOP_HUD = ART_DIRECTORY + "Frame_TopHud_Flat.png";
    private const string WAVE_PREVIEW = ART_DIRECTORY + "Frame_WavePreview_Flat.png";
    private const string SYNERGY_NAMEPLATE = ART_DIRECTORY + "Frame_SynergyNameplate_Flat.png";
    private const string DIAMOND_LARGE = ART_DIRECTORY + "Frame_DiamondLarge_Flat.png";
    private const string WAVE_TOGGLE = ART_DIRECTORY + "Frame_WaveToggle_Flat.png";
    private const string MENU_DIAMOND = ART_DIRECTORY + "Frame_MenuDiamond_Flat.png";
    private const string LEVEL_TRACK = ART_DIRECTORY + "Bar_LevelTrack_Chevron_Flat.png";
    private const string LEVEL_FILL = ART_DIRECTORY + "Bar_LevelFill_Chevron_Flat.png";
    private const string BOTTOM_HUD = ART_DIRECTORY + "Panel_BottomHud_Flat.png";
    private const string START_COMBAT = ART_DIRECTORY + "Frame_StartCombat_Flat.png";
    private const string COST_PLATE = ART_DIRECTORY + "Frame_CostPlate_Flat.png";
    private const string DIVIDER = ART_DIRECTORY + "Divider_Vertical_Flat.png";

    private const string UNDO_LABEL = "전투 UI Flat Reference V1 적용";

    private static readonly Color CHARCOAL = new Color32(17, 17, 18, 255);
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
    }

    [MenuItem(MENU_PATH)]
    public static void Apply()
    {
        Scene scene = ValidateEditorState();
        PreflightSharedPrefabs();
        PreflightScene(scene);
        string[] buttonEventsBefore = CaptureSceneButtonEvents(scene);
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
        Debug.Log("Flat Reference V1 적용 완료: 신규 단색 Sprite 12종, BattleHud/WavePreview/Synergy/GetReady Prefab, InGame 하단 장식. 기존 UI 이벤트와 수치 갱신 구조는 유지했습니다.");
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
        ConfigureImporter(TOP_HUD, new Vector4(80, 28, 80, 28));
        ConfigureImporter(WAVE_PREVIEW, new Vector4(48, 45, 48, 45));
        ConfigureImporter(SYNERGY_NAMEPLATE, new Vector4(48, 28, 62, 28));
        ConfigureImporter(DIAMOND_LARGE, Vector4.zero);
        ConfigureImporter(WAVE_TOGGLE, Vector4.zero);
        ConfigureImporter(MENU_DIAMOND, Vector4.zero);
        ConfigureImporter(LEVEL_TRACK, Vector4.zero);
        ConfigureImporter(LEVEL_FILL, Vector4.zero);
        ConfigureImporter(BOTTOM_HUD, Vector4.zero);
        ConfigureImporter(START_COMBAT, Vector4.zero);
        ConfigureImporter(COST_PLATE, Vector4.zero);
        ConfigureImporter(DIVIDER, Vector4.zero);

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
            CrossedSwords = Load<Sprite>(CROSSED_SWORDS)
        };
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

        foreach (string path in texturePaths)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter))
                throw new InvalidOperationException("필수 UI Texture 또는 TextureImporter가 없습니다: " + path);
        }

        Load<Sprite>(CROSSED_SWORDS);
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
            NeedRect(root, "RemainingTimeText");
        });

        PreflightPrefab(WAVE_PREVIEW_PREFAB, root =>
        {
            NeedImage(root, "Frame/Border");
            Need(root, "HeaderShade");
            Need(root, "HeaderLine");
            NeedComponent<Text>(Need(root, "Title"));
        });

        PreflightPrefab(SYNERGY_TRACKER_PREFAB, root =>
        {
            NeedImage(root, "Frame");
            NeedImage(root, "Nameplate/Body");
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
        importer.filterMode = FilterMode.Point;
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
            RectTransform rootRect = NeedRect(root.transform);
            rootRect.sizeDelta = new Vector2(1640, 79);

            RectTransform body = NeedRect(root.transform, "Body");
            body.sizeDelta = new Vector2(1640, 79);
            RectTransform innerBody = NeedRect(root.transform, "Body/Body");
            innerBody.sizeDelta = new Vector2(1630, 69);
            SetSolidImage(root.transform, "Body/Body", CHARCOAL);

            SetActiveIfPresent(root.transform, "Legacy_Wave", false);
            SetActiveIfPresent(root.transform, "Legacy_Level", false);
            SetActiveIfPresent(root.transform, "Legacy_ExperienceTrack", false);
            SetActiveIfPresent(root.transform, "Legacy_ExperienceFill", false);
            SetActiveIfPresent(root.transform, "Legacy_ElapsedTime", false);

            Image border = NeedImage(root.transform, "Body/Border");
            border.rectTransform.sizeDelta = new Vector2(1640, 79);
            ConfigureImage(border, art.TopHud, Image.Type.Sliced, false, false);

            Image waveIcon = NeedImage(root.transform, "WaveIcon");
            ConfigureImage(waveIcon, art.CrossedSwords, Image.Type.Simple, true, false);
            waveIcon.color = IVORY;

            RectTransform toggleRect = NeedRect(root.transform, "WaveToggleButton");
            PlaceTopLeft(toggleRect, 428, 15, 50, 50);
            Image toggleImage = NeedImage(root.transform, "WaveToggleButton");
            ConfigureImage(toggleImage, art.WaveToggle, Image.Type.Simple, true, true);
            Button toggleButton = NeedComponent<Button>(toggleRect);
            toggleButton.targetGraphic = toggleImage;

            RectTransform arrow = NeedRect(toggleRect, "Arrow");
            CenterInParent(arrow, 26, 26);
            NeedImage(toggleRect, "Arrow").raycastTarget = false;

            ConfigureDivider(root.transform, "Divider_Wave", art.Divider, 500);
            ConfigureDivider(root.transform, "Divider_Level", art.Divider, 1250);
            Need(root.transform, "Divider_Menu").gameObject.SetActive(false);

            PlaceTopLeft(NeedRect(root.transform, "LevelText"), 550, 10, 140, 60);

            Image track = NeedImage(root.transform, "ExperienceTrack");
            PlaceTopLeft(track.rectTransform, 690, 27, 530, 27);
            ConfigureImage(track, art.LevelTrack, Image.Type.Simple, false, false);

            Image fill = NeedImage(root.transform, "ExperienceFill");
            PlaceTopLeft(fill.rectTransform, 690, 27, 530, 27);
            float fillAmount = fill.fillAmount;
            ConfigureImage(fill, art.LevelFill, Image.Type.Filled, false, false);
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillClockwise = true;
            fill.fillAmount = fillAmount;

            RectTransform ticks = NeedRect(root.transform, "ExperienceTicks");
            PlaceTopLeft(ticks, 690, 27, 530, 27);
            for (int i = 0; i < ticks.childCount; i++)
                ticks.GetChild(i).gameObject.SetActive(false);

            PlaceTopLeft(NeedRect(root.transform, "TimerIcon"), 1295, 16, 45, 48);
            PlaceTopLeft(NeedRect(root.transform, "RemainingTimeText"), 1360, 10, 225, 60);

            EnsureButtonEventsUnchanged(root.transform, eventsBefore, BATTLE_HUD_PREFAB);
            SavePrefab(root, BATTLE_HUD_PREFAB);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplyWavePreviewPrefab(ArtSet art)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WAVE_PREVIEW_PREFAB);
        try
        {
            string[] eventsBefore = CaptureButtonEvents(root.transform);
            Image border = NeedImage(root.transform, "Frame/Border");
            ConfigureImage(border, art.WavePreview, Image.Type.Sliced, false, false);
            Need(root.transform, "HeaderShade").gameObject.SetActive(false);
            Need(root.transform, "HeaderLine").gameObject.SetActive(false);
            Text title = NeedComponent<Text>(Need(root.transform, "Title"));
            title.color = IVORY;
            title.raycastTarget = false;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Separator")
                    continue;
                Image separator = child.GetComponent<Image>();
                if (separator == null)
                    continue;
                separator.sprite = null;
                separator.color = IVORY;
                separator.raycastTarget = false;
            }

            EnsureButtonEventsUnchanged(root.transform, eventsBefore, WAVE_PREVIEW_PREFAB);
            SavePrefab(root, WAVE_PREVIEW_PREFAB);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplySynergyTrackerPrefab(ArtSet art)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SYNERGY_TRACKER_PREFAB);
        try
        {
            string[] eventsBefore = CaptureButtonEvents(root.transform);
            Image frame = NeedImage(root.transform, "Frame");
            Color categoryTint = frame.color;
            ConfigureImage(frame, art.DiamondLarge, Image.Type.Simple, true, true);
            frame.color = categoryTint;
            Button button = root.GetComponent<Button>();
            if (button != null)
                button.targetGraphic = frame;

            Image plate = NeedImage(root.transform, "Nameplate/Body");
            ConfigureImage(plate, art.SynergyNameplate, Image.Type.Sliced, false, false);
            plate.color = Color.white;
            Transform oldBorder = root.transform.Find("Nameplate/Border");
            if (oldBorder != null)
                oldBorder.gameObject.SetActive(false);
            SetActiveIfPresent(root.transform, "Nameplate/ReferenceTop", false);
            SetActiveIfPresent(root.transform, "Nameplate/ReferenceBottom", false);
            SetActiveIfPresent(root.transform, "Nameplate/ReferenceRight", false);
            SetActiveIfPresent(root.transform, "Nameplate/ReferenceLeft", false);

            EnsureButtonEventsUnchanged(root.transform, eventsBefore, SYNERGY_TRACKER_PREFAB);
            SavePrefab(root, SYNERGY_TRACKER_PREFAB);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplyGetReadyPrefab(ArtSet art)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GET_READY_PREFAB);
        try
        {
            string[] eventsBefore = CaptureButtonEvents(root.transform);

            RectTransform hud = NeedRect(root.transform, "BattleHUD");
            PlaceTopLeft(hud, 53, 32, 1640, 79);
            NeedRect(root.transform, "BattleHUD/Body").sizeDelta = new Vector2(1640, 79);
            NeedRect(root.transform, "BattleHUD/Body/Body").sizeDelta = new Vector2(1630, 69);
            NeedRect(root.transform, "BattleHUD/Body/Border").sizeDelta = new Vector2(1640, 79);
            SetActiveIfPresent(root.transform, "BattleHUD/Legacy_Wave", false);
            SetActiveIfPresent(root.transform, "BattleHUD/Legacy_Level", false);
            SetActiveIfPresent(root.transform, "BattleHUD/Legacy_ExperienceTrack", false);
            SetActiveIfPresent(root.transform, "BattleHUD/Legacy_ExperienceFill", false);
            SetActiveIfPresent(root.transform, "BattleHUD/Legacy_ElapsedTime", false);
            SetActiveIfPresent(root.transform, "BattleHUD/Divider_Menu", false);
            Transform nestedTicks = Need(root.transform, "BattleHUD/ExperienceTicks");
            for (int i = 0; i < nestedTicks.childCount; i++)
                nestedTicks.GetChild(i).gameObject.SetActive(false);

            RectTransform settings = NeedRect(root.transform, "SettingsButton");
            PlaceTopLeft(settings, 1750, 32, 112, 79);
            Image settingsImage = NeedImage(root.transform, "SettingsButton");
            ConfigureImage(settingsImage, art.MenuDiamond, Image.Type.Simple, true, true);
            Button settingsButton = NeedComponent<Button>(settings);
            settingsButton.targetGraphic = settingsImage;

            RectTransform menuIcon = NeedRect(settings, "Icon");
            CenterInParent(menuIcon, 42, 34);
            Image iconImage = NeedImage(settings, "Icon");
            iconImage.color = IVORY;
            iconImage.raycastTarget = false;

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

        Transform[] targets = { backplate, currentAmount, reroll, rerollCost, startCombat };
        foreach (Transform target in targets)
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { target, NeedComponent<Image>(target) }, UNDO_LABEL);

        RectTransform backplateRect = NeedRect(backplate);
        backplateRect.anchorMin = new Vector2(0, 0);
        backplateRect.anchorMax = new Vector2(1, 0);
        backplateRect.pivot = new Vector2(0.5f, 0);
        backplateRect.anchoredPosition = Vector2.zero;
        backplateRect.sizeDelta = new Vector2(0, 196);
        backplateRect.localScale = Vector3.one;
        backplateRect.localRotation = Quaternion.identity;
        ConfigureImage(NeedComponent<Image>(backplate), art.BottomHud, Image.Type.Simple, false, false);

        ConfigureImage(NeedComponent<Image>(currentAmount), art.DiamondLarge, Image.Type.Simple, true, false);
        ConfigureImage(NeedComponent<Image>(reroll), art.DiamondLarge, Image.Type.Simple, true, false);
        ConfigureImage(NeedComponent<Image>(rerollCost), art.CostPlate, Image.Type.Simple, true, false);
        ConfigureImage(NeedComponent<Image>(startCombat), art.StartCombat, Image.Type.Simple, true, false);
    }

    private static void ValidateAppliedState(Scene scene, ArtSet art)
    {
        ValidateImporterSettings();
        ValidatePrefabSprite(BATTLE_HUD_PREFAB, "Body/Border", art.TopHud);
        ValidatePrefabSprite(BATTLE_HUD_PREFAB, "WaveToggleButton", art.WaveToggle);
        ValidatePrefabSprite(BATTLE_HUD_PREFAB, "ExperienceTrack", art.LevelTrack);
        ValidatePrefabSprite(BATTLE_HUD_PREFAB, "ExperienceFill", art.LevelFill);
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
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Currency/Panel_CurrentAmount", art.DiamondLarge);
        ValidateSceneSprite(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Reroll/Button_Reroll", art.DiamondLarge);
        ValidateSceneSprite(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/Reroll/RerollValue_Frame", art.CostPlate);
        ValidateSceneSprite(scene,
            "BattleInGameUI/UI_BattleScreens/Canvas_Preparation/StartCombatButton/Button_BattleStart", art.StartCombat);
    }

    private static void ValidateImporterSettings()
    {
        ValidateImporter(TOP_HUD, new Vector4(80, 28, 80, 28));
        ValidateImporter(WAVE_PREVIEW, new Vector4(48, 45, 48, 45));
        ValidateImporter(SYNERGY_NAMEPLATE, new Vector4(48, 28, 62, 28));
        ValidateImporter(DIAMOND_LARGE, Vector4.zero);
        ValidateImporter(WAVE_TOGGLE, Vector4.zero);
        ValidateImporter(MENU_DIAMOND, Vector4.zero);
        ValidateImporter(LEVEL_TRACK, Vector4.zero);
        ValidateImporter(LEVEL_FILL, Vector4.zero);
        ValidateImporter(BOTTOM_HUD, Vector4.zero);
        ValidateImporter(START_COMBAT, Vector4.zero);
        ValidateImporter(COST_PLATE, Vector4.zero);
        ValidateImporter(DIVIDER, Vector4.zero);
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
                       importer.filterMode == FilterMode.Point &&
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
        PlaceTopLeft(divider.rectTransform, x, 18, 8, 44);
        ConfigureImage(divider, sprite, Image.Type.Simple, true, false);
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

    private static void SetSolidImage(Transform owner, string path, Color color)
    {
        Image image = NeedImage(owner, path);
        image.overrideSprite = null;
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
        image.material = null;
        image.raycastTarget = false;
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
