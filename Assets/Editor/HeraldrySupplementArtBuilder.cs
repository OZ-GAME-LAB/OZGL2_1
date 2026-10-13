using System;
using System.IO;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>보조 UI 전용 Sprite와 스타일 SO만 생성합니다. Scene과 Prefab은 수정하지 않습니다.</summary>
public static class HeraldrySupplementArtBuilder
{
    public const string ROOT = "Assets/06.UI/SharedUI/Heraldry_Supplement_v1";
    public const string STYLE_PATH = ROOT + "/Resources/UI/HeraldrySupplementStyle.asset";
    private const string FONT_ROOT = "Assets/06.UI/01.Font/02.SDF_File/";
    private const string BUTTON_ROOT = "Assets/06.UI/SharedMenu/Heraldry_Menu_v1/Sprites/";
    private const float FRAME_BORDER = 128f;

    private static readonly string[] ART_NAMES =
    {
        "panel_frame", "header_crest", "speaker_plate", "achievement_medallion", "bubble_tail",
        "panel_complete", "dialogue_complete_left", "result_panel_clean", "result_track_clean", "result_fill_clean"
    };

    [MenuItem("Tools/OZGL2/Shared UI/Build Heraldry Supplement Art")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 실행해 주세요.");

        // 누락 때문에 일부 에셋만 만들어지는 일을 줄이도록 입력을 먼저 확인합니다.
        foreach (string artName in ART_NAMES)
        {
            string pngPath = ROOT + "/Textures/" + artName + ".png";
            if (!File.Exists(pngPath))
                throw new FileNotFoundException("먼저 생성 이미지를 Textures 폴더에 배치해 주세요.", pngPath);
        }

        TMP_FontAsset titleFont = RequireAsset<TMP_FontAsset>(FONT_ROOT + "HeirofLightBold_SDF.asset");
        TMP_FontAsset bodyFont = RequireAsset<TMP_FontAsset>(FONT_ROOT + "NEXON_Lv2_Gothic_SDF.asset");
        TMP_FontAsset numberFont = RequireAsset<TMP_FontAsset>(FONT_ROOT + "Paperlogy-9Black SDF.asset");
        Sprite darkButton = RequireAsset<Sprite>(BUTTON_ROOT + "button_dark.asset");
        Sprite redButton = RequireAsset<Sprite>(BUTTON_ROOT + "button_red.asset");

        EnsureFolder(ROOT + "/Sprites");
        EnsureFolder(ROOT + "/Resources/UI");
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("보조 UI 아트 설정 갱신");

        try
        {
            Sprite frame = CreateOrUpdateSprite("panel_frame", Vector4.one * FRAME_BORDER);
            Sprite crest = CreateOrUpdateSprite("header_crest", Vector4.zero);
            Sprite nameplate = CreateOrUpdateSprite("speaker_plate", new Vector4(160f, 150f, 160f, 150f));
            Sprite medallion = CreateOrUpdateSprite("achievement_medallion", Vector4.zero);
            Sprite tail = CreateOrUpdateSprite("bubble_tail", Vector4.zero);
            Sprite panel = CreateOrUpdateSprite("panel_complete", Vector4.one * FRAME_BORDER);
            Sprite dialogue = CreateOrUpdateSprite("dialogue_complete_left", new Vector4(240f, 80f, 96f, 80f));
            Sprite resultPanel = CreateOrUpdateSprite("result_panel_clean", Vector4.zero);
            Sprite resultTrack = CreateOrUpdateSprite("result_track_clean", new Vector4(80f, 16f, 80f, 16f));
            Sprite resultFill = CreateOrUpdateSprite("result_fill_clean", new Vector4(44f, 8f, 44f, 8f));

            UIHeraldrySupplementStyleSO style = AssetDatabase.LoadAssetAtPath<UIHeraldrySupplementStyleSO>(STYLE_PATH);
            bool isNewStyle = style == null;
            if (isNewStyle)
            {
                if (AssetDatabase.LoadMainAssetAtPath(STYLE_PATH) != null)
                    throw new InvalidOperationException("스타일 경로에 다른 종류의 에셋이 있습니다: " + STYLE_PATH);
                style = ScriptableObject.CreateInstance<UIHeraldrySupplementStyleSO>();
                AssetDatabase.CreateAsset(style, STYLE_PATH);
            }
            else
            {
                Undo.RecordObject(style, "보조 UI 스타일 연결 갱신");
            }

            var serialized = new SerializedObject(style);
            SetReference(serialized, "_frameSprite", frame);
            SetReference(serialized, "_crestSprite", crest);
            SetReference(serialized, "_nameplateSprite", nameplate);
            SetReference(serialized, "_medallionSprite", medallion);
            SetReference(serialized, "_tailSprite", tail);
            SetReference(serialized, "_panelSprite", panel);
            SetReference(serialized, "_dialogueSprite", dialogue);
            SetReference(serialized, "_resultPanelSprite", resultPanel);
            SetReference(serialized, "_resultTrackSprite", resultTrack);
            SetReference(serialized, "_resultFillSprite", resultFill);
            SetReference(serialized, "_titleFont", titleFont);
            SetReference(serialized, "_bodyFont", bodyFont);
            SetReference(serialized, "_numberFont", numberFont);
            SetReference(serialized, "_darkButtonSprite", darkButton);
            SetReference(serialized, "_redButtonSprite", redButton);
            // 재실행할 때 Inspector에서 조정한 색과 프레임 배율은 유지합니다.
            if (isNewStyle)
            {
                serialized.FindProperty("_backgroundColor").colorValue = new Color32(18, 16, 18, 255);
                serialized.FindProperty("_frameMultiplier").floatValue = 4f;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(style);
            // TMP 미리보기 등으로 dirty가 된 다른 에셋까지 함께 저장하지 않습니다.
            AssetDatabase.SaveAssetIfDirty(frame);
            AssetDatabase.SaveAssetIfDirty(crest);
            AssetDatabase.SaveAssetIfDirty(nameplate);
            AssetDatabase.SaveAssetIfDirty(medallion);
            AssetDatabase.SaveAssetIfDirty(tail);
            AssetDatabase.SaveAssetIfDirty(panel);
            AssetDatabase.SaveAssetIfDirty(dialogue);
            AssetDatabase.SaveAssetIfDirty(resultPanel);
            AssetDatabase.SaveAssetIfDirty(resultTrack);
            AssetDatabase.SaveAssetIfDirty(resultFill);
            AssetDatabase.SaveAssetIfDirty(style);
            Debug.Log("보조 UI Sprite 10종과 스타일 SO 연결을 준비했습니다. 기존 Sprite/SO 값 갱신은 Undo를 지원합니다. " +
                      "새 파일·폴더 생성과 Texture Import 설정은 Undo 대상이 아닙니다. Scene/Prefab은 변경하지 않았습니다. " + STYLE_PATH);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static Sprite CreateOrUpdateSprite(string name, Vector4 border)
    {
        string pngPath = ROOT + "/Textures/" + name + ".png";
        string spritePath = ROOT + "/Sprites/" + name + ".asset";
        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("TextureImporter를 찾을 수 없습니다: " + pngPath);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.format = TextureImporterFormat.RGBA32;
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(platform);
        importer.SaveAndReimport();

        Texture2D texture = RequireAsset<Texture2D>(pngPath);
        var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Sprite newSprite = null;
        try
        {
            if (!ImageConversion.LoadImage(pixels, File.ReadAllBytes(pngPath)))
                throw new InvalidOperationException("PNG를 읽을 수 없습니다: " + pngPath);
            if (texture.width != pixels.width || texture.height != pixels.height)
                throw new InvalidOperationException("원본 크기와 임포트 크기가 다릅니다. 최대 Texture 크기를 확인해 주세요: " + pngPath);

            Rect rect = FindAlphaRect(pixels, pngPath);
            float halfWidth = Mathf.Max(0f, (rect.width - 1f) * 0.5f);
            float halfHeight = Mathf.Max(0f, (rect.height - 1f) * 0.5f);
            border = new Vector4(Mathf.Min(border.x, halfWidth), Mathf.Min(border.y, halfHeight),
                Mathf.Min(border.z, halfWidth), Mathf.Min(border.w, halfHeight));
            newSprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            newSprite.name = name;
            if (name == "result_panel_clean")
                HeraldrySpriteSilhouette.Apply(newSprite, pixels, 0.65f);

            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (existing != null)
            {
                // 에셋을 지우고 다시 만들지 않아 이미 연결된 GUID를 유지합니다.
                Undo.RecordObject(existing, "보조 UI Sprite 갱신");
                EditorUtility.CopySerialized(newSprite, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            if (AssetDatabase.LoadMainAssetAtPath(spritePath) != null)
                throw new InvalidOperationException("Sprite 경로에 다른 종류의 에셋이 있습니다: " + spritePath);
            AssetDatabase.CreateAsset(newSprite, spritePath);
            Sprite created = newSprite;
            newSprite = null;
            return created;
        }
        finally
        {
            if (newSprite != null)
                Object.DestroyImmediate(newSprite);
            Object.DestroyImmediate(pixels);
        }
    }

    private static Rect FindAlphaRect(Texture2D pixels, string path)
    {
        Color32[] colors = pixels.GetPixels32();
        int left = pixels.width;
        int bottom = pixels.height;
        int right = -1;
        int top = -1;
        for (int y = 0; y < pixels.height; y++)
        {
            for (int x = 0; x < pixels.width; x++)
            {
                if (colors[y * pixels.width + x].a <= 32)
                    continue;
                left = Mathf.Min(left, x);
                bottom = Mathf.Min(bottom, y);
                right = Mathf.Max(right, x);
                top = Mathf.Max(top, y);
            }
        }
        if (right < left || top < bottom)
            throw new InvalidOperationException("알파가 있는 픽셀이 없는 이미지입니다: " + path);

        left = Mathf.Max(0, left - 4);
        bottom = Mathf.Max(0, bottom - 4);
        right = Mathf.Min(pixels.width - 1, right + 4);
        top = Mathf.Min(pixels.height - 1, top + 4);
        return new Rect(left, bottom, right - left + 1, top - bottom + 1);
    }

    private static T RequireAsset<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException("필수 에셋이 없습니다: " + path);
        return asset;
    }

    private static void SetReference(SerializedObject serialized, string field, Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException("스타일 필드를 찾을 수 없습니다: " + field);
        property.objectReferenceValue = value;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;
        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent))
            throw new InvalidOperationException("Assets 내부의 폴더 경로가 필요합니다: " + folderPath);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
    }
}
