using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 기능 검사는 복제 화면/별도 세션 상태로 격리한다. 에셋, 원본 장착 상태, PlayerPrefs는 저장하지 않는다.
public static class LobbySkillRosterValidation
{
    private const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private const string JSON_PATH = "Tools/Art/SkillRoster_v2.json";
    private const int SKILL_COUNT = 19;
    private static PointerSession _pointerSession;

    [Serializable]
    private sealed class RosterDocument { public RosterEntry[] Entries = Array.Empty<RosterEntry>(); }

    [Serializable]
    private sealed class RosterEntry
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Category = string.Empty;
        public int Tier = 0;
        public int UnlockSp = 0;
        public string Activation = string.Empty;
        public int CooldownSeconds = 0;
        public string EffectLabel = string.Empty;
        public string EffectValue = string.Empty;
        public bool IsUltimate = false;
        public bool DefaultUnlocked = false;
        public string IconPath = string.Empty;
    }

    private sealed class Checks
    {
        private readonly List<string> _passed = new List<string>();
        public void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException("Skill roster 검사 실패 (" + _passed.Count + "개 통과): " + reason);
            _passed.Add(reason);
        }
        public string Report(string label) => _passed.Count + " " + label + " checks passed\n" + string.Join("\n", _passed);
    }

    [MenuItem("Tools/OZGL2/Lobby/Validate Skill Roster V2")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run() => WithClone("skill roster", (view, state, host, controller, checks) =>
    {
        var catalog = Read<UISkillPreviewCatalogSO>(view, "_catalog");
        ValidateStructure(view, checks);
        ValidateRoster(view, catalog, ReadRoster(), checks);
        ValidateCategoriesAndScroll(view, catalog, checks);
        ValidateCardStates(view, checks);
        ValidateFramesAndLock(view, state, catalog, checks);
        ValidateSaveFlow(view, host, controller, checks);
        ValidateMissingReferences(view.gameObject, checks);
    });

    [MenuItem("Tools/OZGL2/Lobby/Validate Skill Roster V2 Text Layout")]
    public static void ValidateTextLayoutFromMenu() => Debug.Log(ValidateTextLayout());

    // 글리프/말줄임 실패를 기능 실패와 구분한다. 폰트 에셋에 문자를 직접 추가하거나 저장하지 않는다.
    public static string ValidateTextLayout() => WithClone("skill roster text", (view, state, host, controller, checks) =>
    {
        var catalog = Read<UISkillPreviewCatalogSO>(view, "_catalog");
        checks.Check(catalog != null && catalog.Entries.Count == SKILL_COUNT, "글자 검사용 19종 카탈로그");
        view.ShowCategory(0);
        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            view.SelectSkill(i);
            Canvas.ForceUpdateCanvases();
            foreach (string field in new[] { "_detailName", "_detailDescription", "_detailMetadata", "_effectLabel", "_effectValue", "_cooldown" })
            {
                TMP_Text text = Read<TMP_Text>(view, field);
                string label = catalog.Entries[i].DisplayName + " / " + field;
                checks.Check(text != null && text.font != null, label + " TMP/폰트 연결");
                text.ForceMeshUpdate(true, true);
                string visible = new string(text.text.Where(character => !char.IsWhiteSpace(character)).ToArray());
                bool hasGlyphs = text.font.HasCharacters(visible, out uint[] missing, true, false);
                checks.Check(hasGlyphs, label + " 누락 글리프 없음" + (hasGlyphs ? string.Empty : ": " + string.Join(",", (missing ?? Array.Empty<uint>()).Select(value => "U+" + value.ToString("X4")))));
                checks.Check(!text.isTextOverflowing && !text.isTextTruncated, label + " overflow/말줄임 없음");
                // GetPreferredValues는 자동 크기 조절의 최대 글자 크기로 다시 측정하므로 실제 렌더된 경계를 확인한다.
                checks.Check(text.textBounds.size.y <= text.rectTransform.rect.height + 1f, label + " 세로 표시 공간 충분");
            }
        }
    });

    private static string WithClone(string label, Action<UISkillLoadoutPreview, UILobbyCollectionState, UILobbyOverlayView, UIPopupController, Checks> validate)
    {
        UILobbyOverlayView host = RequireHost();
        var controller = host.GetComponent<UIPopupController>();
        var original = host.GetComponentsInChildren<UISkillLoadoutPreview>(true).Single();
        if (controller == null || host.IsOpen || controller.OpenCount != 0 || original.gameObject.activeSelf)
            throw new InvalidOperationException("모든 Overlay를 닫은 뒤 실행하세요. 원본 편집 상태는 변경하지 않습니다.");
        if (_pointerSession != null) throw new InvalidOperationException("진행 중인 포인터 검사를 완료하거나 CancelPointerScroll()로 복원하세요.");
        var catalog = Read<UISkillPreviewCatalogSO>(original, "_catalog");
        var style = Read<UISkillCategoryStyleSO>(original, "_categoryStyle");
        var originalState = Read<UILobbyCollectionState>(original, "_unlockState");
        if (catalog == null || style == null || originalState == null) throw new InvalidOperationException("원본 카탈로그/분류 스타일/공유 해금 상태 연결이 필요합니다.");
        string catalogBefore = EditorJsonUtility.ToJson(catalog);
        string styleBefore = EditorJsonUtility.ToJson(style);
        string stateBefore = StateFingerprint(originalState);
        int[] committedBefore = (int[])Read<int[]>(original, "_committed").Clone();
        int[] draftBefore = (int[])Read<int[]>(original, "_draft").Clone();
        var prefsBefore = ReadPrefs(ReadRoster());
        GameObject focusBefore = EventSystem.current.currentSelectedGameObject;
        var checks = new Checks();
        var errors = new List<string>();
        Application.LogCallback onLog = (message, trace, type) =>
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(message);
        };
        GameObject clone = null;
        Application.logMessageReceived += onLog;
        try
        {
            clone = Object.Instantiate(original.gameObject, host.transform, false);
            clone.name = "SkillRosterValidation_Transient";
            clone.SetActive(false);
            var view = clone.GetComponent<UISkillLoadoutPreview>();
            var state = clone.AddComponent<UILobbyCollectionState>();
            Write(view, "_unlockState", state);
            var panel = clone.GetComponent<UIPopupPanel>();
            host.OpenOverlayPopup(panel);
            checks.Check(controller.IsTopPopup(panel) && clone.activeInHierarchy, "복제 화면 공용 스택 열기");
            checks.Check(view.EquippedCount == 3 && !view.HasChanges, "초기 장착 3개/저장 상태");
            checks.Check(Read<int[]>(view, "_initialEquipped").SequenceEqual(new[] { 0, 2, 5 }), "초기 장착 인덱스 0/2/5");
            checks.Check(view.GetEquippedIds().SequenceEqual(new[] { 0, 2, 5 }.Select(index => catalog.Entries[index].Id)), "초기 화염구/연쇄 번개/공허 붕괴 장착");
            checks.Check(Read<int>(view, "_initialSelected") == 0 && view.SelectedSkillId == catalog.Entries[0].Id, "초기 화염구 선택");
            validate(view, state, host, controller, checks);
            checks.Check(errors.Count == 0, "검사 중 Console Error/Exception 없음: " + string.Join(" | ", errors));
        }
        finally
        {
            while (controller.OpenCount > 0) controller.CloseConfirmedPopup();
            if (clone != null) Object.DestroyImmediate(clone);
            Application.logMessageReceived -= onLog;
            EventSystem.current.SetSelectedGameObject(focusBefore != null && focusBefore.activeInHierarchy ? focusBefore : null);
        }
        checks.Check(EditorJsonUtility.ToJson(catalog) == catalogBefore, "원본 카탈로그 불변");
        checks.Check(EditorJsonUtility.ToJson(style) == styleBefore, "원본 분류 스타일 불변");
        checks.Check(StateFingerprint(originalState) == stateBefore, "원본 공유 해금/업적 세션 상태 불변");
        checks.Check(Read<int[]>(original, "_committed").SequenceEqual(committedBefore) && Read<int[]>(original, "_draft").SequenceEqual(draftBefore), "원본 committed/draft 불변");
        checks.Check(ReadPrefs(ReadRoster()).SequenceEqual(prefsBefore), "실제 PlayerPrefs 장착/SP/마일스톤/해금 키와 값 불변");
        checks.Check(!host.IsOpen && controller.OpenCount == 0, "검사 종료 후 팝업 스택 복원");
        return checks.Report(label);
    }

    private static void ValidateStructure(UISkillLoadoutPreview view, Checks checks)
    {
        foreach (string field in new[] { "_cards", "_cardIcons", "_cardSlotTints", "_cardLockIcons", "_cardUltimateFrames" })
        {
            var values = Read<Array>(view, field);
            checks.Check(values != null && values.Length == SKILL_COUNT && values.Cast<Object>().All(value => value != null && ((Component)value).transform.IsChildOf(view.transform)), field + " 19개 내부 참조");
        }
        foreach (string field in new[] { "_equippedIcons", "_equippedFrames", "_equippedSlotTints", "_equippedSlotRects", "_equippedUltimateFrames" })
        {
            var values = Read<Array>(view, field);
            checks.Check(values != null && values.Length == 3 && values.Cast<Object>().All(value => value != null), field + " 3개 참조");
        }
        foreach (string field in new[] { "_detailName", "_detailDescription", "_detailMetadata", "_detailIcon", "_detailSlotTint", "_detailLockIcon", "_detailUltimateFrame", "_effectLabel", "_effectValue", "_cooldown", "_equipButton", "_unequipButton", "_saveButton", "_exitConfirmation" })
        {
            Component target = Read<Component>(view, field);
            checks.Check(target != null && target.transform.IsChildOf(view.transform), field + " 복제 화면 내부 연결");
        }
        var scroll = Read<ScrollRect>(view, "_ownedScrollRect");
        checks.Check(scroll != null && scroll.content != null && scroll.viewport != null && scroll.transform.IsChildOf(view.transform), "ScrollRect/content/viewport 연결");
        checks.Check(scroll.content.parent == scroll.viewport && Mathf.Approximately(scroll.content.anchorMin.y, 1f) &&
            Mathf.Approximately(scroll.content.anchorMax.y, 1f) && Mathf.Approximately(scroll.content.pivot.y, 1f), "Content 상단 고정 anchor/pivot 계약");
        checks.Check(Mathf.Abs(scroll.content.anchoredPosition.y) < .01f, "OnEnable 직후 Content 로컬 상단 위치");
        checks.Check(scroll.vertical && !scroll.horizontal && scroll.movementType == ScrollRect.MovementType.Clamped, "세로 전용 Clamped 스크롤");
        checks.Check(scroll.viewport.GetComponent<Mask>() != null || scroll.viewport.GetComponent<RectMask2D>() != null, "Viewport 잘라내기 마스크");
        var grid = scroll.content.GetComponent<GridLayoutGroup>();
        checks.Check(grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount == 3, "목록 3열 고정");
        var fitter = scroll.content.GetComponent<ContentSizeFitter>();
        checks.Check(fitter != null && fitter.verticalFit == ContentSizeFitter.FitMode.PreferredSize, "목록 높이 자동 계산");
        var cards = Read<UISkillArtButton[]>(view, "_cards");
        checks.Check(cards.All(card => card.transform.parent == scroll.content), "19장 모두 스크롤 Content 소속");
        Rebuild(scroll);
        for (int i = 0; i < cards.Length; i++)
        {
            checks.Check((cards[i].transform.localScale - Vector3.one).sqrMagnitude < .000001f, "카드 " + i + " localScale 1 유지");
            if (cards[i].gameObject.activeInHierarchy)
                checks.Check(HasValidWorldBounds(cards[i].image.rectTransform), "활성 카드 " + i + " 유효한 월드 표시 영역");
            int listenerCount = Enumerable.Range(0, cards[i].onClick.GetPersistentEventCount()).Count(index =>
                cards[i].onClick.GetPersistentTarget(index) == view && cards[i].onClick.GetPersistentMethodName(index) == nameof(UISkillLoadoutPreview.SelectSkill));
            checks.Check(listenerCount == 1, "카드 " + i + " SelectSkill 연결 중복 없음");
            cards[i].onClick.Invoke();
            checks.Check(view.SelectedSkillId == Read<UISkillPreviewCatalogSO>(view, "_catalog").Entries[i].Id, "카드 " + i + " 선택 인덱스 연결");
        }
        checks.Check(view.GetComponentsInChildren<TMP_Text>(true).All(text => text.font != null && !text.raycastTarget), "TMP 폰트 연결/입력 가로채기 없음");
        ValidateMissingReferences(view.gameObject, checks);
    }

    private static void ValidateRoster(UISkillLoadoutPreview view, UISkillPreviewCatalogSO catalog, RosterEntry[] roster, Checks checks)
    {
        checks.Check(catalog.Entries.Count == SKILL_COUNT && roster.Length == SKILL_COUNT, "JSON/카탈로그 19종");
        checks.Check(catalog.Entries.Select(entry => entry.Id).Distinct().Count() == SKILL_COUNT, "19개 ID 고유");
        checks.Check(catalog.Entries.Count(entry => entry.IsUltimate) == 3 && catalog.Entries.Where(entry => entry.IsUltimate).All(entry => entry.Category == eSkillPreviewCategory.DAMAGE), "궁극기 3종 모두 DAMAGE 분류");
        view.ShowCategory(0);
        for (int i = 0; i < roster.Length; i++)
        {
            RosterEntry expected = roster[i];
            var actual = catalog.Entries[i];
            string label = i + " " + expected.Name;
            checks.Check(actual != null && actual.Id == expected.Id && actual.DisplayName == expected.Name, label + " ID/이름/순서");
            checks.Check(actual.Description == expected.Description, label + " 설명 JSON 일치");
            checks.Check(actual.Category.ToString() == expected.Category && actual.IsUltimate == expected.IsUltimate, label + " 분류/궁극기 표시 설정");
            checks.Check(actual.Tier == expected.Tier && actual.UnlockSp == expected.UnlockSp && actual.Activation == expected.Activation, label + " 티어/SP/시전 방식");
            checks.Check(actual.EffectLabel == expected.EffectLabel && actual.EffectValue == expected.EffectValue && actual.Cooldown == expected.CooldownSeconds + "초", label + " 효과/쿨타임");
            checks.Check(actual.Icon != null && AssetDatabase.GetAssetPath(actual.Icon) == expected.IconPath, label + " 승인된 아이콘 경로");
            checks.Check(actual.DefaultUnlocked == expected.DefaultUnlocked && view.IsSkillUnlocked(i) == expected.DefaultUnlocked, label + " 기본 해금 표시");
            view.SelectSkill(i);
            checks.Check(Read<TMP_Text>(view, "_detailName").text == expected.Name && Read<TMP_Text>(view, "_detailDescription").text == expected.Description, label + " 상세 TMP 이름/설명");
            checks.Check(Read<TMP_Text>(view, "_detailMetadata").text == "T" + expected.Tier + " · " + expected.Activation + " · 해금 " + expected.UnlockSp + " SP", label + " 상세 메타데이터 TMP");
            checks.Check(Read<TMP_Text>(view, "_effectLabel").text == expected.EffectLabel && Read<TMP_Text>(view, "_effectValue").text == expected.EffectValue && Read<TMP_Text>(view, "_cooldown").text == expected.CooldownSeconds + "초", label + " 상세 효과/쿨타임 TMP");
            checks.Check(Read<Image[]>(view, "_cardUltimateFrames")[i].enabled == expected.IsUltimate && Read<Image>(view, "_detailUltimateFrame").enabled == expected.IsUltimate, label + " 목록/상세 궁극기 장식");
        }
    }

    private static void ValidateCategoriesAndScroll(UISkillLoadoutPreview view, UISkillPreviewCatalogSO catalog, Checks checks)
    {
        var cards = Read<UISkillArtButton[]>(view, "_cards");
        var tabs = Read<UISkillArtButton[]>(view, "_categoryButtons");
        var scroll = Read<ScrollRect>(view, "_ownedScrollRect");
        int[] counts = { 19, 9, 4, 6 };
        checks.Check(tabs != null && tabs.Length == 4, "전체/딜/버프/디버프 4개 탭");
        for (int category = 0; category < counts.Length; category++)
        {
            view.ShowCategory(0);
            scroll.verticalNormalizedPosition = 0f;
            view.ShowCategory(category);
            checks.Check(cards.Count(card => card.gameObject.activeSelf) == counts[category], "분류 " + category + " 카드 수 " + counts[category]);
            checks.Check(tabs[category].IsChosen && tabs.Count(tab => tab.IsChosen) == 1, "분류 " + category + " 단일 탭 선택");
            checks.Check(IsAtTop(scroll), "분류 " + category + " 스크롤 상단 초기화");
        }
        view.ShowCategory(0);
        Rebuild(scroll);
        checks.Check(scroll.content.rect.height > scroll.viewport.rect.height, "19종은 Viewport보다 긴 Content");
        scroll.verticalNormalizedPosition = 0f;
        Rebuild(scroll);
        checks.Check(IsCenterInsideViewport(scroll, cards[SKILL_COUNT - 1].image.rectTransform), "마지막 19번째 카드 하단 스크롤 도달 가능");
        float position = scroll.verticalNormalizedPosition;
        view.SelectSkill(SKILL_COUNT - 1);
        checks.Check(view.SelectedSkillId == catalog.Entries[SKILL_COUNT - 1].Id && Mathf.Abs(scroll.verticalNormalizedPosition - position) < .001f, "마지막 카드 선택/일반 Refresh는 스크롤 유지");
        Vector3 scaleBefore = view.transform.localScale;
        try
        {
            // 비활성 Canvas 재연결처럼 월드 변환이 퇴화한 상황에서도 상단 초기화가 틀어지지 않아야 한다.
            view.transform.localScale = Vector3.zero;
            view.ShowCategory(0);
            checks.Check(Mathf.Abs(scroll.content.anchoredPosition.y) < .01f, "월드 Scale 0에서도 Content 로컬 상단 초기화");
        }
        finally { view.transform.localScale = scaleBefore; Rebuild(scroll); }
        checks.Check(IsAtTop(scroll), "Scale 복원 후 첫 행 상단 위치 유지");
    }

    private static void ValidateCardStates(UISkillLoadoutPreview view, Checks checks)
    {
        view.ShowCategory(0);
        var cards = Read<UISkillArtButton[]>(view, "_cards");
        view.SelectSkill(1);
        EventSystem.current.SetSelectedGameObject(null);
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1 };
        var first = cards[0];
        first.OnPointerEnter(pointer);
        checks.Check(first.image.overrideSprite != null && first.image.overrideSprite.name == "Card_Hover_Amber", "일반 카드 호버 Amber 유지");
        first.OnPointerDown(pointer); first.OnPointerUp(pointer); first.OnPointerClick(pointer); first.OnPointerExit(pointer);
        checks.Check(first.IsChosen && first.image.overrideSprite.name == "Card_Selected_Gold", "선택 확정 Gold 유지");
        first.OnPointerDown(pointer);
        checks.Check(first.image.overrideSprite.name == "Card_Selected_Gold", "선택 카드 다시 누르기 Gold 유지");
        first.OnPointerUp(pointer); first.OnPointerExit(pointer);
        int ultimate = SKILL_COUNT - 1;
        view.SelectSkill(ultimate);
        cards[ultimate].OnPointerEnter(pointer); cards[ultimate].OnPointerDown(pointer);
        checks.Check(cards[ultimate].image.overrideSprite.name == "Card_Selected_Gold" && Read<Image[]>(view, "_cardUltimateFrames")[ultimate].enabled, "궁극기 장식과 선택 Gold 공존");
        cards[ultimate].OnPointerUp(pointer); cards[ultimate].OnPointerExit(pointer);
        checks.Check(first.image.overrideSprite.name == "Card_Normal" && !first.IsChosen, "다른 카드 선택 시 이전 카드 복원");
    }

    private static void ValidateFramesAndLock(UISkillLoadoutPreview view, UILobbyCollectionState state, UISkillPreviewCatalogSO catalog, Checks checks)
    {
        var style = Read<UISkillCategoryStyleSO>(view, "_categoryStyle");
        var frames = Read<Image[]>(view, "_equippedFrames");
        var ornaments = Read<Image[]>(view, "_equippedUltimateFrames");
        var slots = Read<RectTransform[]>(view, "_equippedSlotRects");
        var icons = Read<Image[]>(view, "_equippedIcons");
        RectTransform[] fixedRects = slots.Concat(icons.Select(image => image.rectTransform)).ToArray();
        Vector2[] sizes = fixedRects.Select(rect => rect.sizeDelta).ToArray();
        Vector3[] positions = fixedRects.Select(rect => rect.anchoredPosition3D).ToArray();
        ClearEquipped(view, catalog);
        var empty = Read<Sprite>(view, "_redFrame");
        checks.Check(frames.All(frame => frame.sprite == empty && frame.material == style.EmptyFrameMaterial) && ornaments.All(frame => !frame.enabled), "빈 슬롯 공통 무채색 프레임/궁극기 장식 숨김");
        int[] representatives = Enum.GetValues(typeof(eSkillPreviewCategory)).Cast<eSkillPreviewCategory>().Select(category => Enumerable.Range(0, catalog.Entries.Count).First(index => catalog.Entries[index].Category == category && !catalog.Entries[index].IsUltimate)).ToArray();
        for (int shift = 0; shift < 3; shift++)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                int index = representatives[(slot + shift) % representatives.Length];
                view.SelectSkill(index); view.EquipSelected();
                checks.Check(frames[slot].sprite == style.GetEquippedFrame(catalog.Entries[index].Category) && frames[slot].material != style.EmptyFrameMaterial, "슬롯 " + slot + " / " + catalog.Entries[index].Category + " 프레임");
                checks.Check(icons[slot].material == style.GetIconMaterial(catalog.Entries[index].Category) && icons[slot].color == Color.white && !ornaments[slot].enabled, "슬롯 " + slot + " 흰 아이콘/분류 Material/일반 장식 숨김");
            }
            ClearEquipped(view, catalog);
        }
        int[] ultimates = Enumerable.Range(0, catalog.Entries.Count).Where(index => catalog.Entries[index].IsUltimate).ToArray();
        for (int slot = 0; slot < ultimates.Length; slot++)
        {
            view.SelectSkill(ultimates[slot]); view.EquipSelected();
            checks.Check(ornaments[slot].enabled && frames[slot].sprite == style.GetEquippedFrame(eSkillPreviewCategory.DAMAGE), "궁극기 " + slot + " 장착 장식 + 딜 프레임");
        }
        checks.Check(Enumerable.Range(0, fixedRects.Length).All(index => fixedRects[index].sizeDelta == sizes[index] && fixedRects[index].anchoredPosition3D == positions[index]), "분류/궁극기 교체 중 슬롯/아이콘 배치 불변");
        checks.Check(Read<Image[]>(view, "_cardUltimateFrames").Concat(ornaments).Append(Read<Image>(view, "_detailUltimateFrame")).All(image => image.sprite != null && !image.raycastTarget), "모든 궁극기 장식 Sprite 연결/입력 미차단");
        view.SavePreview();
        int lockedIndex = ultimates[2];
        string lockedId = catalog.Entries[lockedIndex].Id;
        state.SetUnlocked(lockedId, false);
        var material = Read<Material>(view, "_lockedIconMaterial");
        var cardIcon = Read<Image[]>(view, "_cardIcons")[lockedIndex];
        checks.Check(!view.HasChanges && view.EquippedCount == 2 && !view.GetEquippedIds().Contains(lockedId), "궁극기 재잠금 committed/draft 모두 제거");
        checks.Check(!ornaments[2].enabled && !Read<Image[]>(view, "_cardUltimateFrames")[lockedIndex].enabled && !Read<Image>(view, "_detailUltimateFrame").enabled, "잠금 궁극기 장식 모든 위치 숨김");
        checks.Check(material != null && cardIcon.material == material && cardIcon.color == new Color(.22f, .22f, .22f, 1f), "잠금 실루엣/회색 본체");
        checks.Check(Read<Image[]>(view, "_cardLockIcons")[lockedIndex].enabled && Read<Image>(view, "_detailLockIcon").enabled, "목록/상세 자물쇠");
        view.ShowCategory(1); view.SelectSkill(lockedIndex);
        checks.Check(view.SelectedSkillId == lockedId && Read<UISkillArtButton[]>(view, "_cards")[lockedIndex].gameObject.activeSelf, "잠긴 궁극기도 딜 목록/선택 유지");
        checks.Check(Read<TMP_Text>(view, "_detailName").text == "미발견" && Read<TMP_Text>(view, "_detailDescription").text == "해금 후 정보 확인 가능" && Read<TMP_Text>(view, "_detailMetadata").text == string.Empty, "잠금 상세 이름/설명/메타데이터 마스킹");
        checks.Check(Read<TMP_Text>(view, "_effectValue").text == "—" && Read<TMP_Text>(view, "_cooldown").text == "—" && !Read<UISkillArtButton>(view, "_equipButton").interactable, "잠금 효과/쿨타임/장착 버튼 차단");
        view.EquipSelected();
        checks.Check(view.EquippedCount == 2 && !view.HasChanges, "잠금 직접 EquipSelected 차단");
        state.SetUnlocked(lockedId, true);
        checks.Check(cardIcon.color == Color.white && cardIcon.material == style.GetIconMaterial(eSkillPreviewCategory.DAMAGE) && Read<Image>(view, "_detailUltimateFrame").enabled, "재해금 아이콘/궁극기 상세 복원");
        view.EquipSelected(); view.SavePreview();
        checks.Check(view.EquippedCount == 3 && ornaments[2].enabled && !view.HasChanges, "재해금 후 명시적 장착/저장 복원");
        view.ShowCategory(0);
        // 누락된 표시 데이터와 명시적 재잠금을 구분하는 기존 회귀 방지 항목이다.
        int[] committed = (int[])Read<int[]>(view, "_committed").Clone();
        Write(view, "_catalog", null);
        try { view.ShowCategory(0); checks.Check(Read<int[]>(view, "_committed").SequenceEqual(committed), "일시적 null 카탈로그는 저장 구성을 삭제하지 않음"); }
        finally { Write(view, "_catalog", catalog); view.ShowCategory(0); }
    }

    private static void ValidateSaveFlow(UISkillLoadoutPreview view, UILobbyOverlayView host, UIPopupController controller, Checks checks)
    {
        var panel = view.GetComponent<UIPopupPanel>();
        var save = Read<UISkillArtButton>(view, "_saveButton");
        string[] before = view.GetEquippedIds();
        int events = 0;
        Action<IReadOnlyList<string>> onSave = ids => events++;
        view.SaveRequested += onSave;
        try
        {
            view.SavePreview();
            checks.Check(events == 0 && !save.interactable && !view.HasChanges, "변경 없는 저장 버튼/직접 호출 무동작");
            view.SelectEquippedSlot(1); view.UnequipSelected();
            checks.Check(save.interactable && view.HasChanges && view.EquippedCount == 2, "편집 후 저장 버튼 활성");
            host.CloseTop();
            checks.Check(view.IsExitConfirmationOpen && controller.OpenCount == 2 && !panel.GetComponent<CanvasGroup>().interactable, "공용 뒤로는 미저장 확인창/배경 입력 차단");
            view.EquipSelected(); view.SavePreview();
            checks.Check(view.EquippedCount == 2 && events == 0, "미저장 확인창 중 장착/저장 차단");
            view.CancelExit();
            checks.Check(!view.IsExitConfirmationOpen && view.HasChanges && view.EquippedCount == 2 && controller.IsTopPopup(panel), "취소는 편집 유지");
            host.CloseTop(); view.ConfirmExitWithoutSaving();
            checks.Check(controller.OpenCount == 0 && !view.gameObject.activeSelf && events == 0, "폐기는 저장 없이 확인창/스킬 닫기");
            host.OpenOverlayPopup(panel);
            checks.Check(view.GetEquippedIds().SequenceEqual(before) && !view.HasChanges && !save.interactable, "재열기 마지막 저장 구성 복원");
            checks.Check(IsAtTop(Read<ScrollRect>(view, "_ownedScrollRect")), "재열기 스크롤 상단 초기화");
            view.SelectEquippedSlot(1); view.UnequipSelected(); save.onClick.Invoke();
            checks.Check(events == 1 && !view.HasChanges && !save.interactable, "저장 버튼 연결/이벤트 1회/즉시 비활성");
            string[] saved = view.GetEquippedIds();
            host.CloseTop(); host.OpenOverlayPopup(panel);
            checks.Check(view.GetEquippedIds().SequenceEqual(saved) && !view.IsExitConfirmationOpen, "저장한 편집 구성 세션 재열기 유지");
        }
        finally { view.SaveRequested -= onSave; }
    }

    // 1단계: 실제 스킬 화면만 연 상태에서 호출한 뒤 Game View를 한 프레임 이상 렌더한다.
    public static string PreparePointerScroll()
    {
        if (_pointerSession != null) throw new InvalidOperationException("이전 포인터 검사를 먼저 완료하거나 CancelPointerScroll()를 호출하세요.");
        UILobbyOverlayView host = RequireHost();
        var view = host.GetComponentsInChildren<UISkillLoadoutPreview>(true).Single();
        var controller = host.GetComponent<UIPopupController>();
        var scroll = Read<ScrollRect>(view, "_ownedScrollRect");
        if (controller == null || controller.OpenCount != 1 || !controller.IsTopPopup(view.GetComponent<UIPopupPanel>()) || !view.isActiveAndEnabled || view.HasChanges || view.IsExitConfirmationOpen || scroll == null)
            throw new InvalidOperationException("저장 상태의 스킬 화면만 연 뒤 포인터 검사를 준비하세요.");
        if (!view.IsSkillUnlocked(SKILL_COUNT - 1)) throw new InvalidOperationException("마지막 궁극기가 해금된 미리보기 상태에서 포인터 검사를 준비하세요.");
        var session = new PointerSession
        {
            View = view, Scroll = scroll, Category = view.CategoryIndex, SelectedId = view.SelectedSkillId,
            Normalized = scroll.normalizedPosition, Focus = EventSystem.current.currentSelectedGameObject,
            SavedIds = view.GetEquippedIds(), Frame = Time.frameCount, Phase = 0
        };
        _pointerSession = session;
        try { view.ShowCategory(0); }
        catch { CancelPointerScroll(); throw; }
        return "포인터 검사 준비됨. Game View를 1프레임 이상 렌더한 뒤 ValidatePointerScroll()를 호출하세요.";
    }

    // 2단계에서 실제 휠 이벤트를 보내고 반환한다. 다시 렌더한 뒤 같은 메서드를 호출하면 3단계 실제 클릭을 검사한다.
    public static string ValidatePointerScroll()
    {
        RequireHost();
        var session = _pointerSession;
        if (session == null || session.View == null) throw new InvalidOperationException("스킬창을 연 뒤 PreparePointerScroll(), Game View 렌더, ValidatePointerScroll() 순서로 실행하세요.");
        if (Time.frameCount <= session.Frame) throw new InvalidOperationException("Game View 렌더 프레임이 아직 진행되지 않았습니다. 다음 프레임 후 다시 호출하세요.");
        var view = session.View;
        var cards = Read<UISkillArtButton[]>(view, "_cards");
        var checks = session.Checks;
        try
        {
            checks.Check(view.isActiveAndEnabled && !view.IsExitConfirmationOpen && cards.Length == SKILL_COUNT, "포인터 검사 중 스킬 화면/19장 유지");
            if (session.Phase == 0)
            {
                checks.Check(session.Scroll.viewport.rect.height > 0 && cards[0].image.depth >= 0, "상단 카드/Viewport 실제 렌더 준비");
                checks.Check(Mathf.Abs(session.Scroll.verticalNormalizedPosition - 1f) < .001f, "휠 검사 시작은 목록 상단");
                RaycastResult hit = RaycastCenter(session.Scroll.viewport, out PointerEventData pointer);
                GameObject handler = ExecuteEvents.GetEventHandler<IScrollHandler>(hit.gameObject);
                checks.Check(handler == session.Scroll.gameObject, "Viewport 최상위 Raycast의 스크롤 처리 대상 일치: " + hit.gameObject.name);
                pointer.pointerCurrentRaycast = hit;
                pointer.scrollDelta = new Vector2(0f, -100f);
                checks.Check(ExecuteEvents.Execute(handler, pointer, ExecuteEvents.scrollHandler), "실제 ScrollHandler 휠 이벤트 전달");
                checks.Check(session.Scroll.verticalNormalizedPosition < .01f, "휠 이벤트로 목록 하단 도달");
                session.Phase = 1;
                session.Frame = Time.frameCount;
                return checks.Report("pointer scroll phase 1") + "\n휠 이동 후 Game View를 1프레임 이상 렌더하고 ValidatePointerScroll()를 다시 호출하세요.";
            }
            var last = cards[SKILL_COUNT - 1];
            checks.Check(last.image.depth >= 0 && IsCenterInsideViewport(session.Scroll, last.image.rectTransform), "마지막 궁극기 카드가 렌더된 Viewport 안에 있음");
            RaycastResult lastHit = RaycastCenter(last.image.rectTransform, out PointerEventData click);
            GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(lastHit.gameObject);
            checks.Check(clickHandler == last.gameObject && last.isActiveAndEnabled && last.IsInteractable(), "마지막 카드 최상위 Raycast/클릭 대상 일치: " + lastHit.gameObject.name);
            click.pointerCurrentRaycast = lastHit; click.pointerPressRaycast = lastHit;
            click.pointerPress = clickHandler; click.rawPointerPress = lastHit.gameObject;
            click.eligibleForClick = true; click.clickCount = 1; click.clickTime = Time.unscaledTime;
            checks.Check(ExecuteEvents.Execute(clickHandler, click, ExecuteEvents.pointerDownHandler), "마지막 카드 pointer down");
            checks.Check(ExecuteEvents.Execute(clickHandler, click, ExecuteEvents.pointerUpHandler), "마지막 카드 pointer up");
            checks.Check(ExecuteEvents.Execute(clickHandler, click, ExecuteEvents.pointerClickHandler), "마지막 카드 pointer click");
            var entry = Read<UISkillPreviewCatalogSO>(view, "_catalog").Entries[SKILL_COUNT - 1];
            checks.Check(entry.IsUltimate && view.SelectedSkillId == entry.Id && last.IsChosen, "마지막 궁극기 실제 클릭 선택");
            checks.Check(Read<Image>(view, "_detailUltimateFrame").enabled && last.image.overrideSprite.name == "Card_Selected_Gold", "궁극기 상세 장식/선택 Gold 표시");
            checks.Check(!IsCenterInsideViewport(session.Scroll, cards[0].image.rectTransform), "상단 첫 카드는 Viewport 밖으로 이동");
            RaycastResult clippedHit = RaycastCenter(cards[0].image.rectTransform, out PointerEventData ignored, false);
            checks.Check(clippedHit.gameObject == null || ExecuteEvents.GetEventHandler<IPointerClickHandler>(clippedHit.gameObject) != cards[0].gameObject, "Viewport 밖 첫 카드 Raycast 차단");
            checks.Check(!view.HasChanges && view.GetEquippedIds().SequenceEqual(session.SavedIds), "휠/선택 입력은 장착 저장 상태 불변");
            string report = checks.Report("pointer scroll");
            CancelPointerScroll();
            return report;
        }
        catch { CancelPointerScroll(); throw; }
    }

    public static void CancelPointerScroll()
    {
        PointerSession session = _pointerSession;
        _pointerSession = null;
        if (session == null || session.View == null) return;
        session.View.ShowCategory(session.Category);
        var catalog = Read<UISkillPreviewCatalogSO>(session.View, "_catalog");
        int selected = catalog != null ? Enumerable.Range(0, catalog.Entries.Count).Where(index => catalog.Entries[index].Id == session.SelectedId).DefaultIfEmpty(-1).First() : -1;
        if (selected >= 0) session.View.SelectSkill(selected);
        if (session.Scroll != null) { session.Scroll.StopMovement(); session.Scroll.normalizedPosition = session.Normalized; }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(session.Focus != null && session.Focus.activeInHierarchy ? session.Focus : null);
    }

    private sealed class PointerSession
    {
        public UISkillLoadoutPreview View;
        public ScrollRect Scroll;
        public int Category;
        public string SelectedId;
        public Vector2 Normalized;
        public GameObject Focus;
        public string[] SavedIds;
        public int Frame;
        public int Phase;
        public readonly Checks Checks = new Checks();
    }

    private static void ClearEquipped(UISkillLoadoutPreview view, UISkillPreviewCatalogSO catalog)
    {
        view.ShowCategory(0);
        foreach (string id in view.GetEquippedIds())
        {
            view.SelectSkill(Enumerable.Range(0, catalog.Entries.Count).First(index => catalog.Entries[index].Id == id));
            view.UnequipSelected();
        }
    }

    private static void ValidateMissingReferences(GameObject root, Checks checks)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            Component[] components = child.GetComponents<Component>();
            checks.Check(components.All(component => component != null), child.name + " Missing Script 없음");
            foreach (Component component in components)
            {
                var so = new SerializedObject(component);
                var iterator = so.GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
                        checks.Check(false, child.name + " / " + component.GetType().Name + "." + iterator.propertyPath + " Missing Reference");
            }
        }
    }

    private static UILobbyOverlayView RequireHost()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!Application.isPlaying || scene.path != SCENE) throw new InvalidOperationException("UI_Lobby_MutedPreview Play Mode가 필요합니다.");
        if (EventSystem.current == null || !EventSystem.current.isActiveAndEnabled || EventSystem.current.currentInputModule == null)
            throw new InvalidOperationException("활성 EventSystem/InputModule이 필요합니다.");
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UILobbyOverlayView>(true)).Single();
    }

    private static RosterEntry[] ReadRoster()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", JSON_PATH));
        if (!File.Exists(path)) throw new InvalidOperationException("19종 표 JSON 누락: " + path);
        string json = File.ReadAllText(path).Trim();
        var document = JsonUtility.FromJson<RosterDocument>(json.StartsWith("[", StringComparison.Ordinal) ? "{\"Entries\":" + json + "}" : json);
        if (document?.Entries == null || document.Entries.Length != SKILL_COUNT || document.Entries.Any(entry => entry == null))
            throw new InvalidOperationException("19개 항목이 모두 있는 JSON이 필요합니다.");
        return document.Entries;
    }

    private static string[] ReadPrefs(RosterEntry[] roster)
    {
        var ids = new HashSet<string>(roster.SelectMany(entry => new[] { entry.Id, entry.Name }), StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/01.Scripts/Sandbox/Resources/Skills" }))
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;
            var property = new SerializedObject(asset).FindProperty("skillId");
            if (property != null && !string.IsNullOrEmpty(property.stringValue)) ids.Add(property.stringValue);
        }
        var keys = ids.Select(id => "OZGL2.Skill.Unlock." + id).Concat(new[] { "OZGL2.Skill.SP", "OZGL2.Skill.Milestone" }).OrderBy(key => key, StringComparer.Ordinal);
        return keys.Select(key => key + "=" + PlayerPrefs.HasKey(key) + ":" + PlayerPrefs.GetInt(key, 0)).Append("OZGL2.Skill.Equip=" + PlayerPrefs.HasKey("OZGL2.Skill.Equip") + ":" + PlayerPrefs.GetString("OZGL2.Skill.Equip", string.Empty)).ToArray();
    }

    private static string StateFingerprint(UILobbyCollectionState state) => EditorJsonUtility.ToJson(state) + "|" + Read<bool>(state, "_hasInitialized") + "|" +
        string.Join(",", Read<Dictionary<string, bool>>(state, "_unlockOverrides").OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value)) + "|" +
        string.Join(",", Read<Dictionary<string, int>>(state, "_achievementProgress").OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));

    private static void Rebuild(ScrollRect scroll)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
    }

    private static bool IsCenterInsideViewport(ScrollRect scroll, RectTransform rect)
    {
        Vector3 center = rect.TransformPoint(rect.rect.center);
        return scroll.viewport.rect.Contains(scroll.viewport.InverseTransformPoint(center));
    }

    private static bool HasValidWorldBounds(RectTransform rect)
    {
        if (rect == null || rect.rect.width <= 0f || rect.rect.height <= 0f) return false;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners.All(point => IsFinite(point.x) && IsFinite(point.y) && IsFinite(point.z)) &&
            (corners[1] - corners[0]).sqrMagnitude > .000001f && (corners[3] - corners[0]).sqrMagnitude > .000001f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsAtTop(ScrollRect scroll)
    {
        // Content가 Viewport보다 짧으면 Unity의 normalized getter가 0을 반환할 수 있어 실제 상단 위치를 비교한다.
        Vector3 contentTop = scroll.content.TransformPoint(new Vector3(scroll.content.rect.center.x, scroll.content.rect.yMax, 0f));
        return Mathf.Abs(scroll.viewport.InverseTransformPoint(contentTop).y - scroll.viewport.rect.yMax) < .5f;
    }

    private static RaycastResult RaycastCenter(RectTransform rect, out PointerEventData pointer, bool requireHit = true)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null) throw new InvalidOperationException("포인터 대상 Canvas 누락: " + rect.name);
        Canvas root = canvas.rootCanvas;
        Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1, position = position, pressPosition = position };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        if (requireHit && hits.Count == 0) throw new InvalidOperationException("Raycast 결과 없음. Game View 렌더/GraphicRaycaster 확인: " + rect.name);
        return hits.Count > 0 ? hits[0] : default;
    }

    private static T Read<T>(object target, string name)
    {
        FieldInfo field = target?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new InvalidOperationException("검증 필드 누락: " + name);
        return (T)field.GetValue(target);
    }

    private static void Write(object target, string name, object value)
    {
        FieldInfo field = target?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new InvalidOperationException("검증 필드 누락: " + name);
        field.SetValue(target, value);
    }
}
