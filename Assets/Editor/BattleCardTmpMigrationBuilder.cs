using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// SDF 폰트 생성과 카드 Text 참조 변환만 담당한다. 기물과 전투 데이터는 변경하지 않는다.
public static class BattleCardTmpMigrationBuilder
{
    public const string FONT_ROOT = BattleCardRedesignBuilder.ART_ROOT + "/Fonts";
    public const string TITLE_FONT_PATH = FONT_ROOT + "/BattleCardTitle SDF.asset";
    public const string BODY_FONT_PATH = FONT_ROOT + "/BattleCardBody SDF.asset";
    private const string SOURCE_ROOT = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/00.otf_ttf_file/";
    private const string TITLE_SOURCE = SOURCE_ROOT + "heiroflight_fonts/빛의 계승자 Bold/TTF(Window용)/HeirofLightBold.ttf";
    private const string BODY_SOURCE = SOURCE_ROOT + "NEXON_Lv2_Gothic/TTF/NEXON Lv2 Gothic.ttf";
    private const string SHADER_NAME = "TextMeshPro/Mobile/Distance Field";
    private const string UNDO_NAME = "유닛·지형 카드 TMP 전환";
    private static readonly Dictionary<string, string> TEXT_PATHS = new Dictionary<string, string>
    {
        { "_titleText", "TitleText" }, { "_rankText", "RankText" },
        { "_traitTitleText", "TraitTitleText" }, { "_traitDescriptionText", "TraitDescriptionText" },
        { "_skillTitleText", "SkillTitleText" }, { "_skillDescriptionText", "SkillDescriptionText" },
        { "_attackLabelText", "Stats/Attack/LabelText" }, { "_attackValueText", "Stats/Attack/ValueText" },
        { "_defenseLabelText", "Stats/Defense/LabelText" }, { "_defenseValueText", "Stats/Defense/ValueText" },
        { "_healthLabelText", "Stats/Health/LabelText" }, { "_healthValueText", "Stats/Health/ValueText" },
        { "_areaTitleText", "AreaTitleText" }, { "_areaDescriptionText", "AreaDescriptionText" }
    };

    [MenuItem("Tools/OZGL2/Battle/Redesign/Migrate Saved Unit and Land Text to TMP")]
    public static void ApplySavedMenu() => Debug.Log(ApplySaved());

