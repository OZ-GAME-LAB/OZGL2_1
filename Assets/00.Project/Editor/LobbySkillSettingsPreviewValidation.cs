using System;
using System.Collections.Generic;
using System.Linq;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Play Mode의 시안 전용 인스턴스로만 검증한다. 실제 게임 저장 데이터는 읽기만 한다.
public static class LobbySkillSettingsPreviewValidation
{
    [MenuItem("Tools/OZGL2/Lobby/Validate Skill Settings Preview")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity")
            throw new InvalidOperationException("UI_Lobby_MutedPreview Play Mode에서 스킬창을 열고 실행하세요.");
        var view = UnityEngine.Object.FindFirstObjectByType<UISkillLoadoutPreview>();
        if (view == null) throw new InvalidOperationException("스킬창을 먼저 여세요.");
        var popup = GameObject.Find("UI_Root").GetComponent<UIPopupController>();
        var panel = view.GetComponent<UIPopupPanel>();
        var root = view.transform;
        var checks = new List<string>();
        Action<bool, string> check = (ok, message) => { if (!ok) throw new InvalidOperationException(message); checks.Add(message); };
        string before = PlayerPrefs.GetString("OZGL2.Skill.Equip", "");
        check(view.EquippedCount == 3, "초기 장착 슬롯 3개");
        for (int tab = 0; tab < 4; tab++)
        {
            root.Find("Category_" + tab).GetComponent<Button>().onClick.Invoke();
            int count = root.Find("SkillGrid").Cast<Transform>().Count(t => t.gameObject.activeSelf);
            check(count == (tab == 0 ? 12 : 4), "카테고리 " + tab + " 목록 수 " + count);
            check(root.Find("Category_" + tab).GetComponent<UISkillArtButton>().IsChosen, "카테고리 " + tab + " 선택 표시");
        }
        view.ShowCategory(0); view.SelectSkill(4);
        check(!root.Find("Equip").GetComponent<Button>().interactable, "중복 장착 버튼 비활성");
        check(root.Find("Unequip").GetComponent<Button>().interactable, "장착된 스킬 해제 가능");
        root.Find("Unequip").GetComponent<Button>().onClick.Invoke();
        check(view.EquippedCount == 2, "해제 시 한 슬롯만 비움");
        check(root.Find("Equip").GetComponent<Button>().interactable, "빈 슬롯이 있으면 장착 가능");
        view.SelectSkill(1); root.Find("Equip").GetComponent<Button>().onClick.Invoke();
        check(view.EquippedCount == 3, "빈 슬롯 장착");
        view.EquipSelected(); check(view.EquippedCount == 3, "중복 장착 메서드 차단");
        view.SelectSkill(3);
        check(!root.Find("Equip").GetComponent<Button>().interactable, "3개 한도 버튼 비활성");
        view.EquipSelected(); check(view.EquippedCount == 3, "한도 초과 메서드 차단");
        root.Find("Save").GetComponent<Button>().onClick.Invoke();
        check(!view.HasChanges, "미리보기 저장 후 변경 표시 해제");
        string saved = string.Join(",", view.GetEquippedIds());
        root.Find("Back").GetComponent<Button>().onClick.Invoke();
        check(popup.OpenCount == 0 && !view.gameObject.activeSelf, "뒤로 버튼 닫기");
        check(GameObject.Find("Canvas_Lobby").GetComponent<CanvasGroup>().interactable, "로비 입력 복원");
        popup.OpenPopup(panel);
        check(string.Join(",", view.GetEquippedIds()) == saved, "재열기 시 미리보기 저장 유지");
        view.SelectSkill(1); view.UnequipSelected(); check(view.HasChanges, "저장 전 변경 표시");
        popup.CloseTopPopup(); popup.OpenPopup(panel);
        check(string.Join(",", view.GetEquippedIds()) == saved, "저장하지 않은 변경 취소");
        view.SelectSkill(1); view.UnequipSelected(); view.SelectSkill(4); view.EquipSelected(); view.SavePreview();
        popup.CloseTopPopup(); popup.OpenPopup(panel);
        check(string.Join(",", view.GetEquippedIds()) == "ui_preview_fire,ui_preview_abyss,ui_preview_strike", "기본 시안으로 복원");
        check(before == PlayerPrefs.GetString("OZGL2.Skill.Equip", ""), "실제 저장 데이터 불변");
        var art = root.Find("Category_1").GetComponent<UISkillArtButton>();
        var image = art.GetComponent<Image>();
        var pointer = new PointerEventData(EventSystem.current);
        art.OnPointerEnter(pointer); check(image.overrideSprite.name == "Button_Tab_Hover", "호버 Sprite");
        art.OnPointerDown(pointer); check(image.overrideSprite.name == "Button_Tab_Pressed", "누름 Sprite");
        art.OnPointerUp(pointer); art.OnPointerExit(pointer); art.interactable = false;
        check(image.overrideSprite.name == "Button_Tab_Disabled", "비활성 Sprite");
        art.interactable = true; EventSystem.current.SetSelectedGameObject(null); art.SetChosen(false);
        check(image.overrideSprite.name == "Button_Tab_Normal", "기본 Sprite");
        view.ShowCategory(0);
        check(root.Find("Category_0").GetComponent<Image>().overrideSprite.name == "Button_Tab_Selected", "선택 Sprite");
        ValidateCardStates(view, check);
        check(view.GetComponentsInChildren<TMP_Text>(true).All(t => t.font != null), "모든 TMP 폰트 연결");
        check(view.GetComponentsInChildren<TMP_Text>(true).All(t => !t.raycastTarget), "TMP 입력 가로채기 없음");
        string result = checks.Count + " checks passed\n" + string.Join("\n", checks);
        SessionState.SetString("OZGL2.SkillPreview.Validation", result);
        return result;
    }

