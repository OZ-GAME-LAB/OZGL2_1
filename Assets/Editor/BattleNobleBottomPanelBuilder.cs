using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 승인된 프리뷰 씬에 장식 패널만 배치한다. 기존 버튼·텍스트·카드와 런타임 참조는 변경하지 않는다.
public static class BattleNobleBottomPanelBuilder
{
    private const string PREVIEW_SCENE = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    private const string BODY_PATH = "Assets/06.UI/BattleMutedPreview/NobleBottomPanel/Panel_Body.png";
    private const string FRAME_PATH = "Assets/06.UI/BattleMutedPreview/NobleBottomPanel/Panel_Frame.png";
    private const string UNIFIED_PATH = "Assets/06.UI/BattleMutedPreview/NobleBottomPanel/Panel_Unified.png";
    private const string REROLL_FRAME_PATH = "Assets/06.UI/BattleMutedPreview/ToneFix/Frame_Reroll.png";
    private const string REROLL_ICON_PATH = "Assets/06.UI/BattleMutedPreview/ToneFix/Icon_RerollWhite.png";
    private const string PANEL_NAME = "BottomNobleBackground";
    private const string UNDO_LABEL = "전투 프리뷰 하단 귀족 패널 적용";
    private const string TONE_UNDO_LABEL = "전투 프리뷰 하단 톤 조정";
    private const string UNIFIED_UNDO_LABEL = "전투 프리뷰 하단 통합 패널 적용";
    private const float PANEL_WIDTH = 1920f;
    private const float PANEL_HEIGHT = 432f;
    private static readonly Color PANEL_BODY_COLOR = new Color(0.50f, 0.50f, 0.50f, 1f);
    private static readonly Color PANEL_FRAME_COLOR = new Color(0.62f, 0.62f, 0.62f, 1f);
    private static readonly Color UNIFIED_PANEL_COLOR = new Color(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color ACTION_BODY_COLOR = new Color(0.01f, 0.01f, 0.012f, 1f);

    [MenuItem("Tools/OZGL2/Battle/Apply Noble Bottom Panel")]
    public static void Apply()
    {
        if (File.Exists(UNIFIED_PATH))
        {
            ApplyUnifiedPanel();
            return;
        }

        Scene scene = ValidateScene();
        Transform screens = FindRoot(scene, "UI_BattleScreens");
        RectTransform parent = FindChild(screens, "Canvas_Preparation") as RectTransform;
        if (parent == null || parent.GetComponent<Canvas>() == null)
            throw new InvalidOperationException("Canvas_Preparation의 RectTransform과 Canvas가 필요합니다.");
        if (PrefabUtility.IsPartOfPrefabInstance(parent.gameObject) || parent.GetComponent<LayoutGroup>() != null)
            throw new InvalidOperationException("Prefab 또는 LayoutGroup이 관리하는 부모에는 패널을 적용하지 않습니다.");

        Transform ready = FindChild(screens, "Canvas_GetReady");
        Transform backdrop = ready == null ? null : FindChild(ready, "Background");
        Canvas readyCanvas = ready == null ? null : ready.GetComponent<Canvas>();
        if (readyCanvas == null || backdrop == null || backdrop.GetComponent<Image>() == null)
            throw new InvalidOperationException("기존 배경과 HUD의 표시 순서를 유지할 Canvas_GetReady/Background가 필요합니다.");

        Transform existingPanel = FindChild(parent, PANEL_NAME);
        ValidateExistingPanel(existingPanel);
        // 두 아트가 모두 준비된 뒤에만 Unity 임포트와 씬 변경을 시작한다.
        foreach (string path in new[] { BODY_PATH, FRAME_PATH })
            if (!File.Exists(path)) throw new FileNotFoundException("필수 패널 아트가 없습니다. 씬을 변경하지 않았습니다.", path);

        TextureImporter bodyImporter = GetImporter(BODY_PATH);
        TextureImporter frameImporter = GetImporter(FRAME_PATH);
        string bodySettings = EditorJsonUtility.ToJson(bodyImporter);
        string frameSettings = EditorJsonUtility.ToJson(frameImporter);
        int undoGroup = -1;
        try
        {
            // 임포터 설정은 씬 Undo 대상과 별개다. 성공 후에는 에셋 설정으로 유지하며,
            // 적용 도중 실패하면 아래 catch에서 이전 임포터 설정을 복원한다.
            Sprite bodySprite = ConfigureImporter(bodyImporter, BODY_PATH);
            Sprite frameSprite = ConfigureImporter(frameImporter, FRAME_PATH);
            if (bodySprite.rect.size != frameSprite.rect.size)
                throw new InvalidOperationException("Body와 Frame은 같은 캔버스 크기의 PNG여야 합니다.");
            ValidateScene();

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UNDO_LABEL);
            // 부모의 다른 컴포넌트/하위 UI는 기록하거나 변경하지 않는다.
            // 패널을 첫 형제로 옮기는 데 필요한 부모 Transform의 자식 순서만 기록한다.
            Undo.RegisterCompleteObjectUndo(parent, UNDO_LABEL);
            if (existingPanel != null)
                Undo.RegisterFullObjectHierarchyUndo(existingPanel.gameObject, UNDO_LABEL);

            RectTransform panel = existingPanel as RectTransform;
            if (panel == null) panel = CreateRect(parent, PANEL_NAME);
            ConfigurePanel(panel);
            Image body = GetOrCreateImage(panel, "Body");
            Image frame = GetOrCreateImage(panel, "Frame");
            ConfigureImage(body, bodySprite, 0, PANEL_BODY_COLOR);
            ConfigureImage(frame, frameSprite, 1, PANEL_FRAME_COLOR);
            panel.SetAsFirstSibling();

            // 준비 Canvas(1)가 공통 HUD Canvas(0)보다 위에 있으므로 첫 형제만으로는
            // 시너지 표시를 보호할 수 없다. 배경 두 개만 별도 순서로 보내고 UI는 유지한다.
            ConfigureBackgroundCanvas(backdrop, readyCanvas, -2);
            ConfigureBackgroundCanvas(panel, readyCanvas, -1);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("UI_Battle_MutedPreview 씬을 저장하지 못했습니다.");
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("하단 귀족 패널 적용 및 저장 완료: UI_BattleScreens/Canvas_Preparation/BottomNobleBackground (Body, Frame). 씬 변경은 Undo 가능하며 텍스처 임포터 설정은 별도입니다.");
        }
        catch
        {
            if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
            RestoreImporter(BODY_PATH, bodySettings);
            RestoreImporter(FRAME_PATH, frameSettings);
            throw;
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Apply Unified Noble Panel")]
    public static void ApplyUnifiedPanel()
    {
        Scene scene = ValidateScene();
        Transform screens = FindRoot(scene, "UI_BattleScreens");
        RectTransform parent = FindChild(screens, "Canvas_Preparation") as RectTransform;
        if (parent == null || parent.GetComponent<Canvas>() == null)
            throw new InvalidOperationException("Canvas_Preparation의 RectTransform과 Canvas가 필요합니다.");
        if (PrefabUtility.IsPartOfPrefabInstance(parent.gameObject) || parent.GetComponent<LayoutGroup>() != null)
            throw new InvalidOperationException("Prefab 또는 LayoutGroup이 관리하는 부모에는 패널을 적용하지 않습니다.");

        Transform ready = FindChild(screens, "Canvas_GetReady");
        Canvas readyCanvas = ready == null ? null : ready.GetComponent<Canvas>();
        if (readyCanvas == null)
            throw new InvalidOperationException("기존 HUD의 표시 순서를 유지할 Canvas_GetReady가 필요합니다.");

        RectTransform panel = FindChild(parent, PANEL_NAME) as RectTransform;
        if (panel == null) throw new InvalidOperationException("통합할 기존 BottomNobleBackground가 없습니다.");
        ValidateExistingPanel(panel, true);
        Transform body = FindChild(panel, "Body");
        Transform frame = FindChild(panel, "Frame");
        if (!panel.TryGetComponent(out Image panelImage) && (body == null || frame == null))
            throw new InvalidOperationException("기존 분리 패널의 Body와 Frame이 모두 필요합니다.");
        if (body != null) GetRequiredImage(panel, "Body");
        if (frame != null) GetRequiredImage(panel, "Frame");
        if (!File.Exists(UNIFIED_PATH))
            throw new FileNotFoundException("통합 패널 아트가 없습니다. 씬을 변경하지 않았습니다.", UNIFIED_PATH);

        TextureImporter importer = GetImporter(UNIFIED_PATH);
        string importerSettings = EditorJsonUtility.ToJson(importer);
        int undoGroup = -1;
        try
        {
            Sprite sprite = ConfigureImporter(importer, UNIFIED_PATH);
            if (sprite.rect.size != new Vector2(PANEL_WIDTH, PANEL_HEIGHT))
                throw new InvalidOperationException("통합 패널 PNG는 1920×432 크기여야 합니다.");
            ValidateScene();

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UNIFIED_UNDO_LABEL);
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, UNIFIED_UNDO_LABEL);
            if (!panel.TryGetComponent(out CanvasRenderer renderer))
                Undo.AddComponent<CanvasRenderer>(panel.gameObject);
            if (panelImage == null) panelImage = Undo.AddComponent<Image>(panel.gameObject);

            // 가로로 늘리지 않아 통합 아트에 포함된 45도 경사가 그대로 표시된다.
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;
            panel.anchoredPosition3D = Vector3.zero;
            panel.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
            panel.localScale = Vector3.one;
            panel.localRotation = Quaternion.identity;
            panelImage.enabled = true;
            panelImage.overrideSprite = null;
            panelImage.sprite = sprite;
            panelImage.color = UNIFIED_PANEL_COLOR;
            panelImage.material = null;
            panelImage.type = Image.Type.Simple;
            panelImage.preserveAspect = true;
            panelImage.raycastTarget = false;
            ConfigureBackgroundCanvas(panel, readyCanvas, -1);

            if (body != null) Undo.DestroyObjectImmediate(body.gameObject);
            if (frame != null) Undo.DestroyObjectImmediate(frame.gameObject);
            Undo.FlushUndoRecordObjects();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("UI_Battle_MutedPreview 씬을 저장하지 못했습니다.");
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("하단 통합 패널 적용 및 저장 완료: UI_BattleScreens/Canvas_Preparation/BottomNobleBackground (1920×432 Image). Body/Frame 자식만 제거했으며 원본 아트는 유지했습니다. 씬 변경은 Undo 가능합니다.");
        }
        catch
        {
            if (undoGroup >= 0)
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(undoGroup);
            }
            RestoreImporter(UNIFIED_PATH, importerSettings);
            throw;
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Apply Muted Tone Fix")]
    public static void ApplyToneFix()
    {
        Scene scene = ValidateScene();
        Transform screens = FindRoot(scene, "UI_BattleScreens");
        Transform preparation = FindChild(screens, "Canvas_Preparation");
        if (preparation == null || preparation.GetComponent<Canvas>() == null)
            throw new InvalidOperationException("Canvas_Preparation이 필요합니다.");

        // 모든 대상을 확인한 뒤 이미지의 색상과 Sprite만 변경한다.
        Transform panel = FindChild(preparation, PANEL_NAME);
        Image unifiedPanel = panel == null ? null : panel.GetComponent<Image>();
        Image panelBody = null;
        Image panelFrame = null;
        if (unifiedPanel != null)
            unifiedPanel = GetRequiredImage(preparation, PANEL_NAME);
        else
        {
            panelBody = GetRequiredImage(preparation, PANEL_NAME + "/Body");
            panelFrame = GetRequiredImage(preparation, PANEL_NAME + "/Frame");
        }
        Image rerollBody = GetRequiredImage(preparation, "Reroll_Placeholder/Body");
        Image rerollFrame = GetRequiredImage(preparation, "Reroll_Placeholder/Frame");
        Image rerollIcon = GetRequiredImage(preparation, "Reroll_Placeholder/Icon");
        Image costBody = GetRequiredImage(preparation, "Currency/Body");
        Image startBody = GetRequiredImage(preparation, "StartCombatButton/Body");
        foreach (string path in new[] { REROLL_FRAME_PATH, REROLL_ICON_PATH })
            if (!File.Exists(path)) throw new FileNotFoundException("필수 톤 조정 아트가 없습니다. 씬을 변경하지 않았습니다.", path);

        TextureImporter frameImporter = GetImporter(REROLL_FRAME_PATH);
        TextureImporter iconImporter = GetImporter(REROLL_ICON_PATH);
        string frameSettings = EditorJsonUtility.ToJson(frameImporter);
        string iconSettings = EditorJsonUtility.ToJson(iconImporter);
        int undoGroup = -1;
        try
        {
            Sprite frameSprite = ConfigureImporter(frameImporter, REROLL_FRAME_PATH);
            Sprite iconSprite = ConfigureImporter(iconImporter, REROLL_ICON_PATH);
            ValidateScene();

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(TONE_UNDO_LABEL);
            UnityEngine.Object[] targets = unifiedPanel != null
                ? new UnityEngine.Object[] { unifiedPanel, rerollBody, rerollFrame, rerollIcon, costBody, startBody }
                : new UnityEngine.Object[] { panelBody, panelFrame, rerollBody, rerollFrame, rerollIcon, costBody, startBody };
            Undo.RecordObjects(targets, TONE_UNDO_LABEL);

            // 통합 후에도 이전에 승인된 어두운 벨벳 톤을 유지한다.
            if (unifiedPanel != null) unifiedPanel.color = UNIFIED_PANEL_COLOR;
            else
            {
                panelBody.color = PANEL_BODY_COLOR;
                panelFrame.color = PANEL_FRAME_COLOR;
            }
            rerollBody.color = ACTION_BODY_COLOR;
            costBody.color = ACTION_BODY_COLOR;
            startBody.color = ACTION_BODY_COLOR;
            rerollFrame.overrideSprite = null;
            rerollFrame.sprite = frameSprite;
            rerollFrame.color = Color.white;
            rerollFrame.preserveAspect = true;
            rerollIcon.overrideSprite = null;
            rerollIcon.sprite = iconSprite;
            rerollIcon.color = Color.white;

            // 같은 호출 안에서 저장이 실패해도 RecordObjects 변경을 즉시 되돌릴 수 있도록 확정한다.
            Undo.FlushUndoRecordObjects();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("UI_Battle_MutedPreview 씬을 저장하지 못했습니다.");
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("전투 프리뷰 하단 톤 조정 및 저장 완료: 하단 패널, 리롤 Body/Frame/Icon, 코스트 Body, 시작 Body. 씬 변경은 Undo 가능하며 위치·크기와 버튼 연결은 유지했습니다.");
        }
        catch
        {
            if (undoGroup >= 0)
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(undoGroup);
            }
            RestoreImporter(REROLL_FRAME_PATH, frameSettings);
            RestoreImporter(REROLL_ICON_PATH, iconSettings);
            throw;
        }
    }

