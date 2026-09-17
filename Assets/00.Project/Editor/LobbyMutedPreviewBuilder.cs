using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 생성된 로비 원화를 Unity UI 스프라이트로 준비하고, 별도 시안 씬의 로비 캔버스만 배치한다.
/// </summary>
internal static class LobbyMutedPreviewBuilder
{
    private const string ROOT = "Assets/06.UI/LobbyMutedPreview";
    private const string SOURCE = ROOT + "/Source/";
    private const string SPRITES = ROOT + "/Sprites/";
    private const string PREVIEW_SCENE = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private const string FALLBACK_FONT = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/NotoSansCJKkr-Regular.otf";

    private struct Slice
    {
        public string Source;
        public string Output;
        public RectInt Bounds;

        public Slice(string source, string output, int x, int y, int width, int height)
        {
            Source = source;
            Output = output;
            Bounds = new RectInt(x, y, width, height);
        }
    }

    private static readonly Slice[] SLICES =
    {
        new Slice("StageCards_Chroma.png", "Stage_Previous.png", 0, 0, 724, 724),
        new Slice("StageCards_Chroma.png", "Stage_Current.png", 724, 0, 724, 724),
        new Slice("StageCards_Chroma.png", "Stage_Locked.png", 1448, 0, 724, 724),
        new Slice("BottomNav_Chroma.png", "Nav_Achievements.png", 0, 0, 543, 724),
        new Slice("BottomNav_Chroma.png", "Nav_Traits.png", 543, 0, 543, 724),
        new Slice("BottomNav_Chroma.png", "Nav_Skills.png", 1086, 0, 543, 724),
        new Slice("BottomNav_Chroma.png", "Nav_Codex.png", 1629, 0, 543, 724),
        new Slice("Chrome_Chroma.png", "HUD_Level.png", 0, 95, 1070, 280),
        new Slice("Chrome_Chroma.png", "Button_Menu.png", 1090, 100, 570, 260),
        new Slice("Chrome_Chroma.png", "Button_Battle.png", 0, 470, 1080, 360),
        new Slice("Chrome_Chroma.png", "Plate_StageCaption.png", 1090, 540, 570, 255),
        new Slice("StageArrow_Chroma.png", "Button_StageArrow.png", 0, 0, 1254, 1254)
    };

    [MenuItem("Tools/OZGL2/Lobby/Generate Muted Preview Sprites")]
    private static void GenerateSprites()
    {
        Directory.CreateDirectory(ToAbsolute(SPRITES));

        foreach (Slice slice in SLICES)
        {
            GenerateSprite(slice);
        }

        AssetDatabase.Refresh();
        ConfigureSprite(SPRITES + "Lobby_Background.png");
        Debug.Log("Muted lobby preview: 12 UI sprites generated and imported.");
    }

    [MenuItem("Tools/OZGL2/Lobby/Import Revised Button Sprites")]
    private static void ImportRevisedButtonSprites()
    {
        ConfigureSprite(SPRITES + "Button_MenuIcon_v2.png");
        ConfigureSprite(SPRITES + "Button_BattlePlate_v2.png");
        ConfigureSprite(SPRITES + "Button_BattleIcon_v2.png");
        Debug.Log("Muted lobby preview: revised menu and battle button sprites imported.");
    }

    [MenuItem("Tools/OZGL2/Lobby/Import Stage Caption Sprites")]
    private static void ImportStageCaptionSprites()
    {
        ConfigureSprite(SPRITES + "Plate_PreviousStage_v2.png");
        ConfigureSprite(SPRITES + "Plate_LockedStage_v2.png");
        ConfigureSprite(SPRITES + "Plate_CurrentStage_Tall_v2.png");
        Debug.Log("Muted lobby preview: three separate stage caption sprites imported.");
    }

    private static void GenerateSprite(Slice slice)
    {
        string sourcePath = ToAbsolute(SOURCE + slice.Source);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("로비 원본 스프라이트 시트를 찾을 수 없습니다.", sourcePath);
        }

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            source.LoadImage(File.ReadAllBytes(sourcePath));
            RectInt bounds = slice.Bounds;
            if (bounds.xMax > source.width || bounds.yMax > source.height)
            {
                throw new InvalidOperationException(slice.Source + " 시트 크기와 분할 영역이 맞지 않습니다.");
            }

