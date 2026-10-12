using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>실제 기록을 읽지 않고 중앙 카드 복제본의 표시와 수명 주기를 검사한다.</summary>
public static class LobbyInlineRecordValidation
{
    private const string CAPTURE_DIRECTORY = "Temp/LobbyRecordInline_20261012";
    private const int WIDTH = 800;
    private const int HEIGHT = 1000;
    private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/UI/Lobby Records/Validate And Capture")]
    public static void ValidateMenu() => Debug.Log(Run());

    [MenuItem("Tools/UI/Lobby Records/Validate Mask And Capture")]
    public static void ValidateMaskAndCapture() => Debug.Log(Run(false));

    public static string Run(bool verifyTextFit = true)
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode에서 실행하세요.");
        Scene original = SceneManager.GetActiveScene();
        bool wasDirty = original.isDirty;
        GameObject source = GameObject.Find("Canvas_Lobby/StageSelection/CurrentStage");
        Need(source != null && source.scene == original, "현재 씬의 CurrentStage가 없습니다.");
        var sourcePanel = source.GetComponentInChildren<UILobbyDifficultyRecordPanel>(true);
        string reason;
        Need(sourcePanel != null && sourcePanel.UsesInlineDisplay, "카드 내부 기록 구성이 필요합니다.");
        Need(sourcePanel.TryValidate(out reason), "원본 기록 연결: " + reason);
        var originalComponents = new Dictionary<Component, string>();
        foreach (Component component in source.GetComponentsInChildren<Component>(true))
            if (component != null) originalComponents.Add(component, EditorJsonUtility.ToJson(component));

        var preview = new PreviewRenderUtility();
        GameObject canvasRoot = null;
        try
        {
            Camera camera = preview.camera;
            camera.transform.position = new Vector3(0f, 0f, -100f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = HEIGHT * .5f;
            camera.aspect = (float)WIDTH / HEIGHT;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .037f, .048f, 1f);
            camera.enabled = false;
            // 어두운 UI의 그라데이션이 8bit 선형 색 공간에서 계단처럼 양자화되지 않게 한다.
            camera.allowHDR = true;
            camera.allowMSAA = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.requiresColorTexture = data.requiresDepthTexture = false;
            }

