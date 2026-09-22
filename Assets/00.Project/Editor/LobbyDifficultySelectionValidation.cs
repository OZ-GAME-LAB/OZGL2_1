using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>대상 로비 Play Mode에서만 임시 선택을 변경하고 원래 상태로 복구한다. 에셋/씬은 저장하지 않는다.</summary>
public static class LobbyDifficultySelectionValidation
{
    [MenuItem("Tools/OZGL2/Lobby/Validate Difficulty Selection (Play Mode)")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!Application.isPlaying || scene.path != LobbyDifficultySelectionBuilder.SCENE_PATH)
            throw new InvalidOperationException("대상 로비의 Play Mode에서 실행하세요.");
        UILobbyDifficultySelector view = scene.GetRootGameObjects().First(root => root.name == "Canvas_Lobby")
            .transform.Find("StageSelection").GetComponent<UILobbyDifficultySelector>();
        if (view == null || view.IsTransitioning) throw new InvalidOperationException("선택 제어부가 없거나 전환 중입니다.");
        Transform rootTransform = view.transform;
        string[] slotPaths = { "PreviousStage", "CurrentStage", "NextStage_Locked" };
        UILobbyDifficultySlotView[] slots = slotPaths.Select(path => rootTransform.Find(path).GetComponent<UILobbyDifficultySlotView>()).ToArray();
        CanvasGroup[] frames = slots.Select(slot => slot.transform.Find("VisualLayers/Frame").GetComponent<CanvasGroup>()).ToArray();
        RectTransform[] motion = slots.Select(slot => slot.transform.Find("VisualLayers/ArtworkMask/ArtworkRoot/ArtworkMotion").GetComponent<RectTransform>()).ToArray();
        TMP_Text[] labels = {
            rootTransform.Find("PreviousStage/PreviousCaption/Label").GetComponent<TMP_Text>(),
            rootTransform.Find("CurrentStage/StageRecord/RecordText").GetComponent<TMP_Text>(),
            rootTransform.Find("NextStage_Locked/LockedCaption/Label").GetComponent<TMP_Text>() };
        Button previous = rootTransform.Find("PreviousArrow").GetComponent<Button>();
        Button next = rootTransform.Find("NextArrow").GetComponent<Button>();
        Button start = rootTransform.Find("CurrentStage/StartButton").GetComponent<Button>();
        LobbyDifficultyArtworkSetSO originalSet = view.ArtworkSet;
        eLobbyDifficulty originalDifficulty = view.SelectedDifficulty;
        bool startWasInteractable = start.interactable;
        string catalogBefore = EditorJsonUtility.ToJson(view.Catalog);
        string setBefore = EditorJsonUtility.ToJson(originalSet);
        Vector3 startPosition = start.transform.position;
        Vector3[] framePositions = frames.Select(frame => frame.transform.position).ToArray();
        Vector3[] labelPositions = labels.Select(label => label.transform.position).ToArray();
        Sprite[] frameSprites = frames.Select(frame => frame.GetComponent<Image>().sprite).ToArray();
        int checks = 0;
        int events = 0;
        Action<eLobbyDifficulty> onSelected = _ => events++;
        Action<bool, string> check = (passed, description) =>
        {
            if (!passed) throw new InvalidOperationException("난이도 검사 실패: " + description);
            checks++;
        };
        view.SelectionChanged += onSelected;
        try
        {
            check(view.TryValidate(out _), "필수 Inspector 참조");
            check(!view.TrySelect((eLobbyDifficulty)99), "미지원 난이도 거부");
            check(!view.SetArtworkSet(null), "누락된 아트 세트 거부");
            check(previous.colors.disabledColor.a < 0.7f && next.colors.disabledColor.a < 0.7f, "비활성 화살표 시각 구분");
            foreach (string setPath in new[] { LobbyDifficultySelectionBuilder.CASTLE_SET_PATH, LobbyDifficultySelectionBuilder.HERO_SET_PATH })
            {
                LobbyDifficultyArtworkSetSO set = AssetDatabase.LoadAssetAtPath<LobbyDifficultyArtworkSetSO>(setPath);
                int eventsBeforeSwap = events;
                eLobbyDifficulty difficultyBeforeSwap = view.SelectedDifficulty;
                check(view.SetArtworkSet(set), "아트 세트 교체");
                check(view.SelectedDifficulty == difficultyBeforeSwap && events == eventsBeforeSwap, "아트 교체는 선택값/이벤트 무변경");
                for (int selected = 0; selected < 3; selected++)
                {
                    view.TrySelect((eLobbyDifficulty)selected, false);
                    check(previous.interactable == (selected > 0), "쉬움 왼쪽 경계");
                    check(next.interactable == (selected < 2), "어려움 오른쪽 경계");
                    for (int slotIndex = 0; slotIndex < 3; slotIndex++)
                    {
                        int entryIndex = selected + slotIndex - 1;
                        bool hasEntry = entryIndex >= 0 && entryIndex < 3;
                        UILobbyStageCardView card = slots[slotIndex].GetComponent<UILobbyStageCardView>();
                        check(slots[slotIndex].HasEntry == hasEntry, "유효한 인접 난이도만 표시");
                        check(!card.IsLocked, "고정 잠금 목업 해제");
                        check(card.FrameSprite == frameSprites[slotIndex], "자리별 기존 프레임 유지");
                        check(card.ArtworkSprite == (hasEntry ? set.GetArtwork((eLobbyDifficulty)entryIndex) : null), "슬롯별 난이도 그림 일치");
                        check(labels[slotIndex].text == (hasEntry ? view.Catalog.GetEntry(entryIndex).DisplayName : string.Empty), "난이도 TMP 일치");
                        check(Mathf.Approximately(frames[slotIndex].alpha, hasEntry ? 1f : 0f), "양끝의 빈 자리 숨김");
                    }
                }
            }

            view.SetArtworkSet(originalSet);
            view.TrySelect(eLobbyDifficulty.NORMAL, false);
            events = 0;
            start.interactable = true;
            next.onClick.Invoke();
            check(view.IsTransitioning && !previous.interactable && !next.interactable && !start.interactable, "전환 중 입력 차단");
            Sequence transition = GetTransition(view);
            check(transition != null, "소유 Sequence 생성");
            transition.Pause();
            transition.GotoWithCallbacks(0.07f, false);
            check(frames[1].alpha > 0f && frames[1].alpha < 1f && motion[1].anchoredPosition.x < 0f, "페이드아웃과 그림 좌측 이동");
            check(view.SelectedDifficulty == eLobbyDifficulty.NORMAL && events == 0, "전환 전 선택값 유지");
            check(!view.TrySelect(eLobbyDifficulty.EASY), "연타/역방향 중복 전환 차단");
            next.onClick.Invoke();
            check(GetTransition(view) == transition, "Sequence 중복 생성 없음");
            transition.GotoWithCallbacks(0.15f, false);
            check(labels[1].text == "어려움" && view.SelectedDifficulty == eLobbyDifficulty.NORMAL, "안 보이는 시점 내용 교체, 선택 확정은 지연");
            check(motion[1].anchoredPosition.x > 0f && frames[1].alpha < 1f, "그림 반대쪽에서 페이드인");
            for (int i = 0; i < 3; i++)
            {
                check(frames[i].transform.position == framePositions[i], "테두리 위치 고정");
                check(labels[i].transform.position == labelPositions[i], "글자 위치 고정");
                check(frames[i].GetComponent<Image>().sprite == frameSprites[i], "테두리 Sprite 고정");
            }
            transition.Complete(true);
            check(view.SelectedDifficulty == eLobbyDifficulty.HARD && !view.IsTransitioning && events == 1, "전환 완료/이벤트 한 번");
            check(start.interactable && !next.interactable && previous.interactable, "입력 복구와 우측 경계");
            check(motion[1].anchoredPosition == Vector2.zero && Mathf.Approximately(frames[1].alpha, 1f), "전환 잔여 위치/투명도 없음");

            view.TrySelect(eLobbyDifficulty.NORMAL, false);
            int committedEvents = events;
            view.SelectNext();
            GetTransition(view).GotoWithCallbacks(0.15f, false);
            view.enabled = false;
            check(!view.IsTransitioning && view.SelectedDifficulty == eLobbyDifficulty.NORMAL && labels[1].text == "보통", "비활성화 시 확정 상태 복원");
            check(start.interactable && events == committedEvents, "취소 시 Start 복구/선택 이벤트 없음");
            view.enabled = true;
            next.onClick.Invoke();
            GetTransition(view).Complete(true);
            check(events == committedEvents + 1, "재활성화 입력 중복 구독 없음");

            view.TrySelect(eLobbyDifficulty.NORMAL, false);
            view.SelectNext();
            GetTransition(view).Kill(false);
            check(!view.IsTransitioning && labels[1].text == "보통" && start.interactable, "외부 Tween 종료도 정리");
            start.interactable = false;
            view.SelectNext();
            GetTransition(view).Complete(true);
            check(!start.interactable, "원래 비활성인 Start를 강제로 켜지 않음");
            start.interactable = true;
            view.TrySelect(eLobbyDifficulty.NORMAL, false);
            view.SelectNext();
            view.gameObject.SetActive(false);
            view.gameObject.SetActive(true);
            check(!view.IsTransitioning && view.SelectedDifficulty == eLobbyDifficulty.NORMAL && labels[1].text == "보통", "화면 닫기/재열기 복원");

            Canvas.ForceUpdateCanvases();
            check(start.transform.position == startPosition, "전투 준비 위치 고정");
            check(start.onClick.GetPersistentEventCount() == 1 && start.onClick.GetPersistentMethodName(0) == "LoadScene", "기존 전투 준비 콜백 유지");
            check(EventSystem.current != null, "EventSystem 연결");
            foreach (Button button in new[] { previous, next, start })
            {
                RectTransform rect = (RectTransform)button.transform;
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                PointerEventData pointer = new PointerEventData(EventSystem.current) { position = point };
                List<RaycastResult> hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "버튼 Raycast 도달: " + button.name);
            }
            check(EditorJsonUtility.ToJson(view.Catalog) == catalogBefore, "Catalog SO 런타임 무변경");
            check(EditorJsonUtility.ToJson(originalSet) == setBefore, "Artwork Set SO 런타임 무변경");
            return "Lobby difficulty selection: " + checks + " checks passed.";
        }
        finally
        {
            view.enabled = true;
            view.gameObject.SetActive(true);
            view.SelectionChanged -= onSelected;
            view.SetArtworkSet(originalSet);
            view.TrySelect(originalDifficulty, false);
            start.interactable = startWasInteractable;
        }
    }

    private static Sequence GetTransition(UILobbyDifficultySelector view)
    {
        return typeof(UILobbyDifficultySelector).GetField("_transition", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(view) as Sequence;
    }
}
