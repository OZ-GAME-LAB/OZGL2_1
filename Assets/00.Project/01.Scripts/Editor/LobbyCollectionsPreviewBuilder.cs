using System;
using System.IO;
using System.Linq;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 승인된 Preview 씬에만 신규 화면을 추가한다. 기존 화면 전체 Apply는 하지 않는다.
public static class LobbyCollectionsPreviewBuilder
{
    public const string ROOT = "Assets/06.UI/LobbyMutedPreview/Collections_v1";
    public const string CODEX_PREFAB = ROOT + "/Prefabs/Canvas_UnitCodex.prefab";
    public const string ACHIEVEMENTS_PREFAB = ROOT + "/Prefabs/Canvas_Achievements.prefab";
    private const string OVERLAYS = "Assets/06.UI/LobbyMutedPreview/Overlays";
    private const string HOST_PREFAB = OVERLAYS + "/Prefabs/Canvas_LobbyOverlays.prefab";
    private const string SKILLS = "Assets/06.UI/LobbyMutedPreview/Skills_v1/Sprites/";
    private const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private const string UNDO = "Add lobby codex and achievements";
    private static TMP_FontAsset _font;
    private static Material _textMaterial;
    private static Material _silhouette;
    private static Sprite _lock;
    private static readonly Color IVORY = new Color32(242, 234, 219, 255);
    private static readonly string[] ACHIEVEMENT_IDS = { "first_defense", "first_gate", "veteran", "hero_hunter_1", "hero_hunter_2", "giant_fall", "growth", "tuning", "ready" };

