using System;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// PNG를 다시 그리지 않고, 프레임의 장식 중심 간격을 기존 슬롯 기준으로 보정한다.
public static class LobbySkillFrameAlignmentBuilder
{
    private const string STYLE_PATH = "Assets/06.UI/LobbyMutedPreview/Skills_v1/Styles/SkillCategoryStyle.asset";
    // 회색 Equip_Red의 좌/우/상/하 보석 중심에 256px 원본의 네 중심을 최소제곱 정렬한 값.
    // 불투명 영역이나 반짝임의 중심이 아니라 보석 내부를 측정한다. Z/W는 슬롯 크기 대비 이동량.
    private static readonly Vector4 BUFF_LAYOUT = new Vector4(.998246923f, 1.006273469f, -.003746004f, -.000718412f);
    private static readonly Vector4 DEBUFF_LAYOUT = new Vector4(.976629704f, .977774973f, -.011985075f, .010483412f);

    [MenuItem("Tools/OZGL2/Lobby/Align Buff And Debuff Frames (Style Only)")]
    public static void AlignBuffAndDebuffFrames()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        var style = AssetDatabase.LoadAssetAtPath<UISkillCategoryStyleSO>(STYLE_PATH);
        if (style == null) throw new InvalidOperationException("스킬 표시 설정 없음: " + STYLE_PATH);
        ConfigureCategoryLayouts(style);
        // 다른 dirty 에셋/씬은 저장하지 않는다. Ctrl+Z로 보정값을 되돌릴 수 있다.
        AssetDatabase.SaveAssetIfDirty(style);
        Debug.Log("버프/디버프 Frame Layout 보정 완료. 공격/빈 프레임, 슬롯/아이콘, Scene/Prefab은 변경하지 않았습니다.", style);
    }

    private static void ConfigureCategoryLayouts(UISkillCategoryStyleSO style)
    {
        Undo.RecordObject(style, "Align buff and debuff frame cores");
        var styleSo = new SerializedObject(style);
        // 이미 승인된 공격 프레임 보정과 사용자 지정 색/불투명도는 그대로 보존한다.
        styleSo.FindProperty("_buffFrameLayout").vector4Value = BUFF_LAYOUT;
        styleSo.FindProperty("_debuffFrameLayout").vector4Value = DEBUFF_LAYOUT;
        styleSo.ApplyModifiedProperties();
    }

    public static void Configure(GameObject root)
    {
        var view = root.GetComponent<UISkillLoadoutPreview>();
        if (view == null) throw new InvalidOperationException("스킬 미리보기 없음");
        var so = new SerializedObject(view);
        var style = so.FindProperty("_categoryStyle").objectReferenceValue as UISkillCategoryStyleSO;
        var catalog = so.FindProperty("_catalog").objectReferenceValue as UISkillPreviewCatalogSO;
        if (style == null || catalog == null) throw new InvalidOperationException("표시 설정/카탈로그 없음");
        ConfigureCategoryLayouts(style);
        // 프리팹 신규 구성 경로에서는 기존 공격 프레임의 승인된 보정도 초기화한다.
        // 위 Style Only 메뉴는 이 경로를 실행하지 않는다.
        var styleSo = new SerializedObject(style);
        styleSo.FindProperty("_damageFrameLayout").vector4Value = new Vector4(.94811845f, .9509541f, .003148044f, -.0018944584f);
        styleSo.ApplyModifiedProperties();
        var frames = so.FindProperty("_equippedFrames");
        var slots = so.FindProperty("_equippedSlotRects");
        var initial = so.FindProperty("_initialEquipped");
        slots.arraySize = frames.arraySize;
        for (int i = 0; i < frames.arraySize; i++)
        {
            var slot = root.transform.Find("EquippedSlot_" + i) as RectTransform;
            if (slot == null) throw new InvalidOperationException("장착 슬롯 없음: " + i);
            var slotImage = slot.GetComponent<Image>();
            var artTransform = slot.Find("FrameArt");
            if (artTransform == null)
            {
                var go = new GameObject("FrameArt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = slot.gameObject.layer;
                go.transform.SetParent(slot, false);
                Undo.RegisterCreatedObjectUndo(go, "Separate equipped frame visual");
                artTransform = go.transform;
            }
            var image = artTransform.GetComponent<Image>();
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            int index = i < initial.arraySize ? initial.GetArrayElementAtIndex(i).intValue : -1;
            bool valid = index >= 0 && index < catalog.Entries.Count && catalog.Entries[index] != null;
            var category = valid ? catalog.Entries[index].Category : eSkillPreviewCategory.DAMAGE;
            image.sprite = valid ? style.GetEquippedFrame(category) : so.FindProperty("_redFrame").objectReferenceValue as Sprite;
            image.material = valid ? null : style.EmptyFrameMaterial;
            image.color = Color.white; image.raycastTarget = false; image.preserveAspect = false;
            image.type = Image.Type.Simple;
            Vector4 layout = valid ? style.GetEquippedFrameLayout(category) : new Vector4(1, 1, 0, 0);
            rect.sizeDelta = new Vector2(slot.rect.width * layout.x, slot.rect.height * layout.y);
            rect.anchoredPosition = new Vector2(slot.rect.width * layout.z, slot.rect.height * layout.w);
            rect.SetAsFirstSibling(); // Frame → CategorySlotTint → Icon
            slotImage.sprite = null; slotImage.overrideSprite = null;
            slotImage.color = Color.clear; slotImage.raycastTarget = true;
            slot.GetComponent<Button>().targetGraphic = image;
            frames.GetArrayElementAtIndex(i).objectReferenceValue = image;
            slots.GetArrayElementAtIndex(i).objectReferenceValue = slot;
        }
        so.ApplyModifiedProperties();
    }
}