            canvasRoot = new GameObject("LobbyRecordPreview", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasRoot.hideFlags = HideFlags.HideAndDontSave;
            canvasRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(canvasRoot, camera.gameObject.scene);
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(WIDTH, HEIGHT);
            var inputWrapper = new GameObject("PreviewInputGate", typeof(RectTransform), typeof(CanvasGroup));
            inputWrapper.hideFlags = HideFlags.HideAndDontSave;
            inputWrapper.transform.SetParent(canvasRoot.transform, false);
            var wrapperRect = (RectTransform)inputWrapper.transform;
            wrapperRect.anchorMin = Vector2.zero;
            wrapperRect.anchorMax = Vector2.one;
            wrapperRect.offsetMin = wrapperRect.offsetMax = Vector2.zero;
            GameObject clone = Object.Instantiate(source, inputWrapper.transform, false);
            clone.name = "CurrentStage";
            clone.hideFlags = HideFlags.HideAndDontSave;
            SanitizeClone(clone);
            RectTransform card = (RectTransform)clone.transform;
            Vector2 originalSize = ((RectTransform)source.transform).rect.size;
            card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f;
            card.anchoredPosition = new Vector2(0f, 55f);
            card.localRotation = Quaternion.identity;
            card.localScale = Vector3.one;
            card.sizeDelta = originalSize;
            clone.SetActive(true);
            canvasRoot.SetActive(true);

            var panel = clone.GetComponentInChildren<UILobbyDifficultyRecordPanel>(true);
            var slot = clone.GetComponent<UILobbyDifficultySlotView>();
            Need(panel != null && slot != null, "복제본 기록/슬롯 컴포넌트");
            TMP_Text summary = Get<TMP_Text>(slot, "_summaryLabel");
            Need(summary != null && summary.gameObject.activeInHierarchy,
                "전투 준비 위의 금색 난이도 요약 문구 표시");
            Need(!clone.transform.Find("StageRecord/RecordPanelVisual").gameObject.activeSelf,
                "구형 확장 기록창만 비활성화");
            var catalog = AssetDatabase.LoadAssetAtPath<LobbyDifficultyCatalogSO>(
                "Assets/06.UI/LobbyMutedPreview/Heraldry_v1/HeraldryLobbyDifficultyCatalog.asset");
            Need(catalog != null && catalog.Count >= 3, "난이도 표시 카탈로그");
            CanvasGroup description = Get<CanvasGroup>(panel, "_descriptionGroup");
            CanvasGroup shade = Get<CanvasGroup>(panel, "_shadeGroup");
            CanvasGroup records = Get<CanvasGroup>(panel, "_recordGroup");
            CanvasGroup caption = clone.transform.Find("VisualLayers/HeraldryCaption").GetComponent<CanvasGroup>();
            TMP_Text wave = Get<TMP_Text>(panel, "_waveValueLabel");
            TMP_Text time = Get<TMP_Text>(panel, "_timeValueLabel");
            Need(description != null && shade != null && records != null, "복제본 레이어 연결");
            Need(description.transform.IsChildOf(clone.transform) && shade.transform.IsChildOf(clone.transform) &&
                records.transform.IsChildOf(clone.transform), "복제본 내부 참조");
            Need(!description.ignoreParentGroups && !shade.ignoreParentGroups && !records.ignoreParentGroups,
                "난이도 전환의 부모 CanvasGroup 상속");
            foreach (Graphic graphic in records.GetComponentsInChildren<Graphic>(true))
                Need(!graphic.raycastTarget, "기록 표시가 입력을 가로채지 않아야 합니다: " + graphic.name);
            Need(shade.GetComponent<Graphic>() != null && !shade.GetComponent<Graphic>().raycastTarget,
                "그라데이션 Raycast 비활성");
            foreach (Image icon in records.GetComponentsInChildren<Image>(true))
                Need(icon.sprite != null && icon.color == Color.white, "흰색 아이콘 Sprite 연결");
            CheckShadeMask(clone, shade, records, description);
            CheckShadeCoverage(shade);

            slot.Bind(catalog.GetEntry(0), null);
            panel.ClearAllRecords();
            Need(wave.text == "기록 없음" && time.text == "--:--", "기록 없음 표시");
            Need(panel.SetRecord(eLobbyDifficulty.EASY, 12) && wave.text == "12 웨이브" && time.text == "--:--",
                "미완료 기록 표시");
            Need(panel.SetRecord(eLobbyDifficulty.EASY, 30, 522f) && wave.text == "30 웨이브" && time.text == "08:42",
                "완료 기록 표시");
            var rectSnapshot = new Dictionary<RectTransform, string>();
            foreach (RectTransform rect in clone.GetComponentsInChildren<RectTransform>(true))
                rectSnapshot.Add(rect, EditorJsonUtility.ToJson(rect));
            foreach (float progress in new[] { 0f, .5f, 1f })
            {
                Invoke(panel, "SetExpansion", progress);
                Need(Approximately(description.alpha, 1f - progress) && Approximately(shade.alpha, progress) &&
                    Approximately(records.alpha, progress), "설명/음영/기록 교차 페이드 " + progress);
                Need(summary.gameObject.activeInHierarchy && summary.text == catalog.GetEntry(0).Description,
                    "호버 전환 중 금색 난이도 요약 유지 " + progress);
                foreach (var pair in rectSnapshot)
                    Need(EditorJsonUtility.ToJson(pair.Key) == pair.Value, "호버 중 배치/크기 변경: " + pair.Key.name);
            }
            slot.SetTransition(.35f, 0f);
            Invoke(panel, "SetExpansion", .5f);
            Need(Approximately(caption.alpha, .35f), "호버가 난이도 Caption 알파를 덮어쓰지 않음");
            slot.SetTransition(1f, 0f);
            CheckInputGating(panel, canvasRoot.GetComponent<CanvasGroup>(), inputWrapper.GetComponent<CanvasGroup>());

            Invoke(panel, "SetExpansion", 0f);
            Capture(preview, clone, "Lobby_RecordInline_Normal.png");
            Invoke(panel, "SetExpansion", 1f);
            if (verifyTextFit) CheckTextFit(records.gameObject);
            Capture(preview, clone, "Lobby_RecordInline_Hover.png");
            panel.SetRecord(eLobbyDifficulty.EASY, 30, 7384f);
            Need(time.text == "123:04", "60분 이상 기록의 총 분 표시");
            if (verifyTextFit) CheckTextFit(records.gameObject);
            panel.SetExternalOpen(true);
            Need(!Get<bool>(panel, "_externalOpen"), "구형 외부 열기 중복 입력 차단");
            Invoke(panel, "OnApplicationFocus", false);
            Need(Approximately(panel.Expansion, 0f) && Approximately(description.alpha, 1f), "포커스 이탈 복원");
            Invoke(panel, "OnApplicationFocus", true);
            Invoke(panel, "SetExpansion", 1f);
            // 일반 MonoBehaviour의 Edit Mode 비활성 이벤트를 명시적으로 호출하여 정리 계약을 검사한다.
            Invoke(panel, "OnDisable");
            Need(Approximately(panel.Expansion, 0f) && !Get<bool>(panel, "_externalOpen"), "비활성화 정리");
            Invoke(panel, "OnEnable");
            Need(Approximately(panel.Expansion, 0f), "재활성화 기본 상태");
            Invoke(panel, "SetExpansion", 1f);
            slot.Bind(catalog.GetEntry(1), null);
            Need(panel.DisplayedDifficulty == eLobbyDifficulty.NORMAL && Approximately(panel.Expansion, 0f) &&
                wave.text == "기록 없음" && time.text == "--:--", "난이도 변경 시 이전 기록/호버 제거");
            slot.Bind(catalog.GetEntry(2), null);
            Need(summary.gameObject.activeInHierarchy && summary.text == catalog.GetEntry(2).Description,
                "난이도 변경 시 금색 요약 문구 갱신");
            panel.SetRecord(eLobbyDifficulty.HARD, 30, 7384f);
            Invoke(panel, "SetExpansion", 1f);
            if (verifyTextFit) CheckTextFit(records.gameObject);
            Capture(preview, clone, "Lobby_RecordInline_Hard_LongTime.png");
            panel.Bind(null);
            Need(!panel.HasEntry && Approximately(panel.Expansion, 0f) && wave.text == "기록 없음", "빈 난이도 복원");

            string report = "PASS: 필수 연결, 흰색 아이콘, 기록 없음/미완료/완료/123분 표시, " +
                "0/0.5/1 페이드와 고정 배치, Caption 전환 알파 독립, 부모 입력 차단/ignoreParentGroups, " +
                "난이도 교체, 포커스/비활성화 복원, 그라데이션 전용 마스크와 스텐실 복원, " +
                "마스크 하단/좌우까지 음영 메시 채움과 최대 알파 유지.\n" +
                (verifyTextFit ? "기록 글자 크기/영역 검사: PASS.\n" :
                    "기록 글자 크기/영역 검사: 생략. 수동 정렬과 두 줄 제목은 변경하지 않은 마스크 검증입니다.\n") +
                "CurrentStage 복제본만 사용. 실제 기록 API/계정/Play Mode/씬 저장 미사용.\n" +
                "미확인: 실제 마우스 왕복, 팝업 가림, 난이도 전환 중 포인터, 실시간 Tween.\n" +
                "캡처: " + Path.GetFullPath(CAPTURE_DIRECTORY);
            Directory.CreateDirectory(CAPTURE_DIRECTORY);
            File.WriteAllText(Path.Combine(CAPTURE_DIRECTORY, "ValidationReport.txt"), report);
            return report;
        }
        finally
        {
            if (canvasRoot != null) Object.DestroyImmediate(canvasRoot);
            preview.Cleanup();
            Need(SceneManager.GetActiveScene() == original && original.isDirty == wasDirty, "실제 씬/미저장 상태 보존");
            foreach (var pair in originalComponents)
                Need(pair.Key != null && EditorJsonUtility.ToJson(pair.Key) == pair.Value,
                    "원본 CurrentStage 컴포넌트 보존: " + (pair.Key == null ? "삭제됨" : pair.Key.GetType().Name));
        }
    }

    private static void SanitizeClone(GameObject clone)
    {
        // 계정 연결과 씬 이동 스크립트는 복제본에서도 실행하지 않는다.
        foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            string ns = behaviour.GetType().Namespace ?? string.Empty;
            bool allowed = behaviour is UILobbyDifficultyRecordPanel || behaviour is UILobbyDifficultySlotView ||
                behaviour is UILobbyStageCardView || behaviour is UIRecordCardShade ||
                ns.StartsWith("UnityEngine.UI", StringComparison.Ordinal) ||
                ns.StartsWith("TMPro", StringComparison.Ordinal);
            if (!allowed) Object.DestroyImmediate(behaviour);
        }
        foreach (Component component in clone.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Component targetComponent = property.objectReferenceValue as Component;
                GameObject target = targetComponent != null ? targetComponent.gameObject : property.objectReferenceValue as GameObject;
                if (target != null && target.scene.IsValid() && target.scene != clone.scene)
                    property.objectReferenceValue = null;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void CheckTextFit(GameObject records)
    {
        foreach (TMP_Text text in records.GetComponentsInChildren<TMP_Text>(true))
        {
            text.ForceMeshUpdate(true, true);
            Need(text.preferredWidth <= text.rectTransform.rect.width + 1f &&
                text.preferredHeight <= text.rectTransform.rect.height + 1f, "기록 글자 넘침: " + text.text);
        }
    }

    private static void CheckShadeMask(GameObject clone, CanvasGroup shade, CanvasGroup records,
        CanvasGroup description)
    {
        Transform maskTransform = shade.transform.parent;
        Need(maskTransform != null && maskTransform.name == "RecordShadeMask", "그라데이션 전용 마스크 부모");
        Mask mask = maskTransform.GetComponent<Mask>();
        Image image = maskTransform.GetComponent<Image>();
        Need(mask != null && mask.isActiveAndEnabled && !mask.showMaskGraphic, "숨김 마스크 활성화");
        Need(image != null && image.isActiveAndEnabled && image.sprite != null &&
            Approximately(image.color.a, 1f) && !image.raycastTarget,
            "마스크 Sprite/알파 1/Raycast 비활성 연결");
        Need(maskTransform.childCount == 1 && maskTransform.GetChild(0) == shade.transform,
            "마스크 자식은 기존 그라데이션 하나만 유지");
        Need(!records.transform.IsChildOf(maskTransform) && !description.transform.IsChildOf(maskTransform),
            "기록 글자와 설명은 그라데이션 마스크에서 제외");
        Transform frame = clone.transform.Find("VisualLayers/Frame");
        Need(frame != null && !frame.IsChildOf(maskTransform), "금색 프레임은 마스크에서 제외");
        MaskableGraphic shadeGraphic = shade.GetComponent<MaskableGraphic>();
        Need(shadeGraphic != null && shadeGraphic.maskable, "그라데이션 Maskable 활성화");

        Canvas.ForceUpdateCanvases();
        image.Rebuild(CanvasUpdate.PreRender);
        Material writer = image.materialForRendering;
        Material reader = shadeGraphic.materialForRendering;
        Need(writer.HasProperty("_StencilOp") && writer.GetInt("_StencilOp") == (int)StencilOp.Replace &&
            writer.GetInt("_StencilWriteMask") != 0 && writer.GetInt("_ColorMask") == 0,
            "숨김 마스크의 스텐실 쓰기와 색상 출력 차단");
        Need(reader.HasProperty("_StencilComp") && reader.GetInt("_StencilComp") == (int)CompareFunction.Equal &&
            reader.GetInt("_StencilReadMask") != 0, "그라데이션의 부모 스텐실 검사");
        Need(image.canvasRenderer.popMaterialCount > 0, "마스크 자식 렌더 후 스텐실 복원 재질");
        Material restore = image.canvasRenderer.GetPopMaterial(0);
        Need(restore != null && restore.GetInt("_StencilWriteMask") != 0 && restore.GetInt("_ColorMask") == 0 &&
            (restore.GetInt("_StencilOp") == (int)StencilOp.Zero ||
             restore.GetInt("_StencilOp") == (int)StencilOp.Replace), "마스크 스텐실 복원 설정");
    }

    private static void CheckShadeCoverage(CanvasGroup shade)
    {
        var graphic = shade.GetComponent<UIRecordCardShade>();
        var shadeRect = (RectTransform)shade.transform;
        var maskRect = (RectTransform)shade.transform.parent;
        Vector3[] maskCorners = new Vector3[4];
        maskRect.GetWorldCorners(maskCorners);
        for (int i = 0; i < maskCorners.Length; i++)
            maskCorners[i] = shadeRect.InverseTransformPoint(maskCorners[i]);
        Rect bounds = shadeRect.rect;
        // 마스크 밖으로 새지 않는지만 검사하면 내부에서 끊기는 직선 경계를 놓친다.
        Need(bounds.xMin <= maskCorners[0].x + .01f && bounds.xMax >= maskCorners[3].x - .01f &&
            bounds.yMin <= maskCorners[0].y + .01f, "음영 영역이 마스크 좌우와 하단 끝까지 이어져야 합니다");

        var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            using (var helper = new VertexHelper())
            {
                typeof(UIRecordCardShade).GetMethod("OnPopulateMesh", PRIVATE_INSTANCE,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            float plateauTop = bounds.yMax - Get<float>(graphic, "_fadeHeight");
            Need(plateauTop > bounds.yMin + 2f && plateauTop < bounds.yMax - 2f,
                "페이드 뒤에도 마스크 하단까지 이어지는 최대 음영 영역");
            Vector3[] vertices = mesh.vertices;
            Color32[] colors = mesh.colors32;
            int[] triangles = mesh.triangles;
            float alphaAbove = SampleMeshAlpha(vertices, colors, triangles,
                new Vector2(bounds.center.x, plateauTop + 1f));
            float alphaBelow = SampleMeshAlpha(vertices, colors, triangles,
                new Vector2(bounds.center.x, plateauTop - 1f));
            Need(Mathf.Abs(alphaAbove - alphaBelow) <= .02f,
                "페이드 종료 지점 앞뒤의 알파가 끊기지 않아야 합니다");
            foreach (float fraction in new[] { .05f, .5f, .95f })
            {
                float alpha = SampleMeshAlpha(vertices, colors, triangles,
                    new Vector2(Mathf.Lerp(bounds.xMin, bounds.xMax, fraction), bounds.yMin + 1f));
                Need(Mathf.Abs(alpha - graphic.color.a) <= 2f / 255f,
                    "모서리 컷 없이 마스크 하단 전체에 최대 음영 유지: " + fraction);
            }
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    private static float SampleMeshAlpha(Vector3[] vertices, Color32[] colors, int[] triangles, Vector2 point)
    {
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
            Vector2 a = vertices[ia], b = vertices[ib], c = vertices[ic];
            float denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(denominator) < .0001f) continue;
            float u = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / denominator;
            float v = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / denominator;
            float w = 1f - u - v;
            if (u < -.0001f || v < -.0001f || w < -.0001f) continue;
            return (u * colors[ia].a + v * colors[ib].a + w * colors[ic].a) / 255f;
        }
        throw new InvalidOperationException("Lobby inline record validation: 음영 메시가 하단 검사점을 덮지 않습니다: " + point);
    }

    private static void CheckInputGating(UILobbyDifficultyRecordPanel panel, CanvasGroup outer, CanvasGroup inner)
    {
        Invoke(panel, "CacheLayoutReferences");
        Need((bool)Invoke(panel, "IsInlineInputAllowed"), "기본 부모 그룹 입력 허용");
        Invoke(panel, "SetExpansion", 1f);
        outer.interactable = false;
        Invoke(panel, "OnCanvasGroupChanged");
        Need(!(bool)Invoke(panel, "IsInlineInputAllowed") && Approximately(panel.Expansion, 0f),
            "부모 상호작용 차단 시 호버 복원");
        outer.interactable = true;
        outer.blocksRaycasts = false;
        Need(!(bool)Invoke(panel, "IsInlineInputAllowed"), "부모 Raycast 차단 상속");
        outer.blocksRaycasts = true;
        outer.alpha = 0f;
        Need(!(bool)Invoke(panel, "IsInlineInputAllowed"), "숨겨진 부모 입력 제외");
        outer.alpha = 1f;
        outer.interactable = false;
        inner.ignoreParentGroups = true;
        Invoke(panel, "CacheLayoutReferences");
        Need((bool)Invoke(panel, "IsInlineInputAllowed"), "ignoreParentGroups 위쪽 부모 제외");
        inner.interactable = false;
        Need(!(bool)Invoke(panel, "IsInlineInputAllowed"), "ignoreParentGroups 자신의 차단은 적용");
        inner.interactable = true;
        inner.ignoreParentGroups = false;
        Need(!(bool)Invoke(panel, "IsInlineInputAllowed"), "부모 검사 복원");
        outer.enabled = false;
        Need((bool)Invoke(panel, "IsInlineInputAllowed"), "비활성 CanvasGroup은 차단에서 제외");
        outer.interactable = true;
        outer.enabled = true;
        Invoke(panel, "OnCanvasGroupChanged");
        Need((bool)Invoke(panel, "IsInlineInputAllowed"), "검증용 부모 그룹 복원");
    }

    private static void Capture(PreviewRenderUtility preview, GameObject clone, string fileName)
    {
        var meshes = new List<Mesh>();
        var materials = new List<Material>();
        RenderTexture previous = RenderTexture.active;
        Texture2D image = null;
        Texture2D readback = null;
        bool isOpen = false;
        Canvas canvas = clone.GetComponentInParent<Canvas>();
        bool canvasWasEnabled = canvas.enabled;
        try
        {
            preview.BeginPreview(new Rect(0, 0, WIDTH, HEIGHT), GUIStyle.none);
            isOpen = true;
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();
            int drawIndex = 0;
            DrawHierarchy(preview, clone.transform, meshes, materials, ref drawIndex);
            Need(drawIndex > 0, "카드 UI 메시가 없습니다.");
            // Cull은 렌더 직전 Canvas 갱신으로 풀릴 수 있어, 메시 준비 후 native Canvas를 끈다.
            // 이 렌더에서는 위에 수집한 메시만 그려 그라데이션/글자가 두 번 합성되지 않게 한다.
            canvas.enabled = false;
            preview.Render(true);
            RenderTexture target = preview.EndPreview() as RenderTexture;
            isOpen = false;
            Need(target != null, "프리뷰 RenderTexture");
            Need(target.depth >= 24, "마스크 캡처에 스텐실 포함 깊이 버퍼가 필요합니다: " + target.depth);
            Need(target.format == RenderTextureFormat.ARGBHalf || target.format == RenderTextureFormat.ARGBFloat,
                "그라데이션 캡처에 고정밀 RenderTexture가 필요합니다: " + target.format);
            RenderTexture.active = target;
            // 감마 변환 전에 RGBA32로 읽으면 어두운 영역 정보가 이미 유실된다.
            readback = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBAFloat, false, true);
            readback.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
            readback.Apply();
            Color[] pixels = readback.GetPixels();
            if (QualitySettings.activeColorSpace == ColorSpace.Linear && !target.sRGB)
                for (int i = 0; i < pixels.Length; i++) pixels[i] = pixels[i].gamma;
            image = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false);
            image.SetPixels(pixels);
            image.Apply();
            Directory.CreateDirectory(CAPTURE_DIRECTORY);
            File.WriteAllBytes(Path.Combine(CAPTURE_DIRECTORY, fileName), image.EncodeToPNG());
        }
        finally
        {
            canvas.enabled = canvasWasEnabled;
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            if (readback != null) Object.DestroyImmediate(readback);
            foreach (Mesh mesh in meshes) Object.DestroyImmediate(mesh);
            foreach (Material material in materials) Object.DestroyImmediate(material);
            if (isOpen) preview.EndPreview();
        }
    }

    private static void DrawHierarchy(PreviewRenderUtility preview, Transform target, List<Mesh> meshes,
        List<Material> materials, ref int drawIndex)
    {
        if (!target.gameObject.activeInHierarchy) return;
        Mask mask = target.GetComponent<Mask>();
        Mesh maskMesh = null;
        Graphic maskGraphic = null;
        Material restore = null;
        foreach (Graphic graphic in target.GetComponents<Graphic>())
        {
            float alpha = InheritedAlpha(graphic.transform);
            if (!graphic.isActiveAndEnabled || alpha <= .001f) continue;
            graphic.SetAllDirty();
            graphic.canvasRenderer.cull = false;
            graphic.Rebuild(CanvasUpdate.PreRender);
            graphic.Rebuild(CanvasUpdate.LatePreRender);
            TMP_Text tmp = graphic as TMP_Text;
            TMP_SubMeshUI subMesh = graphic as TMP_SubMeshUI;
            Mesh source = tmp != null ? tmp.mesh : subMesh != null ? subMesh.mesh : graphic.canvasRenderer.GetMesh();
            if (source == null || source.vertexCount == 0) continue;
            Mesh mesh = Object.Instantiate(source);
            mesh.hideFlags = HideFlags.HideAndDontSave;
            Matrix4x4 world = graphic.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = world.MultiplyPoint3x4(vertices[i]);
            mesh.vertices = vertices;
            Color[] colors = mesh.colors;
            for (int i = 0; i < colors.Length; i++)
            {
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) colors[i] = colors[i].linear;
                colors[i].a *= alpha;
            }
            mesh.colors = colors;
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            DrawGraphicMesh(preview, graphic, mesh, graphic.materialForRendering, materials, ref drawIndex);
            if (mask != null && mask.isActiveAndEnabled && mask.graphic == graphic)
            {
                maskMesh = mesh;
                maskGraphic = graphic;
                Need(graphic.canvasRenderer.popMaterialCount > 0, "캡처 마스크의 스텐실 복원 재질");
                restore = graphic.canvasRenderer.GetPopMaterial(0);
                Need(restore != null, "캡처 마스크 복원 재질 누락");
            }
            graphic.canvasRenderer.cull = true;
        }
        for (int i = 0; i < target.childCount; i++)
            DrawHierarchy(preview, target.GetChild(i), meshes, materials, ref drawIndex);
        // native Canvas처럼 마스크 자식 렌더가 끝난 뒤 동일 메시로 스텐실을 되돌린다.
        if (maskMesh != null)
            DrawGraphicMesh(preview, maskGraphic, maskMesh, restore, materials, ref drawIndex);
    }

    private static void DrawGraphicMesh(PreviewRenderUtility preview, Graphic graphic, Mesh mesh,
        Material source, List<Material> materials, ref int drawIndex)
    {
        var material = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
        Texture texture = graphic.mainTexture;
        TMP_Text tmp = graphic as TMP_Text;
        TMP_SubMeshUI subMesh = graphic as TMP_SubMeshUI;
        if (tmp != null) texture = tmp.fontSharedMaterial.GetTexture("_MainTex");
        else if (subMesh != null) texture = subMesh.sharedMaterial.GetTexture("_MainTex");
        material.SetTexture("_MainTex", texture);
        material.renderQueue = 3000 + drawIndex++;
        materials.Add(material);
        preview.DrawMesh(mesh, Matrix4x4.identity, material, 0);
    }

    private static float InheritedAlpha(Transform target)
    {
        float alpha = 1f;
        for (Transform current = target; current != null; current = current.parent)
        {
            bool stop = false;
            foreach (CanvasGroup group in current.GetComponents<CanvasGroup>())
            {
                if (!group.isActiveAndEnabled) continue;
                alpha *= group.alpha;
                stop |= group.ignoreParentGroups;
            }
            if (stop) break;
        }
        return alpha;
    }

    private static T Get<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, PRIVATE_INSTANCE);
        Need(field != null, "검증 필드 없음: " + name);
        return (T)field.GetValue(target);
    }

    private static object Invoke(object target, string name, params object[] parameters)
    {
        MethodInfo method = target.GetType().GetMethod(name, PRIVATE_INSTANCE);
        Need(method != null, "검증 메서드 없음: " + name);
        return method.Invoke(target, parameters);
    }

    private static bool Approximately(float left, float right) => Mathf.Abs(left - right) < .001f;
    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Lobby inline record validation: " + message);
    }
}
