using System;
using System.IO;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 하단 HUD 아트를 배경판/공통 장식/기능별 프레임으로 분리해 조립하고,
// 실제 버튼/카드 데이터는 기존 프리뷰 시스템을 그대로 사용한다.
public static class BattleBottomHudReferenceV3Builder
{
    private const string SCENE_PATH = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    private const string HUD_BACKPLATE_PATH =
        "Assets/06.UI/BattleMutedPreview/BottomHudReferenceV3/Hud_Backplate_Black.png";
    private const string HUD_ORNAMENT_PATH =
        "Assets/06.UI/BattleMutedPreview/BottomHudReferenceV3/Hud_GoldOrnament.png";
    private const string CURRENT_AMOUNT_FRAME_PATH =
        "Assets/06.UI/BattleMutedPreview/BottomHudReferenceV3/Panel_CurrentAmount_Frame.png";
    private const string REROLL_FRAME_PATH =
        "Assets/06.UI/BattleMutedPreview/BottomHudReferenceV3/Button_Reroll_Frame.png";
    private const string BATTLE_START_FRAME_PATH =
        "Assets/06.UI/BattleMutedPreview/BottomHudReferenceV3/Button_BattleStart_Frame.png";
    private const string ICON_FLAME_PATH =
        "Assets/06.UI/BattleMutedPreview/LowerLeftReference/Icon_Flame.png";
    private const string ICON_REROLL_PATH =
        "Assets/06.UI/BattleMutedPreview/LowerLeftReference/Icon_Reroll.png";
    private const string BATTLE_ICON_PATH =
        "Assets/06.UI/LobbyMutedPreview/Sprites/Button_BattleIcon_v2.png";
    private const string UNDO_NAME = "전투 하단 HUD 분리 리소스 적용";

    private const float HAND_PANEL_HEIGHT = 640f;
    private const float CARD_SCALE = 0.258f;
    private const float CARD_BOTTOM_PADDING = 93f;
    private const float CARD_COMFORTABLE_SPACING = -36f;

    private static readonly Color TRANSPARENT = new Color(1f, 1f, 1f, 0f);
    private static readonly Color LABEL_COLOR = new Color32(247, 243, 232, 255);

    [MenuItem("Tools/OZGL2/Battle/Apply Bottom HUD Layered V4")]
    public static void Apply()
    {
        Scene scene = ValidateScene();
        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont == null)
            throw new InvalidOperationException("TMP 기본 SDF Font Asset이 설정되어 있지 않습니다.");

        Sprite hudBackplate = LoadSprite(HUD_BACKPLATE_PATH);
        Sprite hudOrnament = LoadSprite(HUD_ORNAMENT_PATH);
        Sprite currentAmountFrame = LoadSprite(CURRENT_AMOUNT_FRAME_PATH);
        Sprite rerollFrame = LoadSprite(REROLL_FRAME_PATH);
        Sprite battleStartFrame = LoadSprite(BATTLE_START_FRAME_PATH);
        Sprite iconFlame = LoadSprite(ICON_FLAME_PATH);
        Sprite iconReroll = LoadSprite(ICON_REROLL_PATH);
        Sprite battleIcon = LoadSprite(BATTLE_ICON_PATH);

        Transform screens = FindRoot(scene, "UI_BattleScreens");
        RectTransform preparation = NeedRect(screens, "Canvas_Preparation");
        if (preparation.GetComponent<Canvas>() == null)
            throw new InvalidOperationException("Canvas_Preparation의 RectTransform과 Canvas가 필요합니다.");

        RectTransform background = NeedRect(preparation, "BottomNobleBackground");
        RectTransform currency = NeedRect(preparation, "Currency");
        RectTransform reroll = NeedRect(preparation, "Reroll_Placeholder");
        RectTransform rerollPrice = NeedRect(preparation, "RerollPrice");
        RectTransform rerollCostLegacy = NeedRect(preparation, "RerollCost");
        RectTransform start = NeedRect(preparation, "StartCombatButton");
        RectTransform hand = NeedRect(preparation, "BattleCardHand_Preview");
        RectTransform choiceCards = NeedRect(preparation, "ChoiceCards");
        UIBattleMutedPreviewView previewView = Require<UIBattleMutedPreviewView>(screens);
        UIBattleCardHandView handView = Require<UIBattleCardHandView>(hand);
        Button rerollButton = Require<Button>(reroll);
        Button startButton = Require<Button>(start);
        string rerollEventSignature = GetPersistentEventSignature(rerollButton);
        string startEventSignature = GetPersistentEventSignature(startButton);

