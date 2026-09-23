using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>기록 호버의 표시/전환 계약을 검사하고 원래 런타임 기록과 선택 상태를 복구한다. 씬/에셋은 저장하지 않는다.</summary>
public static class LobbyDifficultyRecordPanelValidation
{
    private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;
    private const double POINTER_PROBE_TIMEOUT_SECONDS = 60d;
    private static Mouse _probeMouse;
    private static bool _isPointerProbeActive;
    private static bool _ownsProbeMouse;
    private static Vector2 _originalInputPosition;
    private static Vector2 _probePosition;
    private static Vector2 _lastObservedProbePosition;
    private static double _probeDeadline;
    private static int _probeQueuedEvents;
    private static int _probeInputUpdates;
    private static int _probeMatchingUpdates;
    private static int _lastObservedProbeFrame;
    private static bool _lastProbeUpdateMatched;
    private static string _probeStopReason = "실행 전";

    public static bool IsPointerProbeActive => _isPointerProbeActive;

    /// <summary>
    /// 최대 60초간 UI Input Module이 소비할 Mouse 위치 이벤트만 주입한다.
    /// OS 커서 이동, 클릭, 물리 장치 제거, Input System 설정 변경은 하지 않는다.
    /// </summary>
    public static string BeginPointerProbe(Vector2 screenPosition)
    {
        ValidatePointerProbePosition(screenPosition);
        if (!Application.isPlaying || SceneManager.GetActiveScene().path != LobbyDifficultySelectionBuilder.SCENE_PATH)
            throw new InvalidOperationException("대상 로비의 Play Mode에서만 포인터 검사를 실행할 수 있습니다.");
        if (_isPointerProbeActive) return MovePointerProbe(screenPosition);

        _probeMouse = Mouse.current;
        _ownsProbeMouse = _probeMouse == null;
        if (_ownsProbeMouse) _probeMouse = InputSystem.AddDevice<Mouse>("LobbyDifficultyRecordPointerProbe");
        _originalInputPosition = _probeMouse.position.ReadValue();
        _probePosition = screenPosition;
        _lastObservedProbePosition = _originalInputPosition;
        _probeQueuedEvents = 0;
        _probeInputUpdates = 0;
        _probeMatchingUpdates = 0;
        _lastObservedProbeFrame = -1;
        _lastProbeUpdateMatched = false;
        _probeStopReason = string.Empty;
        _probeDeadline = EditorApplication.timeSinceStartup + POINTER_PROBE_TIMEOUT_SECONDS;
        _isPointerProbeActive = true;
        UnsubscribePointerProbe();
        EditorApplication.update += UpdatePointerProbe;
        EditorApplication.playModeStateChanged += HandleProbePlayModeChange;
        EditorApplication.quitting += HandleProbeQuitting;
        AssemblyReloadEvents.beforeAssemblyReload += HandleProbeAssemblyReload;
        InputSystem.onBeforeUpdate += BeforeProbeInputUpdate;
        InputSystem.onAfterUpdate += AfterProbeInputUpdate;
        QueueProbePosition();
        EditorApplication.QueuePlayerLoopUpdate();
        return GetPointerProbeStatus();
    }

    public static string MovePointerProbe(Vector2 screenPosition)
    {
        ValidatePointerProbePosition(screenPosition);
        if (!_isPointerProbeActive) throw new InvalidOperationException("BeginPointerProbe로 검사를 먼저 시작하세요.");
        if (!CanContinuePointerProbe()) return GetPointerProbeStatus();
        _probePosition = screenPosition;
        _lastProbeUpdateMatched = false;
        QueueProbePosition();
        EditorApplication.QueuePlayerLoopUpdate();
        return GetPointerProbeStatus();
    }

    public static string EndPointerProbe()
    {
        StopPointerProbe("명시적 종료");
        return GetPointerProbeStatus();
    }

