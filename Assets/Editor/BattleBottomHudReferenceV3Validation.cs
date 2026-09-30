using System;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleBottomHudReferenceV3Validation
{
    private const string SCENE_PATH = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    private const string ASSET_ROOT =
        "Assets/06.UI/BattleMutedPreview/BottomHudReferenceV3/";
    private static readonly string[] LAYER_NAMES =
    {
        "Hud_Backplate",
        "Hud_GoldOrnament",
        "Panel_CurrentAmount",
        "Button_Reroll",
        "Button_BattleStart"
    };
    private static readonly string[] LAYER_PATHS =
    {
        ASSET_ROOT + "Hud_Backplate_Black.png",
        ASSET_ROOT + "Hud_GoldOrnament.png",
        ASSET_ROOT + "Panel_CurrentAmount_Frame.png",
        ASSET_ROOT + "Button_Reroll_Frame.png",
        ASSET_ROOT + "Button_BattleStart_Frame.png"
    };
    private static readonly Vector2[] LAYER_POSITIONS =
    {
        Vector2.zero,
        Vector2.zero,
        new Vector2(32.06642f, 40.57944f),
        new Vector2(68.23022f, 17.16842f),
        new Vector2(-79.01806f, 18.83582f)
    };
    private static readonly Vector2[] LAYER_SIZES =
    {
        new Vector2(1920f, 1080f),
        new Vector2(1920f, 1080f),
        new Vector2(1567.1144f, 817.8785f),
        new Vector2(1371.2806f, 772.6105f),
        new Vector2(1985.2642f, 1090.811f)
    };
    private const float EPSILON = 0.51f;
    private const float FLOAT_EPSILON = 0.001f;

    [MenuItem("Tools/OZGL2/Battle/Validate Bottom HUD Layered V4")]
    public static void Validate()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != SCENE_PATH)
            throw new InvalidOperationException("UI_Battle_MutedPreview 씬을 연 뒤 실행해 주세요.");

        Transform screens = FindRoot(scene, "UI_BattleScreens");
        RectTransform preparation = NeedRect(screens, "Canvas_Preparation");
        RectTransform background = NeedRect(preparation, "BottomNobleBackground");
        RectTransform currency = NeedRect(preparation, "Currency");
        RectTransform reroll = NeedRect(preparation, "Reroll_Placeholder");
        RectTransform price = NeedRect(preparation, "RerollPrice");
        RectTransform start = NeedRect(preparation, "StartCombatButton");
        RectTransform hand = NeedRect(preparation, "BattleCardHand_Preview");
        RectTransform handPanel = NeedRect(hand, "HandPanel");
        RectTransform handViewport = NeedRect(handPanel, "Viewport");

        CheckRect(background, Vector2.zero, new Vector2(1920f, 1080f));
        CheckRect(currency, new Vector2(48f, 71f), new Vector2(254f, 279f));
        CheckRect(reroll, new Vector2(238f, 73f), new Vector2(228f, 234f));
        CheckRect(price, new Vector2(264f, 104f), new Vector2(176f, 50f));
        CheckRect(start, new Vector2(1347f, 71f), new Vector2(526f, 233f));
        Check(start.anchorMin, Vector2.zero, "StartCombatButton anchorMin");
        Check(start.anchorMax, Vector2.zero, "StartCombatButton anchorMax");
        Check(start.pivot, Vector2.zero, "StartCombatButton pivot");

        if (background.Find("ReferenceFrame") != null ||
            background.Find("ReferenceOrnamentFrame") != null ||
            background.Find("ReferenceFills") != null ||
            start.Find("VisualsV2") != null ||
            start.Find("VisualsReferenceV3/Plate") != null)
            throw new InvalidOperationException("이전 통합형 HUD 시각 오브젝트가 남아 있습니다.");

        Image backgroundBody = Require<Image>(background);
        Transform layers = Need(background, "ReferenceLayersV4");
        if (layers.childCount != LAYER_NAMES.Length)
            throw new InvalidOperationException("분리형 HUD 레이어 수가 5개여야 합니다.");
        if (backgroundBody.raycastTarget)
            throw new InvalidOperationException("하단 배경은 Raycast Target이 꺼져 있어야 합니다.");

        for (int i = 0; i < LAYER_NAMES.Length; i++)
        {
            RectTransform layer = NeedRect(layers, LAYER_NAMES[i]);
            Image image = Require<Image>(layer);
            Sprite expectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LAYER_PATHS[i]);
            if (expectedSprite == null || image.sprite != expectedSprite)
                throw new InvalidOperationException(LAYER_NAMES[i] + " Sprite 연결이 올바르지 않습니다.");
            if (image.raycastTarget || layer.GetSiblingIndex() != i)
                throw new InvalidOperationException(
                    LAYER_NAMES[i] + "의 Raycast 또는 레이어 순서가 올바르지 않습니다.");
            CheckRect(layer, LAYER_POSITIONS[i], LAYER_SIZES[i]);
        }

        TMP_Text cost = Require<TMP_Text>(Need(currency, "Value_SDF"));
        TMP_Text rerollCost = Require<TMP_Text>(Need(price, "Value_SDF"));
        TMP_Text title = Require<TMP_Text>(Need(start, "VisualsReferenceV3/Title_SDF"));
        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont == null || cost.font != defaultFont ||
            rerollCost.font != defaultFont || title.font != defaultFont)
            throw new InvalidOperationException("하단 숫자와 버튼 라벨은 TMP 기본 SDF를 사용해야 합니다.");
        if (title.text != "BATTLE START" || title.textWrappingMode != TextWrappingModes.NoWrap)
            throw new InvalidOperationException("전투 시작 임시 라벨은 기본 SDF에서 한 줄로 표시되어야 합니다.");

        UIBattleMutedPreviewView previewView = Require<UIBattleMutedPreviewView>(screens);
        SerializedObject serializedPreview = new SerializedObject(previewView);
        SerializedProperty costReference = serializedPreview.FindProperty("_costTextSdf");
        SerializedProperty rerollReference = serializedPreview.FindProperty("_rerollCostTextSdf");
        if (costReference == null || costReference.objectReferenceValue != cost ||
            rerollReference == null || rerollReference.objectReferenceValue != rerollCost)
            throw new InvalidOperationException("UIBattleMutedPreviewView의 SDF 비용 Text 연결이 올바르지 않습니다.");

        UIBattleCardHandView handView = Require<UIBattleCardHandView>(hand);
        if (!hand.gameObject.activeInHierarchy || handPanel.rect.height + EPSILON < 640f ||
            handViewport.rect.height + EPSILON < 640f ||
            !handViewport.TryGetComponent(out RectMask2D _))
            throw new InvalidOperationException("손패 Viewport의 활성/높이/RectMask2D 구성이 올바르지 않습니다.");

        SerializedObject serializedHand = new SerializedObject(handView);
        CheckFloat(serializedHand, "_cardScale", 0.258f);
        CheckFloat(serializedHand, "_bottomPadding", 93f);
        CheckFloat(serializedHand, "_comfortableSpacing", -36f);
        CheckFloat(serializedHand, "_restingYOffset", -200f);
        CheckFloat(serializedHand, "_fanArcHeight", 0f);
        CheckFloat(serializedHand, "_maxFanAngle", 0f);
        CheckFloat(serializedHand, "_hoverScale", 1.728f);
        CheckFloat(serializedHand, "_hoverRise", 240f);
        CheckFloat(serializedHand, "_transitionDuration", 0.15f);

        ScrollRect scrollRect = Require<ScrollRect>(hand);
        AssertNotPrefabOverride(scrollRect, "m_Horizontal");
        if (scrollRect.content != null)
            AssertNotPrefabOverride(scrollRect.content, "m_SizeDelta");
        Scrollbar scrollbar = scrollRect.horizontalScrollbar;
        if (scrollbar != null)
        {
            AssertNotPrefabOverride(scrollbar, "m_Interactable");
            AssertNotPrefabOverride(scrollbar.gameObject, "m_IsActive");
            // Scrollbar가 활성화되면 Handle anchor는 DrivenRectTransformTracker가 즉시 계산한다.
            // 이 값은 Scene에 저장되지 않아도 SerializedProperty.prefabOverride가 true로 보일 수 있다.
            if (scrollbar.handleRect != null && scrollbar.handleRect.drivenByObject == null)
                AssertNotPrefabOverride(scrollbar.handleRect, "m_AnchorMax");
        }

        Transform choiceCards = Need(preparation, "ChoiceCards");
        foreach (Transform child in choiceCards)
            if (child.gameObject.activeSelf)
                throw new InvalidOperationException(
                    "정적 ChoiceCards/" + child.name + "는 동적 손패와 중복되므로 비활성이어야 합니다.");

        Button rerollButton = Require<Button>(reroll);
        Button startButton = Require<Button>(start);
        if (rerollButton.targetGraphic == null || startButton.targetGraphic == null ||
            !rerollButton.targetGraphic.raycastTarget || !startButton.targetGraphic.raycastTarget)
            throw new InvalidOperationException("Reroll/StartCombatButton의 클릭 Graphic이 유지되어야 합니다.");
        for (int i = 0; i < startButton.onClick.GetPersistentEventCount(); i++)
            if (startButton.onClick.GetPersistentTarget(i) == null ||
                string.IsNullOrEmpty(startButton.onClick.GetPersistentMethodName(i)))
                throw new InvalidOperationException("StartCombatButton의 기존 Persistent Event가 손상되었습니다.");

        foreach (string path in LAYER_PATHS)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite ||
                !importer.alphaIsTransparency || importer.mipmapEnabled ||
                importer.filterMode != FilterMode.Point ||
                importer.wrapMode != TextureWrapMode.Clamp)
                throw new InvalidOperationException(
                    path + "의 Sprite/투명도/Point/Clamp import 설정이 올바르지 않습니다.");
        }

        Debug.Log(
            "Bottom HUD Layered V4 검증 완료: 검은 배경/금속 장식/기능 프레임 5종, " +
            "1920×1080 정렬, 기본 SDF, " +
            "기존 버튼 클릭 영역, 직선형 축약 손패/호버 확장 튜닝, " +
            "계산형 Prefab override 정리가 정상입니다.");
    }

    private static void CheckFloat(SerializedObject serializedObject, string propertyPath, float expected)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property == null || Mathf.Abs(property.floatValue - expected) > FLOAT_EPSILON)
            throw new InvalidOperationException(propertyPath + " 값이 " + expected + "와 다릅니다.");
    }

    private static void AssertNotPrefabOverride(UnityEngine.Object target, string propertyPath)
    {
        if (target == null || !PrefabUtility.IsPartOfPrefabInstance(target)) return;
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyPath);
        if (property != null && property.prefabOverride)
            throw new InvalidOperationException(
                target.name + "." + propertyPath + " 계산값이 Scene override로 남아 있습니다.");
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        Transform result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != name) continue;
            if (result != null) throw new InvalidOperationException("중복 루트: " + name);
            result = root.transform;
        }
        return result != null ? result : throw new InvalidOperationException("필수 루트 누락: " + name);
    }

    private static Transform Need(Transform parent, string path)
    {
        foreach (string part in path.Split('/'))
        {
            Transform found = null;
            foreach (Transform child in parent)
            {
                if (child.name != part) continue;
                if (found != null) throw new InvalidOperationException("중복 경로: " + path);
                found = child;
            }
            if (found == null) throw new InvalidOperationException("필수 대상 누락: " + path);
            parent = found;
        }
        return parent;
    }

    private static RectTransform NeedRect(Transform parent, string path)
    {
        Transform target = Need(parent, path);
        if (target is RectTransform rect) return rect;
        throw new InvalidOperationException("RectTransform이 필요한 대상: " + path);
    }

    private static T Require<T>(Transform target) where T : Component
    {
        if (!target.TryGetComponent(out T component))
            throw new InvalidOperationException(
                "필수 컴포넌트 누락: " + target.name + "/" + typeof(T).Name);
        return component;
    }

    private static void CheckRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        Check(rect.anchoredPosition, position, rect.name + " 위치");
        Check(new Vector2(rect.rect.width, rect.rect.height), size, rect.name + " 크기");
    }

    private static void Check(Vector2 actual, Vector2 expected, string label)
    {
        if (Mathf.Abs(actual.x - expected.x) > EPSILON ||
            Mathf.Abs(actual.y - expected.y) > EPSILON)
            throw new InvalidOperationException(
                label + " 불일치: actual=" + actual + ", expected=" + expected);
    }
}
