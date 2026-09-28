using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 난이도 기록 UI의 설명용 SDF와 강조용 Pixel 폰트를 준비한다. 씬 배치와 기존 공용 폰트는 변경하지 않는다.
/// 자동 실행하지 않으며, 호출자가 실제 화면에서 사용할 문자를 모두 전달해야 한다.
/// </summary>
public static class LobbyDifficultyRecordTypography
{
    public const string FONT_FOLDER = "Assets/98.ExternalAssets/00.LocalStaging/01.Font";
    public const string FONT_PATH = FONT_FOLDER + "/LobbyRecordText SDF.asset";
    public const string EMPHASIS_FONT_PATH = FONT_FOLDER + "/LobbyRecordEmphasis Pixel.asset";
    private const string REFERENCE_EMPHASIS_FONT_PATH = FONT_FOLDER + "/DOSMyungjo Pixel.asset";
    private const string SOURCE_PATH = FONT_FOLDER + "/NotoSansCJKkr-Regular.otf";
    private const string EMPHASIS_SOURCE_PATH = FONT_FOLDER + "/DOSMyungjo.ttf";
    private const string SDF_SHADER_NAME = "TextMeshPro/Mobile/Distance Field";
    private const string BITMAP_SHADER_NAME = "TextMeshPro/Mobile/Bitmap";
    private const string UNDO_NAME = "Prepare lobby record typography";
    private const int SAMPLING_POINT_SIZE = 90;
    private const int ATLAS_PADDING = 9;
    private const int ATLAS_SIZE = 2048;
    private const string COMMON_CHARACTERS = "0123456789:.- ";

    /// <summary>
    /// 전용 에셋의 GUID를 유지하면서 필요한 글리프만 추가한 후 Static으로 고정한다.
    /// 생성 에셋과 원래 .meta는 기존 정책에 따라 Git이 아닌 팀 공유 폰트 묶음으로 배포한다.
    /// </summary>
    public static TMP_FontAsset EnsureFont(string characters)
    {
        return EnsureFontAsset(characters, SOURCE_PATH, FONT_PATH, SDF_SHADER_NAME,
            GlyphRenderMode.SDFAA, SAMPLING_POINT_SIZE, ATLAS_PADDING, ATLAS_SIZE, FilterMode.Bilinear);
    }

    /// <summary>기존 명조의 도트 획을 별도 Static 에셋에 준비해 공용 Dynamic atlas는 변경하지 않는다.</summary>
    public static TMP_FontAsset LoadEmphasisFont(string characters)
    {
        TMP_FontAsset reference = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(REFERENCE_EMPHASIS_FONT_PATH);
        if (reference == null || reference.atlasRenderMode != GlyphRenderMode.RASTER)
            throw new InvalidOperationException("기존 명조 Pixel 폰트와 원래 .meta를 공유 묶음에서 복원하세요: "
                + REFERENCE_EMPHASIS_FONT_PATH);
        int pointSize = Mathf.Max(1, Mathf.RoundToInt(reference.faceInfo.pointSize));
        return EnsureFontAsset(characters, EMPHASIS_SOURCE_PATH, EMPHASIS_FONT_PATH, BITMAP_SHADER_NAME,
            GlyphRenderMode.RASTER, pointSize, 2, 1024, FilterMode.Point);
    }

