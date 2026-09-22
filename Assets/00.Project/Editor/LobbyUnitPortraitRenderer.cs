using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OZGL2.Stage;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// 원본 Prefab의 시각 컴포넌트만 별도 Preview Scene에 복사한다. 게임 스크립트와 Animator는 실행하지 않는다.
// 메뉴: Tools/OZGL2/Lobby/Render Unit Codex Portraits (New Files Only)
// 생성 PNG/Importer 설정은 Undo 비지원이다. 기존 PNG는 덮어쓰지 않으며 Scene/원본 Prefab을 저장하지 않는다.
public static class LobbyUnitPortraitRenderer
{
    public const string ROOT = "Assets/06.UI/LobbyMutedPreview/Collections_v1/Portraits";
    private const int IMAGE_SIZE = 512;
    private const int VISIBLE_PIXEL_PADDING = 24;
    private const float FRAME_PADDING = 1.12f;

    private sealed class Source
    {
        internal string Id;
        internal string DisplayName;
        internal string PrefabPath;
        internal GameObject Prefab;
    }

    [MenuItem("Tools/OZGL2/Lobby/Render Unit Codex Portraits (New Files Only)")]
    private static void RenderFromMenu() => Debug.Log(RenderAll());

    public static string RenderAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("유닛 초상화는 Edit Mode에서만 생성할 수 있습니다.");

        var report = new StringBuilder();
        var sources = FindSources(report);
        if (sources.Count == 0) throw new InvalidOperationException("1성 UnitStatData가 연결된 유닛 카탈로그를 찾지 못했습니다.");
        EnsureFolder(ROOT);
        int created = 0;
        int skipped = 0;
        int failed = 0;

