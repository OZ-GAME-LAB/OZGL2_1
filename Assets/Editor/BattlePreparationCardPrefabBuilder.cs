using System;
using System.Collections.Generic;
using System.IO;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 실제 Scene과 기존 Prefab을 건드리지 않고 독립 카드 Prefab만 새로 생성한다.
public static class BattlePreparationCardPrefabBuilder
{
    private const string ROOT = "Assets/06.UI/BattleMutedPreview/Cards_v1";
    private const string SPRITES = ROOT + "/Sprites/";
    private const string PREFABS = ROOT + "/Prefabs";
    private const string FONT_PATH = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/NotoSansCJKkr-Regular.otf";
    private const string DIAMOND_PATH = "Assets/06.UI/BattleMutedPreview/Reference_v2/Frame_DiamondSynergy.png";
    private const string SHIELD_PATH = "Assets/06.UI/BattleMutedPreview/Sprites/Icon_BoneShield.png";
    private const string HEART_PATH = "Assets/06.UI/LobbyMutedPreview/Overlays/TreeArt_v1/Sprites/Icons/Legion/Icon_Legion_UndyingFlesh.png";
    private static readonly Color IVORY = new Color32(246, 240, 226, 255);
    private static readonly Color UNIT_COLOR = new Color32(159, 61, 62, 255);
    private static readonly Color LAND_COLOR = new Color32(190, 153, 78, 255);
    private static readonly Color RELIC_COLOR = new Color32(71, 112, 155, 255);
    private static readonly string[] ART_NAMES =
    {
        "CardShell_Unit", "CardShell_LandSlot", "CardShell_Relic", "Badge_Star",
        "Icon_Sword", "Artwork_ShadowSwordsman", "Artwork_ManaAmplifier",
        "Icon_Type_Unit_Diamond_v2", "Icon_Type_Land_Diamond_v2", "Icon_Type_Relic_Diamond_v2"
    };
    private static readonly string[] CARD_NAMES = { "BattleCard_Unit", "BattleCard_LandSlot", "BattleCard_Relic" };

    [MenuItem("Tools/OZGL2/Battle/Create Card Prefabs")]
    public static void CreateCardPrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("컴파일과 임포트가 끝난 Edit Mode에서 실행해 주세요.");

        foreach (string name in CARD_NAMES)
        {
            string path = PREFABS + "/" + name + ".prefab";
            if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("기존 카드 Prefab은 덮어쓰지 않습니다: " + path);
        }
        foreach (string name in ART_NAMES)
            if (!File.Exists(SPRITES + name + ".png"))
                throw new FileNotFoundException("필수 카드 아트를 먼저 준비해 주세요.", SPRITES + name + ".png");

