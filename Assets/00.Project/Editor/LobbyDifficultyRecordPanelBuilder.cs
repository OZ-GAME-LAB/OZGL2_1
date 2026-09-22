using System;
using System.Collections.Generic;
using System.Linq;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>중앙 난이도 명패만 분리형 기록 프레임으로 구성한다. 원본 그림과 기존 문구 배치는 보존한다.</summary>
public static class LobbyDifficultyRecordPanelBuilder
{
    public const string SPRITE_ROOT = LobbyDifficultySelectionBuilder.ASSET_ROOT + "/RecordHover/Sprites";
    public const float PANEL_WIDTH = 510f;
    public const float COLLAPSED_HEIGHT = 120f;
    public const float EXPANDED_HEIGHT = 220f;
    private const float FRAME_SOURCE_WIDTH = 512f;
    private const float FRAME_TOP_HEIGHT = 60f;
    private const float FRAME_TOP_SOURCE_HEIGHT = 292f;
    private const float FRAME_TOP_SHOULDER_RAIL_OFFSET = 20f;
    private const float FRAME_RAIL_INSET = 16f;
    private const float FRAME_RAIL_WIDTH = 16f;
    private const float FRAME_BODY_INSET = 32f;
    private const float FRAME_SEAM_OVERLAP = 0.5f;
    private const float LABEL_BOTTOM_PADDING = 20f;
    private const string UNDO_NAME = "Configure lobby difficulty record hover";
    private const string PANEL_NAME = "RecordPanelVisual";
    private const string HIT_AREA_NAME = "RecordHoverHitArea";
    private const string APPLY_FRAME_UNDO_NAME = "Apply approved lobby record frame silhouette";
    private static readonly string[] FRAME_SPRITE_NAMES =
    {
        "Frame_Top", "Frame_Bottom", "Frame_LeftRail", "Frame_RightRail", "Frame_Body",
        "Frame_TopLeftShoulder", "Frame_TopRightShoulder", "Frame_TopStrip", "Frame_TopCrest"
    };
    private static readonly string[] SPRITE_NAMES =
    {
        "Frame_Top", "Frame_Bottom", "Frame_LeftRail", "Frame_RightRail",
        "Frame_Body", "Divider", "Icon_Trophy", "Icon_Hourglass",
        "Frame_TopLeftShoulder", "Frame_TopRightShoulder", "Frame_TopStrip", "Frame_TopCrest"
    };

    [MenuItem("Tools/OZGL2/Lobby/Configure Difficulty Record Hover (Current Lobby Only)")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LobbyDifficultySelectionBuilder.SCENE_PATH)
            throw new InvalidOperationException("UI_Lobby_MutedPreview 씬에서만 실행할 수 있습니다.");

        GameObject lobby = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas_Lobby");
        Transform stage = lobby == null ? null : lobby.transform.Find("StageSelection");
        Transform current = stage == null ? null : stage.Find("CurrentStage");
        RectTransform caption = current == null ? null : current.Find("StageRecord") as RectTransform;
        if (stage == null || current == null || caption == null
            || !stage.TryGetComponent(out UILobbyDifficultySelector selector)
            || !current.TryGetComponent(out UILobbyDifficultySlotView slot))
            throw new InvalidOperationException("StageSelection의 난이도 선택과 중앙 슬롯 구성이 먼저 필요합니다.");

        Transform existingHitArea = current.Find(HIT_AREA_NAME);
        if (existingHitArea != null && existingHitArea.TryGetComponent(out UILobbyDifficultyRecordPanel existing))
        {
            if (!existing.TryValidate(out string reason))
                throw new InvalidOperationException("기존 기록 프레임의 연결을 확인하세요: " + reason);
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("기록 프레임이 이미 구성되어 있습니다. 재실행으로 사용자의 배치·설정을 덮어쓰지 않았습니다.", existing);
            return;
        }
        if (existingHitArea != null || caption.Find(PANEL_NAME) != null)
            throw new InvalidOperationException("동일 이름의 부분 구성이 있습니다. Undo 또는 Inspector로 상태를 확인한 후 다시 실행하세요.");

