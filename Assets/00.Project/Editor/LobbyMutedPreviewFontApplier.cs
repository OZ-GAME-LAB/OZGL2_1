using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 별도 로비 시안 씬의 표시 텍스트만 픽셀 폰트 TMP 아틀라스로 전환한다.
/// </summary>
internal static class LobbyMutedPreviewFontApplier
{
    private const string PREVIEW_SCENE = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private const string SOURCE_FOLDER = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/";
    private const string OUTPUT_FOLDER = "Assets/06.UI/LobbyMutedPreview/Fonts";
    private const string GOTHIC_ASSET = OUTPUT_FOLDER + "/DOSGothic Pixel.asset";
    private const string MYUNGJO_ASSET = OUTPUT_FOLDER + "/DOSMyungjo Pixel.asset";
    private const string GOTHIC_OUTLINE_MATERIAL = OUTPUT_FOLDER + "/DOSGothic Pixel Outline.mat";
    private const string MYUNGJO_OUTLINE_MATERIAL = OUTPUT_FOLDER + "/DOSMyungjo Pixel Outline.mat";
    private const string OUTLINE_SHADER = OUTPUT_FOLDER + "/PixelTMPOutline.shader";
    private static readonly Color32 _textColor = new Color32(248, 242, 235, 255);
    private static readonly Color32 _outlineColor = new Color32(80, 73, 73, 255);

    private static readonly string[] _gothicPaths =
    {
        "StageSelection/PreviousStage/PreviousCaption/Label",
        "StageSelection/CurrentStage/StageRecord/RecordText",
        "StageSelection/CurrentStage/StageRecord/ClearTimeText",
        "StageSelection/NextStage_Locked/LockedCaption/Label"
    };

    private static readonly string[] _myungjoPaths =
    {
        "AccountStatus/Level",
        "StageSelection/CurrentStage/StartButton/Label",
        "BottomNavigation/ReservedButton/Label",
        "BottomNavigation/TraitsButton/Label",
        "BottomNavigation/SkillsButton/Label",
        "BottomNavigation/CodexButton/Label"
    };