    private static Scene ValidateScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded || scene.path != PREVIEW_SCENE)
            throw new InvalidOperationException("UI_Battle_MutedPreview 씬의 Edit Mode에서만 실행할 수 있습니다.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Prefab 편집 모드를 닫은 뒤 프리뷰 씬에서 실행해 주세요.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("열린 씬의 미저장 변경을 먼저 저장하거나 보존해 주세요. 패널을 적용하지 않았습니다.");
        return scene;
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        Transform result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != name) continue;
            if (result != null) throw new InvalidOperationException("같은 이름의 씬 루트가 둘 이상 있습니다: " + name);
            result = root.transform;
        }
        if (result == null) throw new InvalidOperationException("필수 씬 루트가 없습니다: " + name);
        return result;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        Transform result = null;
        foreach (Transform child in parent)
        {
            if (child.name != name) continue;
            if (result != null) throw new InvalidOperationException("같은 이름의 자식이 둘 이상 있습니다: " + parent.name + "/" + name);
            result = child;
        }
        return result;
    }

    private static Image GetRequiredImage(Transform parent, string path)
    {
        Transform target = parent;
        foreach (string name in path.Split('/'))
        {
            target = FindChild(target, name);
            if (target == null) throw new InvalidOperationException("필수 톤 조정 대상이 없습니다: " + path);
        }
        if (PrefabUtility.IsPartOfPrefabInstance(target.gameObject) || !target.TryGetComponent(out Image image))
            throw new InvalidOperationException("톤 조정 대상은 Prefab에 속하지 않는 Image여야 합니다: " + path);
        return image;
    }

    private static void ValidateExistingPanel(Transform panel, bool allowUnifiedImage = false)
    {
        if (panel == null) return;
        ValidateComponents(panel, false, allowUnifiedImage);
        // 이름 충돌이나 수동으로 추가한 UI를 덮어쓰지 않고 중단한다.
        foreach (Transform child in panel)
        {
            if (child.name != "Body" && child.name != "Frame")
                throw new InvalidOperationException("기존 패널에 예상하지 못한 자식이 있습니다: " + child.name);
            ValidateComponents(child, true);
            if (child.childCount != 0)
                throw new InvalidOperationException("패널 이미지 아래의 기존 오브젝트를 보호하기 위해 중단했습니다: " + child.name);
        }
        FindChild(panel, "Body");
        FindChild(panel, "Frame");
    }

    private static void ValidateComponents(Transform target, bool isImage, bool allowUnifiedImage = false)
    {
        if (!(target is RectTransform) || PrefabUtility.IsPartOfPrefabInstance(target.gameObject))
            throw new InvalidOperationException("패널은 Prefab에 속하지 않는 RectTransform이어야 합니다: " + target.name);
        foreach (Component component in target.GetComponents<Component>())
        {
            bool isAllowed = component is RectTransform || (!isImage && component is Canvas) || ((isImage || allowUnifiedImage) && (component is CanvasRenderer || component is Image));
            if (!isAllowed)
                throw new InvalidOperationException("기존 컴포넌트를 보호하기 위해 중단했습니다: " + target.name);
        }
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, UNDO_LABEL);
        Undo.SetTransformParent(created.transform, parent, UNDO_LABEL);
        created.layer = parent.gameObject.layer;
        return (RectTransform)created.transform;
    }

    private static Image GetOrCreateImage(RectTransform parent, string name)
    {
        RectTransform target = FindChild(parent, name) as RectTransform;
        if (target == null) target = CreateRect(parent, name);
        if (!target.TryGetComponent(out CanvasRenderer renderer))
            Undo.AddComponent<CanvasRenderer>(target.gameObject);
        if (!target.TryGetComponent(out Image image))
            image = Undo.AddComponent<Image>(target.gameObject);
        return image;
    }

    private static void ConfigurePanel(RectTransform panel)
    {
        panel.gameObject.SetActive(true);
        panel.anchorMin = new Vector2(0, 0);
        panel.anchorMax = new Vector2(1, 0);
        panel.pivot = new Vector2(0.5f, 0);
        panel.anchoredPosition3D = Vector3.zero;
        panel.sizeDelta = new Vector2(0, PANEL_HEIGHT);
        panel.localScale = Vector3.one;
        panel.localRotation = Quaternion.identity;
    }

    private static void ConfigureBackgroundCanvas(Transform target, Canvas hudCanvas, int relativeOrder)
    {
        Canvas canvas = target.GetComponent<Canvas>();
        if (canvas == null) canvas = Undo.AddComponent<Canvas>(target.gameObject);
        else Undo.RecordObject(canvas, UNDO_LABEL);
        canvas.overrideSorting = true;
        canvas.sortingLayerID = hudCanvas.sortingLayerID;
        canvas.sortingOrder = hudCanvas.sortingOrder + relativeOrder;
        // 장식만 별도 정렬한다. GraphicRaycaster는 추가하지 않는다.
    }

    private static void ConfigureImage(Image image, Sprite sprite, int siblingIndex, Color color)
    {
        RectTransform rect = image.rectTransform;
        rect.gameObject.SetActive(true);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = Vector3.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.SetSiblingIndex(siblingIndex);
        image.enabled = true;
        image.overrideSprite = null;
        image.sprite = sprite;
        image.color = color;
        image.material = null;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private static TextureImporter GetImporter(string path)
    {
        // .meta 파일은 직접 작성하지 않고 Unity가 생성하도록 한다.
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("패널 PNG를 임포트할 수 없습니다: " + path);
        return importer;
    }

    private static Sprite ConfigureImporter(TextureImporter importer, string path)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
        importer.spriteBorder = Vector4.zero;
        importer.spritePixelsPerUnit = 100;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("패널 Sprite를 불러올 수 없습니다: " + path);
        return sprite;
    }

    private static void RestoreImporter(string path, string settings)
    {
        try
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("TextureImporter가 없습니다.");
            EditorJsonUtility.FromJsonOverwrite(settings, importer);
            importer.SaveAndReimport();
        }
        catch (Exception exception)
        {
            Debug.LogError("패널 임포터 설정 복원 실패. Inspector에서 확인해 주세요: " + path + "\n" + exception);
        }
    }
}