        Font font = RequireAsset<Font>(FONT_PATH);
        // 재사용 에셋은 읽기만 하며 원본 임포터를 수정하지 않는다.
        Sprite diamond = RequireAsset<Sprite>(DIAMOND_PATH);
        Sprite shield = RequireAsset<Sprite>(SHIELD_PATH);
        Sprite heart = RequireAsset<Sprite>(HEART_PATH);
        Scene activeScene = SceneManager.GetActiveScene();
        List<SceneSnapshot> originalScenes = CaptureScenes();
        int originalSceneCount = SceneManager.sceneCount;
        var importerBackups = new Dictionary<string, string>();
        var attemptedPrefabPaths = new List<string>();
        Scene preview = default;
        try
        {
            var sprites = new Dictionary<string, Sprite>();
            foreach (string name in ART_NAMES)
            {
                string path = SPRITES + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("PNG TextureImporter가 필요합니다: " + path);
                importerBackups.Add(path, EditorJsonUtility.ToJson(importer));
                sprites.Add(name, ImportCardSprite(importer, path));
            }

            preview = EditorSceneManager.NewPreviewScene();
            if (!preview.IsValid()) throw new InvalidOperationException("격리된 PreviewScene 생성에 실패했습니다.");
            var context = new BuildContext(preview, font, sprites, diamond, shield, heart);
            GameObject[] cards = { BuildUnit(context), BuildLand(context), BuildRelic(context) };
            EnsureFolder(ROOT);
            EnsureFolder(PREFABS);

            for (int index = 0; index < cards.Length; index++)
            {
                string path = PREFABS + "/" + CARD_NAMES[index] + ".prefab";
                // 앞선 임포트 중 에셋이 추가된 경우에도 기존 Prefab을 덮어쓰지 않는다.
                if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException("생성 중 같은 경로의 Prefab이 발견되어 중단했습니다: " + path);
                attemptedPrefabPaths.Add(path);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(cards[index], path, out bool success);
                if (!success || saved == null) throw new IOException("카드 Prefab 저장 실패: " + path);
            }
        }
        catch
        {
            // 이번 호출에서 새로 저장을 시도한 경로만 정리한다. 기존 에셋은 사전 검사에서 제외된다.
            foreach (string path in attemptedPrefabPaths)
                if (File.Exists(path) && !AssetDatabase.DeleteAsset(path))
                    Debug.LogError("실패한 신규 카드 Prefab을 정리하지 못했습니다: " + path);
            foreach (KeyValuePair<string, string> backup in importerBackups)
            {
                try
                {
                    var importer = AssetImporter.GetAtPath(backup.Key) as TextureImporter;
                    if (importer == null) continue;
                    EditorJsonUtility.FromJsonOverwrite(backup.Value, importer);
                    importer.SaveAndReimport();
                }
                catch (Exception restoreError)
                {
                    Debug.LogError("카드 Sprite 임포터 복원 실패: " + backup.Key + " / " + restoreError.Message);
                }
            }
            throw;
        }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            VerifyScenes(originalScenes, originalSceneCount, activeScene);
        }

        Debug.Log("독립 전투 준비 카드 Prefab 3종 생성 완료: " + PREFABS +
            "\n실제 Scene은 생성·변경·저장하지 않았습니다. 표시 내용은 예시이며 전투 데이터/선택 기능과 연결되지 않습니다." +
            "\nPrefab 파일·폴더 생성과 Sprite 임포트 설정은 Undo 대상이 아닙니다. 생성 파일은 Project 창에서 별도로 관리해 주세요.");
    }

    private static GameObject BuildUnit(BuildContext context)
    {
        RectTransform root = BeginCard(context, CARD_NAMES[0], "CardShell_Unit", "유닛", "그림자 검사", UNIT_COLOR,
            context.Sprites["Icon_Type_Unit_Diamond_v2"], out SerializedObject bindings);
        Image[] cells = BuildGrid(context, root, 72, 272, UNIT_COLOR);
        Image artwork = Picture(context, root, "Artwork", context.Sprites["Artwork_ShadowSwordsman"], 174, 309, 252, 300);
        Bind(bindings, "_artwork", artwork);
        Bind(bindings, "_traitTitleText", Label(context, root, "TraitTitleText", "특성", 64, 670, 118, 50, 30, true, TextAnchor.MiddleCenter));
        Bind(bindings, "_traitDescriptionText", Label(context, root, "TraitDescriptionText", "3성 달성 시\n30초마다 마법검 발사", 62, 716, 480, 96, 30));
        Bind(bindings, "_skillTitleText", Label(context, root, "SkillTitleText", "보유 스킬", 54, 827, 145, 48, 28, true, TextAnchor.MiddleCenter));
        Bind(bindings, "_skillDescriptionText", Label(context, root, "SkillDescriptionText", "스킬 없음", 62, 884, 480, 54, 32, false, TextAnchor.MiddleCenter));
        RectTransform stats = Group(context, root, "Stats", 24, 954, 552, 90);
        BuildStat(context, stats, "Attack", "공격력", "1000", context.Sprites["Icon_Sword"], 0, bindings, "_attackLabelText", "_attackValueText");
        BuildStat(context, stats, "Defense", "방어력", "1000", context.Shield, 184, bindings, "_defenseLabelText", "_defenseValueText");
        BuildStat(context, stats, "Health", "체력", "1000", context.Heart, 368, bindings, "_healthLabelText", "_healthValueText");
        FinishCard(bindings, cells, new[] { new Vector2Int(2, 1), new Vector2Int(2, 2), new Vector2Int(2, 3), new Vector2Int(3, 3) });
        return root.gameObject;
    }

    private static GameObject BuildLand(BuildContext context)
    {
        RectTransform root = BeginCard(context, CARD_NAMES[1], "CardShell_LandSlot", "땅 슬롯", "가시 지대", LAND_COLOR,
            context.Sprites["Icon_Type_Land_Diamond_v2"], out SerializedObject bindings);
        Image[] cells = BuildGrid(context, root, 72, 310, LAND_COLOR);
        Bind(bindings, "_areaTitleText", Label(context, root, "AreaTitleText", "추가 배치 영역", 60, 782, 480, 70, 42, true, TextAnchor.MiddleCenter));
        Bind(bindings, "_areaDescriptionText", Label(context, root, "AreaDescriptionText", "배치 가능 영역 +4칸", 60, 955, 480, 65, 40, false, TextAnchor.MiddleCenter));
        FinishCard(bindings, cells, new[] { new Vector2Int(2, 1), new Vector2Int(3, 1), new Vector2Int(2, 2), new Vector2Int(2, 3) });
        return root.gameObject;
    }

    private static GameObject BuildRelic(BuildContext context)
    {
        RectTransform root = BeginCard(context, CARD_NAMES[2], "CardShell_Relic", "기물", "마력 증폭기", RELIC_COLOR,
            context.Sprites["Icon_Type_Relic_Diamond_v2"], out SerializedObject bindings);
        Image[] cells = BuildGrid(context, root, 72, 272, RELIC_COLOR);
        Bind(bindings, "_artwork", Picture(context, root, "Artwork", context.Sprites["Artwork_ManaAmplifier"], 106, 277, 390, 378));
        Bind(bindings, "_traitTitleText", Label(context, root, "TraitTitleText", "특성", 64, 670, 118, 50, 30, true, TextAnchor.MiddleCenter));
        Bind(bindings, "_traitDescriptionText", Label(context, root, "TraitDescriptionText", "3성 달성 시\n재사용 시간 감소", 62, 716, 480, 96, 30));
        Bind(bindings, "_skillTitleText", Label(context, root, "SkillTitleText", "보유 스킬", 54, 834, 145, 48, 28, true, TextAnchor.MiddleCenter));
        Bind(bindings, "_skillDescriptionText", Label(context, root, "SkillDescriptionText", "주변 마법 피해 +15%", 62, 941, 480, 66, 32, false, TextAnchor.MiddleCenter));
        FinishCard(bindings, cells, Array.Empty<Vector2Int>());
        return root.gameObject;
    }

    private static RectTransform BeginCard(BuildContext context, string name, string shell, string category, string title,
        Color tint, Sprite typeIcon, out SerializedObject bindings)
    {
        RectTransform root = Group(context, null, name, 0, 0, 600, 1100);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        Picture(context, root, "Shell", context.Sprites[shell], 0, 100, 600, 1000, false);
        bindings = new SerializedObject(root.gameObject.AddComponent<UIBattlePreparationCardView>());
        Bind(bindings, "_categoryText", Label(context, root, "CategoryText", category, 150, 0, 300, 76, 52, true, TextAnchor.MiddleCenter));
        RectTransform badge = Group(context, root, "TypeBadge", 0, 65, 160, 160);
        Image body = Solid(context, badge, "BlackDiamond", 80, 80, 102, 102, new Color32(13, 13, 17, 255));
        body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        body.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
        Picture(context, badge, "Frame", context.Diamond, 0, 0, 160, 160).color = tint;
        // Sprite 내부도 마름모 여백으로 정규화하여 3종의 중심과 사선 간격을 맞춘다.
        Bind(bindings, "_typeIcon", Picture(context, badge, "TypeIcon", typeIcon, 24, 24, 112, 112));
        Picture(context, root, "StarBadge", context.Sprites["Badge_Star"], 476, 72, 124, 124);
        Text rank = Label(context, root, "RankText", "1", 493, 84, 90, 100, 60, true, TextAnchor.MiddleCenter);
        rank.color = new Color32(23, 20, 17, 255);
        Outline rankOutline = rank.gameObject.AddComponent<Outline>();
        rankOutline.effectColor = IVORY;
        rankOutline.effectDistance = new Vector2(1.2f, -1.2f);
        rankOutline.useGraphicAlpha = true;
        Bind(bindings, "_rankText", rank);
        Text titleText = Label(context, root, "TitleText", title, 166, 179, 355, 64, 42, true, TextAnchor.MiddleCenter);
        titleText.resizeTextForBestFit = true;
        titleText.resizeTextMinSize = 26;
        titleText.resizeTextMaxSize = 42;
        Bind(bindings, "_titleText", titleText);
        return root;
    }

    private static Image[] BuildGrid(BuildContext context, RectTransform parent, float x, float y, Color tint)
    {
        const int WIDTH = 6;
        const int HEIGHT = 5;
        const float CELL_SIZE = 76;
        RectTransform grid = Group(context, parent, "FootprintGrid", x, y, WIDTH * CELL_SIZE, HEIGHT * CELL_SIZE);
        Color gridColor = new Color32(185, 180, 165, 68);
        for (int column = 0; column <= WIDTH; column++)
            Solid(context, grid, "ColumnLine_" + column, column * CELL_SIZE - 0.5f, 0, 1, HEIGHT * CELL_SIZE, gridColor);
        for (int row = 0; row <= HEIGHT; row++)
            Solid(context, grid, "RowLine_" + row, 0, row * CELL_SIZE - 0.5f, WIDTH * CELL_SIZE, 1, gridColor);

        var cells = new Image[WIDTH * HEIGHT];
        Color fill = tint;
        fill.a = 0.20f;
        Color border = tint;
        border.a = 0.85f;
        for (int row = 0; row < HEIGHT; row++)
        {
            for (int column = 0; column < WIDTH; column++)
            {
                // Runtime 배열과 동일하게 좌상단부터 행 우선(y * width + x)으로 연결한다.
                Image cell = Solid(context, grid, "Cell_" + column + "_" + row,
                    column * CELL_SIZE, row * CELL_SIZE, CELL_SIZE, CELL_SIZE, fill);
                Solid(context, cell.rectTransform, "TopBorder", 0, 0, CELL_SIZE, 2, border);
                Solid(context, cell.rectTransform, "BottomBorder", 0, CELL_SIZE - 2, CELL_SIZE, 2, border);
                Solid(context, cell.rectTransform, "LeftBorder", 0, 2, 2, CELL_SIZE - 4, border);
                Solid(context, cell.rectTransform, "RightBorder", CELL_SIZE - 2, 2, 2, CELL_SIZE - 4, border);
                cells[row * WIDTH + column] = cell;
            }
        }
        return cells;
    }

    private static void BuildStat(BuildContext context, RectTransform parent, string name, string label, string value,
        Sprite icon, float x, SerializedObject bindings, string labelField, string valueField)
    {
        RectTransform group = Group(context, parent, name, x, 0, 184, 90);
        // 기존 심장 이미지의 투명 여백만 표시 크기로 보정하고 원본은 보존한다.
        if (name == "Health") Picture(context, group, "Icon", icon, -17, 3, 90, 90);
        else if (name == "Attack") Picture(context, group, "Icon", icon, 7, 17, 42, 62);
        else Picture(context, group, "Icon", icon, 8, 28, 40, 40);
        Bind(bindings, labelField, Label(context, group, "LabelText", label, 54, 0, 122, 38, 24, true, TextAnchor.MiddleLeft));
        Bind(bindings, valueField, Label(context, group, "ValueText", value, 54, 31, 124, 59, 38, true, TextAnchor.MiddleLeft));
    }

    private static void FinishCard(SerializedObject bindings, Image[] cells, Vector2Int[] footprint)
    {
        SerializedProperty gridSize = RequireProperty(bindings, "_footprintGridSize");
        gridSize.vector2IntValue = new Vector2Int(6, 5);
        SerializedProperty targets = RequireProperty(bindings, "_footprintCells");
        targets.arraySize = cells.Length;
        for (int index = 0; index < cells.Length; index++) targets.GetArrayElementAtIndex(index).objectReferenceValue = cells[index];
        bindings.ApplyModifiedPropertiesWithoutUndo();
        ((UIBattlePreparationCardView)bindings.targetObject).SetFootprint(footprint);
    }

    private static RectTransform Group(BuildContext context, RectTransform parent, string name, float x, float y, float width, float height)
    {
        // ObjectFactory의 기본 부모 배치 경로를 피하고, 숨긴 임시 객체를 먼저 격리한다.
        GameObject item = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(item, context.Scene);
        if (item.scene != context.Scene) throw new InvalidOperationException("임시 카드 오브젝트 격리 실패: " + name);
        item.hideFlags = HideFlags.None;
        RectTransform rect = item.GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return rect;
    }

    private static Image Picture(BuildContext context, RectTransform parent, string name, Sprite sprite,
        float x, float y, float width, float height, bool preserveAspect = true)
    {
        Image image = Group(context, parent, name, x, y, width, height).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return image;
    }

    private static Image Solid(BuildContext context, RectTransform parent, string name,
        float x, float y, float width, float height, Color color)
    {
        Image image = Picture(context, parent, name, null, x, y, width, height, false);
        image.color = color;
        return image;
    }

    private static Text Label(BuildContext context, RectTransform parent, string name, string content,
        float x, float y, float width, float height, int fontSize, bool bold = false, TextAnchor alignment = TextAnchor.UpperLeft)
    {
        Text text = Group(context, parent, name, x, y, width, height).gameObject.AddComponent<Text>();
        text.font = context.Font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = alignment;
        text.color = IVORY;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = false;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.lineSpacing = 1f;
        return text;
    }

    private static Sprite ImportCardSprite(TextureImporter importer, string path)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.spritePixelsPerUnit = 100;
        importer.spriteBorder = Vector4.zero;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return RequireAsset<Sprite>(path);
    }

    private static T RequireAsset<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException(typeof(T).Name + " 에셋이 없습니다: " + path);
        return asset;
    }

    private static void Bind(SerializedObject bindings, string name, Object reference)
        => RequireProperty(bindings, name).objectReferenceValue = reference;

    private static SerializedProperty RequireProperty(SerializedObject bindings, string name)
        => bindings.FindProperty(name) ?? throw new InvalidOperationException("카드 직렬화 필드가 없습니다: " + name);

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("에셋 폴더 경로가 잘못되었습니다: " + path);
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
            throw new IOException("카드 에셋 폴더 생성 실패: " + path);
    }

    private static List<SceneSnapshot> CaptureScenes()
    {
        var scenes = new List<SceneSnapshot>();
        for (int index = 0; index < SceneManager.sceneCount; index++)
            scenes.Add(new SceneSnapshot(SceneManager.GetSceneAt(index)));
        return scenes;
    }

    private static void VerifyScenes(List<SceneSnapshot> snapshots, int sceneCount, Scene activeScene)
    {
        if (SceneManager.sceneCount != sceneCount || SceneManager.GetActiveScene() != activeScene)
            throw new InvalidOperationException("카드 생성 중 실제 Scene 구성 또는 활성 Scene이 변경되었습니다. 자동 저장하지 않았습니다.");
        foreach (SceneSnapshot snapshot in snapshots) snapshot.Verify();
    }

    private sealed class SceneSnapshot
    {
        private readonly Scene _scene;
        private readonly bool _isDirty;
        private readonly GameObject[] _roots;

        public SceneSnapshot(Scene scene)
        {
            _scene = scene;
            _isDirty = scene.isDirty;
            _roots = scene.isLoaded ? scene.GetRootGameObjects() : Array.Empty<GameObject>();
        }

        public void Verify()
        {
            if (!_scene.IsValid() || _scene.isDirty != _isDirty)
                throw new InvalidOperationException("실제 Scene의 변경 상태가 달라졌습니다: " + _scene.path);
            GameObject[] roots = _scene.isLoaded ? _scene.GetRootGameObjects() : Array.Empty<GameObject>();
            if (roots.Length != _roots.Length) throw new InvalidOperationException("실제 Scene의 루트 수가 달라졌습니다: " + _scene.path);
            for (int index = 0; index < roots.Length; index++)
                if (roots[index] != _roots[index]) throw new InvalidOperationException("실제 Scene의 루트 구성이 달라졌습니다: " + _scene.path);
        }
    }

    private sealed class BuildContext
    {
        public readonly Scene Scene;
        public readonly Font Font;
        public readonly Dictionary<string, Sprite> Sprites;
        public readonly Sprite Diamond;
        public readonly Sprite Shield;
        public readonly Sprite Heart;

        public BuildContext(Scene scene, Font font, Dictionary<string, Sprite> sprites, Sprite diamond, Sprite shield, Sprite heart)
        {
            Scene = scene;
            Font = font;
            Sprites = sprites;
            Diamond = diamond;
            Shield = shield;
            Heart = heart;
        }
    }
}