    // 승인된 클릭 설정만 수정한다. 기존 아트/배치/부모 전체 override는 Apply하지 않는다.
    [MenuItem("Tools/OZGL2/Lobby/Repair Collection Click Targets")]
    public static void RepairClickTargets()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != SCENE || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("UI_Lobby_MutedPreview의 Edit Mode에서 실행하세요.");
        var host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UILobbyOverlayView>(true)).Single();
        var view = host.GetComponentInChildren<UIUnitCodexView>(true);
        if (host.IsOpen || view == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.gameObject) != CODEX_PREFAB)
            throw new InvalidOperationException("도감 원본 Prefab 연결을 확인하고 모든 Overlay를 닫으세요.");
        var lobby = scene.GetRootGameObjects().Single(r => r.name == "Canvas_Lobby");
        var achievementButton = lobby.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "ReservedButton");
        var images = new Image[2];
        for (int i = 0; i < images.Length; i++)
        {
            var emblem = view.transform.Find("Faction_" + i + "/Emblem");
            if (emblem == null || !emblem.TryGetComponent(out images[i]))
                throw new InvalidOperationException("진영 아이콘 연결이 없습니다: " + i);
        }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Repair collection click targets");
        Undo.RecordObject(achievementButton, "Enable achievement button");
        achievementButton.interactable = true;
        EditorUtility.SetDirty(achievementButton);
        if (PrefabUtility.IsPartOfPrefabInstance(achievementButton)) PrefabUtility.RecordPrefabInstancePropertyModifications(achievementButton);
        for (int i = 0; i < images.Length; i++)
        {
            UnityEngine.UI.Button button = ConfigureFactionEmblem(images[i], view, i);
            PrefabUtility.RecordPrefabInstancePropertyModifications(images[i]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            if (PrefabUtility.GetCorrespondingObjectFromSource(button) == null)
                PrefabUtility.ApplyAddedComponent(button, CODEX_PREFAB, InteractionMode.UserAction);
            else
                PrefabUtility.ApplyObjectOverride(button, CODEX_PREFAB, InteractionMode.UserAction);
            var so = new SerializedObject(images[i]);
            PrefabUtility.ApplyPropertyOverride(so.FindProperty("m_RaycastTarget"), CODEX_PREFAB, InteractionMode.UserAction);
        }
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("업적 버튼 Interactable 및 도감 마름모 2개의 클릭 연결 수정. Undo 지원, Scene 저장은 검증 후 별도 수행합니다.");
    }

    [MenuItem("Tools/OZGL2/Lobby/Build Collections Preview")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != SCENE || Application.isPlaying || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("UI_Lobby_MutedPreview의 Edit Mode에서 실행하세요.");
        var host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UILobbyOverlayView>(true)).Single();
        var lobby = scene.GetRootGameObjects().Single(r => r.name == "Canvas_Lobby");
        if (host.IsOpen || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(host.gameObject) != HOST_PREFAB)
            throw new InvalidOperationException("승인된 Overlay를 닫고 실행하세요.");
        if (host.transform.Find("Canvas_UnitCodex") != null || host.transform.Find("Canvas_Achievements") != null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(CODEX_PREFAB) != null || AssetDatabase.LoadAssetAtPath<GameObject>(ACHIEVEMENTS_PREFAB) != null)
            throw new InvalidOperationException("기존 도감/업적 화면을 덮어쓰지 않습니다.");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/98.ExternalAssets/00.LocalStaging/01.Font/DOSMyungjo Overlay Pixel.asset");
        _textMaterial = AssetDatabase.LoadAssetAtPath<Material>(OVERLAYS + "/Fonts/DOSMyungjo Trait Outline.mat");
        if (_font == null || _textMaterial == null) throw new InvalidOperationException("01.Font의 공유 SDF와 .meta가 필요합니다.");
        EnsureFolder(ROOT + "/Prefabs");
        foreach (string path in Directory.GetFiles(ROOT + "/Sprites", "*.png")) ImportSprite(path.Replace('\\', '/'));
        _lock = Art("Lock");
        string materialPath = ROOT + "/Styles/CollectionSilhouette.mat";
        _silhouette = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (_silhouette == null)
        {
            Shader shader = Shader.Find("OZGL2/UI/Collection Silhouette");
            if (shader == null) throw new InvalidOperationException("Collection Silhouette Shader 컴파일이 필요합니다.");
            _silhouette = new Material(shader);
            AssetDatabase.CreateAsset(_silhouette, materialPath);
        }
        // 렌더 작업은 별도 도구에서 실행한다. 불완전한 모델 초상을 등록하지 않는다.
        UIUnitCatalogSO units = CreateUnitCatalog();
        UIAchievementCatalogSO achievements = CreateAchievementCatalog();

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO);
        UILobbyCollectionState state = host.GetComponent<UILobbyCollectionState>();
        if (state == null)
        {
            state = Undo.AddComponent<UILobbyCollectionState>(host.gameObject);
            var properties = new SerializedObject(state);
            var progress = properties.FindProperty("_initialAchievementProgress");
            int[] preview = { 1, 5, 7, 73, 73, 0, 8, 3, 2 };
            progress.arraySize = preview.Length;
            for (int i = 0; i < preview.Length; i++)
            {
                var entry = progress.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_entryId").stringValue = ACHIEVEMENT_IDS[i];
                entry.FindPropertyRelative("_value").intValue = preview[i];
            }
            properties.ApplyModifiedProperties();
            PrefabUtility.ApplyAddedComponent(state, HOST_PREFAB, InteractionMode.UserAction);
        }

        var codex = CreateCodex(host.transform, lobby.transform, units);
        PersistScreen(codex, CODEX_PREFAB, host, state);
        var achievementScreen = CreateAchievements(host.transform, lobby.transform, achievements);
        PersistScreen(achievementScreen, ACHIEVEMENTS_PREFAB, host, state);
        ConnectLobbyButton(lobby, "CodexButton", host, codex.GetComponent<UIPopupPanel>());
        ConnectLobbyButton(lobby, "ReservedButton", host, achievementScreen.GetComponent<UIPopupPanel>());
        ConnectSkillUnlock(host, state);
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("도감/업적 추가 및 스킬 해금 연결 완료. Scene Undo 지원; 새 에셋/Prefab 저장은 Git Diff로 검토하세요. Scene은 검증 후 별도로 저장합니다.");
    }

    private static RectTransform CreateCodex(Transform host, Transform lobby, UIUnitCatalogSO catalog)
    {
        RectTransform root = CreateScreen("Canvas_UnitCodex", "도 감", host, lobby);
        var view = root.gameObject.AddComponent<UIUnitCodexView>();
        Assign(view, "_catalog", catalog);
        Assign(view, "_silhouetteMaterial", _silhouette);
        Assign(view, "_lockSprite", _lock);
        Panel("CollectionPanel", root, new Vector2(180, -68), new Vector2(1430, 795));
        ScrollRect scroll = CreateGrid(root, new Vector2(180, -70), new Vector2(1324, 735), 4, new Vector2(300, 350), new Vector2(27, 20), out RectTransform content);
        Assign(view, "_content", content); Assign(view, "_scrollRect", scroll);
        var template = Rect("UnitCard_Template", root, Vector2.zero, new Vector2(300, 350));
        template.gameObject.SetActive(false);
        var card = template.gameObject.AddComponent<UIUnitCodexCardView>();
        Image frame = Picture("Frame", template, Art("CodexCard"), Vector2.zero, new Vector2(300, 350));
        Assign(card, "_frame", frame);
        Assign(card, "_portrait", Picture("Portrait", template, null, new Vector2(0, 37), new Vector2(230, 244)));
        Assign(card, "_nameText", Label("Name", template, "미발견", new Vector2(0, -123), new Vector2(232, 43), 29));
        Assign(card, "_lockIcon", Picture("Lock", template, _lock, new Vector2(0, 20), new Vector2(49, 62)));
        Assign(view, "_cardTemplate", card);
        var buttons = new UISkillArtButton[2];
        string[] labels = { "마왕군", "인간군" };
        for (int i = 0; i < 2; i++)
        {
            RectTransform group = Rect("Faction_" + i, root, new Vector2(-730, 160 - i * 350), new Vector2(270, 310));
            Image emblem = Picture("Emblem", group, Art(i == 0 ? "FactionDemon" : "FactionHero"), new Vector2(0, 38), new Vector2(255, 255));
            ConfigureFactionEmblem(emblem, view, i);
            buttons[i] = Button("Select", group, labels[i], new Vector2(0, -85), new Vector2(244, 72), 30, LoadSprite(SKILLS + "Button_Tab_Normal.png"));
            SetButtonSprites(buttons[i], LoadSprite(SKILLS + "Button_Tab_Normal.png"), LoadSprite(SKILLS + "Button_Tab_Hover.png"), LoadSprite(SKILLS + "Button_Tab_Selected.png"));
            UnityEventTools.AddIntPersistentListener(buttons[i].onClick, view.ShowFaction, i);
        }
        AssignArray(view, "_factionButtons", buttons);
        Assign(root.GetComponent<UIPopupPanel>(), "_firstSelected", buttons[0]);
        Assign(view, "_pageText", Label("DiscoveredCount", root, "발견", new Vector2(-730, -420), new Vector2(275, 42), 27));
        Assign(view, "_emptyText", Label("Empty", root, string.Empty, new Vector2(180, 0), new Vector2(900, 60), 30));
        return root;
    }

    private static RectTransform CreateAchievements(Transform host, Transform lobby, UIAchievementCatalogSO catalog)
    {
        RectTransform root = CreateScreen("Canvas_Achievements", "업 적", host, lobby);
        var view = root.gameObject.AddComponent<UIAchievementView>();
        Assign(view, "_catalog", catalog);
        Assign(view, "_summaryText", Label("CompletedCount", root, "달성", new Vector2(-707, 437), new Vector2(366, 70), 36));
        Panel("CollectionPanel", root, new Vector2(0, -68), new Vector2(1720, 797));
        CreateGrid(root, new Vector2(0, -65), new Vector2(1620, 737), 3, new Vector2(520, 230), new Vector2(24, 21), out RectTransform content);
        Assign(view, "_content", content);
        var template = Rect("AchievementCard_Template", root, Vector2.zero, new Vector2(520, 230));
        template.gameObject.SetActive(false);
        var card = template.gameObject.AddComponent<UIAchievementCardView>();
        Picture("Frame", template, Art("AchievementCard"), Vector2.zero, new Vector2(520, 230));
        Picture("IconFrame", template, LoadSprite(SKILLS + "Card_Normal.png"), new Vector2(-164, 10), new Vector2(147, 154));
        Assign(card, "_icon", Picture("Icon", template, null, new Vector2(-164, 10), new Vector2(104, 113)));
        Assign(card, "_nameText", Label("Name", template, "업적 이름", new Vector2(66, 69), new Vector2(303, 43), 29));
        var description = Label("Description", template, string.Empty, new Vector2(66, 32), new Vector2(296, 36), 17);
        description.color = new Color32(162, 155, 148, 255);
        Assign(card, "_descriptionText", description);
        Assign(card, "_progressText", Label("Progress", template, "0 / 1", new Vector2(66, -4), new Vector2(296, 35), 28));
        Picture("ProgressRail", template, null, new Vector2(66, -40), new Vector2(294, 26)).color = new Color32(116, 110, 101, 255);
        Picture("ProgressTrack", template, null, new Vector2(66, -40), new Vector2(288, 20)).color = new Color32(41, 41, 45, 255);
        Image fill = Picture("ProgressFill", template, null, new Vector2(66, -40), new Vector2(288, 20));
        // Unity 내장 white Sprite로 진행 막대를 채운다. null Sprite의 Filled 동작 차이를 피한다.
        fill.sprite = GetProgressPixel();
        fill.preserveAspect = false;
        Assign(card, "_progressFill", fill);
        for (int i = 1; i < 10; i++)
            Picture("Segment_" + i, template, null, new Vector2(-78 + i * 28.8f, -40), new Vector2(3, 20)).color = new Color32(16, 14, 17, 255);
        Assign(card, "_completedText", Label("Status", template, "진행 중", new Vector2(66, -78), new Vector2(295, 31), 21));
        Assign(view, "_cardTemplate", card);
        Assign(root.GetComponent<UIPopupPanel>(), "_firstSelected", root.Find("Back").GetComponent<UISkillArtButton>());
        return root;
    }

    private static RectTransform CreateScreen(string name, string title, Transform host, Transform lobby)
    {
        RectTransform root = Rect(name, host, Vector2.zero, Vector2.zero); Stretch(root);
        Undo.RegisterCreatedObjectUndo(root.gameObject, UNDO);
        root.gameObject.SetActive(false);
        var canvas = root.gameObject.AddComponent<Canvas>();
        var canvasProperties = new SerializedObject(canvas);
        canvasProperties.FindProperty("m_OverrideSorting").boolValue = true;
        canvasProperties.FindProperty("m_SortingOrder").intValue = 210;
        canvasProperties.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.AddComponent<GraphicRaycaster>();
        root.gameObject.AddComponent<UIPopupPanel>();
        Image background = Picture("Background", root, LoadSprite("Assets/06.UI/LobbyMutedPreview/Sprites/Lobby_Background.png"), Vector2.zero, Vector2.zero);
        Stretch(background.rectTransform); background.preserveAspect = false; background.raycastTarget = true;
        Image shade = Picture("BackgroundShade", root, null, Vector2.zero, Vector2.zero);
        Stretch(shade.rectTransform); shade.color = new Color(0, 0, 0, .60f);
        Transform gradient = lobby.Find("BackgroundBottomGradient");
        if (gradient == null) throw new InvalidOperationException("로비 하단 Gradient가 필요합니다.");
        GameObject copy = Object.Instantiate(gradient.gameObject, root, false); copy.name = gradient.name;
        foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        Picture("TitleFrame", root, LoadSprite(SKILLS + "Title.png"), new Vector2(0, 427), new Vector2(450, 192));
        Label("Title", root, title, new Vector2(0, 417), new Vector2(320, 70), 50);
        var back = Button("Back", root, "뒤로", new Vector2(757, 447), new Vector2(290, 89), 35, LoadSprite(OVERLAYS + "/DetailArt_v2/Button_Back_268x85.png"));
        Picture("Arrow", back.transform, LoadSprite(SKILLS + "Back_Arrow.png"), new Vector2(-86, 0), new Vector2(33, 40));
        back.GetComponentInChildren<TMP_Text>().rectTransform.anchoredPosition = new Vector2(22, 0);
        return root;
    }

    private static void PersistScreen(RectTransform root, string path, UILobbyOverlayView host, UILobbyCollectionState state)
    {
        PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, path, InteractionMode.UserAction);
        var back = root.Find("Back").GetComponent<UISkillArtButton>();
        UnityEventTools.AddPersistentListener(back.onClick, host.CloseTop);
        PrefabUtility.RecordPrefabInstancePropertyModifications(back);
        var codex = root.GetComponent<UIUnitCodexView>();
        if (codex != null) Assign(codex, "_state", state);
        var achievements = root.GetComponent<UIAchievementView>();
        if (achievements != null) Assign(achievements, "_state", state);
        PrefabUtility.ApplyAddedGameObject(root.gameObject, HOST_PREFAB, InteractionMode.UserAction);
    }

    private static void ConnectLobbyButton(GameObject lobby, string name, UILobbyOverlayView host, UIPopupPanel panel)
    {
        var button = lobby.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        Undo.RecordObject(button, UNDO);
        // 예약 버튼으로 만들어진 업적도 실제 화면 연결 후에는 포인터 입력을 받을 수 있어야 한다.
        button.interactable = true;
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if (button.onClick.GetPersistentTarget(i) is UIPopupController || button.onClick.GetPersistentTarget(i) == host)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddObjectPersistentListener<UIPopupPanel>(button.onClick, host.OpenOverlayPopup, panel);
        EditorUtility.SetDirty(button);
        if (PrefabUtility.IsPartOfPrefabInstance(button)) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
    }

    private static UnityEngine.UI.Button ConfigureFactionEmblem(Image image, UIUnitCodexView view, int faction)
    {
        var button = image.GetComponent<UnityEngine.UI.Button>();
        if (button == null) button = Undo.AddComponent<UnityEngine.UI.Button>(image.gameObject);
        Undo.RecordObject(image, "Enable faction emblem raycast");
        Undo.RecordObject(button, "Connect faction emblem button");
        image.raycastTarget = true;
        button.targetGraphic = image;
        button.enabled = true;
        button.interactable = true;
        // 키보드의 기존 탭 탐색 순서는 유지하고, 마름모에는 포인터 클릭만 추가한다.
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if (button.onClick.GetPersistentTarget(i) == view && button.onClick.GetPersistentMethodName(i) == "ShowFaction")
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddIntPersistentListener(button.onClick, view.ShowFaction, faction);
        return button;
    }

    private static void ConnectSkillUnlock(UILobbyOverlayView host, UILobbyCollectionState state)
    {
        var view = host.GetComponentInChildren<UISkillLoadoutPreview>(true);
        if (view == null) throw new InvalidOperationException("기존 스킬 화면이 없습니다.");
        Undo.RecordObject(view, UNDO);
        var properties = new SerializedObject(view);
        var cards = properties.FindProperty("_cards");
        Image[] locks = new Image[cards.arraySize];
        for (int i = 0; i < locks.Length; i++)
        {
            var button = (UISkillArtButton)cards.GetArrayElementAtIndex(i).objectReferenceValue;
            locks[i] = Picture("UnlockLock", button.transform, _lock, new Vector2(0, -10), new Vector2(29, 36));
            locks[i].enabled = false;
            Undo.RegisterCreatedObjectUndo(locks[i].gameObject, UNDO);
            PrefabUtility.ApplyAddedGameObject(locks[i].gameObject, HOST_PREFAB, InteractionMode.UserAction);
        }
        var icon = (Image)properties.FindProperty("_detailIcon").objectReferenceValue;
        Image detailLock = Picture("DetailUnlockLock", icon.transform, _lock, Vector2.zero, new Vector2(57, 71));
        detailLock.enabled = false;
        Undo.RegisterCreatedObjectUndo(detailLock.gameObject, UNDO);
        PrefabUtility.ApplyAddedGameObject(detailLock.gameObject, HOST_PREFAB, InteractionMode.UserAction);
        Assign(view, "_unlockState", state); Assign(view, "_lockedIconMaterial", _silhouette);
        Assign(view, "_detailLockIcon", detailLock); AssignArray(view, "_cardLockIcons", locks);
        foreach (string field in new[] { "_unlockState", "_lockedIconMaterial", "_detailLockIcon", "_cardLockIcons" })
        {
            var so = new SerializedObject(view);
            PrefabUtility.ApplyPropertyOverride(so.FindProperty(field), HOST_PREFAB, InteractionMode.UserAction);
        }
    }

    private static UIUnitCatalogSO CreateUnitCatalog()
    {
        string path = ROOT + "/UnitCatalog.asset";
        var existing = AssetDatabase.LoadAssetAtPath<UIUnitCatalogSO>(path); if (existing != null) return existing;
        string[] roles = { "WAR", "ARC", "MAG", "SHD", "HEL", "ROG" };
        var catalog = ScriptableObject.CreateInstance<UIUnitCatalogSO>();
        var so = new SerializedObject(catalog); var entries = so.FindProperty("_entries"); entries.arraySize = 12;
        for (int faction = 0; faction < 2; faction++)
        for (int i = 0; i < roles.Length; i++)
        {
            string id = (faction == 0 ? "M_" : "H_") + roles[i] + "_01";
            var stat = AssetDatabase.LoadAssetAtPath<UnitStatData>("Assets/03.ScriptableObjects/UnitStats/" + id + ".asset");
            Sprite portrait = LoadSprite(ROOT + "/Portraits/" + id + ".png");
            if (stat == null) throw new InvalidOperationException("UnitStatData 누락: " + id);
            var e = entries.GetArrayElementAtIndex(faction * 6 + i);
            e.FindPropertyRelative("_id").stringValue = "unit." + stat.unitId;
            e.FindPropertyRelative("_displayName").stringValue = stat.displayName;
            e.FindPropertyRelative("_description").stringValue = (faction == 0 ? "마왕군" : "인간군") + " · " + stat.displayName;
            e.FindPropertyRelative("_portrait").objectReferenceValue = portrait;
            e.FindPropertyRelative("_faction").enumValueIndex = faction;
            e.FindPropertyRelative("_defaultUnlocked").boolValue = i < 3;
        }
        so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(catalog, path); return catalog;
    }

    private static UIAchievementCatalogSO CreateAchievementCatalog()
    {
        string path = ROOT + "/AchievementCatalog.asset";
        var existing = AssetDatabase.LoadAssetAtPath<UIAchievementCatalogSO>(path); if (existing != null) return existing;
        string[] names = { "첫 방어", "첫 관문 돌파", "전장의 수련자", "용사 사냥꾼 I", "용사 사냥꾼 II", "거인의 몰락", "성장의 시작", "힘의 조율", "전투 준비 완료" };
        string[] descriptions = { "첫 전투에서 승리하기", "관문 5회 돌파하기", "전투 10회 완료하기", "용사 100명 처치하기", "용사 500명 처치하기", "거인 1회 처치하기", "성장 10회 달성하기", "힘의 조율 5회 달성하기", "장착 슬롯 3개 채우기" };
        int[] targets = { 1, 5, 10, 100, 500, 1, 10, 5, 3 };
        var catalog = ScriptableObject.CreateInstance<UIAchievementCatalogSO>();
        var so = new SerializedObject(catalog); var entries = so.FindProperty("_entries"); entries.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var e = entries.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("_id").stringValue = ACHIEVEMENT_IDS[i];
            e.FindPropertyRelative("_displayName").stringValue = names[i];
            e.FindPropertyRelative("_description").stringValue = descriptions[i];
            e.FindPropertyRelative("_icon").objectReferenceValue = Art("AchievementIcon_" + i.ToString("00"));
            e.FindPropertyRelative("_target").intValue = targets[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(catalog, path); return catalog;
    }

    // 진행도는 장식 이미지가 아니라 일정한 폭의 UI 데이터 표시용 단색 Sprite로 채운다.
    public static Sprite GetProgressPixel()
    {
        string path = ROOT + "/Styles/ProgressPixel.asset";
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite != null) return sprite;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "ProgressPixel", filterMode = FilterMode.Point };
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        sprite.name = "ProgressPixel"; AssetDatabase.AddObjectToAsset(sprite, texture); AssetDatabase.SaveAssetIfDirty(texture);
        return sprite;
    }

    private static ScrollRect CreateGrid(Transform parent, Vector2 position, Vector2 size, int columns, Vector2 cell, Vector2 spacing, out RectTransform content)
    {
        var viewport = Rect("Viewport", parent, position, size);
        var surface = viewport.gameObject.AddComponent<Image>(); surface.color = Color.clear; surface.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(8, 12);
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
        content = Rect("Content", viewport, Vector2.zero, new Vector2(0, size.y));
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
        var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = cell; grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columns; grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(0, 0, 0, 0);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content; scroll.viewport = viewport; return scroll;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = pos; return rect;
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Image Picture(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = sprite != null; image.raycastTarget = false; return image;
    }
    private static TMP_Text Label(string name, Transform parent, string text, Vector2 pos, Vector2 size, float fontSize)
    {
        var label = Rect(name, parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font; label.fontSharedMaterial = _textMaterial; label.text = text; label.fontSize = fontSize; label.color = IVORY;
        label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true; label.fontSizeMin = fontSize * .78f; label.fontSizeMax = fontSize;
        label.raycastTarget = false; return label;
    }
    private static UISkillArtButton Button(string name, Transform parent, string text, Vector2 pos, Vector2 size, float fontSize, Sprite sprite)
    {
        Image image = Picture(name, parent, sprite, pos, size); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<UISkillArtButton>(); button.targetGraphic = image;
        SetButtonSprites(button, sprite, sprite, sprite);
        Assign(button, "_label", Label("Label", button.transform, text, Vector2.zero, new Vector2(size.x - 32, size.y - 12), fontSize));
        var so = new SerializedObject(button); so.FindProperty("_separatePointerHover").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
        var colors = button.colors; colors.normalColor = new Color(.88f, .86f, .85f); colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(.62f, .57f, .52f); button.colors = colors; return button;
    }
    private static void SetButtonSprites(UISkillArtButton button, Sprite normal, Sprite hover, Sprite chosen)
    {
        Assign(button, "_normal", normal); Assign(button, "_hover", hover); Assign(button, "_pressed", hover); Assign(button, "_chosen", chosen); Assign(button, "_disabled", normal);
    }
    private static void Panel(string name, Transform root, Vector2 pos, Vector2 size)
    {
        Image image = Picture(name, root, LoadSprite(OVERLAYS + "/Sprites/Panel_Frame.png"), pos, size);
        image.type = Image.Type.Sliced; image.preserveAspect = false; image.pixelsPerUnitMultiplier = 3;
    }
    private static Sprite Art(string name) => LoadSprite(ROOT + "/Sprites/" + name + ".png");
    private static Sprite LoadSprite(string path, bool required = true)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (required && sprite == null) throw new InvalidOperationException("Sprite 누락: " + path); return sprite;
    }
    private static void Assign(Object target, string field, Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    private static void AssignArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target); var array = so.FindProperty(field); array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo(); if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return; int last = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, last)); AssetDatabase.CreateFolder(path.Substring(0, last), path.Substring(last + 1));
    }
    private static void ImportSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
    }
}
