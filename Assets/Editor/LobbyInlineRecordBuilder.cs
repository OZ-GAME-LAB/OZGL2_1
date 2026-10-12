using System;
using System.Collections.Generic;
using System.IO;
using OZGL2.UIBridge;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>현재 로비 카드에 기록 레이어를 한 번 구성한다. 저장은 사용자가 별도로 수행한다.</summary>
public static class LobbyInlineRecordBuilder
{
    public const string SCENE_PATH = "Assets/00.Scenes/Builds/Lobby.unity";
    public const string ICON_ROOT = "Assets/06.UI/LobbyMutedPreview/Heraldry_v1/RecordIcons/";
    public const string MASK_PATH = "Assets/06.UI/LobbyMutedPreview/Heraldry_v1/Sprites/difficulty_inner_mask_v1.png";
    private const string UNDO_NAME = "Configure Lobby Inline Records";

    [MenuItem("Tools/UI/Lobby Records/Apply Inline Layout")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != SCENE_PATH)
            throw new InvalidOperationException("Edit Mode에서 Builds/Lobby 씬을 열어 주세요.");
        Transform card = GameObject.Find("Canvas_Lobby/StageSelection/CurrentStage")?.transform;
        if (card == null) throw new InvalidOperationException("CurrentStage가 없습니다.");
        Transform caption = card.Find("VisualLayers/HeraldryCaption");
        var panel = card.Find("RecordHoverHitArea")?.GetComponent<UILobbyDifficultyRecordPanel>();
        var description = caption?.Find("Description")?.GetComponent<TMP_Text>();
        var hourglass = AssetDatabase.LoadAssetAtPath<Sprite>(ICON_ROOT + "record_hourglass_white_v1.png");
        var sword = AssetDatabase.LoadAssetAtPath<Sprite>(ICON_ROOT + "record_sword_white_v1.png");
        if (panel == null || description == null || hourglass == null || sword == null)
            throw new InvalidOperationException("기록 패널, 설명 또는 Sprite 연결이 없습니다.");
        if (caption.Find("InlineRecords") != null)
            throw new InvalidOperationException("이미 구성됐습니다. Inspector에서 기존 레이어를 편집하세요.");

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        try
        {
            var descriptionGroup = description.GetComponent<CanvasGroup>();
            if (descriptionGroup == null) descriptionGroup = Undo.AddComponent<CanvasGroup>(description.gameObject);
            SetGroup(descriptionGroup, 1f);

            RectTransform shadeRect = CreateRect("RecordShade", caption, new Vector2(0f, -140f), new Vector2(412f, 240f));
            shadeRect.SetAsFirstSibling();
            var shade = Undo.AddComponent<UIRecordCardShade>(shadeRect.gameObject);
            shade.color = new Color(0f, 0f, 0f, 0.72f);
            shade.raycastTarget = false;
            var shadeGroup = Undo.AddComponent<CanvasGroup>(shadeRect.gameObject);
            SetGroup(shadeGroup, 0f);
            ConfigureShadeMask(card, shadeRect);

            RectTransform records = CreateRect("InlineRecords", caption, new Vector2(0f, -196f), new Vector2(448f, 136f));
            var recordGroup = Undo.AddComponent<CanvasGroup>(records.gameObject);
            SetGroup(recordGroup, 0f);
            TMP_Text time = CreateColumn(records, description, "ClearTime", -112f, hourglass, "클리어 시간", "--:--");
            TMP_Text wave = CreateColumn(records, description, "HighestWave", 112f, sword, "최대 클리어 웨이브", "기록 없음");

            Undo.RecordObject(panel, UNDO_NAME);
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("_useInlineDisplay").boolValue = true;
            serialized.FindProperty("_descriptionGroup").objectReferenceValue = descriptionGroup;
            serialized.FindProperty("_shadeGroup").objectReferenceValue = shadeGroup;
            serialized.FindProperty("_recordGroup").objectReferenceValue = recordGroup;
            serialized.FindProperty("_waveValueLabel").objectReferenceValue = wave;
            serialized.FindProperty("_timeValueLabel").objectReferenceValue = time;
            serialized.FindProperty("_expandDuration").floatValue = 0.20f;
            serialized.ApplyModifiedProperties();

            // VisualLayers는 표시 전용으로 raycast를 차단한다. 같은 크기의 직계 자식에서 입력만 받는다.
            var hit = (RectTransform)panel.transform;
            Undo.RecordObject(hit, UNDO_NAME);
            hit.anchorMin = Vector2.zero;
            hit.anchorMax = Vector2.one;
            hit.pivot = Vector2.one * 0.5f;
            hit.offsetMin = hit.offsetMax = Vector2.zero;
            hit.localScale = Vector3.one;
            // StartButton은 이 HitArea보다 뒤에 그려져 기존 클릭 우선순위를 유지한다.
            hit.SetAsLastSibling();
            hit.SetSiblingIndex(card.Find("StartButton").GetSiblingIndex());
            // 난이도 요약 금색 문구는 유지하고 구형 확장 기록창만 숨긴다.
            Transform stageRecord = card.Find("StageRecord");
            Undo.RecordObject(stageRecord.gameObject, UNDO_NAME);
            stageRecord.gameObject.SetActive(true);
            Undo.RecordObject(stageRecord.Find("RecordPanelVisual").gameObject, UNDO_NAME);
            stageRecord.Find("RecordPanelVisual").gameObject.SetActive(false);
            foreach (var toggle in UnityEngine.Object.FindObjectsByType<LobbyCardRecordToggle>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (toggle.gameObject.scene != scene) continue;
                Undo.RecordObject(toggle, UNDO_NAME);
                toggle.enabled = false;
            }
            panel.RefreshLayout();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = records.gameObject;
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
        Undo.CollapseUndoOperations(undoGroup);
    }