        TMP_Text nameLabel = GetLabel(caption, "RecordText");
        TMP_Text descriptionLabel = GetLabel(caption, "ClearTimeText");
        if (!caption.TryGetComponent(out Image originalImage) || !caption.TryGetComponent(out CanvasGroup captionGroup)
            || nameLabel == null || descriptionLabel == null || descriptionLabel.font == null)
            throw new InvalidOperationException("StageRecord의 원본 Image, CanvasGroup, 기존 TMP 및 Font 참조가 필요합니다.");
        // 리소스 가져오기는 Unity Importer를 사용한다. 원본 PNG/.meta를 텍스트로 수정하지 않는다.
        Dictionary<string, Sprite> sprites = SPRITE_NAMES.ToDictionary(name => name, ImportSprite);
        ValidateFrameGeometry(sprites, COLLAPSED_HEIGHT);
        float bottomY = FindLabelBottom(caption, nameLabel.rectTransform, descriptionLabel.rectTransform) - LABEL_BOTTOM_PADDING;
        float centerX = caption.rect.center.x;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        Undo.RegisterFullObjectHierarchyUndo(current.gameObject, UNDO_NAME);

        // StageRecord와 두 기존 TMP의 RectTransform은 건드리지 않아 페이드 및 아래 문구의 위치를 그대로 유지한다.
        originalImage.enabled = false;
        RectTransform panel = CreateRect(PANEL_NAME, caption);
        SetFixedRect(panel, new Vector2(PANEL_WIDTH, COLLAPSED_HEIGHT), new Vector2(centerX, bottomY),
            caption.pivot, new Vector2(0.5f, 0f));
        panel.SetAsFirstSibling();
        CreateFrame(panel, sprites);

