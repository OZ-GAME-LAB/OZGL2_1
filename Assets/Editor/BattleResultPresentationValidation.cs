using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using DG.Tweening;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Play Mode의 임시 인스턴스만 검사한다. Scene/Prefab 저장과 실제 보상 지급은 수행하지 않는다.
public static class BattleResultPresentationValidation
{
    private const string PREFAB_PATH = "Assets/06.UI/BattleMutedPreview/Heraldry_Result_v1/Prefabs/Canvas_BattleResult_Heraldry.prefab";
    private const float TOLERANCE = 0.002f;
    private static readonly string[] GROUPS = { "_titleGroup", "_timeRow", "_killsRow", "_deploymentsRow", "_rewardRow" };
    private static bool _isRunning;
    private static int _caseId;
    public static string LastResult { get; private set; } = "아직 실행하지 않았습니다.";

    [MenuItem("Tools/OZGL2/Battle/Validate Result Presentation (Play Mode)")]
    public static async void Run()
    {
        if (_isRunning || !Application.isPlaying)
        {
            Debug.LogWarning("결과 연출 검증은 Play Mode에서 한 번씩 실행하세요.");
            return;
        }
        _isRunning = true;
        LastResult = "검증 중";
        float previousTimeScale = Time.timeScale;
        bool previousRunInBackground = Application.runInBackground;
        GameObject container = null;
        try
        {
            Application.runInBackground = true;
            Time.timeScale = 0f;
            container = new GameObject("__BattleResultPresentationValidation__");
            container.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            Need(prefab != null, "결과 Prefab 없음");
            var instance = Object.Instantiate(prefab, container.transform);
            foreach (GraphicRaycaster raycaster in instance.GetComponentsInChildren<GraphicRaycaster>(true)) raycaster.enabled = false;
            var fixture = new Fixture(instance);
            ValidateReferences(fixture);
            container.SetActive(true);
            instance.SetActive(true);
            fixture.View.ConfigureReturnToLobby(() => { fixture.ReturnCount++; return true; });
            fixture.View.SetState(eBattleResultState.VICTORY);
            await NextFrame();

            await ValidateNaturalCompletion(fixture);
            await ValidateSkips(fixture);
            await ValidateExperienceCases(fixture);
            await ValidateReentry(fixture);
            await ValidatePointerInput(fixture);
            LastResult = "결과 연출 Play Mode 검증 통과\n" +
                "- Prefab 참조·노란 Fill 겹침 순서 및 Raycast 설정\n" +
                "- timeScale 0 자연 재생·최종 표시·로비 이동 차단/허용\n" +
                "- 제목/기록/숫자/Fill 단계 스킵 및 로비 버튼 지연·중복 스킵\n" +
                "- 경험치 0/최대 정수/다중 레벨업, 숫자 고정 시간과 노랑 선행\n" +
                "- 중복 Play·새 결과 교체·disable/reopen과 기존 Tween 정리\n" +
                "- 가상 Mouse: 열림 클릭 무시·다음 클릭 스킵·누른 동안 이동 차단·해제 후 허용\n" +
                "- 상위 팝업 중 클릭 스킵 차단, 물리 장치 입력 주입 및 Scene/Prefab/SO 저장 없음";
            Debug.Log(LastResult);
        }
        catch (Exception exception)
        {
            LastResult = "결과 연출 Play Mode 검증 실패\n" + exception;
            Debug.LogError(LastResult);
        }
        finally
        {
            if (container != null) Object.DestroyImmediate(container);
            Time.timeScale = previousTimeScale;
            Application.runInBackground = previousRunInBackground;
            _isRunning = false;
        }
    }