    [MenuItem("Tools/UI/Lobby Records/Apply Frame Interior Mask")]
    public static void ApplyFrameInteriorMask()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != SCENE_PATH)
            throw new InvalidOperationException("Edit Mode에서 Builds/Lobby 씬을 열어 주세요.");
        Transform card = GameObject.Find("Canvas_Lobby/StageSelection/CurrentStage")?.transform;
        Transform caption = card?.Find("VisualLayers/HeraldryCaption");
        Transform shade = caption?.Find("RecordShade") ?? caption?.Find("RecordShadeMask/RecordShade");
        if (shade == null) throw new InvalidOperationException("RecordShade가 없습니다.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Mask Lobby Record Shade");
        try
        {
            ConfigureShadeMask(card, (RectTransform)shade);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
        Undo.CollapseUndoOperations(group);
    }

    private static void ConfigureShadeMask(Transform card, RectTransform shade)
    {
        Transform caption = card.Find("VisualLayers/HeraldryCaption");
        var frame = card.Find("VisualLayers/Frame").GetComponent<Image>();
        if (frame.sprite == null) throw new InvalidOperationException("난이도 프레임 Sprite가 없습니다.");
        Sprite sprite = BuildInteriorMask(frame.sprite);
        Transform existing = caption.Find("RecordShadeMask");
        RectTransform maskRect;
        if (existing == null)
        {
            int sibling = shade.GetSiblingIndex();
            maskRect = CreateRect("RecordShadeMask", caption, Vector2.zero, Vector2.zero);
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;
            maskRect.SetSiblingIndex(sibling);
        }
        else maskRect = (RectTransform)existing;
        var image = maskRect.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(maskRect.gameObject);
        Undo.RecordObject(image, UNDO_NAME);
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = frame.preserveAspect;
        image.raycastTarget = false;
        var mask = maskRect.GetComponent<Mask>();
        if (mask == null) mask = Undo.AddComponent<Mask>(maskRect.gameObject);
        Undo.RecordObject(mask, UNDO_NAME);
        mask.showMaskGraphic = false;
        // 기록 글자는 마스크 밖에 유지한다. 기존 CanvasGroup 연결도 보존한다.
        if (shade.parent != maskRect) Undo.SetTransformParent(shade, maskRect, UNDO_NAME);
        // 투명해지는 시작 높이는 유지하고 면만 마스크의 좌우·하단까지 채운다.
        // 기존 6각형/고정 높이를 유지하면 마스크 안쪽에 수평 경계가 남는다.
        Vector3 shadeTop = maskRect.InverseTransformPoint(shade.TransformPoint(
            new Vector3(0f, shade.rect.yMax, 0f)));
        Undo.RecordObject(shade, UNDO_NAME);
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = new Vector2(1f, .5f);
        shade.offsetMin = Vector2.zero;
        shade.offsetMax = new Vector2(0f, shadeTop.y - Mathf.Lerp(maskRect.rect.yMin, maskRect.rect.yMax, .5f));
        var graphic = shade.GetComponent<UIRecordCardShade>();
        Undo.RecordObject(graphic, UNDO_NAME);
        graphic.maskable = true;
    }

    private static Sprite BuildInteriorMask(Sprite source)
    {
        if (!source.texture.isReadable)
            throw new InvalidOperationException("마스크 추출용 프레임 Texture는 읽기 가능한 상태여야 합니다.");
        Rect crop = source.rect;
        int width = Mathf.RoundToInt(crop.width), height = Mathf.RoundToInt(crop.height);
        int left = Mathf.RoundToInt(crop.x), bottom = Mathf.RoundToInt(crop.y);
        Color32[] sourcePixels = source.texture.GetPixels32();
        var allowed = new bool[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color32 c = sourcePixels[(bottom + y) * source.texture.width + left + x];
                allowed[y * width + x] = c.a >= 240 && c.r < 100 && c.g < 70 && c.b < 75
                    && c.r - c.g < 45 && c.r - c.b < 45 && c.b - c.g >= 2 && c.r - c.b >= 2;
            }
        int seed = height / 2 * width + width / 2;
        if (!allowed[seed]) throw new InvalidOperationException("프레임 내부 기준 색을 찾지 못했습니다.");
        var interior = new bool[allowed.Length];
        var queue = new Queue<int>();
        interior[seed] = true;
        queue.Enqueue(seed);
        int count = 0;
        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            count++;
            int x = index % width, y = index / width;
            if (x > 0) Visit(index - 1, allowed, interior, queue);
            if (x + 1 < width) Visit(index + 1, allowed, interior, queue);
            if (y > 0) Visit(index - width, allowed, interior, queue);
            if (y + 1 < height) Visit(index + width, allowed, interior, queue);
        }
        if (count < allowed.Length * .45f || count > allowed.Length * .85f)
            throw new InvalidOperationException("프레임 내부 윤곽 면적을 확인해 주세요: " + count);
        var pixels = new Color32[interior.Length];
        // 원본 기준 2px 안쪽으로 물려 금색 면과 가장자리 필터링을 보호한다.
        for (int y = 2; y < height - 2; y++)
            for (int x = 2; x < width - 2; x++)
            {
                bool isInside = true;
                for (int dy = -2; dy <= 2 && isInside; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        if (!interior[(y + dy) * width + x + dx]) { isInside = false; break; }
                pixels[y * width + x] = new Color32(255, 255, 255, isInside ? (byte)255 : (byte)0);
            }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(MASK_PATH, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(MASK_PATH);
        var importer = (TextureImporter)AssetImporter.GetAtPath(MASK_PATH);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        var importSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(importSettings);
        importSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(importSettings);
        importer.spritePixelsPerUnit = source.pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(MASK_PATH);
    }

    private static void Visit(int index, bool[] allowed, bool[] interior, Queue<int> queue)
    {
        if (!allowed[index] || interior[index]) return;
        interior[index] = true;
        queue.Enqueue(index);
    }

    private static TMP_Text CreateColumn(Transform parent, TMP_Text source, string name, float x,
        Sprite sprite, string caption, string value)
    {
        var column = CreateRect(name, parent, new Vector2(x, 0f), new Vector2(220f, 136f));
        var iconRect = CreateRect("Icon", column, new Vector2(0f, 26f), new Vector2(52f, 52f));
        var icon = Undo.AddComponent<Image>(iconRect.gameObject);
        icon.sprite = sprite;
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        CreateText("Label", column, source, caption, new Vector2(0f, -5f), 19f);
        return CreateText("Value", column, source, value, new Vector2(0f, -39f), 28f);
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_Text source, string text, Vector2 position, float size)
    {
        var rect = CreateRect(name, parent, position, new Vector2(220f, 34f));
        var label = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
        label.font = source.font;
        label.fontSharedMaterial = source.fontSharedMaterial;
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
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
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void SetGroup(CanvasGroup group, float alpha)
    {
        Undo.RecordObject(group, UNDO_NAME);
        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.ignoreParentGroups = false;
    }
}