        foreach (Source source in sources.Values)
        {
            string outputPath = ROOT + "/" + source.Id + ".png";
            string mapping = source.Id + " (" + source.DisplayName + ") <- " + source.PrefabPath;
            if (File.Exists(outputPath))
            {
                report.AppendLine("SKIP existing: " + outputPath + " | " + mapping);
                skipped++;
                continue;
            }

            Texture2D portrait = null;
            try
            {
                portrait = RenderPortrait(source.Prefab, out int spriteCount);
                byte[] png = portrait.EncodeToPNG();
                // 중간에 생성된 파일도 덮어쓰지 않도록 CreateNew를 사용한다.
                using (var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write))
                    stream.Write(png, 0, png.Length);
                ImportPortrait(outputPath);
                report.AppendLine("CREATED: " + outputPath + " | " + mapping + " | sprites=" + spriteCount);
                created++;
            }
            catch (Exception exception)
            {
                failed++;
                report.AppendLine("FAILED: " + mapping + " | " + exception.Message);
            }
            finally
            {
                if (portrait != null) Object.DestroyImmediate(portrait);
            }
        }

        report.Insert(0, $"유닛 초상화: 생성 {created}, 기존 유지 {skipped}, 실패 {failed}, 카탈로그 유닛 {sources.Count}.\n");
        report.AppendLine("PNG 생성과 Importer 변경은 Undo 비지원. 원본 Prefab/Scene/게임 상태 변경 없음.");
        return report.ToString();
    }

    private static SortedDictionary<string, Source> FindSources(StringBuilder report)
    {
        var result = new SortedDictionary<string, Source>(StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:DemonArmyCatalog"))
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DemonArmyCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            if (catalog == null || catalog.entries == null) continue;
            foreach (var entry in catalog.entries)
                if (entry != null) AddSource(result, entry.unitId, entry.prefab, report);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:HeroPoolCatalogSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var catalog = AssetDatabase.LoadAssetAtPath<HeroPoolCatalogSO>(path);
            if (catalog == null) continue;
            try
            {
                foreach (HeroPoolEntry entry in catalog.CreateSnapshot())
                {
                    if (entry?.Prefab == null) continue;
                    UnitBase unit = entry.Prefab.GetComponent<UnitBase>();
                    if (unit == null) unit = entry.Prefab.GetComponentInChildren<UnitBase>(true);
                    AddSource(result, entry.HeroId, unit, report);
                }
            }
            catch (Exception exception)
            {
                report.AppendLine("SKIP catalog: " + path + " | " + exception.Message);
            }
        }
        return result;
    }

    private static void AddSource(IDictionary<string, Source> sources, string id, UnitBase unit, StringBuilder report)
    {
        if (unit == null || unit.statData == null || unit.statData.starLevel != 1) return;
        UnitStatData data = unit.statData;
        if (string.IsNullOrWhiteSpace(id) || id != data.unitId || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            id.IndexOf('/') >= 0 || id.IndexOf('\\') >= 0)
        {
            report.AppendLine("SKIP invalid ID: " + id + " | UnitStatData=" + data.unitId);
            return;
        }
        string path = AssetDatabase.GetAssetPath(unit);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null || !PrefabUtility.IsPartOfPrefabAsset(prefab)) return;
        if (sources.TryGetValue(id, out Source existing))
        {
            if (existing.PrefabPath != path) report.AppendLine("SKIP duplicate ID: " + id + " | " + path);
            return;
        }
        sources.Add(id, new Source { Id = id, DisplayName = data.displayName, PrefabPath = path, Prefab = prefab });
    }

    private static Texture2D RenderPortrait(GameObject prefab, out int spriteCount)
    {
        var preview = new PreviewRenderUtility();
        Material material = null;
        Texture2D output = null;
        bool isPreviewOpen = false;
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            bool isUniversal = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
            Shader shader = Shader.Find(isUniversal ? "Universal Render Pipeline/2D/Sprite-Unlit-Default" : "Sprites/Default");
            if (shader == null) throw new InvalidOperationException("초상화용 Unlit Sprite Shader를 찾지 못했습니다.");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            GameObject visualRoot = EditorUtility.CreateGameObjectWithHideFlags("UnitPortraitPreview", HideFlags.HideAndDontSave);
            preview.AddSingleGO(visualRoot);
            CopyVisualHierarchy(prefab.transform, visualRoot.transform, material, true);
            SpriteRenderer[] sprites = visualRoot.GetComponentsInChildren<SpriteRenderer>();
            spriteCount = sprites.Length;
            if (spriteCount == 0) throw new InvalidOperationException("표시할 SpriteRenderer가 없습니다.");
            Bounds bounds = sprites[0].bounds;
            for (int index = 1; index < sprites.Length; index++) bounds.Encapsulate(sprites[index].bounds);

            preview.BeginPreview(new Rect(0, 0, IMAGE_SIZE, IMAGE_SIZE), GUIStyle.none);
            isPreviewOpen = true;
            Camera camera = preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(0.01f, Mathf.Max(bounds.extents.x, bounds.extents.y) * FRAME_PADDING);
            camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(100, bounds.size.z + 20);
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 10);
            camera.transform.rotation = Quaternion.identity;
            if (isUniversal)
            {
                var cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.requiresColorTexture = false;
                cameraData.requiresDepthTexture = false;
            }

            // true는 현재 URP의 Preview Camera 렌더 경로를 사용한다. 게임 Camera/Scene은 렌더하지 않는다.
            preview.Render(true);
            Texture result = preview.EndPreview();
            isPreviewOpen = false;
            var target = result as RenderTexture;
            if (target == null) throw new InvalidOperationException("Preview RenderTexture를 얻지 못했습니다.");
            RenderTexture.active = target;
            output = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            output.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            output.Apply(false, false);
            PrepareTransparentPng(output);
            Texture2D rendered = output;
            output = FitVisiblePixels(rendered);
            Object.DestroyImmediate(rendered);
            return output;
        }
        catch
        {
            if (output != null) Object.DestroyImmediate(output);
            throw;
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (isPreviewOpen) preview.EndPreview();
            preview.Cleanup();
            if (material != null) Object.DestroyImmediate(material);
        }
    }

    private static void CopyVisualHierarchy(Transform source, Transform target, Material material, bool isRoot)
    {
        target.localPosition = isRoot ? Vector3.zero : source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        if (source.TryGetComponent(out SortingGroup sourceGroup) && sourceGroup.enabled)
        {
            var group = target.gameObject.AddComponent<SortingGroup>();
            group.sortingLayerID = sourceGroup.sortingLayerID;
            group.sortingOrder = sourceGroup.sortingOrder;
        }
        if (source.TryGetComponent(out SpriteRenderer original) && original.enabled && original.sprite != null && original.color.a > 0)
        {
            var sprite = target.gameObject.AddComponent<SpriteRenderer>();
            sprite.sprite = original.sprite;
            sprite.sharedMaterial = material;
            sprite.color = original.color;
            sprite.flipX = original.flipX;
            sprite.flipY = original.flipY;
            sprite.sortingLayerID = original.sortingLayerID;
            sprite.sortingOrder = original.sortingOrder;
            sprite.spriteSortPoint = original.spriteSortPoint;
            sprite.drawMode = original.drawMode;
            sprite.size = original.size;
        }
        foreach (Transform child in source)
        {
            if (!child.gameObject.activeSelf || IsPresentationExcluded(child)) continue;
            var copy = EditorUtility.CreateGameObjectWithHideFlags(child.name, HideFlags.HideAndDontSave);
            copy.transform.SetParent(target, false);
            CopyVisualHierarchy(child, copy.transform, material, false);
        }
    }

    private static bool IsPresentationExcluded(Transform source)
    {
        string name = source.name.ToLowerInvariant();
        return source.GetComponent<Canvas>() != null || name.Contains("shadow") || name.Contains("health") ||
               name.Contains("hpbar") || name.Contains("hp_bar") || name.Contains("nameplate");
    }

    private static void PrepareTransparentPng(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int visible = 0;
        int transparent = 0;
        bool isLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        for (int index = 0; index < pixels.Length; index++)
        {
            Color32 pixel = pixels[index];
            if (pixel.a == 0) { transparent++; pixels[index] = new Color32(0, 0, 0, 0); continue; }
            visible++;
            if (pixel.a == 255) continue;
            // 투명 배경에 합성된 premultiplied RGB를 일반 PNG 알파 표현으로 되돌린다.
            Color color = pixel;
            float alpha = color.a;
            if (isLinear) color = color.linear;
            color.r = Mathf.Clamp01(color.r / alpha);
            color.g = Mathf.Clamp01(color.g / alpha);
            color.b = Mathf.Clamp01(color.b / alpha);
            if (isLinear) color = color.gamma;
            color.a = alpha;
            pixels[index] = color;
        }
        if (visible == 0 || transparent == 0)
            throw new InvalidOperationException("유닛 표시 또는 투명 배경을 확인하지 못해 PNG 저장을 중단했습니다.");
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    // 부품 Sprite의 투명 영역이 Bounds에 포함되므로 실제 알파 픽셀로 다시 맞춘다.
    // 원본 비율을 유지하고 Point 샘플링으로 확대해 픽셀 경계가 흐려지지 않게 한다.
    private static Texture2D FitVisiblePixels(Texture2D source)
    {
        Color32[] sourcePixels = source.GetPixels32();
        int minX = source.width;
        int minY = source.height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
            {
                if (sourcePixels[y * source.width + x].a == 0) continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        if (maxX < minX || maxY < minY)
            throw new InvalidOperationException("초상화의 실제 알파 영역을 찾지 못했습니다.");

        int visibleWidth = maxX - minX + 1;
        int visibleHeight = maxY - minY + 1;
        int availableSize = IMAGE_SIZE - VISIBLE_PIXEL_PADDING * 2;
        float scale = Mathf.Min((float)availableSize / visibleWidth, (float)availableSize / visibleHeight);
        int width = Mathf.Clamp(Mathf.RoundToInt(visibleWidth * scale), 1, availableSize);
        int height = Mathf.Clamp(Mathf.RoundToInt(visibleHeight * scale), 1, availableSize);
        int offsetX = (IMAGE_SIZE - width) / 2;
        int offsetY = (IMAGE_SIZE - height) / 2;
        var fittedPixels = new Color32[IMAGE_SIZE * IMAGE_SIZE];
        for (int y = 0; y < height; y++)
        {
            int sourceY = minY + Mathf.Min(visibleHeight - 1, Mathf.FloorToInt((y + 0.5f) * visibleHeight / height));
            for (int x = 0; x < width; x++)
            {
                int sourceX = minX + Mathf.Min(visibleWidth - 1, Mathf.FloorToInt((x + 0.5f) * visibleWidth / width));
                fittedPixels[(offsetY + y) * IMAGE_SIZE + offsetX + x] = sourcePixels[sourceY * source.width + sourceX];
            }
        }

        var fitted = new Texture2D(IMAGE_SIZE, IMAGE_SIZE, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        try
        {
            fitted.SetPixels32(fittedPixels);
            fitted.Apply(false, false);
            return fitted;
        }
        catch
        {
            Object.DestroyImmediate(fitted);
            throw;
        }
    }

    private static void ImportPortrait(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("PNG TextureImporter를 찾지 못했습니다: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = IMAGE_SIZE;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
