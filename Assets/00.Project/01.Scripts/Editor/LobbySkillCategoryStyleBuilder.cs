using System;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 기존 아트/카탈로그를 덮어쓰지 않고 분류별 표시 레이어와 공유 Material만 연결한다.
public static class LobbySkillCategoryStyleBuilder
{
    public const string STYLE_ROOT = "Assets/06.UI/LobbyMutedPreview/Skills_v1/Styles";
    public const string STYLE_PATH = STYLE_ROOT + "/SkillCategoryStyle.asset";
    private const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private static readonly Color FACE = new Color32(248, 242, 235, 255);

    [MenuItem("Tools/OZGL2/Lobby/Apply Skill Category Visual Style")]
    public static void ApplyToExistingPrefab()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if ((stage == null && (scene.path != SCENE || scene.isDirty)) ||
            (stage != null && (stage.assetPath != LobbySkillSettingsPreviewBuilder.PREFAB || stage.scene.isDirty)))
            throw new InvalidOperationException("저장된 대상 씬 또는 스킬 Prefab에서 실행하세요.");
        stage = PrefabStageUtility.OpenPrefab(LobbySkillSettingsPreviewBuilder.PREFAB);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply skill category icon and slot colors");
        Undo.RegisterFullObjectHierarchyUndo(stage.prefabContentsRoot, "Skill category visual references");
        try
        {
            Configure(stage.prefabContentsRoot);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("스킬 목록 12개/장착 슬롯 3개/상세 아이콘에 공통 분류색 적용. Prefab Mode에서 Ctrl+Z 지원. 원본 Sprite/카탈로그/Scene은 유지합니다.");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    public static void Configure(GameObject root)
    {
        if (root == null || !root.TryGetComponent(out UISkillLoadoutPreview view))
            throw new InvalidOperationException("UISkillLoadoutPreview가 필요합니다.");
        var style = EnsureStyle();
        var so = new SerializedObject(view);
        var catalog = so.FindProperty("_catalog").objectReferenceValue as UISkillPreviewCatalogSO;
        if (catalog == null) throw new InvalidOperationException("미리보기 카탈로그 연결이 없습니다.");
        so.FindProperty("_categoryStyle").objectReferenceValue = style;
        var icons = so.FindProperty("_cardIcons");
        var tints = so.FindProperty("_cardSlotTints");
        tints.arraySize = icons.arraySize;
        for (int i = 0; i < icons.arraySize; i++)
        {
            var icon = icons.GetArrayElementAtIndex(i).objectReferenceValue as Image;
            if (icon == null || i >= catalog.Entries.Count) throw new InvalidOperationException("스킬 카드 아이콘 연결 누락");
            Image tint = Tint(icon.transform.parent, "CategorySlotTint");
            // 상태별 금색/주황 프레임 안쪽까지만 표시한다.
            Inset(tint.rectTransform, 16f);
            tint.transform.SetAsFirstSibling();
            SetVisual(icon, tint, style, catalog.Entries[i]);
            tints.GetArrayElementAtIndex(i).objectReferenceValue = tint;
        }
        var equipped = so.FindProperty("_equippedIcons");
        var initial = so.FindProperty("_initialEquipped");
        var equippedTints = so.FindProperty("_equippedSlotTints");
        equippedTints.arraySize = equipped.arraySize;
        for (int i = 0; i < equipped.arraySize; i++)
        {
            var icon = equipped.GetArrayElementAtIndex(i).objectReferenceValue as Image;
            if (icon == null) throw new InvalidOperationException("장착 아이콘 연결 누락");
            Image tint = Tint(icon.transform.parent, "CategorySlotTint");
            var rect = tint.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(138, 138);
            rect.localRotation = Quaternion.Euler(0, 0, 45);
            tint.transform.SetAsFirstSibling();
            int index = i < initial.arraySize ? initial.GetArrayElementAtIndex(i).intValue : -1;
            SetVisual(icon, tint, style, index >= 0 && index < catalog.Entries.Count ? catalog.Entries[index] : null);
            var frames = so.FindProperty("_equippedFrames");
            var equippedFrame = i < frames.arraySize ? frames.GetArrayElementAtIndex(i).objectReferenceValue as Image : null;
            if (equippedFrame != null)
            {
                equippedFrame.sprite = index >= 0 && index < catalog.Entries.Count ? style.GetEquippedFrame(catalog.Entries[index].Category) : so.FindProperty("_redFrame").objectReferenceValue as Sprite;
                equippedFrame.material = index >= 0 && index < catalog.Entries.Count ? null : style.EmptyFrameMaterial;
            }
            equippedTints.GetArrayElementAtIndex(i).objectReferenceValue = tint;
        }
        var detail = so.FindProperty("_detailIcon").objectReferenceValue as Image;
        Transform frame = root.transform.Find("DetailIconFrame");
        if (detail == null || frame == null) throw new InvalidOperationException("상세 아이콘/프레임 연결 누락");
        Image detailTint = Tint(frame, "CategorySlotTint");
        Inset(detailTint.rectTransform, 30);
        int selected = so.FindProperty("_initialSelected").intValue;
        SetVisual(detail, detailTint, style, selected >= 0 && selected < catalog.Entries.Count ? catalog.Entries[selected] : null);
        so.FindProperty("_detailSlotTint").objectReferenceValue = detailTint;
        so.ApplyModifiedProperties();
    }

    private static UISkillCategoryStyleSO EnsureStyle()
    {
        var style = AssetDatabase.LoadAssetAtPath<UISkillCategoryStyleSO>(STYLE_PATH);
        if (style != null)
        {
            ConnectMissingFrames(style);
            ConfigureEmptyFrameMaterial(style);
            return style; // 팀원이 조정한 색/농도를 재실행으로 덮어쓰지 않는다.
        }
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(STYLE_ROOT + "/SkillCategoryUI.shader");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("스킬 표시 Shader 컴파일을 먼저 확인하세요.");
        style = ScriptableObject.CreateInstance<UISkillCategoryStyleSO>();
        var so = new SerializedObject(style);
        so.FindProperty("_damageIcon").objectReferenceValue = Material(shader, "SkillIcon_Damage", new Color32(165, 64, 63, 255), false);
        so.FindProperty("_buffIcon").objectReferenceValue = Material(shader, "SkillIcon_Buff", new Color32(187, 153, 75, 255), false);
        so.FindProperty("_debuffIcon").objectReferenceValue = Material(shader, "SkillIcon_Debuff", new Color32(131, 81, 154, 255), false);
        so.FindProperty("_slotTintMaterial").objectReferenceValue = Material(shader, "SkillSlot_Tint", Color.white, true);
        so.FindProperty("_slotOpacity").floatValue = .14f;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(style, STYLE_PATH);
        ConnectMissingFrames(style);
        ConfigureEmptyFrameMaterial(style);
        AssetDatabase.SaveAssets();
        return style;
    }

    // 표시 SO/Material만 연결한다. Scene/Prefab과 원본 PNG를 수정하지 않는다.
    public static void ConfigureEmptyFrameMaterial(UISkillCategoryStyleSO style)
    {
        if (style == null) throw new InvalidOperationException("스킬 표시 설정이 필요합니다.");
        var so = new SerializedObject(style);
        if (so.FindProperty("_emptyFrameMaterial").objectReferenceValue != null) return;
        const string path = STYLE_ROOT + "/SkillFrame_Empty.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(STYLE_ROOT + "/SkillCategoryUI.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("스킬 표시 Shader를 먼저 확인하세요.");
            material = new Material(shader) { name = "SkillFrame_Empty" };
            material.SetFloat("_EmptyFrame", 1);
            material.EnableKeyword("SKILL_EMPTY_FRAME");
            AssetDatabase.CreateAsset(material, path);
        }
        Undo.RecordObject(style, "Use desaturated empty skill frame");
        so.FindProperty("_emptyFrameMaterial").objectReferenceValue = material;
        so.ApplyModifiedProperties();
    }