    public static string ApplySaved()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행해 주세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage == null || !stage.scene.isDirty, "열린 Prefab의 미저장 변경을 먼저 저장해 주세요.");
        string stagePath = stage != null ? stage.assetPath : null;
        if (stage != null) StageUtility.GoToMainStage();
        try
        {
            EnsureFonts();
            foreach (string name in new[] { "BattleCard_Unit", "BattleCard_LandSlot" })
            {
                string path = BattleCardRedesignBuilder.PREFAB_ROOT + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConvertCardToTmp(root, false);
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    Need(saved, "TMP Prefab 저장 실패: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return "유닛 Text 12개·지형 Text 4개 TMP/SDF 전환 완료. 기물/Scene 저장 없음.";
        }
        finally
        {
            if (!string.IsNullOrEmpty(stagePath)) PrefabStageUtility.OpenPrefab(stagePath);
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Redesign/Migrate Open Card Text to TMP (Undo)")]
    public static void ApplyOpen()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage != null && (stage.assetPath == BattleCardRedesignBuilder.PREFAB_ROOT + "BattleCard_Unit.prefab" ||
            stage.assetPath == BattleCardRedesignBuilder.PREFAB_ROOT + "BattleCard_LandSlot.prefab"),
            "유닛 또는 지형 카드의 Prefab Mode에서 실행해 주세요.");
        EnsureFonts();
        ConvertCardToTmp(stage.prefabContentsRoot, true);
        EditorSceneManager.MarkSceneDirty(stage.scene);
    }

    public static void EnsureFonts()
    {
        var characters = new StringBuilder("점유 칸수 보유 스킬 스킬 없음 배치 영역 확장 배치 발판 유닛이 설 자리 " +
            "확장할 위치에 배치해 유닛을 놓을 공간을 넓힙니다. 이 모양의 바닥 조각이에요. " +
            "영역 위에 먼저 놓고, 그 위에 유닛을 올리세요. 이미 놓은 발판과 이어서 놓아야 해요 " +
            "근접 원거리 사거리 공격속도 공격력 방어력 체력 ·");
        for (int c = 32; c <= 126; c++) characters.Append((char)c);
        foreach (string name in new[] { "BattleCard_Unit", "BattleCard_LandSlot" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(BattleCardRedesignBuilder.PREFAB_ROOT + name + ".prefab");
            Need(root != null, "카드 없음: " + name);
            foreach (Text text in root.GetComponentsInChildren<Text>(true)) characters.Append(text.text);
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) characters.Append(text.text);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:UIUnitCatalogSO"))
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIUnitCatalogSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (catalog == null || catalog.Entries == null) continue;
            foreach (var entry in catalog.Entries)
            {
                if (entry == null) continue;
                characters.Append(entry.DisplayName);
                if (entry.BaseStats != null)
                    for (int star = 1; star <= 3; star++)
                        characters.Append(BattleCardSkillDescription.Build(entry.BaseStats, star));
            }
        }
        string seed = new string(characters.ToString().Where(c => !char.IsControl(c)).Distinct().ToArray());
        EnsureFont(TITLE_SOURCE, TITLE_FONT_PATH, seed);
        EnsureFont(BODY_SOURCE, BODY_FONT_PATH, seed);
    }

    private static void EnsureFont(string sourcePath, string assetPath, string characters)
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        Need(source != null, "원본 폰트 없음: " + sourcePath);
        var shader = Shader.Find(SHADER_NAME);
        Need(shader != null, "TMP SDF 셰이더 없음");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (font == null)
        {
            Need(AssetDatabase.LoadMainAssetAtPath(assetPath) == null, "다른 에셋을 덮어쓰지 않습니다: " + assetPath);
            EnsureFolder(FONT_ROOT);
            font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, true);
            Need(font != null, "SDF 생성 실패: " + sourcePath);
            font.name = Path.GetFileNameWithoutExtension(assetPath);
            font.material.name = font.name + " Material";
            font.material.shader = shader;
            AssetDatabase.CreateAsset(font, assetPath);
        }
        Need(font.sourceFontFile == source && font.atlasRenderMode == GlyphRenderMode.SDFAA &&
            font.atlasPopulationMode == AtlasPopulationMode.Dynamic && font.material.shader == shader,
            "전용 SDF의 원본/렌더 설정 불일치: " + assetPath);
        // 현재 문구는 미리 굽고, 향후 이름·설명에 추가되는 한글은 같은 원본 폰트에서 확장한다.
        font.isMultiAtlasTexturesEnabled = true;
        string pending = new string(characters.Where(c => !font.HasCharacter(c)).ToArray());
        if (pending.Length > 0)
        {
            // Play Mode 종료 직후에도 Editor의 FontEngine 상태를 준비하고 새 문자만 추가한다.
            Need(FontEngine.InitializeFontEngine() == FontEngineError.Success, "FontEngine 초기화 실패");
            if (!font.TryAddCharacters(pending, out string missing))
                throw new InvalidOperationException("SDF 글리프 추가 실패: " + missing + " / " + assetPath);
        }
        foreach (Texture2D atlas in font.atlasTextures)
        {
            if (atlas == null) continue;
            atlas.filterMode = FilterMode.Bilinear;
            if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(atlas);
        }
        if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font.material);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssetIfDirty(font);
    }

    public static void ConvertCardToTmp(GameObject root, bool registerUndo)
    {
        Need(root != null && (root.name == "BattleCard_Unit" || root.name == "BattleCard_LandSlot"),
            "기물은 TMP 전환 대상이 아닙니다.");
        var titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TITLE_FONT_PATH);
        var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BODY_FONT_PATH);
        Need(titleFont != null && bodyFont != null, "EnsureFonts를 먼저 실행해 주세요.");
        var view = root.GetComponent<UIBattlePreparationCardView>();
        Need(view != null, "카드 View 없음");
        if (registerUndo) Undo.RegisterFullObjectHierarchyUndo(root, UNDO_NAME);
        var bindings = new SerializedObject(view);
        foreach (var pair in TEXT_PATHS)
        {
            Transform child = root.transform.Find(pair.Value);
            if (child == null) continue;
            TMP_FontAsset font = IsTitleField(pair.Key) ? titleFont : bodyFont;
            var legacy = child.GetComponent<Text>();
            var tmp = child.GetComponent<TextMeshProUGUI>();
            if (legacy != null)
            {
                string value = legacy.text;
                Color color = legacy.color;
                int size = legacy.fontSize;
                bool autoSize = legacy.resizeTextForBestFit;
                int min = legacy.resizeTextMinSize, max = legacy.resizeTextMaxSize;
                TextAlignmentOptions alignment = ToTmpAlignment(legacy.alignment);
                bool wrap = legacy.horizontalOverflow == HorizontalWrapMode.Wrap;
                bool richText = legacy.supportRichText, maskable = legacy.maskable, enabled = legacy.enabled;
                float lineSpacing = legacy.lineSpacing;
                TextOverflowModes overflow = legacy.verticalOverflow == VerticalWrapMode.Overflow
                    ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;
                FontStyles style = legacy.fontStyle == FontStyle.Bold ? FontStyles.Bold :
                    legacy.fontStyle == FontStyle.Italic ? FontStyles.Italic :
                    legacy.fontStyle == FontStyle.BoldAndItalic ? FontStyles.Bold | FontStyles.Italic : FontStyles.Normal;
                if (registerUndo) Undo.DestroyObjectImmediate(legacy);
                else Object.DestroyImmediate(legacy);
                tmp = registerUndo ? Undo.AddComponent<TextMeshProUGUI>(child.gameObject) : child.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.font = font;
                tmp.fontSharedMaterial = font.material;
                tmp.text = value;
                tmp.color = color;
                tmp.fontSize = size;
                tmp.enableAutoSizing = autoSize;
                tmp.fontSizeMin = autoSize ? min : size;
                tmp.fontSizeMax = autoSize ? max : size;
                tmp.alignment = alignment;
                tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                tmp.overflowMode = overflow;
                tmp.richText = richText;
                tmp.parseCtrlCharacters = false;
                tmp.fontStyle = style;
                tmp.lineSpacing = (lineSpacing - 1f) * size;
                tmp.margin = Vector4.zero;
                tmp.maskable = maskable;
                tmp.enabled = enabled;
                tmp.raycastTarget = false;
            }
            Need(tmp != null, "Text/TMP 없음: " + pair.Value);
            Need(tmp.font == font, "다른 TMP 폰트가 연결되어 있습니다: " + pair.Value);
            var property = bindings.FindProperty(pair.Key);
            Need(property != null, "View 필드 없음: " + pair.Key);
            property.objectReferenceValue = tmp;
        }
        if (registerUndo) bindings.ApplyModifiedProperties();
        else bindings.ApplyModifiedPropertiesWithoutUndo();
        Need(root.GetComponentsInChildren<Text>(true).Length == 0, "대상 카드에 Legacy Text가 남았습니다.");
    }

    private static bool IsTitleField(string field) => field == "_titleText" || field == "_rankText" ||
        field == "_traitTitleText" || field == "_skillTitleText" || field == "_areaTitleText" || field.EndsWith("ValueText");

    public static TextAlignmentOptions ToTmpAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            default: return TextAlignmentOptions.BottomRight;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
