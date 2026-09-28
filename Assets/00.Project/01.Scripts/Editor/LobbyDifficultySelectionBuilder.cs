using System;
using System.Linq;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>이미 분리된 로비 카드에 난이도 선택을 연결한다. 다른 화면/씬과 원본 리소스는 변경하지 않는다.</summary>
public static class LobbyDifficultySelectionBuilder
{
    public const string SCENE_PATH = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    public const string ASSET_ROOT = "Assets/06.UI/LobbyMutedPreview/DifficultySelection_v1";
    public const string CATALOG_PATH = ASSET_ROOT + "/Data/LobbyDifficultyCatalog.asset";
    public const string CASTLE_SET_PATH = ASSET_ROOT + "/Data/DifficultyArtwork_Castle.asset";
    public const string HERO_SET_PATH = ASSET_ROOT + "/Data/DifficultyArtwork_Hero.asset";
    private const string UNDO_NAME = "Configure lobby difficulty selection";

    [MenuItem("Tools/OZGL2/Lobby/Configure Difficulty Selection (Current Lobby Only)")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != SCENE_PATH)
            throw new InvalidOperationException("UI_Lobby_MutedPreview 씬에서만 실행할 수 있습니다.");
        GameObject lobby = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas_Lobby");
        Transform stage = lobby == null ? null : lobby.transform.Find("StageSelection");
        if (stage == null) throw new InvalidOperationException("Canvas_Lobby/StageSelection이 없습니다.");
        if (stage.TryGetComponent(out UILobbyDifficultySelector existing))
        {
            UILobbyDifficultySelectorEditor.RefreshWithUndo(existing);
            return; // 재실행으로 사용자의 카탈로그/시간/배치를 덮어쓰지 않는다.
        }

