using System;
using System.Globalization;
using System.Linq;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 카드 아트 임포트와 공용 Prefab 레이아웃 변경만 담당한다. 전투 데이터는 수정하지 않는다.
public static class BattleCardRedesignBuilder
{
    public const string ART_ROOT = "Assets/06.UI/BattleMutedPreview/Heraldry_Cards_v2";
    public const string PREFAB_ROOT = "Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/";
    public const string CATALOG_PATH = ART_ROOT + "/Resources/UIExpansionCardVisualCatalog.asset";
    public const string EXPANSION_TYPE_ICON_PATH = ART_ROOT + "/TypeIcons/Icon_Type_Expansion.png";
    public const string RANK_MATERIAL_PATH = ART_ROOT + "/Fonts/BattleCardRank_WhiteOutline.mat";
    private const float RANK_OUTLINE_WIDTH = 0.18f;
    private static readonly string[] STAT_NAMES = { "Attack", "Defense", "Health" };
    public static readonly string[] SHAPE_IDS =
        { "floor_domino", "floor_line3", "floor_corner3", "floor_square4", "floor_tee4", "floor_l4" };
    private static readonly Color IVORY = new Color32(255, 248, 230, 255);

    [MenuItem("Tools/OZGL2/Battle/Redesign/Import Art and Apply Saved Cards")]
    public static void ApplySavedCardsMenu() => Debug.Log(ApplySavedCards());