        RectTransform reveal = CreateRect("RecordReveal", panel);
        SetFixedRect(reveal, new Vector2(PANEL_WIDTH - 36f, 0f), new Vector2(0f, COLLAPSED_HEIGHT),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        Undo.AddComponent<RectMask2D>(reveal.gameObject);
        RectTransform content = CreateRect("RecordContent", reveal);
        SetFixedRect(content, new Vector2(PANEL_WIDTH - 36f, EXPANDED_HEIGHT - COLLAPSED_HEIGHT), Vector2.zero,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        CanvasGroup recordGroup = Undo.AddComponent<CanvasGroup>(content.gameObject);
        recordGroup.alpha = 0f;
        recordGroup.interactable = false;
        recordGroup.blocksRaycasts = false;

        TMP_Text wave = CreateRecordColumn(content, "HighestWave", -115f, "최고 클리어 웨이브", "기록 없음",
            sprites["Icon_Trophy"], descriptionLabel);
        TMP_Text time = CreateRecordColumn(content, "ClearTime", 115f, "클리어 시간", "--:--",
            sprites["Icon_Hourglass"], descriptionLabel);
        Image horizontalDivider = CreateImage("HorizontalDivider", content, sprites["Divider"]);
        SetFixedRect(horizontalDivider.rectTransform, new Vector2(PANEL_WIDTH - 70f, 20f), new Vector2(0f, 5f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        Image verticalDivider = CreateImage("VerticalDivider", content, null);
        verticalDivider.color = new Color32(121, 96, 69, 255);
        SetFixedRect(verticalDivider.rectTransform, new Vector2(1.5f, 52f), new Vector2(0f, 44f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

        // 표시 전용 CanvasGroup 밖으로 입력 면을 분리한다. StartButton과 부모만 공유하고 버튼 본체는 변경하지 않는다.
        RectTransform hitArea = CreateRect(HIT_AREA_NAME, current);
        RectTransform currentRect = (RectTransform)current;
        Vector3 localBottom = current.InverseTransformPoint(panel.TransformPoint(Vector3.zero));
        SetFixedRect(hitArea, new Vector2(PANEL_WIDTH, COLLAPSED_HEIGHT), new Vector2(localBottom.x, localBottom.y),
            currentRect.pivot, new Vector2(0.5f, 0f));
        hitArea.SetSiblingIndex(caption.GetSiblingIndex() + 1);
        Image hitImage = Undo.AddComponent<Image>(hitArea.gameObject);
        hitImage.color = Color.clear;
        hitImage.raycastTarget = true;
        UILobbyDifficultyRecordPanel controller = Undo.AddComponent<UILobbyDifficultyRecordPanel>(hitArea.gameObject);
        SerializedObject serialized = new SerializedObject(controller);
        SetReference(serialized, "_selector", selector);
        SetReference(serialized, "_panelRect", panel);
        SetReference(serialized, "_revealRect", reveal);
        SetReference(serialized, "_recordGroup", recordGroup);
        SetReference(serialized, "_waveValueLabel", wave);
        SetReference(serialized, "_timeValueLabel", time);
        serialized.FindProperty("_collapsedHeight").floatValue = COLLAPSED_HEIGHT;
        serialized.FindProperty("_expandedHeight").floatValue = EXPANDED_HEIGHT;
        serialized.ApplyModifiedProperties();

        SerializedObject slotObject = new SerializedObject(slot);
        SetReference(slotObject, "_recordPanel", controller);
        slotObject.ApplyModifiedProperties();
        controller.Bind(selector.SelectedEntry);
        controller.RefreshLayout();
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = hitArea.gameObject;
        Debug.Log("중앙 StageRecord의 분리형 프레임/기록 Reveal/별도 HitArea를 구성했습니다. 기존 문구·전투 준비 버튼·좌우 카드는 유지했습니다. 검증 후 씬을 저장하세요.", controller);
        LobbyDifficultyRecordReferencePolisher.Apply();
        Undo.CollapseUndoOperations(undoGroup);
    }

    [MenuItem("Tools/OZGL2/Lobby/Apply Approved Difficulty Record Frame (Current Lobby Only)")]
    public static void ApplyApprovedFrameSilhouette()
    {
        LobbyDifficultyRecordReferencePolisher.Apply();
    }

    private static void CreateFrame(RectTransform panel, IDictionary<string, Sprite> sprites)
    {
        Image body = CreateImage("Body", panel, sprites["Frame_Body"]);
        Image leftRail = CreateImage("LeftRail", panel, sprites["Frame_LeftRail"]);
        Image rightRail = CreateImage("RightRail", panel, sprites["Frame_RightRail"]);
        Image bottom = CreateImage("BottomCap", panel, sprites["Frame_Bottom"]);
        Image top = CreateImage("TopCap", panel, sprites["Frame_Top"]);
        LayoutFrame(panel, top, bottom, leftRail, rightRail, body, sprites);
    }

    private static void ValidateFrameGeometry(IDictionary<string, Sprite> sprites, float collapsedHeight, float width = PANEL_WIDTH)
    {
        Sprite bottom = sprites["Frame_Bottom"];
        float capHeight = FRAME_TOP_HEIGHT + width * bottom.rect.height / bottom.rect.width;
        if (capHeight > collapsedHeight + 0.01f)
            throw new InvalidOperationException("상하 캡 높이 합계가 축소 높이보다 큽니다. 장식을 겹치거나 압축하지 않도록 분리 리소스 규격을 확인하세요.");
    }

    private static void LayoutFrame(RectTransform panel, Image top, Image bottom, Image leftRail, Image rightRail, Image body,
        IDictionary<string, Sprite> sprites)
    {
        float width = panel.rect.width;
        float sourceScale = width / FRAME_SOURCE_WIDTH;
        float seamOverlap = FRAME_SEAM_OVERLAP * sourceScale;
        float topHeight = FRAME_TOP_HEIGHT;
        float bottomHeight = width * bottom.sprite.rect.height / bottom.sprite.rect.width;
        foreach (bool isTop in new[] { false, true })
        {
            Image cap = isTop ? top : bottom;
            Vector2 anchor = new Vector2(0.5f, isTop ? 1f : 0f);
            SetFixedRect(cap.rectTransform, new Vector2(width, isTop ? topHeight : bottomHeight), Vector2.zero, anchor, anchor);
            cap.type = Image.Type.Simple;
            cap.preserveAspect = false;
            cap.color = Color.white;
            cap.raycastTarget = false;
            cap.enabled = !isTop;
        }
        // 기존 통 상단 Sprite는 비활성 Image에 보존하고, 새 상단은 비율을 유지한 조각으로 조립한다.
        LayoutTopCap(top.rectTransform, sprites, width, sourceScale);

        // 모따기와 바깥 마름모는 캡 자체에 포함된다. 직사각형 배경이 실루엣 밖으로 새지 않게 가운데만 채운다.
        // 반 픽셀만 캡과 레일 아래로 겹쳐 Canvas 배율에 따른 가느다란 배경 틈을 막는다.
        SetStretch(body.rectTransform, new Vector2(FRAME_BODY_INSET * sourceScale - seamOverlap, bottomHeight - seamOverlap),
            new Vector2(-FRAME_BODY_INSET * sourceScale + seamOverlap, -topHeight + seamOverlap));
        body.type = Image.Type.Tiled;
        body.color = Color.white;
        body.raycastTarget = false;
        foreach (bool isRight in new[] { false, true })
        {
            Image rail = isRight ? rightRail : leftRail;
            float railWidth = FRAME_RAIL_WIDTH * sourceScale;
            RectTransform rect = rail.rectTransform;
            rect.anchorMin = new Vector2(isRight ? 1f : 0f, 0f);
            rect.anchorMax = new Vector2(isRight ? 1f : 0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(railWidth, -(topHeight + bottomHeight) + 2f * seamOverlap);
            rect.anchoredPosition = new Vector2((isRight ? -1f : 1f) * (FRAME_RAIL_INSET * sourceScale + railWidth * 0.5f),
                (bottomHeight - topHeight) * 0.5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rail.type = Image.Type.Tiled;
            rail.color = Color.white;
            rail.raycastTarget = false;
            Canvas canvas = rail.GetComponentInParent<Canvas>();
            float canvasPixelsPerUnit = canvas == null ? 100f : canvas.referencePixelsPerUnit;
            float spritePixelsPerUnit = rail.sprite.pixelsPerUnit / canvasPixelsPerUnit;
            rail.pixelsPerUnitMultiplier = rail.sprite.rect.width / (railWidth * spritePixelsPerUnit);
        }
    }

    private static void LayoutTopCap(RectTransform parent, IDictionary<string, Sprite> sprites, float width, float frameScale)
    {
        float pieceScale = FRAME_TOP_HEIGHT / FRAME_TOP_SOURCE_HEIGHT;
        float shoulderWidth = sprites["Frame_TopLeftShoulder"].rect.width * pieceScale;
        float shoulderInset = FRAME_RAIL_INSET * frameScale - FRAME_TOP_SHOULDER_RAIL_OFFSET * pieceScale;
        float stripInset = shoulderInset + shoulderWidth;
        Image strip = GetTopPiece(parent, "TopStrip", sprites["Frame_TopStrip"]);
        SetFixedRect(strip.rectTransform, new Vector2(width - 2f * stripInset, FRAME_TOP_HEIGHT), Vector2.zero,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        foreach (bool isRight in new[] { false, true })
        {
            Sprite sprite = sprites[isRight ? "Frame_TopRightShoulder" : "Frame_TopLeftShoulder"];
            Image shoulder = GetTopPiece(parent, isRight ? "TopRightShoulder" : "TopLeftShoulder", sprite);
            Vector2 anchor = new Vector2(isRight ? 1f : 0f, 1f);
            SetFixedRect(shoulder.rectTransform, sprite.rect.size * pieceScale,
                new Vector2(isRight ? -shoulderInset : shoulderInset, 0f), anchor, anchor);
        }
        Sprite crestSprite = sprites["Frame_TopCrest"];
        Image crest = GetTopPiece(parent, "CenterCrest", crestSprite);
        SetFixedRect(crest.rectTransform, crestSprite.rect.size * pieceScale, Vector2.zero,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
    }

    private static Image GetTopPiece(RectTransform parent, string name, Sprite sprite)
    {
        Transform child = parent.Find(name);
        Image image;
        if (child == null)
            image = CreateImage(name, parent, sprite);
        else
        {
            if (!child.TryGetComponent(out image))
                throw new InvalidOperationException("상단 조각에 Image가 없습니다: " + name);
            Undo.RecordObjects(new UnityEngine.Object[] { image, image.rectTransform }, APPLY_FRAME_UNDO_NAME);
        }
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = false;
        image.enabled = true;
        image.rectTransform.SetAsLastSibling();
        return image;
    }

    private static TMP_Text CreateRecordColumn(RectTransform parent, string objectName, float x, string label, string value,
        Sprite iconSprite, TMP_Text style)
    {
        RectTransform column = CreateRect(objectName, parent);
        SetFixedRect(column, new Vector2(222f, EXPANDED_HEIGHT - COLLAPSED_HEIGHT), new Vector2(x, 0f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        Image icon = CreateImage("Icon", column, iconSprite);
        SetFixedRect(icon.rectTransform, new Vector2(20f, 20f), new Vector2(-91f, 59f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        icon.preserveAspect = true;
        CreateText("Label", column, label, style, 18f, new Vector2(180f, 24f), new Vector2(13f, 59f),
            new Color32(205, 192, 171, 255));
        return CreateText("Value", column, value, style, 22f, new Vector2(212f, 32f), new Vector2(0f, 30f),
            new Color32(231, 217, 188, 255));
    }

    private static TMP_Text CreateText(string name, RectTransform parent, string content, TMP_Text style,
        float size, Vector2 dimensions, Vector2 position, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        SetFixedRect(rect, dimensions, position, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
        text.font = style.font;
        // 기존 외곽선 재질에는 TMP 마스킹용 _CullMode가 없으므로 새 기록 글자만 기본 Bitmap 재질을 쓴다.
        text.fontSharedMaterial = style.font.material;
        text.fontSize = size;
        text.fontStyle = style.fontStyle;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        text.richText = false;
        text.text = content;
        return text;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = Undo.AddComponent<Image>(rect.gameObject);
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, UNDO_NAME);
        created.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)created.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void SetFixedRect(RectTransform rect, Vector2 size, Vector2 position, Vector2 anchor, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = minimum;
        rect.offsetMax = maximum;
    }

    private static float FindLabelBottom(RectTransform parent, params RectTransform[] labels)
    {
        float minimum = float.PositiveInfinity;
        Vector3[] corners = new Vector3[4];
        foreach (RectTransform label in labels)
        {
            label.GetWorldCorners(corners);
            foreach (Vector3 corner in corners) minimum = Mathf.Min(minimum, parent.InverseTransformPoint(corner).y);
        }
        return minimum;
    }

    private static TMP_Text GetLabel(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child == null ? null : child.GetComponent<TMP_Text>();
    }

    private static void SetReference(SerializedObject target, string field, UnityEngine.Object reference)
    {
        SerializedProperty property = target.FindProperty(field);
        if (property == null) throw new InvalidOperationException("필요한 직렬화 필드가 없습니다: " + field);
        property.objectReferenceValue = reference;
    }

    private static string GetSpritePath(string name) => SPRITE_ROOT + "/" + name + ".png";

    private static Sprite ImportSprite(string name)
    {
        string path = GetSpritePath(name);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("TextureImporter가 없습니다: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (result == null) throw new InvalidOperationException("Sprite Import에 실패했습니다: " + path);
        return result;
    }
}
