using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 승인된 전투 Canvas 안에만 표시용 슬롯을 배치한다. 저장/시전 시스템은 연결하지 않는다.
public static class CombatSkillHudBuilder
{
    public const string SCENE_PATH = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    public const string ROOT = "Assets/06.UI/BattleMutedPreview/CombatSkills_v1";
    public const string PREFAB_PATH = ROOT + "/Prefabs/CombatSkillSlot.prefab";
    private const string STYLE_PATH = "Assets/06.UI/LobbyMutedPreview/Skills_v1/Styles/SkillCategoryStyle.asset";
    private const string CATALOG_PATH = "Assets/06.UI/LobbyMutedPreview/Skills_v2/SkillRosterCatalog.asset";
    private const string FONT_PATH = "Assets/06.UI/BattleMutedPreview/Overlays_v1/Fonts/BattleOverlay Pixel.asset";
    private const string MENU_PATH = "Tools/OZGL2/Battle/Build Combat Skill HUD (Noble Diamond)";
    private const string UNDO_NAME = "Build combat skill HUD (Noble Diamond)";
    private const string BACKGROUND_NAME = "BottomSkillBackground";
    private static readonly string[] SPRITE_NAMES = { "Frame_Damage", "Frame_Buff", "Frame_Debuff", "Cooldown_Ring", "Bottom_Velvet" };
    private static readonly string[] SAMPLE_IDS = { "ui_preview_fire", "ui_preview_iron_skin", "ui_preview_time_stop" };
    private static readonly eSkillPreviewCategory[] SAMPLE_CATEGORIES = { eSkillPreviewCategory.DAMAGE, eSkillPreviewCategory.BUFF, eSkillPreviewCategory.DEBUFF };
    private static readonly float[] SAMPLE_DURATIONS = { 12f, 25f, 40f };
    private static readonly Color FACE = new Color32(248, 242, 235, 255);

    private sealed class BuildAssets
    {
        public UISkillCategoryStyleSO Style;
        public UISkillPreviewCatalogSO Catalog;
        public TMP_FontAsset Font;
        public readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    }

