using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>승인된 확장 프레임의 실제 분할 리소스와 글자 비례를 중앙 기록판에만 적용한다.</summary>
public static class LobbyDifficultyRecordReferencePolisher
{
    private const float PANEL_WIDTH = 560f;
    private const float REFERENCE_WIDTH = 524f;
    private const float COLLAPSED_HEIGHT = 128f;
    private const float EXPANDED_HEIGHT = 234f;
    private const float REVEAL_BASELINE = 104f;
    private const float START_BUTTON_GAP = 8f;
    private const string UNDO_NAME = "Polish lobby record to approved reference";
    private const string TROPHY_SPRITE = "Icon_Trophy_Centered_v2";
    private const string HOURGLASS_SPRITE = "Icon_Hourglass_Centered_v2";
    private const float ICON_SIZE = 32f;
    private static readonly string[] SPRITE_NAMES =
    {
        "Ref_Top", "Ref_Bottom", "Ref_LeftRail", "Ref_RightRail", TROPHY_SPRITE, HOURGLASS_SPRITE,
        "Ref_Divider", "Ref_VerticalDivider", "Ref_NameDivider"
    };

    [MenuItem("Tools/OZGL2/Lobby/Polish Difficulty Record To Approved Reference (Current Lobby Only)")]
    public static void ApplyMenu() => Apply();

