using System;
using System.Linq;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>승인된 봉인 카드 아트를 현재 로비의 세 슬롯에 연결한다. 해금 데이터는 사용하지 않는다.</summary>
public static class LobbyStageLockBuilder
{
    public const string ART_ROOT = "Assets/06.UI/LobbyMutedPreview/Heraldry_v1/StageLocks_v1/Sprites/";
    private const string UNDO_NAME = "Apply Lobby Stage Lock Theme";

    [MenuItem("Tools/UI/Lobby Stage Locks/Apply Reference Layout")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != LobbyInlineRecordBuilder.SCENE_PATH)
            throw new InvalidOperationException("Edit Mode에서 Builds/Lobby를 열어 주세요.");
        Sprite chains = ImportSprite("stage_lock_chains_v1");
        Sprite padlock = ImportSprite("stage_lock_padlock_gray_v1");
        Sprite ribbon = ImportSprite("stage_lock_seal_ribbon_v1");
        Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/06.UI/LobbyMutedPreview/Heraldry_v1/Sprites/difficulty_selected.asset");
        Sprite sideFrame = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/06.UI/LobbyMutedPreview/Heraldry_v1/Sprites/difficulty_card.asset");
        if (frame == null || sideFrame == null) throw new InvalidOperationException("난이도 카드 프레임이 없습니다.");
        var cards = UnityEngine.Object.FindObjectsByType<UILobbyStageCardView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.gameObject.scene == scene).ToArray();
        if (cards.Length != 3) throw new InvalidOperationException("로비 카드 세 개를 확인해 주세요.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        try
        {
            foreach (var card in cards) ConfigureCard(card, chains, padlock, ribbon,
                card.name == "CurrentStage" ? frame : sideFrame);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }

    private static void ConfigureCard(UILobbyStageCardView card, Sprite chains, Sprite padlock, Sprite ribbon, Sprite frame)
    {
        Transform visual = card.transform.Find("VisualLayers");
        Transform caption = visual.Find("HeraldryCaption");
        var title = caption.Find("Title").GetComponent<TMP_Text>();
        var description = caption.Find("Description").GetComponent<TMP_Text>();
        var descriptionGroup = description.GetComponent<CanvasGroup>();
        if (descriptionGroup == null) descriptionGroup = Undo.AddComponent<CanvasGroup>(description.gameObject);
        var overlay = (RectTransform)visual.Find("LockOverlay");
        if (overlay.Find("Chains") != null)
            throw new InvalidOperationException(card.name + "는 이미 구성됐습니다. Inspector에서 조정하세요.");
        Undo.RecordObject(overlay, UNDO_NAME);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        overlay.localScale = Vector3.one;
        float scale = ((RectTransform)card.transform).rect.width / 530f;

        Image chainImage = CreateImage("Chains", overlay, chains, Vector2.zero, new Vector2(398f, 282f) * scale);
        var lockImage = overlay.Find("Lock").GetComponent<Image>();
        Undo.RecordObject(lockImage, UNDO_NAME);
        lockImage.sprite = padlock;
        lockImage.type = Image.Type.Simple;
        lockImage.preserveAspect = true;
        lockImage.raycastTarget = false;
        lockImage.color = new Color(1f, .89f, .69f, 1f);
        var lockRect = lockImage.rectTransform;
        SetRect(lockRect, Vector2.zero, new Vector2(82f, 108f) * scale);
        Undo.RecordObject(lockRect, UNDO_NAME);
        lockRect.SetAsLastSibling();
        chainImage.transform.SetAsFirstSibling();
        float sealY = card.name == "CurrentStage" ? -104.4f : -112f * scale;
        var seal = CreateImage("SealRibbon", overlay, ribbon,
            new Vector2(0f, sealY), new Vector2(166.1f, 68.3f) * scale);
        seal.preserveAspect = false;
        var sealLabel = CreateText("SealLabel", seal.transform, title, "봉인됨", new Vector2(0f, 3.66f),
            new Vector2(96.7f, 34f) * scale, 26f * scale, title.color);
        sealLabel.enableAutoSizing = false;
        sealLabel.fontSizeMin = 9.900001f * scale;
        sealLabel.fontSizeMax = 18f * scale;
        CreateText("ConditionHeader", overlay, description, "해금 조건", new Vector2(0f, -185f) * scale,
            new Vector2(450f, 32f) * scale, 20f * scale, new Color(.61f, .56f, .49f, 1f));
        TMP_Text condition = CreateText("ConditionValue", overlay, description, "보통 난이도 클리어 시 해금",
            new Vector2(0f, -224f) * scale, new Vector2(450f, 38f) * scale, 23f * scale,
            new Color(.94f, .91f, .84f, 1f));

        // 비활성 난이도 묶음도 포함해 슬롯을 재사용할 때 이전 실루엣이 남지 않게 한다.
        Transform portraits = visual.Find("ArtworkMask/ArtworkRoot/ArtworkMotion");
        if (portraits == null) portraits = visual.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(t => t.name == "ArtworkMotion");
        if (portraits == null) throw new InvalidOperationException(card.name + "의 용사 묶음이 없습니다.");
        Image[] images = portraits.GetComponentsInChildren<Image>(true).Where(i => i.sprite != null).ToArray();
        if (images.Length != 9) throw new InvalidOperationException(card.name + "의 용사 1/3/5개 연결을 확인해 주세요.");
        Undo.RecordObject(card, UNDO_NAME);
        Undo.RecordObject(visual.Find("Frame").GetComponent<Image>(), UNDO_NAME);
        var serialized = new SerializedObject(card);
        var imageArray = serialized.FindProperty("_silhouetteImages");
        var colors = serialized.FindProperty("_unlockedPortraitColors");
        imageArray.arraySize = colors.arraySize = images.Length;
        for (int i = 0; i < images.Length; i++)
        {
            imageArray.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
            colors.GetArrayElementAtIndex(i).colorValue = images[i].color;
        }
        serialized.FindProperty("_silhouetteColor").colorValue = new Color(.035f, .035f, .045f, 1f);
        serialized.FindProperty("_descriptionGroup").objectReferenceValue = descriptionGroup;
        serialized.FindProperty("_lockConditionLabel").objectReferenceValue = condition;
        serialized.FindProperty("_lockSprite").objectReferenceValue = padlock;
        serialized.FindProperty("_frameSprite").objectReferenceValue = frame;
        serialized.ApplyModifiedProperties();
        var panel = card.GetComponentInChildren<UILobbyDifficultyRecordPanel>(true);
        if (panel != null && panel.UsesInlineDisplay)
        {
            Undo.RecordObject(panel, UNDO_NAME);
            var panelObject = new SerializedObject(panel);
            panelObject.FindProperty("_stageCard").objectReferenceValue = card;
            panelObject.ApplyModifiedProperties();
        }
        Undo.RecordObject(descriptionGroup, UNDO_NAME);
        descriptionGroup.interactable = false;
        descriptionGroup.blocksRaycasts = false;
        card.RefreshVisuals();
    }

    [MenuItem("Tools/UI/Lobby Stage Locks/Refresh Seal Ribbon Sprite")]
    public static void RefreshSealRibbonSprite()
    {
        ImportSprite("stage_lock_seal_ribbon_v1", true);
        AssetDatabase.SaveAssets();
    }

    private static Sprite ImportSprite(string name, bool refreshCrop = false)
    {
        string path = ART_ROOT + name + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("생성 PNG가 없습니다: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        // 생성 PNG의 RGB/알파는 그대로 두고 Unity Sprite 영역만 투명 여백을 제외한다.
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
        for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a >= 12)
                { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
        if (maxX < minX) throw new InvalidOperationException("투명 PNG의 도안을 확인해 주세요: " + path);
        Rect crop = new Rect(Mathf.Max(0, minX - 2), Mathf.Max(0, minY - 2),
            Mathf.Min(texture.width - Mathf.Max(0, minX - 2), maxX - minX + 5),
            Mathf.Min(texture.height - Mathf.Max(0, minY - 2), maxY - minY + 5));
        string spritePath = ART_ROOT + name + ".asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (existing != null && !refreshCrop) return existing;
        Sprite sprite = Sprite.Create(texture, crop, Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        if (existing != null)
        {
            // 네이티브 Sprite 내용만 갱신해 기존 씬 참조와 GUID를 유지한다.
            Undo.RecordObject(existing, "Refresh Seal Ribbon Sprite");
            EditorUtility.CopySerialized(sprite, existing);
            UnityEngine.Object.DestroyImmediate(sprite);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(sprite, spritePath);
        return sprite;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        var image = Undo.AddComponent<Image>(rect.gameObject);
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_Text source, string text,
        Vector2 position, Vector2 size, float fontSize, Color color)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        var label = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
        label.font = source.font;
        label.fontSharedMaterial = source.fontSharedMaterial;
        label.fontSize = fontSize;
        label.text = text;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
        Undo.SetTransformParent(go.transform, parent, UNDO_NAME);
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        SetRect(rect, position, size);
        return rect;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        Undo.RecordObject(rect, UNDO_NAME);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
