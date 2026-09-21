using System;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 저장 확인 UI 조립만 담당한다. Runtime 편집/저장 상태와 분리한다.
public static class LobbySkillSaveFlowBuilder
{
    private const string OVERLAYS = "Assets/06.UI/LobbyMutedPreview/Overlays";

    [MenuItem("Tools/OZGL2/Lobby/Apply Skill Save Flow And Frames")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if ((stage == null && (scene.path != "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity" || scene.isDirty)) ||
            (stage != null && (stage.assetPath != LobbySkillSettingsPreviewBuilder.PREFAB || stage.scene.isDirty)))
            throw new InvalidOperationException("저장된 대상 씬 또는 스킬 Prefab에서 실행하세요.");
        stage = PrefabStageUtility.OpenPrefab(LobbySkillSettingsPreviewBuilder.PREFAB);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply skill save confirmation and category frames");
        Undo.RegisterFullObjectHierarchyUndo(stage.prefabContentsRoot, "Skill save flow");
        try
        {
            LobbySkillCategoryStyleBuilder.Configure(stage.prefabContentsRoot);
            Configure(stage.prefabContentsRoot);
            LobbySkillFrameAlignmentBuilder.Configure(stage.prefabContentsRoot);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("스킬 Save 초기 비활성, ExitConfirmation, 분류별 장착 프레임 연결. Prefab Mode에서 Ctrl+Z 지원. 확인 후 저장하세요.");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    public static void Configure(GameObject root)
    {
        if (!root.TryGetComponent(out UISkillLoadoutPreview view)) throw new InvalidOperationException("스킬 미리보기 컴포넌트 누락");
        var so = new SerializedObject(view);
        var save = so.FindProperty("_saveButton").objectReferenceValue as UISkillArtButton;
        if (save == null) throw new InvalidOperationException("Save 연결 누락");
        save.interactable = false;
        var status = so.FindProperty("_status").objectReferenceValue as TMP_Text;
        if (status != null) status.text = string.Empty;
        Transform previous = root.transform.Find("ExitConfirmation");
        if (previous != null)
        {
            if (!previous.TryGetComponent(out UIPopupPanel existing)) throw new InvalidOperationException("기존 확인창 구조를 확인하세요.");
            so.FindProperty("_exitConfirmation").objectReferenceValue = existing;
            so.ApplyModifiedProperties();
            ConfigureDialogButtons(previous);
            return; // 수동 레이아웃 조정을 재실행으로 덮어쓰지 않는다.
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/98.ExternalAssets/00.LocalStaging/01.Font/DOSMyungjo Overlay Pixel.asset");
        var material = AssetDatabase.LoadAssetAtPath<Material>(OVERLAYS + "/Fonts/DOSMyungjo Trait Outline.mat");
        if (font == null || material == null) throw new InvalidOperationException("공유 폰트/Material을 복원하세요.");
        Image overlay = Image("ExitConfirmation", root.transform, null, Vector2.zero, Vector2.zero);
        overlay.rectTransform.anchorMin = Vector2.zero;
        overlay.rectTransform.anchorMax = Vector2.one;
        overlay.rectTransform.offsetMin = overlay.rectTransform.offsetMax = Vector2.zero;
        overlay.color = new Color(0, 0, 0, .76f);
        var popup = Undo.AddComponent<UIPopupPanel>(overlay.gameObject);
        overlay.GetComponent<CanvasGroup>().ignoreParentGroups = true;
        var canvas = Undo.AddComponent<Canvas>(overlay.gameObject);
        canvas.overrideSorting = true;
        canvas.sortingOrder = root.GetComponent<Canvas>().sortingOrder + 1;
        Undo.AddComponent<GraphicRaycaster>(overlay.gameObject);
        Image panel = Image("Panel", overlay.transform, LoadSprite(OVERLAYS + "/Sprites/Panel_Frame.png"), Vector2.zero, new Vector2(860, 350));
        panel.type = UnityEngine.UI.Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 8;
        Label("Message", panel.transform, "스킬 변경 사항이 저장되지 않았습니다.\n저장하지 않고 로비로 돌아가시겠습니까?",
            new Vector2(0, 44), new Vector2(780, 126), 28, font, material);
        var cancel = Button(panel.transform, "Cancel", "취소", -147, font, material);
        var leave = Button(panel.transform, "Leave", "로비로", 147, font, material);
        UnityEventTools.AddPersistentListener(cancel.onClick, view.CancelExit);
        UnityEventTools.AddPersistentListener(leave.onClick, view.ConfirmExitWithoutSaving);
        var nav = cancel.navigation; nav.mode = Navigation.Mode.Explicit; nav.selectOnRight = leave; cancel.navigation = nav;
        nav = leave.navigation; nav.mode = Navigation.Mode.Explicit; nav.selectOnLeft = cancel; leave.navigation = nav;
        var panelSo = new SerializedObject(popup);
        panelSo.FindProperty("_firstSelected").objectReferenceValue = cancel;
        panelSo.ApplyModifiedProperties();
        so.FindProperty("_exitConfirmation").objectReferenceValue = popup;
        so.ApplyModifiedProperties();
        overlay.gameObject.SetActive(false);
    }

    private static Button Button(Transform parent, string name, string text, float x, TMP_FontAsset font, Material material)
    {
        var image = Image(name, parent, LoadSprite(OVERLAYS + "/DetailArt_v2/Button_Dialog_244x72.png"), new Vector2(x, -102), new Vector2(244, 72));
        image.preserveAspect = true;
        var button = Undo.AddComponent<UISkillArtButton>(image.gameObject);
        button.targetGraphic = image;
        Label("Label", image.transform, text, Vector2.zero, new Vector2(200, 54), 30, font, material);
        StyleDialogButton(button);
        return button;
    }

    private static void ConfigureDialogButtons(Transform dialog)
    {
        var buttons = new UISkillArtButton[2];
        string[] names = { "Cancel", "Leave" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform target = dialog.Find("Panel/" + names[i]);
            var old = target.GetComponent<Button>();
            var art = old as UISkillArtButton;
            if (art == null)
            {
                var click = old.onClick;
                Undo.DestroyObjectImmediate(old);
                art = Undo.AddComponent<UISkillArtButton>(target.gameObject);
                art.onClick = click; // 취소/나가기의 기존 저장 기능 연결은 그대로 보존한다.
            }
            StyleDialogButton(art);
            buttons[i] = art;
        }
        var nav = buttons[0].navigation; nav.mode = Navigation.Mode.Explicit; nav.selectOnRight = buttons[1]; buttons[0].navigation = nav;
        nav = buttons[1].navigation; nav.mode = Navigation.Mode.Explicit; nav.selectOnLeft = buttons[0]; buttons[1].navigation = nav;
        var panel = new SerializedObject(dialog.GetComponent<UIPopupPanel>());
        panel.FindProperty("_firstSelected").objectReferenceValue = buttons[0];
        panel.ApplyModifiedProperties();
    }

    private static void StyleDialogButton(UISkillArtButton button)
    {
        Undo.RecordObject(button, "Unify confirmation button hover");
        var image = button.GetComponent<Image>();
        image.sprite = LoadSprite(OVERLAYS + "/DetailArt_v2/Button_Dialog_244x72.png");
        image.color = Color.white;
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .74f, .36f);
        colors.pressedColor = new Color(.63f, .54f, .48f);
        colors.selectedColor = new Color(1f, .9f, .7f);
        colors.colorMultiplier = 1;
        colors.fadeDuration = .1f;
        button.colors = colors;
        var so = new SerializedObject(button);
        foreach (string field in new[] { "_normal", "_hover", "_pressed", "_chosen", "_disabled" })
            so.FindProperty(field).objectReferenceValue = image.sprite;
        so.FindProperty("_label").objectReferenceValue = button.GetComponentInChildren<TMP_Text>();
        so.FindProperty("_separatePointerHover").boolValue = true;
        so.ApplyModifiedProperties();
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        Undo.RegisterCreatedObjectUndo(go, "Create skill exit confirmation");
        return rect;
    }

    private static Image Image(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var image = Undo.AddComponent<Image>(Rect(name, parent, position, size).gameObject);
        image.sprite = sprite;
        image.raycastTarget = true;
        return image;
    }

    private static void Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, TMP_FontAsset font, Material material)
    {
        var label = Undo.AddComponent<TextMeshProUGUI>(Rect(name, parent, position, size).gameObject);
        label.font = font; label.fontSharedMaterial = material; label.text = text;
        label.fontSize = fontSize; label.color = new Color32(248, 242, 235, 255);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
    }

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Sprite 누락: " + path);
}
