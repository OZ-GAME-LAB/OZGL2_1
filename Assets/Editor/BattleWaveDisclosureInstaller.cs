using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>승인된 전투 미리보기의 웨이브 펼침 UI만 연결하는 일회성 마이그레이션.</summary>
public static class BattleWaveDisclosureInstaller
{
    private const string SCENE = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    private const string CANVAS = "Assets/02.Prefabs/UI/UI_Panel/Canvas_GetReady.prefab";
    private const string HUD = "Assets/06.UI/BattleMutedPreview/Prefabs/BattleHud.prefab";
    private const string ARROW = "Assets/06.UI/BattleMutedPreview/WaveDisclosure_v1/Icon_WaveChevron.png";
    private const string FRAME = "Assets/06.UI/BattleMutedPreview/Sprites/Frame_DiamondNeutral.png";
    private const string UNDO = "웨이브 펼침 UI 연결";

    private sealed class Binding
    {
        public MonoBehaviour Owner;
        public string Property;
        public string Path;
        public Type Type;
    }

    [MenuItem("Tools/OZGL2/Battle/Install Wave Preview Disclosure")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            scene.path != SCENE || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("UI_Battle_MutedPreview의 Edit Mode에서 실행하세요.");
        if (scene.isDirty) throw new InvalidOperationException("미저장 씬 변경을 먼저 저장하여 보존하세요.");
        Transform screens = scene.GetRootGameObjects().Single(g => g.name == "UI_BattleScreens").transform;
        Transform ready = screens.Find("Canvas_GetReady");
        if (ready == null || ready.Find("WavePreview") == null || ready.Find("WavePreviewViewport") != null)
            throw new InvalidOperationException("예상한 설치 전 구조가 아닙니다. 기존 설정을 덮어쓰지 않습니다.");
        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ready.gameObject) != CANVAS)
            throw new InvalidOperationException("Canvas_GetReady의 원본 연결이 다릅니다.");
        var bindings = CaptureBindings(scene, ready);
        if (bindings.Count(b => b.Owner == screens.GetComponent<UIBattleMutedPreviewView>()) != 10)
            throw new InvalidOperationException("HUD/적 표시 참조 10개를 먼저 점검하세요.");
        string[] buttonsBefore = CaptureButtons(scene);
        string backup = "Tools/Art/Backups/WaveDisclosure_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        foreach (string file in new[] { SCENE, CANVAS, HUD }) File.Copy(file, backup + "/" + Path.GetFileName(file));

        ConfigureArrowImporter();
        Sprite arrow = AssetDatabase.LoadAssetAtPath<Sprite>(ARROW);
        Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(FRAME);
        if (arrow == null || frame == null) throw new InvalidOperationException("화살표/프레임 Sprite 누락");

        // 원본 Prefab 파일은 백업으로 복구하고, 씬 참조 재연결은 Undo로 복구할 수 있다.
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO);
        foreach (MonoBehaviour owner in bindings.Select(b => b.Owner).Distinct())
            Undo.RegisterCompleteObjectUndo(owner, UNDO);

        GameObject hud = PrefabUtility.LoadPrefabContents(HUD);
        try
        {
            Text label = hud.transform.Find("WaveText").GetComponent<Text>();
            label.rectTransform.sizeDelta = new Vector2(288f, 60f);
            label.raycastTarget = true;
            Button labelButton = hud.transform.Find("WaveText").gameObject.AddComponent<Button>();
            ConfigureButton(labelButton, label);

            RectTransform buttonRect = CreateRect("WaveToggleButton", hud.transform, new Vector2(436f, -17.5f), new Vector2(44f, 44f));
            Image frameImage = buttonRect.gameObject.AddComponent<Image>();
            frameImage.sprite = frame;
            frameImage.preserveAspect = true;
            frameImage.raycastTarget = true;
            Button button = buttonRect.gameObject.AddComponent<Button>();
            ConfigureButton(button, frameImage);

            RectTransform arrowRect = CreateRect("Arrow", buttonRect, Vector2.zero, new Vector2(26f, 26f));
            arrowRect.anchorMin = arrowRect.anchorMax = arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Image arrowImage = arrowRect.gameObject.AddComponent<Image>();
            arrowImage.sprite = arrow;
            arrowImage.preserveAspect = true;
            arrowImage.raycastTarget = false;
            SavePrefab(hud, HUD);
        }
        finally { PrefabUtility.UnloadPrefabContents(hud); }

        GameObject contents = PrefabUtility.LoadPrefabContents(CANVAS);
        try
        {
            RectTransform panel = (RectTransform)contents.transform.Find("WavePreview");
            int sibling = panel.GetSiblingIndex();
            RectTransform viewport = CreateRect("WavePreviewViewport", contents.transform, panel.anchoredPosition, panel.sizeDelta);
            viewport.anchorMin = panel.anchorMin;
            viewport.anchorMax = panel.anchorMax;
            viewport.pivot = panel.pivot;
            viewport.SetSiblingIndex(sibling);
            viewport.gameObject.AddComponent<RectMask2D>();
            panel.SetParent(viewport, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = Vector2.zero;
            panel.localScale = Vector3.one;
            CanvasGroup panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.ignoreParentGroups = false;
            panelGroup.alpha = 1f;

            UIWavePreviewDisclosure disclosure = contents.AddComponent<UIWavePreviewDisclosure>();
            var serialized = new SerializedObject(disclosure);
            SetReference(serialized, "_waveLabelButton", contents.transform.Find("BattleHUD/WaveText").GetComponent<Button>());
            SetReference(serialized, "_toggleButton", contents.transform.Find("BattleHUD/WaveToggleButton").GetComponent<Button>());
            SetReference(serialized, "_arrow", contents.transform.Find("BattleHUD/WaveToggleButton/Arrow"));
            SetReference(serialized, "_viewport", viewport);
            SetReference(serialized, "_panel", panel);
            SetReference(serialized, "_panelCanvasGroup", panelGroup);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            SavePrefab(contents, CANVAS);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        ready = screens.Find("Canvas_GetReady");
        foreach (Binding binding in bindings)
        {
            string path = binding.Path.StartsWith("WavePreview/", StringComparison.Ordinal)
                ? "WavePreviewViewport/" + binding.Path : binding.Path;
            Transform target = ready.Find(path);
            if (target == null || binding.Owner == null) throw new InvalidOperationException("참조 복구 대상 누락: " + path);
            Object value = binding.Type == typeof(GameObject) ? target.gameObject : target.GetComponent(binding.Type);
            var serialized = new SerializedObject(binding.Owner);
            var property = serialized.FindProperty(binding.Property);
            if (value == null || property == null) throw new InvalidOperationException("참조 복구 실패: " + binding.Property);
            if (property.objectReferenceValue == value) continue;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            if (PrefabUtility.IsPartOfPrefabInstance(binding.Owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(binding.Owner);
        }
        string[] buttonsAfter = CaptureButtons(scene);
        if (!buttonsBefore.SequenceEqual(buttonsAfter)) throw new InvalidOperationException("기존 버튼 연결이 달라졌습니다. 백업: " + backup);
        if (ready.GetComponent<UIWavePreviewDisclosure>() == null || ready.Find("WavePreviewViewport/WavePreview") == null)
            throw new InvalidOperationException("펼침 구조 연결 실패. 백업: " + backup);
        screens.GetComponent<UIBattleMutedPreviewView>().RefreshView();
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장 실패. 백업: " + backup);
        Debug.Log("웨이브 펼침 설치 완료. 변경: BattleHud, Canvas_GetReady, Scene 참조. 씬 참조 Undo 지원 / Prefab 백업: " + backup);
    }

    private static void ConfigureArrowImporter()
    {
        AssetDatabase.ImportAsset(ARROW, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(ARROW) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("화살표 PNG를 먼저 생성하세요.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void ConfigureButton(Button button, Graphic graphic)
    {
        button.targetGraphic = graphic;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.77f, 0.48f, 1f);
        colors.pressedColor = new Color(0.78f, 0.48f, 0.27f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }

    private static void SetReference(SerializedObject serialized, string field, Object value)
    {
        if (value == null) throw new InvalidOperationException("필수 참조 누락: " + field);
        serialized.FindProperty(field).objectReferenceValue = value;
    }

    private static void SavePrefab(GameObject contents, string path)
    {
        bool saved;
        PrefabUtility.SaveAsPrefabAsset(contents, path, out saved);
        if (!saved) throw new IOException("Prefab 저장 실패: " + path);
    }

    private static List<Binding> CaptureBindings(Scene scene, Transform ready)
    {
        var result = new List<Binding>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (MonoBehaviour owner in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (owner == null || owner.transform.IsChildOf(ready)) continue;
            var property = new SerializedObject(owner).GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object value = property.objectReferenceValue;
                var component = value as Component;
                var go = value as GameObject;
                Transform target = component != null ? component.transform : go != null ? go.transform : null;
                if (target == null || !target.IsChildOf(ready)) continue;
                string path = AnimationUtility.CalculateTransformPath(target, ready);
                if (!path.StartsWith("BattleHUD/", StringComparison.Ordinal) && !path.StartsWith("WavePreview/", StringComparison.Ordinal)) continue;
                result.Add(new Binding { Owner = owner, Property = property.propertyPath, Path = path, Type = value.GetType() });
            }
        }
        return result;
    }

    private static string[] CaptureButtons(Scene scene)
    {
        var result = new List<string>();
        foreach (Button button in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Button>(true)))
        {
            if (button.name == "WaveText" || button.name == "WaveToggleButton") continue;
            string path = AnimationUtility.CalculateTransformPath(button.transform, null);
            result.Add(path + ":" + button.interactable + ":" + button.onClick.GetPersistentEventCount());
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                result.Add(path + ":" + button.onClick.GetPersistentMethodName(i) + ":" + button.onClick.GetPersistentTarget(i));
        }
        return result.ToArray();
    }
}
