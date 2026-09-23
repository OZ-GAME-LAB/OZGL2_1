using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using OZGL2.UIFlow;

// 원본 씬/제외 대상은 유지하고 승인된 복제 씬의 표시 레이어만 구성한다.
public static class BattleMutedPreviewBuilder
{
    public const string SOURCE_SCENE = "Assets/00.Scenes/UI_Flow/UI_Battle.unity";
    public const string PREVIEW_SCENE = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    public const string SPRITES = "Assets/06.UI/BattleMutedPreview/Sprites/";
    private const string FONT_PATH = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/DOSGothic.ttf";
    private static readonly Color INK = new Color(0.018f, 0.017f, 0.022f, 0.97f);
    private static readonly Color IVORY = new Color(0.95f, 0.92f, 0.84f);
    private static Font _font;

    [MenuItem("Tools/OZGL2/Battle/Create Muted Preview Scene")]
    public static void CreatePreviewScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode에서 실행해 주세요.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("미저장 씬을 먼저 저장하거나 변경을 보존해 주세요.");
        if (File.Exists(PREVIEW_SCENE))
            throw new InvalidOperationException("기존 프리뷰 씬은 덮어쓰지 않습니다.");
        _font = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
        if (_font == null) throw new InvalidOperationException("기존 DOSGothic 원본 폰트가 필요합니다: " + FONT_PATH);
        ConfigureSprites();
        if (!AssetDatabase.CopyAsset(SOURCE_SCENE, PREVIEW_SCENE))
            throw new InvalidOperationException("원본 전투 씬 복제 실패");
        Scene scene = EditorSceneManager.OpenScene(PREVIEW_SCENE, OpenSceneMode.Single);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("전투 Muted Preview 표시 레이어 배치");
        Build(scene);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("BattleMutedPreview: 복제 씬의 분리 UI 배치 및 저장 완료. 카드/타일/유닛/팝업 원본 유지.");
    }

    public static void ConfigureSprites()
    {
        if (!Directory.Exists(SPRITES)) throw new DirectoryNotFoundException(SPRITES);
        foreach (string raw in Directory.GetFiles(SPRITES, "*.png"))
        {
            string path = raw.Replace('\\', '/');
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            string name = Path.GetFileNameWithoutExtension(path);
            importer.spriteBorder = name == "Frame_Hud" ? new Vector4(42, 38, 42, 38)
                : name == "Frame_Wave" ? new Vector4(66, 66, 66, 66)
                : name == "Frame_SynergyRow" ? new Vector4(44, 40, 44, 40)
                : name == "Frame_CostPlate" ? new Vector4(58, 52, 58, 52) : Vector4.zero;
            importer.SaveAndReimport();
        }
    }

    private static void Build(Scene scene)
    {
        Transform screens = Root(scene, "UI_BattleScreens");
        Transform ready = screens.Find("Canvas_GetReady");
        Transform prep = screens.Find("Canvas_Preparation");
        Transform combat = screens.Find("Canvas_Combat");
        if (ready == null || prep == null || combat == null) throw new InvalidOperationException("원본 페이지 구조가 다릅니다.");
        Transform uiRoot = Root(scene, "UI_Root");
        UIPopupController popups = uiRoot.GetComponent<UIPopupController>();
        UIPopupPanel synergyPopup = Root(scene, "Canvas_Popups").Find("Popup_Synergy").GetComponent<UIPopupPanel>();

        Image background = ready.Find("Background").GetComponent<Image>();
        Undo.RecordObject(background, "공통 화면 배경색");
        background.color = new Color(0.019f, 0.019f, 0.024f, 1f);
        background.raycastTarget = false;

        RectTransform hud = ResetVisuals(ready.Find("BattleHUD"), 26, 22, 1868, 91);
        Panel(hud, "Body", "Frame_Hud", 0, 0, 1868, 91);
        Icon(hud, "WaveIcon", "Icon_Skull", 28, 24, 48, 48);
        Text wave = Label(hud, "WaveText", "WAVE 3 / 10", 93, 16, 365, 60, 40, TextAnchor.MiddleLeft);
        Divider(hud, "Divider_Wave", 492, 20, 6, 50);
        Text level = Label(hud, "LevelText", "LV. 20", 529, 16, 174, 60, 40, TextAnchor.MiddleLeft);

        Image track = Picture(hud, "ExperienceTrack", Sprite("Experience_Track"), 720, 29, 433, 32);
        if (track.sprite == null) throw new InvalidOperationException("기존 로비 경험치 Track이 필요합니다.");
        track.type = Image.Type.Sliced;
        Image fill = Picture(hud, "ExperienceFill", Sprite("Experience_Fill"), 724, 33, 425, 24);
        if (fill.sprite == null) throw new InvalidOperationException("기존 로비 경험치 Fill이 필요합니다.");
        RectTransform ticks = Group(hud, "ExperienceTicks", 724, 33, 425, 24);
        for (int i = 1; i < 10; i++)
        {
            Image tick = Picture(ticks, "Tick_" + i, null, i * 42.5f - 2, 0, 4, 24);
            tick.color = new Color(0.025f, 0.023f, 0.025f, 1f);
        }
        Divider(hud, "Divider_Level", 1198, 20, 6, 50);
        Icon(hud, "TimerIcon", "Icon_Hourglass", 1254, 20, 49, 51);
        Text time = Label(hud, "RemainingTimeText", "00:42", 1318, 16, 245, 60, 42, TextAnchor.MiddleCenter);
        Divider(hud, "Divider_Menu", 1644, 20, 6, 50);
        RectTransform menu = ResetVisuals(ready.Find("SettingsButton"), 1724, 28, 150, 77);
        Image menuIcon = Icon(menu, "Icon", "Icon_Menu", 48, 18, 49, 42);
        StyleButton(menu.GetComponent<Button>(), menuIcon);

        RectTransform incoming = Group(ready, "WavePreview", 42, 132, 618, 179);
        Panel(incoming, "Frame", "Frame_Wave", 0, 0, 618, 179);
        Image headerShade = Picture(incoming, "HeaderShade", null, 24, 17, 570, 40);
        headerShade.color = new Color(0.21f, 0.045f, 0.065f, 0.45f);
        Label(incoming, "Title", "이번 웨이브", 175, 12, 265, 44, 30, TextAnchor.MiddleCenter);
        Picture(incoming, "HeaderLine", null, 24, 57, 570, 1).color = new Color(0.45f, 0.32f, 0.28f);
        Text[] enemyNames = new Text[3];
        Text[] enemyCounts = new Text[3];
        string[] enemies = { "Icon_EnemyMelee", "Icon_EnemyRanged", "Icon_EnemyElite" };
        for (int i = 0; i < 3; i++)
        {
            RectTransform row = Group(incoming, "Enemy_" + i, 20 + i * 194, 61, 190, 103);
            Icon(row, "Icon", enemies[i], 18, 0, 65, 69);
            enemyCounts[i] = Label(row, "Count", "", 90, 21, 90, 43, 30, TextAnchor.MiddleCenter);
            enemyNames[i] = Label(row, "Name", "", 0, 72, 185, 29, 23, TextAnchor.MiddleCenter);
            if (i > 0) Picture(row, "Separator", null, -6, 0, 1, 96).color = new Color(0.38f, 0.22f, 0.22f);
        }

        // 이전 증강 상세용 자리표시자는 삭제하지 않고 보관한다.
        Hide(ready.Find("SynergySummary"));
        RectTransform synergies = Group(ready, "SynergyTrackers", 1665, 209, 237, 555);
        string[] synergyIcons = { "Icon_Arcane", "Icon_Bow", "Icon_BoneShield", "Icon_CursedEye" };
        Color[] colors = { new Color(0.66f, 0.44f, 0.80f), new Color(0.80f, 0.64f, 0.38f), IVORY, new Color(0.72f, 0.29f, 0.51f) };
        Text[] synergyNames = new Text[4];
        Text[] synergyThresholds = new Text[4];
        for (int i = 0; i < 4; i++)
        {
            RectTransform row = Group(synergies, "Synergy_" + i, 0, 138 * i, 237, 112);
            Image hit = Undo.AddComponent<Image>(row.gameObject);
            hit.color = Color.clear;
            Button button = Undo.AddComponent<Button>(row.gameObject);
            Panel(row, "Nameplate", "Frame_SynergyRow", 57, 10, 180, 94);
            DiamondBody(row, 6, 4, 107);
            Image diamond = Icon(row, "Frame", "Frame_DiamondNeutral", 0, 0, 119, 119);
            diamond.color = colors[i];
            Icon(row, "Icon", synergyIcons[i], 36, 33, 47, 53);
            synergyNames[i] = Label(row, "Name", "", 106, 20, 125, 31, 23, TextAnchor.MiddleCenter);
            synergyThresholds[i] = Label(row, "Thresholds", "", 111, 58, 111, 36, 31, TextAnchor.MiddleCenter);
            StyleButton(button, diamond);
            if (popups != null && synergyPopup != null)
                UnityEventTools.AddObjectPersistentListener<UIPopupPanel>(button.onClick, popups.OpenPopup, synergyPopup);
        }

        // 참고 이미지에 없는 설명/배경 자리표시자는 복제 씬에서만 숨김. 원본과 자식은 그대로 보존한다.
        Hide(prep.Find("IncomingDirection"));
        Hide(prep.Find("Tutorial"));
        Hide(prep.Find("TutorialPortrait_Placeholder"));
        Hide(prep.Find("PreparationControls"));

        RectTransform currency = ResetVisuals(prep.Find("Currency"), 24, 790, 268, 268);
        DiamondBody(currency, 0, 0, 268);
        Image currencyFrame = Icon(currency, "Frame", "Frame_DiamondRed", 0, 0, 268, 268);
        Icon(currency, "Icon", "Icon_Flame", 91, 62, 85, 90);
        Text cost = Label(currency, "Value", "100", 67, 158, 134, 61, 50, TextAnchor.MiddleCenter);
        StyleButton(currency.GetComponent<Button>(), currencyFrame);

        RectTransform start = ResetVisuals(prep.Find("StartCombatButton"), 1628, 790, 268, 268);
        DiamondBody(start, 0, 0, 268);
        Image startFrame = Icon(start, "Frame", "Frame_DiamondRed", 0, 0, 268, 268);
        Icon(start, "Icon", "Icon_CrossedSwords", 87, 68, 94, 88);
        Label(start, "Title", "전투 시작", 45, 163, 178, 45, 30, TextAnchor.MiddleCenter);
        StyleButton(start.GetComponent<Button>(), startFrame);

        RectTransform reroll = ResetVisuals(prep.Find("Reroll_Placeholder"), 311, 878, 146, 146);
        DiamondBody(reroll, 0, 0, 146);
        Image rerollFrame = Icon(reroll, "Frame", "Frame_DiamondNeutral", 0, 0, 146, 146);
        Icon(reroll, "Icon", "Icon_Reroll", 40, 40, 66, 66);
        StyleButton(reroll.GetComponent<Button>(), rerollFrame);
        RectTransform price = Group(prep, "RerollPrice", 296, 1025, 177, 50);
        Panel(price, "Frame", "Frame_CostPlate", 0, 0, 177, 50);
        Icon(price, "CurrencyIcon", "Icon_CostGem", 20, 11, 28, 28);
        Text rerollCost = prep.Find("RerollCost").GetComponent<Text>();
        Undo.RecordObject(rerollCost, "리롤 가격 표시");
        Undo.RecordObject(rerollCost.rectTransform, "리롤 가격 위치");
        Rect(rerollCost.rectTransform, 346, 1030, 115, 39);
        FontStyle(rerollCost, 32, TextAnchor.MiddleCenter);

        UIBattleMutedPreviewView view = Undo.AddComponent<UIBattleMutedPreviewView>(screens.gameObject);
        view.Configure(wave, level, time, cost, rerollCost, fill, enemyNames, enemyCounts, synergyNames, synergyThresholds);
        ApplyVisualSpacing(screens);
        EditorUtility.SetDirty(view);
        Selection.activeGameObject = screens.gameObject;
    }

    private static Transform Root(Scene scene, string name)
    {
        GameObject go = scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);
        if (go == null) throw new InvalidOperationException("필수 루트 없음: " + name);
        return go.transform;
    }

    private static void Hide(Transform target)
    {
        if (target == null) return;
        Undo.RecordObject(target.gameObject, "기존 자리표시자 보관");
        target.gameObject.SetActive(false);
    }

    private static RectTransform ResetVisuals(Transform target, float x, float y, float w, float h)
    {
        if (target == null) throw new InvalidOperationException("필수 UI 대상 없음");
        Undo.RegisterFullObjectHierarchyUndo(target.gameObject, "원본 이벤트 유지 / 표시 교체");
        foreach (Transform child in target)
        {
            child.gameObject.SetActive(false);
            if (!child.name.StartsWith("Legacy_")) child.name = "Legacy_" + child.name;
        }
        RectTransform rt = target as RectTransform;
        Rect(rt, x, y, w, h);
        Image image = target.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.color = Color.clear;
            image.raycastTarget = target.GetComponent<Button>() != null;
        }
        return rt;
    }

    private static RectTransform Group(Transform parent, string name, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "분리 UI 생성");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        Rect(rt, x, y, w, h);
        return rt;
    }

    private static void Rect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static Image Picture(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
    {
        Image image = Undo.AddComponent<Image>(Group(parent, name, x, y, w, h).gameObject);
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite Sprite(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + name + ".png");
        if (sprite == null) throw new InvalidOperationException("스프라이트 없음: " + name);
        return sprite;
    }

    private static Image Icon(Transform parent, string name, string asset, float x, float y, float w, float h)
    {
        Image image = Picture(parent, name, Sprite(asset), x, y, w, h);
        image.preserveAspect = true;
        return image;
    }

    private static void Panel(Transform parent, string name, string asset, float x, float y, float w, float h)
    {
        RectTransform panel = Group(parent, name, x, y, w, h);
        Picture(panel, "Body", null, 7, 7, w - 14, h - 14).color = INK;
        Image frame = Picture(panel, "Border", Sprite(asset), 0, 0, w, h);
        frame.type = Image.Type.Sliced;
    }

    private static void DiamondBody(Transform parent, float x, float y, float size)
    {
        float side = size * 0.55f;
        Image body = Picture(parent, "Body", null, 0, 0, side, side);
        RectTransform rt = body.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x + size / 2, -(y + size / 2));
        rt.localRotation = Quaternion.Euler(0, 0, 45);
        body.color = INK;
    }

    private static void Divider(Transform parent, string name, float x, float y, float w, float h)
    {
        Icon(parent, name, "Divider_Vertical", x, y, w, h);
    }

    private static Text Label(Transform parent, string name, string value, float x, float y, float w, float h, int size, TextAnchor align)
    {
        Text text = Undo.AddComponent<Text>(Group(parent, name, x, y, w, h).gameObject);
        text.text = value;
        FontStyle(text, size, align);
        return text;
    }

    private static void FontStyle(Text text, int size, TextAnchor align)
    {
        text.font = _font;
        text.fontSize = size;
        text.color = IVORY;
        text.alignment = align;
        text.fontStyle = UnityEngine.FontStyle.Normal;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = false;
        text.raycastTarget = false;
    }

    private static void StyleButton(Button button, Graphic target)
    {
        if (button == null) return;
        Undo.RecordObject(button, "버튼 표시 상태");
        button.targetGraphic = target;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        // 분류 색상은 Image.color에만 둬 ColorTint가 두 번 곱해지지 않도록 한다.
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        // 원본에서 동작이 없는 Cost/리롤 자리표시자는 기능 상태를 유지하되 아트에 회색을 중복 곱하지 않는다.
        if (!button.interactable)
        {
            button.transition = Selectable.Transition.None;
            target.canvasRenderer.SetColor(Color.white);
        }
    }

    // 9-slice 장식과 텍스트의 여백을 공통 규칙으로 맞춘다. 새 씬 생성 시에도 같은 규칙이 적용된다.
    private static void ApplyVisualSpacing(Transform screens)
    {
        Transform ready = screens.Find("Canvas_GetReady");
        Transform prep = screens.Find("Canvas_Preparation");
        foreach (Transform owner in new[] { ready.Find("BattleHUD"), ready.Find("SettingsButton"), prep.Find("Currency"), prep.Find("Reroll_Placeholder"), prep.Find("StartCombatButton") })
            foreach (Transform child in owner)
                if (!child.gameObject.activeSelf && !child.name.StartsWith("Legacy_")) child.name = "Legacy_" + child.name;
        foreach (Image image in screens.GetComponentsInChildren<Image>(true))
            if (image.name == "Border" && image.sprite != null && AssetDatabase.GetAssetPath(image.sprite).StartsWith(SPRITES))
                image.pixelsPerUnitMultiplier = 2f;
        Image track = ready.Find("BattleHUD/ExperienceTrack").GetComponent<Image>();
        track.sprite = Sprite("Experience_Track");
        track.type = Image.Type.Simple;
        Image fill = ready.Find("BattleHUD/ExperienceFill").GetComponent<Image>();
        fill.sprite = Sprite("Experience_Fill");
        fill.preserveAspect = false;
        Rect(ready.Find("WavePreview/Title") as RectTransform, 175, 22, 265, 34);
        foreach (Transform row in ready.Find("SynergyTrackers"))
        {
            Text title = row.Find("Name").GetComponent<Text>();
            Rect(title.rectTransform, 106, 27, 125, 28);
            title.fontSize = 22;
            Text threshold = row.Find("Thresholds").GetComponent<Text>();
            Rect(threshold.rectTransform, 111, 59, 111, 32);
            threshold.fontSize = 28;
        }
        prep.Find("RerollCost").SetAsLastSibling();
        foreach (string name in new[] { "Currency", "Reroll_Placeholder" })
        {
            Button button = prep.Find(name).GetComponent<Button>();
            StyleButton(button, prep.Find(name + "/Frame").GetComponent<Image>());
        }
        screens.GetComponent<UIBattleMutedPreviewView>().RefreshView();
    }

    [MenuItem("Tools/OZGL2/Battle/Apply Preview Spacing")]
    public static void ApplyPreviewSpacing()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != PREVIEW_SCENE)
            throw new InvalidOperationException("복제 전투 씬의 Edit Mode에서만 실행할 수 있습니다.");
        Transform screens = Root(scene, "UI_BattleScreens");
        Undo.RegisterFullObjectHierarchyUndo(screens.gameObject, "전투 UI 여백 보정");
        ApplyVisualSpacing(screens);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
