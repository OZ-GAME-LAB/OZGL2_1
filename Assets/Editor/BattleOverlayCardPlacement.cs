using System;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 승인된 전투 프리뷰의 임시 카드만 보존 후 숨기고, 기존 카드 Prefab 인스턴스를 배치한다.
// 씬 저장은 호출한 오버레이 Builder가 담당한다.
public static class BattleOverlayCardPlacement
{
    private const string PREVIEW_SCENE = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    private const string PREFAB_DIRECTORY = "Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/";
    private const string INSTANCE_GROUP_NAME = "CardPrefabInstances";
    private const string UNDO_NAME = "전투 프리뷰 카드 Prefab 배치";
    private static readonly string[] PREFAB_NAMES =
    {
        "BattleCard_Unit", "BattleCard_LandSlot", "BattleCard_Relic"
    };

    public static void Apply(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.path != PREVIEW_SCENE)
            throw new InvalidOperationException("UI_Battle_MutedPreview 씬에만 카드를 배치할 수 있습니다.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 카드를 배치해 주세요.");

        Transform screens = RequireUniqueRoot(scene, "UI_BattleScreens");
        Transform preparation = RequireUniqueChild(screens, "Canvas_Preparation");
        RectTransform choices = RequireRect(RequireUniqueChild(preparation, "ChoiceCards"));
        foreach (Transform child in choices)
            if (child.name == INSTANCE_GROUP_NAME)
                throw new InvalidOperationException("이미 카드 Prefab 배치 그룹이 있습니다. 기존 인스턴스는 덮어쓰지 않습니다.");

        var originalCards = new RectTransform[PREFAB_NAMES.Length];
        var prefabs = new GameObject[PREFAB_NAMES.Length];
        var centers = new Vector3[PREFAB_NAMES.Length];
        var scales = new float[PREFAB_NAMES.Length];

        // 씬을 수정하기 전에 모든 대상과 크기를 검증한다.
        for (int index = 0; index < PREFAB_NAMES.Length; index++)
        {
            RectTransform original = RequireRect(RequireUniqueChild(choices, "Card_" + (index + 1)));
            string prefabPath = PREFAB_DIRECTORY + PREFAB_NAMES[index] + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null || !PrefabUtility.IsPartOfPrefabAsset(prefab))
                throw new InvalidOperationException("카드 Prefab이 없습니다: " + prefabPath);
            RectTransform prefabRect = RequireRect(prefab.transform);
            if (prefab.GetComponent<UIBattlePreparationCardView>() == null)
                throw new InvalidOperationException("카드 표시 컴포넌트가 없습니다: " + prefabPath);
            if (original.rect.width <= 0 || original.rect.height <= 0 ||
                prefabRect.rect.width <= 0 || prefabRect.rect.height <= 0)
                throw new InvalidOperationException("카드의 표시 영역이 유효하지 않습니다: " + original.name);
            if (original.localRotation != Quaternion.identity || original.localScale != Vector3.one)
                throw new InvalidOperationException("기존 카드의 회전 또는 배율을 먼저 확인해 주세요: " + original.name);

            originalCards[index] = original;
            prefabs[index] = prefab;
            centers[index] = original.TransformPoint(original.rect.center);
            // 원본 600×1100의 비율을 유지한다. 현재 영역 280.77×319에서는 174×319로 표시된다.
            scales[index] = Mathf.Min(original.rect.width / prefabRect.rect.width,
                original.rect.height / prefabRect.rect.height);
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);
        try
        {
            var groupObject = new GameObject(INSTANCE_GROUP_NAME, typeof(RectTransform));
            groupObject.layer = choices.gameObject.layer;
            RectTransform group = groupObject.GetComponent<RectTransform>();
            group.SetParent(choices, false);
            group.anchorMin = Vector2.zero;
            group.anchorMax = Vector2.one;
            group.pivot = new Vector2(0.5f, 0.5f);
            group.offsetMin = group.offsetMax = Vector2.zero;
            Undo.RegisterCreatedObjectUndo(groupObject, UNDO_NAME);

            for (int index = 0; index < prefabs.Length; index++)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefabs[index], group) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("카드 Prefab 인스턴스 생성에 실패했습니다: " + PREFAB_NAMES[index]);
                Undo.RegisterCreatedObjectUndo(instance, UNDO_NAME);
                RectTransform instanceRect = RequireRect(instance.transform);
                instanceRect.anchorMin = instanceRect.anchorMax = new Vector2(0, 1);
                instanceRect.pivot = new Vector2(0.5f, 0.5f);
                instanceRect.localRotation = Quaternion.identity;
                instanceRect.localScale = Vector3.one * scales[index];
                instanceRect.position = centers[index];
                PrefabUtility.RecordPrefabInstancePropertyModifications(instanceRect);

                GameObject original = originalCards[index].gameObject;
                Undo.RecordObject(original, UNDO_NAME);
                original.SetActive(false);
                if (PrefabUtility.IsPartOfPrefabInstance(original))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(original);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("ChoiceCards/CardPrefabInstances에 카드 3종을 원본 비율로 배치했습니다. " +
                "기존 Card_1~3은 이름·내용을 보존하고 비활성화했으며 씬은 저장하지 않았습니다.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    private static Transform RequireUniqueRoot(Scene scene, string name)
    {
        Transform found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != name) continue;
            if (found != null) throw new InvalidOperationException("중복 씬 루트가 있습니다: " + name);
            found = root.transform;
        }
        return found != null ? found : throw new InvalidOperationException("필수 씬 루트가 없습니다: " + name);
    }

    private static Transform RequireUniqueChild(Transform parent, string name)
    {
        Transform found = null;
        foreach (Transform child in parent)
        {
            if (child.name != name) continue;
            if (found != null) throw new InvalidOperationException("중복 UI 오브젝트가 있습니다: " + parent.name + "/" + name);
            found = child;
        }
        return found != null ? found : throw new InvalidOperationException("필수 UI 오브젝트가 없습니다: " + parent.name + "/" + name);
    }

    private static RectTransform RequireRect(Transform target)
    {
        if (target is RectTransform rect) return rect;
        throw new InvalidOperationException("RectTransform이 없습니다: " + target.name);
    }
}
