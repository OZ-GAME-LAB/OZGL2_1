using System;
using System.Collections.Generic;
using OZGL2.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>열린 시너지 설명 프리팹에만 편집 가능한 표시 구조를 적용합니다.</summary>
public static class SynergyTooltipLayoutBuilder
{
    private const string MENU_PATH = "Tools/OZGL2/Battle Tooltips/Apply Synergy Reference Layout";
    private const string UNDO_NAME = "시너지 설명창 레퍼런스 구조 적용";
    private const string GRADIENT_PATH = "Assets/06.UI/LobbyMutedPreview/Overlays/Sprites/SideFade.asset";

    private static readonly Color GOLD = new Color32(226, 190, 111, 255);
    private static readonly Color CREAM = new Color32(244, 227, 184, 255);
    private static readonly Color BODY = new Color32(245, 242, 232, 255);
    private static readonly Color RED = new Color32(183, 50, 56, 255);

    [MenuItem(MENU_PATH)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 완료된 Edit Mode에서 실행해 주세요.");

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null || stage.assetPath != HeraldryTooltipPrefabBuilder.SYNERGY_PREFAB)
            throw new InvalidOperationException("SynergyTooltipPanel_Heraldry를 Prefab Mode로 연 상태에서만 적용합니다.");

        GameObject root = stage.prefabContentsRoot;
        if (root == null || !root.TryGetComponent(out UIAdaptiveTooltipPanel panel) || panel.ContentRoot == null)
            throw new InvalidOperationException("시너지 패널과 ContentRoot 연결을 먼저 확인해 주세요.");
        if (panel.ContentRoot == root.transform || !panel.ContentRoot.IsChildOf(root.transform))
            throw new InvalidOperationException("ContentRoot는 시너지 패널 아래의 독립 오브젝트여야 합니다.");
        if (panel.TitleFont == null || panel.BodyFont == null)
            throw new InvalidOperationException("패널의 제목·본문 글꼴 연결이 필요합니다.");

        // 이미 완성된 구조를 다시 파괴·생성하지 않아 수동 조정과 Undo 참조를 보존합니다.
        UISynergyTooltipView existingView = root.GetComponent<UISynergyTooltipView>();
        if (existingView != null)
        {
            if (!existingView.IsConfigured)
                throw new InvalidOperationException("기존 표시 연결이 불완전합니다. Inspector에서 누락된 참조를 복구해 주세요. 자동 덮어쓰기는 하지 않습니다.");
            Debug.Log("시너지 설명창 구조가 이미 적용돼 있습니다. 글자·간격은 UISynergyTooltipView, 아래 여백은 UIAdaptiveTooltipPanel에서 조정해 주세요.", root);
            return;
        }

        Texture2D fade = AssetDatabase.LoadAssetAtPath<Texture2D>(GRADIENT_PATH);
        if (fade == null) throw new InvalidOperationException("기존 SideFade 그라데이션 텍스처 연결이 필요합니다.");

