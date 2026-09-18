using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OZGL2.Progression;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 특성 영역만 한 Undo 그룹으로 조립한다. 직접 저장하지 않지만 Prefab Mode의 Auto Save 설정은 적용된다.
internal static class LobbyTraitTreeBuilder
{
    private const string PREFAB = "Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab";
    private const string ART = "Assets/06.UI/LobbyMutedPreview/Overlays/TreeArt_v1/";
    private const string FONT = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/DOSMyungjo Overlay Pixel.asset";
    private const string MATERIAL = "Assets/06.UI/LobbyMutedPreview/Overlays/Fonts/DOSMyungjo Trait Outline.mat";
    private static readonly Color _textColor = new Color32(248, 242, 235, 255);
    private static TMP_FontAsset _font;
    private static Material _material;

    private const string DETAIL_ART = "Assets/06.UI/LobbyMutedPreview/Overlays/DetailArt_v2/";

    [MenuItem("Tools/OZGL2/Lobby/Align Trait Heart And Enlarge Detail Header")]
    public static void ApplyTraitIconAndHeaderLayout()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if ((stage == null && scene.path != "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity") ||
            (stage != null && (stage.assetPath != PREFAB || stage.scene.isDirty)))
            throw new InvalidOperationException("대상 씬 또는 저장된 대상 Prefab에서 실행하세요.");
        stage = PrefabStageUtility.OpenPrefab(PREFAB);
        var view = stage.prefabContentsRoot.GetComponentInChildren<UITraitOverlayView>(true);
        if (view == null) throw new InvalidOperationException("특성 화면 없음");
        var heart = view.GetComponentsInChildren<UITraitFrameView>(true)
            .Single(n => n.Definition != null && n.Definition.id == TraitId.Mon_B2);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Align trait heart and enlarge detail header");
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Trait icon and header layout");
        try
        {
            FitIcon(heart.transform.Find("Icon").GetComponent<Image>(), ((RectTransform)heart.transform).rect.width * 0.30f, true);
            LayoutDetailHeader(view.transform.Find("SelectedTraitDetail"));
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("불굴의 살 하트 시각 중심 보정 / 설명창 아이콘 128×128 확대 / 아이콘→등급→이름→레벨 순서 적용. 대상 Prefab만 변경, Ctrl+Z 지원.");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    private static void LayoutDetailHeader(Transform detail)
    {
        // 상단 정보의 읽는 순서와 Hierarchy 순서를 일치시킨다. 하단 내용은 겹침 방지만 조정한다.
        string[] order = { "Icon", "Type", "Name", "Level" };
        for (int i = 0; i < order.Length; i++) detail.Find(order[i]).SetSiblingIndex(i);
        var icon = detail.Find("Icon").GetComponent<Image>();
        Undo.RecordObject(icon, "Preserve enlarged detail icon aspect");
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        SetDetailRect(detail.Find("Icon"), 0, 276, 128, 128);
        SetDetailRect(detail.Find("Type"), 0, 193, 346, 32, 23);
        SetDetailRect(detail.Find("Name"), 0, 153, 354, 40, 32);
        SetDetailRect(detail.Find("Level"), 0, 115, 346, 30, 23);
        SetDetailRect(detail.Find("Description"), 0, 39, 346, 112, 22);
        SetDetailRect(detail.Find("Divider"), 0, -28, 320, 20);
        SetDetailRect(detail.Find("NextDescription"), 0, -95, 346, 112, 22);
        SetDetailRect(detail.Find("Status"), 0, -190, 346, 68, 20);
        SetDetailRect(detail.Find("Refund"), -87, -242, 160, 28, 20);
        SetDetailRect(detail.Find("Cost"), 87, -242, 160, 28, 20);
        SetDetailRect(detail.Find("Downgrade"), -87, -295, 142, 72);
        SetDetailRect(detail.Find("Upgrade"), 87, -295, 142, 72);
    }

    [MenuItem("Tools/OZGL2/Lobby/Apply Demon King Black Backing")]
    public static void ApplyDemonKingBlackBacking()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if ((stage == null && scene.path != "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity") ||
            (stage != null && (stage.assetPath != PREFAB || stage.scene.isDirty)))
            throw new InvalidOperationException("대상 씬 또는 저장된 대상 Prefab에서 실행하세요.");
        var sprite = CreateDemonKingBackingSprite();
        stage = PrefabStageUtility.OpenPrefab(PREFAB);
        var nodes = stage.prefabContentsRoot.transform.Find("TraitsScreen/TraitTreeViewport/TraitTreeContent/Nodes");
        var frame = nodes != null ? nodes.Find("DemonKingFrame") as RectTransform : null;
        if (frame == null) throw new InvalidOperationException("데몬킹 프레임 없음");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add demon king black backing");
        Undo.RegisterFullObjectHierarchyUndo(nodes.gameObject, "Demon king background order");
        try
        {
            var existing = nodes.Find("DemonKingBackground");
            var image = existing != null ? existing.GetComponent<Image>() :
                Image("DemonKingBackground", nodes, sprite, frame.anchoredPosition, frame.sizeDelta);
            if (image == null) throw new InvalidOperationException("기존 배경에 Image가 없습니다.");
            Undo.RecordObject(image, "Configure opaque black backing");
            Undo.RecordObject(image.rectTransform, "Match frame coordinates");
            image.sprite = sprite;
            image.color = Color.white;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.maskable = true;
            var rect = image.rectTransform;
            rect.anchorMin = frame.anchorMin;
            rect.anchorMax = frame.anchorMax;
            rect.pivot = frame.pivot;
            rect.anchoredPosition3D = frame.anchoredPosition3D;
            rect.sizeDelta = frame.sizeDelta;
            rect.localScale = frame.localScale;
            rect.localRotation = frame.localRotation;
            // 마지막으로 보낸 뒤 프레임 앞에 삽입하므로 재실행해도 순서가 바뀌지 않는다.
            rect.SetAsLastSibling();
            rect.SetSiblingIndex(frame.GetSiblingIndex());
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("DemonKingBackground 추가: 연결선 위, 프레임/마왕 아이콘 아래. 클릭 차단 없음. Prefab 변경은 Ctrl+Z 지원, 메인 씬은 저장하지 않음.");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    private static Sprite CreateDemonKingBackingSprite()
    {
        string sourcePath = DETAIL_ART + "Frame_DemonKing_Symmetric.png";
        string outputPath = DETAIL_ART + "Background_DemonKing_Black.png";
        var source = new Texture2D(2, 2);
        Texture2D output = null;
        try
        {
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(sourcePath)))
                throw new InvalidOperationException("프레임 PNG를 읽을 수 없습니다.");
            int width = source.width, height = source.height;
            var pixels = source.GetPixels32();
            var interior = new bool[pixels.Length];
            var queue = new Queue<int>();
            int center = (height / 2) * width + width / 2;
            if (pixels[center].a >= 128) throw new InvalidOperationException("프레임 중앙이 투명하지 않습니다.");
            interior[center] = true;
            queue.Enqueue(center);
            while (queue.Count > 0)
            {
                int p = queue.Dequeue(), x = p % width, y = p / width;
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    throw new InvalidOperationException("프레임 안쪽이 외부와 연결되어 배경을 안전하게 만들 수 없습니다.");
                foreach (int neighbor in new[] { p - 1, p + 1, p - width, p + width })
                    if (!interior[neighbor] && pixels[neighbor].a < 128)
                    {
                        interior[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
            }
            var mask = new Color32[pixels.Length];
            var black = new Color32(0, 0, 0, 255);
            for (int y = 0; y < height; y++)
            {
                int left = width, right = -1;
                for (int x = 0; x < width; x++)
                    if (pixels[y * width + x].a >= 128)
                    { left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
                // 바깥 레일의 좌우 경계 사이를 채워 이중 테두리 틈도 가린다.
                // 중앙의 투명 구역만 채우면 레일/장식 아래의 투명 틈으로 선이 비친다.
                for (int x = left; x <= right; x++)
                {
                    mask[y * width + x] = black;
                }
            }
            output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            output.SetPixels32(mask);
            output.Apply();
            File.WriteAllBytes(outputPath, output.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(source);
            if (output != null) Object.DestroyImmediate(output);
        }
        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("검은 배경 Import 실패");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteBorder = Vector4.zero;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
    }

    [MenuItem("Tools/OZGL2/Lobby/Apply Trait Detail Art And Effect Comparison")]
    public static void ApplyDetailArtAndComparison()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if ((stage == null && scene.path != "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity") ||
            (stage != null && (stage.assetPath != PREFAB || stage.scene.isDirty)))
            throw new InvalidOperationException("대상 씬 또는 저장된 대상 Prefab에서 실행하세요.");

        string[] names = { "Button_Footer_310x75", "Button_Dialog_244x72", "Button_Back_268x85",
            "Button_Step_142x72", "Divider_CurrentNext", "Frame_DemonKing_Symmetric" };
        foreach (string name in names)
            if (!File.Exists(DETAIL_ART + name + ".png")) throw new InvalidOperationException("새 아트 누락: " + name);
        // 새 생성 이미지의 Import 설정만 바꾼다. 공용 원본/외부 에셋에는 적용하지 않는다.
        foreach (string name in names)
        {
            string path = DETAIL_ART + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("이미지 Import 실패: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteBorder = Vector4.zero;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
        _material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);
        if (_font == null || _material == null) throw new InvalidOperationException("공유 폰트/머티리얼 누락");
        stage = PrefabStageUtility.OpenPrefab(PREFAB);
        var view = stage.prefabContentsRoot.GetComponentInChildren<UITraitOverlayView>(true);
        if (view == null) throw new InvalidOperationException("특성 화면 없음");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Trait comparison and proportion-specific art");
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Trait comparison and sprites");
        try
        {
            Transform screen = view.transform;
            ApplyDetailSprite(screen.Find("ResetTraits"), names[0]);
            ApplyDetailSprite(screen.Find("Recenter"), names[0]);
            ApplyDetailSprite(screen.Find("ResetConfirmation/Panel/Cancel"), names[1]);
            ApplyDetailSprite(screen.Find("ResetConfirmation/Panel/Confirm"), names[1]);
            ApplyDetailSprite(screen.Find("Back"), names[2]);
            var detail = screen.Find("SelectedTraitDetail");
            ApplyDetailSprite(detail.Find("Downgrade"), names[3]);
            ApplyDetailSprite(detail.Find("Upgrade"), names[3]);
            ApplyDetailSprite(screen.Find("TraitTreeViewport/TraitTreeContent/Nodes/DemonKingFrame"), names[5]);

            SetDetailRect(detail.Find("Type"), 0, 303, 346, 32, 23);
            SetDetailRect(detail.Find("Name"), 0, 263, 354, 48, 32);
            SetDetailRect(detail.Find("Level"), 0, 222, 346, 32, 23);
            SetDetailRect(detail.Find("Icon"), 0, 165, 66, 66);
            SetDetailRect(detail.Find("Description"), 0, 67, 346, 116, 22);
            var current = detail.Find("Description").GetComponent<TMP_Text>();
            ConfigureEffectLabel(current, _textColor);
            var nextTransform = detail.Find("NextDescription");
            var next = nextTransform != null ? nextTransform.GetComponent<TMP_Text>() :
                Label("NextDescription", detail, "", new Vector2(0, -69), new Vector2(346, 116), 22);
            SetDetailRect(next.transform, 0, -69, 346, 116, 22);
            ConfigureEffectLabel(next, new Color32(155, 150, 146, 255));
            Assign(view, "_detailNextDescription", next);
            ApplyDetailSprite(detail.Find("Divider"), names[4]);
            SetDetailRect(detail.Find("Divider"), 0, 0, 320, 20);
            detail.Find("Divider").GetComponent<Image>().color = Color.white;
            SetDetailRect(detail.Find("Status"), 0, -182, 346, 74, 20);
            SetDetailRect(detail.Find("Refund"), -87, -242, 160, 28, 20);
            SetDetailRect(detail.Find("Cost"), 87, -242, 160, 28, 20);
            SetDetailRect(detail.Find("Downgrade"), -87, -291, 142, 72);
            SetDetailRect(detail.Find("Upgrade"), 87, -291, 142, 72);
            LayoutDetailHeader(detail);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("TraitsScreen만 수정: 현재/다음 효과와 구분선, 버튼 7개 비율별 Sprite, 대칭 마왕 프레임. 마왕 아이콘·메뉴·설정·메인 씬은 유지. Prefab 저장 전 Ctrl+Z 지원.");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    private static void ApplyDetailSprite(Transform target, string name)
    {
        if (target == null || !target.TryGetComponent<Image>(out var image))
            throw new InvalidOperationException("대상 Image 누락: " + name);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DETAIL_ART + name + ".png");
        if (sprite == null) throw new InvalidOperationException("Sprite 누락: " + name);
        Undo.RecordObject(image, "Assign proportion-specific sprite");
        image.sprite = sprite;
        image.type = UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect = true;
        image.pixelsPerUnitMultiplier = 1;
    }

    private static void SetDetailRect(Transform target, float x, float y, float w, float h, float fontSize = 0)
    {
        var rect = target as RectTransform;
        if (rect == null) throw new InvalidOperationException("설명창 Rect 누락");
        Undo.RecordObject(rect, "Arrange trait detail");
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        if (fontSize > 0 && target.TryGetComponent<TMP_Text>(out var label))
        {
            Undo.RecordObject(label, "Trait detail type size");
            label.fontSize = fontSize;
        }
    }

    private static void ConfigureEffectLabel(TMP_Text label, Color color)
    {
        Undo.RecordObject(label, "Current/next trait effect text");
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = color;
        label.text = "";
    }

    [Serializable] private sealed class ArtMap { public ArtRecord[] records; }
    [Serializable] private sealed class ArtRecord { public string label; public string kind; public string file; }

    [MenuItem("Tools/OZGL2/Lobby/Build Interactive Trait Tree")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (currentStage != null && currentStage.scene.isDirty)
            throw new InvalidOperationException("열린 Prefab의 변경사항을 먼저 저장하세요.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (currentStage == null && (scene.isDirty || scene.path != "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity"))
            throw new InvalidOperationException("저장된 UI_Lobby_MutedPreview 씬에서 실행하세요.");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
        _material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);
        if (_font == null || _material == null) throw new InvalidOperationException("공유 폰트/머티리얼을 먼저 복원하세요.");
        var defs = Resources.LoadAll<TraitData>("Traits").OrderBy(d => (int)d.id).ToArray();
        if (defs.Length != 40 || defs.Select(d => d.id).Distinct().Count() != 40)
            throw new InvalidOperationException("중복 없는 특성 데이터 40개가 필요합니다.");
        string[] definitionPaths = defs.Select(AssetDatabase.GetAssetPath).ToArray();
        var map = JsonUtility.FromJson<ArtMap>(File.ReadAllText(ART + "AssetMap.json"));
        var icons = new Dictionary<TraitId, Sprite>();
        foreach (var def in defs)
        {
            var record = map.records.Single(r => r.label == def.displayName && (r.kind == "Normal" || r.kind == "Specialized"));
            icons.Add(def.id, LoadSprite(record.file));
        }

        var stage = PrefabStageUtility.OpenPrefab(PREFAB);
        // Prefab Stage 전환 중 미사용 Resources가 언로드될 수 있어 에셋 핸들을 다시 얻는다.
        defs = definitionPaths.Select(path => AssetDatabase.LoadAssetAtPath<TraitData>(path)).ToArray();
        var view = stage.prefabContentsRoot.GetComponentInChildren<UITraitOverlayView>(true);
        if (view == null) throw new InvalidOperationException("특성 화면이 없습니다.");
        if (view.GetComponent<UITraitProgressionController>() != null)
            throw new InvalidOperationException("이미 구현된 트리입니다. 기존 배치를 덮어쓰지 않습니다.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build interactive trait tree");
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Configure trait screen");
        try
        {
            Transform screen = view.transform;
            var viewport = (RectTransform)screen.Find("TraitTreeViewport");
            viewport.sizeDelta = new Vector2(1720, 630);
            viewport.anchoredPosition = new Vector2(0, -105);
            var content = (RectTransform)viewport.Find("TraitTreeContent");
            for (int i = content.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(content.GetChild(i).gameObject);
            content.sizeDelta = new Vector2(2600, 2600);
            content.anchoredPosition = Vector2.zero;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.horizontal = scroll.vertical = true;
            scroll.velocity = Vector2.zero;
            ConfigureZoom(scroll);
            AddDismiss(viewport.gameObject, view);
            AddDismiss(screen.Find("Background").gameObject, view);
            var shade = screen.Find("BackgroundShade").GetComponent<Image>();
            shade.color = new Color(0.015f, 0.015f, 0.02f, 0.67f);
            var circle = Image("MagicCircle", content, LoadSprite("Sprites/Decor/MagicCircle_Garnet.png"), Vector2.zero, new Vector2(760, 760));
            circle.color = new Color(1, 1, 1, 0.16f);
            RectTransform lines = Rect("Connections", content, Vector2.zero, Vector2.zero);
            RectTransform nodes = Rect("Nodes", content, Vector2.zero, Vector2.zero);
            Image("DemonKingFrame", nodes, LoadSprite("Sprites/Frames/Frame_Central_DemonKing.png"), Vector2.zero, new Vector2(210, 210));
            FitIcon(Image("DemonKingIcon", nodes, LoadSprite("Sprites/Icons/Core/Icon_DemonKing.png"), Vector2.zero, new Vector2(210, 210)), 210 * 0.31f);
            var positions = new Dictionary<TraitId, Vector2>();
            var nodeViews = new List<UITraitFrameView>();
            foreach (TraitBranch branch in Enum.GetValues(typeof(TraitBranch)))
            {
                string artGroup = Group(branch);
                Vector2 headerPosition = Direction(branch) * 235;
                var header = Rect("Header_" + artGroup, nodes, headerPosition, new Vector2(162, 162));
                Image("Icon", header, LoadSprite("Sprites/Icons/Core/Icon_Header_" + artGroup + ".png"), Vector2.zero, new Vector2(162, 162));
                Caption("Name", header, BranchName(branch), new Vector2(0, -92), new Vector2(210, 36), 27);
                Connect(lines, Vector2.zero, headerPosition, BranchColor(branch));
                foreach (var def in defs.Where(d => d.branch == branch))
                {
                    Vector2 position = Position(def);
                    positions.Add(def.id, position);
                    nodeViews.Add(CreateNode(nodes, def, position, artGroup, icons[def.id]));
                }
                foreach (var def in defs.Where(d => d.branch == branch))
                {
                    if (def.parents == null || def.parents.Length == 0)
                        Connect(lines, headerPosition, positions[def.id], BranchColor(branch));
                    else foreach (var parent in def.parents)
                        Connect(lines, positions[parent], positions[def.id], BranchColor(branch));
                }
            }
            BuildDetail(view);
            var controller = Undo.AddComponent<UITraitProgressionController>(view.gameObject);
            Assign(controller, "_view", view);
            Assign(controller, "_accountLevel", screen.Find("AccountStatus/Level").GetComponent<TMP_Text>());
            var serialized = new SerializedObject(controller);
            var array = serialized.FindProperty("_nodes");
            array.arraySize = nodeViews.Count;
            for (int i = 0; i < nodeViews.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = nodeViews[i];
            serialized.ApplyModifiedProperties();
            screen.Find("Points").GetComponent<TMP_Text>().text = "보유 포인트  -";
            screen.Find("PanHint").GetComponent<TMP_Text>().text = "드래그로 이동 / 휠로 확대·축소 / 특성 클릭으로 상세 보기";
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("특성 40개, 계열 상징 4개, 중앙 마왕, 설명/레벨 버튼을 구성했습니다. Prefab Mode에서 확인 후 저장하세요. Ctrl+Z 지원.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }

    private static UITraitFrameView CreateNode(Transform parent, TraitData def, Vector2 position, string group, Sprite icon)
    {
        bool specialized = def.line == TraitLine.Capstone;
        float size = specialized ? 186 : 154;
        RectTransform node = Rect("Trait_" + def.id, parent, position, new Vector2(size, size));
        Image frame = Image("Frame", node, LoadSprite("Sprites/Frames/Frame_" + (specialized ? "Specialized_" : "Normal_") + group + ".png"), Vector2.zero, new Vector2(size, size));
        // 클릭 영역은 다이아몬드 안쪽만이 아니라 노드 사각형 전체로 확보한다.
        var hit = node.gameObject.AddComponent<Image>();
        hit.color = new Color(0, 0, 0, 0);
        hit.raycastTarget = false;
        var hitArea = Image("HitArea", node, null, new Vector2(0, -23), new Vector2(204, size + 54));
        hitArea.color = new Color(0, 0, 0, 0);
        hitArea.raycastTarget = true;
        hitArea.transform.SetAsFirstSibling();
        var button = node.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        StyleButton(button);
        Image iconImage = Image("Icon", node, icon, Vector2.zero, new Vector2(size, size));
        float labelY = -size * 0.5f - 32;
        TMP_Text name = Caption("Name", node, def.displayName, new Vector2(0, labelY), new Vector2(204, 32), 25);
        Image selected = Image("Selection", node, null, new Vector2(0, size * 0.5f + 13), new Vector2(10, 10));
        selected.color = _textColor;
        selected.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        selected.gameObject.SetActive(false);
        var view = node.gameObject.AddComponent<UITraitFrameView>();
        Assign(view, "_definition", def);
        Assign(view, "_frame", frame);
        Assign(view, "_icon", iconImage);
        Assign(view, "_nameLabel", name);
        Assign(view, "_selectionIndicator", selected.gameObject);
        PolishNode(view);
        if (view.Definition == null) throw new InvalidOperationException("특성 데이터 연결 실패: " + def.id);
        return view;
    }

    [MenuItem("Tools/OZGL2/Lobby/Polish Trait Nodes And Wheel Zoom")]
    public static void PolishExistingTree()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty || (currentStage == null && scene.path != "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity"))
            throw new InvalidOperationException("저장된 대상 씬 또는 Prefab에서 실행하세요.");
        if (currentStage != null && currentStage.assetPath != PREFAB)
            throw new InvalidOperationException("다른 Prefab이 열려 있습니다.");
        var stage = PrefabStageUtility.OpenPrefab(PREFAB);
        var view = stage.prefabContentsRoot.GetComponentInChildren<UITraitOverlayView>(true);
        if (view == null || view.GetComponent<UITraitProgressionController>() == null)
            throw new InvalidOperationException("먼저 특성 트리를 구현하세요.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Polish trait nodes and wheel zoom");
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Polish trait screen");
        try
        {
            foreach (var node in view.GetComponentsInChildren<UITraitFrameView>(true)) PolishNode(node);
            var scroll = view.GetComponentInChildren<ScrollRect>(true);
            ConfigureZoom(scroll);
            var central = scroll.content.Find("Nodes/DemonKingIcon").GetComponent<Image>();
            FitIcon(central, 210 * 0.31f);
            view.transform.Find("PanHint").GetComponent<TMP_Text>().text = "드래그로 이동 / 휠로 확대·축소 / 특성 클릭으로 상세 보기";
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("특성 40개: 레벨 문구 제거, 최대 레벨 테두리, 아이콘 정렬, 휠 줌 적용. Prefab Mode에서 확인 후 저장하세요. Ctrl+Z 지원.");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    private static void ConfigureZoom(ScrollRect scroll)
    {
        Undo.RecordObject(scroll, "Disable wheel panning");
        scroll.scrollSensitivity = 0;
        var zoom = scroll.GetComponent<UITraitTreeZoom>();
        if (zoom == null) zoom = Undo.AddComponent<UITraitTreeZoom>(scroll.gameObject);
        Assign(zoom, "_scroll", scroll);
        var serialized = new SerializedObject(zoom);
        serialized.FindProperty("_minZoom").floatValue = 0.25f;
        serialized.ApplyModifiedProperties();
    }

    private static void PolishNode(UITraitFrameView node)
    {
        var rect = (RectTransform)node.transform;
        float size = rect.sizeDelta.x;
        foreach (string oldName in new[] { "Rank", "RankShade" })
        {
            var old = rect.Find(oldName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
        foreach (string name in new[] { "Name", "NameShade" })
        {
            var label = (RectTransform)rect.Find(name);
            Undo.RecordObject(label, "Make room for max level border");
            label.anchoredPosition = new Vector2(0, -size * 0.5f - 32);
        }
        var hit = (RectTransform)rect.Find("HitArea");
        Undo.RecordObject(hit, "Fit node hit area");
        hit.anchoredPosition = new Vector2(0, -23);
        hit.sizeDelta = new Vector2(204, size + 54);
        var border = rect.Find("MaxLevelBorder");
        if (border != null) Undo.DestroyObjectImmediate(border.gameObject);
        AssignFrameSprites(node);
        FitIcon(rect.Find("Icon").GetComponent<Image>(), size * (node.IsSpecialized ? 0.27f : 0.30f),
            node.Definition != null && node.Definition.id == TraitId.Mon_B2);
    }

    // 프레임 이미지 교체만 수행한다. 레이아웃/아이콘/기존 레벨 데이터에는 손대지 않는다.
    private static void AssignFrameSprites(UITraitFrameView node)
    {
        if (node.Definition == null) throw new InvalidOperationException("특성 데이터 누락: " + node.name);
        string key = (node.IsSpecialized ? "Specialized_" : "Normal_") + Group(node.Definition.branch);
        Sprite original = LoadSprite("Sprites/Frames/Frame_" + key + ".png");
        Sprite maxLevel = LoadSprite("MaxLevel/Frame_" + key + "_Max.png");
        Assign(node, "_defaultFrameSprite", original);
        Assign(node, "_maxLevelFrameSprite", maxLevel);
        var frame = node.transform.Find("Frame").GetComponent<Image>();
        Undo.RecordObject(frame, "Set default trait frame");
        frame.sprite = original;
    }

    [MenuItem("Tools/OZGL2/Lobby/Apply Trait Reset And Name States")]
    public static void ApplyResetAndNameStates()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if ((stage == null && scene.path != "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity") ||
            (stage != null && (stage.assetPath != PREFAB || stage.scene.isDirty)))
            throw new InvalidOperationException("대상 씬 또는 저장된 대상 Prefab에서 실행하세요.");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
        _material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);
        if (_font == null || _material == null) throw new InvalidOperationException("공유 폰트/머티리얼 누락");
        stage = PrefabStageUtility.OpenPrefab(PREFAB);
        var view = stage.prefabContentsRoot.GetComponentInChildren<UITraitOverlayView>(true);
        if (view == null) throw new InvalidOperationException("특성 화면 없음");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Trait reset controls and name states");
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Trait reset controls");
        try
        {
            foreach (var node in view.GetComponentsInChildren<UITraitFrameView>(true))
            {
                var serialized = new SerializedObject(node);
                serialized.FindProperty("_defaultNameColor").colorValue = _textColor;
                serialized.FindProperty("_lockedNameColor").colorValue = new Color32(155, 150, 146, 255);
                Color maxColor;
                switch (node.Definition.branch)
                {
                    case TraitBranch.Monster: maxColor = new Color32(158, 195, 144, 255); break;
                    case TraitBranch.Hero: maxColor = new Color32(193, 150, 210, 255); break;
                    case TraitBranch.Skill: maxColor = new Color32(126, 179, 214, 255); break;
                    default: maxColor = new Color32(211, 178, 111, 255); break;
                }
                serialized.FindProperty("_maxLevelNameColor").colorValue = maxColor;
                serialized.ApplyModifiedProperties();
                // Prefab의 초기 모습은 저장 데이터를 사용하지 않는 0레벨 상태다.
                var label = node.transform.Find("Name").GetComponent<TMP_Text>();
                Undo.RecordObject(label, "Trait initial label color");
                label.color = node.Definition.parents == null || node.Definition.parents.Length == 0
                    ? _textColor : new Color32(155, 150, 146, 255);
            }
            var center = (RectTransform)view.transform.Find("Recenter");
            Undo.RecordObject(center, "Align footer button pair");
            center.anchoredPosition = new Vector2(167, -470);
            center.sizeDelta = new Vector2(310, 75);
            var reset = view.transform.Find("ResetTraits");
            if (reset == null)
            {
                var clone = Object.Instantiate(center.gameObject, view.transform);
                clone.name = "ResetTraits";
                Undo.RegisterCreatedObjectUndo(clone, "Create trait reset button");
                reset = clone.transform;
            }
            var resetRect = (RectTransform)reset;
            Undo.RecordObject(resetRect, "Align reset button");
            resetRect.anchoredPosition = new Vector2(-167, -470);
            resetRect.sizeDelta = center.sizeDelta;
            var resetButton = reset.GetComponent<Button>();
            Undo.RecordObject(resetButton, "Wire trait reset at runtime");
            resetButton.onClick = new Button.ButtonClickedEvent();
            resetButton.interactable = false;
            StyleButton(resetButton);
            var resetLabel = reset.GetComponentInChildren<TMP_Text>(true);
            Undo.RecordObject(resetLabel, "Trait reset caption");
            resetLabel.text = "특성 초기화";
            resetLabel.color = new Color32(101, 98, 98, 255);
            Assign(view, "_resetButton", resetButton);
            Assign(view, "_resetLabel", resetLabel);
            BuildResetConfirmation(view);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("특성명 40개 상태 색상, 하단 버튼 2개 중앙 정렬, LP 환급 확인창 적용. Prefab만 확인 후 저장하세요. Ctrl+Z 지원.");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    private static void BuildResetConfirmation(UITraitOverlayView view)
    {
        var previous = view.transform.Find("ResetConfirmation");
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
        var overlay = Image("ResetConfirmation", view.transform, null, Vector2.zero, Vector2.zero);
        overlay.rectTransform.anchorMin = Vector2.zero;
        overlay.rectTransform.anchorMax = Vector2.one;
        overlay.color = new Color(0, 0, 0, 0.76f);
        overlay.raycastTarget = true;
        var panel = Image("Panel", overlay.transform,
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/06.UI/LobbyMutedPreview/Overlays/Sprites/Panel_Frame.png"),
            Vector2.zero, new Vector2(680, 370));
        panel.preserveAspect = false;
        panel.type = UnityEngine.UI.Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 8;
        panel.raycastTarget = true;
        Label("Title", panel.transform, "특성 초기화", new Vector2(0, 118), new Vector2(570, 50), 34);
        var message = Label("Message", panel.transform, "모든 특성을 초기화할까요?\n투자한 0 LP를 돌려받습니다.\n마왕 레벨과 경험치는 유지됩니다.",
            new Vector2(0, 20), new Vector2(570, 114), 25);
        message.textWrappingMode = TextWrappingModes.Normal;
        var cancel = CreateResetDialogButton(panel.transform, "Cancel", "취소", -143);
        var confirm = CreateResetDialogButton(panel.transform, "Confirm", "초기화", 143);
        Assign(view, "_resetConfirmation", overlay.gameObject);
        Assign(view, "_resetMessage", message);
        Assign(view, "_cancelResetButton", cancel);
        Assign(view, "_confirmResetButton", confirm);
        overlay.gameObject.SetActive(false);
    }

    private static Button CreateResetDialogButton(Transform parent, string name, string text, float x)
    {
        var image = Image(name, parent,
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/06.UI/LobbyMutedPreview/Overlays/Sprites/Button_Frame.png"),
            new Vector2(x, -115), new Vector2(244, 72));
        image.preserveAspect = false;
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 8;
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        StyleButton(button);
        Label("Label", image.transform, text, Vector2.zero, new Vector2(204, 58), 30);
        return button;
    }

    [MenuItem("Tools/OZGL2/Lobby/Apply Max Level Trait Frame Sprites")]
    public static void ApplyMaxLevelFrames()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        // Prefab Mode는 메인 씬의 미저장 변경을 보존한다. 메인 씬을 저장하거나 되돌리지 않는다.
        if ((stage == null && scene.path != "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity") ||
            (stage != null && (stage.assetPath != PREFAB || stage.scene.isDirty)))
            throw new InvalidOperationException("대상 씬 또는 저장된 대상 Prefab에서 실행하세요.");
        // 누락된 리소스가 있으면 Prefab을 수정하기 전에 중단한다.
        foreach (string kind in new[] { "Normal", "Specialized" })
            foreach (string branch in new[] { "Legion", "Curse", "Spells", "Wisdom" })
                LoadSprite("MaxLevel/Frame_" + kind + "_" + branch + "_Max.png");
        stage = PrefabStageUtility.OpenPrefab(PREFAB);
        var view = stage.prefabContentsRoot.GetComponentInChildren<UITraitOverlayView>(true);
        if (view == null) throw new InvalidOperationException("특성 화면 없음");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Replace max level trait frame sprites");
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Max level frame sprites");
        try
        {
            var nodes = view.GetComponentsInChildren<UITraitFrameView>(true);
            if (nodes.Length != 40) throw new InvalidOperationException("특성 노드 40개 필요");
            foreach (var node in nodes)
            {
                AssignFrameSprites(node);
                var oldBorder = node.transform.Find("MaxLevelBorder");
                if (oldBorder != null) Undo.DestroyObjectImmediate(oldBorder.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("특성 40개에 기본/만렙 Sprite 연결, 기존 얇은 만렙 외곽선 제거. Prefab Mode에서 확인 후 저장하세요. Ctrl+Z 지원.");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    // PNG는 변경하지 않고, 실제 알파 영역을 읽어 다이아몬드 안쪽에 맞는 Rect만 계산한다.
    private static void FitIcon(Image icon, float diamondRadius, bool useOpticalCenter = false)
    {
        var sprite = icon.sprite;
        if (sprite == null) throw new InvalidOperationException("아이콘이 없습니다.");
        var texture = new Texture2D(2, 2);
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))))
                throw new InvalidOperationException("아이콘 픽셀을 읽지 못했습니다.");
            var pixels = texture.GetPixels32();
            Rect source = sprite.rect;
            int minX = (int)source.xMax, minY = (int)source.yMax, maxX = -1, maxY = -1;
            for (int y = (int)source.yMin; y < source.yMax; y++)
                for (int x = (int)source.xMin; x < source.xMax; x++)
                    if (pixels[y * texture.width + x].a > 24)
                    { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            if (maxX < minX) throw new InvalidOperationException("비어 있는 아이콘: " + sprite.name);
            Vector2 center = new Vector2((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f);
            if (useOpticalCenter)
            {
                // 하트처럼 윗부분 면적이 큰 모양은 사각형 중앙과 눈에 보이는 중앙이 다르다.
                // 어두운 외곽선은 제외한 밝은 실루엣의 세로 무게중심만 보정한다.
                float weightedY = 0, weight = 0;
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        Color32 pixel = pixels[y * texture.width + x];
                        if (pixel.a <= 24 || pixel.r + pixel.g + pixel.b <= 240) continue;
                        weightedY += (y + 0.5f) * pixel.a;
                        weight += pixel.a;
                    }
                if (weight > 0) center.y = weightedY / weight;
            }
            float extent = 1;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (pixels[y * texture.width + x].a > 24)
                        extent = Mathf.Max(extent, Mathf.Abs(x + 0.5f - center.x) + Mathf.Abs(y + 0.5f - center.y) + 1);
            float scale = diamondRadius / extent;
            Undo.RecordObject(icon.rectTransform, "Center icon within diamond");
            icon.rectTransform.sizeDelta = source.size * scale;
            icon.rectTransform.anchoredPosition = -(center - source.center) * scale;
            icon.preserveAspect = true;
        }
        finally { Object.DestroyImmediate(texture); }
    }

    private static void BuildDetail(UITraitOverlayView view)
    {
        var detail = (RectTransform)view.transform.Find("SelectedTraitDetail");
        for (int i = detail.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(detail.GetChild(i).gameObject);
        detail.sizeDelta = new Vector2(428, 700);
        detail.anchoredPosition = new Vector2(698, -64);
        Assign(view, "_detailType", Label("Type", detail, "일반 특성", new Vector2(0, 279), new Vector2(346, 40), 24));
        Assign(view, "_detailTitle", Label("Name", detail, "특성 이름", new Vector2(0, 226), new Vector2(354, 52), 34));
        Assign(view, "_detailLevel", Label("Level", detail, "현재 레벨  0 / 3", new Vector2(0, 180), new Vector2(346, 40), 24));
        var divider = Image("Divider", detail, null, new Vector2(0, 149), new Vector2(266, 2));
        divider.color = new Color32(125, 57, 63, 255);
        Assign(view, "_detailIcon", Image("Icon", detail, null, new Vector2(0, 87), new Vector2(102, 102)));
        TMP_Text desc = Label("Description", detail, "", new Vector2(0, -22), new Vector2(324, 126), 25);
        desc.alignment = TextAlignmentOptions.TopLeft;
        desc.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "_detailDescription", desc);
        TMP_Text status = Label("Status", detail, "", new Vector2(0, -149), new Vector2(334, 84), 22);
        status.textWrappingMode = TextWrappingModes.Normal;
        status.color = new Color32(186, 166, 157, 255);
        Assign(view, "_detailStatus", status);
        Assign(view, "_downgradeRefund", Label("Refund", detail, "최소 레벨", new Vector2(-87, -219), new Vector2(160, 30), 20));
        Assign(view, "_upgradeCost", Label("Cost", detail, "필요 1 LP", new Vector2(87, -219), new Vector2(160, 30), 20));
        CreateAction(view, detail, "Downgrade", "-", -87, "_downgradeButton", "_downgradeLabel");
        CreateAction(view, detail, "Upgrade", "+", 87, "_upgradeButton", "_upgradeLabel");
        detail.gameObject.SetActive(false);
    }

    private static void CreateAction(UITraitOverlayView view, Transform parent, string name, string text, float x, string buttonField, string labelField)
    {
        var root = Rect(name, parent, new Vector2(x, -274), new Vector2(142, 72));
        var image = root.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/06.UI/LobbyMutedPreview/Overlays/Sprites/Button_Frame.png");
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 8;
        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        StyleButton(button);
        Assign(view, buttonField, button);
        Assign(view, labelField, Label("Label", root, text, Vector2.zero, new Vector2(110, 64), 42));
    }

    private static void AddDismiss(GameObject target, UITraitOverlayView view)
    {
        var surface = Undo.AddComponent<UITraitDismissSurface>(target);
        Assign(surface, "_view", view);
    }

    private static Vector2 Direction(TraitBranch branch)
    {
        switch (branch)
        {
            case TraitBranch.Monster: return Vector2.up;
            case TraitBranch.Hero: return Vector2.right;
            case TraitBranch.Skill: return Vector2.down;
            default: return Vector2.left;
        }
    }

    private static Vector2 Position(TraitData def)
    {
        Vector2 direction = Direction(def.branch);
        if (def.line == TraitLine.Capstone) return direction * 1090;
        int tier = (int)def.id % 10 % 3;
        float distance = 445 + tier * 210;
        float lane = ((int)def.line - 1) * 210;
        return direction * distance + (def.branch == TraitBranch.Monster || def.branch == TraitBranch.Skill
            ? Vector2.right * lane : Vector2.down * lane);
    }

    private static string Group(TraitBranch branch)
    {
        switch (branch)
        {
            case TraitBranch.Monster: return "Legion";
            case TraitBranch.Hero: return "Curse";
            case TraitBranch.Skill: return "Spells";
            default: return "Wisdom";
        }
    }

    private static string BranchName(TraitBranch branch)
    {
        switch (branch)
        {
            case TraitBranch.Monster: return "마왕군 조련";
            case TraitBranch.Hero: return "인간계 저주";
            case TraitBranch.Skill: return "주문 연구";
            default: return "지배의 지혜";
        }
    }

    private static Color BranchColor(TraitBranch branch)
    {
        switch (branch)
        {
            case TraitBranch.Monster: return new Color32(88, 133, 89, 255);
            case TraitBranch.Hero: return new Color32(153, 89, 163, 255);
            case TraitBranch.Skill: return new Color32(71, 131, 166, 255);
            default: return new Color32(163, 125, 63, 255);
        }
    }

    private static void Connect(Transform parent, Vector2 from, Vector2 to, Color color)
    {
        Vector2 delta = to - from;
        var line = Image("Link", parent, null, (from + to) * 0.5f, new Vector2(delta.magnitude, 4));
        line.color = color;
        line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        var bead = Image("Joint", parent, null, (from + to) * 0.5f, new Vector2(9, 9));
        bead.color = color;
        bead.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
    }

    private static TMP_Text Caption(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var shade = Image(name + "Shade", parent, null, position, size);
        shade.color = new Color(0.025f, 0.024f, 0.029f, 0.86f);
        return Label(name, parent, text, position, size, fontSize);
    }

    private static TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.fontSharedMaterial = _material;
        label.text = text;
        label.fontSize = fontSize;
        label.color = _textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Undo.RegisterCreatedObjectUndo(go, "Create trait UI");
        return rect;
    }

    private static Image Image(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite LoadSprite(string relativePath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ART + relativePath);
        if (sprite == null) throw new InvalidOperationException("트리 아트 누락: " + relativePath);
        return sprite;
    }

    private static void StyleButton(Button button)
    {
        var colors = button.colors;
        colors.highlightedColor = new Color(1, 0.90f, 0.78f);
        colors.pressedColor = new Color(0.65f, 0.55f, 0.50f);
        colors.disabledColor = new Color(0.24f, 0.23f, 0.23f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }

    private static void Assign(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }
}
