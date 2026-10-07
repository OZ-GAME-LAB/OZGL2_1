using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OZGL2.InGame;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 저장된 카드와 임시 복제본만 검사한다. Scene/Prefab/원본 전투 데이터는 저장하거나 수정하지 않는다.
public static class BattleCardRedesignValidation
{
    private static readonly string[] UNIT_IDS =
        { "M_WAR_01", "M_SHD_01", "M_ARC_01", "M_MAG_01", "M_ROG_01", "M_HEL_01" };
    private const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/OZGL2/Battle/Redesign/Validate Saved Cards")]
    public static void ValidateMenu() => Debug.Log(Validate());

    public static string Validate()
    {
        Need(!Application.isPlaying, "저장된 Prefab 검증은 Edit Mode에서 실행해 주세요.");
        var report = new List<string>();
        var catalog = Require<UIExpansionCardVisualCatalogSO>(BattleCardRedesignBuilder.CATALOG_PATH);
        Need(UIExpansionCardVisualCatalogSO.LoadDefault() == catalog, "Resources 기본 카탈로그 연결 불일치");
        var sprites = new HashSet<Sprite>();
        foreach (string id in BattleCardRedesignBuilder.SHAPE_IDS)
        {
            Sprite sprite = catalog.GetArtwork(id);
            Need(sprite != null && sprites.Add(sprite), id + ": 확장 삽화 누락 또는 중복 연결");
            Need(sprite == Require<Sprite>(BattleCardRedesignBuilder.ART_ROOT + "/ExpansionArt/" + id + ".png"),
                id + ": 다른 확장 모양의 삽화가 연결됨");
        }
        Need(catalog.GetArtwork("__unknown_shape__") == null && catalog.GetArtwork(null) == null,
            "미등록 확장에 잘못된 기본 삽화가 표시됨");
        report.Add("확장 6종: ID별 개별 Sprite / Resources 로드 / 미등록 ID null 정상");

        foreach (string name in new[] { "BattleCard_Unit", "BattleCard_LandSlot" })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BattleCardRedesignBuilder.PREFAB_ROOT + name + ".prefab");
            try
            {
                // TMP는 Canvas가 있어야 실제 메시와 잘림 정보를 생성한다. 임시 복제본에만 추가한다.
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                Canvas.ForceUpdateCanvases();
                bool isUnit = name == "BattleCard_Unit";
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                        name + "/" + child.name + ": Missing Script");
                Need(root.GetComponentsInChildren<Text>(true).Length == 0, name + ": Legacy Text가 남아 있음");
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                    Need(text.font != null && text.font.atlasRenderMode == UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA &&
                        text.font.material.shader.name == "TextMeshPro/Mobile/Distance Field", name + "/" + text.name + ": SDF Font 참조 누락");

                var view = root.GetComponent<UIBattlePreparationCardView>();
                Need(view != null, name + ": 카드 View 누락");
                var bindings = new SerializedObject(view);
                foreach (string field in new[] { "_titleText", "_rankText", "_typeIcon", "_artwork", "_footprintGridRoot" })
                    NeedReference(bindings, field);
                foreach (string field in isUnit
                    ? new[] { "_traitTitleText", "_traitDescriptionText", "_skillTitleText", "_skillDescriptionText",
                        "_attackLabelText", "_attackValueText", "_defenseLabelText", "_defenseValueText", "_healthLabelText", "_healthValueText" }
                    : new[] { "_areaTitleText", "_areaDescriptionText" })
                    NeedReference(bindings, field);
                Transform grid = root.transform.Find("FootprintGrid");
                Need(grid != null && !grid.gameObject.activeSelf, name + ": 기본 5x5 격자가 숨겨지지 않음");
                Need(bindings.FindProperty("_footprintGridRoot").objectReferenceValue == grid, name + ": 격자 연결 불일치");
                Image artwork = root.transform.Find("Artwork").GetComponent<Image>();
                Need(artwork != null && artwork.preserveAspect && artwork.sprite != null, name + ": 삽화/비율 유지 설정");
                string shellName = isUnit ? "CardShell_Unit" : "CardShell_LandSlot";
                Need(root.transform.Find("Shell").GetComponent<Image>().sprite ==
                    Require<Sprite>(BattleCardRedesignBuilder.ART_ROOT + "/Sprites/" + shellName + ".png"), name + ": v2 셸 미적용");

