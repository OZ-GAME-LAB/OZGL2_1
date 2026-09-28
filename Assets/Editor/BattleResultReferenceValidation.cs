using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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

// 원본 Prefab과 실제 씬을 저장하지 않고 결과 화면의 연결 및 표시를 검증한다.
public static class BattleResultReferenceValidation
{
    private const string PREFABS = "Assets/06.UI/BattleMutedPreview/Overlays_v1/Prefabs/";
    private const int WIDTH = 1920;
    private const int HEIGHT = 1080;
    private static readonly string[] NAMES =
        { "Canvas_BattleResultBase", "Canvas_BattleVictory", "Canvas_BattleDefeat" };
    private static readonly string[] REQUIRED_REFERENCES =
    {
        "_difficultyText", "_resultTitleText", "_timeLabelText", "_timeText", "_killsText",
        "_deploymentsText", "_rewardText", "_levelText", "_levelUpText", "_experience",
        "_crown", "_bannerLeft", "_bannerRight", "_bannerTailLeft", "_bannerTailRight",
        "_rune", "_titleAccent", "_victoryCrown", "_defeatCrown", "_victoryBanner",
        "_defeatBanner", "_victoryBannerTail", "_defeatBannerTail", "_lobbyButton", "_popup"
    };

    [MenuItem("Tools/OZGL2/Battle/Validate Result Reference Prefabs")]
    public static void ValidateMenu() => Debug.Log(Validate());