            Color32[] sourcePixels = source.GetPixels32();
            Color32[] cropped = new Color32[bounds.width * bounds.height];
            int minX = bounds.width;
            int minY = bounds.height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < bounds.height; y++)
            {
                int sourceY = source.height - bounds.y - bounds.height + y;
                for (int x = 0; x < bounds.width; x++)
                {
                    Color32 pixel = sourcePixels[sourceY * source.width + bounds.x + x];
                    if (IsChromaGreen(pixel))
                    {
                        pixel.a = 0;
                    }
                    else
                    {
                        pixel.a = 255;
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }

                    cropped[y * bounds.width + x] = pixel;
                }
            }

            if (maxX < minX || maxY < minY)
            {
                throw new InvalidOperationException(slice.Output + "에서 불투명한 스프라이트 픽셀을 찾지 못했습니다.");
            }

            const int padding = 8;
            minX = Mathf.Max(0, minX - padding);
            minY = Mathf.Max(0, minY - padding);
            maxX = Mathf.Min(bounds.width - 1, maxX + padding);
            maxY = Mathf.Min(bounds.height - 1, maxY + padding);
            int outputWidth = maxX - minX + 1;
            int outputHeight = maxY - minY + 1;
            Color32[] outputPixels = new Color32[outputWidth * outputHeight];
            for (int y = 0; y < outputHeight; y++)
            {
                Array.Copy(cropped, (minY + y) * bounds.width + minX,
                    outputPixels, y * outputWidth, outputWidth);
            }

            Texture2D output = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
            try
            {
                output.SetPixels32(outputPixels);
                output.Apply(false);
                string assetPath = SPRITES + slice.Output;
                File.WriteAllBytes(ToAbsolute(assetPath), output.EncodeToPNG());
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ConfigureSprite(assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    private static bool IsChromaGreen(Color32 pixel)
    {
        return pixel.g > pixel.r + 5 && pixel.g > pixel.b + 5 && pixel.g > 15;
    }

    private static void ConfigureSprite(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("스프라이트를 가져오지 못했습니다: " + assetPath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    [MenuItem("Tools/OZGL2/Lobby/Apply Muted Preview Layout")]
    private static void ApplyLayout()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != PREVIEW_SCENE)
        {
            throw new InvalidOperationException("기존 씬 보호: UI_Lobby_MutedPreview.unity에서만 실행할 수 있습니다.");
        }

        GameObject canvas = GameObject.Find("Canvas_Lobby");
        if (canvas == null)
        {
            throw new InvalidOperationException("Canvas_Lobby를 찾을 수 없습니다.");
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply muted lobby preview layout");
        Transform root = canvas.transform;
        Font font = RequireFallbackFont();
        Color ivory = new Color(0.95f, 0.94f, 0.91f, 1f);

        Image background = Require(root, "Background").GetComponent<Image>();
        SetImage(background, "Lobby_Background.png", false);
        SetStretch(background.rectTransform);

        Transform account = Require(root, "AccountStatus");
        SetRect((RectTransform)account, 32, -20, 660, 92);
        SetImage(account.GetComponent<Image>(), "HUD_Level.png", false, false);
        SetActive(Require(account, "DemonIcon_Placeholder"), false);
        SetActive(Require(account, "ExperienceTrack"), false);
        SetActive(Require(account, "ExperienceFill"), false);
        SetText(RequireTextGraphic(Require(account, "Level")), "LV. 20", 36, ivory, TextAnchor.MiddleCenter);
        SetRect((RectTransform)Require(account, "Level"), 45, -15, 150, 60);
        for (int i = 0; i < 8; i++)
        {
            Transform segment = FindOrCreateImage(account, "XP_Segment_" + i);
            SetRect((RectTransform)segment, 215 + i * 50, -34, 43, 25);
            Image image = segment.GetComponent<Image>();
            Undo.RecordObject(image, "Set XP segment color");
            image.sprite = null;
            image.color = i < 5 ? new Color(0.52f, 0.18f, 0.20f, 1f) : new Color(0.19f, 0.19f, 0.20f, 1f);
            image.raycastTarget = false;
        }

        ApplyMenuButton(root);

        Transform stages = Require(root, "StageSelection");
        Transform previous = Require(stages, "PreviousStage");
        Transform current = Require(stages, "CurrentStage");
        Transform locked = Require(stages, "NextStage_Locked");
        SetRect((RectTransform)previous, 250, -208, 430, 430);
        SetRect((RectTransform)current, 690, -120, 540, 540);
        SetRect((RectTransform)locked, 1240, -208, 430, 430);
        SetImage(previous.GetComponent<Image>(), "Stage_Previous.png", false);
        SetImage(current.GetComponent<Image>(), "Stage_Current.png", false);
        SetImage(locked.GetComponent<Image>(), "Stage_Locked.png", false);
        SetActive(Require(previous, "PlaceholderLabel"), false);
        SetActive(Require(current, "PlaceholderLabel"), false);
        SetActive(Require(current, "LowerShade"), false);
        SetActive(Require(locked, "Lock_Placeholder"), false);
        SetActive(Require(locked, "UnlockCondition"), false);
        ApplyStageCaptions(root, font, ivory);

        ApplyBattleButton(current, ivory);

        Transform previousArrow = Require(stages, "PreviousArrow");
        Transform nextArrow = Require(stages, "NextArrow");
        SetRect((RectTransform)previousArrow, 190, -455, 150, 150);
        SetRect((RectTransform)nextArrow, 1730, -455, 150, 150);
        SetImage(previousArrow.GetComponent<Image>(), "Button_StageArrow.png", true);
        SetImage(nextArrow.GetComponent<Image>(), "Button_StageArrow.png", true);
        Undo.RecordObject(previousArrow, "Center previous stage arrow pivot");
        ((RectTransform)previousArrow).pivot = new Vector2(0.5f, 0.5f);
        Undo.RecordObject(nextArrow, "Center next stage arrow pivot");
        ((RectTransform)nextArrow).pivot = new Vector2(0.5f, 0.5f);
        Undo.RecordObject(previousArrow, "Rotate previous stage arrow");
        previousArrow.localRotation = Quaternion.Euler(0, 0, 180);
        Undo.RecordObject(nextArrow, "Rotate next stage arrow");
        nextArrow.localRotation = Quaternion.identity;
        SetActive(Require(previousArrow, "Label"), false);
        SetActive(Require(nextArrow, "Label"), false);

        Transform nav = Require(root, "BottomNavigation");
        SetNavigation(nav, "ReservedButton", "Nav_Achievements.png", "업적", 450, font, ivory);
        SetNavigation(nav, "TraitsButton", "Nav_Traits.png", "특성", 710, font, ivory);
        SetNavigation(nav, "SkillsButton", "Nav_Skills.png", "스킬 세팅", 1000, font, ivory);
        SetNavigation(nav, "CodexButton", "Nav_Codex.png", "도감", 1260, font, ivory);

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("Muted lobby preview: Canvas_Lobby layout applied. Save the separate preview scene after review.");
    }

    [MenuItem("Tools/OZGL2/Lobby/Apply Revised Buttons Only")]
    private static void ApplyRevisedButtonsOnly()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != PREVIEW_SCENE)
        {
            throw new InvalidOperationException("기존 씬 보호: UI_Lobby_MutedPreview.unity에서만 실행할 수 있습니다.");
        }

        GameObject canvas = GameObject.Find("Canvas_Lobby");
        if (canvas == null)
        {
            throw new InvalidOperationException("Canvas_Lobby를 찾을 수 없습니다.");
        }

        Transform root = canvas.transform;
        Transform current = Require(root, "StageSelection/CurrentStage");
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply revised lobby buttons");
        ApplyMenuButton(root);
        ApplyBattleButton(current, new Color(0.95f, 0.94f, 0.91f, 1f));
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("Muted lobby preview: menu and battle buttons updated in the preview scene only.");
    }

    [MenuItem("Tools/OZGL2/Lobby/Apply Stage Captions Only")]
    private static void ApplyStageCaptionsOnly()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != PREVIEW_SCENE)
        {
            throw new InvalidOperationException("기존 씬 보호: UI_Lobby_MutedPreview.unity에서만 실행할 수 있습니다.");
        }

        GameObject canvas = GameObject.Find("Canvas_Lobby");
        if (canvas == null)
        {
            throw new InvalidOperationException("Canvas_Lobby를 찾을 수 없습니다.");
        }

        Transform root = canvas.transform;
        Font font = RequireFallbackFont();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply separate stage caption frames");
        ApplyStageCaptions(root, font, new Color(0.95f, 0.94f, 0.91f, 1f));
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("Muted lobby preview: only the three stage captions were updated.");
    }

    private static void ApplyStageCaptions(Transform root, Font font, Color ivory)
    {
        Transform previous = Require(root, "StageSelection/PreviousStage");
        Transform current = Require(root, "StageSelection/CurrentStage");
        Transform locked = Require(root, "StageSelection/NextStage_Locked");
        SetCaption(previous, "PreviousCaption", "Plate_PreviousStage_v2.png", "이전 스테이지",
            70, -435, 290, 68, font, ivory);
        SetCaption(locked, "LockedCaption", "Plate_LockedStage_v2.png", "잠금",
            70, -435, 290, 68, font, ivory);

        Transform record = Require(current, "StageRecord");
        SetRect((RectTransform)record, 55, -480, 430, 118);
        SetImage(record.GetComponent<Image>(), "Plate_CurrentStage_Tall_v2.png", false, false);
        Graphic recordText = RequireTextGraphic(Require(record, "RecordText"));
        SetRect((RectTransform)recordText.transform, 45, -24, 340, 28);
        SetText(recordText, "최고 도달  40", 24, ivory, TextAnchor.MiddleCenter);

        Transform clearTime = record.Find("ClearTimeText");
        if (clearTime == null)
        {
            GameObject created = new GameObject("ClearTimeText", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            Undo.RegisterCreatedObjectUndo(created, "Create clear time text");
            created.transform.SetParent(record, false);
            clearTime = created.transform;
        }

        Graphic clearTimeText = RequireTextGraphic(clearTime);
        if (clearTimeText is Text clearTimeLegacy)
        {
            Undo.RecordObject(clearTimeLegacy, "Set clear time font");
            clearTimeLegacy.font = font;
        }
        SetRect((RectTransform)clearTime, 45, -64, 340, 25);
        SetText(clearTimeText, "클리어 시간  미기록", 18, ivory, TextAnchor.MiddleCenter);
    }

    private static void ApplyMenuButton(Transform root)
    {
        Transform menu = Require(root, "SettingsButton");
        SetRect((RectTransform)menu, 1780, -18, 100, 100);
        SetImage(menu.GetComponent<Image>(), "Button_MenuIcon_v2.png", true);
        SetActive(Require(menu, "Label"), false);
    }

    private static void ApplyBattleButton(Transform current, Color ivory)
    {
        Transform start = Require(current, "StartButton");
        SetRect((RectTransform)start, -10, -605, 560, 145);
        SetImage(start.GetComponent<Image>(), "Button_BattlePlate_v2.png", true, false);
        Transform battleIcon = FindOrCreateImage(start, "IconVisual");
        SetRect((RectTransform)battleIcon, 0, -1, 150, 147);
        SetImage(battleIcon.GetComponent<Image>(), "Button_BattleIcon_v2.png", false);
        Graphic startLabel = RequireTextGraphic(Require(start, "Label"));
        SetRect((RectTransform)startLabel.transform, 145, -10, 365, 125);
        SetText(startLabel, "전투 준비", 56, ivory, TextAnchor.MiddleCenter);
    }

    private static void SetNavigation(Transform root, string name, string sprite, string label,
        float x, Font font, Color textColor)
    {
        Transform button = Require(root, name);
        SetRect((RectTransform)button, x, -855, 210, 190);
        SetImage(button.GetComponent<Image>(), sprite, true);
        Graphic text = RequireTextGraphic(Require(button, "Label"));
        if (text is Text legacy)
        {
            Undo.RecordObject(legacy, "Set lobby navigation label font");
            legacy.font = font;
        }
        SetRect((RectTransform)text.transform, 0, -154, 210, 50);
        SetText(text, label, label == "스킬 세팅" ? 26 : 28, textColor, TextAnchor.MiddleCenter);
    }

    private static void SetCaption(Transform parent, string name, string spriteName, string text, float x, float y,
        float width, float height, Font font, Color textColor)
    {
        Transform plate = FindOrCreateImage(parent, name);
        SetRect((RectTransform)plate, x, y, width, height);
        SetImage(plate.GetComponent<Image>(), spriteName, false, false);
        Transform label = plate.Find("Label");
        if (label == null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Undo.RegisterCreatedObjectUndo(labelObject, "Create lobby stage caption label");
            labelObject.transform.SetParent(plate, false);
            label = labelObject.transform;
        }

        Graphic caption = RequireTextGraphic(label);
        if (caption is Text legacy)
        {
            Undo.RecordObject(legacy, "Set lobby stage caption font");
            legacy.font = font;
        }
        SetRect((RectTransform)label, 8, -4, width - 16, height - 8);
        SetText(caption, text, 29, textColor, TextAnchor.MiddleCenter);
    }

    private static Transform FindOrCreateImage(Transform parent, string name)
    {
        Transform result = parent.Find(name);
        if (result != null)
        {
            return result;
        }

        GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(created, "Create muted lobby UI element");
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static Transform Require(Transform parent, string path)
    {
        Transform result = parent.Find(path);
        if (result == null)
        {
            throw new InvalidOperationException("필수 로비 UI 오브젝트를 찾을 수 없습니다: " + path);
        }
        return result;
    }

    private static void SetImage(Image image, string fileName, bool raycastTarget, bool preserveAspect = true)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + fileName);
        if (image == null || sprite == null)
        {
            throw new InvalidOperationException("로비 이미지 또는 스프라이트가 없습니다: " + fileName);
        }

        Undo.RecordObject(image, "Set muted lobby sprite");
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.color = Color.white;
        image.raycastTarget = raycastTarget;
    }

    private static Font RequireFallbackFont()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FALLBACK_FONT);
        if (font == null)
        {
            throw new InvalidOperationException("로비 기본 폰트를 찾을 수 없습니다: " + FALLBACK_FONT);
        }

        return font;
    }

    private static Graphic RequireTextGraphic(Transform target)
    {
        Graphic graphic = target.GetComponent<Text>();
        if (graphic == null)
        {
            graphic = target.GetComponent<TMP_Text>();
        }

        if (graphic == null)
        {
            throw new InvalidOperationException("로비 텍스트 컴포넌트를 찾을 수 없습니다: " + target.name);
        }

        return graphic;
    }

    private static void SetText(Graphic graphic, string value, int fontSize, Color color, TextAnchor alignment)
    {
        Undo.RecordObject(graphic, "Set muted lobby text");
        graphic.color = color;
        graphic.raycastTarget = false;
        if (graphic is Text text)
        {
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }
        else if (graphic is TMP_Text tmp)
        {
            tmp.text = value;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = alignment == TextAnchor.MiddleCenter
                ? TextAlignmentOptions.Center : tmp.alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private static void SetRect(RectTransform rect, float x, float y, float width, float height)
    {
        Undo.RecordObject(rect, "Place muted lobby UI element");
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform rect)
    {
        Undo.RecordObject(rect, "Stretch muted lobby background");
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetActive(Transform target, bool active)
    {
        Undo.RecordObject(target.gameObject, "Toggle muted lobby placeholder");
        target.gameObject.SetActive(active);
    }

    private static string ToAbsolute(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
