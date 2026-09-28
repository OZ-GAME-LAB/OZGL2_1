using System;
using System.IO;
using System.Linq;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>승리/패배 공통 표시만 갱신한다. 전투 데이터와 팝업/로비 연결은 보존한다.</summary>
public static class BattleResultReferenceRefiner
{
    private const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    private const string PREFABS = "Assets/06.UI/BattleMutedPreview/Overlays_v1/Prefabs/";
    private const string ART = "Assets/06.UI/BattleMutedPreview/Overlays_v1/Result/";
    private const string NEW_ART = "Assets/06.UI/BattleMutedPreview/ResultRefinement_v2/Sprites/";
    private const string OLD_ART = "Assets/06.UI/BattleMutedPreview/Sprites/";
    private const string WHITE = "Assets/06.UI/BattleMutedPreview/Reference_v2/Experience_White.png";
    private const string UNDO = "결과 화면 레퍼런스 정렬";
    private static readonly Color IVORY = new Color32(232, 220, 201, 255);
    private static readonly Color METAL = new Color32(255, 222, 181, 255);
    private static readonly Color SECONDARY = new Color32(196, 183, 162, 255);

    [MenuItem("Tools/OZGL2/Battle/Refine Result Reference Layout")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            scene.path != SCENE || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("UI_Battle_MutedPreview를 Edit Mode에서 열어 주세요. Prefab Stage는 먼저 닫아 주세요.");
        string[] names = { "Canvas_BattleResultBase", "Canvas_BattleVictory", "Canvas_BattleDefeat" };
        foreach (string name in names) Require<GameObject>(PREFABS + name + ".prefab");
        // 파일 저장은 씬 Undo와 다르므로 사용자 편집을 작업별 사본으로 먼저 보존한다.
        string backup = "Tools/Art/Backups/BattleResult_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        foreach (string name in names) File.Copy(PREFABS + name + ".prefab", backup + "/" + name + ".prefab");
        File.Copy(SCENE, backup + "/UI_Battle_MutedPreview.unity");
        // 원본 저장 전에 디스크 버전과 현재 미저장 상태를 모두 별도 사본으로 남긴다.
        if (scene.isDirty)
        {
            if (!EditorSceneManager.SaveScene(scene, backup + "/UI_Battle_MutedPreview.unsaved-copy.unity", true))
                throw new IOException("미저장 씬 사본 보존 실패. 원본은 저장하지 않았습니다.");
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("보존 후 씬 저장 실패");
        }
        ImportNewArt();
        TMP_FontAsset titleFont = BattleResultTypography.EnsureTitleFont();
        TMP_FontAsset bodyFont = BattleResultTypography.EnsureBodyFont();
        GameObject root = PrefabUtility.LoadPrefabContents(PREFABS + names[0] + ".prefab");
        try
        {
            ApplyLayout(root, titleFont, bodyFont);
            PrefabUtility.SaveAsPrefabAsset(root, PREFABS + names[0] + ".prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // 전체 Revert는 하지 않는다. 위치/색/폰트의 승인된 시각 속성만 공통 Base를 상속시킨다.
        foreach (string name in names.Skip(1))
        {
            root = PrefabUtility.LoadPrefabContents(PREFABS + name + ".prefab");
            try
            {
                RemoveVisualOverrides(root);
                root.GetComponent<UIBattleResultView>().RefreshView();
                PrefabUtility.SaveAsPrefabAsset(root, PREFABS + name + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        Transform popupRoot = scene.GetRootGameObjects().First(g => g.name == "Canvas_Popups").transform;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO);
        foreach (string name in names.Skip(1))
        {
            GameObject instance = popupRoot.Find(name).gameObject;
            Undo.RegisterFullObjectHierarchyUndo(instance, UNDO);
            RemoveVisualOverrides(instance);
            instance.GetComponent<UIBattleResultView>().RefreshView();
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("결과 프리뷰 씬 저장 실패");
        Debug.Log("결과 Base/승리/패배 표시 정렬 완료. 씬 Undo 지원. Prefab 파일 복구 사본: " + backup);
    }

    private static void ApplyLayout(GameObject root, TMP_FontAsset titleFont, TMP_FontAsset bodyFont)
    {
        Undo.RegisterFullObjectHierarchyUndo(root, UNDO);
        Transform content = root.transform.Find("Content");
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = bodyFont;
            text.fontSharedMaterial = bodyFont.material;
            text.fontStyle = FontStyles.Normal;
            text.color = IVORY;
            text.enableAutoSizing = false;
            text.characterSpacing = 0;
            text.wordSpacing = 0;
            text.richText = true;
            text.margin = Vector4.zero;
            text.raycastTarget = false;
        }
        Image shade = root.transform.Find("BackgroundShade").GetComponent<Image>();
        shade.color = new Color(.012f, .006f, .003f, .94f);
        Set(content, "RuneRing", 0, 340, 410, 410);
        content.Find("RuneRing").GetComponent<Image>().color = new Color(1, 1, 1, .86f);
        Set(content, "CrestFrame", 0, 340, 370, 370);
        Tint(content, "CrestFrame", METAL);
        Set(content, "Crown", 0, 331, 206, 206);
        Tint(content, "Crown", Color.white);
        Set(content, "BannerLeft", -365, 145, 190, 380);
        Set(content, "BannerRight", 365, 145, 190, 380);
        content.Find("BannerRight").localScale = new Vector3(-1, 1, 1);
        Tint(content, "BannerLeft", METAL); Tint(content, "BannerRight", METAL);
        Set(content, "Difficulty", 0, 135, 420, 46);
        Text(content, "Difficulty", 29, IVORY, TextAlignmentOptions.Center);
        Set(content, "TitleAccent", 0, 70, 730, 245);
        content.Find("TitleAccent").GetComponent<Image>().color = new Color(1, 1, 1, .82f);
        Set(content, "ResultTitle", 0, 54, 620, 138);
        TMP_Text title = Text(content, "ResultTitle", 118, IVORY, TextAlignmentOptions.Center);
        title.font = titleFont; title.fontSharedMaterial = titleFont.material;
        title.characterSpacing = 2;

        // 투명 여백을 포함한 1200x400 원본의 실제 외곽은 1160x290이다.
        RectTransform board = Set(content, "RecordPanel", 0, -164, 1280, 426.667f);
        Set(board, "Frame", 0, 0, 1280, 426.667f);
        Tint(board, "Frame", METAL);
        Image interior = Picture(board, "InteriorShade", Require<Sprite>(WHITE), 0, 0, 1110, 220, new Color(0, 0, 0, .52f));
        interior.transform.SetSiblingIndex(1);

        string[] groups = { "Time", "Kills", "Deployments" };
        float[] x = { -382, 0, 382 };
        for (int i = 0; i < groups.Length; i++)
        {
            RectTransform stat = Set(board, groups[i], x[i], 47, 346, 108);
            Set(stat, "IconFrame", -92, 0, 88, 88);
            Tint(stat, "IconFrame", new Color32(198, 169, 131, 255));
            Set(stat, "Icon", -92, 0, 44, 44);
            Tint(stat, "Icon", IVORY);
            Set(stat, "Label", 67, 25, 210, 36);
            Text(stat, "Label", 26, SECONDARY, TextAlignmentOptions.MidlineLeft);
            Set(stat, "Value", 67, -22, 210, 56);
            Text(stat, "Value", 40, IVORY, TextAlignmentOptions.MidlineLeft);
        }
        Set(board, "Divider_0", -180, 49, 7, 96);
        Set(board, "Divider_1", 202, 49, 7, 96);
        Tint(board, "Divider_0", METAL); Tint(board, "Divider_1", METAL);
        Set(board, "ExperienceDivider", 0, -19, 1050, 1.5f);
        Tint(board, "ExperienceDivider", new Color32(125, 98, 70, 190));
        Image center = Picture(board, "ExperienceDividerDiamond", Require<Sprite>(OLD_ART + "Frame_DiamondNeutral.png"), 0, -19, 21, 21, METAL);
        center.transform.SetAsLastSibling();
        Set(board, "ExperienceLabel", -424, -65, 190, 40);
        Text(board, "ExperienceLabel", 25, SECONDARY, TextAlignmentOptions.MidlineLeft);
        Set(board, "ExperienceValue", -257, -65, 210, 44);
        Text(board, "ExperienceValue", 34, IVORY, TextAlignmentOptions.MidlineLeft);
        RectTransform xp = Set(board, "Experience", 58, -65, 456, 76);
        RectTransform track = Set(xp, "Track", 0, 0, 414, 26);
        Set(track, "Background", 0, 0, 414, 26);
        Image fill = track.Find("Fill").GetComponent<Image>();
        fill.color = new Color32(156, 52, 40, 255);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        for (int i = 1; i < 10; i++) Set(track, "Tick_" + i, -207 + i * 41.4f, 0, 3, 26);
        Set(xp, "Frame", 0, 0, 456, 76); Tint(xp, "Frame", METAL);
        Set(board, "Level", 362, -65, 132, 46);
        Text(board, "Level", 34, IVORY, TextAlignmentOptions.Center);
        Set(board, "LevelUp", 500, -65, 134, 44);
        Text(board, "LevelUp", 26, new Color32(189, 53, 43, 255), TextAlignmentOptions.Center);

        Image leftTail = Picture(content, "BannerTailLeft", Require<Sprite>(NEW_ART + "Underframe_Defeat.png"), -310, -347, 142, 148.2f, Color.white);
        Image rightTail = Picture(content, "BannerTailRight", leftTail.sprite, 310, -347, 142, 148.2f, Color.white);
        rightTail.rectTransform.localScale = new Vector3(-1, 1, 1);
        leftTail.transform.SetSiblingIndex(board.GetSiblingIndex());
        rightTail.transform.SetSiblingIndex(board.GetSiblingIndex());
        Set(content, "LobbyButton", 0, -367, 430, 161.25f);
        Tint(content, "LobbyButton", METAL);
        Set(content, "LobbyButton/Text", 0, 8, 270, 62);
        Text(content, "LobbyButton/Text", 45, IVORY, TextAlignmentOptions.Center);
        content.Find("LobbyButton").SetAsLastSibling();
        SerializedObject view = new SerializedObject(root.GetComponent<UIBattleResultView>());
        view.FindProperty("_titleColor").colorValue = IVORY;
        view.FindProperty("_difficultyAccent").colorValue = new Color32(184, 48, 36, 255);
        view.FindProperty("_victoryAccent").colorValue = new Color32(218, 142, 52, 255);
        view.FindProperty("_defeatAccent").colorValue = new Color32(167, 52, 33, 255);
        view.FindProperty("_bannerTailLeft").objectReferenceValue = leftTail;
        view.FindProperty("_bannerTailRight").objectReferenceValue = rightTail;
        view.FindProperty("_victoryBannerTail").objectReferenceValue = Require<Sprite>(NEW_ART + "Underframe_Victory.png");
        view.FindProperty("_defeatBannerTail").objectReferenceValue = leftTail.sprite;
        view.ApplyModifiedPropertiesWithoutUndo();
        root.GetComponent<UIBattleResultView>().RefreshView();
    }

    private static void RemoveVisualOverrides(GameObject instance)
    {
        PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instance);
        if (modifications == null) return;
        PrefabUtility.SetPropertyModifications(instance, modifications.Where(m => !IsVisualOverride(m)).ToArray());
    }

    private static bool IsVisualOverride(PropertyModification modification)
    {
        Component c = modification.target as Component;
        if (c == null) return false;
        string p = modification.propertyPath;
        if (c is UIBattleResultView)
            return p.StartsWith("_victoryAccent.") || p.StartsWith("_defeatAccent.") || p.StartsWith("_titleColor.");
        // 루트의 화면 맞춤/활성 상태/내비게이터와 사용자가 입력한 데이터는 건드리지 않는다.
        bool inContent = c.transform.name == "Content";
        for (Transform t = c.transform.parent; t != null; t = t.parent) if (t.name == "Content") inContent = true;
        if (!inContent) return false;
        if (c is RectTransform)
            return p.StartsWith("m_AnchoredPosition.") || p.StartsWith("m_SizeDelta.") ||
                p.StartsWith("m_LocalScale.") || p.StartsWith("m_AnchorMin.") || p.StartsWith("m_AnchorMax.") || p.StartsWith("m_Pivot.");
        if (c is Image) return p.StartsWith("m_Color.") || p == "m_PreserveAspect";
        if (c is TMP_Text) return p.StartsWith("m_fontColor") || p == "m_fontAsset" || p == "m_sharedMaterial" ||
            p == "m_fontSize" || p == "m_fontSizeBase" || p == "m_fontStyle" || p == "m_enableAutoSizing" ||
            p == "m_HorizontalAlignment" || p == "m_VerticalAlignment" || p == "m_characterSpacing";
        return false;
    }

    private static RectTransform Set(Transform parent, string path, float x, float y, float w, float h)
    {
        RectTransform r = parent.Find(path) as RectTransform;
        if (r == null) throw new InvalidOperationException("결과 레이아웃 누락: " + path);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
        return r;
    }

    private static TMP_Text Text(Transform parent, string path, float size, Color color, TextAlignmentOptions align)
    {
        TMP_Text text = parent.Find(path).GetComponent<TMP_Text>();
        text.fontSize = size; text.color = color; text.alignment = align;
        return text;
    }

    private static void Tint(Transform parent, string path, Color color)
    {
        Image image = parent.Find(path).GetComponent<Image>();
        image.color = color; image.preserveAspect = false;
    }

    private static Image Picture(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        if (existing == null) { go.layer = 5; go.transform.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(go, UNDO); }
        Image image = go.GetComponent<Image>();
        image.sprite = sprite; image.color = color; image.raycastTarget = false;
        Set(parent, name, x, y, w, h);
        return image;
    }

    private static void ImportNewArt()
    {
        foreach (string name in new[] { "Underframe_Victory", "Underframe_Defeat" })
        {
            string path = NEW_ART + name + ".png";
            if (!File.Exists(path)) throw new FileNotFoundException("하단 장식 아트가 필요합니다", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(.5f, .5f);
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    private static T Require<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("필수 결과 에셋 누락: " + path);
        return asset;
    }
}