        int undoGroup = -1;
        try
        {
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UNDO_NAME);

            RemoveLegacyV2Objects(background, start);
            ApplyBackground(
                background, hudBackplate, hudOrnament,
                currentAmountFrame, rerollFrame, battleStartFrame);
            TMP_Text costSdf = ApplyCurrency(currency, iconFlame, defaultFont);
            TMP_Text rerollSdf = ApplyReroll(
                reroll, rerollPrice, rerollCostLegacy, iconReroll, defaultFont);
            ApplyStartButton(start, battleIcon, defaultFont);
            ApplyHandAlignment(hand, handView);
            DisableStaticChoiceCards(choiceCards);
            RevertCalculatedHandOverrides(hand, handView);

            if (GetPersistentEventSignature(rerollButton) != rerollEventSignature ||
                GetPersistentEventSignature(startButton) != startEventSignature)
                throw new InvalidOperationException("기존 Reroll/StartCombatButton 이벤트 연결이 변경되었습니다.");

            Undo.RecordObject(previewView, UNDO_NAME);
            previewView.ConfigureSdfCostTexts(costSdf, rerollSdf);
            RecordPrefabOverride(previewView);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("UI_Battle_MutedPreview 씬을 저장하지 못했습니다.");

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "전투 하단 HUD 분리 리소스 적용 완료: 검은 배경판/공통 금속 장식/" +
                "현재량/리롤/전투 시작 프레임을 독립 Image로 구성하고, " +
                "기존 Cost/Reroll/Start 이벤트, 기존 동적 카드 손패를 유지했습니다. " +
                "원본 Prefab은 변경하지 않고 씬 Override만 저장했습니다.");
        }
        catch
        {
            if (undoGroup >= 0)
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(undoGroup);
            }
            throw;
        }
    }

    private static void RemoveLegacyV2Objects(RectTransform background, RectTransform start)
    {
        DestroyDirectChildIfPresent(background, "ReferenceFrame");
        DestroyDirectChildIfPresent(background, "ReferenceOrnamentFrame");
        DestroyDirectChildIfPresent(background, "ReferenceFills");
        DestroyDirectChildIfPresent(start, "VisualsV2");
        DestroyDirectChildIfPresent(start, "Title_SDF");
    }

    private static void ApplyBackground(
        RectTransform background,
        Sprite hudBackplate,
        Sprite hudOrnament,
        Sprite currentAmountFrame,
        Sprite rerollFrame,
        Sprite battleStartFrame)
    {
        Image body = Require<Image>(background);
        Record(background, body);
        SetBottomLeft(background, 0f, 0f, 1920f, 1080f);
        body.sprite = null;
        body.overrideSprite = null;
        body.color = TRANSPARENT;
        body.type = Image.Type.Simple;
        body.preserveAspect = false;
        body.raycastTarget = false;

        RectTransform layers = GetOrCreateRect(background, "ReferenceLayersV4");
        Stretch(layers);
        layers.SetAsLastSibling();

        Image backplateImage = ApplyFullScreenLayer(layers, "Hud_Backplate", hudBackplate, 0);
        Image ornamentImage = ApplyFullScreenLayer(layers, "Hud_GoldOrnament", hudOrnament, 1);
        Image currentFrameImage = ApplyFittedLayer(
            layers, "Panel_CurrentAmount_Frame", currentAmountFrame, 2,
            new Rect(17f, 35f, 271f, 321f), new Rect(48f, 71f, 254f, 279f));
        Image rerollFrameImage = ApplyFittedLayer(
            layers, "Button_Reroll_Frame", rerollFrame, 3,
            new Rect(207f, 68f, 278f, 285f), new Rect(238f, 73f, 228f, 234f));
        Image battleFrameImage = ApplyFittedLayer(
            layers, "Button_BattleStart_Frame", battleStartFrame, 4,
            new Rect(1201f, 45f, 443f, 201f), new Rect(1347f, 71f, 526f, 233f));

        Canvas canvas = Require<Canvas>(background);
        Record(canvas);
        canvas.overrideSorting = true;
        canvas.sortingOrder = -1;

        RecordOverrides(
            background, body, layers, backplateImage, ornamentImage,
            currentFrameImage, rerollFrameImage, battleFrameImage, canvas);
    }

    private static TMP_Text ApplyCurrency(
        RectTransform currency, Sprite iconSprite, TMP_FontAsset font)
    {
        RectTransform body = NeedRect(currency, "Body");
        RectTransform frame = NeedRect(currency, "Frame");
        RectTransform icon = NeedRect(currency, "Icon");
        RectTransform legacyValue = NeedRect(currency, "Value");
        Image iconImage = Require<Image>(icon);
        Text legacyText = Require<Text>(legacyValue);

        RevertVisualObject(body, false);
        RevertVisualObject(frame, false);
        RevertVisualObject(legacyValue, false);
        Record(currency, body, frame, icon, legacyValue, iconImage, legacyText);
        SetBottomLeft(currency, 48f, 71f, 254f, 279f);
        SetActive(body.gameObject, false);
        SetActive(frame.gameObject, false);
        SetActive(legacyValue.gameObject, false);
        SetBottomLeft(icon, 91f, 119f, 72f, 96f);
        SetSprite(iconImage, iconSprite, Image.Type.Simple, true, false);

        TextMeshProUGUI value = GetOrCreateSdfLabel(currency, "Value_SDF", font);
        PlaceLabel(value.rectTransform, 68f, 34f, 118f, 58f);
        ConfigureLabel(value, font, 42f, "100");
        RecordOverrides(currency, body, frame, icon, legacyValue, iconImage, legacyText, value);
        return value;
    }

    private static TMP_Text ApplyReroll(
        RectTransform reroll, RectTransform price, RectTransform legacyCost,
        Sprite rerollIcon, TMP_FontAsset font)
    {
        RectTransform body = NeedRect(reroll, "Body");
        RectTransform frame = NeedRect(reroll, "Frame");
        RectTransform icon = NeedRect(reroll, "Icon");
        Image bodyImage = Require<Image>(body);
        Image iconImage = Require<Image>(icon);
        Button button = Require<Button>(reroll);
        Text legacyCostText = Require<Text>(legacyCost);

        RectTransform priceFrame = NeedRect(price, "Frame");
        RectTransform priceBody = NeedRect(priceFrame, "Body");
        RectTransform priceBorder = NeedRect(priceFrame, "Border");
        RectTransform priceIcon = NeedRect(price, "CurrencyIcon");

        RevertVisualObject(body, false);
        RevertVisualObject(frame, false);
        RevertVisualObject(priceFrame, true);
        RevertVisualObject(priceIcon, false);
        RevertVisualObject(legacyCost, false);
        Record(reroll, body, frame, icon, bodyImage, iconImage, button,
            price, priceFrame, priceBody, priceBorder, priceIcon, legacyCost, legacyCostText);

        SetBottomLeft(reroll, 238f, 73f, 228f, 234f);
        Stretch(body);
        body.localRotation = Quaternion.identity;
        bodyImage.sprite = null;
        bodyImage.overrideSprite = null;
        bodyImage.color = TRANSPARENT;
        bodyImage.raycastTarget = true;
        SetActive(body.gameObject, true);
        SetActive(frame.gameObject, false);
        SetBottomLeft(icon, 76f, 91f, 76f, 66f);
        SetSprite(iconImage, rerollIcon, Image.Type.Simple, true, false);
        button.targetGraphic = bodyImage;

        SetBottomLeft(price, 264f, 104f, 176f, 50f);
        SetActive(priceFrame.gameObject, false);
        SetActive(priceIcon.gameObject, false);
        SetActive(legacyCost.gameObject, false);

        TextMeshProUGUI value = GetOrCreateSdfLabel(price, "Value_SDF", font);
        PlaceLabel(value.rectTransform, 49f, 1f, 122f, 48f);
        ConfigureLabel(value, font, 36f, "100");
        RecordOverrides(reroll, body, frame, icon, bodyImage, iconImage, button,
            price, priceFrame, priceBody, priceBorder, priceIcon,
            legacyCost, legacyCostText, value);
        return value;
    }

    private static void ApplyStartButton(
        RectTransform start, Sprite battleIcon, TMP_FontAsset font)
    {
        RectTransform body = NeedRect(start, "Body");
        RectTransform frame = NeedRect(start, "Frame");
        RectTransform legacyIcon = NeedRect(start, "Icon");
        RectTransform legacyTitle = NeedRect(start, "Title");
        Image bodyImage = Require<Image>(body);
        Text legacyTitleText = Require<Text>(legacyTitle);
        Button button = Require<Button>(start);

        RevertVisualObject(body, false);
        RevertVisualObject(frame, false);
        RevertVisualObject(legacyIcon, false);
        RevertVisualObject(legacyTitle, false);
        Record(start, body, frame, legacyIcon, legacyTitle, bodyImage, legacyTitleText, button);
        // 분리된 프레임 레이어와 동일한 1920x1080 좌하단 기준을 사용한다.
        // 우측 앵커를 유지하면 초광폭 화면에서 클릭 영역만 프레임 밖으로 이동한다.
        SetBottomLeft(start, 1347f, 71f, 526f, 233f);

        RectTransform visuals = GetOrCreateRect(start, "VisualsReferenceV3");
        Stretch(visuals);
        visuals.SetAsLastSibling();
        DestroyDirectChildIfPresent(visuals, "Plate");

        RectTransform icon = GetOrCreateRect(visuals, "Icon");
        Image iconImage = GetOrAddImage(icon);
        SetBottomLeft(icon, 50f, 9f, 215f, 215f);
        SetSprite(iconImage, battleIcon, Image.Type.Simple, true, false);

        Stretch(body);
        bodyImage.sprite = null;
        bodyImage.overrideSprite = null;
        bodyImage.color = TRANSPARENT;
        bodyImage.type = Image.Type.Simple;
        bodyImage.preserveAspect = false;
        bodyImage.raycastTarget = true;
        SetActive(body.gameObject, true);
        SetActive(frame.gameObject, false);
        SetActive(legacyIcon.gameObject, false);
        SetActive(legacyTitle.gameObject, false);

        TextMeshProUGUI title = GetOrCreateSdfLabel(visuals, "Title_SDF", font);
        PlaceLabel(title.rectTransform, 258f, 72f, 230f, 58f);
        ConfigureLabel(title, font, 32f, "BATTLE START");
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Overflow;
        title.rectTransform.SetAsLastSibling();
        button.targetGraphic = bodyImage;

        RecordOverrides(start, body, bodyImage, frame, legacyIcon, legacyTitle,
            legacyTitleText, button, visuals, icon, iconImage, title);
    }

    private static void ApplyHandAlignment(RectTransform hand, UIBattleCardHandView handView)
    {
        RectTransform handPanel = NeedRect(hand, "HandPanel");
        Record(hand, handPanel, handView);

        hand.anchorMin = hand.anchorMax = Vector2.zero;
        hand.pivot = new Vector2(0.5f, 0.5f);
        hand.anchoredPosition3D = new Vector3(960f, 540f, 0f);
        hand.sizeDelta = new Vector2(1920f, 1080f);
        hand.localScale = Vector3.one;
        hand.localRotation = Quaternion.identity;

        handPanel.anchorMin = handPanel.anchorMax = new Vector2(0.5f, 0f);
        handPanel.pivot = new Vector2(0.5f, 0f);
        handPanel.anchoredPosition3D = Vector3.zero;
        handPanel.sizeDelta = new Vector2(1080f, HAND_PANEL_HEIGHT);
        handPanel.localScale = Vector3.one;
        handPanel.localRotation = Quaternion.identity;

        SerializedObject serializedView = new SerializedObject(handView);
        SetFloat(serializedView, "_cardScale", CARD_SCALE);
        SetFloat(serializedView, "_bottomPadding", CARD_BOTTOM_PADDING);
        SetFloat(serializedView, "_comfortableSpacing", CARD_COMFORTABLE_SPACING);
        serializedView.ApplyModifiedProperties();

        RecordPrefabOverride(hand);
        RecordPrefabOverride(handPanel);
        RecordPrefabOverride(handView);
    }

    private static void RevertCalculatedHandOverrides(
        RectTransform hand, UIBattleCardHandView handView)
    {
        ScrollRect scrollRect = Require<ScrollRect>(hand);
        RevertPropertyOverride(scrollRect, "m_Horizontal");

        RectTransform content = scrollRect.content;
        if (content != null)
            RevertPropertyOverride(content, "m_SizeDelta");

        Scrollbar scrollbar = scrollRect.horizontalScrollbar;
        if (scrollbar != null)
        {
            RevertPropertyOverride(scrollbar, "m_Interactable");
            RevertPropertyOverride(scrollbar.gameObject, "m_IsActive");
            if (scrollbar.handleRect != null)
                RevertPropertyOverride(scrollbar.handleRect, "m_AnchorMax");
        }

        // Revert 과정에서 View 자체의 의도된 튜닝값은 유지한다.
        SerializedObject serializedView = new SerializedObject(handView);
        SetFloat(serializedView, "_cardScale", CARD_SCALE);
        SetFloat(serializedView, "_bottomPadding", CARD_BOTTOM_PADDING);
        SetFloat(serializedView, "_comfortableSpacing", CARD_COMFORTABLE_SPACING);
        serializedView.ApplyModifiedProperties();
        RecordPrefabOverride(handView);
    }

    private static void DisableStaticChoiceCards(RectTransform choiceCards)
    {
        foreach (Transform child in choiceCards)
            SetActive(child.gameObject, false);
    }

    private static string GetPersistentEventSignature(Button button)
    {
        System.Text.StringBuilder signature = new System.Text.StringBuilder();
        int count = button.onClick.GetPersistentEventCount();
        signature.Append(count);
        for (int i = 0; i < count; i++)
        {
            UnityEngine.Object target = button.onClick.GetPersistentTarget(i);
            signature.Append('|')
                .Append(target != null ? target.GetInstanceID() : 0)
                .Append(':')
                .Append(button.onClick.GetPersistentMethodName(i))
                .Append(':')
                .Append((int)button.onClick.GetPersistentListenerState(i));
        }
        return signature.ToString();
    }

    private static Image ApplyFullScreenLayer(
        RectTransform parent, string name, Sprite sprite, int siblingIndex)
    {
        RectTransform layer = GetOrCreateRect(parent, name);
        Image image = GetOrAddImage(layer);
        Stretch(layer);
        layer.SetSiblingIndex(siblingIndex);
        SetSprite(image, sprite, Image.Type.Simple, false, false);
        RecordOverrides(layer, image);
        return image;
    }

    private static Image ApplyFittedLayer(
        RectTransform parent,
        string name,
        Sprite sprite,
        int siblingIndex,
        Rect sourceAlphaBounds,
        Rect targetUiBounds)
    {
        RectTransform layer = GetOrCreateRect(parent, name);
        Image image = GetOrAddImage(layer);

        float layerWidth = targetUiBounds.width * sprite.rect.width / sourceAlphaBounds.width;
        float layerHeight = targetUiBounds.height * sprite.rect.height / sourceAlphaBounds.height;
        float layerX = targetUiBounds.x - sourceAlphaBounds.x * layerWidth / sprite.rect.width;
        float layerY = targetUiBounds.y - sourceAlphaBounds.y * layerHeight / sprite.rect.height;

        SetBottomLeft(layer, layerX, layerY, layerWidth, layerHeight);
        layer.SetSiblingIndex(siblingIndex);
        SetSprite(image, sprite, Image.Type.Simple, false, false);
        RecordOverrides(layer, image);
        return image;
    }

    private static void RevertPropertyOverride(UnityEngine.Object target, string propertyPath)
    {
        if (target == null || !PrefabUtility.IsPartOfPrefabInstance(target)) return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property == null || !property.prefabOverride) return;

        PrefabUtility.RevertPropertyOverride(property, InteractionMode.UserAction);
    }

    private static void RevertVisualObject(Transform target, bool includeChildren)
    {
        Transform[] targets = includeChildren
            ? target.GetComponentsInChildren<Transform>(true)
            : new[] { target };

        foreach (Transform current in targets)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(current)) continue;

            PrefabUtility.RevertObjectOverride(current.gameObject, InteractionMode.UserAction);
            Component[] components = current.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null || !PrefabUtility.IsPartOfPrefabInstance(component)) continue;
                PrefabUtility.RevertObjectOverride(component, InteractionMode.UserAction);
            }
        }
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyPath, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property == null)
            throw new InvalidOperationException(
                serializedObject.targetObject.name + "." + propertyPath + "을 찾지 못했습니다.");
        property.floatValue = value;
    }

    private static Scene ValidateScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded ||
            scene.path != SCENE_PATH || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("UI_Battle_MutedPreview 씬의 Edit Mode에서 실행해 주세요.");

        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("열린 씬의 미저장 변경을 먼저 보존해 주세요.");
        return scene;
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

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return sprite != null ? sprite : throw new FileNotFoundException("Sprite를 찾지 못했습니다.", path);
    }

    private static void DestroyDirectChildIfPresent(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing == null) return;
        if (existing.parent != parent)
            throw new InvalidOperationException("직접 자식이 아닌 정리 대상: " + name);
        Undo.DestroyObjectImmediate(existing.gameObject);
    }

    private static RectTransform GetOrCreateRect(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            if (existing.parent != parent || !(existing is RectTransform rect))
                throw new InvalidOperationException("예상하지 못한 기존 UI 대상: " + name);
            return rect;
        }

        GameObject created = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, UNDO_NAME);
        RectTransform result = (RectTransform)created.transform;
        result.SetParent(parent, false);
        return result;
    }

    private static Image GetOrAddImage(RectTransform target)
    {
        if (target.TryGetComponent(out Image image)) return image;
        if (!target.TryGetComponent(out CanvasRenderer _))
            Undo.AddComponent<CanvasRenderer>(target.gameObject);
        return Undo.AddComponent<Image>(target.gameObject);
    }

    private static TextMeshProUGUI GetOrCreateSdfLabel(
        RectTransform parent, string name, TMP_FontAsset font)
    {
        Transform existing = parent.Find(name);
        TextMeshProUGUI label;
        if (existing == null)
        {
            GameObject created = new GameObject(
                name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(created, UNDO_NAME);
            created.transform.SetParent(parent, false);
            label = created.GetComponent<TextMeshProUGUI>();
        }
        else if (existing.parent == parent && existing.TryGetComponent(out label))
        {
            Record(existing, label);
        }
        else
        {
            throw new InvalidOperationException("예상하지 못한 기존 SDF Label: " + name);
        }

        label.font = font;
        return label;
    }

    private static void ConfigureLabel(
        TextMeshProUGUI label, TMP_FontAsset font, float fontSize, string text)
    {
        Record(label);
        label.font = font;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.color = LABEL_COLOR;
        label.raycastTarget = false;
        label.text = text;
        RecordPrefabOverride(label);
    }

    private static void SetSprite(
        Image image, Sprite sprite, Image.Type type, bool preserveAspect, bool raycastTarget)
    {
        Record(image);
        image.overrideSprite = null;
        image.sprite = sprite;
        image.color = Color.white;
        image.type = type;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = raycastTarget;
        RecordPrefabOverride(image);
    }

    private static void SetBottomLeft(RectTransform rect, float x, float y, float width, float height)
    {
        Record(rect);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition3D = new Vector3(x, y, 0f);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        RecordPrefabOverride(rect);
    }

    private static void PlaceLabel(
        RectTransform rect, float x, float y, float width, float height)
    {
        SetBottomLeft(rect, x, y, width, height);
    }

    private static void SetCentered(
        RectTransform rect, float centerX, float centerY, float width, float height)
    {
        Record(rect);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = new Vector3(centerX, centerY, 0f);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        RecordPrefabOverride(rect);
    }

    private static void Stretch(RectTransform rect)
    {
        Record(rect);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        RecordPrefabOverride(rect);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target.activeSelf == active) return;
        Undo.RecordObject(target, UNDO_NAME);
        target.SetActive(active);
        RecordPrefabOverride(target);
    }

    private static void Record(params UnityEngine.Object[] targets)
    {
        Undo.RecordObjects(targets, UNDO_NAME);
    }

    private static void RecordOverrides(params UnityEngine.Object[] targets)
    {
        foreach (UnityEngine.Object target in targets) RecordPrefabOverride(target);
    }

    private static void RecordPrefabOverride(UnityEngine.Object target)
    {
        if (target == null) return;
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
