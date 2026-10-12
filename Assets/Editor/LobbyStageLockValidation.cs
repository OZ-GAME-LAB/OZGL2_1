using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>실제 해금 기록을 읽지 않고 카드 복제본의 봉인 표시와 기록 호버 복구를 검사한다.</summary>
public static class LobbyStageLockValidation
{
    private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PRIVATE_STATIC = BindingFlags.Static | BindingFlags.NonPublic;
    private const string CAPTURE_DIRECTORY = "Temp/LobbyRecordInline_20261012";

    [MenuItem("Tools/UI/Lobby Stage Lock/Validate And Capture")]
    public static void ValidateMenu() => Debug.Log(Run());

    public static string Run()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행하세요.");
        Scene original = SceneManager.GetActiveScene();
        bool wasDirty = original.isDirty;
        GameObject selection = GameObject.Find("Canvas_Lobby/StageSelection");
        Need(selection != null && selection.scene == original, "현재 씬의 StageSelection이 필요합니다.");
        var snapshots = new Dictionary<Component, string>();
        foreach (Component component in selection.GetComponentsInChildren<Component>(true))
            if (component != null) snapshots.Add(component, EditorJsonUtility.ToJson(component));
        var catalog = AssetDatabase.LoadAssetAtPath<LobbyDifficultyCatalogSO>(
            "Assets/06.UI/LobbyMutedPreview/Heraldry_v1/HeraldryLobbyDifficultyCatalog.asset");
        Need(catalog != null && catalog.Count >= 3, "난이도 표시 카탈로그");
        var preview = new PreviewRenderUtility();
        GameObject canvasRoot = null;
        int cases = 0;
        try
        {
            Camera camera = preview.camera;
            camera.transform.position = new Vector3(0f, 0f, -100f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 500f;
            camera.aspect = .8f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .037f, .048f, 1f);
            camera.enabled = false;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.requiresColorTexture = data.requiresDepthTexture = false;
            }
            canvasRoot = new GameObject("LobbyStageLockPreview", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasRoot.hideFlags = HideFlags.HideAndDontSave;
            canvasRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(canvasRoot, camera.gameObject.scene);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(800f, 1000f);
            foreach (var sourceSlot in selection.GetComponentsInChildren<UILobbyDifficultySlotView>(true))
            {
                canvasRoot.SetActive(false);
                GameObject clone = Object.Instantiate(sourceSlot.gameObject, canvasRoot.transform, false);
                UILobbyDifficultyRecordPanel panel = null;
                try
                {
                    InvokeHelper("SanitizeClone", new[] { typeof(GameObject) }, clone);
                    var rect = (RectTransform)clone.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                    rect.anchoredPosition = new Vector2(0f, 55f);
                    rect.localRotation = Quaternion.identity;
                    rect.localScale = Vector3.one;
                    rect.sizeDelta = ((RectTransform)sourceSlot.transform).rect.size;
                    clone.SetActive(true);
                    canvasRoot.SetActive(true);
                    var slot = clone.GetComponent<UILobbyDifficultySlotView>();
                    var card = clone.GetComponent<UILobbyStageCardView>();
                    panel = clone.GetComponentInChildren<UILobbyDifficultyRecordPanel>(true);
                    Need(slot != null && card != null, "복제본 카드/슬롯 연결");
                    // 일반 MonoBehaviour의 Edit Mode 활성 콜백도 명시해 실제 구독 계약을 검사한다.
                    if (panel != null)
                    {
                        InvokeLifecycle(panel, "OnEnable");
                        Need(Get<UILobbyStageCardView>(panel, "_subscribedStageCard") == card,
                            "복제본 기록 패널의 잠금 이벤트 구독");
                    }
                    CheckReferences(card, clone, panel);
                    for (int index = 0; index < 3; index++)
                    {
                        slot.Bind(catalog.GetEntry(index), null);
                        string condition = index == 0 ? "기본 난이도 잠금 표시 검증" :
                            (index == 1 ? "보통" : "어려움") + " 난이도 클리어 시 해금";
                        card.SetLockCondition(condition);
                        if (panel != null)
                        {
                            panel.SetRecord(catalog.GetEntry(index).Difficulty, 30, 522f);
                            InvokePanel(panel, "SetExpansion", 1f);
                            Need(Approximately(panel.Expansion, 1f), "해금 카드 기록 호버 표시");
                        }
                        card.SetLocked(true);
                        CheckState(card, panel, true, condition);
                        CheckFade(slot, card, true);
                        if (sourceSlot.name == "CurrentStage" && index > 0)
                            Capture(preview, clone, index == 1 ? "Lobby_StageLock_Normal.png" : "Lobby_StageLock_Hard.png");
                        else if (index == 1)
                            Capture(preview, clone, "Lobby_StageLock_" + sourceSlot.name + ".png");
                        card.SetLocked(false);
                        CheckState(card, panel, false, condition);
                        CheckFade(slot, card, false);
                        if (sourceSlot.name == "CurrentStage" && index == 1)
                            Capture(preview, clone, "Lobby_StageUnlock_Normal.png");
                        // 잠금 상태에서 슬롯을 재사용해도 이전 실루엣/기록 진행률이 남지 않아야 한다.
                        card.SetLocked(true);
                        slot.Bind(catalog.GetEntry((index + 1) % 3), null);
                        Need(!card.IsLocked && (panel == null || Approximately(panel.Expansion, 0f)), "재바인딩 잠금/호버 복구");
                        CheckState(card, panel, false, condition);
                        cases += 2;
                    }
                }
                finally
                {
                    try { if (panel != null) InvokeLifecycle(panel, "OnDisable"); }
                    finally { Object.DestroyImmediate(clone); }
                }
            }
            string report = "PASS: " + cases + "개 카드/난이도/잠금 상태, 실루엣과 원래색 복구, 해금 조건, " +
                "기록 호버 도중 잠금 전환, 재바인딩, 0/0.5/1 전환 페이드, 분리 사슬·금색 틴트 자물쇠 연결, " +
                "직선 봉인 리본 연결·수동 배치와 좌우 비례 크기·공통 글자 Y 3.66·실제 글자 영역, 표시 Raycast 비활성.\n" +
                "현재 StageSelection 복제본만 사용. 실제 계정/StageClearStore/Play Mode/씬 저장 미사용.\n" +
                "미확인: 실제 마우스 호버, 실시간 난이도 Tween, 실제 진행 해금/전투 시작 버튼.\n캡처: " + Path.GetFullPath(CAPTURE_DIRECTORY);
            Directory.CreateDirectory(CAPTURE_DIRECTORY);
            File.WriteAllText(Path.Combine(CAPTURE_DIRECTORY, "StageLockValidationReport.txt"), report);
            return report;
        }
        finally
        {
            if (canvasRoot != null) Object.DestroyImmediate(canvasRoot);
            preview.Cleanup();
            Need(SceneManager.GetActiveScene() == original && original.isDirty == wasDirty, "실제 씬/미저장 상태 보존");
            foreach (var pair in snapshots)
                Need(pair.Key != null && EditorJsonUtility.ToJson(pair.Key) == pair.Value, "원본 컴포넌트 보존");
        }
    }

    private static void CheckReferences(UILobbyStageCardView card, GameObject clone, UILobbyDifficultyRecordPanel panel)
    {
        Image[] portraits = Get<Image[]>(card, "_silhouetteImages");
        Color[] colors = Get<Color[]>(card, "_unlockedPortraitColors");
        Need(portraits != null && portraits.Length > 0 && colors != null && colors.Length == portraits.Length,
            "실루엣/원래색 배열 연결");
        foreach (Image portrait in portraits)
            Need(portrait != null && portrait.transform.IsChildOf(clone.transform) && portrait.sprite != null, "용사 Sprite 연결");
        Transform overlay = clone.transform.Find("VisualLayers/LockOverlay");
        var frame = clone.transform.Find("VisualLayers/Frame").GetComponent<Image>();
        string expectedFrame = clone.name.StartsWith("CurrentStage", StringComparison.Ordinal)
            ? "difficulty_selected" : "difficulty_card";
        Need(card.FrameSprite != null && card.FrameSprite == frame.sprite &&
            card.FrameSprite.name == expectedFrame, "중앙/좌우 프레임 연결: " + expectedFrame);
        Need(overlay != null && Get<CanvasGroup>(card, "_lockGroup").transform == overlay, "잠금 레이어 연결");
        foreach (string name in new[] { "Lock", "Chains" })
        {
            Image image = overlay.Find(name)?.GetComponent<Image>();
            Need(image != null && image.sprite != null && image.sprite.name != "StageLock_Chains", "새 분리 이미지: " + name);
        }
        foreach (Graphic graphic in overlay.GetComponentsInChildren<Graphic>(true))
            Need(!graphic.raycastTarget, "잠금 표시 입력 차단: " + graphic.name);
        Need(overlay.Find("Lock").GetComponent<Image>().color == new Color(1f, .89f, .69f, 1f),
            "자물쇠의 옅은 금색 표시");
        CheckSealLayout(card, overlay);
        Need(Get<TMP_Text>(card, "_lockConditionLabel") != null && Get<CanvasGroup>(card, "_descriptionGroup") != null,
            "해금 안내/설명 연결");
        if (panel != null) Need(Get<UILobbyStageCardView>(panel, "_stageCard") == card, "호버 잠금 상태 카드 연결");
    }

    private static void CheckSealLayout(UILobbyStageCardView card, Transform overlay)
    {
        float ratio = ((RectTransform)card.transform).rect.width / 530f;
        Image ribbon = overlay.Find("SealRibbon")?.GetComponent<Image>();
        Need(ribbon != null && ribbon.sprite != null && ribbon.sprite.texture != null &&
            ribbon.sprite.name == "stage_lock_seal_ribbon_v1" &&
            AssetDatabase.GetAssetPath(ribbon.sprite) == LobbyStageLockBuilder.ART_ROOT + "stage_lock_seal_ribbon_v1.asset",
            card.name + " 봉인 리본 Sprite 연결");
        Need(ribbon.type == Image.Type.Simple && !ribbon.preserveAspect &&
            ribbon.color == Color.white && !ribbon.raycastTarget,
            card.name + " 봉인 리본 Image 설정");
        float sealY = card.name.StartsWith("CurrentStage", StringComparison.Ordinal) ? -104.4f : -112f * ratio;
        CheckSealRect(ribbon.rectTransform, new Vector2(0f, sealY),
            new Vector2(166.1f, 68.3f) * ratio, card.name + " 봉인 리본");

        TMP_Text label = ribbon.transform.Find("SealLabel")?.GetComponent<TMP_Text>();
        Need(label != null && label.text == "봉인됨" && !label.enableAutoSizing &&
            Approximately(label.fontSize, 26f * ratio) && label.alignment == TextAlignmentOptions.Center &&
            !label.raycastTarget, card.name + " 봉인 글자 설정");
        CheckSealRect(label.rectTransform, new Vector2(0f, 3.66f),
            new Vector2(96.7f, 34f) * ratio, card.name + " 봉인 글자");

        // 행 높이에는 글자 위아래의 폰트 여백도 포함되므로 실제 출력되는 글리프만 검사한다.
        label.ForceMeshUpdate(true, true);
        Rect textRect = label.rectTransform.rect;
        int visibleCharacters = 0;
        for (int i = 0; i < label.textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = label.textInfo.characterInfo[i];
            if (!character.isVisible) continue;
            visibleCharacters++;
            Need(character.bottomLeft.x >= textRect.xMin - 1f &&
                character.bottomLeft.y >= textRect.yMin - 1f &&
                character.topRight.x <= textRect.xMax + 1f &&
                character.topRight.y <= textRect.yMax + 1f,
                card.name + " 봉인 글자 영역 넘침: " + character.character +
                " / glyph=" + character.bottomLeft + "~" + character.topRight + " / rect=" + textRect);
        }
        Need(visibleCharacters == 3, card.name + " 봉인 글자 3개 출력");
    }

    private static void CheckSealRect(RectTransform rect, Vector2 position, Vector2 size, string name)
    {
        Need(Vector2.Distance(rect.anchoredPosition, position) < .02f &&
            Vector2.Distance(rect.sizeDelta, size) < .02f &&
            Vector3.Distance(rect.localScale, Vector3.one) < .001f,
            name + " 지정 위치와 카드 너비 비례 크기");
    }

    private static void CheckState(UILobbyStageCardView card, UILobbyDifficultyRecordPanel panel, bool locked, string condition)
    {
        Need(card.IsLocked == locked, "잠금 표시 상태");
        Image[] portraits = Get<Image[]>(card, "_silhouetteImages");
        Color[] originals = Get<Color[]>(card, "_unlockedPortraitColors");
        Color silhouette = Get<Color>(card, "_silhouetteColor");
        for (int i = 0; i < portraits.Length; i++)
        {
            Color expected = locked ? silhouette : originals[i];
            if (locked) expected.a *= originals[i].a;
            Need(portraits[i].color == expected, "실루엣/원래색: " + portraits[i].name);
        }
        TMP_Text conditionLabel = Get<TMP_Text>(card, "_lockConditionLabel");
        Need(conditionLabel.text == condition, "해금 조건 표시");
        if (locked)
        {
            conditionLabel.ForceMeshUpdate(true, true);
            Need(conditionLabel.preferredWidth <= conditionLabel.rectTransform.rect.width + 1f &&
                conditionLabel.preferredHeight <= conditionLabel.rectTransform.rect.height + 1f,
                "해금 조건 글자 넘침: " + condition);
        }
        Need(Approximately(Get<CanvasGroup>(card, "_descriptionGroup").alpha, locked ? 0f : 1f), "설명/해금 안내 전환");
        if (panel == null) return;
        Need(Approximately(panel.Expansion, 0f), "잠금/해금 전환 시 기록 호버 초기화");
        Need(Approximately(Get<CanvasGroup>(panel, "_recordGroup").alpha, 0f) &&
            Approximately(Get<CanvasGroup>(panel, "_shadeGroup").alpha, 0f), "잠긴 카드 기록/음영 숨김");
        if (locked)
        {
            InvokePanel(panel, "SetExpansion", 1f);
            Need(Approximately(panel.Expansion, 0f), "잠금 중 기록 호버 확장 차단");
        }
    }

    private static void CheckFade(UILobbyDifficultySlotView slot, UILobbyStageCardView card, bool locked)
    {
        foreach (float alpha in new[] { 0f, .5f, 1f })
        {
            slot.SetTransition(alpha, 0f);
            card.SetLockTransitionAlpha(alpha);
            Need(Approximately(Get<CanvasGroup>(card, "_lockGroup").alpha, locked ? alpha : 0f), "봉인 전환 알파 " + alpha);
            float expected = locked ? Get<Color>(card, "_lockedShadeColor").a * alpha : 0f;
            Need(Approximately(Get<Image>(card, "_lockedShade").color.a, expected), "음영 전환 알파 " + alpha);
        }
    }

    private static void Capture(PreviewRenderUtility preview, GameObject clone, string name) =>
        InvokeHelper("Capture", new[] { typeof(PreviewRenderUtility), typeof(GameObject), typeof(string) }, preview, clone, name);

    private static void InvokeHelper(string name, Type[] signature, params object[] args) =>
        typeof(LobbyInlineRecordValidation).GetMethod(name, PRIVATE_STATIC, null, signature, null).Invoke(null, args);

    private static void InvokePanel(UILobbyDifficultyRecordPanel panel, string name, float value) =>
        typeof(UILobbyDifficultyRecordPanel).GetMethod(name, PRIVATE_INSTANCE, null, new[] { typeof(float) }, null)
            .Invoke(panel, new object[] { value });

    private static void InvokeLifecycle(UILobbyDifficultyRecordPanel panel, string name) =>
        typeof(UILobbyDifficultyRecordPanel).GetMethod(name, PRIVATE_INSTANCE, null, Type.EmptyTypes, null)
            .Invoke(panel, null);

    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, PRIVATE_INSTANCE).GetValue(target);
    private static bool Approximately(float a, float b) => Mathf.Abs(a - b) < .001f;
    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Lobby stage lock validation: " + message);
    }
}