    private static TMP_FontAsset EnsureFontAsset(string characters, string sourcePath, string assetPath,
        string shaderName, GlyphRenderMode renderMode, int pointSize, int padding, int atlasSize, FilterMode filterMode)
    {
        if (!AssetDatabase.IsValidFolder(FONT_FOLDER))
            throw new InvalidOperationException("공유 폰트 폴더를 먼저 복원하세요: " + FONT_FOLDER);

        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
            throw new InvalidOperationException("기록 UI 원본 폰트가 없습니다: " + sourcePath);

        if (Shader.Find(shaderName) == null)
            throw new InvalidOperationException("TMP 기본 폰트 셰이더가 없습니다: " + shaderName);

        string requested = CollectCharacters(COMMON_CHARACTERS + (characters ?? string.Empty));
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        bool isNew = font == null;
        if (!isNew)
            ValidateExistingFont(font, source, assetPath, shaderName, renderMode);

        string pending = CollectMissingCharacters(font, requested);
        // 이미 준비된 폰트를 재실행할 때 불필요한 atlas 갱신과 파일 저장을 피한다.
        if (!isNew && pending.Length == 0 && font.atlasPopulationMode == AtlasPopulationMode.Static)
            return font;

        VerifySourceCharacters(source, pending, pointSize);
        if (isNew)
        {
            font = TMP_FontAsset.CreateFontAsset(source, pointSize, padding,
                renderMode, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, false);
            if (font == null)
                throw new InvalidOperationException("기록 UI 전용 폰트 생성에 실패했습니다: " + sourcePath);
            font.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        }
        else
        {
            Undo.RecordObject(font, UNDO_NAME);
            Undo.RecordObject(font.material, UNDO_NAME);
            foreach (Texture2D atlas in font.atlasTextures)
                if (atlas != null) Undo.RecordObject(atlas, UNDO_NAME);
        }

        try
        {
            // Static은 sourceFontFile을 비우므로, 추가 작업 동안에만 Editor 원본 참조를 복원한다.
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            try
            {
                if (pending.Length > 0 && !font.TryAddCharacters(pending, out string missing))
                    throw new InvalidOperationException("기록 UI 전용 폰트에 문자를 추가하지 못했습니다. 누락: "
                        + DescribeCharacters(missing) + ". 원본 글리프와 atlas 용량을 확인하세요. 기존 에셋을 삭제하거나 재생성하지 마세요.");
            }
            finally
            {
                font.atlasPopulationMode = AtlasPopulationMode.Static;
            }

            if (isNew)
                AssetDatabase.CreateAsset(font, assetPath);

            foreach (Texture2D atlas in font.atlasTextures)
            {
                if (atlas == null) continue;
                atlas.filterMode = filterMode;
                if (!AssetDatabase.Contains(atlas))
                    AssetDatabase.AddObjectToAsset(atlas, font);
                EditorUtility.SetDirty(atlas);
            }

            if (!AssetDatabase.Contains(font.material))
                AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font.material);
            EditorUtility.SetDirty(font);
            // 다른 작업 중인 에셋까지 SaveAssets로 저장하지 않는다.
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }
        catch
        {
            if (isNew && !AssetDatabase.Contains(font))
            {
                foreach (Texture2D atlas in font.atlasTextures)
                    if (atlas != null && !AssetDatabase.Contains(atlas)) UnityEngine.Object.DestroyImmediate(atlas);
                if (font.material != null && !AssetDatabase.Contains(font.material))
                    UnityEngine.Object.DestroyImmediate(font.material);
                UnityEngine.Object.DestroyImmediate(font);
            }
            throw;
        }
    }

    private static void ValidateExistingFont(TMP_FontAsset font, Font source, string assetPath,
        string shaderName, GlyphRenderMode renderMode)
    {
        SerializedObject serialized = new SerializedObject(font);
        SerializedProperty sourceGuid = serialized.FindProperty("m_SourceFontFileGUID");
        bool hasSameSource = sourceGuid != null
            && sourceGuid.stringValue == AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        if (!hasSameSource || font.atlasRenderMode != renderMode
            || font.material == null || font.material.shader == null
            || font.material.shader.name != shaderName)
            throw new InvalidOperationException("기존 기록 전용 폰트의 원본 또는 셰이더가 예상과 다릅니다. GUID 보존을 위해 덮어쓰지 않습니다: "
                + assetPath);
        if (font.atlasTextures == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null)
            throw new InvalidOperationException("기록 전용 폰트 atlas 참조가 없습니다: " + assetPath);
    }

    private static string CollectCharacters(string value)
    {
        HashSet<int> seen = new HashSet<int>();
        StringBuilder result = new StringBuilder();
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsControl(value, index)) continue;
            int unicode = char.ConvertToUtf32(value, index);
            if (seen.Add(unicode)) result.Append(char.ConvertFromUtf32(unicode));
            if (char.IsHighSurrogate(value[index])) index++;
        }
        return result.ToString();
    }

    private static string CollectMissingCharacters(TMP_FontAsset font, string characters)
    {
        if (font == null) return characters;
        StringBuilder result = new StringBuilder();
        for (int index = 0; index < characters.Length; index++)
        {
            int unicode = char.ConvertToUtf32(characters, index);
            if (!font.HasCharacter(unicode)) result.Append(char.ConvertFromUtf32(unicode));
            if (char.IsHighSurrogate(characters[index])) index++;
        }
        return result.ToString();
    }

    private static void VerifySourceCharacters(Font source, string characters, int pointSize)
    {
        if (characters.Length == 0) return;
        FontEngine.InitializeFontEngine();
        if (FontEngine.LoadFontFace(source, pointSize) != FontEngineError.Success)
            throw new InvalidOperationException("기록 폰트 원본을 읽지 못했습니다. Include Font Data를 확인하세요: "
                + AssetDatabase.GetAssetPath(source));
        StringBuilder missing = new StringBuilder();
        for (int index = 0; index < characters.Length; index++)
        {
            int unicode = char.ConvertToUtf32(characters, index);
            if (!FontEngine.TryGetGlyphWithUnicodeValue((uint)unicode, GlyphLoadFlags.LOAD_NO_BITMAP, out _))
                missing.Append(char.ConvertFromUtf32(unicode));
            if (char.IsHighSurrogate(characters[index])) index++;
        }
        if (missing.Length > 0)
            throw new InvalidOperationException(source.name + " 원본에 없는 기록 UI 문자: " + DescribeCharacters(missing.ToString()));
    }

    private static string DescribeCharacters(string characters)
    {
        if (string.IsNullOrEmpty(characters)) return "atlas 용량 부족 또는 알 수 없는 문자";
        StringBuilder result = new StringBuilder();
        for (int index = 0; index < characters.Length; index++)
        {
            int unicode = char.ConvertToUtf32(characters, index);
            if (result.Length > 0) result.Append(", ");
            result.Append(char.ConvertFromUtf32(unicode)).Append(" (U+")
                .Append(unicode.ToString("X4")).Append(')');
            if (char.IsHighSurrogate(characters[index])) index++;
        }
        return result.ToString();
    }
}