    [MenuItem("Tools/OZGL2/Lobby/Apply Preview Pixel Fonts")]
    private static void ApplyPreviewPixelFonts()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != PREVIEW_SCENE)
        {
            throw new InvalidOperationException("UI_Lobby_MutedPreview.unity에서만 실행할 수 있습니다.");
        }

        GameObject canvas = GameObject.Find("Canvas_Lobby");
        if (canvas == null)
        {
            throw new InvalidOperationException("Canvas_Lobby를 찾을 수 없습니다.");
        }

        ValidateTargets(canvas.transform, _gothicPaths);
        ValidateTargets(canvas.transform, _myungjoPaths);
        EnsureOutputFolder();

        TMP_FontAsset gothic = GetOrCreateBitmap("DOSGothic.ttf", GOTHIC_ASSET,
            CollectCharacters(canvas.transform, _gothicPaths));
        TMP_FontAsset myungjo = GetOrCreateBitmap("DOSMyungjo.ttf", MYUNGJO_ASSET,
            CollectCharacters(canvas.transform, _myungjoPaths));
        Material gothicMaterial = GetOrCreateOutlineMaterial(gothic, GOTHIC_OUTLINE_MATERIAL);
        Material myungjoMaterial = GetOrCreateOutlineMaterial(myungjo, MYUNGJO_OUTLINE_MATERIAL);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply preview lobby pixel fonts");
        ApplyFont(canvas.transform, _gothicPaths, gothic, gothicMaterial);
        ApplyFont(canvas.transform, _myungjoPaths, myungjo, myungjoMaterial);
        FitMyungjoLabels(canvas.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        Debug.Log("Muted lobby preview: 4 DOSGothic and 6 DOSMyungjo pixel TMP labels applied.");
    }

    private static void ValidateTargets(Transform root, IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            Transform target = root.Find(path);
            if (target == null || (target.GetComponent<Text>() == null &&
                                   target.GetComponent<TMP_Text>() == null))
            {
                throw new InvalidOperationException("로비 텍스트를 찾을 수 없습니다: " + path);
            }
        }
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/06.UI/LobbyMutedPreview", "Fonts");
        }
    }

    private static string CollectCharacters(Transform root, IEnumerable<string> paths)
    {
        HashSet<char> characters = new HashSet<char>();
        foreach (char character in "0123456789:./% -")
        {
            characters.Add(character);
        }

        foreach (string path in paths)
        {
            Transform target = root.Find(path);
            Text legacy = target.GetComponent<Text>();
            TMP_Text tmp = target.GetComponent<TMP_Text>();
            string value = legacy != null ? legacy.text : tmp.text;
            foreach (char character in value)
            {
                if (!char.IsControl(character))
                {
                    characters.Add(character);
                }
            }
        }

        StringBuilder result = new StringBuilder(characters.Count);
        foreach (char character in characters)
        {
            result.Append(character);
        }

        return result.ToString();
    }

    private static TMP_FontAsset GetOrCreateBitmap(string sourceName, string outputPath,
        string characters)
    {
        string sourcePath = SOURCE_FOLDER + sourceName;
        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
        {
            throw new InvalidOperationException("원본 폰트가 없습니다: " + sourcePath);
        }

        TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);
        if (asset == null)
        {
            FontEngine.InitializeFontEngine();
            // DOS 픽셀 글꼴은 SDF에서 글자 아래에 수평 잔상이 생겨 원본 비트맵 획을 사용한다.
            asset = TMP_FontAsset.CreateFontAsset(source, 90, 4,
                GlyphRenderMode.RASTER, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                throw new InvalidOperationException("픽셀 폰트 아틀라스 생성에 실패했습니다: " + sourceName);
            }

            asset.name = System.IO.Path.GetFileNameWithoutExtension(outputPath);
            AssetDatabase.CreateAsset(asset, outputPath);
            foreach (Texture2D texture in asset.atlasTextures)
            {
                if (texture != null)
                {
                    AssetDatabase.AddObjectToAsset(texture, asset);
                }
            }

            if (asset.material != null)
            {
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
        }
        else if (asset.sourceFontFile != source || asset.atlasRenderMode != GlyphRenderMode.RASTER)
        {
            throw new InvalidOperationException("기존 픽셀 폰트 에셋의 원본 또는 렌더 모드가 다릅니다: " + outputPath);
        }

        StringBuilder charactersToAdd = new StringBuilder();
        foreach (char character in characters)
        {
            if (!asset.HasCharacter((int)character))
            {
                charactersToAdd.Append(character);
            }
        }

        if (charactersToAdd.Length > 0)
        {
            string missing;
            if (!asset.TryAddCharacters(charactersToAdd.ToString(), out missing))
            {
                throw new InvalidOperationException(sourceName + "에 없는 글리프: " + missing);
            }
        }

        foreach (Texture2D texture in asset.atlasTextures)
        {
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                if (!AssetDatabase.Contains(texture))
                {
                    AssetDatabase.AddObjectToAsset(texture, asset);
                }

                EditorUtility.SetDirty(texture);
            }
        }

        EditorUtility.SetDirty(asset);
        if (asset.material != null)
        {
            EditorUtility.SetDirty(asset.material);
        }

        return asset;
    }

    private static Material GetOrCreateOutlineMaterial(TMP_FontAsset font, string outputPath)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(OUTLINE_SHADER);
        if (shader == null)
        {
            throw new InvalidOperationException("로비 픽셀 글꼴 테두리 셰이더가 없습니다: " + OUTLINE_SHADER);
        }

        if (font.atlasTextures == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null)
        {
            throw new InvalidOperationException("픽셀 글꼴 아틀라스가 없습니다: " + font.name);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
        if (material == null)
        {
            material = new Material(shader);
            material.name = System.IO.Path.GetFileNameWithoutExtension(outputPath);
            AssetDatabase.CreateAsset(material, outputPath);
        }
        else
        {
            Undo.RecordObject(material, "Configure lobby pixel font outline");
            material.shader = shader;
        }

        material.SetTexture("_MainTex", font.atlasTextures[0]);
        material.SetColor("_Color", Color.white);
        material.SetColor("_OutlineColor", _outlineColor);
        material.SetFloat("_OutlineTexels", 3.5f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ApplyFont(Transform root, IEnumerable<string> paths, TMP_FontAsset font,
        Material outlineMaterial)
    {
        foreach (string path in paths)
        {
            GameObject target = root.Find(path).gameObject;
            TMP_Text tmp = target.GetComponent<TMP_Text>();
            if (tmp == null)
            {
                Text legacy = target.GetComponent<Text>();
                string value = legacy.text;
                Color color = legacy.color;
                int size = legacy.fontSize;
                TextAnchor alignment = legacy.alignment;
                bool raycastTarget = legacy.raycastTarget;
                bool maskable = legacy.maskable;
                bool richText = legacy.supportRichText;
                bool autoSizing = legacy.resizeTextForBestFit;
                FontStyle style = legacy.fontStyle;

                Undo.DestroyObjectImmediate(legacy);
                tmp = Undo.AddComponent<TextMeshProUGUI>(target);
                Undo.RecordObject(tmp, "Configure lobby TMP text");
                tmp.text = value;
                tmp.color = color;
                tmp.fontSize = size;
                tmp.alignment = ConvertAlignment(alignment);
                tmp.raycastTarget = raycastTarget;
                tmp.maskable = maskable;
                tmp.richText = richText;
                tmp.enableAutoSizing = autoSizing;
                tmp.fontStyle = ConvertStyle(style);
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
            }
            else
            {
                Undo.RecordObject(tmp, "Set lobby TMP font");
            }

            tmp.font = font;
            tmp.fontSharedMaterial = outlineMaterial;
            tmp.fontStyle = FontStyles.Normal;
            tmp.color = _textColor;
            Outline outline = target.GetComponent<Outline>();
            if (outline != null)
            {
                Undo.DestroyObjectImmediate(outline);
            }

            EditorUtility.SetDirty(tmp);
        }
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private static void FitMyungjoLabels(Transform root)
    {
        SetSize(root, "AccountStatus/Level", 36);
        RectTransform levelRect = (RectTransform)root.Find("AccountStatus/Level");
        Undo.RecordObject(levelRect, "Space lobby level label");
        levelRect.anchoredPosition = new Vector2(45, -15);
        levelRect.sizeDelta = new Vector2(150, 60);
        SetNavigationLabel(root, "BottomNavigation/ReservedButton/Label", 28);
        SetNavigationLabel(root, "BottomNavigation/TraitsButton/Label", 28);
        SetNavigationLabel(root, "BottomNavigation/SkillsButton/Label", 26);
        SetNavigationLabel(root, "BottomNavigation/CodexButton/Label", 28);
    }

    private static void SetSize(Transform root, string path, float size)
    {
        TMP_Text text = root.Find(path).GetComponent<TMP_Text>();
        Undo.RecordObject(text, "Fit lobby TMP label");
        text.fontSize = size;
    }

    private static void SetNavigationLabel(Transform root, string path, float size)
    {
        Transform target = root.Find(path);
        SetSize(root, path, size);
        RectTransform rect = (RectTransform)target;
        Undo.RecordObject(rect, "Space lobby navigation label");
        Vector2 position = rect.anchoredPosition;
        position.y = -154;
        rect.anchoredPosition = position;
    }

    private static FontStyles ConvertStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold: return FontStyles.Bold;
            case FontStyle.Italic: return FontStyles.Italic;
            case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
            default: return FontStyles.Normal;
        }
    }
}
