using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 전투 결과 화면만 사용하는 명조 SDF를 준비한다. 씬, 프리팹, 기존 공용 폰트는 변경하지 않는다.
/// 한글 문구가 늘어나면 아래 문자 집합에 추가한 뒤 다시 실행하여 Static atlas에 포함한다.
/// </summary>
public static class BattleResultTypography
{
    public const string FONT_FOLDER = "Assets/06.UI/BattleMutedPreview/ResultRefinement_v2/Fonts";
    public const string TITLE_FONT_PATH = FONT_FOLDER + "/BattleResultTitle SDF.asset";
    public const string BODY_FONT_PATH = FONT_FOLDER + "/BattleResultBody SDF.asset";

    private const string SOURCE_FOLDER = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/00.otf_ttf_file/";
    private const string TITLE_SOURCE_PATH = SOURCE_FOLDER + "HS봄바람체/HS봄바람체2.0.ttf";
    private const string BODY_SOURCE_PATH = SOURCE_FOLDER + "KMU80SungkokHaeong_01/OTF/static/KMU80SungkokSerif.otf";
    private const string SHADER_NAME = "TextMeshPro/Mobile/Distance Field";
    private const string UNDO_NAME = "전투 결과 전용 명조 폰트 준비";
    private const string TITLE_CHARACTERS = "승리패배";
    private const string BODY_CHARACTERS =
        "쉬움 보통 어려움 난이도 플레이 클리어 시간 분 초 처치 용사 수 유닛 배치 개 명 " +
        "얻은 경험치 로비로 승리 패배 레벨 업 최고 웨이브 획득 보상 전투 결과";

    /// <summary>큰 승리/패배 제목에 사용할 HS봄바람체 SDF를 반환한다.</summary>
    public static TMP_FontAsset EnsureTitleFont()
    {
        return EnsureFont(TITLE_SOURCE_PATH, TITLE_FONT_PATH, TITLE_CHARACTERS, 160, 16, 1024);
    }

    /// <summary>정보 문구와 숫자에 사용할 KMU Sungkok Serif SDF를 반환한다.</summary>
    public static TMP_FontAsset EnsureBodyFont()
    {
        StringBuilder characters = new StringBuilder(BODY_CHARACTERS);
        // 동적으로 변하는 숫자, 영문, 구분 기호를 미리 포함한다. 게임 데이터는 저장하지 않는다.
        for (int unicode = 32; unicode <= 126; unicode++)
            characters.Append((char)unicode);
        return EnsureFont(BODY_SOURCE_PATH, BODY_FONT_PATH, characters.ToString(), 90, 9, 2048);
    }

    private static TMP_FontAsset EnsureFont(string sourcePath, string assetPath, string characters,
        int pointSize, int padding, int atlasSize)
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
            throw new InvalidOperationException("전투 결과 원본 폰트를 먼저 복원하세요: " + sourcePath);
        Shader shader = Shader.Find(SHADER_NAME);
        if (shader == null)
            throw new InvalidOperationException("TMP SDF 셰이더가 없습니다: " + SHADER_NAME);

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        bool isNew = font == null;
        if (!isNew)
            ValidateExistingFont(font, source, assetPath);
        else if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            throw new InvalidOperationException("전용 폰트 경로에 다른 에셋이 있어 덮어쓰지 않습니다: " + assetPath);

        string requested = CollectCharacters(characters);
        string pending = CollectMissingCharacters(font, requested);
        bool hasBilinearAtlas = !isNew;
        if (!isNew)
            foreach (Texture2D atlas in font.atlasTextures)
                hasBilinearAtlas &= atlas != null && atlas.filterMode == FilterMode.Bilinear;
        if (!isNew && pending.Length == 0 && font.atlasPopulationMode == AtlasPopulationMode.Static && hasBilinearAtlas)
            return font;