    private static void ValidateCardStates(UISkillLoadoutPreview view, Action<bool, string> check)
    {
        view.ShowCategory(0); view.SelectSkill(4);
        EventSystem.current.SetSelectedGameObject(null);
        var first = view.transform.Find("SkillGrid/SkillCard_00").GetComponent<UISkillArtButton>();
        var second = view.transform.Find("SkillGrid/SkillCard_01").GetComponent<UISkillArtButton>();
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        first.OnPointerEnter(pointer);
        check(first.image.overrideSprite.name == "Card_Hover_Amber", "미선택 카드 호버 주황");
        first.OnPointerExit(pointer);
        check(first.image.overrideSprite.name == "Card_Normal", "미선택 카드 호버 해제 복원");
        first.OnPointerEnter(pointer); first.OnPointerDown(pointer);
        check(!first.IsChosen && first.image.overrideSprite.name == "Card_Hover_Amber", "클릭 확정 전 선택하지 않음");
        first.OnPointerUp(pointer); first.OnPointerClick(pointer);
        check(first.IsChosen && first.image.overrideSprite.name == "Card_Selected_Gold", "클릭 선택 금색");
        first.OnPointerExit(pointer);
        check(first.image.overrideSprite.name == "Card_Selected_Gold", "마우스 이탈 후 선택 유지");
        first.OnPointerEnter(pointer); first.OnPointerDown(pointer);
        check(first.image.overrideSprite.name == "Card_Selected_Gold", "선택 카드 재누름 금색 유지");
        first.OnPointerUp(pointer); first.OnPointerExit(pointer);
        second.OnPointerEnter(pointer); second.OnPointerDown(pointer); second.OnPointerUp(pointer); second.OnPointerClick(pointer); second.OnPointerExit(pointer);
        check(!first.IsChosen && first.image.overrideSprite.name == "Card_Normal", "다른 카드 선택 시 이전 카드 기본 복원");
        check(second.IsChosen && second.image.overrideSprite.name == "Card_Selected_Gold", "새 카드만 금색 선택");
        check(view.transform.Find("DetailIconFrame").GetComponent<Image>().sprite.name == "Card_Selected_Gold", "상세 프레임 선택 색 통일");
        second.interactable = false;
        check(second.image.overrideSprite.name == "Card_Normal", "비활성 상태 우선");
        second.interactable = true;
        EventSystem.current.SetSelectedGameObject(null);
        view.SelectSkill(4);
    }

    // 활성화 직후 Canvas 갱신이 끝난 다음 프레임에 별도로 호출한다.
    public static string ValidateRaycast()
    {
        var view = UnityEngine.Object.FindFirstObjectByType<UISkillLoadoutPreview>();
        if (view == null || EventSystem.current == null) throw new InvalidOperationException("스킬창과 EventSystem이 필요합니다.");
        var pointer = new PointerEventData(EventSystem.current);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, view.transform.Find("Category_0").position);
        var results = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, results);
        if (results.Count == 0 || results[0].gameObject.name != "Category_0") throw new InvalidOperationException("탭 Raycast 대상 오류");
        return "Category_0 receives raycast; underlying lobby is blocked.";
    }
}
