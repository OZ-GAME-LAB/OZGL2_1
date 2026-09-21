using System;
using System.Collections.Generic;
using System.Linq;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UI 미리보기 상태만 검사한다. 실제 게임 저장소는 변경하지 않는다.
public static class LobbySkillSaveFlowValidation
{
    [MenuItem("Tools/OZGL2/Lobby/Validate Skill Save Flow And Frames")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity")
            throw new InvalidOperationException("대상 씬 Play Mode에서 실행하세요.");
        var view = UnityEngine.Object.FindFirstObjectByType<UISkillLoadoutPreview>();
        if (view == null || view.HasChanges) throw new InvalidOperationException("저장 상태의 스킬창을 먼저 여세요.");
        var root = view.transform;
        var controller = GameObject.Find("UI_Root").GetComponent<UIPopupController>();
        var panel = view.GetComponent<UIPopupPanel>();
        var dialog = root.Find("ExitConfirmation").GetComponent<UIPopupPanel>();
        var save = root.Find("Save").GetComponent<UISkillArtButton>();
        var cancel = dialog.transform.Find("Panel/Cancel").GetComponent<Button>();
        var leave = dialog.transform.Find("Panel/Leave").GetComponent<Button>();
        var so = new SerializedObject(view);
        var style = so.FindProperty("_categoryStyle").objectReferenceValue as UISkillCategoryStyleSO;
        var catalog = so.FindProperty("_catalog").objectReferenceValue as UISkillPreviewCatalogSO;
        var checks = new List<string>();
        Action<bool, string> check = (ok, message) => { if (!ok) throw new InvalidOperationException(message); checks.Add(message); };
        string saved = string.Join(",", view.GetEquippedIds());
        string prefs = PlayerPrefs.GetString("OZGL2.Skill.Equip", "");
        int savedEvents = 0;
        Action<IReadOnlyList<string>> onSave = ids => savedEvents++;
        view.SaveRequested += onSave;
        try
        {
            check(!save.interactable && save.image.overrideSprite.name == "Button_Save_Disabled", "저장 상태는 버튼/아트 비활성");
            view.SavePreview(); check(savedEvents == 0, "동일 상태 저장 메서드도 무동작");
            view.ShowCategory(0); view.SelectEquippedSlot(1); view.UnequipSelected();
            check(view.HasChanges && save.interactable, "편집 시 저장 활성");
            check(root.Find("Status").GetComponent<TMP_Text>().text == string.Empty, "미저장 상태 문구 제거");
            view.EquipSelected();
            check(!view.HasChanges && !save.interactable, "원래 슬롯 구성으로 복원하면 저장 비활성");
            view.UnequipSelected();
            root.Find("Back").GetComponent<Button>().onClick.Invoke();
            check(view.IsExitConfirmationOpen && controller.OpenCount == 2, "뒤로 버튼은 확인창만 엶");
            check(!panel.GetComponent<CanvasGroup>().interactable && cancel.IsInteractable(), "편집 UI 잠금/확인창 입력 허용");
            check(EventSystem.current.currentSelectedGameObject == cancel.gameObject, "취소 버튼에 안전한 기본 포커스");
            check(dialog.transform.Find("Panel/Message").GetComponent<TMP_Text>().text == "스킬 변경 사항이 저장되지 않았습니다.\n저장하지 않고 로비로 돌아가시겠습니까?", "확인 문구 일치");
            view.SavePreview(); view.EquipSelected();
            check(savedEvents == 0 && view.EquippedCount == 2, "확인 중 저장/편집 메서드 차단");
            cancel.onClick.Invoke();
            check(!view.IsExitConfirmationOpen && view.HasChanges && view.EquippedCount == 2 && controller.OpenCount == 1, "취소는 편집 유지");
            controller.CloseTopPopup(); // ESC가 사용하는 공통 진입점
            check(view.IsExitConfirmationOpen, "ESC 닫기 경로도 확인창을 거침");
            controller.CloseTopPopup();
            check(!view.IsExitConfirmationOpen && view.HasChanges && controller.IsTopPopup(panel), "확인창 ESC는 취소로 처리");
            controller.CloseTopPopup(); leave.onClick.Invoke();
            check(!view.gameObject.activeSelf && controller.OpenCount == 0 && savedEvents == 0, "로비로는 저장 없이 두 창 닫기");
            check(GameObject.Find("Canvas_Lobby").GetComponent<CanvasGroup>().interactable, "로비 입력 복원");
            leave.onClick.Invoke(); check(controller.OpenCount == 0, "중복 확인 무동작");
            controller.OpenPopup(panel);
            check(string.Join(",", view.GetEquippedIds()) == saved && !save.interactable, "재열기 시 마지막 저장 복원");
            view.SelectEquippedSlot(1); view.UnequipSelected(); view.SavePreview();
            check(savedEvents == 1 && !view.HasChanges && !save.interactable, "저장 성공 시 즉시 비활성/이벤트 1회");
            controller.CloseTopPopup(); check(!view.gameObject.activeSelf && !view.IsExitConfirmationOpen, "저장 후 바로 닫힘");
            controller.OpenPopup(panel); view.ShowCategory(0); view.SelectSkill(4); view.EquipSelected(); view.SavePreview();
            check(string.Join(",", view.GetEquippedIds()) == saved, "기본 저장 구성 복원");
            foreach (string id in view.GetEquippedIds())
            {
                view.SelectSkill(Enumerable.Range(0, catalog.Entries.Count).First(i => catalog.Entries[i].Id == id)); view.UnequipSelected();
            }
            int[] indexes = { 4, 3, 6 }; // IsArcane 딜/버프/디버프를 모든 슬롯 위치에서 검사
            for (int shift = 0; shift < 3; shift++)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    int index = indexes[(slot + shift) % 3];
                    view.SelectSkill(index); view.EquipSelected();
                    var frame = root.Find("EquippedSlot_" + slot + "/FrameArt").GetComponent<Image>();
                    var expected = style.GetEquippedFrame(catalog.Entries[index].Category);
                    check(expected != null && frame.sprite == expected && frame.overrideSprite == expected, "슬롯 " + slot + " / " + catalog.Entries[index].Category + " 분류 전용 Sprite");
                    check(frame.material != style.EmptyFrameMaterial, "장착 시 무채색 Material 해제");
                }
                foreach (int index in indexes) { view.SelectSkill(index); view.UnequipSelected(); }
                var empty = root.Find("EquippedSlot_0/FrameArt").GetComponent<Image>().sprite;
                check(Enumerable.Range(0, 3).All(i => root.Find("EquippedSlot_" + i + "/FrameArt").GetComponent<Image>().sprite == empty), "빈 슬롯은 같은 프레임");
                check(style.EmptyFrameMaterial != null && style.EmptyFrameMaterial.IsKeywordEnabled("SKILL_EMPTY_FRAME"), "빈 슬롯 무채색 설정");
                check(Enumerable.Range(0, 3).All(i => root.Find("EquippedSlot_" + i + "/FrameArt").GetComponent<Image>().material == style.EmptyFrameMaterial), "빈 슬롯 3개 무채색 Material 적용");
            }
            check(prefs == PlayerPrefs.GetString("OZGL2.Skill.Equip", ""), "실제 스킬 저장 불변");
        }
        finally
        {
            view.SaveRequested -= onSave;
            if (view.IsExitConfirmationOpen) view.CancelExit();
            if (controller.IsTopPopup(panel)) controller.CloseTopPopup();
            if (view.IsExitConfirmationOpen) view.ConfirmExitWithoutSaving();
            controller.OpenPopup(panel);
        }
        return checks.Count + " save flow/frame checks passed\n" + string.Join("\n", checks);
    }

    // 확인창을 연 다음 렌더 프레임에서 호출한다.
    public static string ValidateAlignmentAndHover()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Mode 필요");
        var view = UnityEngine.Object.FindFirstObjectByType<UISkillLoadoutPreview>();
        if (view == null || view.HasChanges) throw new InvalidOperationException("저장 상태의 스킬창 필요");
        var root = view.transform;
        var controller = GameObject.Find("UI_Root").GetComponent<UIPopupController>();
        var panel = view.GetComponent<UIPopupPanel>();
        int count = 0;
        Action<bool, string> check = (ok, reason) => { if (!ok) throw new InvalidOperationException(reason); count++; };
        try
        {
            view.ShowCategory(0);
            foreach (int index in new[] { 0, 4, 2 }) { view.SelectSkill(index); view.UnequipSelected(); }
            int[] indexes = { 4, 3, 6 };
            Vector4[] landmarks = { new Vector4(27.5f, 226.8f, 30.54f, 224.44f), new Vector4(32.12f, 221.17f, 35.3f, 217.27f), new Vector4(33.67f, 229.25f, 39.77f, 225.14f) };
            Vector2[] positions = { new Vector2(-165, 124), new Vector2(165, 124), new Vector2(0, -155) };
            for (int i = 0; i < 3; i++)
            {
                view.SelectSkill(indexes[i]); view.EquipSelected();
                var slot = root.Find("EquippedSlot_" + i) as RectTransform;
                var frame = slot.Find("FrameArt") as RectTransform;
                Vector4 p = landmarks[i];
                float cx = frame.anchoredPosition.x + frame.rect.width * ((p.x + p.y) * .5f - 128) / 256;
                float cy = frame.anchoredPosition.y + frame.rect.height * (128 - (p.z + p.w) * .5f) / 256;
                check(Mathf.Abs(cx) < .01f && Mathf.Abs(cy) < .01f, "프레임 중심 불일치: " + i);
                check(Mathf.Abs(frame.rect.width * (p.y-p.x) / 256 - slot.rect.width * 188.96f / 256) < .01f, "프레임 가로 간격 불일치");
                check(Mathf.Abs(frame.rect.height * (p.w-p.z) / 256 - slot.rect.height * 184.39f / 256) < .01f, "프레임 세로 간격 불일치");
                check(slot.anchoredPosition == positions[i] && slot.sizeDelta == new Vector2(246, 246), "기존 슬롯 위치/클릭 영역 불변");
                check(((RectTransform)slot.Find("Icon")).anchoredPosition == Vector2.zero, "아이콘 중심 불변");
                check(frame.GetSiblingIndex() == 0 && !frame.GetComponent<Image>().raycastTarget, "프레임 레이어/입력 설정");
            }
            controller.CloseTopPopup();
            var cancel = root.Find("ExitConfirmation/Panel/Cancel").GetComponent<UISkillArtButton>();
            var leave = root.Find("ExitConfirmation/Panel/Leave").GetComponent<UISkillArtButton>();
            check(cancel.image.sprite == leave.image.sprite, "두 버튼 공통 테두리");
            check(cancel.colors == leave.colors, "두 버튼 공통 상태 색");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            foreach (var button in new[] { cancel, leave })
            {
                var originalColors = button.colors;
                var immediate = originalColors; immediate.fadeDuration = 0; button.colors = immediate;
                try
                {
                    button.Select(); button.OnPointerExit(pointer);
                    check(button.image.canvasRenderer.GetColor() == immediate.normalColor, "선택만 된 상태는 기본 프레임");
                    button.OnPointerEnter(pointer);
                    check(button.image.canvasRenderer.GetColor() == immediate.highlightedColor, "선택된 버튼도 호버 강조");
                    button.OnPointerDown(pointer);
                    check(button.image.canvasRenderer.GetColor() == immediate.pressedColor, "누름 상태 동일");
                    button.OnPointerUp(pointer); button.OnPointerExit(pointer);
                    check(button.image.canvasRenderer.GetColor() == immediate.normalColor, "포인터 이탈 후 프레임 복원");
                }
                finally { button.colors = originalColors; }
            }
        }
        finally
        {
            if (view.IsExitConfirmationOpen) view.ConfirmExitWithoutSaving();
            else if (controller.IsTopPopup(panel)) { controller.CloseTopPopup(); if (view.IsExitConfirmationOpen) view.ConfirmExitWithoutSaving(); }
            controller.OpenPopup(panel);
        }
        return count + " alignment/hover checks passed";
    }

    // 확인창을 연 다음 렌더 프레임에서 호출한다.
    public static string ValidateModalRaycast()
    {
        var view = UnityEngine.Object.FindFirstObjectByType<UISkillLoadoutPreview>();
        if (view == null || !view.IsExitConfirmationOpen) throw new InvalidOperationException("확인창이 필요합니다.");
        var targets = new[] { "ExitConfirmation/Panel/Cancel", "ExitConfirmation/Panel/Leave", "Category_0" };
        var expected = new[] { "Cancel", "Leave", "ExitConfirmation" };
        for (int i = 0; i < targets.Length; i++)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, view.transform.Find(targets[i]).position) };
            var results = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, results);
            if (results.Count == 0 || results[0].gameObject.name != expected[i]) throw new InvalidOperationException("확인창 Raycast 실패: " + targets[i]);
        }
        return "Cancel/Leave clickable; categories and underlying lobby blocked.";
    }
}