    private static void ValidateReferences(Fixture f)
    {
        foreach (Transform child in f.Instance.GetComponentsInChildren<Transform>(true))
            Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, child.name + ": Missing Script");
        foreach (string name in GROUPS) Need(Field<CanvasGroup>(f.Presentation, name) != null, name + " 참조");
        Need(f.Lobby != null && f.Button != null && f.Reward != null && f.Level != null && f.Title != null, "표시 참조");
        Need(Field<UIBattleResultPresentation>(f.View, "_presentation") == f.Presentation, "View 연출 참조");
        Need(f.Yellow != null && f.Slider != null && f.Slider.fillRect != null, "Fill 참조");
        Need(f.Yellow.parent == f.Slider.fillRect.parent && f.Yellow.GetSiblingIndex() < f.Slider.fillRect.GetSiblingIndex(), "노란 Fill이 빨간 Fill 뒤에 있어야 함");
        Need(Near(f.Yellow.anchorMin.y, 0f) && Near(f.Yellow.anchorMax.y, 1f) &&
            Near(f.Yellow.offsetMin.y, 0f) && Near(f.Yellow.offsetMax.y, 0f), "노란 Fill의 세로 Stretch/높이 설정");
        Need(f.Yellow.TryGetComponent(out Image image) && !image.raycastTarget && image.color.r > .5f && image.color.g > .4f && image.color.b < .5f, "노란 Fill 색상/Raycast");
    }

    private static async Task ValidateNaturalCompletion(Fixture f)
    {
        BattleResultDisplayData data = Data(2400, 3, .8f, 3, .2f);
        Begin(f, data);
        Need(f.Reward.text == "0 exp" && Near(f.Title.localScale.x, f.BaseScale.x * 1.2f), "제목/경험치 시작 상태");
        foreach (string name in GROUPS) Need(Near(Field<CanvasGroup>(f.Presentation, name).alpha, 0f), name + " 초기 투명도");
        Sequence original = CurrentSequence(f);
        f.View.PlayPresentation();
        f.View.RefreshView();
        Need(CurrentSequence(f) == original && f.Reward.text == "0 exp", "중복 재생/Refresh가 연출을 덮어씀");
        int returns = f.ReturnCount;
        f.View.ReturnToLobby();
        Need(f.ReturnCount == returns, "연출 중 로비 이동 차단");
        bool sawYellowLead = false;
        bool sawVisibleCount = false;
        await WaitUntil(() => !f.Presentation.IsPlaying, 10d, () =>
        {
            Need(f.Yellow.rect.height > 0f && Near(f.Yellow.rect.height, f.Slider.fillRect.rect.height), "노란 Fill 실제 표시 높이");
            Need(f.Yellow.anchorMax.x + TOLERANCE >= f.Slider.normalizedValue, "노란 Fill 선행 순서");
            if (f.Yellow.anchorMax.x > f.Slider.normalizedValue + .01f) sawYellowLead = true;
            if (Field<CanvasGroup>(f.Presentation, "_rewardRow").alpha > .9f &&
                f.Reward.text != "0 exp" && f.Reward.text != "2,400 exp") sawVisibleCount = true;
        });
        Need(sawYellowLead, "자연 재생에서 노란 Fill 선행을 관찰하지 못함");
        Need(sawVisibleCount, "자연 재생에서 획득량 숫자 증가가 보이지 않음");
        AssertFinal(f, data);
        Need(f.Button.interactable && f.Presentation.CanReturnToLobby, "자연 완료 후 버튼 허용");
        f.View.ReturnToLobby();
        Need(f.ReturnCount == returns + 1, "자연 완료 후 로비 핸들러 호출");
    }

    private static async Task ValidateSkips(Fixture f)
    {
        float countStart = Field<float>(f.Presentation, "_titleDuration") + 3f * Field<float>(f.Presentation, "_rowInterval") + Field<float>(f.Presentation, "_rewardPause");
        float fillStart = countStart + Field<float>(f.Presentation, "_countDuration") + Field<float>(f.Presentation, "_fillStartDelay");
        foreach (float checkpoint in new[] { .1f, .6f, countStart + .3f, fillStart + .3f })
        {
            BattleResultDisplayData data = Data(2400, 5, .7f, 3, .1f);
            Begin(f, data);
            CurrentSequence(f).Pause().Goto(checkpoint, false);
            int returns = f.ReturnCount;
            f.Presentation.Skip();
            AssertFinal(f, data);
            Need(Near(f.Lobby.alpha, 0f) && !f.Button.interactable && !f.Presentation.CanReturnToLobby, "스킵 직후 버튼 등장 대기");
            Sequence reveal = CurrentSequence(f);
            f.Presentation.Skip();
            f.View.ReturnToLobby();
            Need(CurrentSequence(f) == reveal && f.ReturnCount == returns, "중복 스킵/스킵 클릭 이동 차단");
            await WaitUntil(() => !f.Presentation.IsPlaying, 3d);
            Need(Near(f.Lobby.alpha, 1f) && f.Button.interactable, "스킵 버튼 등장 완료");
        }
    }

    private static async Task ValidateExperienceCases(Fixture f)
    {
        float countStart = Field<float>(f.Presentation, "_titleDuration") + 3f * Field<float>(f.Presentation, "_rowInterval") + Field<float>(f.Presentation, "_rewardPause");
        float countDuration = Field<float>(f.Presentation, "_countDuration");
        float fillStart = countStart + countDuration + Field<float>(f.Presentation, "_fillStartDelay");
        foreach (BattleResultDisplayData data in new[] { Data(0, 1, .2f, 1, .2f), Data(int.MaxValue, 3, .8f, 3, .2f), Data(12345, 9, .4f, 1, .9f), Data(2400, 4, 0f, 1, 0f) })
        {
            Begin(f, data);
            Sequence sequence = CurrentSequence(f).Pause();
            float fillDuration = sequence.Duration() - fillStart;
            sequence.Goto(countStart + countDuration * .5f, false);
            if (data.EarnedXp > 0)
                Need(f.Reward.text != "0 exp" && f.Reward.text != data.EarnedXp.ToString("N0", CultureInfo.InvariantCulture) + " exp", "획득량 숫자 중간 진행");
            sequence.Goto(countStart + countDuration + .001f, false);
            Need(f.Reward.text == data.EarnedXp.ToString("N0", CultureInfo.InvariantCulture) + " exp", "고정 시간 내 획득량 도달");
            for (int step = 1; step <= 60; step++)
            {
                sequence.Goto(fillStart + fillDuration * step / 61f, false);
                Need(f.Yellow.anchorMax.x + TOLERANCE >= f.Slider.normalizedValue, "다중 레벨 노랑/빨강 순서");
            }
            f.Presentation.Skip();
            AssertFinal(f, data);
            await WaitUntil(() => !f.Presentation.IsPlaying, 3d);
        }
    }

    private static async Task ValidateReentry(Fixture f)
    {
        Begin(f, Data(120, 2, .7f, 1, .3f));
        Sequence previous = CurrentSequence(f);
        BattleResultDisplayData replacement = Data(330, 4, .4f, 2, .1f);
        Begin(f, replacement);
        Need(!previous.IsActive() && CurrentSequence(f) != previous, "새 결과 교체 시 이전 Tween 정리");
        Sequence interrupted = CurrentSequence(f);
        f.Instance.SetActive(false);
        Need(!interrupted.IsActive() && !f.Presentation.IsPlaying, "OnDisable Tween 정리");
        AssertFinal(f, replacement);
        f.Instance.SetActive(true);
        await NextFrame();
        f.View.PlayPresentation();
        Need(!f.Presentation.IsPlaying, "동일 결과 재오픈 시 중복 재생");
        Begin(f, Data(30, 1, .5f, 1, 0f));
        Need(f.Presentation.IsPlaying && f.Reward.text == "0 exp", "새 결과 재시작");
        f.Presentation.Skip();
        await WaitUntil(() => !f.Presentation.IsPlaying, 3d);
    }

    private static async Task ValidatePointerInput(Fixture f)
    {
        Mouse originalMouse = Mouse.current;
        InputSettings inputSettings = InputSystem.settings;
        InputSettings.BackgroundBehavior previousBackground = inputSettings.backgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode previousRouting = inputSettings.editorInputBehaviorInPlayMode;
        Mouse probe = null;
        GameObject controllerObject = null, overlay = null;
        UIPopupController controller = null;
        try
        {
            // MCP 검증 중 Game View에 포커스가 없어도 가상 장치 이벤트를 Player Loop로 전달한다.
            // 기존 설정 객체의 메모리 값만 임시 변경하며 에셋 저장/Dirty 처리는 하지 않는다.
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            probe = InputSystem.AddDevice<Mouse>("BattleResultValidationMouse");
            await SetLeftButton(probe, true);
            BattleResultDisplayData data = Data(700, 3, .6f, 2, .1f);
            Begin(f, data);
            Sequence playing = CurrentSequence(f).Pause();
            await NextFrame();
            Need(CurrentSequence(f) == playing, "결과창을 연 기존 클릭이 스킵으로 처리됨");
            await SetLeftButton(probe, false);
            Need(CurrentSequence(f) == playing, "클릭 해제만으로 연출 스킵");

            // 실제 팝업 스택에 임시 결과창과 상위 팝업을 등록하여 입력 우선순위도 확인한다.
            controllerObject = new GameObject("__ResultInputValidationController__");
            controllerObject.transform.SetParent(f.Instance.transform.parent);
            controller = controllerObject.AddComponent<UIPopupController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_popupRoot").objectReferenceValue = f.Instance.transform.parent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Need(f.View.Popup != null, "결과 Popup 참조");
            controller.OpenPopup(f.View.Popup);
            overlay = new GameObject("__ResultInputValidationOverlay__", typeof(CanvasGroup), typeof(UIPopupPanel));
            overlay.transform.SetParent(f.Instance.transform.parent);
            UIPopupPanel top = overlay.GetComponent<UIPopupPanel>();
            controller.OpenPopup(top);
            await SetLeftButton(probe, true);
            Need(controller.IsTopPopup(top) && CurrentSequence(f) == playing, "상위 팝업 클릭이 결과 연출을 스킵함");
            await SetLeftButton(probe, false);
            controller.CloseResolvedPopup(top);

            int returns = f.ReturnCount;
            await SetLeftButton(probe, true);
            Need(CurrentSequence(f) != playing && !playing.IsActive(), "새 마우스 왼쪽 클릭으로 스킵되지 않음");
            AssertFinal(f, data);
            await WaitUntil(() => Near(f.Lobby.alpha, 1f), 2d);
            await NextFrame();
            Need(probe.leftButton.isPressed && !f.Presentation.CanReturnToLobby && !f.Button.interactable, "스킵 클릭을 누른 동안 로비 버튼이 활성화됨");
            f.View.ReturnToLobby();
            Need(f.ReturnCount == returns, "스킵 클릭 유지 중 로비 핸들러 호출");
            await SetLeftButton(probe, false);
            await WaitUntil(() => !f.Presentation.IsPlaying, 2d);
            Need(f.Presentation.CanReturnToLobby && f.Button.interactable, "클릭 해제 후 로비 버튼 활성화");
            f.View.ReturnToLobby();
            Need(f.ReturnCount == returns + 1, "클릭 해제 후 로비 핸들러 호출");
        }
        finally
        {
            if (controller != null) controller.CloseResolvedPopup(f.View.Popup);
            if (overlay != null) Object.DestroyImmediate(overlay);
            if (controllerObject != null) Object.DestroyImmediate(controllerObject);
            // 테스트가 만든 장치만 제거한다. 물리 Mouse에는 상태 이벤트를 보내지 않는다.
            if (probe != null && probe.added) InputSystem.RemoveDevice(probe);
            if (inputSettings != null)
            {
                inputSettings.backgroundBehavior = previousBackground;
                inputSettings.editorInputBehaviorInPlayMode = previousRouting;
            }
            if (originalMouse != null && originalMouse.added) originalMouse.MakeCurrent();
        }
    }

    private static async Task SetLeftButton(Mouse probe, bool isPressed)
    {
        probe.MakeCurrent();
        int queuedFrame = Time.frameCount;
        InputSystem.QueueStateEvent(probe, new MouseState { position = new Vector2(32f, 32f) }.WithButton(MouseButton.Left, isPressed));
        await WaitUntil(() => Time.frameCount > queuedFrame && probe.leftButton.isPressed == isPressed, 2d);
        await NextFrame();
        Need(Mouse.current == probe, "가상 Mouse 검증 중 외부 입력으로 current 장치가 변경됨");
    }

    private static void Begin(Fixture f, BattleResultDisplayData data)
    {
        f.View.SetData(data);
        f.View.PlayPresentation();
        Need(f.Presentation.IsPlaying && CurrentSequence(f) != null && !f.Button.interactable, "연출 시작");
    }

    private static void AssertFinal(Fixture f, BattleResultDisplayData data)
    {
        foreach (string name in GROUPS) Need(Near(Field<CanvasGroup>(f.Presentation, name).alpha, 1f), name + " 최종 표시");
        Need((f.Title.localScale - f.BaseScale).sqrMagnitude < TOLERANCE, "제목 최종 크기");
        Need(f.Reward.text == data.EarnedXp.ToString("N0", CultureInfo.InvariantCulture) + " exp", "최종 경험치 숫자");
        Need(f.Level.text == "LV." + data.Level && Near(f.Slider.normalizedValue, data.NormalizedXp) && Near(f.Yellow.anchorMax.x, data.NormalizedXp), "최종 레벨/두 Fill 일치");
    }

    private static BattleResultDisplayData Data(int xp, int level, float normalized, int start, float startNormalized) =>
        new BattleResultDisplayData("보통", 318f, 73, 9, xp, level, normalized, level > start, start, startNormalized, "presentation-validation-" + ++_caseId);
    private static Sequence CurrentSequence(Fixture f) => Field<Sequence>(f.Presentation, "_sequence");
    private static bool Near(float a, float b) => Mathf.Abs(a - b) <= TOLERANCE;
    private static T Field<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Need(field != null, target.GetType().Name + "." + name + " 필드 없음");
        return (T)field.GetValue(target);
    }
    private static async Task WaitUntil(Func<bool> condition, double timeout, Action observe = null)
    {
        double deadline = EditorApplication.timeSinceStartup + timeout;
        while (!condition())
        {
            Need(Application.isPlaying && EditorApplication.timeSinceStartup < deadline, "Play Mode 종료 또는 검증 대기 시간 초과");
            observe?.Invoke();
            await Task.Delay(5);
        }
    }
    private static Task NextFrame()
    {
        int frame = Time.frameCount;
        return WaitUntil(() => Time.frameCount > frame, 3d);
    }
    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        public readonly GameObject Instance;
        public readonly UIBattleResultView View;
        public readonly UIBattleResultPresentation Presentation;
        public readonly CanvasGroup Lobby;
        public readonly Button Button;
        public readonly TMP_Text Reward, Level;
        public readonly RectTransform Title, Yellow;
        public readonly Slider Slider;
        public readonly Vector3 BaseScale;
        public int ReturnCount;
        public Fixture(GameObject instance)
        {
            Instance = instance;
            View = instance.GetComponent<UIBattleResultView>();
            Presentation = instance.GetComponent<UIBattleResultPresentation>();
            Need(View != null && Presentation != null, "View/Presentation 컴포넌트 없음");
            Lobby = Field<CanvasGroup>(Presentation, "_lobbyGroup");
            Button = Field<Button>(View, "_lobbyButton");
            Reward = Field<TMP_Text>(View, "_rewardText");
            Level = Field<TMP_Text>(View, "_levelText");
            TMP_Text title = Field<TMP_Text>(View, "_resultTitleText");
            Need(title != null, "제목 참조");
            Title = title.rectTransform;
            BaseScale = Title.localScale;
            Yellow = Field<RectTransform>(Presentation, "_rewardFill");
            Slider = Field<Slider>(View, "_experience");
        }
    }
}