                if (isUnit)
                {
                    TMP_Text skill = FindText(root, "SkillDescriptionText");
                    Need(skill.fontSizeMin >= 24, "보유 스킬 최소 글자 크기가 24 미만");
                    foreach (string id in UNIT_IDS)
                    {
                        var entry = BattleCardRedesignBuilder.FindUnitEntry("unit." + id);
                        string description = BattleCardSkillDescription.Build(entry.BaseStats);
                        Need(description.StartsWith("3성 기준\n", StringComparison.Ordinal) &&
                            description.Split('\n').Length <= 4, id + ": 3성 기준/최대 4줄 설명 계약");
                        view.SetSkill("보유 스킬", description);
                        CheckTextFits(skill, description, id + " 보유 스킬", 4, report);
                        CheckTextFits(FindText(root, "TitleText"), entry.DisplayName, id + " 이름", 1, null);
                        for (int star = 0; star < 3; star++)
                            Need(entry.GetPortrait(star) != null, id + ": " + (star + 1) + "성 초상화 누락");
                    }
                    CheckTextFits(FindText(root, "Stats/Health/ValueText"), "1024", "3성 방패병 체력", 1, null);
                    CheckTextFits(FindText(root, "TraitDescriptionText"), "4칸", "점유 칸수", 1, null);
                    ValidateCodexDescriptions(root, view, report);
                }
                else
                {
                    ValidateLandGridBounds((RectTransform)root.transform, (RectTransform)grid);
                    CheckTextFits(FindText(root, "AreaTitleText"), "배치 영역 +4칸", "확장 칸수", 1, null);
                    CheckTextFits(FindText(root, "AreaDescriptionText"), "확장할 위치에 배치해\n유닛을 놓을 공간을 넓힙니다.",
                        "확장 설명", 2, null);
                    report.Add("발판/미등록 확장 격자: 네 모서리가 새 이미지 프레임 내부에 위치");
                }
                report.Add(name + ": 필수 참조 / 폰트 / v2 셸 / 숨긴 격자 / 실제 문구 잘림 없음");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return "카드 개편 에셋 검증 완료\n- " + string.Join("\n- ", report);
    }

    private static void ValidateLandGridBounds(RectTransform card, RectTransform grid)
    {
        var corners = new Vector3[4];
        grid.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 local = card.InverseTransformPoint(corner);
            // 카드 pivot과 무관하게 카드 좌상단을 원점으로 환산한다.
            float x = local.x - card.rect.xMin;
            float y = card.rect.yMax - local.y;
            Need(x >= 60f && x <= 540f && y >= 280f && y <= 650f,
                "발판 격자가 새 이미지 프레임 밖으로 나옴: 좌상단 기준 (" + x.ToString("0.#") + ", " + y.ToString("0.#") + ")");
        }
    }

    private static void ValidateCodexDescriptions(GameObject cardRoot, UIBattlePreparationCardView card, List<string> report)
    {
        var host = new GameObject("__CodexDescriptionValidation__");
        host.SetActive(false);
        host.transform.SetParent(cardRoot.transform, false);
        try
        {
            var source = host.AddComponent<UIUnitCodexCardView>();
            var detail = host.AddComponent<UIUnitCodexDetailView>();
            SetField(detail, "_cardView", card);
            int demonSamples = 0;
            foreach (string id in UNIT_IDS)
            {
                var entry = BattleCardRedesignBuilder.FindUnitEntry("unit." + id);
                source.ShowUnit(entry, true, null, null);
                Need(source.AppearanceCount == 3, id + ": 도감에서 1~3성 외형 선택 불가");
                for (int star = 1; star <= 3; star++)
                {
                    Need(source.AppearanceIndex == star - 1 && detail.ShowUnit(source), id + " " + star + "성 도감 바인딩 실패");
                    CheckCodexText(cardRoot, id + " " + star + "성 도감");
                    source.NextAppearance();
                    demonSamples++;
                }
            }

            var heroes = AssetDatabase.FindAssets("t:UIUnitCatalogSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<UIUnitCatalogSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(catalog => catalog != null && catalog.Entries != null)
                .SelectMany(catalog => catalog.Entries)
                .Where(entry => entry != null && entry.Faction == eUnitCodexFaction.HERO && entry.BaseStats != null)
                .GroupBy(entry => entry.Id).Select(group => group.First()).ToArray();
            Need(heroes.Length > 0, "실제 전투 데이터가 연결된 인간군 도감 항목 없음");
            foreach (var entry in heroes)
            {
                source.ShowUnit(entry, true, null, null);
                Need(detail.ShowUnit(source), entry.Id + ": 인간군 도감 바인딩 실패");
                CheckCodexText(cardRoot, entry.Id + " 인간군 도감");
            }
            report.Add("도감 공용 카드: 마왕군 " + demonSamples + "개 성급 표본 / 인간군 " + heroes.Length + "종 실제 설명 잘림 없음");
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static void CheckCodexText(GameObject root, string label)
    {
        TMP_Text skill = FindText(root, "SkillDescriptionText");
        // ShowUnit이 실제 BuildEffects와 선택 성급을 조합한 최종 문자열을 검사한다.
        CheckTextFits(skill, skill.text, label + " 전투 정보", 4, null);
        TMP_Text category = FindText(root, "TraitDescriptionText");
        CheckTextFits(category, category.text, label + " 분류", 1, null);
        TMP_Text title = FindText(root, "TitleText");
        CheckTextFits(title, title.text, label + " 이름", 1, null);
    }

    // Play Mode에서는 임시 비활성 오브젝트로, Edit Mode에서는 Preview Scene으로 표시 API만 검증한다.
    [MenuItem("Tools/OZGL2/Battle/Redesign/Validate Card Reuse and Star Stats")]
    public static void ValidateRuntimeMenu() => Debug.Log(ValidateRuntime());

    public static string ValidateRuntime()
    {
        Scene preview = default;
        GameObject root = null;
        try
        {
            if (!Application.isPlaying) preview = EditorSceneManager.NewPreviewScene();
            root = new GameObject("__BattleCardRedesignValidation__", typeof(RectTransform));
            root.SetActive(false);
            root.hideFlags = HideFlags.HideAndDontSave;
            if (preview.IsValid()) SceneManager.MoveGameObjectToScene(root, preview);
            var scroll = root.AddComponent<ScrollRect>();
            var hand = root.AddComponent<UIBattleCardHandView>();
            RectTransform viewport = ChildRect(root.transform, "Viewport", new Vector2(1100, 600));
            RectTransform content = ChildRect(viewport, "Content", new Vector2(1100, 600));
            SetField(hand, "_scrollRect", scroll);
            SetField(hand, "_viewport", viewport);
            SetField(hand, "_content", content);
            SetField(hand, "_landSlotCardPrefab", Require<GameObject>(BattleCardRedesignBuilder.PREFAB_ROOT + "BattleCard_LandSlot.prefab"));
            SetField(hand, "_isInitialized", false);

            Sprite sprite = Require<UIExpansionCardVisualCatalogSO>(BattleCardRedesignBuilder.CATALOG_PATH).GetArtwork("floor_corner3");
            Vector2Int[] cells = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
            var expansion = new BattleHandCardDisplayData("reuse", eBattleHandCardKind.LAND_SLOT, "배치 영역 확장",
                artwork: sprite, footprint: cells, showFootprint: false);
            var block = new BattleHandCardDisplayData("reuse", eBattleHandCardKind.LAND_SLOT, "배치 발판", footprint: cells);
            var unknown = new BattleHandCardDisplayData("reuse", eBattleHandCardKind.LAND_SLOT, "배치 영역 확장",
                footprint: cells, showFootprint: false);
            hand.SetItems(new[] { expansion });
            Need(hand.Slots.Count == 1, "검증용 확장 카드 생성 실패");
            UIBattleCardHandSlot slot = hand.Slots[0];
            var card = slot.GetComponentInChildren<UIBattlePreparationCardView>(true);
            Need(card != null, "재사용 카드 View 누락");
            AssertVisual(card, sprite, false, "확장 이미지 최초 표시");
            hand.SetItems(new[] { block });
            Need(hand.Slots[0] == slot, "동일 카드 ID에서 인스턴스 재사용 안 됨");
            AssertVisual(card, null, true, "일반 발판으로 재사용");
            var cellBindings = new SerializedObject(card).FindProperty("_footprintCells");
            int visibleCells = 0;
            for (int i = 0; i < cellBindings.arraySize; i++)
            {
                var cell = cellBindings.GetArrayElementAtIndex(i).objectReferenceValue as Image;
                if (cell != null && cell.gameObject.activeSelf) visibleCells++;
            }
            Need(visibleCells == cells.Length, "발판 격자의 실제 점유칸이 복구되지 않음");
            hand.SetItems(new[] { expansion });
            AssertVisual(card, sprite, false, "확장 이미지로 복귀");
            hand.SetItems(new[] { unknown });
            AssertVisual(card, null, true, "미등록 확장에 실제 격자 대체 표시");

            ValidateStarStats(root);
            return "카드 표시 API 검증 완료 (" + (Application.isPlaying ? "Play Mode" : "Edit Mode 임시 Preview Scene") +
                ")\n- 동일 슬롯 확장 이미지 → 발판 격자 → 확장 이미지 → 미등록 확장 격자 정상" +
                "\n- 1·2·3성 공격력/체력·방어율, 미등록 유닛 빈값 정상. 원본 전투 데이터 불변.";
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static void ValidateStarStats(GameObject root)
    {
        var config = AssetDatabase.FindAssets("t:InGamePrototypeConfigSO")
            .Select(guid => AssetDatabase.LoadAssetAtPath<InGamePrototypeConfigSO>(AssetDatabase.GUIDToAssetPath(guid)))
            .FirstOrDefault(candidate => candidate != null && candidate.DemonArmyCatalog != null);
        Need(config != null, "마왕군이 연결된 InGame 설정 없음");
        var bootstrap = root.AddComponent<InGamePrototypeBootstrap>();
        var adapter = root.AddComponent<UIGridStorageHandAdapter>();
        SetField(bootstrap, "_config", config);
        SetField(adapter, "_bootstrap", bootstrap);
        MethodInfo method = typeof(UIGridStorageHandAdapter).GetMethod("GetUnitStatTexts", PRIVATE, null,
            new[] { typeof(string), typeof(int), typeof(string).MakeByRefType(), typeof(string).MakeByRefType(), typeof(string).MakeByRefType() }, null);
        Need(method != null, "성급 스탯 표시 경로를 찾을 수 없음");
        UnitStatData original = config.DemonArmyCatalog.FindPrefab("M_WAR_01", 1).statData;
        string snapshot = EditorJsonUtility.ToJson(original);
        AssertStats(method, adapter, "M_WAR_01", 1, "18", "10%", "140");
        AssertStats(method, adapter, "M_WAR_01", 2, "30.6", "10%", "238");
        AssertStats(method, adapter, "M_WAR_01", 3, "57.6", "10%", "448");
        AssertStats(method, adapter, "M_SHD_01", 3, "35.2", "35%", "1024");
        AssertStats(method, adapter, "__unknown_unit__", 3, "", "", "");
        Need(EditorJsonUtility.ToJson(original) == snapshot, "스탯 표시가 원본 UnitStatData를 변경함");
    }

    private static void AssertStats(MethodInfo method, object adapter, string id, int star, string attack, string defense, string health)
    {
        object[] args = { id, star, null, null, null };
        method.Invoke(adapter, args);
        Need((string)args[2] == attack && (string)args[3] == defense && (string)args[4] == health,
            id + " " + star + "성: 기대 " + attack + "/" + defense + "/" + health + " 실제 " + args[2] + "/" + args[3] + "/" + args[4]);
    }

    private static void AssertVisual(UIBattlePreparationCardView card, Sprite sprite, bool gridVisible, string label)
    {
        Image image = card.transform.Find("Artwork").GetComponent<Image>();
        Need(image.sprite == sprite && image.enabled == (sprite != null), label + ": 이전 삽화 잔존 또는 삽화 활성 상태");
        Need(card.transform.Find("FootprintGrid").gameObject.activeSelf == gridVisible, label + ": 격자 활성 상태");
    }

    private static void CheckTextFits(TMP_Text text, string sample, string label, int maximumLines, List<string> report)
    {
        Need(text != null && text.font != null, label + ": TMP/Font 없음");
        string previous = text.text;
        try
        {
            text.text = sample;
            text.ForceMeshUpdate(true, true);
            Need(text.textInfo.characterCount > 0 && text.textInfo.lineCount > 0, label + ": TMP 메시가 생성되지 않음");
            Need(text.fontSize > 0 && (!text.enableAutoSizing || text.fontSize >= text.fontSizeMin - 0.01f),
                label + ": 최소 글자 크기 미달");
            Need(!text.isTextOverflowing && !text.isTextTruncated, label + ": TMP 문구 잘림");
            Need(text.textInfo.lineCount <= maximumLines,
                label + ": 자동 줄바꿈 후 " + text.textInfo.lineCount + "줄 (최대 " + maximumLines + "줄)");
            foreach (char character in sample)
                if (!char.IsWhiteSpace(character)) Need(text.font.HasCharacter(character, false, true), label + ": 폰트 문자 누락 " + character);
            report?.Add(label + ": " + text.textInfo.lineCount + "줄 / " + text.fontSize.ToString("0.#") + "pt SDF");
        }
        finally { text.text = previous; }
    }

    private static RectTransform ChildRect(Transform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text FindText(GameObject root, string path) => root.transform.Find(path)?.GetComponent<TMP_Text>();
    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, PRIVATE);
        Need(field != null, target.GetType().Name + ": 필드 없음 " + name);
        field.SetValue(target, value);
    }
    private static void NeedReference(SerializedObject target, string name) =>
        Need(target.FindProperty(name)?.objectReferenceValue != null, target.targetObject.name + ": 참조 없음 " + name);
    private static T Require<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Need(asset != null, "필수 에셋 없음: " + path);
        return asset;
    }
    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
