using System;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// PNG를 다시 그리지 않고, 프레임의 장식 중심 간격을 기존 슬롯 기준으로 보정한다.
public static class LobbySkillFrameAlignmentBuilder
{
    private static readonly Vector2 REFERENCE_SPAN = new Vector2(188.96f, 184.39f);
    // 256px 이미지에서 측정한 좌/우 장식 중심 X, 상/하 장식 중심 Y. Y는 이미지 아래 방향.
    private static readonly Vector4[] LANDMARKS =
    {
        new Vector4(27.50f, 226.80f, 30.54f, 224.44f),
        new Vector4(32.12f, 221.17f, 35.30f, 217.27f),
        new Vector4(33.67f, 229.25f, 39.77f, 225.14f)
    };

    public static void Configure(GameObject root)
    {
        var view = root.GetComponent<UISkillLoadoutPreview>();
        if (view == null) throw new InvalidOperationException("스킬 미리보기 없음");
        var so = new SerializedObject(view);
        var style = so.FindProperty("_categoryStyle").objectReferenceValue as UISkillCategoryStyleSO;
        var catalog = so.FindProperty("_catalog").objectReferenceValue as UISkillPreviewCatalogSO;
        if (style == null || catalog == null) throw new InvalidOperationException("표시 설정/카탈로그 없음");
        Undo.RecordObject(style, "Align category frame landmarks");
        var styleSo = new SerializedObject(style);
        string[] fields = { "_damageFrameLayout", "_buffFrameLayout", "_debuffFrameLayout" };
        for (int i = 0; i < fields.Length; i++)
        {
            Vector4 points = LANDMARKS[i];
            float sx = REFERENCE_SPAN.x / (points.y - points.x);
            float sy = REFERENCE_SPAN.y / (points.w - points.z);
            float cx = (points.x + points.y) * .5f;
            float cy = (points.z + points.w) * .5f;
            styleSo.FindProperty(fields[i]).vector4Value = new Vector4(sx, sy, (128 - cx) * sx / 256, (cy - 128) * sy / 256);
        }
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
