using System;
using System.Collections.Generic;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(UIBattlePreparationCardView)), CanEditMultipleObjects]
public sealed class UIBattlePreparationCardViewEditor : Editor
{
    private const string PREFAB_ROOT = "Assets/06.UI/BattleMutedPreview/Cards_v1/Prefabs/";
    private static readonly string[] PREFAB_NAMES =
    {
        "BattleCard_Unit",
        "BattleCard_LandSlot",
        "BattleCard_Relic"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "격자 크기, 셀 크기, 선 굵기를 변경한 뒤 아래 버튼을 눌러야 " +
            "FootprintGrid 자식에 배치가 반영됩니다. 배포 권장값은 5x5 / 76 / 4입니다.",
            MessageType.Info);

        if (!GUILayout.Button("격자 레이아웃 적용")) return;

        foreach (UnityEngine.Object item in targets)
        {
            var view = item as UIBattlePreparationCardView;
            if (view == null) continue;

            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "전투 카드 격자 레이아웃 적용");
            if (!view.ApplyFootprintGridLayout()) continue;

            EditorUtility.SetDirty(view);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            if (view.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }
    }

    [MenuItem("Tools/OZGL2/Battle/Apply 5x5 Card Grids")]
    private static void ApplyFiveByFiveCardGrids()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("컴파일과 임포트가 끝난 Edit Mode에서 실행해 주세요.");

        var changedPaths = new List<string>();
        foreach (string prefabName in PREFAB_NAMES)
        {
            string path = PREFAB_ROOT + prefabName + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<UIBattlePreparationCardView>();
                if (view == null) throw new InvalidOperationException("UIBattlePreparationCardView가 없습니다: " + path);

                RectTransform grid = root.transform.Find("FootprintGrid") as RectTransform;
                if (grid == null) throw new InvalidOperationException("FootprintGrid가 없습니다: " + path);

                var properties = new SerializedObject(view);
                properties.FindProperty("_footprintGridRoot").objectReferenceValue = grid;
                properties.FindProperty("_footprintGridSize").vector2IntValue = new Vector2Int(5, 5);
                properties.FindProperty("_footprintCellSize").floatValue = 76f;
                properties.FindProperty("_footprintGridLineThickness").floatValue = 4f;
                properties.ApplyModifiedPropertiesWithoutUndo();

                if (!view.ApplyFootprintGridLayout())
                    throw new InvalidOperationException("격자 레이아웃 적용에 실패했습니다: " + path);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || saved == null) throw new InvalidOperationException("Prefab 저장에 실패했습니다: " + path);
                changedPaths.Add(path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("5x5 카드 격자 적용 완료:\n" + string.Join("\n", changedPaths) +
                  "\nPrefab Asset 저장은 Undo 대상이 아니므로 Git Diff에서 변경을 확인해 주세요.");
    }
}
