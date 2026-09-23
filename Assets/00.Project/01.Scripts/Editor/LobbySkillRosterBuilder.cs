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

// 부모 Prefab에 보존된 스킬 화면만 확장한다. 원본 Skills_v1과 다른 Overlay/Scene override는 수정하지 않는다.
public static class LobbySkillRosterBuilder
{
    public const string ROOT = "Assets/06.UI/LobbyMutedPreview/Skills_v2";
    public const string CATALOG_PATH = ROOT + "/SkillRosterCatalog.asset";
    private const string JSON_PATH = "Tools/Art/SkillRoster_v2.json";
    private const string SCENE_PATH = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private const string PREFAB_PATH = "Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab";
    private const string CARD_ORNAMENT = ROOT + "/Sprites/Ultimate_Card_Ornament.png";
    private const string EQUIPPED_ORNAMENT = ROOT + "/Sprites/Ultimate_Equipped_Ornament.png";
    private const string UNDO_NAME = "Build 19 skill roster in lobby overlays";
    private const int SKILL_COUNT = 19;
    private static readonly int[] INITIAL_EQUIPPED = { 0, 2, 5 };

    [Serializable]
    private sealed class RosterDocument { public RosterEntry[] Entries = Array.Empty<RosterEntry>(); }

    [Serializable]
    private sealed class RosterEntry
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Category = string.Empty;
        public int Tier = 1;
        public int UnlockSp = 0;
        public string Activation = string.Empty;
        public int CooldownSeconds = 0;
        public string EffectLabel = string.Empty;
        public string EffectValue = string.Empty;
        public bool IsUltimate = false;
        public bool DefaultUnlocked = true;
        public string IconPath = string.Empty;
        public string StatsText = string.Empty;
    }

    [MenuItem("Tools/OZGL2/Lobby/Build Skill Roster V2 (19 Skills)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("스킬 목록 변경은 Edit Mode에서 실행하세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if ((stage != null && stage.assetPath != PREFAB_PATH) ||
            (stage == null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != SCENE_PATH))
            throw new InvalidOperationException("UI_Lobby_MutedPreview 또는 Canvas_LobbyOverlays Prefab Mode에서 실행하세요.");
        ReadRoster(); // 데이터/이미지 누락은 현재 편집 화면을 바꾸기 전에 확인한다.
        if (stage == null) stage = PrefabStageUtility.OpenPrefab(PREFAB_PATH);
        string result = Configure(stage.prefabContentsRoot);
        EditorSceneManager.MarkSceneDirty(stage.scene);
        Debug.Log(result + "\nPrefab Mode에서 Undo 지원. Scene 저장/부모 전체 Apply는 호출하지 않습니다. Prefab 저장은 편집 결과를 확인한 뒤 실행하세요.", stage.prefabContentsRoot);
    }

    // LoadPrefabContents로 호출할 수도 있다. 그 경우 SaveAsPrefabAsset/Unload는 호출자의 책임이며 디스크 저장 Undo는 보장하지 않는다.
    public static string Configure(GameObject overlayRoot)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || overlayRoot == null || overlayRoot.name != "Canvas_LobbyOverlays" ||
            !overlayRoot.TryGetComponent(out UILobbyOverlayView _) || !EditorSceneManager.IsPreviewScene(overlayRoot.scene))
            throw new InvalidOperationException("승인된 Canvas_LobbyOverlays의 Prefab Mode/LoadPrefabContents 루트만 처리합니다.");
        var skill = overlayRoot.transform.Find("Canvas_SkillSettings");
        if (skill == null || !skill.TryGetComponent(out UISkillLoadoutPreview view))
            throw new InvalidOperationException("부모 안의 기존 Canvas_SkillSettings가 필요합니다.");
        if (PrefabUtility.IsAnyPrefabInstanceRoot(skill.gameObject))
            throw new InvalidOperationException("스킬이 별도 중첩 Prefab으로 바뀌었습니다. 원본/부모 override 적용 범위를 다시 확인하세요.");
        var gridRoot = skill.Find("SkillGrid") as RectTransform;
        var so = new SerializedObject(view);
        var existingCards = GetRequired(so, "_cards");
        if (existingCards.arraySize != 12 && existingCards.arraySize != SKILL_COUNT)
            throw new InvalidOperationException("기존 12장 또는 이미 변환된 19장 스킬 목록만 처리합니다.");
        var template = existingCards.GetArrayElementAtIndex(0).objectReferenceValue as UISkillArtButton;
        var style = GetRequired(so, "_categoryStyle").objectReferenceValue as UISkillCategoryStyleSO;
        var description = GetRequired(so, "_detailDescription").objectReferenceValue as TMP_Text;
        var detailIcon = GetRequired(so, "_detailIcon").objectReferenceValue as Image;
        var detailFrame = skill.Find("DetailIconFrame");
        var lockTemplate = template != null ? template.transform.Find("UnlockLock")?.GetComponent<Image>() : null;
        if (gridRoot == null || template == null || !template.transform.IsChildOf(gridRoot) || style == null ||
            description == null || detailIcon == null || detailFrame == null || lockTemplate == null || lockTemplate.sprite == null)
            throw new InvalidOperationException("현재 스킬 Grid/카드/분류 스타일/설명/잠금 아트 연결을 먼저 확인하세요.");
        if (!description.transform.IsChildOf(skill) || !detailIcon.transform.IsChildOf(skill))
            throw new InvalidOperationException("스킬 밖의 설명/아이콘 참조는 변경하지 않습니다.");
        foreach (string field in new[] { "_equippedIcons", "_equippedFrames", "_equippedSlotTints" })
            if (GetRequired(so, field).arraySize != INITIAL_EQUIPPED.Length)
                throw new InvalidOperationException("장착 슬롯 배열은 3개여야 합니다: " + field);
        foreach (string field in new[] { "_ownedScrollRect", "_cardUltimateFrames", "_detailUltimateFrame", "_equippedUltimateFrames", "_detailMetadata" })
            GetRequired(so, field);
        var entries = ReadRoster();
        Sprite[] icons = entries.Select(entry => LoadSprite(entry.IconPath)).ToArray();
        Sprite cardOrnament = LoadSprite(CARD_ORNAMENT);
        Sprite equippedOrnament = LoadSprite(EQUIPPED_ORNAMENT);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        Undo.RegisterFullObjectHierarchyUndo(skill.gameObject, UNDO_NAME);
        try
        {
            var catalog = ConfigureCatalog(entries, icons);
            ScrollRect scroll = ConfigureScroll(gridRoot);
            RectTransform content = scroll.content;
            var cards = new UISkillArtButton[SKILL_COUNT];
            var cardIcons = new Image[SKILL_COUNT];
            var tints = new Image[SKILL_COUNT];
            var locks = new Image[SKILL_COUNT];
            var ornaments = new Image[SKILL_COUNT];
            for (int i = 0; i < SKILL_COUNT; i++)
            {
                string name = "SkillCard_" + i.ToString("00");
                Transform existing = content.Find(name);
                if (existing == null)
                {
                    var copy = Object.Instantiate(template.gameObject, content, false);
                    copy.name = name;
                    Undo.RegisterCreatedObjectUndo(copy, UNDO_NAME);
                    existing = copy.transform;
                }
                cards[i] = existing.GetComponent<UISkillArtButton>();
                cardIcons[i] = existing.Find("Icon")?.GetComponent<Image>();
                tints[i] = existing.Find("CategorySlotTint")?.GetComponent<Image>();
                locks[i] = existing.Find("UnlockLock")?.GetComponent<Image>();
                if (cards[i] == null || cardIcons[i] == null || tints[i] == null || locks[i] == null)
                    throw new InvalidOperationException("카드 구성 누락: " + name);
                existing.SetSiblingIndex(i);
                // 비활성 Prefab 조립 중 부모 이동으로 생길 수 있는 0 배율을 기존 카드 기준으로 복원한다.
                existing.localScale = Vector3.one;
                existing.localRotation = Quaternion.identity;
                existing.gameObject.SetActive(true);
                // 기존 normal/hover/chosen Sprite와 targetGraphic은 그대로 둔다.
                for (int listener = cards[i].onClick.GetPersistentEventCount() - 1; listener >= 0; listener--)
                    if (cards[i].onClick.GetPersistentTarget(listener) is UISkillLoadoutPreview &&
                        cards[i].onClick.GetPersistentMethodName(listener) == nameof(UISkillLoadoutPreview.SelectSkill))
                        UnityEventTools.RemovePersistentListener(cards[i].onClick, listener);
                UnityEventTools.AddIntPersistentListener(cards[i].onClick, view.SelectSkill, i);
                ApplyCategory(cardIcons[i], tints[i], icons[i], style, ParseCategory(entries[i]));
                locks[i].enabled = !entries[i].DefaultUnlocked;
                locks[i].raycastTarget = false;
                ornaments[i] = EnsureOrnament(existing, cardOrnament, entries[i].IsUltimate && entries[i].DefaultUnlocked);
                locks[i].transform.SetAsLastSibling();
                cards[i].SetChosen(i == 0);
            }

            var equippedOrnaments = new Image[INITIAL_EQUIPPED.Length];
            var equippedImages = GetRequired(so, "_equippedIcons");
            var equippedFrames = GetRequired(so, "_equippedFrames");
            var equippedTints = GetRequired(so, "_equippedSlotTints");
            for (int i = 0; i < INITIAL_EQUIPPED.Length; i++)
            {
                int index = INITIAL_EQUIPPED[i];
                var frame = equippedFrames.GetArrayElementAtIndex(i).objectReferenceValue as Image;
                var icon = equippedImages.GetArrayElementAtIndex(i).objectReferenceValue as Image;
                var tint = equippedTints.GetArrayElementAtIndex(i).objectReferenceValue as Image;
                if (frame == null || icon == null || tint == null) throw new InvalidOperationException("장착 슬롯 참조 누락: " + i);
                var category = ParseCategory(entries[index]);
                frame.sprite = style.GetEquippedFrame(category);
                frame.material = null;
                ApplyCategory(icon, tint, icons[index], style, category);
                // FrameArt의 기존 배율/위치 보정을 따라가되, 선택/호버 대상과 별개 레이어로 둔다.
                equippedOrnaments[i] = EnsureOrnament(frame.transform, equippedOrnament, entries[index].IsUltimate);
            }
            Image detailOrnament = EnsureOrnament(detailFrame, cardOrnament, entries[0].IsUltimate);
            TMP_Text metadata = ConfigureMetadata(skill, description);
            var detailTint = GetRequired(so, "_detailSlotTint").objectReferenceValue as Image;
            ApplyCategory(detailIcon, detailTint, icons[0], style, ParseCategory(entries[0]));
            description.text = Describe(entries[0]);
            metadata.text = Metadata(entries[0]);
            SetLabel(so, "_detailName", entries[0].Name);
            SetLabel(so, "_effectLabel", entries[0].EffectLabel);
            SetLabel(so, "_effectValue", entries[0].EffectValue);
            SetLabel(so, "_cooldown", entries[0].CooldownSeconds + "초");
            ConfigureStatLabel(so, "_effectLabel", 30f);
            ConfigureStatLabel(so, "_effectValue", 32f);
            Assign(so, "_catalog", catalog);
            Assign(so, "_ownedScrollRect", scroll);
            Assign(so, "_detailMetadata", metadata);
            Assign(so, "_detailUltimateFrame", detailOrnament);
            AssignArray(so, "_cards", cards);
            AssignArray(so, "_cardIcons", cardIcons);
            AssignArray(so, "_cardSlotTints", tints);
            AssignArray(so, "_cardLockIcons", locks);
            AssignArray(so, "_cardUltimateFrames", ornaments);
            AssignArray(so, "_equippedUltimateFrames", equippedOrnaments);
            var initial = GetRequired(so, "_initialEquipped");
            initial.arraySize = INITIAL_EQUIPPED.Length;
            for (int i = 0; i < INITIAL_EQUIPPED.Length; i++) initial.GetArrayElementAtIndex(i).intValue = INITIAL_EQUIPPED[i];
            GetRequired(so, "_initialSelected").intValue = 0;
            so.ApplyModifiedProperties();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            // 비활성 Prefab의 Canvas bounds에 의존하지 않고 상단 고정 위치를 저장한다.
            scroll.content.anchoredPosition = Vector2.zero;
            EditorUtility.SetDirty(view);
            Undo.CollapseUndoOperations(group);
            return "19종 스킬 카탈로그/3열 스크롤/궁극기 전용 장식/상세 메타데이터 연결 완료. 변경 대상: " +
                "Canvas_LobbyOverlays/Canvas_SkillSettings 하위 카드·스크롤·상세 텍스트·장식 및 " + CATALOG_PATH +
                ". 초기 장착: 화염구/연쇄 번개/공허 붕괴. 원본 스킬 Prefab과 Scene은 저장하지 않았습니다. 새 카탈로그/폴더 생성 자체는 Undo 삭제 대상이 아닙니다.";
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    private static ScrollRect ConfigureScroll(RectTransform gridRoot)
    {
        var viewport = EnsureRect(gridRoot, "Viewport");
        Stretch(viewport);
        var graphic = viewport.GetComponent<Image>() ?? Undo.AddComponent<Image>(viewport.gameObject);
        graphic.color = Color.clear;
        graphic.raycastTarget = true;
        var stencilMask = viewport.GetComponent<Mask>();
        if (stencilMask != null) Undo.DestroyObjectImmediate(stencilMask);
        var mask = viewport.GetComponent<RectMask2D>() ?? Undo.AddComponent<RectMask2D>(viewport.gameObject);
        mask.softness = new Vector2Int(0, 6);
        var content = EnsureRect(viewport, "Content");
        var layout = content.GetComponent<GridLayoutGroup>() ?? Undo.AddComponent<GridLayoutGroup>(content.gameObject);
        var originalLayout = gridRoot.GetComponent<GridLayoutGroup>();
        if (originalLayout != null)
        {
            EditorUtility.CopySerialized(originalLayout, layout);
            Undo.DestroyObjectImmediate(originalLayout);
        }
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.childAlignment = TextAnchor.UpperCenter;
        foreach (var card in gridRoot.GetComponentsInChildren<UISkillArtButton>(true))
            if (card.transform.parent == gridRoot) Undo.SetTransformParent(card.transform, content, UNDO_NAME);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
        content.localScale = Vector3.one;
        content.localRotation = Quaternion.identity;
        var fitter = content.GetComponent<ContentSizeFitter>() ?? Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = gridRoot.GetComponent<ScrollRect>() ?? Undo.AddComponent<ScrollRect>(gridRoot.gameObject);
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        scroll.inertia = true;
        return scroll;
    }

    private static TMP_Text ConfigureMetadata(Transform skill, TMP_Text description)
    {
        var existing = skill.Find("Skill_Metadata");
        TMP_Text metadata;
        if (existing == null)
        {
            var rect = EnsureRect(skill, "Skill_Metadata");
            metadata = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
            metadata.font = description.font;
            metadata.fontSharedMaterial = description.fontSharedMaterial;
            metadata.color = description.color;
            var source = description.rectTransform;
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.localScale = source.localScale;
            rect.localRotation = source.localRotation;
            float oldTop = source.anchoredPosition.y + source.rect.yMax;
            rect.anchoredPosition = new Vector2(source.anchoredPosition.x, oldTop - 12f);
            rect.sizeDelta = new Vector2(source.sizeDelta.x, 24f);
            // 설명 하단은 유지하고 첫 28 UI 단위만 메타데이터에 양보한다. 재실행 시 다시 줄이지 않는다.
            source.sizeDelta = new Vector2(source.sizeDelta.x, Mathf.Max(80f, source.sizeDelta.y - 28f));
            source.anchoredPosition -= new Vector2(0f, 14f);
        }
        else metadata = existing.GetComponent<TMP_Text>();
        if (metadata == null) throw new InvalidOperationException("Skill_Metadata에는 TMP_Text가 필요합니다.");
        metadata.raycastTarget = false;
        metadata.alignment = TextAlignmentOptions.Center;
        metadata.textWrappingMode = TextWrappingModes.NoWrap;
        metadata.enableAutoSizing = true;
        metadata.fontSize = 20f; metadata.fontSizeMin = 16f; metadata.fontSizeMax = 20f;
        description.raycastTarget = false;
        description.alignment = TextAlignmentOptions.TopLeft;
        description.textWrappingMode = TextWrappingModes.Normal;
        description.enableAutoSizing = true;
        description.fontSize = 24f; description.fontSizeMin = 20f; description.fontSizeMax = 24f;
        description.overflowMode = TextOverflowModes.Ellipsis;
        return metadata;
    }

    private static UISkillPreviewCatalogSO ConfigureCatalog(RosterEntry[] entries, Sprite[] icons)
    {
        EnsureFolder(ROOT);
        var catalog = AssetDatabase.LoadAssetAtPath<UISkillPreviewCatalogSO>(CATALOG_PATH);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<UISkillPreviewCatalogSO>();
            AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
        }
        Undo.RecordObject(catalog, UNDO_NAME);
        var so = new SerializedObject(catalog);
        var serializedEntries = GetRequired(so, "_entries");
        serializedEntries.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            var source = entries[i];
            var entry = serializedEntries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("_id").stringValue = source.Id;
            entry.FindPropertyRelative("_displayName").stringValue = source.Name;
            entry.FindPropertyRelative("_description").stringValue = Describe(source);
            entry.FindPropertyRelative("_category").enumValueIndex = (int)ParseCategory(source);
            entry.FindPropertyRelative("_icon").objectReferenceValue = icons[i];
            entry.FindPropertyRelative("_defaultUnlocked").boolValue = source.DefaultUnlocked;
            entry.FindPropertyRelative("_isArcane").boolValue = false;
            entry.FindPropertyRelative("_isUltimate").boolValue = source.IsUltimate;
            entry.FindPropertyRelative("_tier").intValue = source.Tier;
            entry.FindPropertyRelative("_unlockSp").intValue = source.UnlockSp;
            entry.FindPropertyRelative("_activation").stringValue = source.Activation;
            entry.FindPropertyRelative("_effectLabel").stringValue = source.EffectLabel;
            entry.FindPropertyRelative("_effectValue").stringValue = source.EffectValue;
            entry.FindPropertyRelative("_cooldown").stringValue = source.CooldownSeconds + "초";
        }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(catalog);
        return catalog;
    }

    private static RosterEntry[] ReadRoster()
    {
        if (!File.Exists(JSON_PATH)) throw new InvalidOperationException("스킬 표 JSON이 없습니다: " + JSON_PATH);
        string json = File.ReadAllText(JSON_PATH).Trim();
        var document = JsonUtility.FromJson<RosterDocument>(json.StartsWith("[", StringComparison.Ordinal) ? "{\"Entries\":" + json + "}" : json);
        var entries = document?.Entries;
        if (entries == null || entries.Length != SKILL_COUNT || entries.Any(entry => entry == null))
            throw new InvalidOperationException("스킬 표는 누락 없는 19개 항목이어야 합니다.");
        if (entries.Select(entry => entry.Id).Distinct().Count() != SKILL_COUNT)
            throw new InvalidOperationException("스킬 ID가 중복됐습니다.");
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Name) ||
                entry.Tier < 1 || entry.UnlockSp < 0 || entry.CooldownSeconds < 0)
                throw new InvalidOperationException("스킬 표의 ID/이름/티어/SP/재사용을 확인하세요: " + entry.Name);
            var category = ParseCategory(entry);
            if (entry.IsUltimate && category != eSkillPreviewCategory.DAMAGE)
                throw new InvalidOperationException("궁극기는 DAMAGE 표시 분류를 사용합니다: " + entry.Name);
            CheckImagePath(entry.IconPath);
        }
        CheckImagePath(CARD_ORNAMENT);
        CheckImagePath(EQUIPPED_ORNAMENT);
        return entries;
    }

    private static void CheckImagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/06.UI/LobbyMutedPreview/", StringComparison.Ordinal) ||
            !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.Contains("..") || !File.Exists(path))
            throw new InvalidOperationException("승인된 스킬 아이콘/장식 경로가 비었거나 파일이 없습니다: " + path);
    }

    private static Sprite LoadSprite(string path)
    {
        // 신규 v2 에셋만 Sprite로 임포트한다. 기존 원본 아트의 임포트 설정은 변경하지 않는다.
        if (path.StartsWith(ROOT + "/", StringComparison.Ordinal))
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) throw new InvalidOperationException("TextureImporter 없음: " + path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool needsImport = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp || settings.spriteMeshType != SpriteMeshType.FullRect;
            if (needsImport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Sprite 연결 실패: " + path);
        return sprite;
    }

    private static eSkillPreviewCategory ParseCategory(RosterEntry entry)
    {
        if (!Enum.TryParse(entry.Category, out eSkillPreviewCategory category) || !Enum.IsDefined(typeof(eSkillPreviewCategory), category))
            throw new InvalidOperationException("분류는 DAMAGE/BUFF/DEBUFF여야 합니다: " + entry.Name);
        return category;
    }

    private static string Describe(RosterEntry entry) => entry.Description;
    private static string Metadata(RosterEntry entry) => "T" + entry.Tier + " · " + entry.Activation + " · 해금 " + entry.UnlockSp + " SP";

    private static void ApplyCategory(Image icon, Image tint, Sprite sprite, UISkillCategoryStyleSO style, eSkillPreviewCategory category)
    {
        icon.sprite = sprite; icon.material = style.GetIconMaterial(category); icon.color = Color.white; icon.raycastTarget = false;
        if (tint == null) return;
        tint.material = style.SlotTintMaterial; tint.color = style.GetSlotColor(category); tint.raycastTarget = false;
    }

    private static Image EnsureOrnament(Transform parent, Sprite sprite, bool enabled)
    {
        var rect = EnsureRect(parent, "UltimateOrnament");
        Stretch(rect);
        var image = rect.GetComponent<Image>() ?? Undo.AddComponent<Image>(rect.gameObject);
        image.sprite = sprite; image.color = Color.white; image.material = null;
        image.type = Image.Type.Simple; image.preserveAspect = false; image.raycastTarget = false; image.enabled = enabled;
        return image;
    }

    private static RectTransform EnsureRect(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing as RectTransform ?? throw new InvalidOperationException("RectTransform 필요: " + name);
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }

    private static SerializedProperty GetRequired(SerializedObject so, string name) => so.FindProperty(name) ?? throw new InvalidOperationException("필드 없음: " + name);
    private static void Assign(SerializedObject so, string name, Object value) => GetRequired(so, name).objectReferenceValue = value;
    private static void AssignArray(SerializedObject so, string name, Object[] values)
    {
        var property = GetRequired(so, name); property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
    private static void SetLabel(SerializedObject so, string name, string text)
    {
        if (GetRequired(so, name).objectReferenceValue is TMP_Text label) label.text = text;
    }
    private static void ConfigureStatLabel(SerializedObject so, string name, float maximumSize)
    {
        if (!(GetRequired(so, name).objectReferenceValue is TMP_Text label)) return;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = maximumSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