    [MenuItem(MENU_PATH)]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null ||
            !scene.IsValid() || !scene.isLoaded || scene.path != SCENE_PATH)
            throw new InvalidOperationException("Edit Mode의 UI_Battle_MutedPreview Scene에서만 실행할 수 있습니다.");

        Canvas[] canvases = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
            .Where(canvas => canvas.name == "Canvas_Combat").ToArray();
        if (canvases.Length != 1) throw new InvalidOperationException("대상 Scene에 Canvas_Combat이 정확히 하나 있어야 합니다.");
        Canvas canvas = canvases[0];
        RectTransform slots = FindDirectChild(canvas.transform, "SkillSlots") as RectTransform;
        Transform bottomStrip = FindDirectChild(canvas.transform, "BottomStrip");
        if (slots == null || bottomStrip == null)
            throw new InvalidOperationException("Canvas_Combat의 기존 SkillSlots와 BottomStrip 연결을 확인하세요.");
        ValidateSceneTargets(canvas.transform, slots);
        BuildAssets assets = PreflightAssets();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (File.Exists(PREFAB_PATH) && prefab == null)
            throw new InvalidOperationException("기존 슬롯 Prefab을 읽을 수 없어 덮어쓰지 않습니다: " + PREFAB_PATH);
        if (prefab != null) ValidatePrefab(prefab);

        // 승인된 기존 편집 내용을 먼저 보존한다. 저장 취소/실패 시 Scene과 에셋을 변경하지 않는다.
        if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("현재 Scene 저장이 완료되지 않아 스킬 HUD 생성을 중단했습니다.");
        bool canvasWasActive = canvas.gameObject.activeSelf;
        ImportSprites(assets);
        if (prefab == null) prefab = CreatePrefab(assets);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, UNDO_NAME);
        var changedObjects = new List<string>();
        try
        {
            if (bottomStrip.gameObject.activeSelf)
            {
                Undo.RecordObject(bottomStrip.gameObject, UNDO_NAME);
                bottomStrip.gameObject.SetActive(false);
                changedObjects.Add("Canvas_Combat/BottomStrip (비활성 보존)");
            }
            EnsureBackground(canvas.transform, slots, assets.Sprites["Bottom_Velvet"], changedObjects);
            for (int index = 0; index < SAMPLE_IDS.Length; index++)
                EnsureSlot(slots, prefab, index, changedObjects);

            if (canvas.gameObject.activeSelf != canvasWasActive)
                throw new InvalidOperationException("Canvas_Combat 활성 상태가 변경되어 작업을 되돌립니다.");
            if (changedObjects.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("변경한 Scene을 저장하지 못했습니다.");
            }
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("전투 스킬 HUD 구성 완료. " +
                (changedObjects.Count > 0 ? string.Join(", ", changedObjects) : "기존 슬롯/배경을 재사용하여 Scene 변경 없음.") +
                "\nScene 오브젝트 변경은 Ctrl+Z로 되돌린 뒤 Scene을 다시 저장할 수 있습니다. " +
                "이미지 import 설정과 신규 폴더/Prefab 에셋 생성은 Undo 대상이 아닙니다. " +
                "Canvas_Combat 활성 상태와 다른 Canvas는 보존했습니다. 이전 BottomStrip은 비활성 보존했습니다.", canvas.gameObject);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    private static BuildAssets PreflightAssets()
    {
        foreach (string name in SPRITE_NAMES)
            if (!File.Exists(SpritePath(name))) throw new FileNotFoundException("생성된 스킬 HUD 이미지가 필요합니다.", SpritePath(name));

        var assets = new BuildAssets
        {
            Style = LoadRequired<UISkillCategoryStyleSO>(STYLE_PATH),
            Catalog = LoadRequired<UISkillPreviewCatalogSO>(CATALOG_PATH),
            Font = LoadRequired<TMP_FontAsset>(FONT_PATH)
        };
        if (assets.Style.SlotTintMaterial == null || assets.Style.EmptyFrameMaterial == null)
            throw new InvalidOperationException("공유 스타일의 Slot Tint/Empty Frame Material 연결이 필요합니다.");
        for (int index = 0; index < SAMPLE_IDS.Length; index++)
        {
            UISkillPreviewCatalogSO.Entry entry = assets.Catalog.Entries?.FirstOrDefault(item => item != null && item.Id == SAMPLE_IDS[index]);
            if (entry == null || entry.Icon == null || entry.Category != SAMPLE_CATEGORIES[index] || assets.Style.GetIconMaterial(entry.Category) == null)
                throw new InvalidOperationException("미리보기 스킬의 아이콘/분류/Material 연결이 올바르지 않습니다: " + SAMPLE_IDS[index]);
        }
        return assets;
    }

    private static void ImportSprites(BuildAssets assets)
    {
        foreach (string name in SPRITE_NAMES)
        {
            string path = SpritePath(name);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("이미지 importer를 읽을 수 없습니다: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteBorder = Vector4.zero;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            Sprite sprite = LoadRequired<Sprite>(path);
            Vector2 requiredSize = name == "Bottom_Velvet" ? new Vector2(1920, 256) : new Vector2(256, 256);
            if (sprite.rect.size != requiredSize) throw new InvalidOperationException("HUD 이미지 크기를 확인하세요: " + path + " / " + requiredSize);
            assets.Sprites.Add(name, sprite);
        }
    }

    private static GameObject CreatePrefab(BuildAssets assets)
    {
        EnsureFolder(ROOT + "/Prefabs");
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("CombatSkillSlot", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, previewScene);
            RectTransform rect = root.GetComponent<RectTransform>();
            ConfigureRect(rect, new Vector2(256, 256), Vector2.zero);
            Image plate = CreateImage(rect, "Plate", new Vector2(151, 151), null, new Color(0, 0, 0, 0.88f));
            plate.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            Image tint = CreateImage(rect, "CategorySlotTint", new Vector2(151, 151), null, Color.clear);
            tint.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            Image frame = CreateImage(rect, "FrameArt", new Vector2(256, 256), assets.Sprites["Frame_Damage"], Color.white);
            Image track = CreateImage(rect, "CooldownTrack", new Vector2(150, 150), assets.Sprites["Cooldown_Ring"], Color.white);
            Image fill = CreateImage(rect, "CooldownFill", new Vector2(150, 150), assets.Sprites["Cooldown_Ring"], Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            Image icon = CreateImage(rect, "Icon", new Vector2(104, 104), null, Color.white);
            icon.preserveAspect = true;
            TMP_Text seconds = CreateSeconds(rect, assets.Font);
            var button = root.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.None;
            button.interactable = false;
            button.enabled = false;
            var view = root.AddComponent<UICombatSkillSlotView>();
            var serialized = new SerializedObject(view);
            SetReference(serialized, "_categoryStyle", assets.Style);
            SetReference(serialized, "_icon", icon);
            SetReference(serialized, "_slotTint", tint);
            SetReference(serialized, "_frame", frame);
            SetReference(serialized, "_damageFrame", assets.Sprites["Frame_Damage"]);
            SetReference(serialized, "_buffFrame", assets.Sprites["Frame_Buff"]);
            SetReference(serialized, "_debuffFrame", assets.Sprites["Frame_Debuff"]);
            SetReference(serialized, "_cooldownTrack", track);
            SetReference(serialized, "_cooldownFill", fill);
            SetReference(serialized, "_remainingText", seconds);
            SetReference(serialized, "_previewCatalog", assets.Catalog);
            serialized.FindProperty("_previewEntryId").stringValue = SAMPLE_IDS[0];
            serialized.FindProperty("_previewRemainingSeconds").floatValue = 0f;
            serialized.FindProperty("_previewDuration").floatValue = SAMPLE_DURATIONS[0];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            view.UsePreviewValues();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out bool succeeded);
            if (!succeeded || prefab == null) throw new InvalidOperationException("스킬 슬롯 Prefab을 생성하지 못했습니다.");
            ValidatePrefab(prefab);
            return prefab;
        }
        finally { EditorSceneManager.ClosePreviewScene(previewScene); }
    }

    private static void EnsureSlot(RectTransform parent, GameObject prefab, int index, List<string> changedObjects)
    {
        string name = "SkillSlot_" + (index + 1);
        Transform existing = FindDirectChild(parent, name);
        if (existing != null && existing.GetComponent<UICombatSkillSlotView>() != null) return;
        if (existing != null)
        {
            Undo.RecordObject(existing.gameObject, UNDO_NAME);
            existing.name = "Legacy_" + name;
            existing.gameObject.SetActive(false);
            changedObjects.Add("Canvas_Combat/SkillSlots/" + existing.name + " (비활성 보존)");
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(instance, UNDO_NAME);
        instance.name = name;
        var rect = instance.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(256, 256);
        rect.anchoredPosition = new Vector2(600 + 360 * index, -925);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 1.125f;
        var view = instance.GetComponent<UICombatSkillSlotView>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("_previewEntryId").stringValue = SAMPLE_IDS[index];
        serialized.FindProperty("_previewRemainingSeconds").floatValue = index == 2 ? 8f : 0f;
        serialized.FindProperty("_previewDuration").floatValue = SAMPLE_DURATIONS[index];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        view.UsePreviewValues();
        // 표시 갱신으로 바뀐 자식 Image/TMP도 인스턴스 override로 저장한다.
        foreach (Component component in instance.GetComponentsInChildren<Component>(true))
            if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        changedObjects.Add("Canvas_Combat/SkillSlots/" + name);
    }

    private static void EnsureBackground(Transform canvas, RectTransform slots, Sprite sprite, List<string> changedObjects)
    {
        if (FindDirectChild(canvas, BACKGROUND_NAME) != null) return;
        Image image = CreateImage(canvas, BACKGROUND_NAME, new Vector2(-32, 246), sprite, new Color(0.6f, 0.6f, 0.6f, 1f));
        Undo.RegisterCreatedObjectUndo(image.gameObject, UNDO_NAME);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 8);
        rect.SetSiblingIndex(slots.GetSiblingIndex());
        changedObjects.Add("Canvas_Combat/" + BACKGROUND_NAME);
    }

    private static void ValidateSceneTargets(Transform canvas, RectTransform slots)
    {
        Transform background = FindDirectChild(canvas, BACKGROUND_NAME);
        if (background != null && (background.GetComponent<Image>() == null ||
            AssetDatabase.GetAssetPath(background.GetComponent<Image>().sprite) != SpritePath("Bottom_Velvet")))
            throw new InvalidOperationException("같은 이름의 다른 배경을 덮어쓰지 않습니다: " + BACKGROUND_NAME);
        for (int index = 1; index <= SAMPLE_IDS.Length; index++)
        {
            Transform current = FindDirectChild(slots, "SkillSlot_" + index);
            Transform legacy = FindDirectChild(slots, "Legacy_SkillSlot_" + index);
            bool isOurSlot = current != null && current.GetComponent<UICombatSkillSlotView>() != null;
            if (isOurSlot && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(current.gameObject) != PREFAB_PATH)
                throw new InvalidOperationException("다른 원본을 가진 스킬 슬롯을 덮어쓰지 않습니다: " + current.name);
            if (!isOurSlot && current != null && legacy != null)
                throw new InvalidOperationException("기존 슬롯과 Legacy 이름이 겹칩니다: SkillSlot_" + index);
            if (current == null && legacy == null)
                throw new InvalidOperationException("보존할 기존 슬롯이 없습니다: SkillSlot_" + index);
            if (legacy != null && legacy.gameObject.activeSelf)
                throw new InvalidOperationException("Legacy 슬롯이 활성화되어 있습니다. 상태를 확인하세요: " + legacy.name);
        }
    }

    private static void ValidatePrefab(GameObject prefab)
    {
        if (prefab.GetComponent<RectTransform>() == null || !prefab.TryGetComponent(out UICombatSkillSlotView view) ||
            !prefab.TryGetComponent(out Button button) || button.onClick.GetPersistentEventCount() != 0)
            throw new InvalidOperationException("기존 CombatSkillSlot Prefab 구조가 다릅니다. 자동으로 덮어쓰지 않습니다.");
        var serialized = new SerializedObject(view);
        foreach (string field in new[] { "_categoryStyle", "_icon", "_slotTint", "_frame", "_damageFrame", "_buffFrame", "_debuffFrame", "_cooldownTrack", "_cooldownFill", "_remainingText", "_previewCatalog" })
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || property.objectReferenceValue == null)
                throw new InvalidOperationException("기존 슬롯 Prefab 연결이 누락되어 재사용을 중단합니다: " + field);
            if (property.objectReferenceValue is Component component && !component.transform.IsChildOf(prefab.transform))
                throw new InvalidOperationException("슬롯 외부의 표시 참조는 변경하지 않습니다: " + field);
        }
    }

    private static Image CreateImage(Transform parent, string name, Vector2 size, Sprite sprite, Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        ConfigureRect(image.rectTransform, size, Vector2.zero);
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = false;
        return image;
    }

    private static TMP_Text CreateSeconds(Transform parent, TMP_FontAsset font)
    {
        var gameObject = new GameObject("CooldownSeconds", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        var text = gameObject.GetComponent<TextMeshProUGUI>();
        ConfigureRect(text.rectTransform, new Vector2(46, 32), new Vector2(48, -20));
        text.font = font;
        text.fontSize = 26;
        text.color = FACE;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }

    private static void ConfigureRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        Transform result = null;
        foreach (Transform child in parent)
        {
            if (child.name != name) continue;
            if (result != null) throw new InvalidOperationException("같은 이름의 자식이 중복되어 있습니다: " + parent.name + "/" + name);
            result = child;
        }
        return result;
    }

    private static void SetReference(SerializedObject serialized, string field, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException("표시 컴포넌트 필드가 없습니다: " + field);
        property.objectReferenceValue = value;
    }

    private static T LoadRequired<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("필수 에셋이 없습니다: " + path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent) || !path.StartsWith(ROOT + "/", StringComparison.Ordinal))
            throw new InvalidOperationException("승인된 스킬 아트 폴더 밖에는 폴더를 생성하지 않습니다: " + path);
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
            throw new InvalidOperationException("Prefab 폴더 생성에 실패했습니다: " + path);
    }

    private static string SpritePath(string name) => ROOT + "/Sprites/" + name + ".png";
}
