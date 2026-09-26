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

// 기존 슬롯의 GUID와 스킬 아이콘을 유지하고 프레임/쿨다운/배경 표시만 조정한다.
// Scene 배치는 Undo를 지원하지만 LoadPrefabContents 저장과 이미지 import는 Undo 대상이 아니다.
public static class CombatSkillHudReferencePolisher
{
    public const string SCENE_PATH = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    public const string PREFAB_PATH = "Assets/06.UI/BattleMutedPreview/CombatSkills_v1/Prefabs/CombatSkillSlot.prefab";
    public const string ART_ROOT = "Assets/06.UI/BattleMutedPreview/CombatSkills_v2/Sprites";
    private const string UNDO_NAME = "Polish combat skill HUD to reference";
    private static readonly string[] SPRITE_NAMES = { "Frame_Damage", "Frame_Buff", "Frame_Debuff", "Ring_Track", "Ring_Fill", "Bottom_Velvet" };
    private static readonly string[] DIRECTIONS = { "Top", "Bottom", "Left", "Right" };
    private static readonly Vector2[] TICK_POSITIONS = { new Vector2(0, 92), new Vector2(0, -92), new Vector2(-92, 0), new Vector2(92, 0) };
    private static readonly float[] SLOT_X = { 586f, 960f, 1334f };
    private static readonly Color PLATE_COLOR = Color.black;

    private sealed class SlotParts
    {
        public RectTransform Root;
        public UICombatSkillSlotView View;
        public Image Plate;
        public Image Tint;
        public Image Frame;
        public Image Track;
        public Image Fill;
        public Image Icon;
        public TMP_Text Seconds;
    }

    private sealed class PreservedViewState
    {
        private readonly Sprite _sprite;
        private readonly Material _material;
        private readonly Color _color;
        private readonly UnityEngine.Object _catalog;
        private readonly UnityEngine.Object _style;
        private readonly string _entryId;
        private readonly float _remainingSeconds;
        private readonly float _duration;

        public PreservedViewState(SlotParts parts)
        {
            _sprite = parts.Icon.sprite;
            _material = parts.Icon.material;
            _color = parts.Icon.color;
            var serialized = new SerializedObject(parts.View);
            _catalog = RequiredProperty(serialized, "_previewCatalog").objectReferenceValue;
            _style = RequiredProperty(serialized, "_categoryStyle").objectReferenceValue;
            _entryId = RequiredProperty(serialized, "_previewEntryId").stringValue;
            _remainingSeconds = RequiredProperty(serialized, "_previewRemainingSeconds").floatValue;
            _duration = RequiredProperty(serialized, "_previewDuration").floatValue;
        }

        public void RefreshAndPreserve(SlotParts parts)
        {
            parts.View.UsePreviewValues();
            // 프레임/쿨다운 갱신 전후에도 기존 아이콘과 데이터 연결은 같아야 한다.
            var serialized = new SerializedObject(parts.View);
            if (parts.Icon.sprite != _sprite || parts.Icon.material != _material || parts.Icon.color != _color ||
                RequiredProperty(serialized, "_icon").objectReferenceValue != parts.Icon ||
                RequiredProperty(serialized, "_previewCatalog").objectReferenceValue != _catalog ||
                RequiredProperty(serialized, "_categoryStyle").objectReferenceValue != _style ||
                RequiredProperty(serialized, "_previewEntryId").stringValue != _entryId ||
                !RequiredProperty(serialized, "_previewRemainingSeconds").floatValue.Equals(_remainingSeconds) ||
                !RequiredProperty(serialized, "_previewDuration").floatValue.Equals(_duration))
                throw new InvalidOperationException("스킬 아이콘/분류 스타일/카탈로그/미리보기 값이 변경되어 저장을 중단합니다.");
        }
    }

    public static void Build() => Polish();

