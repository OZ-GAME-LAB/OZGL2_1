using System;
using System.IO;
using System.Linq;
using OZGL2.UI;
using OZGL2.UIBridge;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>설명창 외형만 생성합니다. 기존 게임 데이터와 외부 원본 이미지는 수정하지 않습니다.</summary>
public static class HeraldryTooltipPrefabBuilder
{
    public const string ROOT = "Assets/06.UI/BattleMutedPreview/Heraldry_Tooltips_v1";
    public const string REWARD_PREFAB = ROOT + "/Prefabs/RoundRewardPanel_Heraldry.prefab";
    public const string SYNERGY_PREFAB = ROOT + "/Prefabs/SynergyTooltipPanel_Heraldry.prefab";
    private const string FONT_ROOT = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/02.SDF_File/";

    [MenuItem("Tools/OZGL2/Battle Tooltips/Build New Heraldry Prefabs")]
    public static void Build()
    {
        RequireEditMode();
        // 수동 편집한 기존 프리팹을 생성 메뉴로 덮어쓰지 않습니다.
        if (File.Exists(REWARD_PREFAB) || File.Exists(SYNERGY_PREFAB))
            throw new InvalidOperationException("이미 생성된 설명창은 Prefab Mode에서 편집해 주세요. 자동 덮어쓰기는 하지 않습니다.");
        Sprite frame = CreateSprite("tooltip_frame_gold_v1", true);
        Sprite crest = CreateSprite("tooltip_crest_v1", false);
        CreatePanel(REWARD_PREFAB, 420f, 240f, frame, crest, true);
        CreatePanel(SYNERGY_PREFAB, 600f, 530f, frame, crest, false);
        Debug.Log("새 보상·시너지 설명창 프리팹을 생성했습니다. Connect 메뉴로 연결한 뒤 씬은 별도로 저장해 주세요.");
    }