    public static string Validate()
    {
        RequireEditMode();
        Scene original = SceneManager.GetActiveScene();
        bool dirty = original.isDirty;
        var summaries = new List<string>();
        try
        {
            foreach (string name in NAMES)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(PREFABS + name + ".prefab");
                try
                {
                    bool savedActive = root.activeSelf;
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                            name + "/" + child.name + ": Missing Script");
                    CheckBrokenReferences(root, name);
                    UIBattleResultView view = root.GetComponent<UIBattleResultView>();
                    Need(view != null, name + ": UIBattleResultView");
                    var bindings = new SerializedObject(view);
                    foreach (string field in REQUIRED_REFERENCES)
                    {
                        Object target = Reference<Object>(bindings, field);
                        Component component = target as Component;
                        if (component != null && component.gameObject != root)
                            Need(IsActiveBelowRoot(component.transform, root.transform),
                                name + ": 비활성 표시 오브젝트 " + field);
                    }
                    Need(root.transform.Find("Content") != null && root.transform.Find("Content").gameObject.activeSelf,
                        name + ": Content activeSelf");
                    Need(root.GetComponent<Canvas>() != null, name + ": Canvas");
                    Need(root.GetComponent<CanvasGroup>() != null && root.GetComponent<CanvasGroup>().alpha > 0f,
                        name + ": CanvasGroup alpha");
                    Need(Reference<Button>(bindings, "_lobbyButton").interactable, name + ": 로비 버튼 활성");
                    CheckFonts(root, name);

                    BattleResultDisplayData previewData = view.DisplayData;
                    root.SetActive(true);
                    view.SetData(new BattleResultDisplayData("보통 난이도", 522f, 128, 12, 2400, 20, .6f, true));
                    Need(Reference<TMP_Text>(bindings, "_timeText").text == "08분 42초", name + ": 시간 표시");
                    Need(Reference<TMP_Text>(bindings, "_killsText").text == "128명", name + ": 처치 수");
                    Need(Reference<TMP_Text>(bindings, "_deploymentsText").text == "12개", name + ": 배치 수");
                    Need(Reference<TMP_Text>(bindings, "_rewardText").text == "2,400 exp", name + ": 경험치 표시");
                    Need(Reference<TMP_Text>(bindings, "_levelText").text == "LV.20", name + ": 레벨 표시");
                    Need(Reference<TMP_Text>(bindings, "_levelUpText").text == "LV UP!", name + ": 레벨업 표시");
                    TMP_Text difficulty = Reference<TMP_Text>(bindings, "_difficultyText");
                    Need(difficulty.richText && Regex.Replace(difficulty.text, "<[^>]*>", string.Empty) == "보통 난이도" &&
                        difficulty.text.Contains("</color> 난이도"), name + ": 난이도 명칭 강조");

                    Slider experience = Reference<Slider>(bindings, "_experience");
                    Need(Mathf.Approximately(experience.value, .6f) && !experience.interactable &&
                        !experience.wholeNumbers && experience.minValue == 0f && experience.maxValue == 1f,
                        name + ": 경험치 Slider");
                    CheckState(view, bindings, eBattleResultState.VICTORY, name);
                    CheckState(view, bindings, eBattleResultState.DEFEAT, name);
                    view.SetData(new BattleResultDisplayData(null, float.NaN, -1, -1, -1, -1, -1f, false));
                    Need(experience.value == 0f && Reference<TMP_Text>(bindings, "_timeText").text == "00분 00초" &&
                        Reference<TMP_Text>(bindings, "_levelText").text == "LV.1" &&
                        Reference<TMP_Text>(bindings, "_levelUpText").text == string.Empty, name + ": 음수/NaN 하한 처리");
                    view.SetData(new BattleResultDisplayData("어려움", 0f, 0, 0, 0, 1, 2f, false));
                    Need(experience.value == 1f, name + ": 경험치 상한 처리");
                    view.SetData(null);
                    Need(ReferenceEquals(view.DisplayData, previewData), name + ": null 입력 시 미리보기 복귀");
                    summaries.Add(name + ": references/fonts/active children/state/format/Slider/null OK; saved root activeSelf=" + savedActive);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        finally { Need(SceneManager.GetActiveScene() == original && original.isDirty == dirty, "실제 씬 불변 확인"); }
        return string.Join("\n", summaries);
    }

    private static void CheckState(UIBattleResultView view, SerializedObject bindings, eBattleResultState state, string name)
    {
        view.SetState(state);
        bool victory = state == eBattleResultState.VICTORY;
        string prefix = victory ? "_victory" : "_defeat";
        TMP_Text title = Reference<TMP_Text>(bindings, "_resultTitleText");
        Need(title.text == (victory ? "승리" : "패배"), name + ": 상태별 제목");
        Need(title.color == bindings.FindProperty("_titleColor").colorValue, name + ": 공통 아이보리 제목");
        Need(Reference<TMP_Text>(bindings, "_timeLabelText").text == (victory ? "클리어 시간" : "플레이 시간"),
            name + ": 상태별 시간 문구");
        Need(Reference<Image>(bindings, "_crown").sprite == Reference<Sprite>(bindings, prefix + "Crown"), name + ": 왕관 교체");
        foreach (string field in new[] { "_bannerLeft", "_bannerRight" })
            Need(Reference<Image>(bindings, field).sprite == Reference<Sprite>(bindings, prefix + "Banner"), name + ": " + field);
        foreach (string field in new[] { "_bannerTailLeft", "_bannerTailRight" })
        {
            Image image = Reference<Image>(bindings, field);
            Need(image.enabled && image.sprite == Reference<Sprite>(bindings, prefix + "BannerTail"), name + ": " + field);
        }
        Color accent = bindings.FindProperty(prefix + "Accent").colorValue;
        Color rune = Reference<Image>(bindings, "_rune").color;
        Need(Mathf.Approximately(rune.r, accent.r) && Mathf.Approximately(rune.g, accent.g) &&
            Mathf.Approximately(rune.b, accent.b), name + ": 상태별 룬 강조색");
    }

    private static void CheckFonts(GameObject root, string name)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            Need(text.font != null && text.fontSharedMaterial != null && text.fontSharedMaterial.shader != null,
                name + "/" + text.name + ": Font/Material");
            string expected = text.name == "ResultTitle" ? BattleResultTypography.TITLE_FONT_PATH : BattleResultTypography.BODY_FONT_PATH;
            Need(AssetDatabase.GetAssetPath(text.font) == expected, name + "/" + text.name + ": 결과 전용 폰트");
            Need(text.font.atlasPopulationMode == AtlasPopulationMode.Static && text.font.atlasTexture != null,
                name + "/" + text.name + ": Static SDF atlas");
            Need(!text.raycastTarget && text.fontSize > 0 && text.transform.localScale.x != 0,
                name + "/" + text.name + ": TMP 크기/Raycast");
            string parsed = Regex.Replace(text.text, "<[^>]*>", string.Empty);
            foreach (char character in parsed)
                if (!char.IsControl(character))
                    Need(text.font.HasCharacter(character), name + "/" + text.name + ": glyph " + character);
        }
    }

    private static void CheckBrokenReferences(GameObject root, string name)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            while (property.NextVisible(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                    Need(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                        name + "/" + component.name + ": Missing reference " + property.propertyPath);
        }
    }

    // 실제 씬이나 Play Mode를 사용하지 않는 아트/글자 정렬 검토용 프리뷰이다.
    public static string Render(string prefabName)
    {
        RequireEditMode();
        Need(Array.IndexOf(NAMES, prefabName) >= 0, "검증 대상이 아닌 결과 Prefab: " + prefabName);
        Scene original = SceneManager.GetActiveScene();
        bool dirty = original.isDirty;
        var preview = new PreviewRenderUtility();
        var meshes = new List<Mesh>();
        var materials = new List<Material>();
        Texture2D image = null;
        RenderTexture previous = RenderTexture.active;
        bool isPreviewOpen = false;
        try
        {
            preview.BeginPreview(new Rect(0, 0, WIDTH, HEIGHT), GUIStyle.none);
            isPreviewOpen = true;
            Camera camera = preview.camera;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = HEIGHT * .5f;
            camera.aspect = (float)WIDTH / HEIGHT;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 300f;
            camera.cullingMask = ~0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .032f, .029f, 1f);
            camera.enabled = false;
            camera.allowHDR = camera.allowMSAA = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.requiresColorTexture = data.requiresDepthTexture = false;
            }

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PREFABS + prefabName + ".prefab");
            Need(asset != null, "결과 Prefab 없음: " + prefabName);
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(asset, camera.gameObject.scene);
            root.hideFlags = HideFlags.HideAndDontSave;
            foreach (CanvasScaler scaler in root.GetComponentsInChildren<CanvasScaler>(true)) scaler.enabled = false;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.pixelPerfect = false;
            canvas.scaleFactor = 1f;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.position = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(WIDTH, HEIGHT);
            root.SetActive(true);
            root.GetComponent<UIBattleResultView>().RefreshView();
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();

            // Preview Camera가 uGUI Canvas를 생략하는 경우에도 Unity가 만든 원래 메시/셰이더를 사용한다.
            int drawIndex = 0;
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.isActiveAndEnabled) continue;
                graphic.SetAllDirty();
                graphic.canvasRenderer.cull = false;
                graphic.Rebuild(CanvasUpdate.PreRender);
                graphic.Rebuild(CanvasUpdate.LatePreRender);
                // TMP의 UV0.w에는 SDF 배율이 들어 있으므로 Canvas 변환본이 아닌 원본 메시를 사용한다.
                TMP_Text tmp = graphic as TMP_Text;
                TMP_SubMeshUI subMesh = graphic as TMP_SubMeshUI;
                Mesh source = tmp != null ? tmp.mesh : subMesh != null ? subMesh.mesh : graphic.canvasRenderer.GetMesh();
                if (source == null || source.vertexCount == 0) continue;
                if (tmp != null || subMesh != null)
                    Need(source.GetVertexAttributeDimension(VertexAttribute.TexCoord0) == 4,
                        graphic.name + ": TMP SDF UV0.xy/w 누락");
                Mesh mesh = Object.Instantiate(source);
                mesh.hideFlags = HideFlags.HideAndDontSave;
                // PreviewRenderUtility의 행렬 분해에서 좌우 반전이 유실되지 않도록 임시 메시만 월드 좌표로 굽는다.
                Matrix4x4 world = graphic.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = world.MultiplyPoint3x4(vertices[i]);
                mesh.vertices = vertices;
                Vector3[] normals = mesh.normals;
                Matrix4x4 normalMatrix = world.inverse.transpose;
                for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                mesh.normals = normals;
                mesh.RecalculateBounds();
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    Color[] colors = mesh.colors;
                    for (int i = 0; i < colors.Length; i++) colors[i] = colors[i].linear;
                    mesh.colors = colors;
                }
                meshes.Add(mesh);
                Material material = new Material(graphic.materialForRendering) { hideFlags = HideFlags.HideAndDontSave };
                // TMP_Text는 Graphic.mainTexture의 흰 텍스처를 상속한다. 실제 폰트 atlas를 명시적으로 연결한다.
                Texture texture = graphic.mainTexture;
                if (tmp != null)
                    texture = tmp.fontSharedMaterial != null ? tmp.fontSharedMaterial.GetTexture("_MainTex") : null;
                else if (subMesh != null)
                    texture = subMesh.sharedMaterial != null ? subMesh.sharedMaterial.GetTexture("_MainTex") : null;
                if (texture == null && tmp != null && tmp.font != null) texture = tmp.font.atlasTexture;
                if (texture == null && subMesh != null && subMesh.fontAsset != null) texture = subMesh.fontAsset.atlasTexture;
                Need(texture != null, graphic.name + ": 프리뷰 texture/atlas 누락");
                material.SetTexture("_MainTex", texture);
                if (graphic is Text) material.SetVector("_TextureSampleAdd", new Vector4(1, 1, 1, 0));
                material.renderQueue = 3000 + drawIndex++;
                materials.Add(material);
                preview.DrawMesh(mesh, Matrix4x4.identity, material, 0);
                graphic.canvasRenderer.cull = true;
            }
            Need(drawIndex > 0, "결과 화면 UI 메시가 비었습니다.");
            preview.Render(true);
            RenderTexture target = preview.EndPreview() as RenderTexture;
            isPreviewOpen = false;
            Need(target != null, "결과 Preview RenderTexture");
            RenderTexture.active = target;
            image = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear && !target.sRGB)
            {
                Color[] pixels = image.GetPixels();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = pixels[i].gamma;
                image.SetPixels(pixels);
            }
            image.Apply();
            string path = "Tools/Art/Previews/ResultRefinement_" + prefabName + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            return Path.GetFullPath(path);
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            foreach (Mesh mesh in meshes) Object.DestroyImmediate(mesh);
            foreach (Material material in materials) Object.DestroyImmediate(material);
            if (isPreviewOpen) preview.EndPreview();
            preview.Cleanup();
            Need(SceneManager.GetActiveScene() == original && original.isDirty == dirty, "실제 씬 불변 확인");
        }
    }

    private static T Reference<T>(SerializedObject bindings, string field) where T : Object
    {
        SerializedProperty property = bindings.FindProperty(field);
        Need(property != null, "표시 필드 없음: " + field);
        T value = property.objectReferenceValue as T;
        Need(value != null, "표시 참조 없음: " + field);
        return value;
    }

    private static bool IsActiveBelowRoot(Transform target, Transform root)
    {
        for (Transform current = target; current != null && current != root; current = current.parent)
            if (!current.gameObject.activeSelf) return false;
        return true;
    }

    private static void RequireEditMode()
    {
        Need(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
            "결과 Prefab 검증은 컴파일 완료 후 Edit Mode에서 실행합니다.");
    }

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