    public static string GetPointerProbeStatus()
    {
        double remaining = _isPointerProbeActive ? Math.Max(0d, _probeDeadline - EditorApplication.timeSinceStartup) : 0d;
        return "Pointer probe active=" + _isPointerProbeActive + ", target=" + _probePosition
            + ", observed=" + _lastObservedProbePosition + ", currentMouseMatched=" + _lastProbeUpdateMatched
            + ", inputUpdates=" + _probeInputUpdates + ", matchingUpdates=" + _probeMatchingUpdates
            + ", queuedEvents=" + _probeQueuedEvents + ", observedFrame=" + _lastObservedProbeFrame
            + ", secondsRemaining=" + remaining.ToString("F1") + ", stopReason=" + _probeStopReason;
    }

    [MenuItem("Tools/OZGL2/Lobby/Stop Record Hover Pointer Probe")]
    public static void StopPointerProbeFromMenu() => Debug.Log(EndPointerProbe());

    private static void UpdatePointerProbe()
    {
        if (!CanContinuePointerProbe()) return;
        QueueProbePosition();
        // Time.timeScale을 바꾸거나 InputSystem.Update를 중복 호출하지 않고 정상 Player Loop를 요청한다.
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private static void BeforeProbeInputUpdate()
    {
        if (!_isPointerProbeActive || !IsPlayerInputUpdate()) return;
        if (!CanContinuePointerProbe()) return;
        // 네이티브 입력 큐가 소비되기 직전에 마지막 위치를 추가한다. 실제 반영 여부는 After에서 확인한다.
        QueueProbePosition();
    }

    private static void AfterProbeInputUpdate()
    {
        if (!_isPointerProbeActive || !IsPlayerInputUpdate() || _probeMouse == null || !_probeMouse.added) return;
        _probeInputUpdates++;
        _lastObservedProbeFrame = Time.frameCount;
        _lastObservedProbePosition = _probeMouse.position.ReadValue();
        _lastProbeUpdateMatched = Mouse.current == _probeMouse
            && (_lastObservedProbePosition - _probePosition).sqrMagnitude < 0.25f;
        if (_lastProbeUpdateMatched) _probeMatchingUpdates++;
    }

    private static bool IsPlayerInputUpdate()
    {
        InputUpdateType type = InputState.currentUpdateType;
        return type == InputUpdateType.Dynamic || type == InputUpdateType.Fixed || type == InputUpdateType.Manual;
    }

    private static void QueueProbePosition()
    {
        if (!_isPointerProbeActive || _probeMouse == null || !_probeMouse.added) return;
        InputSystem.QueueDeltaStateEvent(_probeMouse.position, _probePosition);
        _probeQueuedEvents++;
    }

    private static bool CanContinuePointerProbe()
    {
        if (!_isPointerProbeActive) return false;
        if (!Application.isPlaying || SceneManager.GetActiveScene().path != LobbyDifficultySelectionBuilder.SCENE_PATH)
            StopPointerProbe("Play Mode/대상 씬 종료");
        else if (_probeMouse == null || !_probeMouse.added)
            StopPointerProbe("검사 장치 연결 해제");
        else if (EditorApplication.timeSinceStartup >= _probeDeadline)
            StopPointerProbe("60초 제한 자동 종료");
        return _isPointerProbeActive;
    }

    private static void StopPointerProbe(string reason)
    {
        UnsubscribePointerProbe();
        bool wasActive = _isPointerProbeActive;
        _isPointerProbeActive = false;
        Mouse mouse = _probeMouse;
        bool owned = _ownsProbeMouse;
        _probeMouse = null;
        _ownsProbeMouse = false;
        if (!wasActive) return;
        _probeStopReason = reason;
        if (mouse == null || !mouse.added) return;
        // 직접 만든 가상 장치만 제거한다. 기존 물리 장치는 위치 이벤트로 입력 상태만 복구한다.
        if (owned) InputSystem.RemoveDevice(mouse);
        else InputSystem.QueueDeltaStateEvent(mouse.position, _originalInputPosition);
    }

    private static void UnsubscribePointerProbe()
    {
        EditorApplication.update -= UpdatePointerProbe;
        EditorApplication.playModeStateChanged -= HandleProbePlayModeChange;
        EditorApplication.quitting -= HandleProbeQuitting;
        AssemblyReloadEvents.beforeAssemblyReload -= HandleProbeAssemblyReload;
        InputSystem.onBeforeUpdate -= BeforeProbeInputUpdate;
        InputSystem.onAfterUpdate -= AfterProbeInputUpdate;
    }

    private static void HandleProbePlayModeChange(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            StopPointerProbe("Play Mode 종료");
    }

    private static void HandleProbeAssemblyReload() => StopPointerProbe("Assembly Reload");
    private static void HandleProbeQuitting() => StopPointerProbe("Editor 종료");

    private static void ValidatePointerProbePosition(Vector2 position)
    {
        if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.x) || float.IsInfinity(position.y))
            throw new ArgumentException("유효한 화면 좌표를 입력하세요.", nameof(position));
    }

    [MenuItem("Tools/OZGL2/Lobby/Validate Difficulty Record Hover (Play Mode)")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!Application.isPlaying || scene.path != LobbyDifficultySelectionBuilder.SCENE_PATH)
            throw new InvalidOperationException("대상 로비의 Play Mode에서 실행하세요.");
        Transform stage = scene.GetRootGameObjects().First(root => root.name == "Canvas_Lobby")
            .transform.Find("StageSelection");
        UILobbyDifficultySelector selector = stage.GetComponent<UILobbyDifficultySelector>();
        UILobbyDifficultyRecordPanel[] panels = stage.GetComponentsInChildren<UILobbyDifficultyRecordPanel>(true);
        if (selector == null || selector.IsTransitioning || panels.Length != 1 || !panels[0].isActiveAndEnabled)
            throw new InvalidOperationException("활성 기록 패널 1개와 전환 중이 아닌 난이도 선택기가 필요합니다.");
        UILobbyDifficultyRecordPanel view = panels[0];
        if (Get<Tween>(view, "_motion") != null)
            throw new InvalidOperationException("진행 중인 호버 애니메이션이 끝난 후 검사하세요.");

        RectTransform panel = Get<RectTransform>(view, "_panelRect");
        RectTransform reveal = Get<RectTransform>(view, "_revealRect");
        CanvasGroup records = Get<CanvasGroup>(view, "_recordGroup");
        TMP_Text wave = Get<TMP_Text>(view, "_waveValueLabel");
        TMP_Text time = Get<TMP_Text>(view, "_timeValueLabel");
        Transform current = stage.Find("CurrentStage");
        Transform caption = current.Find("StageRecord");
        TMP_Text title = caption.Find("RecordText").GetComponent<TMP_Text>();
        TMP_Text description = caption.Find("ClearTimeText").GetComponent<TMP_Text>();
        Button start = current.Find("StartButton").GetComponent<Button>();
        eLobbyDifficulty originalDifficulty = selector.SelectedDifficulty;
        LobbyDifficultyArtworkSetSO originalArtwork = selector.ArtworkSet;
        float originalTimeScale = Time.timeScale;
        bool originalStartInteractable = start.interactable;
        bool originalFocus = Get<bool>(view, "_hasFocus");
        float originalExpansion = view.Expansion;
        IDictionary dictionary = Get<IDictionary>(view, "_records");
        // 키와 값은 enum/불변 struct이므로 boxed 값 복사로 기존 기록을 모두 보존한다.
        List<DictionaryEntry> recordSnapshot = new List<DictionaryEntry>();
        foreach (DictionaryEntry item in dictionary) recordSnapshot.Add(item);
        string catalogBefore = EditorJsonUtility.ToJson(selector.Catalog);
        string artworkBefore = EditorJsonUtility.ToJson(originalArtwork);
        string startTransformBefore = EditorJsonUtility.ToJson(start.transform);
        string startButtonBefore = EditorJsonUtility.ToJson(start);
        Vector3 titlePosition = title.transform.position;
        Vector3 descriptionPosition = description.transform.position;
        Color waveColor = wave.color;
        Color timeColor = time.color;
        int checks = 0;
        List<bool> transitions = new List<bool>();
        Action<bool> onTransition = state => transitions.Add(state);
        Action<bool, string> check = (passed, descriptionText) =>
        {
            if (!passed) throw new InvalidOperationException("기록 호버 검사 실패: " + descriptionText);
            checks++;
        };
        selector.TransitionStateChanged += onTransition;
        try
        {
            // Editor 자동 검사 시 Game View 포커스와 무관하게 이벤트 핸들러의 계약을 검사한다.
            // 실제 마우스/포커스 이동은 별도 수동 검사로 남긴다.
            Invoke(view, "OnApplicationFocus", true);
            check(view.TryValidate(out _), "필수 Inspector 참조");
            check(view.transform.parent == current, "중앙 카드에만 별도 입력 면 배치");
            check(Get<UILobbyDifficultyRecordPanel>(current.GetComponent<UILobbyDifficultySlotView>(), "_recordPanel") == view,
                "중앙 슬롯의 투명 시점 바인딩 연결");
            check(view.GetComponent<Image>().raycastTarget && !view.transform.IsChildOf(caption), "표시 CanvasGroup과 호버 입력 면 분리");
            check(!caption.GetComponent<Image>().enabled, "기존 합성 프레임 비표시");
            check(reveal.GetComponent<RectMask2D>() != null, "기록 영역 Reveal 마스크");
            check(!records.interactable && !records.blocksRaycasts, "기록 시각 요소는 입력을 차단하지 않음");
            check(panel.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), "새 표시 Graphic의 Raycast 비활성");
            check(panel.pivot.y == 0f && ((RectTransform)view.transform).pivot.y == 0f, "하단 기준 확장 피벗");

            selector.TrySelect(eLobbyDifficulty.NORMAL, false);
            view.ClearAllRecords();
            Invoke(view, "SetExpansion", 0f);
            Canvas.ForceUpdateCanvases();
            float collapsedHeight = Get<float>(view, "_collapsedHeight");
            float expandedHeight = Get<float>(view, "_expandedHeight");
            Vector3 bottom = BottomCenter(panel);
            Vector3 hitBottom = BottomCenter((RectTransform)view.transform);
            RectTransform topCap = (RectTransform)panel.Find("TopCap");
            RectTransform bottomCap = (RectTransform)panel.Find("BottomCap");
            RectTransform leftRail = (RectTransform)panel.Find("LeftRail");
            RectTransform rightRail = (RectTransform)panel.Find("RightRail");
            Vector2 topSize = topCap.rect.size;
            Vector2 bottomSize = bottomCap.rect.size;
            float leftWidth = leftRail.rect.width;
            float rightWidth = rightRail.rect.width;
            float leftHeight = leftRail.rect.height;
            check(Approximately(panel.rect.height, collapsedHeight) && Approximately(reveal.rect.height, 0f), "접힌 패널/Reveal 높이");
            check(Approximately(records.alpha, 0f) && !view.IsExpanded, "접힌 상태의 기록 비표시");
            check(!view.HasRecord && wave.text == "기록 없음" && time.text == "--:--", "실제 기록 미연결 기본 문구");

            Invoke(view, "SetExpansion", 1f);
            Canvas.ForceUpdateCanvases();
            check(view.IsExpanded && Approximately(panel.rect.height, expandedHeight), "확장 높이");
            check(Approximately(reveal.rect.height, expandedHeight - collapsedHeight), "열린 공간만 Reveal 확장");
            check(Approximately(records.alpha, 1f), "확장 기록 불투명도");
            check(Approximately(BottomCenter(panel), bottom) && Approximately(BottomCenter((RectTransform)view.transform), hitBottom), "패널과 HitArea 하단 고정");
            check(Approximately(((RectTransform)view.transform).rect.height, expandedHeight), "펼쳐진 영역으로 HitArea 확장");
            check(topCap.rect.size == topSize && bottomCap.rect.size == bottomSize, "상하 장식 크기 고정");
            check(Approximately(leftRail.rect.width, leftWidth) && Approximately(rightRail.rect.width, rightWidth), "좌우 레일 두께 고정");
            check(Approximately(leftRail.rect.height - leftHeight, expandedHeight - collapsedHeight), "레일 길이만 확장");
            check(leftRail.GetComponent<Image>().type == Image.Type.Tiled && rightRail.GetComponent<Image>().type == Image.Type.Tiled,
                "레일 도트 확대 대신 타일 반복");
            check(title.transform.position == titlePosition && description.transform.position == descriptionPosition, "기존 난이도 문구 위치 고정");
            check(EditorJsonUtility.ToJson(start.transform) == startTransformBefore, "전투 준비 배치 보존");
            float fadeStart = Get<float>(view, "_recordFadeStart");
            Invoke(view, "SetExpansion", fadeStart * 0.5f);
            check(Approximately(records.alpha, 0f), "초기 확장 중 기록 숨김");
            Invoke(view, "SetExpansion", Mathf.Lerp(fadeStart, 1f, 0.5f));
            check(Approximately(records.alpha, 0.5f), "확장 진행률과 기록 페이드 연결");

            check(view.SetRecord(eLobbyDifficulty.NORMAL, 25, 522f), "유효 기록 입력");
            check(view.HasRecord && wave.text == "25 웨이브" && time.text == "08:42", "기록 값 표시");
            check(view.SetRecord(eLobbyDifficulty.NORMAL, 0, 0f) && wave.text == "0 웨이브" && time.text == "00:00", "0 기록도 유효");
            check(view.SetRecord(eLobbyDifficulty.NORMAL, 30) && wave.text == "30 웨이브" && time.text == "--:--", "시간 미확정 표시");
            check(view.SetRecord(eLobbyDifficulty.NORMAL, 40, 3723.9f) && time.text == "62:03", "60분 이상 총 분/초 및 소수초 버림");
            check(!view.SetRecord((eLobbyDifficulty)99, 1), "미지원 난이도 거부");
            check(!view.SetRecord(eLobbyDifficulty.NORMAL, -1), "음수 웨이브 거부");
            check(!view.SetRecord(eLobbyDifficulty.NORMAL, 1, -1f), "음수 시간 거부");
            check(!view.SetRecord(eLobbyDifficulty.NORMAL, 1, float.NaN), "NaN 시간 거부");
            check(!view.SetRecord(eLobbyDifficulty.NORMAL, 1, float.PositiveInfinity), "무한 시간 거부");
            check(wave.text == "40 웨이브" && time.text == "62:03", "잘못된 입력이 기존 기록을 덮어쓰지 않음");
            view.ClearRecord(eLobbyDifficulty.NORMAL);
            check(!view.HasRecord && wave.text == "기록 없음" && time.text == "--:--", "난이도 기록 삭제");
            view.Bind(null);
            check(!view.HasEntry && !view.HasRecord && Approximately(view.Expansion, 0f), "빈 바인딩은 기록과 확장 숨김");
            check(!view.IsRaycastLocationValid(Vector2.zero, null), "빈 슬롯은 호버 입력 제외");
            view.Bind(selector.SelectedEntry);
            check(view.HasEntry && view.DisplayedDifficulty == eLobbyDifficulty.NORMAL, "유효 난이도 재바인딩");
            check(view.IsRaycastLocationValid(Vector2.zero, null), "일반 상태의 호버 입력 허용");

            Invoke(view, "SetExpansion", 0f);
            Invoke(view, "AnimateTo", true);
            Tween expanding = Get<Tween>(view, "_motion");
            check(expanding != null && IsIndependentUpdate(expanding), "확장 Tween은 시간 배율 무관");
            expanding.Pause();
            expanding.GotoWithCallbacks(expanding.Duration() * 0.35f, false);
            float partialExpansion = view.Expansion;
            check(partialExpansion > 0f && partialExpansion < 1f, "확장 중간 상태");
            Invoke(view, "AnimateTo", false);
            Tween collapsing = Get<Tween>(view, "_motion");
            check(!expanding.IsActive() && collapsing != null && collapsing != expanding, "역방향 시 소유 Tween만 교체");
            check(Approximately(view.Expansion, partialExpansion), "역전 시 현재 높이 유지");
            collapsing.Pause();
            collapsing.GotoWithCallbacks(collapsing.Duration() * 0.35f, false);
            check(view.Expansion > 0f && view.Expansion < partialExpansion, "현재 높이에서 자연스럽게 접힘");
            float reversedExpansion = view.Expansion;
            view.OnPointerEnter(new PointerEventData(EventSystem.current));
            check(Get<Tween>(view, "_motion") == null && Approximately(view.Expansion, reversedExpansion), "접는 중 재진입은 유예 동안 높이 보존");
            check(Get<bool>(view, "_hasPendingPointerRequest") && Get<bool>(view, "_pendingExpanded"), "포인터 진입 유예 예약");
            Invoke(view, "AnimateTo", true);
            Get<Tween>(view, "_motion").Complete(true);
            check(view.IsExpanded && Get<Tween>(view, "_motion") == null, "확장 완료 후 Tween 참조 해제");
            view.OnPointerExit(new PointerEventData(EventSystem.current));
            check(Get<bool>(view, "_hasPendingPointerRequest") && !Get<bool>(view, "_pendingExpanded"), "포인터 이탈 유예 예약");
            Invoke(view, "AnimateTo", false);
            Get<Tween>(view, "_motion").Kill(false);
            check(Get<Tween>(view, "_motion") == null, "외부에서 소유 Tween 종료 시 참조 정리");
            Invoke(view, "SetExpansion", 0.5f);
            Invoke(view, "AnimateTo", true);
            view.enabled = false;
            check(Approximately(view.Expansion, 0f) && Get<Tween>(view, "_motion") == null, "비활성화 시 펼침/동작 정리");
            check(!Get<bool>(view, "_hasPendingPointerRequest") && Get<UILobbyDifficultySelector>(view, "_subscribedSelector") == null,
                "비활성화 시 지연 입력/이벤트 구독 정리");
            check(!view.IsRaycastLocationValid(Vector2.zero, null), "비활성 패널 입력 제외");
            view.enabled = true;
            check(Get<UILobbyDifficultySelector>(view, "_subscribedSelector") == selector && Approximately(view.Expansion, 0f), "재활성화 구독/접힌 상태 복구");
            Time.timeScale = 0f;
            Invoke(view, "AnimateTo", true);
            Tween pausedTimeMotion = Get<Tween>(view, "_motion");
            check(pausedTimeMotion != null && IsIndependentUpdate(pausedTimeMotion), "timeScale 0에서도 독립 업데이트 설정");
            pausedTimeMotion.Complete(true);
            check(view.IsExpanded, "timeScale 0 강제 타임라인 완료");
            Time.timeScale = originalTimeScale;

            view.SetRecord(eLobbyDifficulty.NORMAL, 12, 123f);
            view.SetRecord(eLobbyDifficulty.HARD, 48, 456f);
            transitions.Clear();
            check(selector.TrySelect(eLobbyDifficulty.HARD), "난이도 전환 시작");
            check(view.IsTransitionLocked && transitions.SequenceEqual(new[] { true }), "전환 시작 시 호버 잠금 알림");
            check(!view.IsRaycastLocationValid(Vector2.zero, null) && !Get<bool>(view, "_hasPendingPointerRequest"), "전환 중 HitArea와 지연 입력 해제");
            check(wave.text == "12 웨이브" && time.text == "02:03", "페이드아웃 중 이전 난이도 기록 유지");
            Get<Tween>(view, "_motion")?.Complete(true);
            Sequence transition = Get<Sequence>(selector, "_transition");
            transition.Pause();
            float fadeOut = Get<float>(selector, "_fadeOutDuration");
            transition.GotoWithCallbacks(fadeOut + 0.001f, false);
            check(view.DisplayedDifficulty == eLobbyDifficulty.HARD && selector.SelectedDifficulty == eLobbyDifficulty.NORMAL,
                "투명한 교체 시점 기록 바인딩, 선택 확정은 이후");
            check(wave.text == "48 웨이브" && time.text == "07:36" && title.text == "어려움", "표시 난이도와 기록이 함께 갱신됨");
            check(wave.color == waveColor && time.color == timeColor, "갱신 시 기록 표시 색 보존");
            transition.Complete(true);
            check(!view.IsTransitionLocked && selector.SelectedDifficulty == eLobbyDifficulty.HARD, "완료 시 호버 잠금 해제");
            check(transitions.SequenceEqual(new[] { true, false }), "완료 알림 true/false 각 1회");
            check(view.IsRaycastLocationValid(Vector2.zero, null), "전환 완료 후 입력 복구");

            selector.TrySelect(eLobbyDifficulty.NORMAL, false);
            transitions.Clear();
            selector.TrySelect(eLobbyDifficulty.HARD);
            transition = Get<Sequence>(selector, "_transition");
            transition.GotoWithCallbacks(fadeOut + 0.001f, false);
            transition.Kill(false);
            check(!view.IsTransitionLocked && view.DisplayedDifficulty == eLobbyDifficulty.NORMAL && selector.SelectedDifficulty == eLobbyDifficulty.NORMAL,
                "전환 중단 시 확정 난이도 복구/잠금 해제");
            check(wave.text == "12 웨이브" && time.text == "02:03" && title.text == "보통", "중단 후 이전 기록/문구 복구");
            check(transitions.SequenceEqual(new[] { true, false }), "외부 종료 알림 true/false 각 1회");
            transitions.Clear();
            selector.TrySelect(eLobbyDifficulty.HARD);
            selector.RefreshPresentation();
            check(!view.IsTransitionLocked && !selector.IsTransitioning && view.DisplayedDifficulty == selector.SelectedDifficulty,
                "전환 중 표시 갱신도 기록 잠금을 남기지 않음");
            check(transitions.SequenceEqual(new[] { true, false }), "표시 갱신 종료 알림 중복 없음");
            view.ClearAllRecords();
            check(!view.HasRecord && dictionary.Count == 0 && wave.text == "기록 없음", "전체 기록 비우기");
            check(EditorJsonUtility.ToJson(selector.Catalog) == catalogBefore && EditorJsonUtility.ToJson(originalArtwork) == artworkBefore,
                "SO에는 런타임 기록/진행도를 기록하지 않음");
            start.interactable = originalStartInteractable;
            check(EditorJsonUtility.ToJson(start) == startButtonBefore && EditorJsonUtility.ToJson(start.transform) == startTransformBefore,
                "전투 준비 Button/RectTransform 설정 보존");
            return "Lobby difficulty record hover: " + checks + " checks passed. 실제 포인터 왕복/팝업 가림/timeScale 0 실시간 경과는 별도 Play 검증이 필요합니다.";
        }
        finally
        {
            selector.TransitionStateChanged -= onTransition;
            Get<Sequence>(selector, "_transition")?.Kill(false);
            Invoke(view, "CancelMotion");
            view.enabled = true;
            selector.SetArtworkSet(originalArtwork);
            selector.TrySelect(originalDifficulty, false);
            // 표시 복구 중 생성됐을 수 있는 접힘 Tween도 이 패널 소유분만 정리한다.
            Invoke(view, "CancelMotion");
            dictionary.Clear();
            foreach (DictionaryEntry item in recordSnapshot) dictionary.Add(item.Key, item.Value);
            view.Bind(selector.SelectedEntry);
            Invoke(view, "OnApplicationFocus", originalFocus);
            Invoke(view, "SetExpansion", originalExpansion);
            start.interactable = originalStartInteractable;
            Time.timeScale = originalTimeScale;
        }
    }

    private static T Get<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, PRIVATE_INSTANCE);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        return (T)field.GetValue(target);
    }

    private static void Invoke(object target, string name, params object[] parameters)
    {
        MethodInfo method = target.GetType().GetMethod(name, PRIVATE_INSTANCE);
        if (method == null) throw new MissingMethodException(target.GetType().Name, name);
        method.Invoke(target, parameters);
    }

    private static Vector3 BottomCenter(RectTransform rect) => rect.TransformPoint(new Vector3(rect.rect.center.x, rect.rect.yMin, 0f));
    private static bool IsIndependentUpdate(Tween tween) =>
        (bool)typeof(Tween).GetField("isIndependentUpdate", PRIVATE_INSTANCE).GetValue(tween);
    private static bool Approximately(float left, float right) => Mathf.Abs(left - right) < 0.01f;
    private static bool Approximately(Vector3 left, Vector3 right) => Vector3.Distance(left, right) < 0.01f;
}