        RectTransform content = panel.ContentRoot;
        var replaceableNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Title", "Preview", "JobHeader", "HeaderDivider", "TierProgression", "ComboHeading", "ComboEntries"
        };
        for (int i = 0; i < content.childCount; i++)
            if (!replaceableNames.Contains(content.GetChild(i).name))
                throw new InvalidOperationException("알 수 없는 Content 자식을 보존하기 위해 중단했습니다: " + content.GetChild(i).name);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        try
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(content.GetChild(i).gameObject);

            UISynergyTooltipView view = Undo.AddComponent<UISynergyTooltipView>(root);
            Undo.RecordObject(view, UNDO_NAME);

            RectTransform header = Rect(content, "JobHeader", 32f, -50f, 536f, 104f);
            RawImage headerGradient = Gradient(header, "HeaderGradient", fade, new Color32(152, 72, 72, 140));
            Image icon = Picture(header, "JobIcon", Color.white);
            icon.preserveAspect = true;
            SetRect(icon.rectTransform, 0f, 0f, 104f, 104f);
            TMP_Text title = Label(header, "JobTitle", panel.TitleFont, 52f, CREAM);
            SetRect(title.rectTransform, 124f, 0f, 412f, 62f);
            TMP_Text count = Label(header, "DeploymentCount", panel.BodyFont, 26f, BODY);
            SetRect(count.rectTransform, 124f, -68f, 412f, 36f);

            RectTransform headerDivider = Rect(content, "HeaderDivider", 32f, -170f, 536f, 2f);
            Image redLine = Undo.AddComponent<Image>(headerDivider.gameObject);
            redLine.color = RED;
            redLine.raycastTarget = false;
            AddEndpointDiamonds(headerDivider);

            RectTransform tiers = Rect(content, "TierProgression", 32f, -190f, 536f, 138f);
            Image tierBackground = Undo.AddComponent<Image>(tiers.gameObject);
            tierBackground.color = new Color32(6, 6, 10, 170);
            tierBackground.raycastTarget = false;
            AddThinBorder(tiers);
            TMP_Text tier1Title = Label(tiers, "Tier1Title", panel.TitleFont, 36f, new Color32(156, 210, 255, 255));
            TMP_Text tier1Requirement = Label(tiers, "Tier1Requirement", panel.BodyFont, 26f, BODY);
            TMP_Text tier1Effect = Label(tiers, "Tier1Effect", panel.BodyFont, 26f, BODY);
            TMP_Text tier2Title = Label(tiers, "Tier2Title", panel.TitleFont, 36f, CREAM);
            TMP_Text tier2Requirement = Label(tiers, "Tier2Requirement", panel.BodyFont, 26f, BODY);
            TMP_Text tier2Effect = Label(tiers, "Tier2Effect", panel.BodyFont, 26f, BODY);
            TMP_Text arrow = Label(tiers, "Arrow", panel.TitleFont, 36f, GOLD);
            arrow.text = "›";
            arrow.alignment = TextAlignmentOptions.Center;
            ArrangeTier(tier1Title, tier1Requirement, tier1Effect, 12f);
            ArrangeTier(tier2Title, tier2Requirement, tier2Effect, 294f);
            SetRect(arrow.rectTransform, 248f, -42f, 40f, 48f);

            RectTransform comboHeading = Rect(content, "ComboHeading", 32f, -350f, 536f, 56f);
            RawImage comboGradient = Gradient(comboHeading, "GoldGradient", fade, new Color32(211, 168, 70, 125));
            TMP_Text comboTitle = Label(comboHeading, "SectionTitle", panel.TitleFont, 40f, CREAM);
            comboTitle.text = "조합 시너지";
            SetRect(comboTitle.rectTransform, 16f, -4f, 504f, 52f);

            RectTransform comboRoot = Rect(content, "ComboEntries", 32f, -430f, 536f, 0f);
            RectTransform entryTemplate = Rect(comboRoot, "EntryTemplate", 0f, 0f, 536f, 120f);
            TMP_Text name = Label(entryTemplate, "Name", panel.TitleFont, 30f, CREAM);
            TMP_Text condition = Label(entryTemplate, "Condition", panel.BodyFont, 26f, BODY);
            TMP_Text effect = Label(entryTemplate, "Effect", panel.BodyFont, 26f, BODY);
            SetRect(name.rectTransform, 12f, 0f, 512f, 40f);
            SetRect(condition.rectTransform, 12f, -46f, 512f, 34f);
            SetRect(effect.rectTransform, 12f, -82f, 512f, 34f);
            Image divider = Picture(entryTemplate, "Divider", new Color32(226, 190, 111, 150));
            divider.rectTransform.anchorMin = new Vector2(0f, 0f);
            divider.rectTransform.anchorMax = new Vector2(1f, 0f);
            divider.rectTransform.pivot = new Vector2(0.5f, 0f);
            divider.rectTransform.offsetMin = new Vector2(12f, 0f);
            divider.rectTransform.offsetMax = new Vector2(-12f, 1f);
            entryTemplate.gameObject.SetActive(false);

            var settings = new SerializedObject(view);
            SetReference(settings, "_panel", panel);
            SetReference(settings, "_header", header);
            SetReference(settings, "_headerGradient", headerGradient);
            SetReference(settings, "_icon", icon);
            SetReference(settings, "_title", title);
            SetReference(settings, "_count", count);
            SetReference(settings, "_headerDivider", headerDivider);
            SetReference(settings, "_tiers", tiers);
            SetReference(settings, "_tier1Title", tier1Title);
            SetReference(settings, "_tier1Requirement", tier1Requirement);
            SetReference(settings, "_tier1Effect", tier1Effect);
            SetReference(settings, "_tier2Title", tier2Title);
            SetReference(settings, "_tier2Requirement", tier2Requirement);
            SetReference(settings, "_tier2Effect", tier2Effect);
            SetReference(settings, "_arrow", arrow);
            SetReference(settings, "_comboHeading", comboHeading);
            SetReference(settings, "_comboGradient", comboGradient);
            SetReference(settings, "_comboTitle", comboTitle);
            SetReference(settings, "_comboRoot", comboRoot);
            SetReference(settings, "_entryTemplate", entryTemplate);
            settings.ApplyModifiedProperties();

            // 테두리와 장식은 그대로 두고, 그라데이션·내용 위에 그려지도록 순서만 맞춥니다.
            Undo.RecordObjects(new Object[] { content, root.transform }, UNDO_NAME);
            Transform frame = root.transform.Find("Frame");
            Transform ornament = root.transform.Find("HeaderOrnament");
            if (frame != null) { Undo.RecordObject(frame, UNDO_NAME); frame.SetAsLastSibling(); }
            if (ornament != null) { Undo.RecordObject(ornament, UNDO_NAME); ornament.SetAsLastSibling(); }
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("시너지 설명창 구조와 20개 표시 참조를 적용했습니다. Undo 지원. 미리보기 Render와 프리팹 저장은 별도로 진행해 주세요.", root);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        SetRect(rect, x, y, width, height);
        Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
        return rect;
    }

    private static void SetRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static Image Picture(Transform parent, string name, Color color)
    {
        RectTransform rect = Rect(parent, name, 0f, 0f, 1f, 1f);
        Image image = Undo.AddComponent<Image>(rect.gameObject);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RawImage Gradient(Transform parent, string name, Texture2D texture, Color color)
    {
        RectTransform rect = Rect(parent, name, 0f, 0f, 1f, 1f);
        RawImage image = Undo.AddComponent<RawImage>(rect.gameObject);
        image.texture = texture;
        image.color = color;
        image.raycastTarget = false;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return image;
    }

    private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size, Color color)
    {
        RectTransform rect = Rect(parent, name, 0f, 0f, 1f, 1f);
        TextMeshProUGUI label = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
        label.font = font;
        label.fontSize = size;
        label.color = color;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.richText = true;
        label.text = string.Empty;
        return label;
    }

    private static void ArrangeTier(TMP_Text title, TMP_Text requirement, TMP_Text effect, float x)
    {
        title.alignment = requirement.alignment = effect.alignment = TextAlignmentOptions.Top;
        SetRect(title.rectTransform, x, -12f, 230f, 44f);
        SetRect(requirement.rectTransform, x, -58f, 230f, 34f);
        SetRect(effect.rectTransform, x, -92f, 230f, 34f);
    }

    private static void AddThinBorder(RectTransform parent)
    {
        Image top = Picture(parent, "BorderTop", GOLD);
        Image bottom = Picture(parent, "BorderBottom", GOLD);
        Image left = Picture(parent, "BorderLeft", GOLD);
        Image right = Picture(parent, "BorderRight", GOLD);
        StretchEdge(top.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -1f), Vector2.zero);
        StretchEdge(bottom.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
        StretchEdge(left.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(1f, 0f));
        StretchEdge(right.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-1f, 0f), Vector2.zero);
    }

    private static void StretchEdge(RectTransform rect, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = insetMin;
        rect.offsetMax = insetMax;
    }

    private static void AddEndpointDiamonds(RectTransform parent)
    {
        // 별도 이미지 생성 없이 작은 금색 정사각형을 회전해 기존 테마의 끝장식을 표현합니다.
        Image left = Picture(parent, "LeftDiamond", GOLD);
        Image right = Picture(parent, "RightDiamond", GOLD);
        foreach (Image image in new[] { left, right })
        {
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(image == left ? 0f : 1f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.sizeDelta = new Vector2(8f, 8f);
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
    }

    private static void SetReference(SerializedObject settings, string name, Object value)
    {
        SerializedProperty property = settings.FindProperty(name);
        if (property == null) throw new InvalidOperationException("UISynergyTooltipView 표시 필드를 찾을 수 없습니다: " + name);
        property.objectReferenceValue = value;
    }
}