    [MenuItem("Tools/OZGL2/Battle/Polish Combat Skill HUD (Reference)")]
    public static void Polish()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null ||
            !scene.IsValid() || !scene.isLoaded || scene.path != SCENE_PATH)
            throw new InvalidOperationException("Edit Mode의 UI_Battle_MutedPreview Scene에서만 실행하세요.");
        foreach (string name in SPRITE_NAMES)
            if (!File.Exists(SpritePath(name))) throw new FileNotFoundException("v2 스킬 HUD 이미지가 필요합니다.", SpritePath(name));
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null) throw new InvalidOperationException("기존 CombatSkillSlot Prefab이 필요합니다. 새 Prefab으로 교체하지 않습니다.");
        ReadParts(prefab);
        Canvas canvas = FindCanvas(scene);
        RectTransform slots = RequiredChild<RectTransform>(canvas.transform, "SkillSlots");
        Image background = RequiredChild<Image>(canvas.transform, "BottomSkillBackground");
        SlotParts[] sceneParts = ReadSceneSlots(slots);
        var preservedScene = sceneParts.Select(parts => new PreservedViewState(parts)).ToArray();
        bool canvasWasActive = canvas.gameObject.activeSelf;
        Color backgroundColor = background.color;
        string prefabGuid = AssetDatabase.AssetPathToGUID(PREFAB_PATH);

        // 기존 사용자 편집을 먼저 저장하며, 저장이 실패하면 에셋 변경도 시작하지 않는다.
        if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("현재 Scene 저장이 완료되지 않아 표시 정리를 중단합니다.");
        Dictionary<string, Sprite> sprites = ImportSprites();
        PolishPrefab(sprites);
        if (AssetDatabase.AssetPathToGUID(PREFAB_PATH) != prefabGuid)
            throw new InvalidOperationException("기존 Prefab GUID가 달라졌습니다. Scene 변경을 중단합니다.");

        // Prefab 저장으로 갱신된 인스턴스 참조를 다시 찾고, Scene 배치 변경만 별도 Undo로 기록한다.
        sceneParts = ReadSceneSlots(slots);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        foreach (SlotParts parts in sceneParts) Undo.RegisterFullObjectHierarchyUndo(parts.Root.gameObject, UNDO_NAME);
        Undo.RecordObjects(new UnityEngine.Object[] { background, background.rectTransform }, UNDO_NAME);
        try
        {
            for (int index = 0; index < sceneParts.Length; index++)
            {
                SlotParts parts = sceneParts[index];
                parts.Root.anchorMin = parts.Root.anchorMax = new Vector2(0, 1);
                parts.Root.pivot = new Vector2(0.5f, 0.5f);
                parts.Root.sizeDelta = new Vector2(336, 336);
                parts.Root.anchoredPosition = new Vector2(SLOT_X[index], -878);
                parts.Root.localScale = Vector3.one;
                parts.Root.localRotation = Quaternion.identity;
                ValidateInheritedArt(parts, sprites);
                preservedScene[index].RefreshAndPreserve(parts);
                RecordDisplayOverrides(parts);
            }

            background.sprite = sprites["Bottom_Velvet"];
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = new Vector2(1, 0);
            backgroundRect.pivot = new Vector2(0.5f, 0);
            backgroundRect.sizeDelta = new Vector2(0, 256);
            backgroundRect.anchoredPosition = new Vector2(0, 12);
            if (background.color != backgroundColor || canvas.gameObject.activeSelf != canvasWasActive)
                throw new InvalidOperationException("기존 배경색 또는 Canvas 활성 상태가 바뀌어 Scene 변경을 되돌립니다.");
            if (PrefabUtility.IsPartOfPrefabInstance(background))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(background);
                PrefabUtility.RecordPrefabInstancePropertyModifications(backgroundRect);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("정리한 전투 HUD Scene을 저장하지 못했습니다.");
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("전투 스킬 HUD를 참고 아트에 맞게 정리했습니다. 기존 CombatSkillSlot Prefab GUID/스킬 아이콘/카탈로그/미리보기 값 보존. " +
                "변경 Scene 오브젝트: Canvas_Combat/SkillSlots/SkillSlot_1~3, Canvas_Combat/BottomSkillBackground. " +
                "기존 다른 Canvas 오브젝트와 PreviewTriggers 위치는 건드리지 않았습니다.\n" +
                "Scene 배치 변경은 Ctrl+Z 후 다시 저장하여 복구할 수 있습니다. Prefab 내용 저장과 이미지 import는 Undo 대상이 아닙니다.", canvas.gameObject);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogWarning("Scene 배치 변경은 되돌렸습니다. 이미 저장된 Prefab 내용과 이미지 import는 Undo 대상이 아니므로 별도 확인이 필요합니다.");
            throw;
        }
    }

    private static void PolishPrefab(Dictionary<string, Sprite> sprites)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            SlotParts parts = ReadParts(contents);
            var preserved = new PreservedViewState(parts);
            CenterRect(parts.Root, new Vector2(336, 336), Vector2.zero);
            CenterRect(parts.Frame.rectTransform, new Vector2(336, 336), Vector2.zero);
            CenterRect(parts.Plate.rectTransform, new Vector2(194, 194), Vector2.zero, 45f);
            CenterRect(parts.Tint.rectTransform, new Vector2(194, 194), Vector2.zero, 45f);
            parts.Plate.color = PLATE_COLOR;
            CenterRect(parts.Track.rectTransform, new Vector2(200, 200), Vector2.zero);
            CenterRect(parts.Fill.rectTransform, new Vector2(200, 200), Vector2.zero);
            parts.Track.sprite = sprites["Ring_Track"];
            parts.Fill.sprite = sprites["Ring_Fill"];
            CenterRect(parts.Icon.rectTransform, new Vector2(117, 117), Vector2.zero);
            CenterRect(parts.Seconds.rectTransform, new Vector2(44, 38), new Vector2(66, -26));
            parts.Seconds.fontSize = 30f;

            RectTransform ticksRoot = EnsureTickRoot(parts);
            Image[] ticks = ConfigureTicks(ticksRoot);
            var serialized = new SerializedObject(parts.View);
            RequiredProperty(serialized, "_damageFrame").objectReferenceValue = sprites["Frame_Damage"];
            RequiredProperty(serialized, "_buffFrame").objectReferenceValue = sprites["Frame_Buff"];
            RequiredProperty(serialized, "_debuffFrame").objectReferenceValue = sprites["Frame_Debuff"];
            RequiredProperty(serialized, "_slotTintStrength").floatValue = 0.28f;
            RequiredProperty(serialized, "_cooldownTrackOpacity").floatValue = 0.78f;
            RequiredProperty(serialized, "_cooldownFillOpacity").floatValue = 1f;
            SerializedProperty tickReferences = RequiredProperty(serialized, "_cooldownTicks");
            tickReferences.arraySize = ticks.Length;
            for (int index = 0; index < ticks.Length; index++) tickReferences.GetArrayElementAtIndex(index).objectReferenceValue = ticks[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            preserved.RefreshAndPreserve(parts);
            PrefabUtility.SaveAsPrefabAsset(contents, PREFAB_PATH, out bool succeeded);
            if (!succeeded) throw new InvalidOperationException("기존 CombatSkillSlot Prefab 저장에 실패했습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static RectTransform EnsureTickRoot(SlotParts parts)
    {
        Transform existing = FindChild(parts.Root, "CooldownTicks");
        RectTransform ticksRoot;
        if (existing == null)
        {
            var gameObject = new GameObject("CooldownTicks", typeof(RectTransform));
            gameObject.transform.SetParent(parts.Root, false);
            ticksRoot = gameObject.GetComponent<RectTransform>();
        }
        else
        {
            ticksRoot = existing as RectTransform;
            if (ticksRoot == null) throw new InvalidOperationException("기존 CooldownTicks가 UI RectTransform이 아닙니다.");
        }
        CenterRect(ticksRoot, new Vector2(200, 200), Vector2.zero);
        // 다른 레이어 순서를 유지하며 Fill 바로 다음, Icon 앞에 눈금을 배치한다.
        int fillIndex = parts.Fill.transform.GetSiblingIndex();
        int destination = fillIndex + (ticksRoot.GetSiblingIndex() < fillIndex ? 0 : 1);
        ticksRoot.SetSiblingIndex(destination);
        if (ticksRoot.GetSiblingIndex() >= parts.Icon.transform.GetSiblingIndex())
            throw new InvalidOperationException("기존 Fill/Icon 렌더 순서를 확인하세요. 다른 레이어를 임의로 재정렬하지 않습니다.");
        return ticksRoot;
    }

    private static Image[] ConfigureTicks(RectTransform root)
    {
        var ticks = new Image[DIRECTIONS.Length];
        for (int index = 0; index < DIRECTIONS.Length; index++)
        {
            bool vertical = index < 2;
            Image gap = EnsureImage(root, "Gap_" + DIRECTIONS[index]);
            CenterRect(gap.rectTransform, vertical ? new Vector2(9, 18) : new Vector2(18, 9), TICK_POSITIONS[index]);
            gap.color = Color.black;
            gap.transform.SetSiblingIndex(index * 2);
            Image tick = EnsureImage(root, "Tick_" + DIRECTIONS[index]);
            CenterRect(tick.rectTransform, vertical ? new Vector2(3, 13) : new Vector2(13, 3), TICK_POSITIONS[index]);
            tick.transform.SetSiblingIndex(index * 2 + 1);
            ticks[index] = tick;
        }
        return ticks;
    }

    private static Image EnsureImage(Transform parent, string name)
    {
        Transform existing = FindChild(parent, name);
        Image image;
        if (existing == null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            image = gameObject.GetComponent<Image>();
        }
        else image = existing.GetComponent<Image>();
        if (image == null) throw new InvalidOperationException("같은 이름의 다른 눈금 오브젝트를 변경하지 않습니다: " + name);
        image.sprite = null;
        image.material = null;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        image.enabled = true;
        return image;
    }

    private static Dictionary<string, Sprite> ImportSprites()
    {
        var sprites = new Dictionary<string, Sprite>();
        foreach (string name in SPRITE_NAMES)
        {
            string path = SpritePath(name);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("v2 이미지 importer를 읽을 수 없습니다: " + path);
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
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Vector2 expectedSize = name == "Bottom_Velvet" ? new Vector2(1920, 256) :
                name.StartsWith("Frame_", StringComparison.Ordinal) ? new Vector2(336, 336) : new Vector2(200, 200);
            if (sprite == null || sprite.rect.size != expectedSize) throw new InvalidOperationException("v2 이미지 크기를 확인하세요: " + path);
            sprites.Add(name, sprite);
        }
        return sprites;
    }

    private static void ValidateInheritedArt(SlotParts parts, Dictionary<string, Sprite> sprites)
    {
        var serialized = new SerializedObject(parts.View);
        if (RequiredProperty(serialized, "_damageFrame").objectReferenceValue != sprites["Frame_Damage"] ||
            RequiredProperty(serialized, "_buffFrame").objectReferenceValue != sprites["Frame_Buff"] ||
            RequiredProperty(serialized, "_debuffFrame").objectReferenceValue != sprites["Frame_Debuff"] ||
            parts.Track.sprite != sprites["Ring_Track"] || parts.Fill.sprite != sprites["Ring_Fill"] ||
            RequiredProperty(serialized, "_cooldownTicks").arraySize != 4)
            throw new InvalidOperationException("기존 Scene override가 v2 표시 참조를 가리고 있습니다. 전체 override를 되돌리지 않고 중단합니다: " + parts.Root.name);
    }

    private static void RecordDisplayOverrides(SlotParts parts)
    {
        foreach (UnityEngine.Object target in new UnityEngine.Object[] { parts.Root, parts.Frame, parts.Tint, parts.Track, parts.Fill, parts.Seconds })
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        SerializedProperty ticks = RequiredProperty(new SerializedObject(parts.View), "_cooldownTicks");
        for (int index = 0; index < ticks.arraySize; index++)
            if (ticks.GetArrayElementAtIndex(index).objectReferenceValue is Image tick)
                PrefabUtility.RecordPrefabInstancePropertyModifications(tick);
    }

    private static SlotParts[] ReadSceneSlots(RectTransform parent)
    {
        var result = new SlotParts[3];
        for (int index = 0; index < result.Length; index++)
        {
            RectTransform root = RequiredChild<RectTransform>(parent, "SkillSlot_" + (index + 1));
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) != PREFAB_PATH)
                throw new InvalidOperationException("예상한 기존 스킬 Prefab 인스턴스가 아닙니다: " + root.name);
            result[index] = ReadParts(root.gameObject);
        }
        return result;
    }

    private static SlotParts ReadParts(GameObject root)
    {
        if (!root.TryGetComponent(out UICombatSkillSlotView view) || !root.TryGetComponent(out RectTransform rect))
            throw new InvalidOperationException("기존 스킬 슬롯의 UI 표시 컴포넌트가 필요합니다: " + root.name);
        var parts = new SlotParts
        {
            Root = rect, View = view,
            Plate = RequiredChild<Image>(rect, "Plate"), Tint = RequiredChild<Image>(rect, "CategorySlotTint"),
            Frame = RequiredChild<Image>(rect, "FrameArt"), Track = RequiredChild<Image>(rect, "CooldownTrack"),
            Fill = RequiredChild<Image>(rect, "CooldownFill"), Icon = RequiredChild<Image>(rect, "Icon"),
            Seconds = RequiredChild<TMP_Text>(rect, "CooldownSeconds")
        };
        var serialized = new SerializedObject(view);
        if (RequiredProperty(serialized, "_icon").objectReferenceValue != parts.Icon)
            throw new InvalidOperationException("기존 Icon 참조가 다른 오브젝트를 가리킵니다.");
        return parts;
    }

    private static Canvas FindCanvas(Scene scene)
    {
        Canvas[] canvases = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
            .Where(canvas => canvas.name == "Canvas_Combat").ToArray();
        if (canvases.Length != 1) throw new InvalidOperationException("대상 Scene의 Canvas_Combat이 정확히 하나여야 합니다.");
        return canvases[0];
    }

    private static T RequiredChild<T>(Transform parent, string name) where T : Component
    {
        Transform child = FindChild(parent, name);
        T component = child != null ? child.GetComponent<T>() : null;
        if (component == null) throw new InvalidOperationException("필수 UI 오브젝트가 없습니다: " + parent.name + "/" + name);
        return component;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        Transform result = null;
        foreach (Transform child in parent)
        {
            if (child.name != name) continue;
            if (result != null) throw new InvalidOperationException("같은 이름의 UI 오브젝트가 중복되었습니다: " + name);
            result = child;
        }
        return result;
    }

    private static SerializedProperty RequiredProperty(SerializedObject serialized, string field)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException("표시 컴포넌트 필드가 없습니다. 컴파일을 확인하세요: " + field);
        return property;
    }

    private static void CenterRect(RectTransform rect, Vector2 size, Vector2 position, float rotation = 0f)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.Euler(0, 0, rotation);
    }

    private static string SpritePath(string name) => ART_ROOT + "/" + name + ".png";
}
