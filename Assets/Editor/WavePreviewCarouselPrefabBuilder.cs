using System;
using System.IO;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 웨이브 데이터와 상위 HUD 참조는 유지하고, 승인된 표시 영역만 Prefab 안에 설치한다.
public static class WavePreviewCarouselPrefabBuilder
{
    public const string PREFAB_PATH = "Assets/06.UI/BattleMutedPreview/Prefabs/WavePreviewPanel.prefab";
    public const string ARROW_PATH = "Assets/06.UI/BattleMutedPreview/WaveCarousel_v1/Arrow_WavePrevious.png";
    public const string ARROW_SPRITE_PATH = "Assets/06.UI/BattleMutedPreview/WaveCarousel_v1/Arrow_WavePrevious.asset";
    public const string HEADER_DIVIDER_PATH = "Assets/06.UI/BattleMutedPreview/WaveCarousel_v1/Divider_WaveHeader.png";
    public const string HEADER_DIVIDER_SPRITE_PATH = "Assets/06.UI/BattleMutedPreview/WaveCarousel_v1/Divider_WaveHeader.asset";
    public const string UNIT_DIVIDER_SPRITE_PATH = "Assets/06.UI/BattleMutedPreview/FlatReference_v1/Sprites/Divider_WaveUnit_Flat.png";
    private const string PORTRAIT_ROOT = "Assets/06.UI/BattleMutedPreview/WaveCarousel_v1/Portraits";
    private const string BOSS_PREFAB_ROOT = "Assets/02.Prefabs/Hero_Unit/Boss/";
    private const string FONT_PATH = "Assets/06.UI/BattleMutedPreview/Heraldry_Cards_v2/Fonts/BattleCardBody SDF.asset";
    private const string UNIT_CATALOG_PATH = "Assets/06.UI/LobbyMutedPreview/Heraldry_Codex_v1/UnitCatalog.asset";
    private const string UNDO_NAME = "웨이브 미리보기 캐러셀 배치";
    private const string ART_UNDO_NAME = "웨이브 미리보기 화살표·구분선 적용";
    private const float ITEM_WIDTH = 144f;
    private const float ITEM_HEIGHT = 103f;
    private static readonly Color IVORY = new Color32(246, 232, 198, 255);
    private static readonly string[] BOSS_IDS = { "H_BOSS_01", "H_BOSS_02", "H_BOSS_FINAL_01", "H_BOSS_04", "H_BOSS_05" };

    [MenuItem("Tools/OZGL2/Battle/Wave Preview/Apply Carousel to Saved Prefab")]
    public static void ApplySavedMenu()
    {
        Debug.Log(ApplySaved());
    }

