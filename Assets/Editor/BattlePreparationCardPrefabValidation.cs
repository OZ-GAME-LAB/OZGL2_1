using System;
using System.Collections.Generic;
using System.IO;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// 실제 씬에 배치하지 않고 임시 복제본으로 표시 API와 렌더 결과를 확인한다.
public static class BattlePreparationCardPrefabValidation
{
    private const string ROOT = "Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/";
    private static readonly string[] NAMES = { "BattleCard_Unit", "BattleCard_LandSlot", "BattleCard_Relic" };
    private const string PREVIEW_PATH = "Tools/Art/Previews/BattlePreparationCards_v1.png";

    [MenuItem("Tools/OZGL2/Battle/Validate Card Prefabs")]
    public static void ValidateMenu() => Debug.Log(Validate());

    public static string Validate()
    {
        var summaries = new List<string>();
        foreach (string name in NAMES)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ROOT + name + ".prefab");
            try
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, name + ": Missing Script");
                Text[] labels = root.GetComponentsInChildren<Text>(true);
                foreach (Text label in labels)
                    Need(label.font != null && !label.raycastTarget, name + "/" + label.name + ": Font/Raycast");
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                    Need(!graphic.raycastTarget, name + "/" + graphic.name + ": Raycast");
                Need(root.GetComponent<Button>() == null, name + ": 선택 기능은 이번 범위가 아님");
                var view = root.GetComponent<UIBattlePreparationCardView>();
                Need(view != null, name + ": View");
                var bindings = new SerializedObject(view);
                Need(root.transform.Find("CategoryText") == null, name + ": 미사용 종류 문구 제거");
                foreach (string field in new[] { "_titleText", "_rankText", "_typeIcon" })
                    Need(bindings.FindProperty(field).objectReferenceValue != null, name + ":" + field);
                bool land = name.EndsWith("LandSlot");
                foreach (string field in land
                    ? new[] { "_areaTitleText", "_areaDescriptionText" }
                    : new[] { "_traitTitleText", "_traitDescriptionText", "_skillTitleText", "_skillDescriptionText", "_artwork" })
                    Need(bindings.FindProperty(field).objectReferenceValue != null, name + ":" + field);
                if (name.EndsWith("Unit"))
                    foreach (string field in new[] { "_attackLabelText", "_attackValueText", "_defenseLabelText", "_defenseValueText", "_healthLabelText", "_healthValueText" })
                        Need(bindings.FindProperty(field).objectReferenceValue != null, name + ":" + field);
                SerializedProperty cells = bindings.FindProperty("_footprintCells");
                Need(cells.arraySize == 30, name + ": 6x5 grid");
                var images = new List<Image>();
                for (int i = 0; i < cells.arraySize; i++)
                {
                    Image cell = cells.GetArrayElementAtIndex(i).objectReferenceValue as Image;
                    Need(cell != null, name + ": cell " + i);
                    images.Add(cell);
                }

                view.SetTitle("검증용 제목");
                view.SetRank(3);
                view.SetTrait("특성", "교체한 특성 설명");
                view.SetSkill("보유 스킬", "교체한 스킬 설명");
                view.SetStats("123", "45%", "678");
                view.SetAreaDescription("변경 영역", "+2칸");
                Need(root.transform.Find("TitleText").GetComponent<Text>().text == "검증용 제목", name + ": SetTitle");
                Need(root.transform.Find("RankText").GetComponent<Text>().text == "3", name + ": SetRank");
                view.SetFootprint(new[] { new Vector2Int(1, 1), new Vector2Int(1, 1), new Vector2Int(5, 4), new Vector2Int(-1, 0), new Vector2Int(6, 1) });
                Need(images.FindAll(image => image.gameObject.activeSelf).Count == 2, name + ": footprint bounds/duplicates");
                view.SetFootprint(null);
                Need(images.FindAll(image => image.gameObject.activeSelf).Count == 0, name + ": clear footprint");
                view.SetArtwork(null);
                if (!land) Need(!root.transform.Find("Artwork").GetComponent<Image>().enabled, name + ": null artwork");
                view.SetTypeIcon(null);
                Need(!root.transform.Find("TypeBadge/TypeIcon").GetComponent<Image>().enabled, name + ": null icon");
                view.SetTitle(null);
                Need(root.transform.Find("TitleText").GetComponent<Text>().text == string.Empty, name + ": null string");
                summaries.Add(name + ": Text=" + labels.Length + ", references/API/footprint/null checks OK");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return string.Join("\n", summaries);
    }

    [MenuItem("Tools/OZGL2/Battle/Render Card Prefab Preview")]
    public static void RenderMenu() => Debug.Log(RenderPreview());

    public static string RenderPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("독립 카드 미리보기는 Edit Mode에서 실행합니다.");
        Scene original = SceneManager.GetActiveScene();
        bool dirty = original.isDirty;
        var preview = new PreviewRenderUtility();
        Scene previewScene = preview.camera.gameObject.scene;
        bool isPreviewOpen = false;
        Texture2D image = null;
        var renderMeshes = new List<Mesh>();
        var renderMaterials = new List<Material>();
        RenderTexture previous = RenderTexture.active;
        try
        {
            preview.BeginPreview(new Rect(0, 0, 1920, 1160), GUIStyle.none);
            isPreviewOpen = true;
            Camera camera = preview.camera;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true;
            camera.orthographicSize = 580;
            camera.aspect = 1920f / 1160f;
            camera.cullingMask = ~0;
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.014f, 0.014f, 0.016f, 1);
            camera.enabled = false;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
            {
                var cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.requiresColorTexture = false;
                cameraData.requiresDepthTexture = false;
            }

            var canvasObject = EditorUtility.CreateGameObjectWithHideFlags("CardPreviewCanvas", HideFlags.HideAndDontSave, typeof(RectTransform), typeof(Canvas));
            preview.AddSingleGO(canvasObject);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.pixelPerfect = true;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(1920, 1160);
            canvasRect.position = Vector3.zero;
            for (int i = 0; i < NAMES.Length; i++)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + NAMES[i] + ".prefab");
                if (asset == null) throw new InvalidOperationException("카드 Prefab이 없습니다: " + NAMES[i]);
                GameObject card = (GameObject)PrefabUtility.InstantiatePrefab(asset, previewScene);
                if (card.scene != previewScene) SceneManager.MoveGameObjectToScene(card, previewScene);
                var rect = (RectTransform)card.transform;
                rect.SetParent(canvasRect, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(30 + i * 630, -30);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                // Dynamic Font가 Editor의 첫 렌더부터 모든 한글을 준비하게 한다.
                foreach (Text text in card.GetComponentsInChildren<Text>(true))
                    text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            }
            Canvas.ForceUpdateCanvases();
            // Preview Camera에서 uGUI 배치가 생략될 수 있어 Unity가 만든 실제 UI 메시를 렌더한다.
            int drawIndex = 0;
            foreach (Graphic graphic in canvasObject.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.isActiveAndEnabled) continue;
                graphic.SetAllDirty();
                graphic.canvasRenderer.cull = false;
                graphic.Rebuild(CanvasUpdate.PreRender);
                graphic.Rebuild(CanvasUpdate.LatePreRender);
                Mesh sourceMesh = graphic.canvasRenderer.GetMesh();
                if (sourceMesh == null || sourceMesh.vertexCount == 0) continue;
                Mesh mesh = Object.Instantiate(sourceMesh);
                mesh.hideFlags = HideFlags.HideAndDontSave;
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    // Canvas 대신 직접 그리므로 uGUI의 버텍스 색상 공간 변환도 수행한다.
                    Color[] colors = mesh.colors;
                    for (int i = 0; i < colors.Length; i++) colors[i] = colors[i].linear;
                    mesh.colors = colors;
                }
                renderMeshes.Add(mesh);
                // Text의 알파 전용 폰트 텍스처는 원래 폰트 셰이더로 렌더해야 한다.
                var material = new Material(graphic.materialForRendering) { hideFlags = HideFlags.HideAndDontSave };
                material.mainTexture = graphic.mainTexture;
                if (graphic is Text) material.SetVector("_TextureSampleAdd", new Vector4(1, 1, 1, 0));
                material.renderQueue = 3000 + drawIndex++;
                renderMaterials.Add(material);
                preview.DrawMesh(mesh, graphic.transform.localToWorldMatrix, material, 0);
                graphic.canvasRenderer.cull = true;
            }
            Need(drawIndex > 0, "프리뷰 UI 메시가 비었습니다.");
            preview.Render(true);
            var target = preview.EndPreview() as RenderTexture;
            isPreviewOpen = false;
            Need(target != null, "Preview RenderTexture");
            RenderTexture.active = target;
            image = new Texture2D(1920, 1160, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1160), 0, 0);
            // Linear 프리뷰 RT의 픽셀을 PNG의 sRGB로 인코딩한다. 아트 원본은 바꾸지 않는다.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear && !target.sRGB)
            {
                Color[] pixels = image.GetPixels();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = pixels[i].gamma;
                image.SetPixels(pixels);
            }
            image.Apply();
            Directory.CreateDirectory("Tools/Art/Previews");
            File.WriteAllBytes(PREVIEW_PATH, image.EncodeToPNG());
            return Path.GetFullPath(PREVIEW_PATH);
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            foreach (Mesh mesh in renderMeshes) Object.DestroyImmediate(mesh);
            foreach (Material material in renderMaterials) Object.DestroyImmediate(material);
            if (isPreviewOpen) preview.EndPreview();
            preview.Cleanup();
            Need(SceneManager.GetActiveScene() == original && original.isDirty == dirty, "실제 씬 불변 확인");
        }
    }

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
