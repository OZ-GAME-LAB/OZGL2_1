using System;
using System.IO;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// UI_Lobby_MutedPreview 한 씬에만 별도 스킬 UI를 조립한다. 기존 팝업/공용 리소스는 보존한다.
public static class LobbySkillSettingsPreviewBuilder
{
    public const string ROOT = "Assets/06.UI/LobbyMutedPreview/Skills_v1";
    public const string PREFAB = ROOT + "/Prefabs/Canvas_SkillSettings.prefab";
    private const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private const string OVERLAYS = "Assets/06.UI/LobbyMutedPreview/Overlays";
    private const string LOBBY_BACKGROUND = "Assets/06.UI/LobbyMutedPreview/Sprites/Lobby_Background.png";
    private const string FONT = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/DOSMyungjo Overlay Pixel.asset";
    private static readonly Color IVORY = new Color32(248, 242, 235, 255);
    private static TMP_FontAsset _font;
    private static Material _material;

    [MenuItem("Tools/OZGL2/Lobby/Connect Skill Description")]
    public static void ConnectSkillDescription()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != SCENE || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("UI_Lobby_MutedPreview 씬의 Edit Mode에서 실행하세요.");
        const string overlayPrefab = OVERLAYS + "/Prefabs/Canvas_LobbyOverlays.prefab";
        var host = GameObject.Find("Canvas_LobbyOverlays");
        var skill = host != null ? host.transform.Find("Canvas_SkillSettings") : null;
        if (skill == null || !skill.TryGetComponent(out UISkillLoadoutPreview view))
            throw new InvalidOperationException("공용 Overlay의 스킬 화면이 필요합니다.");
        var description = skill.Find("Skill_Description");
        if (description == null || !description.TryGetComponent(out TMP_Text label) ||
            PrefabUtility.GetCorrespondingObjectFromSource(label) == null ||
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(host) != overlayPrefab)
            throw new InvalidOperationException("부모 Prefab에 저장된 Skill_Description TMP가 필요합니다.");
        Undo.RecordObject(view, "Connect skill catalog description");
        var properties = new SerializedObject(view);
        var reference = properties.FindProperty("_detailDescription");
        reference.objectReferenceValue = label;
        properties.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(view);
        // 다른 사용자 override나 미저장 씬 배치를 Apply/저장하지 않는다.
        properties.Update();
        PrefabUtility.ApplyPropertyOverride(properties.FindProperty("_detailDescription"), overlayPrefab, InteractionMode.UserAction);
        Debug.Log("Canvas_LobbyOverlays의 스킬 설명 TMP 참조만 연결했습니다. Undo 지원, Scene은 저장하지 않습니다.", view);
    }

    [MenuItem("Tools/OZGL2/Lobby/Build Skill Settings Preview")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != SCENE || Application.isPlaying) throw new InvalidOperationException("편집 모드 UI_Lobby_MutedPreview 씬에서만 실행하세요.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB) != null) throw new InvalidOperationException("기존 스킬 프리팹을 덮어쓰지 않습니다.");
        if (GameObject.Find("Canvas_LobbyOverlays") == null) throw new InvalidOperationException("공용 Canvas_LobbyOverlays를 먼저 배치하세요.");
        GameObject uiRoot = GameObject.Find("UI_Root");
        GameObject lobby = GameObject.Find("Canvas_Lobby");
        if (uiRoot == null || lobby == null || !uiRoot.TryGetComponent(out UIPopupController popupController)) throw new InvalidOperationException("로비 UI 연결이 필요합니다.");
        var popupRoot = new SerializedObject(popupController).FindProperty("_popupRoot").objectReferenceValue as Transform;
        if (popupRoot == null) throw new InvalidOperationException("PopupRoot 연결이 없습니다.");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
        _material = AssetDatabase.LoadAssetAtPath<Material>(OVERLAYS + "/Fonts/DOSMyungjo Trait Outline.mat");
        if (_font == null || _material == null) throw new InvalidOperationException("공유 폰트 파일과 .meta를 01.Font에 복원해야 합니다.");
        EnsureFolder(ROOT + "/Prefabs");
        foreach (string path in Directory.GetFiles(ROOT + "/Sprites", "*.png")) ImportSprite(path.Replace('\\', '/'));
        var catalog = CreateCatalog();

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build skill settings preview");
        RectTransform root = Rect("Canvas_SkillSettings", popupRoot, Vector2.zero, Vector2.zero);
        Stretch(root);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Create skill settings Canvas");
        root.gameObject.SetActive(false);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 150;
        root.gameObject.AddComponent<GraphicRaycaster>();
        UIPopupPanel panel = root.gameObject.AddComponent<UIPopupPanel>();
        UISkillLoadoutPreview view = root.gameObject.AddComponent<UISkillLoadoutPreview>();
        Assign(view, "_catalog", catalog);

        // 특성창과 동일하게 로비 배경 에셋을 재사용하고, 별도 검은 오버레이만 얹는다.
        Image background = Picture("Background", root, AssetDatabase.LoadAssetAtPath<Sprite>(LOBBY_BACKGROUND), Vector2.zero, Vector2.zero);
        Stretch(background.rectTransform);
        background.preserveAspect = false;
        background.raycastTarget = true;
        Image shade = Picture("BackgroundShade", root, null, Vector2.zero, Vector2.zero);
        Stretch(shade.rectTransform);
        shade.color = new Color(0, 0, 0, .60f);
        // 로비와 같은 높이/색/텍스처를 UI 아래에 복사한다. 입력은 가로채지 않는다.
        Transform gradient = lobby.transform.Find("BackgroundBottomGradient");
        if (gradient != null)
        {
            GameObject copy = Object.Instantiate(gradient.gameObject, root, false);
            copy.name = "BackgroundBottomGradient";
            foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }

        Transform account = lobby.transform.Find("AccountStatus");
        if (account != null)
        {
            GameObject copy = Object.Instantiate(account.gameObject, root, false);
            copy.name = "AccountStatus";
            foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }
        Picture("TitleFrame", root, Sprite("Title"), new Vector2(0, 426), new Vector2(490, 218));
        Label("Title", root, "스킬 세팅", new Vector2(0, 415), new Vector2(340, 75), 52);

        Sprite backSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OVERLAYS + "/DetailArt_v2/Button_Back_268x85.png");
        UISkillArtButton back = ArtButton("Back", root, "뒤로", new Vector2(751, 455), new Vector2(304, 94), 37, null, backSprite);
        Picture("Arrow", back.transform, Sprite("Back_Arrow"), new Vector2(-91, 0), new Vector2(35, 42));
        ((RectTransform)back.GetComponentInChildren<TMP_Text>().transform).anchoredPosition = new Vector2(24, 0);

        CreatePanel("OwnedSkillsPanel", root, new Vector2(-660, -25), new Vector2(550, 820));
        Label("OwnedTitle", root, "보유 스킬", new Vector2(-660, 321), new Vector2(460, 62), 40);
        string[] tabs = { "전체", "딜", "버프", "디버프" };
        UISkillArtButton[] categoryButtons = new UISkillArtButton[4];
        for (int i = 0; i < tabs.Length; i++)
        {
            categoryButtons[i] = ArtButton("Category_" + i, root, tabs[i], new Vector2(-849 + i * 126, 246), new Vector2(120, 65), 28, "Tab");
            UnityEventTools.AddIntPersistentListener(categoryButtons[i].onClick, view.ShowCategory, i);
        }
        AssignArray(view, "_categoryButtons", categoryButtons);
        Assign(panel, "_firstSelected", categoryButtons[0]);

        RectTransform gridRoot = Rect("SkillGrid", root, new Vector2(-660, -102), new Vector2(494, 608));
        GridLayoutGroup grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(156, 144);
        grid.spacing = new Vector2(10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;
        UISkillArtButton[] cards = new UISkillArtButton[catalog.Entries.Count];
        Image[] icons = new Image[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = ArtButton("SkillCard_" + i.ToString("00"), gridRoot, null, Vector2.zero, new Vector2(156, 144), 0, null, Sprite("Card_Normal"));
            SetSprites(cards[i], Sprite("Card_Normal"), Sprite("Card_Hover_Amber"), Sprite("Card_Hover_Amber"), Sprite("Card_Selected_Gold"), Sprite("Card_Normal"));
            var cardProperties = new SerializedObject(cards[i]);
            cardProperties.FindProperty("_keepChosenWhilePressed").boolValue = true;
            cardProperties.ApplyModifiedPropertiesWithoutUndo();
            icons[i] = Picture("Icon", cards[i].transform, catalog.Entries[i].Icon, new Vector2(0, -2), new Vector2(102, 102));
            UnityEventTools.AddIntPersistentListener(cards[i].onClick, view.SelectSkill, i);
        }
        AssignArray(view, "_cards", cards);
        AssignArray(view, "_cardIcons", icons);

        Label("EquippedTitle", root, "장착 스킬", new Vector2(0, 318), new Vector2(470, 62), 40);
        Picture("EquippedConnector", root, Sprite("Connectors"), new Vector2(0, 0), new Vector2(465, 465));
        Vector2[] positions = { new Vector2(-165, 124), new Vector2(165, 124), new Vector2(0, -155) };
        Image[] equippedIcons = new Image[3];
        Image[] equippedFrames = new Image[3];
        int[] initial = { 0, 4, 2 };
        for (int i = 0; i < 3; i++)
        {
            equippedFrames[i] = Picture("EquippedSlot_" + i, root, Sprite("Equip_Red"), positions[i], new Vector2(246, 246));
            equippedFrames[i].raycastTarget = true;
            Button button = equippedFrames[i].gameObject.AddComponent<Button>();
            button.targetGraphic = equippedFrames[i];
            var colors = button.colors;
            colors.highlightedColor = new Color(.9f, .85f, .86f);
            colors.pressedColor = new Color(.65f, .6f, .62f);
            button.colors = colors;
            equippedIcons[i] = Picture("Icon", equippedFrames[i].transform, catalog.Entries[initial[i]].Icon, Vector2.zero, new Vector2(125, 125));
            UnityEventTools.AddIntPersistentListener(button.onClick, view.SelectEquippedSlot, i);
        }
        AssignArray(view, "_equippedIcons", equippedIcons);
        AssignArray(view, "_equippedFrames", equippedFrames);
        Assign(view, "_redFrame", Sprite("Equip_Red"));

        CreatePanel("SkillDetailPanel", root, new Vector2(697, 43), new Vector2(455, 684));
        Picture("DetailIconFrame", root, Sprite("Card_Selected_Gold"), new Vector2(697, 187), new Vector2(290, 290));
        Assign(view, "_detailIcon", Picture("DetailIcon", root, Sprite("Icon_Abyss"), new Vector2(697, 185), new Vector2(236, 236)));
        Assign(view, "_detailName", Label("SkillName", root, "심연의 파동", new Vector2(697, 8), new Vector2(410, 68), 41));
        Picture("StatFrame", root, Sprite("Stats"), new Vector2(697, -117), new Vector2(412, 156));
        Picture("EffectIcon", root, AssetDatabase.LoadAssetAtPath<Sprite>(OVERLAYS + "/TreeArt_v1/Sprites/Icons/Spells/Icon_Spells_RuinEssence.png"), new Vector2(547, -79), new Vector2(68, 68));
        Picture("CooldownIcon", root, AssetDatabase.LoadAssetAtPath<Sprite>(OVERLAYS + "/TreeArt_v1/Sprites/Icons/Spells/Icon_Spells_Acceleration.png"), new Vector2(547, -150), new Vector2(68, 68));
        Assign(view, "_effectLabel", Label("EffectLabel", root, "피해", new Vector2(634, -79), new Vector2(130, 50), 30));
        Assign(view, "_effectValue", Label("EffectValue", root, "120", new Vector2(816, -79), new Vector2(125, 50), 32));
        Label("CooldownLabel", root, "재사용", new Vector2(634, -150), new Vector2(150, 50), 30);
        Assign(view, "_cooldown", Label("CooldownValue", root, "8초", new Vector2(816, -150), new Vector2(125, 50), 32));
        var equip = ArtButton("Equip", root, "장착", new Vector2(586, -230), new Vector2(213, 94), 37, "Action");
        var unequip = ArtButton("Unequip", root, "해제", new Vector2(808, -230), new Vector2(213, 94), 37, "Action");
        var save = ArtButton("Save", root, "저장", new Vector2(697, -374), new Vector2(518, 140), 55, "Save");
        Assign(view, "_equipButton", equip); Assign(view, "_unequipButton", unequip); Assign(view, "_saveButton", save);
        Assign(view, "_status", Label("Status", root, string.Empty, new Vector2(697, -464), new Vector2(500, 35), 22));
        UnityEventTools.AddPersistentListener(equip.onClick, view.EquipSelected);
        UnityEventTools.AddPersistentListener(unequip.onClick, view.UnequipSelected);
        UnityEventTools.AddPersistentListener(save.onClick, view.SavePreview);

        // 외부 Scene 참조는 Prefab 저장 이후에 인스턴스 override로만 연결한다.
        LobbySkillCategoryStyleBuilder.Configure(root.gameObject);
        LobbySkillSaveFlowBuilder.Configure(root.gameObject);
        LobbySkillFrameAlignmentBuilder.Configure(root.gameObject);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PREFAB, InteractionMode.AutomatedAction);
        UnityEventTools.AddPersistentListener(back.onClick, popupController.CloseTopPopup);
        PrefabUtility.RecordPrefabInstancePropertyModifications(back);
        foreach (Button button in lobby.GetComponentsInChildren<Button>(true))
        {
            if (button.name != "SkillsButton") continue;
            Undo.RecordObject(button, "Connect new skill settings popup");
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                if (button.onClick.GetPersistentTarget(i) == popupController && button.onClick.GetPersistentMethodName(i) == "OpenPopup")
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddObjectPersistentListener<UIPopupPanel>(button.onClick, popupController.OpenPopup, panel);
            EditorUtility.SetDirty(button);
        }
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        LobbyOverlayIntegrationBuilder.Integrate();
        Debug.Log("스킬 세팅 프리팹 생성 및 Canvas_LobbyOverlays 통합: 기존 팝업 보존, 4개 필터, 3개 장착 슬롯, TMP 분리. 실제 게임 저장은 미연결입니다.");
    }

    private static UISkillPreviewCatalogSO CreateCatalog()
    {
        string path = ROOT + "/SkillPreviewCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<UISkillPreviewCatalogSO>(path);
        if (catalog != null) return catalog;
        catalog = ScriptableObject.CreateInstance<UISkillPreviewCatalogSO>();
        string[] icons = { "Fire", "Focus", "Strike", "Sword", "Abyss", "Crown", "Moon", "Curse", "Trident", "Vortex", "Crosshair", "Portal" };
        string[] names = { "화염구", "정밀 주문", "번개 강타", "마력 강화", "심연의 파동", "마왕의 가호", "감속의 달", "저주 낙인", "군단의 축복", "공허 회오리", "마력 집중", "침묵의 문" };
        int[] categories = { 0, 0, 0, 1, 0, 1, 2, 2, 1, 2, 1, 2 };
        string[] labels = { "피해", "피해", "피해", "공격력", "피해", "방어력", "감속", "약화", "강화", "감속", "강화", "침묵" };
        string[] values = { "100", "80", "160", "+15%", "120", "+20%", "30%", "20%", "+10%", "40%", "+15%", "3초" };
        var so = new SerializedObject(catalog);
        SerializedProperty entries = so.FindProperty("_entries"); entries.arraySize = icons.Length;
        for (int i = 0; i < icons.Length; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("_id").stringValue = "ui_preview_" + icons[i].ToLowerInvariant();
            entry.FindPropertyRelative("_displayName").stringValue = names[i];
            entry.FindPropertyRelative("_category").enumValueIndex = categories[i];
            entry.FindPropertyRelative("_icon").objectReferenceValue = Sprite("Icon_" + icons[i]);
            entry.FindPropertyRelative("_isArcane").boolValue = i == 4 || i == 9;
            entry.FindPropertyRelative("_effectLabel").stringValue = labels[i];
            entry.FindPropertyRelative("_effectValue").stringValue = values[i];
            entry.FindPropertyRelative("_cooldown").stringValue = i == 4 ? "8초" : (10 + i).ToString() + "초";
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(catalog, path);
        return catalog;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Image Picture(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = sprite != null; image.raycastTarget = false; return image;
    }
    private static void CreatePanel(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OVERLAYS + "/Sprites/Panel_Frame.png");
        var image = Picture(name, parent, sprite, position, size);
        image.preserveAspect = false; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 3;
    }
    private static TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font; label.fontSharedMaterial = _material; label.text = text; label.color = IVORY;
        label.fontSize = fontSize; label.fontStyle = FontStyles.Normal; label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
        return label;
    }
    private static UISkillArtButton ArtButton(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, string family, Sprite fallback = null)
    {
        Image image = Picture(name, parent, family == null ? fallback : Sprite("Button_" + family + "_Normal"), position, size);
        image.raycastTarget = true;
        UISkillArtButton button = image.gameObject.AddComponent<UISkillArtButton>(); button.targetGraphic = image;
        if (text != null) Assign(button, "_label", Label("Label", image.transform, text, Vector2.zero, new Vector2(size.x - 26, size.y - 14), fontSize));
        if (family != null) SetSprites(button, Sprite("Button_" + family + "_Normal"), Sprite("Button_" + family + "_Hover"), Sprite("Button_" + family + "_Pressed"), Sprite("Button_" + family + "_Selected"), Sprite("Button_" + family + "_Disabled"));
        else SetSprites(button, fallback, fallback, fallback, fallback, fallback);
        return button;
    }
    private static void SetSprites(UISkillArtButton button, Sprite normal, Sprite hover, Sprite pressed, Sprite chosen, Sprite disabled)
    {
        Assign(button, "_normal", normal); Assign(button, "_hover", hover); Assign(button, "_pressed", pressed); Assign(button, "_chosen", chosen); Assign(button, "_disabled", disabled);
    }
    private static Sprite Sprite(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ROOT + "/Sprites/" + name + ".png");
        if (sprite == null) throw new InvalidOperationException("Sprite 누락: " + name); return sprite;
    }
    private static void Assign(Object target, string name, Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void AssignArray(Object target, string name, Object[] values)
    {
        var so = new SerializedObject(target); var array = so.FindProperty(name); array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int last = path.LastIndexOf('/'); string parent = path.Substring(0, last); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(last + 1));
    }
    private static void ImportSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("TextureImporter 누락: " + path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.spritePixelsPerUnit = 100;
        importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }
}