    [MenuItem("Tools/OZGL2/Battle Tooltips/Connect To Current InGame")]
    public static void Connect()
    {
        RequireEditMode();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/00.Scenes/Builds/InGame.unity")
            throw new InvalidOperationException("InGame 씬에서만 연결합니다.");
        var reward = AssetDatabase.LoadAssetAtPath<GameObject>(REWARD_PREFAB)?.GetComponent<UIAdaptiveTooltipPanel>();
        var synergy = AssetDatabase.LoadAssetAtPath<GameObject>(SYNERGY_PREFAB)?.GetComponent<UIAdaptiveTooltipPanel>();
        if (reward == null || synergy == null) throw new InvalidOperationException("신규 프리팹 두 개가 필요합니다.");
        var hud = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InGameCurrencyHud>(true)).Single();
        var view = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UIBattleMutedPreviewView>(true)).Single();
        Undo.RecordObject(hud, "보상 설명창 프리팹 연결");
        var bridge = view.GetComponent<InGameSynergyHudBridge>();
        if (bridge == null) bridge = Undo.AddComponent<InGameSynergyHudBridge>(view.gameObject);
        Undo.RecordObject(bridge, "시너지 설명창 프리팹 연결");
        SetObject(hud, "_summaryPanelPrefab", reward);
        SetObject(bridge, "_tooltipPanelPrefab", synergy);
        PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        PrefabUtility.RecordPrefabInstancePropertyModifications(bridge);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("InGame 프리팹 참조를 연결했습니다. Undo 지원, 씬은 자동 저장하지 않습니다.");
    }

    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 실행해 주세요.");
    }

    private static Sprite CreateSprite(string name, bool sliced)
    {
        string png = ROOT + "/Sprites/" + name + ".png";
        string path = ROOT + "/Sprites/" + name + ".asset";
        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(png);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            ImageConversion.LoadImage(pixels, File.ReadAllBytes(png));
            Color32[] colors = pixels.GetPixels32();
            int left = pixels.width, bottom = pixels.height, right = 0, top = 0;
            for (int y = 0; y < pixels.height; y++)
                for (int x = 0; x < pixels.width; x++)
                    if (colors[y * pixels.width + x].a > 32)
                    {
                        left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                    }
            left = Mathf.Max(0, left - 4); bottom = Mathf.Max(0, bottom - 4);
            right = Mathf.Min(pixels.width - 1, right + 4); top = Mathf.Min(pixels.height - 1, top + 4);
            Rect rect = new Rect(left, bottom, right - left + 1, top - bottom + 1);
            Vector4 border = sliced ? Vector4.one * Mathf.Ceil(Mathf.Min(rect.width, rect.height) * 0.18f) : Vector4.zero;
            Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }
        finally { Object.DestroyImmediate(pixels); }
    }

    private static void CreatePanel(string path, float width, float height, Sprite frame, Sprite crest, bool reward)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(path), typeof(RectTransform), typeof(UIAdaptiveTooltipPanel));
        try
        {
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(width, height);
            var background = ImageChild(root.transform, "Background", null);
            background.color = new Color32(18, 16, 18, 255);
            Stretch(background.rectTransform, 10f);
            var edge = ImageChild(root.transform, "Frame", frame);
            edge.type = Image.Type.Sliced;
            edge.fillCenter = false;
            edge.pixelsPerUnitMultiplier = 4f;
            Stretch(edge.rectTransform, 0f);
            var ornament = ImageChild(root.transform, "HeaderOrnament", crest);
            ornament.preserveAspect = true;
            ornament.rectTransform.anchorMin = ornament.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            ornament.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            ornament.rectTransform.sizeDelta = new Vector2(88f, 70f);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            Stretch((RectTransform)content.transform, 0f);
            TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ROOT + "HeirofLightBold_SDF.asset");
            TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ROOT + "NEXON_Lv2_Gothic_SDF.asset");
            if (titleFont == null || bodyFont == null) throw new InvalidOperationException("기존 공용 글꼴 참조가 필요합니다.");
            var settings = new SerializedObject(root.GetComponent<UIAdaptiveTooltipPanel>());
            settings.FindProperty("_contentRoot").objectReferenceValue = content.transform;
            settings.FindProperty("_frame").objectReferenceValue = edge;
            settings.FindProperty("_headerOrnament").objectReferenceValue = ornament.rectTransform;
            settings.FindProperty("_baseWidth").floatValue = width;
            settings.FindProperty("_padding").vector4Value = new Vector4(32f, 50f, 32f, 28f);
            settings.FindProperty("_ornamentSize").vector2Value = new Vector2(88f, 70f);
            settings.FindProperty("_titleFont").objectReferenceValue = titleFont;
            settings.FindProperty("_bodyFont").objectReferenceValue = bodyFont;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PreviewText(content.transform, "Title", reward ? "이번 라운드 보상" : "방패병 시너지", titleFont, reward ? 23f : 28f, new Vector2(32f, -50f), new Vector2(width - 64f, 40f));
            PreviewText(content.transform, "Preview", reward ? "재화  최대 +33\n용사 처치 + 클리어 보상\n\nSP  +1\n스킬 해금" : "현재 1명 배치\n\n1단계  3명\n체력 +15%\n\n2단계  5명\n체력 +35%\n\n조합 시너지\n불멸의 방벽\n방패병 체력 +15%, 힐러 회복량 +15%", bodyFont, reward ? 19f : 24f, new Vector2(32f, -96f), new Vector2(width - 64f, height - 124f));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Image ImageChild(Transform parent, string name, Sprite sprite)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        var image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
    }

    private static void PreviewText(Transform parent, string name, string text, TMP_FontAsset font, float size, Vector2 pos, Vector2 area)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        var label = child.GetComponent<TextMeshProUGUI>();
        label.font = font; label.fontSize = size; label.text = text;
        label.color = new Color32(244, 227, 184, 255); label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.anchoredPosition = pos; label.rectTransform.sizeDelta = area;
    }

    private static void SetObject(Object owner, string property, Object value)
    {
        var serialized = new SerializedObject(owner);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }
}
