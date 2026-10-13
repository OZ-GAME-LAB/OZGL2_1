using TMPro;
using UnityEditor;
using UnityEngine;

namespace OZGL2.UIBridge.EditorTools
{
    /// <summary>
    /// 동적 TMP 글꼴(빛의 계승자 등)은 플레이 중 새 글자가 들어올 때마다 글꼴 에셋 파일에 글자 정보와 그림판이 쌓이고,
    /// 에디터는 그 상태를 그대로 저장한다. 플레이를 여러 번 하면 글자 정보와 그림판이 어긋나(그림판 장 수·내용이 달라져)
    /// 글자가 조각처럼 깨져 보이는 일이 있었다(로딩 숫자, 난이도 선택의 「현재 진행」 등).
    /// 에디터가 열릴 때와 플레이를 끝낼 때마다 외부 패키지(98.ExternalAssets)의 동적 글꼴 쌓인 데이터를 비워, 항상 깨끗한 상태에서 시작하게 한다.
    /// 글자는 필요할 때 다시 만들어지므로 게임에는 영향이 없다. (빌드는 ClearDynamicDataOnBuild 로 이미 깨끗하다.)
    /// </summary>
    [InitializeOnLoad]
    public static class DynamicFontCleaner
    {
        static DynamicFontCleaner()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) Clean();
            };
            EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Clean(); };
        }

        [MenuItem("Tools/OZGL2/동적 글꼴 데이터 비우기")]
        private static void CleanFromMenu() => Clean();

        private static void Clean()
        {
            int cleaned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                // 팀이 글자를 미리 채워 저장해 둔 글꼴(06.UI 등 git 에 올라간 것, 예: BattleOverlay Symbols SDF 의 아이콘 글자)은 절대 건드리지 않는다.
                // git 에 올라가지 않는 외부 글꼴 패키지(98.ExternalAssets)만 비운다.
                if (!path.StartsWith("Assets/98.ExternalAssets/")) continue;
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;
                if (font.characterTable != null && font.characterTable.Count == 0 && font.glyphTable != null && font.glyphTable.Count == 0) continue;   // 이미 깨끗하다
                font.ClearFontAssetData(true);
                EditorUtility.SetDirty(font);
                cleaned++;
            }
            if (cleaned > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[글꼴] 동적 글꼴 " + cleaned + "개의 쌓인 글자 데이터를 비웠습니다(글자는 필요할 때 다시 만들어집니다).");
            }
        }
    }
}