    // 저장형은 임시 Prefab을 언로드한다. 원본 파일 저장 자체는 Unity Undo 대상이 아니다.
    public static string ApplySaved()
    {
        RequireEditMode();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage == null || !stage.scene.isDirty, "열린 Prefab의 미저장 변경을 먼저 저장해 주세요.");
        string stagePath = stage != null ? stage.assetPath : null;
        if (stage != null) StageUtility.GoToMainStage();
        try
        {
            ImportArtwork();
            PrepareBossPortraits();
            GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
            try
            {
                ApplyLayout(root, false);
                bool saved;
                PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out saved);
                Need(saved, "WavePreviewPanel Prefab 저장 실패.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return "WavePreviewPanel 캐러셀 설치 완료. 기존 프레임·제목·HUD 참조 유지, Scene 저장 없음.";
        }
        finally
        {
            if (!string.IsNullOrEmpty(stagePath)) PrefabStageUtility.OpenPrefab(stagePath);
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Wave Preview/Apply Carousel to Open Prefab (Undo)")]
    public static void ApplyOpen()
    {
        RequireEditMode();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage != null && stage.assetPath == PREFAB_PATH,
            "WavePreviewPanel의 Prefab Mode에서 실행해 주세요.");
        ImportArtwork();
        PrepareBossPortraits();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        try
        {
            ApplyLayout(stage.prefabContentsRoot, true);
            EditorSceneManager.MarkSceneDirty(stage.scene);
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }
        Debug.Log("열린 WavePreviewPanel에 캐러셀 배치를 적용했습니다. Prefab Mode 변경은 Undo 가능하며 저장은 별도입니다.");
    }

    [MenuItem("Tools/OZGL2/Battle/Wave Preview/Apply Artwork to Saved Prefab")]
    public static void ApplySavedArtworkMenu()
    {
        Debug.Log(ApplySavedArtwork());
    }

    // 장식 교체는 기존 Carousel과 항목의 FileID, 시각 배치, Runtime 참조를 보존한다.
    public static string ApplySavedArtwork()
    {
        RequireEditMode();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage == null || !stage.scene.isDirty, "열린 Prefab의 미저장 변경을 먼저 저장해 주세요.");
        string stagePath = stage != null ? stage.assetPath : null;
        if (stage != null) StageUtility.GoToMainStage();
        try
        {
            ImportArtwork();
            GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
            try
            {
                ApplyArtwork(root, false);
                bool saved;
                PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out saved);
                Need(saved, "WavePreviewPanel 장식 저장 실패.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return "WavePreviewPanel 화살표·헤더·유닛 구분선 교체 완료. 기존 시각 위치·참조 유지, Scene 저장 없음.";
        }
        finally
        {
            if (!string.IsNullOrEmpty(stagePath)) PrefabStageUtility.OpenPrefab(stagePath);
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Wave Preview/Apply Artwork to Open Prefab (Undo)")]
    public static void ApplyOpenArtwork()
    {
        RequireEditMode();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Need(stage != null && stage.assetPath == PREFAB_PATH,
            "WavePreviewPanel의 Prefab Mode에서 실행해 주세요.");
        ImportArtwork();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(ART_UNDO_NAME);
        try
        {
            Undo.RegisterFullObjectHierarchyUndo(stage.prefabContentsRoot, ART_UNDO_NAME);
            ApplyArtwork(stage.prefabContentsRoot, true);
            EditorSceneManager.MarkSceneDirty(stage.scene);
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }
        Debug.Log("열린 WavePreviewPanel의 장식만 교체했습니다. 배치 변경은 Undo 가능하며 저장은 별도입니다.");
    }

    private static void ImportArtwork()
    {
        ImportArrow();
        ImportGeneratedSprite(HEADER_DIVIDER_PATH, HEADER_DIVIDER_SPRITE_PATH, "Divider_WaveHeader");
    }

    private static void ImportArrow()
    {
        ImportGeneratedSprite(ARROW_PATH, ARROW_SPRITE_PATH, "Arrow_WavePrevious");
    }

    private static void ImportGeneratedSprite(string texturePath, string spritePath, string spriteName)
    {
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        Need(importer != null, "새 장식 이미지가 필요합니다: " + texturePath);
        // 생성한 전용 Sprite만 임포트하며, 재사용하는 기존 그림과 폰트는 수정하지 않는다.
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spritePixelsPerUnit = 100;
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        CreateTightSprite(importer, texturePath, spritePath, spriteName);
    }

    private static void PrepareBossPortraits()
    {
        if (!AssetDatabase.IsValidFolder(PORTRAIT_ROOT))
            Need(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(
                "Assets/06.UI/BattleMutedPreview/WaveCarousel_v1", "Portraits")), "보스 초상화 폴더 생성 실패.");
        for (int i = 0; i < BOSS_IDS.Length; i++)
        {
            string pngPath = PORTRAIT_ROOT + "/" + BOSS_IDS[i] + ".png";
            string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", pngPath));
            // 생성한 표시용 이미지가 이미 있으면 디자이너의 수정 결과를 유지한다.
            if (!File.Exists(absolutePath))
                RenderBossPortrait(BOSS_PREFAB_ROOT + "Boss_" + (i + 1) + ".prefab", absolutePath);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            Need(importer != null, "보스 초상화 이미지 임포트 실패: " + pngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
            CreateTightSprite(importer, pngPath, PORTRAIT_ROOT + "/" + BOSS_IDS[i] + ".asset", BOSS_IDS[i]);
        }
    }

    private static void RenderBossPortrait(string prefabPath, string absolutePath)
    {
        GameObject clone = null;
        PreviewRenderUtility preview = null;
        Texture2D texture = null;
        RenderTexture readback = null;
        RenderTexture previousTarget = RenderTexture.active;
        bool previousSrgbWrite = GL.sRGBWrite;
        bool isPreviewOpen = false;
        try
        {
            clone = Object.Instantiate(RequireAsset<GameObject>(prefabPath));
            clone.hideFlags = HideFlags.HideAndDontSave;
            preview = new PreviewRenderUtility();
            preview.AddSingleGO(clone);
            foreach (Canvas canvas in clone.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            bool hasBounds = false;
            Bounds bounds = new Bounds();
            foreach (SpriteRenderer renderer in clone.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name == "Shadow")
                {
                    renderer.enabled = false;
                    continue;
                }
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null) continue;
                if (hasBounds) bounds.Encapsulate(renderer.bounds);
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }
            Need(hasBounds, "보스의 표시 가능한 SpriteRenderer가 없습니다: " + prefabPath);
            Camera camera = preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(0.01f, Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.1f);
            camera.transform.position = bounds.center + Vector3.back * 10;
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            // EndStaticPreview는 RGB24로 변환하므로 배경의 알파를 보존하는 일반 Preview를 읽는다.
            preview.BeginPreview(new Rect(0, 0, 256, 256), GUIStyle.none);
            isPreviewOpen = true;
            preview.Render(true);
            RenderTexture rendered = preview.EndPreview() as RenderTexture;
            isPreviewOpen = false;
            Need(rendered != null, "보스 초상화 RenderTexture 렌더 실패: " + prefabPath);
            // 고해상도 Editor 배율에서도 출력은 256px로 고정하고 Linear 프로젝트의 색을 PNG용 sRGB로 변환한다.
            readback = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            readback.filterMode = FilterMode.Point;
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(rendered, readback);
            GL.sRGBWrite = previousSrgbWrite;
            RenderTexture.active = readback;
            texture = new Texture2D(256, 256, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0, false);
            texture.Apply(false, false);
            RenderTexture.active = previousTarget;
            bool hasTransparent = false, hasBody = false;
            foreach (Color32 pixel in texture.GetPixels32())
            {
                if (pixel.a == 0) hasTransparent = true;
                if (pixel.a > 5) hasBody = true;
                if (hasTransparent && hasBody) break;
            }
            Need(hasTransparent && hasBody, "보스 초상화의 투명 배경 또는 본체 렌더를 확인할 수 없습니다: " + prefabPath);
            byte[] png = texture.EncodeToPNG();
            Need(png != null && png.Length > 0, "보스 초상화 PNG 인코딩 실패: " + prefabPath);
            File.WriteAllBytes(absolutePath, png);
        }
        finally
        {
            try
            {
                if (isPreviewOpen && preview != null) preview.EndPreview();
            }
            finally
            {
                GL.sRGBWrite = previousSrgbWrite;
                RenderTexture.active = previousTarget;
                if (readback != null) RenderTexture.ReleaseTemporary(readback);
                if (texture != null) Object.DestroyImmediate(texture);
                try
                {
                    if (preview != null) preview.Cleanup();
                }
                finally
                {
                    // Preview가 정리하기 전 단계에서 예외가 나도 임시 복제본을 남기지 않는다.
                    if (clone != null) Object.DestroyImmediate(clone);
                }
            }
        }
    }

    private static void CreateTightSprite(TextureImporter importer, string texturePath, string spritePath, string spriteName)
    {
        bool wasReadable = importer.isReadable;
        Sprite temporary = null;
        try
        {
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            Texture2D texture = RequireAsset<Texture2D>(texturePath);
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a <= 5) continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            Need(maxX >= minX && maxY >= minY, "PNG에 불투명한 픽셀이 없습니다: " + texturePath);
            const int PADDING = 3;
            minX = Mathf.Max(0, minX - PADDING);
            minY = Mathf.Max(0, minY - PADDING);
            maxX = Mathf.Min(texture.width - 1, maxX + PADDING);
            maxY = Mathf.Min(texture.height - 1, maxY + PADDING);
            var bounds = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            temporary = Sprite.Create(texture, bounds, new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            temporary.name = spriteName;
            Sprite saved = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (saved == null)
            {
                Need(AssetDatabase.LoadMainAssetAtPath(spritePath) == null,
                    "다른 에셋을 덮어쓰지 않습니다: " + spritePath);
                AssetDatabase.CreateAsset(temporary, spritePath);
                saved = temporary;
                temporary = null;
            }
            else
            {
                // 파일을 재생성하지 않고 내용만 갱신해 기존 Sprite GUID와 연결을 보존한다.
                EditorUtility.CopySerialized(temporary, saved);
                EditorUtility.SetDirty(saved);
            }
            AssetDatabase.SaveAssetIfDirty(saved);
        }
        finally
        {
            if (temporary != null) Object.DestroyImmediate(temporary);
            if (importer.isReadable != wasReadable)
            {
                importer.isReadable = wasReadable;
                importer.SaveAndReimport();
            }
        }
    }

    private static void ApplyLayout(GameObject root, bool registerUndo)
    {
        Need(root != null && root.GetComponent<RectTransform>() != null,
            "WavePreviewPanel RectTransform이 필요합니다.");
        Sprite arrow = RequireAsset<Sprite>(ARROW_SPRITE_PATH);
        Sprite dividerSprite = RequireAsset<Sprite>(UNIT_DIVIDER_SPRITE_PATH);
        TMP_FontAsset font = RequireAsset<TMP_FontAsset>(FONT_PATH);
        UIUnitCatalogSO catalog = RequireAsset<UIUnitCatalogSO>(UNIT_CATALOG_PATH);

        // 외부 UIBattleMutedPreviewView의 직접 참조가 연결된 노드는 삭제하거나 이름을 바꾸지 않는다.
        var legacyRows = new GameObject[3];
        var sampleNames = new string[3];
        var sampleCounts = new string[3];
        var sampleSprites = new Sprite[3];
        for (int i = 0; i < legacyRows.Length; i++)
        {
            Transform row = root.transform.Find("Enemy_" + i);
            Need(row != null, "기존 HUD 참조용 Enemy_" + i + "가 없습니다.");
            legacyRows[i] = row.gameObject;
            Text name = row.Find("Name") != null ? row.Find("Name").GetComponent<Text>() : null;
            Text count = row.Find("Count") != null ? row.Find("Count").GetComponent<Text>() : null;
            Image icon = row.Find("Icon") != null ? row.Find("Icon").GetComponent<Image>() : null;
            sampleNames[i] = name != null ? name.text : "용사";
            sampleCounts[i] = count != null ? count.text : "×1";
            sampleSprites[i] = icon != null ? icon.sprite : null;
        }

        if (registerUndo) Undo.RegisterFullObjectHierarchyUndo(root, UNDO_NAME);
        Transform previous = root.transform.Find("Carousel");
        if (previous != null)
        {
            if (registerUndo) Undo.DestroyObjectImmediate(previous.gameObject);
            else Object.DestroyImmediate(previous.gameObject);
        }
        foreach (GameObject row in legacyRows) row.SetActive(false);

        RectTransform carouselRoot = CreateRect("Carousel", root.transform, 0, 0, 592, 242, registerUndo);
        // 중첩 Prefab 인스턴스의 높이 오버라이드도 따라가되 원본 프레임/제목은 변경하지 않는다.
        carouselRoot.anchorMin = Vector2.zero;
        carouselRoot.anchorMax = Vector2.one;
        carouselRoot.pivot = new Vector2(0, 1);
        carouselRoot.offsetMin = Vector2.zero;
        carouselRoot.offsetMax = Vector2.zero;
        carouselRoot.sizeDelta = Vector2.zero;
        carouselRoot.anchoredPosition = Vector2.zero;
        RectTransform viewport = CreateRect("EnemyViewport", carouselRoot, 80, 103, ITEM_WIDTH * 3, ITEM_HEIGHT, registerUndo);
        viewport.anchorMin = new Vector2(0, 0.5f);
        viewport.anchorMax = new Vector2(1, 0.5f);
        viewport.pivot = new Vector2(0, 1);
        viewport.sizeDelta = new Vector2(-160, ITEM_HEIGHT);
        viewport.anchoredPosition = new Vector2(80, 9);
        AddComponent<RectMask2D>(viewport.gameObject, registerUndo);
        RectTransform content = CreateRect("Content", viewport, 0, 0, ITEM_WIDTH * 3, ITEM_HEIGHT, registerUndo);

        UIWavePreviewEnemyItem template = CreateItem("EnemyItemTemplate", content, 0, font,
            sampleSprites[0], sampleNames[0], sampleCounts[0], registerUndo);
        template.gameObject.SetActive(false);
        RectTransform designPreview = CreateRect("DesignPreview", content, 0, 0, ITEM_WIDTH * 3, ITEM_HEIGHT, registerUndo);
        for (int i = 0; i < 3; i++)
            CreateItem("Preview_" + i, designPreview, ITEM_WIDTH * i, font,
                sampleSprites[i], sampleNames[i], sampleCounts[i], registerUndo);

        // 고정 구분선은 움직이는 Content의 밖에 놓고 입력을 받지 않도록 한다.
        var separators = new RectTransform[2];
        for (int i = 1; i <= 2; i++)
        {
            RectTransform separator = CreateRect("Separator_" + i, viewport,
                ITEM_WIDTH * i - 1, 4, 2, 95, registerUndo);
            separators[i - 1] = separator;
            Image image = AddComponent<Image>(separator.gameObject, registerUndo);
            image.sprite = dividerSprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        Button previousButton = CreateButton("PreviousButton", carouselRoot, 35, arrow, false, registerUndo);
        Button nextButton = CreateButton("NextButton", carouselRoot, 517, arrow, true, registerUndo);

        var controller = root.GetComponent<UIWavePreviewCarousel>();
        if (controller == null) controller = AddComponent<UIWavePreviewCarousel>(root, registerUndo);
        var bindings = new SerializedObject(controller);
        Bind(bindings, "_viewport", viewport);
        Bind(bindings, "_content", content);
        Bind(bindings, "_previousButton", previousButton);
        Bind(bindings, "_nextButton", nextButton);
        Bind(bindings, "_itemTemplate", template);
        Bind(bindings, "_designTimePreview", designPreview.gameObject);
        Bind(bindings, "_unitCatalog", catalog);
        Bind(bindings, "_bootstrap", null);
        SerializedProperty separatorBindings = RequireProperty(bindings, "_separators");
        separatorBindings.arraySize = separators.Length;
        for (int i = 0; i < separators.Length; i++)
            separatorBindings.GetArrayElementAtIndex(i).objectReferenceValue = separators[i];
        UpsertBossPortraits(bindings);
        SerializedProperty legacy = RequireProperty(bindings, "_legacyRows");
        legacy.arraySize = legacyRows.Length;
        for (int i = 0; i < legacyRows.Length; i++)
            legacy.GetArrayElementAtIndex(i).objectReferenceValue = legacyRows[i];
        RequireProperty(bindings, "_visibleCount").intValue = 3;
        RequireProperty(bindings, "_duration").floatValue = 0.2f;
        if (registerUndo) bindings.ApplyModifiedProperties();
        else bindings.ApplyModifiedPropertiesWithoutUndo();
        ApplyArtwork(root, registerUndo);
        EditorUtility.SetDirty(root);
    }

    private static void ApplyArtwork(GameObject root, bool registerUndo)
    {
        Need(root != null, "WavePreviewPanel이 필요합니다.");
        Sprite arrow = RequireAsset<Sprite>(ARROW_SPRITE_PATH);
        Sprite headerSprite = RequireAsset<Sprite>(HEADER_DIVIDER_SPRITE_PATH);
        Sprite unitSprite = RequireAsset<Sprite>(UNIT_DIVIDER_SPRITE_PATH);
        Transform header = root.transform.Find("HeaderLine");
        Need(header != null && header.GetComponent<RectTransform>() != null,
            "기존 HeaderLine이 없습니다.");
        RectTransform headerRect = header.GetComponent<RectTransform>();
        RectTransform headerParent = header.parent as RectTransform;
        Need(headerParent != null && Mathf.Approximately(headerRect.anchorMin.x, headerRect.anchorMax.x),
            "HeaderLine의 가로 Stretch anchor는 자동 변경하지 않습니다. 고정 anchor를 확인해 주세요.");
        Image headerImage = header.GetComponent<Image>();
        Need(headerImage != null, "기존 HeaderLine Image가 없습니다.");
        Image previous = RequireImage(root.transform, "Carousel/PreviousButton/Arrow");
        Image next = RequireImage(root.transform, "Carousel/NextButton/Arrow");
        var separators = new Image[2];
        for (int i = 0; i < separators.Length; i++)
            separators[i] = RequireImage(root.transform, "Carousel/EnemyViewport/Separator_" + (i + 1));

        // 가로 anchor를 왼쪽 기준으로 통일해 기존 Scene의 x=51 오버라이드와 일치시킨다.
        // 원본의 시각적 위치와 Y 설정, 폭, ID는 유지한다. 예: (.5,-245) → (0,51).
        Vector2 headerPosition = headerRect.anchoredPosition;
        headerPosition.x += headerParent.rect.width * headerRect.anchorMin.x;
        headerRect.anchorMin = new Vector2(0, headerRect.anchorMin.y);
        headerRect.anchorMax = new Vector2(0, headerRect.anchorMax.y);
        headerRect.anchoredPosition = headerPosition;
        headerImage.enabled = false;
        Transform existingArtwork = header.Find("Artwork");
        RectTransform artwork = existingArtwork != null ? existingArtwork.GetComponent<RectTransform>() :
            CreateRect("Artwork", header, 0, 0, 1, 12, registerUndo);
        Need(artwork != null, "HeaderLine/Artwork에는 RectTransform이 필요합니다.");
        artwork.anchorMin = new Vector2(0, 0.5f);
        artwork.anchorMax = new Vector2(1, 0.5f);
        artwork.pivot = new Vector2(0.5f, 0.5f);
        artwork.sizeDelta = new Vector2(0, 12);
        artwork.anchoredPosition = Vector2.zero;
        Image artworkImage = artwork.GetComponent<Image>();
        if (artworkImage == null) artworkImage = AddComponent<Image>(artwork.gameObject, registerUndo);
        artworkImage.sprite = headerSprite;
        artworkImage.type = Image.Type.Simple;
        artworkImage.color = Color.white;
        artworkImage.preserveAspect = false;
        artworkImage.raycastTarget = false;

        foreach (Image image in new[] { previous, next })
        {
            image.sprite = arrow;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        foreach (Image image in separators)
        {
            RectTransform rect = image.rectTransform;
            Vector2 position = rect.anchoredPosition;
            float centerX = position.x + (0.5f - rect.pivot.x) * rect.rect.width;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 2);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 95);
            position.x = centerX - (0.5f - rect.pivot.x) * rect.rect.width;
            rect.anchoredPosition = position;
            image.sprite = unitSprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }
        EditorUtility.SetDirty(root);
    }

    private static Image RequireImage(Transform root, string path)
    {
        Transform child = root.Find(path);
        Image image = child != null ? child.GetComponent<Image>() : null;
        Need(image != null, "캐러셀 장식 교체 대상이 없습니다. 먼저 캐러셀을 설치해 주세요: " + path);
        return image;
    }

    private static void UpsertBossPortraits(SerializedObject bindings)
    {
        SerializedProperty portraits = RequireProperty(bindings, "_portraitOverrides");
        foreach (string id in BOSS_IDS)
        {
            int index = -1;
            for (int i = 0; i < portraits.arraySize; i++)
            {
                SerializedProperty candidate = portraits.GetArrayElementAtIndex(i).FindPropertyRelative("_heroId");
                if (candidate != null && candidate.stringValue == id)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                index = portraits.arraySize;
                portraits.InsertArrayElementAtIndex(index);
            }
            SerializedProperty item = portraits.GetArrayElementAtIndex(index);
            SerializedProperty heroId = item.FindPropertyRelative("_heroId");
            SerializedProperty portrait = item.FindPropertyRelative("_portrait");
            Need(heroId != null && portrait != null, "보스 초상화 Runtime 필드 연결 누락.");
            heroId.stringValue = id;
            portrait.objectReferenceValue = RequireAsset<Sprite>(PORTRAIT_ROOT + "/" + id + ".asset");
        }
    }

    private static UIWavePreviewEnemyItem CreateItem(string name, Transform parent, float x, TMP_FontAsset font,
        Sprite sprite, string unitName, string count, bool registerUndo)
    {
        RectTransform item = CreateRect(name, parent, x, 0, ITEM_WIDTH, ITEM_HEIGHT, registerUndo);
        RectTransform iconRect = CreateRect("Icon", item, 22, 4, 56, 58, registerUndo);
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 1);
        iconRect.anchoredPosition = new Vector2(-22, -4);
        Image icon = AddComponent<Image>(iconRect.gameObject, registerUndo);
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        TextMeshProUGUI countText = CreateText("Count", item, 81, 16, 54, 35, font, count, 25, registerUndo);
        countText.rectTransform.anchorMin = countText.rectTransform.anchorMax = new Vector2(0.5f, 1);
        countText.rectTransform.pivot = new Vector2(0, 1);
        countText.rectTransform.anchoredPosition = new Vector2(9, -16);
        countText.alignment = TextAlignmentOptions.MidlineLeft;
        TextMeshProUGUI nameText = CreateText("Name", item, 7, 66, 130, 34, font, unitName, 18, registerUndo);
        nameText.rectTransform.anchorMin = new Vector2(0, 1);
        nameText.rectTransform.anchorMax = Vector2.one;
        nameText.rectTransform.pivot = new Vector2(0, 1);
        nameText.rectTransform.sizeDelta = new Vector2(-14, 34);
        nameText.rectTransform.anchoredPosition = new Vector2(7, -66);
        nameText.fontSizeMin = 14;
        nameText.fontSizeMax = 18;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        UIWavePreviewEnemyItem view = AddComponent<UIWavePreviewEnemyItem>(item.gameObject, registerUndo);
        var bindings = new SerializedObject(view);
        Bind(bindings, "_icon", icon);
        Bind(bindings, "_nameText", nameText);
        Bind(bindings, "_countText", countText);
        if (registerUndo) bindings.ApplyModifiedProperties();
        else bindings.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static Button CreateButton(string name, Transform parent, float x, Sprite sprite,
        bool reverse, bool registerUndo)
    {
        RectTransform rect = CreateRect(name, parent, x, 109, 40, 94, registerUndo);
        rect.anchorMin = rect.anchorMax = new Vector2(reverse ? 1 : 0, 0.5f);
        rect.pivot = new Vector2(reverse ? 1 : 0, 0.5f);
        rect.anchoredPosition = new Vector2(reverse ? -35 : 35, -42.5f);
        Image hitArea = AddComponent<Image>(rect.gameObject, registerUndo);
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        RectTransform arrowRect = CreateRect("Arrow", rect, 6, 23, 28, 48, registerUndo);
        arrowRect.anchorMin = arrowRect.anchorMax = arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.anchoredPosition = Vector2.zero;
        arrowRect.localRotation = Quaternion.Euler(0, 0, reverse ? 180 : 0);
        Image arrow = AddComponent<Image>(arrowRect.gameObject, registerUndo);
        arrow.sprite = sprite;
        arrow.color = Color.white;
        arrow.preserveAspect = true;
        arrow.raycastTarget = false;
        Button button = AddComponent<Button>(rect.gameObject, registerUndo);
        button.targetGraphic = arrow;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(255, 241, 190, 255);
        colors.pressedColor = new Color32(200, 149, 73, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.35f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        return button;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, float x, float y, float width, float height,
        TMP_FontAsset font, string value, float size, bool registerUndo)
    {
        RectTransform rect = CreateRect(name, parent, x, y, width, height, registerUndo);
        TextMeshProUGUI text = AddComponent<TextMeshProUGUI>(rect.gameObject, registerUndo);
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.text = value;
        text.fontSize = size;
        text.fontSizeMin = Mathf.Max(12, size - 3);
        text.fontSizeMax = size;
        text.enableAutoSizing = true;
        text.color = IVORY;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.richText = false;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent, float x, float y,
        float width, float height, bool registerUndo)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (registerUndo) Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
        go.layer = parent.gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static T AddComponent<T>(GameObject go, bool registerUndo) where T : Component
    {
        return registerUndo ? Undo.AddComponent<T>(go) : go.AddComponent<T>();
    }

    private static void Bind(SerializedObject bindings, string name, Object value)
    {
        RequireProperty(bindings, name).objectReferenceValue = value;
    }

    private static SerializedProperty RequireProperty(SerializedObject bindings, string name)
    {
        SerializedProperty property = bindings.FindProperty(name);
        Need(property != null, "Runtime 필드 연결 누락: " + name);
        return property;
    }

    private static T RequireAsset<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Need(asset != null, "필수 에셋 누락: " + path);
        return asset;
    }

    private static void RequireEditMode()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행해 주세요.");
    }

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
