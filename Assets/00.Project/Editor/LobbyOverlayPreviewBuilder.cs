using System;
using System.Collections.Generic;
using System.IO;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 외부 원본과 기존 특성 시스템은 수정하지 않고 시안 전용 Canvas/Prefab을 조립한다.
internal static class LobbyOverlayPreviewBuilder
{
    private const string ROOT = "Assets/06.UI/LobbyMutedPreview/Overlays";
    private const string BASE = "Assets/06.UI/LobbyMutedPreview";
    private const string FONT_FOLDER = "Assets/98.ExternalAssets/00.LocalStaging/01.Font";
    private const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private const string CANVAS_NAME = "Canvas_LobbyOverlays";
    private static readonly string[] NAMES = { "Panel_Frame", "Button_Frame", "Header_Ornament", "Points_Frame", "Trait_Frame_Normal", "Trait_Frame_Specialized" };
    private static readonly Color TEXT = new Color32(248, 242, 235, 255);
    private static readonly Color RED = new Color32(125, 57, 63, 255);
    private static TMP_FontAsset _font;
    private static Material _fontMaterial;

    [MenuItem("Tools/OZGL2/Lobby/Build Menu And Traits Overlay Preview")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != SCENE || Application.isPlaying)
            throw new InvalidOperationException("편집 모드의 UI_Lobby_MutedPreview 씬에서만 실행하세요.");
        if (GameObject.Find(CANVAS_NAME) != null)
            throw new InvalidOperationException("이미 Canvas가 있습니다. 중복 생성하지 않습니다. 기존 Prefab에서 수정하세요.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "/Prefabs/" + CANVAS_NAME + ".prefab") != null)
            throw new InvalidOperationException("기존 Overlay Prefab을 덮어쓰지 않습니다. 기존 Prefab을 씬에 배치하세요.");
        var lobby = GameObject.Find("Canvas_Lobby");
        var popups = GameObject.Find("Canvas_Popups");
        var uiRoot = GameObject.Find("UI_Root");
        if (lobby == null || popups == null || uiRoot == null) throw new InvalidOperationException("기존 로비 UI 연결을 찾지 못했습니다.");
        EnsureFolder(ROOT);
        EnsureFolder(ROOT + "/Sprites");
        EnsureFolder(ROOT + "/Fonts");
        EnsureFolder(ROOT + "/Prefabs");
        foreach (string name in NAMES) ImportSprite(name);
        PrepareFont();
        CreateGradient();

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build lobby menu and traits preview");
        RectTransform root = Rect(CANVAS_NAME, null, Vector2.zero, new Vector2(1920, 1080));
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Create overlay Canvas");
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0;
        root.gameObject.AddComponent<GraphicRaycaster>();
        UILobbyOverlayView host = root.gameObject.AddComponent<UILobbyOverlayView>();

        GameObject normal = CreateFramePrefab(false);
        GameObject special = CreateFramePrefab(true);
        UITraitOverlayView traits = BuildTraits(root, host, normal, special, out Button traitsFirst);
        GameObject menu = BuildMenu(root, host, out Button menuFirst, out Toggle bgmMenu, out Toggle sfxMenu, out TMP_Text bgmMenuState, out TMP_Text sfxMenuState);
        GameObject settings = BuildSettings(root, host, out Button settingsFirst, out Toggle bgmSettings, out Toggle sfxSettings, out TMP_Text bgmSettingsState, out TMP_Text sfxSettingsState);
        Assign(host, "_menuPanel", menu);
        Assign(host, "_settingsPanel", settings);
        Assign(host, "_traitsScreen", traits);
        Assign(host, "_menuFirst", menuFirst);
        Assign(host, "_settingsFirst", settingsFirst);
        Assign(host, "_traitsFirst", traitsFirst);
        AssignArray(host, "_bgmToggles", bgmMenu, bgmSettings);
        AssignArray(host, "_sfxToggles", sfxMenu, sfxSettings);
        AssignArray(host, "_bgmStateLabels", bgmMenuState, bgmSettingsState);
        AssignArray(host, "_sfxStateLabels", sfxMenuState, sfxSettingsState);
        menu.SetActive(false);
        settings.SetActive(false);
        traits.gameObject.SetActive(false);

        // Prefab 내부 참조를 먼저 저장하고 씬 객체 참조는 인스턴스 override로만 연결한다.
        PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, ROOT + "/Prefabs/" + CANVAS_NAME + ".prefab", InteractionMode.AutomatedAction);
        Assign(host, "_lobbyGroup", lobby.GetComponent<CanvasGroup>());
        Assign(host, "_legacyPopupController", uiRoot.GetComponent<UIPopupController>());
        Assign(host, "_legacyRaycaster", popups.GetComponent<GraphicRaycaster>());
        PrefabUtility.RecordPrefabInstancePropertyModifications(host);