    [MenuItem("Tools/OZGL2/Lobby/Replace Difficulty Record Icons Only (Current Lobby Only)")]
    public static void ApplyRecordIconsOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LobbyDifficultySelectionBuilder.SCENE_PATH)
            throw new InvalidOperationException("UI_Lobby_MutedPreview 씬에서만 실행하세요.");
        GameObject lobby = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas_Lobby");
        Transform content = lobby == null ? null : lobby.transform.Find(
            "StageSelection/CurrentStage/StageRecord/RecordPanelVisual/RecordReveal/RecordContent");
        Image trophy = Require<Image>(content, "HighestWave/Icon");
        Image hourglass = Require<Image>(content, "ClearTime/Icon");
        TMP_Text waveLabel = Require<TMP_Text>(content, "HighestWave/Label");
        TMP_Text timeLabel = Require<TMP_Text>(content, "ClearTime/Label");
        Sprite trophySprite = ImportSprite(TROPHY_SPRITE);
        Sprite timeSprite = ImportSprite(HOURGLASS_SPRITE);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Replace and center record icons");
        Undo.RecordObjects(new UnityEngine.Object[] { trophy, trophy.rectTransform, hourglass, hourglass.rectTransform },
            "Replace and center record icons");
        AlignRecordIcon(trophy, trophySprite, waveLabel);
        AlignRecordIcon(hourglass, timeSprite, timeLabel);
        EditorUtility.SetDirty(trophy);
        EditorUtility.SetDirty(hourglass);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Debug.Log("기록 아이콘 2개만 32×32/중앙 pivot/라벨 중심 높이로 교체했습니다. 다른 미저장 씬 변경은 유지합니다.");
    }

    private static void AlignRecordIcon(Image image, Sprite sprite, TMP_Text heading)
    {
        RectTransform rect = image.rectTransform;
        RectTransform parent = (RectTransform)rect.parent;
        Vector3 currentCenter = parent.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        Vector3 headingCenter = parent.InverseTransformPoint(heading.rectTransform.TransformPoint(heading.rectTransform.rect.center));
        ConfigureImage(image, sprite);
        image.overrideSprite = null;
        image.preserveAspect = true;
        SetRect(rect, Vector2.one * ICON_SIZE, new Vector2(currentCenter.x, headingCenter.y), parent.pivot,
            new Vector2(0.5f, 0.5f));
    }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LobbyDifficultySelectionBuilder.SCENE_PATH)
            throw new InvalidOperationException("UI_Lobby_MutedPreview 씬에서만 실행할 수 있습니다.");
        GameObject lobby = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas_Lobby");
        Transform stage = lobby == null ? null : lobby.transform.Find("StageSelection");
        Transform current = stage == null ? null : stage.Find("CurrentStage");
        if (current == null || !stage.TryGetComponent(out UILobbyDifficultySelector selector))
            throw new InvalidOperationException("중앙 난이도 선택 구성이 필요합니다.");

        RectTransform caption = Require<RectTransform>(current, "StageRecord");
        RectTransform panel = Require<RectTransform>(caption, "RecordPanelVisual");
        RectTransform hit = Require<RectTransform>(current, "RecordHoverHitArea");
        RectTransform start = Require<RectTransform>(current, "StartButton");
        UILobbyDifficultyRecordPanel controller = Require<UILobbyDifficultyRecordPanel>(current, "RecordHoverHitArea");
        if (!controller.TryValidate(out string reason))
            throw new InvalidOperationException("기록판 Inspector 연결을 확인하세요: " + reason);
        SerializedObject serialized = new SerializedObject(controller);
        if (serialized.FindProperty("_panelRect").objectReferenceValue != panel)
            throw new InvalidOperationException("컨트롤러의 Panel Rect가 대상 프레임과 다릅니다.");

        RectTransform reveal = Require<RectTransform>(panel, "RecordReveal");
        RectTransform content = Require<RectTransform>(reveal, "RecordContent");
        CanvasGroup recordGroup = Require<CanvasGroup>(reveal, "RecordContent");
        if (serialized.FindProperty("_revealRect").objectReferenceValue != reveal
            || serialized.FindProperty("_recordGroup").objectReferenceValue != recordGroup)
            throw new InvalidOperationException("컨트롤러의 기록 Reveal과 CanvasGroup 연결이 예상과 다릅니다.");
        Image top = Require<Image>(panel, "TopCap");
        Image bottom = Require<Image>(panel, "BottomCap");
        Image leftRail = Require<Image>(panel, "LeftRail");
        Image rightRail = Require<Image>(panel, "RightRail");
        Image oldBody = Require<Image>(panel, "Body");
        TMP_Text difficulty = Require<TMP_Text>(caption, "RecordText");
        TMP_Text description = Require<TMP_Text>(caption, "ClearTimeText");
        RectTransform waveColumn = Require<RectTransform>(content, "HighestWave");
        RectTransform timeColumn = Require<RectTransform>(content, "ClearTime");
        Image trophy = Require<Image>(waveColumn, "Icon");
        Image hourglass = Require<Image>(timeColumn, "Icon");
        TMP_Text waveHeading = Require<TMP_Text>(waveColumn, "Label");
        TMP_Text timeHeading = Require<TMP_Text>(timeColumn, "Label");
        TMP_Text waveValue = Require<TMP_Text>(waveColumn, "Value");
        TMP_Text timeValue = Require<TMP_Text>(timeColumn, "Value");
        Image divider = Require<Image>(content, "HorizontalDivider");
        Image verticalDivider = Require<Image>(content, "VerticalDivider");
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas == null ? null : canvas.rootCanvas.transform as RectTransform;
        if (canvasRect == null)
            throw new InvalidOperationException("기록판과 전투 준비 버튼의 Canvas 좌표를 확인하세요.");

        Dictionary<string, Sprite> sprites = SPRITE_NAMES.ToDictionary(name => name, ImportSprite);
        string characters = CollectCharacters(selector, caption);
        TMP_FontAsset smallFont = LobbyDifficultyRecordTypography.EnsureFont(characters);
        TMP_FontAsset emphasisFont = LobbyDifficultyRecordTypography.LoadEmphasisFont(characters);
        float scale = PANEL_WIDTH / REFERENCE_WIDTH;
        Vector3 desiredBottom = FindPanelBottom(canvasRect, panel, start);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        Undo.RegisterFullObjectHierarchyUndo(caption.gameObject, UNDO_NAME);
        Undo.RecordObjects(new UnityEngine.Object[] { hit, controller }, UNDO_NAME);

        panel.pivot = new Vector2(0.5f, 0f);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PANEL_WIDTH);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, COLLAPSED_HEIGHT * scale);
        panel.position = canvasRect.TransformPoint(desiredBottom);
        hit.pivot = new Vector2(0.5f, 0f);
        hit.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PANEL_WIDTH);
        hit.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, COLLAPSED_HEIGHT * scale);
        hit.position = panel.position;
        serialized.FindProperty("_collapsedHeight").floatValue = COLLAPSED_HEIGHT * scale;
        serialized.FindProperty("_expandedHeight").floatValue = EXPANDED_HEIGHT * scale;
        serialized.ApplyModifiedProperties();

        oldBody.enabled = false;
        UIRecordPanelBackground interior = GetOrCreateInterior(panel);
        SetStretch(interior.rectTransform);
        interior.color = Color.white;
        interior.raycastTarget = false;
        ConfigureImage(top, sprites["Ref_Top"]);
        ConfigureImage(bottom, sprites["Ref_Bottom"]);
        foreach (Transform child in top.transform)
            child.gameObject.SetActive(false); // 이전 모듈 조각은 삭제하지 않고 새 승인 프레임 아래에 보존한다.
        SetRect(top.rectTransform, new Vector2(PANEL_WIDTH, 70f * scale), Vector2.zero,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        SetRect(bottom.rectTransform, new Vector2(PANEL_WIDTH, 94f * scale), Vector2.zero,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        ConfigureImage(leftRail, sprites["Ref_LeftRail"]);
        ConfigureImage(rightRail, sprites["Ref_RightRail"]);
        leftRail.type = rightRail.type = Image.Type.Tiled;
        SetTileScale(leftRail, scale, canvas);
        SetTileScale(rightRail, scale, canvas);
        interior.Configure(leftRail, rightRail, scale);
        // 코드로 설정한 Graphic 참조도 명시적으로 직렬화해 Play Mode 진입 후 유지한다.
        SerializedObject interiorData = new SerializedObject(interior);
        interiorData.FindProperty("_leftRail").objectReferenceValue = leftRail;
        interiorData.FindProperty("_rightRail").objectReferenceValue = rightRail;
        interiorData.FindProperty("_artScale").floatValue = scale;
        interiorData.ApplyModifiedProperties();
        EditorUtility.SetDirty(interior);

        SetRect(reveal, new Vector2(PANEL_WIDTH, 0f), new Vector2(0f, REVEAL_BASELINE * scale),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        SetRect(content, new Vector2(PANEL_WIDTH, (EXPANDED_HEIGHT - COLLAPSED_HEIGHT) * scale), Vector2.zero,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        recordGroup.alpha = 0f;
        recordGroup.interactable = false;
        recordGroup.blocksRaycasts = false;

        Color labels = new Color32(211, 202, 185, 255);
        Color values = new Color32(244, 234, 212, 255);
        SetRect(waveColumn, new Vector2(238f, 106f) * scale, new Vector2(-95f * scale, 0f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        SetRect(timeColumn, new Vector2(238f, 106f) * scale, new Vector2(107f * scale, 0f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        ConfigureImage(trophy, sprites[TROPHY_SPRITE]);
        ConfigureImage(hourglass, sprites[HOURGLASS_SPRITE]);
        trophy.preserveAspect = hourglass.preserveAspect = true;
        SetRecordRect(trophy.rectTransform, new Vector2(-66f, 175f), Vector2.one * (ICON_SIZE / scale), scale);
        SetRecordRect(hourglass.rectTransform, new Vector2(-46f, 175f), Vector2.one * (ICON_SIZE / scale), scale);
        ConfigureText(waveHeading, smallFont, 17f * scale, labels);
        ConfigureText(timeHeading, smallFont, 17f * scale, labels);
        waveHeading.text = "최고 클리어 웨이브";
        timeHeading.text = "클리어 시간";
        SetRecordRect(waveHeading.rectTransform, new Vector2(25f, 175f), new Vector2(150f, 28f), scale);
        SetRecordRect(timeHeading.rectTransform, new Vector2(24f, 175f), new Vector2(100f, 28f), scale);
        ConfigureText(waveValue, emphasisFont, 24f * scale, values);
        ConfigureText(timeValue, emphasisFont, 24f * scale, values);
        SetRecordRect(waveValue.rectTransform, new Vector2(0f, 143f), new Vector2(185f, 36f), scale);
        SetRecordRect(timeValue.rectTransform, new Vector2(7f, 143f), new Vector2(165f, 36f), scale);
        ConfigureImage(divider, sprites["Ref_Divider"]);
        ConfigureImage(verticalDivider, sprites["Ref_VerticalDivider"]);
        SetRecordRect(divider.rectTransform, new Vector2(0f, 114f), new Vector2(380f, 16f), scale);
        SetRecordRect(verticalDivider.rectTransform, new Vector2(14f, 159f), new Vector2(8f, 68f), scale);

        Image nameDivider = GetOrCreateImage(panel, "NameDivider");
        ConfigureImage(nameDivider, sprites["Ref_NameDivider"]);
        SetRect(nameDivider.rectTransform, new Vector2(362f, 8f) * scale, new Vector2(0f, 66f * scale),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        ConfigureText(difficulty, emphasisFont, 26f * scale, values);
        ConfigureText(description, smallFont, 19f * scale, labels);
        PositionCaptionText(difficulty.rectTransform, panel, new Vector2(0f, 86f) * scale, new Vector2(360f, 38f) * scale);
        PositionCaptionText(description.rectTransform, panel, new Vector2(0f, 48f) * scale, new Vector2(420f, 32f) * scale);

        interior.transform.SetAsFirstSibling();
        leftRail.transform.SetAsLastSibling();
        rightRail.transform.SetAsLastSibling();
        top.transform.SetAsLastSibling();
        bottom.transform.SetAsLastSibling();
        nameDivider.transform.SetAsLastSibling();
        reveal.SetAsLastSibling();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = panel.gameObject;
        string summary = "승인 레퍼런스 기록 프레임 적용: 폭 560, 높이 "
            + (COLLAPSED_HEIGHT * scale).ToString("F2") + "→" + (EXPANDED_HEIGHT * scale).ToString("F2")
            + ", 전투 준비 위 간격 8, 제목/값 명조·작은 설명 Noto. 실제 기록·호버 로직·초상화·전투 준비는 유지. 씬 저장 전 검증 필요.";
        Debug.Log(summary, panel);
        return summary;
    }

    private static Vector3 FindPanelBottom(RectTransform canvas, RectTransform panel, RectTransform start)
    {
        Vector3[] corners = new Vector3[4];
        start.GetWorldCorners(corners);
        float top = float.NegativeInfinity;
        foreach (Vector3 corner in corners) top = Mathf.Max(top, canvas.InverseTransformPoint(corner).y);
        Vector3 result = canvas.InverseTransformPoint(panel.TransformPoint(Vector3.zero));
        result.y = top + START_BUTTON_GAP;
        return result;
    }

    private static string CollectCharacters(UILobbyDifficultySelector selector, Transform caption)
    {
        StringBuilder text = new StringBuilder("최고 클리어 웨이브 클리어 시간 기록 없음 웨이브 --:-- 0123456789");
        if (selector.Catalog != null)
            foreach (LobbyDifficultyCatalogSO.Entry entry in selector.Catalog.Entries)
                if (entry != null) text.Append(entry.DisplayName).Append(entry.Description);
        foreach (TMP_Text label in caption.GetComponentsInChildren<TMP_Text>(true)) text.Append(label.text);
        return text.ToString();
    }

    private static T Require<T>(Transform parent, string path) where T : Component
    {
        Transform child = parent == null ? null : parent.Find(path);
        T component = child == null ? null : child.GetComponent<T>();
        if (component == null) throw new InvalidOperationException("필요한 기록판 구성 없음: " + path + " / " + typeof(T).Name);
        return component;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, UNDO_NAME);
        created.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)created.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image GetOrCreateImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return Require<Image>(parent, name);
        return Undo.AddComponent<Image>(CreateRect(parent, name).gameObject);
    }

    private static UIRecordPanelBackground GetOrCreateInterior(Transform parent)
    {
        Transform existing = parent.Find("Interior");
        if (existing != null)
        {
            if (!existing.TryGetComponent<CanvasRenderer>(out _))
                Undo.AddComponent<CanvasRenderer>(existing.gameObject);
            return Require<UIRecordPanelBackground>(parent, "Interior");
        }
        return Undo.AddComponent<UIRecordPanelBackground>(CreateRect(parent, "Interior").gameObject);
    }

    private static void ConfigureImage(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = false;
        image.enabled = true;
    }

    private static void ConfigureText(TMP_Text text, TMP_FontAsset font, float size, Color color)
    {
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.fontSize = size;
        text.fontStyle = FontStyles.Normal;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.Center;
        text.margin = Vector4.zero;
        text.color = color;
        text.raycastTarget = false;
        text.richText = false;
    }

    private static void PositionCaptionText(RectTransform rect, RectTransform panel, Vector2 position, Vector2 size)
    {
        RectTransform parent = (RectTransform)rect.parent;
        Vector3 local = parent.InverseTransformPoint(panel.TransformPoint(position));
        SetRect(rect, size, new Vector2(local.x, local.y), parent.pivot, new Vector2(0.5f, 0.5f));
    }

    private static void SetRecordRect(RectTransform rect, Vector2 referencePosition, Vector2 size, float scale)
    {
        SetRect(rect, size * scale, new Vector2(referencePosition.x, referencePosition.y - REVEAL_BASELINE) * scale,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position, Vector2 anchor, Vector2 pivot)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void SetTileScale(Image image, float scale, Canvas canvas)
    {
        float referencePixels = canvas == null ? 100f : canvas.referencePixelsPerUnit;
        image.pixelsPerUnitMultiplier = referencePixels / (image.sprite.pixelsPerUnit * scale);
    }

    private static Sprite ImportSprite(string name)
    {
        string path = LobbyDifficultyRecordPanelBuilder.SPRITE_ROOT + "/" + name + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("승인 프레임의 분리 Sprite가 없습니다: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("승인 프레임 Sprite import 실패: " + path);
        return sprite;
    }
}
