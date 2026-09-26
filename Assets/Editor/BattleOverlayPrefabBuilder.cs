using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OZGL2.Augment;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 승인된 프리뷰 한 씬에만 적용한다. 원본 씬/기존 프리팹/게임플레이 데이터는 수정하지 않는다.
public static class BattleOverlayPrefabBuilder
{
    public const string ROOT = "Assets/06.UI/BattleMutedPreview/Overlays_v1";
    public const string PREFABS = ROOT + "/Prefabs/";
    public const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    private const string LOBBY_SCENE = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private const string SPRITES = "Assets/06.UI/BattleMutedPreview/Sprites/";
    private const string FONT_SOURCE = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/DOSMyungjo.ttf";
    private const string WHITE = "Assets/06.UI/BattleMutedPreview/Reference_v2/Experience_White.png";
    private const string UNDO = "전투 결과/증강 프리팹 적용";
    private static readonly Color IVORY = new Color32(235, 225, 207, 255);
    private static readonly Color GOLD = new Color32(204, 175, 117, 255);
    private static TMP_FontAsset _font;

    [MenuItem("Tools/OZGL2/Battle/Create And Apply Overlay Prefabs")]
    public static void CreateAndApply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            scene.path != SCENE || scene.isDirty)
            throw new InvalidOperationException("저장된 UI_Battle_MutedPreview를 Edit Mode에서 열어 주세요. 미저장 변경은 자동 덮어쓰지 않습니다.");

        Transform popupRoot = RequireRoot(scene, "Canvas_Popups");
        Transform screens = RequireRoot(scene, "UI_BattleScreens");
        Transform uiRoot = RequireRoot(scene, "UI_Root");
        var controller = uiRoot.GetComponent<UIPopupController>();
        var navigator = uiRoot.GetComponent<UISceneNavigator>();
        if (controller == null || navigator == null) throw new InvalidOperationException("기존 팝업/씬 이동 연결이 필요합니다.");
        string[] oldNames = { "Popup_StageClear", "Popup_Defeat", "Popup_AugmentSelection" };
        string[] names = { "Canvas_BattleVictory", "Canvas_BattleDefeat", "Canvas_AugmentSelection" };
        string[] triggers = { "ClearButton", "DefeatButton", "AugmentButton" };
        UIPopupPanel[] oldPanels = oldNames.Select(n => RequireChild(popupRoot, n).GetComponent<UIPopupPanel>()).ToArray();
        Button[] buttons = triggers.Select(n => RequireChild(screens, "Canvas_Combat/PreviewTriggers/" + n).GetComponent<Button>()).ToArray();
        if (oldPanels.Any(p => p == null) || buttons.Any(b => b == null)) throw new InvalidOperationException("기존 미리보기 패널/버튼이 없습니다.");
        foreach (string name in names.Concat(new[] { "Canvas_BattleResultBase", "AugmentChoiceCard" }))
            if (File.Exists(PREFABS + name + ".prefab") || popupRoot.Find(name) != null)
                throw new InvalidOperationException("기존 작업은 덮어쓰지 않습니다: " + name);
        if (File.Exists(ROOT + "/UIAugmentVisualCatalog.asset")) throw new InvalidOperationException("기존 증강 표시 카탈로그를 덮어쓰지 않습니다.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LOBBY_SCENE) == null)
            throw new InvalidOperationException("프리뷰 로비 이동 경로가 유효하지 않습니다: " + LOBBY_SCENE);
        Require<Font>(FONT_SOURCE);
        var choices = new[] { LoadAugment("skill_damage"), LoadAugment("mon_hp"), LoadAugment("mon_speed") };

        EnsureFolder(ROOT + "/Prefabs");
        EnsureFolder(ROOT + "/Fonts");
        ImportArt();
        _font = CreateFont();
        var catalog = CreateCatalog();
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject result = BuildResult(preview);
            SavePrefab(result, "Canvas_BattleResultBase");
            Object.DestroyImmediate(result);
            MakeResultVariant(preview, "Canvas_BattleVictory", eBattleResultState.VICTORY);
            MakeResultVariant(preview, "Canvas_BattleDefeat", eBattleResultState.DEFEAT);
            GameObject card = BuildAugmentCard(preview, catalog, choices[0]);
            SavePrefab(card, "AugmentChoiceCard");
            Object.DestroyImmediate(card);
            GameObject augment = BuildAugmentSelection(preview, catalog, choices);
            SavePrefab(augment, "Canvas_AugmentSelection");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        // 새 파일은 AssetDatabase로 생성하고, 씬 안의 배치와 연결 변경은 Undo로 묶는다.
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO);
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(PREFABS + names[i] + ".prefab"), popupRoot);
                Undo.RegisterCreatedObjectUndo(instance, UNDO);
                var rect = (RectTransform)instance.transform;
                Stretch(rect);
                instance.SetActive(false);
                var panel = instance.GetComponent<UIPopupPanel>();
                if (i < 2)
                {
                    var view = instance.GetComponent<UIBattleResultView>();
                    view.ConfigureNavigation(navigator, LOBBY_SCENE);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                RebindTrigger(buttons[i], controller, oldPanels[i], panel);
                Undo.RecordObject(oldPanels[i].gameObject, UNDO);
                oldPanels[i].gameObject.SetActive(false);
            }
            BattleOverlayCardPlacement.Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("전투 프리뷰 씬 저장 실패");
            Undo.CollapseUndoOperations(group);
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
        Debug.Log("승리/패배(공통 Base의 Variant), 증강 Canvas와 중첩 증강 카드, 기존 준비 카드 3종 적용 완료. " +
            "씬 배치/연결은 Undo 가능. 새 Prefab/SO/Font 파일 및 Importer 설정은 Undo 대상이 아닙니다.");
    }

    private static GameObject BuildResult(Scene scene)
    {
        GameObject root = BeginCanvas(scene, "Canvas_BattleResultBase", out RectTransform content, out UIPopupPanel panel);
        var view = root.AddComponent<UIBattleResultView>();
        var so = new SerializedObject(view);
        Bind(so, "_popup", panel);
        Bind(so, "_rune", Picture(content, "RuneRing", Art("Result/Result_RuneRing"), 0, 292, 440, 440, new Color(1, 1, 1, .55f)));
        // 깃발은 하나의 동일한 자산을 좌우 반전하여 기준점/높이를 공유한다.
        Image left = Picture(content, "BannerLeft", Art("Result/Result_Banner_Defeat"), -420, -56, 264, 528);
        Image right = Picture(content, "BannerRight", Art("Result/Result_Banner_Defeat"), 420, -56, 264, 528);
        right.rectTransform.localScale = new Vector3(-1, 1, 1);
        Bind(so, "_bannerLeft", left); Bind(so, "_bannerRight", right);
        Picture(content, "CrestFrame", Art("Result/Result_CrestFrame"), 0, 292, 400, 400);
        Bind(so, "_crown", Picture(content, "Crown", Art("Result/Result_Crown_Defeat"), 0, 292, 224, 224));
        Bind(so, "_difficultyText", Label(content, "Difficulty", "보통 난이도", 0, 104, 400, 46, 30));
        Bind(so, "_titleAccent", Picture(content, "TitleAccent", Art("Result/Result_TitleAccent"), 0, 26, 576, 192, new Color(1, 1, 1, .45f)));
        Bind(so, "_resultTitleText", Label(content, "ResultTitle", "패배", 0, 25, 700, 140, 104));

        RectTransform board = Group(content, "RecordPanel", 0, -184, 1200, 400);
        Picture(board, "Frame", Art("Result/Result_RecordPanel"), 0, 0, 1200, 400);
        string[] statNames = { "Time", "Kills", "Deployments" };
        string[] labels = { "플레이 시간", "처치 용사 수", "유닛 배치 수" };
        Sprite[] icons = { Require<Sprite>(SPRITES + "Icon_Hourglass.png"), Require<Sprite>(SPRITES + "Icon_Skull.png"), Art("Result/Icon_Deployment") };
        string[] fields = { "_timeText", "_killsText", "_deploymentsText" };
        for (int i = 0; i < 3; i++)
        {
            float x = -368 + i * 368;
            RectTransform stat = Group(board, statNames[i], x, 52, 346, 100);
            Picture(stat, "IconFrame", Require<Sprite>(SPRITES + "Frame_DiamondNeutral.png"), -112, 0, 90, 90);
            Picture(stat, "Icon", icons[i], -112, 0, 46, 46);
            TMP_Text label = Label(stat, "Label", labels[i], 52, 26, 220, 36, 25);
            if (i == 0) Bind(so, "_timeLabelText", label);
            Bind(so, fields[i], Label(stat, "Value", "0", 52, -22, 220, 54, 40));
            if (i < 2) Picture(board, "Divider_" + i, Require<Sprite>(SPRITES + "Divider_Vertical.png"), x + 184, 52, 8, 96);
        }
        Picture(board, "ExperienceDivider", Require<Sprite>(WHITE), 0, -21, 1070, 1, new Color(.48f, .4f, .3f, .8f));
        Label(board, "ExperienceLabel", "얻은 경험치", -443, -82, 170, 42, 27);
        Bind(so, "_rewardText", Label(board, "ExperienceValue", "+ 860 EXP", -255, -82, 196, 42, 30));
        RectTransform xp = Group(board, "Experience", 62, -82, 400, 68);
        RectTransform area = Group(xp, "Track", 0, 0, 366, 25);
        Picture(area, "Background", Require<Sprite>(WHITE), 0, 0, 366, 25, new Color32(41, 41, 42, 255));
        RectTransform fill = Picture(area, "Fill", Require<Sprite>(WHITE), 0, 0, 366, 25, new Color32(141, 47, 54, 255)).rectTransform;
        Stretch(fill);
        Slider slider = xp.gameObject.AddComponent<Slider>();
        slider.fillRect = fill; slider.minValue = 0; slider.maxValue = 1; slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.SetValueWithoutNotify(.6f);
        for (int i = 1; i < 10; i++) Picture(area, "Tick_" + i, Require<Sprite>(WHITE), -183 + i * 36.6f, 0, 3, 25, new Color32(17, 14, 16, 255));
        Picture(xp, "Frame", Art("Result/Result_ExperienceFrame"), 0, 0, 400, 66.667f);
        Bind(so, "_experience", slider);
        Bind(so, "_levelText", Label(board, "Level", "LV. 20", 351, -82, 146, 44, 32));
        Bind(so, "_levelUpText", Label(board, "LevelUp", "레벨 업!", 491, -82, 134, 44, 28, GOLD));
        Image buttonImage = Picture(content, "LobbyButton", Art("Result/Result_LobbyButton"), 0, -410, 430, 161.25f);
        Button button = buttonImage.gameObject.AddComponent<Button>(); SetButton(button, buttonImage);
        Label(buttonImage.transform, "Text", "로비로", 0, 0, 276, 60, 43);
        Bind(so, "_lobbyButton", button);
        Bind(so, "_victoryCrown", Art("Result/Result_Crown_Victory"));
        Bind(so, "_defeatCrown", Art("Result/Result_Crown_Defeat"));
        Bind(so, "_victoryBanner", Art("Result/Result_Banner_Victory"));
        Bind(so, "_defeatBanner", Art("Result/Result_Banner_Defeat"));
        so.ApplyModifiedPropertiesWithoutUndo();
        SetFirstSelected(panel, button);
        view.RefreshView();
        return root;
    }

    private static void MakeResultVariant(Scene scene, string name, eBattleResultState state)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(PREFABS + "Canvas_BattleResultBase.prefab"), scene);
        go.name = name;
        var view = go.GetComponent<UIBattleResultView>();
        view.SetState(state);
        PrefabUtility.RecordPrefabInstancePropertyModifications(view);
        foreach (Component c in go.GetComponentsInChildren<Component>(true))
            if (c != null) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        SavePrefab(go, name);
        Object.DestroyImmediate(go);
    }

    private static GameObject BuildAugmentCard(Scene scene, UIAugmentVisualCatalogSO catalog, AugmentData choice)
    {
        var root = new GameObject("AugmentChoiceCard", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.layer = 5;
        var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(600, 1100);
        Image hitArea = root.AddComponent<Image>(); hitArea.color = Color.clear; hitArea.raycastTarget = true;
        Button button = root.AddComponent<Button>(); button.targetGraphic = hitArea; button.transition = Selectable.Transition.None;
        Image velvet = Picture(rect, "VelvetBackground", Art("Augment/Separated/VelvetBackground_Silver"), 0, 0, 600, 1100);
        Image panel = Picture(rect, "DescriptionPanel", Art("Augment/Separated/DescriptionPanel_Silver"), 0, 0, 600, 1100);
        Image crest = Picture(rect, "Crest", Art("Augment/Crest_Silver"), 0, 420, 512, 512);
        Image icon = Picture(crest.transform, "Icon", catalog.GetIcon(choice.augmentId), 0, 36, 140, 140);
        TMP_Text grade = Label(crest.transform, "Grade", "실버", 0, -122, 156, 36, 28);
        TMP_Text name = Label(rect, "Name", choice.displayName, 0, 170, 340, 78, 48);
        Picture(rect, "NameDivider", Require<Sprite>(WHITE), 0, 102, 264, 2, new Color32(150, 127, 89, 255));
        TMP_Text desc = Label(rect, "Description", choice.description, 0, -12, 310, 184, 40);
        desc.enableAutoSizing = true; desc.fontSizeMin = 30; desc.fontSizeMax = 40;
        Picture(rect, "EffectPlate", Art("Augment/Augment_EffectPlate"), 0, -194, 350, 98.4375f);
        TMP_Text effect = Label(rect, "Effect", "런 한정 증강", 0, -194, 300, 52, 32, GOLD);
        Image highlight = Picture(rect, "HoverHighlight", Art("Augment/Crest_Silver"), 0, 420, 512, 512, Color.clear);
        var view = root.AddComponent<UIAugmentCardView>();
        view.Configure(velvet, panel, crest, icon, grade, name, desc, effect, highlight, button);
        view.Bind(choice, catalog);
        return root;
    }

    private static GameObject BuildAugmentSelection(Scene scene, UIAugmentVisualCatalogSO catalog, AugmentData[] choices)
    {
        GameObject root = BeginCanvas(scene, "Canvas_AugmentSelection", out RectTransform content, out UIPopupPanel panel);
        Image title = Picture(content, "TitlePlate", Art("Augment/Augment_TitlePlate"), 0, 385, 760, 237.5f);
        Label(title.transform, "Text", "레 벨 업", 0, -8, 500, 100, 68);
        Label(content, "Instruction", "증강 하나를 선택하세요", 0, 258, 800, 54, 32);
        RectTransform row = Group(content, "Cards", 0, -174, 1380, 660);
        var cards = new UIAugmentCardView[3];
        for (int i = 0; i < cards.Length; i++)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(PREFABS + "AugmentChoiceCard.prefab"), row);
            go.name = "AugmentCard_" + (i + 1);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2((i - 1) * 435, 0);
            rect.localScale = Vector3.one * .60f;
            cards[i] = go.GetComponent<UIAugmentCardView>();
            cards[i].Bind(choices[i], catalog);
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
                if (c != null) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        }
        var view = root.AddComponent<UIAugmentSelectionView>();
        view.Configure(panel, cards, catalog, choices);
        SetFirstSelected(panel, cards[0].Button);
        return root;
    }

    private static GameObject BeginCanvas(Scene scene, string name, out RectTransform content, out UIPopupPanel panel)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.SetActive(false); root.layer = 5;
        Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        ((RectTransform)root.transform).sizeDelta = new Vector2(1920, 1080);
        Image shade = Picture(root.transform, "BackgroundShade", null, 0, 0, 1920, 1080, new Color(0, 0, 0, .78f));
        Stretch(shade.rectTransform); shade.raycastTarget = true;
        content = Group(root.transform, "Content", 0, 0, 1920, 1080);
        panel = root.AddComponent<UIPopupPanel>();
        var so = new SerializedObject(panel); so.FindProperty("_canDismiss").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static UIAugmentVisualCatalogSO CreateCatalog()
    {
        var catalog = ScriptableObject.CreateInstance<UIAugmentVisualCatalogSO>();
        var tiers = new UIAugmentVisualCatalogSO.TierVisual[3];
        string[] names = { "Silver", "Gold", "Platinum" };
        Color[] colors = { IVORY, GOLD, new Color32(206, 233, 238, 255) };
        float[] labels = { -122, -128, -110 };
        for (int i = 0; i < 3; i++) tiers[i] = new UIAugmentVisualCatalogSO.TierVisual(i + 1,
            Art("Augment/Separated/VelvetBackground_" + names[i]), Art("Augment/Separated/DescriptionPanel_" + names[i]),
            Art("Augment/Crest_" + names[i]), colors[i], labels[i]);
        Sprite sword = Require<Sprite>("Assets/06.UI/BattleMutedPreview/Cards_v1/Sprites/Icon_Type_Unit_Diamond_v2.png");
        var icons = new[] { new UIAugmentVisualCatalogSO.IconEntry("mon_hp", Require<Sprite>(SPRITES + "Icon_BoneShield.png")) };
        catalog.Configure(tiers, icons, sword);
        AssetDatabase.CreateAsset(catalog, ROOT + "/UIAugmentVisualCatalog.asset");
        return catalog;
    }

    private static TMP_FontAsset CreateFont()
    {
        string path = ROOT + "/Fonts/BattleOverlay Pixel.asset";
        if (File.Exists(path)) throw new InvalidOperationException("이미 준비된 전용 폰트가 있습니다: " + path);
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(Require<Font>(FONT_SOURCE), 90, 4, GlyphRenderMode.RASTER,
            2048, 2048, AtlasPopulationMode.Dynamic, true);
        if (font == null) throw new InvalidOperationException("전용 TMP 폰트를 만들지 못했습니다.");
        font.name = "BattleOverlay Pixel";
        string text = "승리패배전투보통난이도클리어생존플레이시간처치용사수유닛배개명얻은경험레벨업로비증강하나를선택하세요실버골드플래티넘런한정즉시효과LV.EXP0123456789 +%:-/!";
        foreach (string guid in AssetDatabase.FindAssets("t:AugmentData"))
        {
            var data = Require<AugmentData>(AssetDatabase.GUIDToAssetPath(guid));
            text += data.displayName + data.description;
        }
        text = new string(text.Where(c => !char.IsControl(c)).Distinct().ToArray());
        if (!font.TryAddCharacters(text, out string missing) && !string.IsNullOrEmpty(missing))
        {
            // 명조 도트 원본에 없는 수학 기호만 전용 SDF 폰트로 보완한다.
            string fallbackPath = ROOT + "/Fonts/BattleOverlay Symbols SDF.asset";
            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fallbackPath);
            bool isNewFallback = fallback == null;
            if (isNewFallback)
            {
                fallback = TMP_FontAsset.CreateFontAsset(Require<Font>(
                    "Assets/98.ExternalAssets/00.LocalStaging/01.Font/NotoSansCJKkr-Regular.otf"),
                    90, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (fallback == null) throw new InvalidOperationException("보조 SDF 폰트 생성 실패");
                fallback.name = "BattleOverlay Symbols SDF";
            }
            string pending = new string(missing.Where(c => !fallback.HasCharacter(c)).ToArray());
            if (pending.Length > 0 && !fallback.TryAddCharacters(pending, out string unavailable))
                throw new InvalidOperationException("보조 폰트에도 없는 글자: " + unavailable);
            if (isNewFallback) AssetDatabase.CreateAsset(fallback, fallbackPath);
            foreach (Texture2D atlas in fallback.atlasTextures)
                if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, fallback);
            if (!AssetDatabase.Contains(fallback.material)) AssetDatabase.AddObjectToAsset(fallback.material, fallback);
            EditorUtility.SetDirty(fallback); AssetDatabase.SaveAssetIfDirty(fallback);
            if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            font.fallbackFontAssetTable.Add(fallback);
        }
        AssetDatabase.CreateAsset(font, path);
        foreach (Texture2D atlas in font.atlasTextures)
        {
            atlas.filterMode = FilterMode.Point;
            if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
        }
        AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
        return font;
    }

    private static void ImportArt()
    {
        foreach (string path in Directory.GetFiles(ROOT, "*.png", SearchOption.AllDirectories))
        {
            string normalized = path.Replace('\\', '/');
            // 결합 원본은 그대로 둔다. 이번 화면에서 사용하는 분리 자산만 설정한다.
            if (normalized.Contains("Augment_Body_")) continue;
            var importer = AssetImporter.GetAtPath(normalized) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("PNG importer 없음: " + normalized);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    private static void RebindTrigger(Button button, UIPopupController controller, UIPopupPanel oldPanel, UIPopupPanel newPanel)
    {
        Undo.RecordObject(button, UNDO);
        var so = new SerializedObject(button);
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        bool replaced = false;
        for (int i = 0; i < calls.arraySize; i++)
        {
            var call = calls.GetArrayElementAtIndex(i);
            if (call.FindPropertyRelative("m_Target").objectReferenceValue != controller ||
                call.FindPropertyRelative("m_MethodName").stringValue != "OpenPopup" ||
                call.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue != oldPanel) continue;
            call.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue = newPanel;
            replaced = true;
        }
        if (!replaced) throw new InvalidOperationException("기존 열기 이벤트를 찾지 못했습니다: " + button.name);
        so.ApplyModifiedProperties();
    }

    private static RectTransform Group(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var r = (RectTransform)go.transform; r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        return r;
    }
    private static Image Picture(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, Color? color = null)
    {
        Image image = Group(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = color ?? Color.white;
        image.preserveAspect = sprite != null && AssetDatabase.GetAssetPath(sprite) != WHITE; image.raycastTarget = false;
        return image;
    }
    private static TMP_Text Label(Transform parent, string name, string text, float x, float y, float w, float h, float size, Color? color = null)
    {
        var label = Group(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font; label.text = text; label.fontSize = size; label.color = color ?? IVORY;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis;
        label.margin = Vector4.zero;
        return label;
    }
    private static void SetButton(Button button, Image target)
    {
        target.raycastTarget = true; button.targetGraphic = target;
        ColorBlock colors = button.colors; colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.12f, 1.06f, 1); colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.72f, .67f, .63f, 1); colors.fadeDuration = .1f; button.colors = colors;
    }
    private static void SetFirstSelected(UIPopupPanel panel, Button button)
    {
        var so = new SerializedObject(panel); so.FindProperty("_firstSelected").objectReferenceValue = button;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
    private static void SavePrefab(GameObject root, string name)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PREFABS + name + ".prefab", out bool success);
        if (!success || saved == null) throw new IOException("Prefab 저장 실패: " + name);
    }
    private static void Bind(SerializedObject so, string field, Object value)
    {
        SerializedProperty property = so.FindProperty(field);
        if (property == null) throw new InvalidOperationException("필드 없음: " + field);
        property.objectReferenceValue = value;
    }
    private static Sprite Art(string name) => Require<Sprite>(ROOT + "/" + name + ".png");
    private static AugmentData LoadAugment(string id) => Require<AugmentData>("Assets/01.Scripts/Sandbox/Resources/Augments/SO_Augment_" + id + ".asset");
    private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("필수 에셋 없음: " + path);
    private static Transform RequireRoot(Scene scene, string name) => scene.GetRootGameObjects().Single(g => g.name == name).transform;
    private static Transform RequireChild(Transform root, string path) => root.Find(path) ?? throw new InvalidOperationException("오브젝트 없음: " + path);
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