        foreach (Button button in lobby.GetComponentsInChildren<Button>(true))
        {
            if (button.name != "SettingsButton" && button.name != "TraitsButton") continue;
            Undo.RecordObject(button, "Connect new overlay button");
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                if (button.onClick.GetPersistentTarget(i) == uiRoot.GetComponent<UIPopupController>() &&
                    button.onClick.GetPersistentMethodName(i) == "OpenPopup")
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
            if (button.name == "SettingsButton") UnityEventTools.AddPersistentListener(button.onClick, host.OpenMenu);
            else UnityEventTools.AddPersistentListener(button.onClick, host.OpenTraits);
            EditorUtility.SetDirty(button);
        }
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Overlay 생성: Canvas_LobbyOverlays, 메뉴/설정/특성 화면, 일반/특화 프레임 Prefab 2종. 씬 저장 전 검토하세요.");
    }

    private static GameObject BuildMenu(Transform root, UILobbyOverlayView host, out Button first, out Toggle bgm, out Toggle sfx, out TMP_Text bgmState, out TMP_Text sfxState)
    {
        RectTransform layer = Rect("MenuPopup", root, Vector2.zero, Vector2.zero);
        Stretch(layer);
        Solid("Dimmer", layer, new Color(0.01f, 0.01f, 0.015f, 0.78f), true);
        Image panel = Panel("Panel", layer, Vector2.zero, new Vector2(620, 640));
        Picture("Crest", panel.transform, "Header_Ornament", new Vector2(0, 305), new Vector2(430, 126));
        Label("Title", panel.transform, "메뉴", new Vector2(0, 206), new Vector2(440, 76), 58);
        Divider(panel.transform, 155, 220);
        first = Button("Resume", panel.transform, "돌아가기", new Vector2(-62, 70), new Vector2(330, 88), 36);
        UnityEventTools.AddPersistentListener(first.onClick, host.CloseTop);
        Button settings = Button("Settings", panel.transform, "설정", new Vector2(-62, -54), new Vector2(330, 88), 36);
        UnityEventTools.AddPersistentListener(settings.onClick, host.OpenSettings);
        Button quit = Button("Quit", panel.transform, "게임 종료", new Vector2(-62, -178), new Vector2(330, 88), 36);
        UnityEventTools.AddPersistentListener(quit.onClick, host.RequestQuit);
        quit.interactable = false;
        bgm = AudioToggle(panel.transform, "BGM", "배경음악", new Vector2(204, 71), host.SetBgmEnabled, out bgmState);
        sfx = AudioToggle(panel.transform, "SFX", "효과음", new Vector2(204, -54), host.SetSfxEnabled, out sfxState);
        Label("PreviewNote", panel.transform, "음향 / 종료 기능 연결 예정", new Vector2(0, -272), new Vector2(490, 28), 18).color = new Color32(156, 147, 145, 255);
        return layer.gameObject;
    }

    private static GameObject BuildSettings(Transform root, UILobbyOverlayView host, out Button first, out Toggle bgm, out Toggle sfx, out TMP_Text bgmState, out TMP_Text sfxState)
    {
        RectTransform layer = Rect("SettingsPopup", root, Vector2.zero, Vector2.zero);
        Stretch(layer);
        Solid("Dimmer", layer, new Color(0.01f, 0.01f, 0.015f, 0.78f), true);
        Image panel = Panel("Panel", layer, Vector2.zero, new Vector2(620, 640));
        Picture("Crest", panel.transform, "Header_Ornament", new Vector2(0, 305), new Vector2(430, 126));
        Label("Title", panel.transform, "설정", new Vector2(0, 206), new Vector2(440, 76), 58);
        Divider(panel.transform, 155, 220);
        Label("BgmLabel", panel.transform, "배경음악", new Vector2(-86, 65), new Vector2(270, 58), 34);
        Label("SfxLabel", panel.transform, "효과음", new Vector2(-86, -65), new Vector2(270, 58), 34);
        bgm = AudioToggle(panel.transform, "BGM", "", new Vector2(167, 65), host.SetBgmEnabled, out bgmState);
        sfx = AudioToggle(panel.transform, "SFX", "", new Vector2(167, -65), host.SetSfxEnabled, out sfxState);
        first = Button("Back", panel.transform, "돌아가기", new Vector2(0, -200), new Vector2(350, 88), 34);
        UnityEventTools.AddPersistentListener(first.onClick, host.CloseTop);
        Label("PreviewNote", panel.transform, "음향 시스템 연결 예정", new Vector2(0, -274), new Vector2(490, 28), 18).color = new Color32(156, 147, 145, 255);
        return layer.gameObject;
    }

    private static UITraitOverlayView BuildTraits(Transform root, UILobbyOverlayView host, GameObject normal, GameObject special, out Button first)
    {
        RectTransform screen = Rect("TraitsScreen", root, Vector2.zero, Vector2.zero);
        Stretch(screen);
        screen.gameObject.AddComponent<CanvasGroup>();
        UITraitOverlayView view = screen.gameObject.AddComponent<UITraitOverlayView>();
        Image background = Picture("Background", screen, null, Vector2.zero, Vector2.zero);
        Stretch(background.rectTransform);
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BASE + "/Sprites/Lobby_Background.png");
        background.raycastTarget = true;
        Solid("BackgroundShade", screen, new Color(0.015f, 0.015f, 0.02f, 0.40f), false);
        // 트리나 고정 UI 위가 아닌 배경 레이어에 로비의 하단 그라데이션을 재사용한다.
        Transform gradient = GameObject.Find("Canvas_Lobby").transform.Find("BackgroundBottomGradient");
        if (gradient != null)
        {
            GameObject copy = Object.Instantiate(gradient.gameObject, screen, false);
            copy.name = "BackgroundBottomGradient";
            foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }

        RectTransform viewport = Rect("TraitTreeViewport", screen, new Vector2(0, -95), new Vector2(1720, 710));
        Image surface = viewport.gameObject.AddComponent<Image>();
        surface.color = new Color(0, 0, 0, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        RectTransform content = Rect("TraitTreeContent", viewport, Vector2.zero, new Vector2(3440, 1800));
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Unrestricted;
        scroll.inertia = true;
        scroll.decelerationRate = 0.08f;
        scroll.scrollSensitivity = 28;
        CreateSample(normal, content, new Vector2(-200, 0), "일반 특성");
        CreateSample(special, content, new Vector2(200, 0), "특화 특성");
        Label("PreviewNote", content, "프레임 미리보기", new Vector2(0, 180), new Vector2(800, 45), 24).color = new Color32(182, 169, 165, 255);
        Label("TreePending", content, "특성 트리는 추후 배치", new Vector2(0, -198), new Vector2(900, 40), 22).color = new Color32(182, 169, 165, 255);

        Fade("LeftFade", screen, new Vector2(0, 0.5f), new Vector2(0, 0.5f), false);
        Fade("RightFade", screen, new Vector2(1, 0.5f), new Vector2(1, 0.5f), true);
        Transform account = GameObject.Find("Canvas_Lobby").transform.Find("AccountStatus");
        if (account != null)
        {
            GameObject accountCopy = Object.Instantiate(account.gameObject, screen, false);
            accountCopy.name = "AccountStatus";
        }
        Picture("HeaderOrnament", screen, "Header_Ornament", new Vector2(0, 447), new Vector2(490, 144));
        Label("Title", screen, "특성", new Vector2(0, 355), new Vector2(380, 80), 64);
        Picture("PointsFrame", screen, "Points_Frame", new Vector2(0, 271), new Vector2(460, 96));
        TMP_Text points = Label("Points", screen, "보유 포인트  -", new Vector2(0, 271), new Vector2(330, 44), 30);
        first = Button("Back", screen, "뒤로", new Vector2(765, 451), new Vector2(268, 85), 32);
        UnityEventTools.AddPersistentListener(first.onClick, host.CloseTraits);
        Button recenter = Button("Recenter", screen, "중앙으로", new Vector2(0, -470), new Vector2(270, 75), 26);
        UnityEventTools.AddPersistentListener(recenter.onClick, view.ResetView);
        Label("PanHint", screen, "드래그로 이동 / 클릭으로 상세 보기", new Vector2(0, -525), new Vector2(850, 30), 18).color = new Color32(182, 169, 165, 255);

        Image detail = Panel("SelectedTraitDetail", screen, new Vector2(698, -78), new Vector2(428, 590));
        TMP_Text type = Label("Type", detail.transform, "일반 특성", new Vector2(0, 211), new Vector2(350, 40), 24);
        type.color = new Color32(182, 139, 136, 255);
        TMP_Text title = Label("Name", detail.transform, "특성 이름", new Vector2(0, 155), new Vector2(346, 55), 36);
        Divider(detail.transform, 104, 250);
        Image detailIcon = Picture("Icon", detail.transform, null, new Vector2(0, 40), new Vector2(86, 86));
        detailIcon.enabled = false;
        TMP_Text desc = Label("Description", detail.transform, "", new Vector2(0, -78), new Vector2(324, 170), 24);
        desc.alignment = TextAlignmentOptions.TopLeft;
        desc.textWrappingMode = TextWrappingModes.Normal;
        Button close = Button("Close", detail.transform, "닫기", new Vector2(0, -222), new Vector2(244, 70), 28);
        UnityEventTools.AddPersistentListener(close.onClick, view.HideDetail);
        Assign(view, "_treeScroll", scroll);
        Assign(view, "_detailPanel", detail.gameObject);
        Assign(view, "_detailTitle", title);
        Assign(view, "_detailType", type);
        Assign(view, "_detailDescription", desc);
        Assign(view, "_detailIcon", detailIcon);
        Assign(view, "_pointsLabel", points);
        detail.gameObject.SetActive(false);
        return view;
    }

    private static GameObject CreateFramePrefab(bool specialized)
    {
        string name = specialized ? "TraitFrame_Specialized" : "TraitFrame_Normal";
        string path = ROOT + "/Prefabs/" + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        RectTransform root = Rect(name, null, Vector2.zero, Vector2.one * (specialized ? 190 : 148));
        try
        {
            Image frame = root.gameObject.AddComponent<Image>();
            frame.sprite = Sprite(specialized ? "Trait_Frame_Specialized" : "Trait_Frame_Normal");
            frame.preserveAspect = true;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            StyleButton(button);
            Image icon = Picture("Icon", root, null, Vector2.zero, Vector2.one * (specialized ? 74 : 58));
            icon.enabled = false;
            Image selected = Picture("SelectionIndicator", root, null, new Vector2(0, specialized ? -113 : -90), new Vector2(10, 10));
            selected.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            selected.color = TEXT;
            selected.gameObject.SetActive(false);
            UITraitFrameView view = root.gameObject.AddComponent<UITraitFrameView>();
            Assign(view, "_icon", icon);
            Assign(view, "_selectionIndicator", selected.gameObject);
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("_displayName").stringValue = specialized ? "특화 특성" : "일반 특성";
            serialized.FindProperty("_description").stringValue = "아이콘과 설명 데이터를 연결합니다.\n\n클릭 시 설명만 표시합니다.";
            serialized.FindProperty("_isSpecialized").boolValue = specialized;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }

    private static void CreateSample(GameObject prefab, Transform content, Vector2 position, string caption)
    {
        GameObject sample = (GameObject)PrefabUtility.InstantiatePrefab(prefab, content);
        sample.name = "Preview_" + prefab.name;
        var rect = (RectTransform)sample.transform;
        rect.anchoredPosition = position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        Label("PreviewLabel_" + prefab.name, content, caption, position + new Vector2(0, -133), new Vector2(300, 42), 28);
    }

    private static Toggle AudioToggle(Transform parent, string name, string title, Vector2 position, UnityEngine.Events.UnityAction<bool> changed, out TMP_Text state)
    {
        Image panel = Panel(name + "Toggle", parent, position, new Vector2(118, 110));
        panel.pixelsPerUnitMultiplier = 8;
        Toggle toggle = panel.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = panel;
        toggle.isOn = true;
        toggle.toggleTransition = Toggle.ToggleTransition.None;
        Label("Label", panel.transform, title, new Vector2(0, 25), new Vector2(105, 36), 19);
        Image track = Picture("Track", panel.transform, null, new Vector2(0, -12), new Vector2(69, 28));
        track.color = new Color32(50, 42, 46, 255);
        Image on = Picture("On", track.transform, null, Vector2.zero, new Vector2(65, 24));
        on.color = RED;
        toggle.graphic = on;
        state = Label("State", panel.transform, "켜짐", new Vector2(0, -12), new Vector2(85, 30), 17);
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, changed);
        return toggle;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = (RectTransform)go.transform;
        if (parent != null) rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Image Picture(string name, Transform parent, string sprite, Vector2 position, Vector2 size)
    {
        Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        if (sprite != null) image.sprite = Sprite(sprite);
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        return image;
    }

    private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size)
    {
        Image image = Picture(name, parent, "Panel_Frame", position, size);
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;
        image.pixelsPerUnitMultiplier = 3;
        image.raycastTarget = true;
        return image;
    }

    private static void Solid(string name, Transform parent, Color color, bool raycast)
    {
        Image image = Picture(name, parent, null, Vector2.zero, Vector2.zero);
        Stretch(image.rectTransform);
        image.color = color;
        image.raycastTarget = raycast;
    }

    private static TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        TextMeshProUGUI label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.fontSharedMaterial = _fontMaterial;
        label.text = text;
        label.color = TEXT;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static Button Button(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        Image image = Picture(name, parent, "Button_Frame", position, size);
        image.preserveAspect = false;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 3;
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        StyleButton(button);
        Label("Label", image.transform, text, Vector2.zero, new Vector2(size.x - 54, size.y - 18), fontSize);
        return button;
    }

    private static void StyleButton(Button button)
    {
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1, 0.84f, 0.80f);
        colors.selectedColor = new Color(1, 0.89f, 0.82f);
        colors.pressedColor = new Color(0.72f, 0.52f, 0.53f);
        colors.disabledColor = new Color(0.38f, 0.35f, 0.35f, 0.7f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
    }

    private static void Divider(Transform parent, float y, float width)
    {
        Image divider = Picture("Divider", parent, null, new Vector2(0, y), new Vector2(width, 2));
        divider.color = RED;
        Image diamond = Picture("DividerDiamond", parent, null, new Vector2(0, y), new Vector2(10, 10));
        diamond.color = RED;
        diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
    }

    private static void Fade(string name, Transform parent, Vector2 anchor, Vector2 pivot, bool reverse)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero, new Vector2(390, 1080));
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ROOT + "/Sprites/SideFade.asset");
        image.uvRect = reverse ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
        image.color = new Color(0.008f, 0.008f, 0.013f, 0.97f);
        image.raycastTarget = false;
    }

    private static Sprite Sprite(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ROOT + "/Sprites/" + name + ".png");
        if (sprite == null) throw new InvalidOperationException("필수 스프라이트 누락: " + name);
        return sprite;
    }

    private static void Assign(Object owner, string name, Object value)
    {
        var serialized = new SerializedObject(owner);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignArray(Object owner, string name, params Object[] values)
    {
        var serialized = new SerializedObject(owner);
        var array = serialized.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    private static void ImportSprite(string name)
    {
        string sourcePath = ROOT + "/Source/" + name + ".png";
        string targetPath = ROOT + "/Sprites/" + name + ".png";
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("이미지 생성 원본이 필요합니다.", sourcePath);
        // 생성기의 알파는 보존하고, 녹색 크로마 원본은 프로젝트 리소스용으로만 투명화한다.
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D cropped = null;
        try
        {
            texture.LoadImage(File.ReadAllBytes(sourcePath));
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            {
                int i = y * texture.width + x;
                Color32 p = pixels[i];
                if (p.g > p.r + 20 && p.g > p.b + 20 && p.g > 50) p.a = 0;
                pixels[i] = p;
                if (p.a < 16) continue;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            if (maxX < minX) throw new InvalidOperationException("비어 있는 이미지: " + name);
            minX = Mathf.Max(0, minX - 4); minY = Mathf.Max(0, minY - 4);
            maxX = Mathf.Min(texture.width - 1, maxX + 4); maxY = Mathf.Min(texture.height - 1, maxY + 4);
            int width = maxX - minX + 1, height = maxY - minY + 1;
            Color32[] data = new Color32[width * height];
            for (int y = 0; y < height; y++) Array.Copy(pixels, (y + minY) * texture.width + minX, data, y * width, width);
            cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cropped.SetPixels32(data); cropped.Apply();
            File.WriteAllBytes(targetPath, cropped.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(texture);
            if (cropped != null) Object.DestroyImmediate(cropped);
        }
        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(targetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;
        if (name == "Panel_Frame") importer.spriteBorder = new Vector4(130, 130, 130, 130);
        if (name == "Button_Frame") importer.spriteBorder = new Vector4(200, 52, 200, 52);
        importer.SaveAndReimport();
    }

    private static void CreateGradient()
    {
        string path = ROOT + "/Sprites/SideFade.asset";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) return;
        var texture = new Texture2D(256, 1, TextureFormat.RGBA32, false);
        texture.name = "SideFade";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        for (int x = 0; x < 256; x++) texture.SetPixel(x, 0, new Color(1, 1, 1, Mathf.Pow(1f - x / 255f, 1.4f)));
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
    }

    private static void PrepareFont()
    {
        if (!AssetDatabase.IsValidFolder(FONT_FOLDER))
            throw new InvalidOperationException("공유 폰트 폴더를 먼저 복원하세요: " + FONT_FOLDER);
        string path = FONT_FOLDER + "/DOSMyungjo Overlay Pixel.asset";
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (_font == null)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FONT_FOLDER + "/DOSMyungjo.ttf");
            FontEngine.InitializeFontEngine();
            _font = TMP_FontAsset.CreateFontAsset(font, 90, 4, GlyphRenderMode.RASTER, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            _font.name = "DOSMyungjo Overlay Pixel";
            AssetDatabase.CreateAsset(_font, path);
            foreach (Texture2D atlas in _font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, _font);
            AssetDatabase.AddObjectToAsset(_font.material, _font);
        }
        string text = "메뉴설정돌아가기게임종료배경음악효과켜짐꺼닫특성보유포인트뒤로중앙으로일반화프레임미리보기트리는추후배치드래그이동클릭상세이름아이콘설명데이터를연결해사용합니다표시만수행하며소모하지않습니다시스템예정0123456789 -/.";
        var pending = new System.Text.StringBuilder();
        foreach (char c in new HashSet<char>(text)) if (!_font.HasCharacter(c)) pending.Append(c);
        if (pending.Length > 0 && !_font.TryAddCharacters(pending.ToString(), out string missing))
            throw new InvalidOperationException("폰트 글리프 누락: " + missing);
        foreach (Texture2D atlas in _font.atlasTextures)
        {
            atlas.filterMode = FilterMode.Point;
            if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, _font);
            EditorUtility.SetDirty(atlas);
        }
        path = ROOT + "/Fonts/DOSMyungjo Overlay Outline.mat";
        _fontMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (_fontMaterial == null)
        {
            _fontMaterial = new Material(AssetDatabase.LoadAssetAtPath<Shader>(FONT_FOLDER + "/PixelTMPOutline.shader"));
            _fontMaterial.name = "DOSMyungjo Overlay Outline";
            AssetDatabase.CreateAsset(_fontMaterial, path);
        }
        _fontMaterial.SetTexture("_MainTex", _font.atlasTextures[0]);
        _fontMaterial.SetColor("_Color", Color.white);
        _fontMaterial.SetColor("_OutlineColor", new Color32(80, 73, 73, 255));
        _fontMaterial.SetFloat("_OutlineTexels", 3.5f);
        EditorUtility.SetDirty(_font);
        EditorUtility.SetDirty(_fontMaterial);
    }
}