    private static void ConnectMissingFrames(UISkillCategoryStyleSO style)
    {
        var so = new SerializedObject(style);
        string[] fields = { "_damageFrame", "_buffFrame", "_debuffFrame" };
        string[] names = { "Damage", "Buff", "Debuff" };
        for (int i = 0; i < fields.Length; i++)
        {
            if (so.FindProperty(fields[i]).objectReferenceValue != null) continue;
            string path = LobbySkillSettingsPreviewBuilder.ROOT + "/Sprites/Equip_" + names[i] + "_Muted.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("장착 프레임 Sprite를 먼저 임포트하세요: " + path);
            so.FindProperty(fields[i]).objectReferenceValue = sprite;
        }
        so.ApplyModifiedProperties();
    }

    private static Material Material(Shader shader, string name, Color outline, bool slot)
    {
        string path = STYLE_ROOT + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(shader) { name = name };
        material.SetColor("_FaceColor", FACE);
        material.SetColor("_OutlineColor", outline);
        material.SetFloat("_OutlineTexels", 2);
        material.SetFloat("_ShadowCutoff", .15f);
        material.SetFloat("_SlotTint", slot ? 1 : 0);
        if (slot) material.EnableKeyword("SKILL_SLOT_TINT");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Image Tint(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out Image image)) return image;
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create skill slot category tint");
        return go.GetComponent<Image>();
    }

    private static void Inset(RectTransform rect, float margin)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = Vector2.one * margin; rect.offsetMax = Vector2.one * -margin;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetVisual(Image icon, Image tint, UISkillCategoryStyleSO style, UISkillPreviewCatalogSO.Entry entry)
    {
        icon.material = entry != null ? style.GetIconMaterial(entry.Category) : null;
        icon.color = Color.white;
        tint.sprite = null;
        tint.raycastTarget = false;
        tint.material = style.SlotTintMaterial;
        tint.color = entry != null ? style.GetSlotColor(entry.Category) : Color.clear;
        tint.enabled = entry != null;
    }
}