    public static string ApplySavedCards()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행해 주세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage == null || !stage.scene.isDirty, "열린 Prefab의 미저장 변경을 먼저 저장해 주세요.");
        string stagePath = stage != null ? stage.assetPath : null;
        if (stage != null) StageUtility.GoToMainStage();
        try
        {
            ImportArt();
            foreach (string name in new[] { "BattleCard_Unit", "BattleCard_LandSlot" })
            {
                string path = PREFAB_ROOT + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyLayout(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    Need(saved, "Prefab 저장 실패: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            BindHandCatalogs();
            // 저장형 배치는 임시 Prefab을 언로드하므로 Undo 대상이 아니다.
            return "카드 2종, 손패 카탈로그, 확장 Sprite 6종 적용 완료. Scene 저장 없음.";
        }
        finally
        {
            if (!string.IsNullOrEmpty(stagePath)) PrefabStageUtility.OpenPrefab(stagePath);
        }
    }

    private static void BindHandCatalogs()
    {
        string path = PREFAB_ROOT + "BattleCardHand.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var adapter = root.GetComponent<UIGridStorageHandAdapter>();
            Need(adapter != null, "손패 어댑터 연결 누락");
            var data = new SerializedObject(adapter);
            data.FindProperty("_unitCatalog").objectReferenceValue =
                Require<UIUnitCatalogSO>("Assets/06.UI/LobbyMutedPreview/Heraldry_Codex_v1/UnitCatalog.asset");
            data.FindProperty("_expansionCardVisuals").objectReferenceValue =
                Require<UIExpansionCardVisualCatalogSO>(CATALOG_PATH);
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            Need(saved, "손패 Prefab 저장 실패");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/OZGL2/Battle/Redesign/Import and Apply Saved Stat Icons")]
    public static void ApplySavedStatIconsMenu() => Debug.Log(ApplySavedStatIcons());

    // 저장형 적용은 유닛 카드의 아이콘 참조만 변경한다. 다른 카드와 Scene은 저장하지 않는다.
    public static string ApplySavedStatIcons()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행해 주세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage == null || !stage.scene.isDirty, "열린 Prefab의 미저장 변경을 먼저 저장해 주세요.");
        string stagePath = stage != null ? stage.assetPath : null;
        if (stage != null) StageUtility.GoToMainStage();
        try
        {
            ImportStatIcons();
            string path = PREFAB_ROOT + "BattleCard_Unit.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyStatIcons(root);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                Need(saved, "아이콘 Prefab 저장 실패: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "유닛 카드 공격력·방어력·체력 Sprite 3종 적용 완료. Scene 저장 없음.";
        }
        finally
        {
            if (!string.IsNullOrEmpty(stagePath)) PrefabStageUtility.OpenPrefab(stagePath);
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Redesign/Apply Stat Icons to Open Unit Card (Undo)")]
    public static void ApplyOpenStatIcons()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage != null && stage.assetPath == PREFAB_ROOT + "BattleCard_Unit.prefab",
            "유닛 카드의 Prefab Mode에서 실행해 주세요.");
        Undo.RegisterFullObjectHierarchyUndo(stage.prefabContentsRoot, "카드 능력치 아이콘 적용");
        ApplyStatIcons(stage.prefabContentsRoot);
        EditorSceneManager.MarkSceneDirty(stage.scene);
    }

    private static void ImportStatIcons()
    {
        foreach (string stat in STAT_NAMES) ImportSprite(ART_ROOT + "/StatIcons/Icon_Stat_" + stat + ".png");
    }

    private static void ApplyStatIcons(GameObject root)
    {
        foreach (string stat in STAT_NAMES)
        {
            var icon = root.transform.Find("Stats/" + stat + "/Icon").GetComponent<Image>();
            icon.sprite = Require<Sprite>(ART_ROOT + "/StatIcons/Icon_Stat_" + stat + ".png");
            icon.color = Color.white;
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Redesign/Import and Apply Saved Type Badges")]
    public static void ApplySavedTypeBadgesMenu() => Debug.Log(ApplySavedTypeBadges());

    // 금색 테두리를 포함한 전용 Sprite를 연결하며, Image 전체의 색을 물들이지 않는다.
    public static string ApplySavedTypeBadges()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행해 주세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage == null || !stage.scene.isDirty, "열린 Prefab의 미저장 변경을 먼저 저장해 주세요.");
        string stagePath = stage != null ? stage.assetPath : null;
        if (stage != null) StageUtility.GoToMainStage();
        try
        {
            ImportTypeBadges();
            foreach (string name in new[] { "BattleCard_Unit", "BattleCard_LandSlot" })
            {
                string path = PREFAB_ROOT + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyTypeBadge(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    Need(saved, "배지 Prefab 저장 실패: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return "TypeBadge/Frame: 유닛 진홍색·지형 올리브색 Sprite 적용 완료. Scene 저장 없음.";
        }
        finally
        {
            if (!string.IsNullOrEmpty(stagePath)) PrefabStageUtility.OpenPrefab(stagePath);
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Redesign/Apply Type Badge to Open Card (Undo)")]
    public static void ApplyOpenTypeBadge()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage != null && (stage.assetPath == PREFAB_ROOT + "BattleCard_Unit.prefab" ||
            stage.assetPath == PREFAB_ROOT + "BattleCard_LandSlot.prefab"), "대상 카드의 Prefab Mode에서 실행해 주세요.");
        Undo.RegisterFullObjectHierarchyUndo(stage.prefabContentsRoot, "카드 종류 배경 적용");
        ApplyTypeBadge(stage.prefabContentsRoot);
        EditorSceneManager.MarkSceneDirty(stage.scene);
    }

    private static void ImportTypeBadges()
    {
        foreach (string type in new[] { "Unit", "LandSlot" })
            ImportSprite(ART_ROOT + "/Badges/Badge_Type_" + type + ".png");
    }

    private static void ApplyTypeBadge(GameObject root)
    {
        Need(root != null && (root.name == "BattleCard_Unit" || root.name == "BattleCard_LandSlot"),
            "유닛 또는 지형 카드만 적용할 수 있습니다.");
        var frame = root.transform.Find("TypeBadge/Frame");
        Need(frame != null && frame.TryGetComponent<Image>(out _), "TypeBadge/Frame Image 연결 누락");
        var image = frame.GetComponent<Image>();
        string type = root.name == "BattleCard_Unit" ? "Unit" : "LandSlot";
        image.sprite = Require<Sprite>(ART_ROOT + "/Badges/Badge_Type_" + type + ".png");
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    [MenuItem("Tools/OZGL2/Battle/Redesign/Import and Apply Saved Expansion Type Icon")]
    public static void ApplySavedExpansionTypeIconMenu() => Debug.Log(ApplySavedExpansionTypeIcon());

    // 확장 종류 아이콘과 배지 배치를 저장한다. 배치는 저장된 유닛 카드와 통일한다.
    public static string ApplySavedExpansionTypeIcon()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행해 주세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage == null || !stage.scene.isDirty, "열린 Prefab의 미저장 변경을 먼저 저장해 주세요.");
        string stagePath = stage != null ? stage.assetPath : null;
        if (stage != null) StageUtility.GoToMainStage();
        try
        {
            ImportSprite(EXPANSION_TYPE_ICON_PATH);
            string path = PREFAB_ROOT + "BattleCard_LandSlot.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyExpansionTypeIcon(root);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                Need(saved, "확장 종류 아이콘 Prefab 저장 실패: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "확장 카드 전용 Sprite 적용 및 유닛 기준 Frame·TypeIcon 배치 통일 완료. Scene 저장 없음.";
        }
        finally
        {
            if (!string.IsNullOrEmpty(stagePath)) PrefabStageUtility.OpenPrefab(stagePath);
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Redesign/Apply Expansion Type Icon to Open Land Card (Undo)")]
    public static void ApplyOpenExpansionTypeIcon()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행해 주세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage != null && stage.assetPath == PREFAB_ROOT + "BattleCard_LandSlot.prefab",
            "지형 카드의 Prefab Mode에서 실행해 주세요.");
        Undo.RegisterFullObjectHierarchyUndo(stage.prefabContentsRoot, "확장 카드 종류 아이콘 적용");
        ApplyExpansionTypeIcon(stage.prefabContentsRoot);
        EditorSceneManager.MarkSceneDirty(stage.scene);
    }

    [MenuItem("Tools/OZGL2/Battle/Redesign/Apply Saved Unit Rank Bold and White Outline")]
    public static void ApplySavedUnitRankStyleMenu() => Debug.Log(ApplySavedUnitRankStyle());

    public static string ApplySavedUnitRankStyle()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행해 주세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage == null || !stage.scene.isDirty, "열린 Prefab의 미저장 변경을 먼저 저장해 주세요.");
        string stagePath = stage != null ? stage.assetPath : null;
        if (stage != null) StageUtility.GoToMainStage();
        try
        {
            string path = PREFAB_ROOT + "BattleCard_Unit.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyUnitRankStyle(root);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                Need(saved, "유닛 RankText Prefab 저장 실패: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "유닛 RankText Bold·전용 흰색 Outline 적용 완료. 공용 폰트/Material과 Scene 저장 없음.";
        }
        finally
        {
            if (!string.IsNullOrEmpty(stagePath)) PrefabStageUtility.OpenPrefab(stagePath);
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Redesign/Apply Unit Rank Style to Open Card (Undo)")]
    public static void ApplyOpenUnitRankStyle()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행해 주세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage != null && stage.assetPath == PREFAB_ROOT + "BattleCard_Unit.prefab",
            "유닛 카드의 Prefab Mode에서 실행해 주세요.");
        Undo.RecordObject(GetRankText(stage.prefabContentsRoot), "RankText Bold와 테두리 적용");
        var material = AssetDatabase.LoadAssetAtPath<Material>(RANK_MATERIAL_PATH);
        if (material != null) Undo.RecordObject(material, "RankText 전용 테두리 적용");
        ApplyUnitRankStyle(stage.prefabContentsRoot);
        EditorSceneManager.MarkSceneDirty(stage.scene);
    }

    private static TMP_Text GetRankText(GameObject root)
    {
        var view = root.GetComponent<UIBattlePreparationCardView>();
        Need(view != null, "카드 View 연결 누락");
        var binding = new SerializedObject(view).FindProperty("_rankText");
        var rank = binding != null ? binding.objectReferenceValue as TMP_Text : null;
        Need(rank != null, "RankText TMP 연결 누락");
        return rank;
    }

    private static void ApplyUnitRankStyle(GameObject root)
    {
        Need(root != null && root.name == "BattleCard_Unit", "유닛 카드만 적용할 수 있습니다.");
        var rank = GetRankText(root);
        Need(rank.font != null && rank.font.material != null, "RankText 폰트/Material 연결 누락");
        var material = AssetDatabase.LoadAssetAtPath<Material>(RANK_MATERIAL_PATH);
        if (material == null)
        {
            // atlas와 기존 셰이더 설정을 복사하되, 공용 SDF Material은 수정하지 않는다.
            material = new Material(rank.font.material) { name = "BattleCardRank_WhiteOutline" };
            AssetDatabase.CreateAsset(material, RANK_MATERIAL_PATH);
        }
        Need(material.HasProperty("_OutlineColor") && material.HasProperty("_OutlineWidth"),
            "RankText Material의 Outline 지원 누락");
        material.SetColor("_OutlineColor", Color.white);
        material.SetFloat("_OutlineWidth", RANK_OUTLINE_WIDTH);
        material.EnableKeyword("OUTLINE_ON");
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);

        rank.fontStyle |= FontStyles.Bold;
        rank.fontSharedMaterial = material;
        rank.extraPadding = true;
        rank.UpdateMeshPadding();
        EditorUtility.SetDirty(rank);
    }

    private static void ApplyExpansionTypeIcon(GameObject root)
    {
        Need(root != null && root.name == "BattleCard_LandSlot", "지형 카드만 적용할 수 있습니다.");
        var target = FindTypeIcon(root);
        Need(target != null && target.TryGetComponent<Image>(out _), "TypeBadge의 TypeIcon Image 연결 누락");
        var image = target.GetComponent<Image>();
        image.sprite = Require<Sprite>(EXPANSION_TYPE_ICON_PATH);
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        ApplyUnitTypeBadgeLayout(root);
    }

    private static Transform FindTypeIcon(GameObject root)
    {
        return root.transform.Find("TypeBadge/Frame/TypeIcon") ??
            root.transform.Find("TypeBadge/TypeIcon");
    }

    private static void ApplyUnitTypeBadgeLayout(GameObject root)
    {
        var unit = Require<GameObject>(PREFAB_ROOT + "BattleCard_Unit.prefab");
        var sourceBadge = unit.transform.Find("TypeBadge") as RectTransform;
        var sourceFrame = unit.transform.Find("TypeBadge/Frame") as RectTransform;
        var sourceIcon = FindTypeIcon(unit) as RectTransform;
        var targetBadge = root.transform.Find("TypeBadge") as RectTransform;
        var targetFrame = root.transform.Find("TypeBadge/Frame") as RectTransform;
        var targetIcon = FindTypeIcon(root) as RectTransform;
        Need(sourceBadge != null && sourceFrame != null && sourceIcon != null &&
            targetBadge != null && targetFrame != null && targetIcon != null,
            "유닛/확장 카드의 TypeBadge, Frame 또는 TypeIcon 연결 누락");

        // 같은 부모 아래에서 RectTransform 값을 복사해야 실제 카드상의 위치도 일치한다.
        var targetParent = sourceIcon.parent == sourceFrame ? targetFrame : targetBadge;
        if (targetIcon.parent != targetParent)
            Undo.SetTransformParent(targetIcon, targetParent, "카드 종류 아이콘 부모 통일");
        CopyRectTransform(sourceBadge, targetBadge);
        CopyRectTransform(sourceFrame, targetFrame);
        CopyRectTransform(sourceIcon, targetIcon);
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition3D = source.anchoredPosition3D;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    // Prefab Mode에서 미세 조정할 때는 이 메뉴를 사용한다. Ctrl+Z로 되돌린 뒤 사용자가 저장한다.
    [MenuItem("Tools/OZGL2/Battle/Redesign/Apply Layout to Open Card (Undo)")]
    public static void ApplyOpenCard()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage != null && (stage.assetPath == PREFAB_ROOT + "BattleCard_Unit.prefab" ||
            stage.assetPath == PREFAB_ROOT + "BattleCard_LandSlot.prefab"), "대상 카드의 Prefab Mode에서 실행해 주세요.");
        Undo.RegisterFullObjectHierarchyUndo(stage.prefabContentsRoot, "카드 레이아웃 적용");
        ApplyLayout(stage.prefabContentsRoot);
        EditorSceneManager.MarkSceneDirty(stage.scene);
    }

    public static void ImportArt()
    {
        foreach (string shell in new[] { "CardShell_Unit", "CardShell_LandSlot" })
            ImportSprite(ART_ROOT + "/Sprites/" + shell + ".png");
        foreach (string id in SHAPE_IDS) ImportSprite(ART_ROOT + "/ExpansionArt/" + id + ".png");
        ImportStatIcons();
        ImportTypeBadges();
        ImportSprite(EXPANSION_TYPE_ICON_PATH);
        BattleCardTmpMigrationBuilder.EnsureFonts();
        if (!AssetDatabase.IsValidFolder(ART_ROOT + "/Resources"))
            AssetDatabase.CreateFolder(ART_ROOT, "Resources");
        var catalog = AssetDatabase.LoadAssetAtPath<UIExpansionCardVisualCatalogSO>(CATALOG_PATH);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<UIExpansionCardVisualCatalogSO>();
            AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
        }
        var data = new SerializedObject(catalog);
        var entries = data.FindProperty("_entries");
        entries.arraySize = SHAPE_IDS.Length;
        for (int i = 0; i < SHAPE_IDS.Length; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("_shapeId").stringValue = SHAPE_IDS[i];
            entry.FindPropertyRelative("_artwork").objectReferenceValue =
                Require<Sprite>(ART_ROOT + "/ExpansionArt/" + SHAPE_IDS[i] + ".png");
        }
        data.FindProperty("_fallbackArtwork").objectReferenceValue = null;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
    }

    private static void ImportSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Need(importer != null, "이미지가 없습니다: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.spriteBorder = Vector4.zero;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static void ApplyLayout(GameObject root)
    {
        BattleCardTmpMigrationBuilder.ConvertCardToTmp(root, true);
        bool isUnit = root.name == "BattleCard_Unit";
        var view = root.GetComponent<UIBattlePreparationCardView>();
        Need(view != null, "카드 View 연결 누락");
        Image shell = root.transform.Find("Shell").GetComponent<Image>();
        shell.sprite = Require<Sprite>(ART_ROOT + "/Sprites/CardShell_" + (isUnit ? "Unit" : "LandSlot") + ".png");
        shell.color = Color.white;
        shell.type = Image.Type.Simple;
        shell.preserveAspect = false;
        Rect(shell.rectTransform, 0, 100, 600, 1000);
        // 수동으로 조정한 유닛 배지는 보존하고, 확장 카드는 아래에서 유닛 기준으로 맞춘다.
        ApplyTypeBadge(root);
        root.transform.Find("TypeBadge/BlackDiamond").gameObject.SetActive(false);
        Rect(Find(root, "StarBadge"), 464, isUnit ? 197 : 182, 94, 94);
        // 별 배지 아래로 옮긴 RankText의 수동 배치는 그대로 유지한다.
        Style(root, "RankText", 42, TextAnchor.MiddleCenter, false);
        GetRankText(root).color = new Color32(35, 25, 18, 255);
        if (isUnit) ApplyUnitRankStyle(root);
        Rect(Find(root, "TitleText"), 159, isUnit ? 213 : 198, 302, 60);
        Style(root, "TitleText", 34, TextAnchor.MiddleCenter, false, 28);
        view.SetFootprintVisible(false);

        if (isUnit)
        {
            Rect(Find(root, "Artwork"), 156, 324, 288, 226);
            root.transform.Find("Artwork").GetComponent<Image>().preserveAspect = true;
            root.transform.Find("Artwork").GetComponent<Image>().color = Color.white;
            Rect(Find(root, "TraitTitleText"), 82, 592, 133, 38);
            Style(root, "TraitTitleText", 25, TextAnchor.MiddleCenter, false);
            Rect(Find(root, "TraitDescriptionText"), 239, 595, 265, 62);
            Style(root, "TraitDescriptionText", 34, TextAnchor.MiddleCenter, true, 28);
            Rect(Find(root, "SkillTitleText"), 82, 686, 133, 38);
            Style(root, "SkillTitleText", 25, TextAnchor.MiddleCenter, false);
            Rect(Find(root, "SkillDescriptionText"), 92, 730, 416, 128);
            Style(root, "SkillDescriptionText", 27, TextAnchor.UpperLeft, true, 24);
            Rect(Find(root, "Stats"), 66, 886, 468, 88);
            int index = 0;
            foreach (string stat in STAT_NAMES)
            {
                string path = "Stats/" + stat;
                Rect(Find(root, path), index++ * 156, 0, 156, 88);
                Rect(Find(root, path + "/Icon"), 7, 0, 31, 31);
                Rect(Find(root, path + "/LabelText"), 41, 0, 102, 29);
                Style(root, path + "/LabelText", 23, TextAnchor.MiddleLeft, true);
                Rect(Find(root, path + "/ValueText"), 5, 31, 146, 53);
                Style(root, path + "/ValueText", 42, TextAnchor.MiddleCenter, false, 34);
            }
            ApplyStatIcons(root);
            var entry = FindUnitEntry("unit.M_WAR_01");
            view.SetTitle(entry.DisplayName);
            view.SetRank(1);
            view.SetArtwork(entry.GetPortrait(0));
            view.SetTrait("점유 칸수", "1칸");
            view.SetSkill("보유 스킬", BattleCardSkillDescription.Build(entry.BaseStats, 1));
            view.SetStats(entry.BaseStats.attackPower.ToString("0.#", CultureInfo.InvariantCulture),
                (entry.BaseStats.defensePercent * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%",
                entry.BaseStats.maxHealth.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            // 일반 발판과 미등록 모양의 대체 격자도 새 삽화 창 안에 맞춘다.
            Rect(Find(root, "FootprintGrid"), 144, 317, 380, 380);
            Find(root, "FootprintGrid").localScale = new Vector3(0.82f, 0.82f, 1f);
            var artwork = root.transform.Find("Artwork");
            if (artwork == null)
            {
                var go = new GameObject("Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "확장 카드 삽화 추가");
                go.transform.SetParent(root.transform, false);
                artwork = go.transform;
            }
            Rect((RectTransform)artwork, 133, 315, 334, 310);
            var image = artwork.GetComponent<Image>();
            image.preserveAspect = true;
            image.color = Color.white;
            var data = new SerializedObject(view);
            data.FindProperty("_artwork").objectReferenceValue = image;
            data.ApplyModifiedPropertiesWithoutUndo();
            Rect(Find(root, "AreaTitleText"), 81, 688, 438, 56);
            Style(root, "AreaTitleText", 35, TextAnchor.MiddleCenter, false, 30);
            root.transform.Find("AreaTitleText").GetComponent<TMP_Text>().color = new Color32(255, 226, 113, 255);
            Rect(Find(root, "AreaDescriptionText"), 85, 784, 430, 156);
            Style(root, "AreaDescriptionText", 29, TextAnchor.UpperCenter, true, 25);
            view.SetTitle("배치 영역 확장");
            ApplyExpansionTypeIcon(root);
            view.SetRankText(string.Empty);
            view.SetArtwork(Require<UIExpansionCardVisualCatalogSO>(CATALOG_PATH).GetArtwork("floor_square4"));
            view.SetAreaDescription("배치 영역 +4칸", "확장할 위치에 배치해\n유닛을 놓을 공간을 넓힙니다.");
        }
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }

    public static UIUnitCatalogSO.Entry FindUnitEntry(string id)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UIUnitCatalogSO"))
        {
            var catalog = Require<UIUnitCatalogSO>(AssetDatabase.GUIDToAssetPath(guid));
            var entry = catalog.Entries.FirstOrDefault(item => item != null && item.Id == id);
            if (entry != null && entry.BaseStats != null) return entry;
        }
        throw new InvalidOperationException("유닛 카탈로그 누락: " + id);
    }

    private static void Style(GameObject root, string path, int size, TextAnchor alignment, bool body, int min = 0)
    {
        var text = path == "RankText" ? GetRankText(root) :
            root.transform.Find(path).GetComponent<TMP_Text>();
        text.font = Require<TMP_FontAsset>(body ? BattleCardTmpMigrationBuilder.BODY_FONT_PATH : BattleCardTmpMigrationBuilder.TITLE_FONT_PATH);
        text.fontSharedMaterial = text.font.material;
        text.color = IVORY;
        text.fontStyle = FontStyles.Normal;
        text.fontSize = size;
        text.alignment = BattleCardTmpMigrationBuilder.ToTmpAlignment(alignment);
        text.lineSpacing = 0f;
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = min > 0;
        text.fontSizeMin = min > 0 ? min : size;
        text.fontSizeMax = size;
        text.enabled = true;
    }

    private static RectTransform Find(GameObject root, string path) => (RectTransform)root.transform.Find(path);
    private static void Rect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
    private static T Require<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Need(asset != null, "필수 에셋 없음: " + path);
        return asset;
    }
    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