        string[] names = { "Castle_Easy", "Castle_Normal", "Castle_Hard", "Hero_Easy", "Hero_Normal", "Hero_Hard" };
        foreach (string name in names)
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(ASSET_ROOT + "/Artwork/" + name + ".png") == null)
                throw new InvalidOperationException("필요한 그림 없음: " + name);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        Undo.RegisterFullObjectHierarchyUndo(stage.gameObject, UNDO_NAME);
        foreach (string name in names) ImportArtwork(name);
        EnsureFolder(ASSET_ROOT + "/Data");
        LobbyDifficultyCatalogSO catalog = CreateCatalogIfMissing();
        CreateArtworkSetIfMissing(CASTLE_SET_PATH, "01 마왕성 공성전", "Castle");
        LobbyDifficultyArtworkSetSO hero = CreateArtworkSetIfMissing(HERO_SET_PATH, "02 용사군의 위협", "Hero");

        UILobbyDifficultySlotView previous = ConfigureSlot(stage.Find("PreviousStage"), "PreviousCaption", "Label", null);
        UILobbyDifficultySlotView current = ConfigureSlot(stage.Find("CurrentStage"), "StageRecord", "RecordText", "ClearTimeText");
        UILobbyDifficultySlotView next = ConfigureSlot(stage.Find("NextStage_Locked"), "LockedCaption", "Label", null);
        // 잠금 목업에만 있었던 회색 틴트를 해제하여 양옆 프레임을 같은 중립색으로 표시한다.
        next.transform.Find("VisualLayers/Frame").GetComponent<Image>().color = Color.white;

        foreach (string arrowPath in new[] { "PreviousArrow", "NextArrow" })
        {
            Button arrow = stage.Find(arrowPath).GetComponent<Button>();
            ColorBlock colors = arrow.colors;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.55f);
            arrow.colors = colors;
        }

        UILobbyDifficultySelector selector = Undo.AddComponent<UILobbyDifficultySelector>(stage.gameObject);
        SerializedObject so = new SerializedObject(selector);
        SetReference(so, "_catalog", catalog);
        SetReference(so, "_artworkSet", hero);
        SetReference(so, "_previousSlot", previous);
        SetReference(so, "_currentSlot", current);
        SetReference(so, "_nextSlot", next);
        SetReference(so, "_previousButton", stage.Find("PreviousArrow").GetComponent<Button>());
        SetReference(so, "_nextButton", stage.Find("NextArrow").GetComponent<Button>());
        SetReference(so, "_startButton", stage.Find("CurrentStage/StartButton").GetComponent<Button>());
        so.FindProperty("_initialDifficulty").enumValueIndex = (int)eLobbyDifficulty.NORMAL;
        so.ApplyModifiedProperties();
        selector.RefreshPresentation();
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = stage.gameObject;
        Debug.Log("난이도 선택 연결 완료: StageSelection의 슬롯 3개, 좌우 입력, 카탈로그, 그림 세트 2종. 씬은 검증 후 저장하세요.", selector);
    }

    private static UILobbyDifficultySlotView ConfigureSlot(Transform root, string captionPath, string namePath, string descriptionPath)
    {
        if (root == null || !root.TryGetComponent(out UILobbyStageCardView card))
            throw new InvalidOperationException("분리형 UILobbyStageCardView가 필요합니다.");
        Transform visual = root.Find("VisualLayers");
        Transform frame = visual.Find("Frame");
        Transform artRoot = visual.Find("ArtworkMask/ArtworkRoot");
        Transform artwork = artRoot.Find("Artwork");
        Transform caption = root.Find(captionPath);
        if (artwork == null || caption == null) throw new InvalidOperationException("Artwork 또는 캡션 참조 없음.");

        GameObject motionObject = new GameObject("ArtworkMotion", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(motionObject, UNDO_NAME);
        motionObject.layer = root.gameObject.layer;
        RectTransform motion = (RectTransform)motionObject.transform;
        motion.SetParent(artRoot, false);
        motion.SetSiblingIndex(artwork.GetSiblingIndex());
        SetStretch(motion);
        Undo.SetTransformParent(artwork, motion, UNDO_NAME);
        SetStretch((RectTransform)artwork);

        UILobbyDifficultySlotView slot = Undo.AddComponent<UILobbyDifficultySlotView>(root.gameObject);
        SerializedObject so = new SerializedObject(slot);
        SetReference(so, "_card", card);
        SetReference(so, "_visualGroup", GetVisualGroup(visual));
        SetReference(so, "_frameGroup", GetVisualGroup(frame));
        SetReference(so, "_captionGroup", GetVisualGroup(caption));
        SetReference(so, "_artworkGroup", GetVisualGroup(motion));
        SetReference(so, "_artworkMotion", motion);
        SetReference(so, "_nameLabel", caption.Find(namePath).GetComponent<TMP_Text>());
        if (!string.IsNullOrEmpty(descriptionPath))
            SetReference(so, "_descriptionLabel", caption.Find(descriptionPath).GetComponent<TMP_Text>());
        so.ApplyModifiedProperties();
        return slot;
    }

    private static CanvasGroup GetVisualGroup(Transform target)
    {
        if (!target.TryGetComponent(out CanvasGroup group)) group = Undo.AddComponent<CanvasGroup>(target.gameObject);
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void ImportArtwork(string name)
    {
        string path = ASSET_ROOT + "/Artwork/" + name + ".png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("TextureImporter 없음: " + path);
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
    }

    private static LobbyDifficultyCatalogSO CreateCatalogIfMissing()
    {
        LobbyDifficultyCatalogSO existing = AssetDatabase.LoadAssetAtPath<LobbyDifficultyCatalogSO>(CATALOG_PATH);
        if (existing != null) return existing;
        LobbyDifficultyCatalogSO catalog = ScriptableObject.CreateInstance<LobbyDifficultyCatalogSO>();
        SerializedObject so = new SerializedObject(catalog);
        SerializedProperty entries = so.FindProperty("_entries");
        entries.arraySize = 3;
        string[] labels = { "쉬움", "보통", "어려움" };
        string[] descriptions = { "소수 정찰대를 상대하는 방어전", "중무장 기사단과의 전투", "정예 성기사의 대규모 침공" };
        for (int i = 0; i < 3; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("_difficulty").enumValueIndex = i;
            entry.FindPropertyRelative("_displayName").stringValue = labels[i];
            entry.FindPropertyRelative("_description").stringValue = descriptions[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
        AssetDatabase.SaveAssetIfDirty(catalog);
        return catalog;
    }

    private static LobbyDifficultyArtworkSetSO CreateArtworkSetIfMissing(string path, string label, string prefix)
    {
        LobbyDifficultyArtworkSetSO existing = AssetDatabase.LoadAssetAtPath<LobbyDifficultyArtworkSetSO>(path);
        if (existing != null) return existing;
        LobbyDifficultyArtworkSetSO set = ScriptableObject.CreateInstance<LobbyDifficultyArtworkSetSO>();
        SerializedObject so = new SerializedObject(set);
        so.FindProperty("_displayName").stringValue = label;
        SetReference(so, "_easy", LoadArtwork(prefix + "_Easy"));
        SetReference(so, "_normal", LoadArtwork(prefix + "_Normal"));
        SetReference(so, "_hard", LoadArtwork(prefix + "_Hard"));
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(set, path);
        AssetDatabase.SaveAssetIfDirty(set);
        return set;
    }

    private static Sprite LoadArtwork(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ASSET_ROOT + "/Artwork/" + name + ".png");
    private static void SetReference(SerializedObject so, string field, UnityEngine.Object value) => so.FindProperty(field).objectReferenceValue = value;

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