        VerifySourceCharacters(source, pending, pointSize);
        if (isNew)
        {
            EnsureFolder(FONT_FOLDER);
            font = TMP_FontAsset.CreateFontAsset(source, pointSize, padding, GlyphRenderMode.SDFAA,
                atlasSize, atlasSize, AtlasPopulationMode.Dynamic, false);
            if (font == null)
                throw new InvalidOperationException("전투 결과 SDF 생성에 실패했습니다: " + sourcePath);
            font.name = Path.GetFileNameWithoutExtension(assetPath);
            font.material.shader = shader;
            font.material.name = font.name + " Material";
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
            // Static은 런타임 원본 참조를 비우므로 기존 GUID를 확인한 Editor 참조만 복구한다.
            SerializedObject serialized = new SerializedObject(font);
            SerializedProperty editorSource = serialized.FindProperty("m_SourceFontFile_EditorRef");
            if (editorSource != null)
            {
                editorSource.objectReferenceValue = source;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            try
            {
                if (pending.Length > 0 && !font.TryAddCharacters(pending, out string missing))
                    throw new InvalidOperationException("전투 결과 SDF에 문자를 추가하지 못했습니다: " +
                        DescribeCharacters(missing) + ". atlas 용량을 확인하세요. 기존 에셋을 삭제하거나 재생성하지 마세요.");
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
                atlas.filterMode = FilterMode.Bilinear;
                if (!AssetDatabase.Contains(atlas))
                    AssetDatabase.AddObjectToAsset(atlas, font);
                EditorUtility.SetDirty(atlas);
            }
            if (!AssetDatabase.Contains(font.material))
                AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font.material);
            EditorUtility.SetDirty(font);
            // 사용자가 편집 중인 다른 폰트와 에셋은 저장하지 않는다.
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

    private static void ValidateExistingFont(TMP_FontAsset font, Font source, string assetPath)
    {
        SerializedProperty sourceGuid = new SerializedObject(font).FindProperty("m_SourceFontFileGUID");
        if (sourceGuid == null || sourceGuid.stringValue != AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) ||
            font.atlasRenderMode != GlyphRenderMode.SDFAA || font.material == null || font.material.shader == null ||
            font.material.shader.name != SHADER_NAME)
            throw new InvalidOperationException("전투 결과 전용 폰트의 원본 또는 셰이더가 다릅니다. GUID 보존을 위해 중단합니다: " + assetPath);
        if (font.atlasTextures == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null)
            throw new InvalidOperationException("전투 결과 전용 폰트의 atlas 참조가 없습니다: " + assetPath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
            throw new IOException("전투 결과 폰트 폴더를 생성하지 못했습니다: " + path);
    }

    private static string CollectCharacters(string characters)
    {
        HashSet<int> seen = new HashSet<int>();
        StringBuilder result = new StringBuilder();
        for (int index = 0; index < characters.Length; index++)
        {
            if (char.IsControl(characters, index)) continue;
            int unicode = char.ConvertToUtf32(characters, index);
            if (seen.Add(unicode)) result.Append(char.ConvertFromUtf32(unicode));
            if (char.IsHighSurrogate(characters[index])) index++;
        }
        return result.ToString();
    }

    private static string CollectMissingCharacters(TMP_FontAsset font, string characters)
    {
        if (font == null) return characters;
        StringBuilder pending = new StringBuilder();
        for (int index = 0; index < characters.Length; index++)
        {
            int unicode = char.ConvertToUtf32(characters, index);
            if (!font.HasCharacter(unicode)) pending.Append(char.ConvertFromUtf32(unicode));
            if (char.IsHighSurrogate(characters[index])) index++;
        }
        return pending.ToString();
    }

    private static void VerifySourceCharacters(Font source, string characters, int pointSize)
    {
        if (characters.Length == 0) return;
        FontEngine.InitializeFontEngine();
        if (FontEngine.LoadFontFace(source, pointSize) != FontEngineError.Success)
            throw new InvalidOperationException("원본 폰트를 읽지 못했습니다. Include Font Data를 확인하세요: " + AssetDatabase.GetAssetPath(source));
        StringBuilder missing = new StringBuilder();
        for (int index = 0; index < characters.Length; index++)
        {
            int unicode = char.ConvertToUtf32(characters, index);
            if (!FontEngine.TryGetGlyphWithUnicodeValue((uint)unicode, GlyphLoadFlags.LOAD_NO_BITMAP, out _))
                missing.Append(char.ConvertFromUtf32(unicode));
            if (char.IsHighSurrogate(characters[index])) index++;
        }
        if (missing.Length > 0)
            throw new InvalidOperationException(source.name + " 원본에 없는 전투 결과 문자: " + DescribeCharacters(missing.ToString()));
    }

    private static string DescribeCharacters(string characters)
    {
        if (string.IsNullOrEmpty(characters)) return "atlas 용량 부족 또는 알 수 없는 문자";
        StringBuilder result = new StringBuilder();
        for (int index = 0; index < characters.Length; index++)
        {
            int unicode = char.ConvertToUtf32(characters, index);
            if (result.Length > 0) result.Append(", ");
            result.Append(char.ConvertFromUtf32(unicode)).Append(" (U+").Append(unicode.ToString("X4")).Append(')');
            if (char.IsHighSurrogate(characters[index])) index++;
        }
        return result.ToString();
    }
}
