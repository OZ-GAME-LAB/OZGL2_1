using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 격리한 임시 카드만 변경한다. 실제 화면 검사는 진행도, 팝업 상태, 에셋과 Scene을 변경하지 않는다.
public static class LobbyAchievementProgressValidation
{
    private const string CATALOG_PATH = "Assets/06.UI/LobbyMutedPreview/Collections_v1/AchievementCatalog.asset";
    private const string SCENE_PATH = "Assets/00.Scenes/UI_Flow/UI_Lobby_MutedPreview.unity";
    private static readonly MethodInfo _populateMesh = typeof(UIAchievementProgressDividers)
        .GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);

    [MenuItem("Tools/OZGL2/Lobby/Validate Achievement Progress")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        var source = AssetDatabase.LoadAssetAtPath<UIAchievementCatalogSO>(CATALOG_PATH);
        if (source == null) throw new InvalidOperationException("업적 카탈로그가 필요합니다: " + CATALOG_PATH);
        string sourceJson = EditorJsonUtility.ToJson(source);
        bool sourceDirty = EditorUtility.IsDirty(source);
        Scene activeScene = SceneManager.GetActiveScene();
        bool sceneDirty = activeScene.isDirty;
        Scene preview = EditorSceneManager.NewPreviewScene();
        UIAchievementCatalogSO catalog = null;
        GameObject root = null;
        Mesh mesh = null;
        Texture2D tickTexture = null;
        Sprite tickSprite = null;
        var checks = new List<string>();
        Action<bool, string> check = CreateCheck(checks);
        try
        {
            catalog = Object.Instantiate(source);
            catalog.hideFlags = HideFlags.HideAndDontSave;
            root = new GameObject("AchievementProgressValidation", typeof(RectTransform));
            root.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(root, preview);
            var card = root.AddComponent<UIAchievementCardView>();
            var fill = CreateChild<Image>("Fill", root.transform);
            var dividers = CreateChild<UIAchievementProgressDividers>("Dividers", root.transform);
            check(fill.TryGetComponent<CanvasRenderer>(out _) && dividers.TryGetComponent<CanvasRenderer>(out _), "임시 채움/구분선 CanvasRenderer 필수 연결");
            Write(card, "_progressFill", fill);
            Write(card, "_progressDividers", dividers);
            dividers.rectTransform.sizeDelta = new Vector2(288f, 20f);
            dividers.color = new Color32(25, 34, 45, 210);
            mesh = new Mesh { name = "AchievementProgressValidationMesh", hideFlags = HideFlags.HideAndDontSave };

            // 기대 칸 수를 명시해 나머지/0/음수/정수 최대값에서 올림 계산과 overflow를 함께 검사한다.
            int[,] cases =
            {
                { 1, 1, 1 }, { 3, 1, 3 }, { 5, 1, 5 }, { 10, 1, 10 },
                { 100, 10, 10 }, { 100, 25, 4 }, { 500, 50, 10 }, { 95, 10, 10 },
                { 5, 9, 1 }, { 0, 0, 1 }, { -5, -10, 1 }, { 5, 0, 5 },
                { int.MaxValue, 1, int.MaxValue }, { int.MaxValue, 2, 1073741824 },
                { int.MaxValue, int.MaxValue, 1 }
            };
            for (int index = 0; index < cases.GetLength(0); index++)
            {
                UIAchievementCatalogSO.Entry entry = SetEntry(catalog, cases[index, 0], cases[index, 1]);
                string label = cases[index, 0] + "/칸당 " + cases[index, 1];
                check(entry.Target >= 1 && entry.ProgressPerSegment >= 1 && entry.SegmentCount == cases[index, 2], label + " SO 안전한 칸 수");
                dividers.raycastTarget = true;
                card.Bind(entry, 3);
                check(dividers.Target == entry.Target && dividers.ProgressPerSegment == entry.ProgressPerSegment &&
                    dividers.SegmentCount == cases[index, 2], label + " 카드에서 경계 설정 전달");
                check(card.CurrentProgress == Mathf.Min(3, entry.Target) && Mathf.Approximately(fill.fillAmount, (float)card.CurrentProgress / entry.Target), label + " 실제 횟수 비율 채움");
                check(dividers.DividerCount == cases[index, 2] - 1 && !dividers.raycastTarget && dividers.maskable, label + " 구분선 수/입력/마스크");
                ValidateMesh(dividers, mesh, label, check);
            }

            var partial = SetEntry(catalog, 95, 10);
            card.Bind(partial, 73);
            check(Mathf.Approximately(fill.fillAmount, 73f / 95f) && Mathf.Approximately(dividers.GetDividerRatio(8), 90f / 95f), "95회 목표의 마지막 5회 칸과 73회 채움 유지");
            check(dividers.GetDividerRatio(-1) == 0f && dividers.GetDividerRatio(9) == 0f, "범위를 벗어난 경계 조회 안전");
            card.Bind(partial, -10);
            check(card.CurrentProgress == 0 && fill.fillAmount == 0f && !card.IsCompleted, "음수 진행도 표시 0 제한");
            card.Bind(partial, int.MaxValue);
            check(card.CurrentProgress == 95 && fill.fillAmount == 1f && card.IsCompleted, "초과 진행도 표시 목표 제한");

            card.Bind(SetEntry(catalog, 5, 1), 3);
            check(dividers.DividerCount == 4 && Mathf.Approximately(fill.fillAmount, 0.6f), "3/5는 4개 경계와 60% 채움");
            Populate(dividers, mesh);
            float firstCenter = (mesh.vertices[0].x + mesh.vertices[2].x) * 0.5f;
            dividers.rectTransform.sizeDelta = new Vector2(576f, 32f);
            Populate(dividers, mesh);
            float resizedCenter = (mesh.vertices[0].x + mesh.vertices[2].x) * 0.5f;
            check(Mathf.Approximately(resizedCenter, firstCenter * 2f) && Mathf.Approximately(mesh.bounds.size.y, 32f), "RectTransform 크기 변경 시 경계 위치/높이 갱신");

            dividers.rectTransform.sizeDelta = new Vector2(10f, 20f);
            dividers.Configure(100, 1);
            Populate(dividers, mesh);
            Vector3[] narrowVertices = mesh.vertices;
            bool noOverlap = true;
            for (int index = 0; index < dividers.RenderedDividerCount - 1; index++)
                noOverlap &= narrowVertices[index * 4 + 2].x < narrowVertices[(index + 1) * 4].x;
            check(noOverlap && narrowVertices[2].x > narrowVertices[0].x, "좁은 막대는 양수 선폭을 유지하며 구분선 겹침 방지");

            dividers.rectTransform.sizeDelta = new Vector2(288f, 20f);
            dividers.Configure(int.MaxValue, 1);
            Populate(dividers, mesh);
            check(dividers.SegmentCount == int.MaxValue && dividers.RenderedDividerCount == 4096 && mesh.vertexCount == 16384, "정수 최대 목표: 논리 칸 유지/렌더 예산/정점 한도 보호");
            Write(dividers, "_renderDividerLimit", 3);
            dividers.Configure(100, 10);
            Populate(dividers, mesh);
            Vector3[] limited = mesh.vertices;
            float[] expectedRatios = { 0.1f, 0.5f, 0.9f };
            bool actualBoundaries = dividers.DividerCount == 9 && dividers.RenderedDividerCount == 3;
            for (int index = 0; index < expectedRatios.Length; index++)
                actualBoundaries &= Mathf.Abs((limited[index * 4].x + limited[index * 4 + 2].x) * 0.5f - (-144f + 288f * expectedRatios[index])) < 0.001f;
            check(actualBoundaries, "렌더 예산 3개는 실제 10/50/90회 경계 선택");

            // 선택적인 눈금 스프라이트는 경계 계산을 바꾸지 않고 UV/높이/완료 대비만 변경한다.
            tickTexture = new Texture2D(8, 16) { hideFlags = HideFlags.HideAndDontSave };
            tickSprite = Sprite.Create(tickTexture, new Rect(2f, 4f, 4f, 8f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            tickSprite.hideFlags = HideFlags.HideAndDontSave;
            Write(dividers, "_tickSprite", tickSprite);
            Write(dividers, "_heightRatio", 0.3f);
            Write(dividers, "_bottomInset", 1f);
            Write(dividers, "_completedTint", new Color(0.2f, 0.2f, 0.2f, 1f));
            Write(dividers, "_renderDividerLimit", 4096);
            card.Bind(SetEntry(catalog, 5, 1), 3);
            Populate(dividers, mesh);
            Vector4 outerUv = UnityEngine.Sprites.DataUtility.GetOuterUV(tickSprite);
            check(dividers.TickSprite == tickSprite && dividers.mainTexture == tickTexture, "별도 눈금 스프라이트 텍스처 연결");
            check(mesh.vertexCount == 16 && Mathf.Approximately(fill.fillAmount, 0.6f), "눈금 스프라이트 적용 후 3/5 경계/채움 유지");
            check(mesh.uv[0] == new Vector2(outerUv.x, outerUv.y) && mesh.uv[2] == new Vector2(outerUv.z, outerUv.w), "눈금 스프라이트 영역 UV 적용");
            check(Mathf.Approximately(mesh.bounds.min.y, -9f) && Mathf.Approximately(mesh.bounds.size.y, 6f), "하단 눈금 높이 비율/여백 적용");
            check(mesh.colors32[0].Equals((Color32)dividers.color), "진행 중 눈금 기본색 유지");
            card.Bind(catalog.Entries[0], 5);
            Populate(dividers, mesh);
            check(mesh.colors32[0].Equals((Color32)(dividers.color * new Color(0.2f, 0.2f, 0.2f, 1f))), "완료 시 눈금 대비 틴트 적용");
            card.Bind(catalog.Entries[0], 3);
            Populate(dividers, mesh);
            check(mesh.colors32[0].Equals((Color32)dividers.color), "진행 상태 복귀 시 눈금 색상 복원");
            Write(dividers, "_heightRatio", 0f);
            Populate(dividers, mesh);
            check(mesh.vertexCount == 0, "눈금 높이 0은 메시 숨김");
            Write(dividers, "_heightRatio", 1f);
            Write(dividers, "_bottomInset", 0f);
            Write(dividers, "_tickSprite", null);
            Populate(dividers, mesh);
            check(dividers.mainTexture != tickTexture && Mathf.Approximately(mesh.bounds.size.y, 20f), "스프라이트 제거 시 기존 단색 구분선 복원");

            card.Bind(null, 3);
            Populate(dividers, mesh);
            check(dividers.SegmentCount == 0 && dividers.DividerCount == 0 && mesh.vertexCount == 0 && fill.fillAmount == 0f, "무효 항목 바인딩은 이전 구분선/채움 제거");
            dividers.Clear();
            Populate(dividers, mesh);
            check(mesh.vertexCount == 0, "반복 Clear 안전");
            dividers.Configure(0, -1);
            check(dividers.Target == 1 && dividers.ProgressPerSegment == 1 && dividers.DividerCount == 0, "직접 Configure의 0/음수 입력 제한");
        }
        finally
        {
            if (mesh != null) Object.DestroyImmediate(mesh);
            if (tickSprite != null) Object.DestroyImmediate(tickSprite);
            if (tickTexture != null) Object.DestroyImmediate(tickTexture);
            if (root != null) Object.DestroyImmediate(root);
            if (catalog != null) Object.DestroyImmediate(catalog);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
        check(EditorJsonUtility.ToJson(source) == sourceJson && EditorUtility.IsDirty(source) == sourceDirty, "원본 카탈로그 값/저장 상태 불변");
        check(SceneManager.GetActiveScene() == activeScene && activeScene.isDirty == sceneDirty, "활성 Scene과 기존 미저장 상태 불변");
        return checks.Count + " achievement progress checks passed\n" + string.Join("\n", checks);
    }

    // 실제 업적 화면을 열고 렌더한 뒤 호출한다. 화면 갱신, 상태 초기화, 팝업 전환은 호출하지 않는다.
    public static string RunLive()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!Application.isPlaying || scene.path != SCENE_PATH)
            throw new InvalidOperationException("UI_Lobby_MutedPreview Play Mode에서 업적 화면을 연 뒤 실행하세요.");
        UIAchievementView[] views = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UIAchievementView>(true)).ToArray();
        if (views.Length != 1 || !views[0].isActiveAndEnabled)
            throw new InvalidOperationException("업적 화면 하나가 활성 상태여야 합니다.");
        UIAchievementView view = views[0];
        UIAchievementCatalogSO catalog = view.Catalog;
        UILobbyCollectionState state = Read<UILobbyCollectionState>(view, "_state");
        if (catalog == null || state == null || !Read<bool>(state, "_hasInitialized"))
            throw new InvalidOperationException("카탈로그와 초기화된 Runtime 상태가 필요합니다. 검증 도구는 상태를 초기화하지 않습니다.");
        string catalogJson = EditorJsonUtility.ToJson(catalog);
        string stateJson = EditorJsonUtility.ToJson(state);
        bool sceneDirty = scene.isDirty;
        var checks = new List<string>();
        Action<bool, string> check = CreateCheck(checks);
        check(catalog.Entries.Count == 9 && view.TotalCount == 9 && view.CardCount >= 9, "실제 카탈로그와 업적 카드 9개");
        check(Enumerable.Range(0, view.CardCount).Count(index => view.GetCard(index) != null && view.GetCard(index).gameObject.activeInHierarchy) == 9, "활성 업적 카드 정확히 9개");
        check(Read<RectTransform>(view, "_content") != null && Read<UIAchievementCardView>(view, "_cardTemplate") != null, "목록/카드 템플릿 연결");
        check(!Read<UIAchievementCardView>(view, "_cardTemplate").gameObject.activeSelf, "카드 템플릿 비활성 유지");
        int completed = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < catalog.Entries.Count; index++)
        {
            UIAchievementCatalogSO.Entry entry = catalog.Entries[index];
            UIAchievementCardView card = view.GetCard(index);
            check(entry != null && !string.IsNullOrWhiteSpace(entry.Id) && ids.Add(entry.Id), "업적 " + index + " ID 유효/고유");
            check(card != null && card.gameObject.activeInHierarchy && card.EntryId == entry.Id, entry.Id + " 활성 카드 연결");
            int progress = Mathf.Clamp(state.GetAchievementProgress(entry.Id), 0, entry.Target);
            Image fill = Read<Image>(card, "_progressFill");
            UIAchievementProgressDividers dividers = Read<UIAchievementProgressDividers>(card, "_progressDividers");
            check(fill != null && dividers != null && dividers.isActiveAndEnabled, entry.Id + " 채움/구분선 연결");
            check(fill.TryGetComponent<CanvasRenderer>(out _) && dividers.TryGetComponent<CanvasRenderer>(out _), entry.Id + " 채움/구분선 CanvasRenderer 연결");
            check(dividers.Target == entry.Target && dividers.ProgressPerSegment == entry.ProgressPerSegment &&
                dividers.SegmentCount == entry.SegmentCount && dividers.DividerCount == entry.SegmentCount - 1, entry.Id + " 카탈로그 목표/칸당 횟수/구분선 수 일치");
            check(fill.type == Image.Type.Filled && fill.fillMethod == Image.FillMethod.Horizontal && fill.fillOrigin == (int)Image.OriginHorizontal.Left &&
                Mathf.Approximately(fill.fillAmount, (float)progress / entry.Target), entry.Id + " 실제 진행도 가로 채움");
            check(card.CurrentProgress == progress && card.Target == entry.Target && card.IsCompleted == (progress >= entry.Target), entry.Id + " 진행/달성 상태 일치");
            check(!fill.raycastTarget && !dividers.raycastTarget && dividers.maskable, entry.Id + " 장식 입력 차단/마스크 지원");
            Image rail = card.transform.Find("ProgressRail").GetComponent<Image>();
            check(rail.sprite != null && fill.sprite != null && dividers.TickSprite != null &&
                rail.sprite != fill.sprite && fill.sprite != dividers.TickSprite, entry.Id + " Gothic Metal 프레임/Fill/눈금 독립 리소스 연결");
            check(rail.transform.GetSiblingIndex() > fill.transform.GetSiblingIndex() && !rail.raycastTarget &&
                Read<float>(dividers, "_heightRatio") < 1f, entry.Id + " 프레임 전면/하단 짧은 눈금 설정");
            check(Read<Image>(card, "_icon") != null && Read<TMP_Text>(card, "_nameText") != null &&
                Read<TMP_Text>(card, "_descriptionText") != null && Read<TMP_Text>(card, "_completedText") != null, entry.Id + " 아이콘/문구 참조 연결");
            TMP_Text progressText = Read<TMP_Text>(card, "_progressText");
            check(progressText != null && progressText.text == progress + " / " + entry.Target, entry.Id + " 진행 문구 일치");
            check(card.GetComponentsInChildren<Transform>(true).Where(child => child.name.StartsWith("Segment_", StringComparison.Ordinal)).All(child => !child.gameObject.activeSelf), entry.Id + " 기존 고정 구분선 비활성");
            if (card.IsCompleted) completed++;
        }
        TMP_Text summary = Read<TMP_Text>(view, "_summaryText");
        check(view.CompletedCount == completed && summary != null && summary.text == "달성 " + completed + " / 9", "전체 달성 카운터 동기화");
        check(EditorJsonUtility.ToJson(catalog) == catalogJson && EditorJsonUtility.ToJson(state) == stateJson && scene.isDirty == sceneDirty, "실제 카탈로그/초기 상태/Scene 저장 상태 불변");
        return checks.Count + " live achievement progress checks passed\n" + string.Join("\n", checks);
    }

    private static void ValidateMesh(UIAchievementProgressDividers dividers, Mesh mesh, string label, Action<bool, string> check)
    {
        Populate(dividers, mesh);
        check(mesh.vertexCount == dividers.RenderedDividerCount * 4 && mesh.triangles.Length == dividers.RenderedDividerCount * 6, label + " 단일 메시 정점/삼각형 수");
        Vector3[] vertices = mesh.vertices;
        Color32[] colors = mesh.colors32;
        Rect rect = dividers.rectTransform.rect;
        Color32 expectedColor = dividers.color;
        bool valid = true;
        for (int index = 0; index < vertices.Length; index++)
            valid &= !float.IsNaN(vertices[index].x) && !float.IsInfinity(vertices[index].x) &&
                vertices[index].x >= rect.xMin - 0.001f && vertices[index].x <= rect.xMax + 0.001f &&
                Mathf.Abs(Mathf.Abs(vertices[index].y) - rect.height * 0.5f) < 0.001f && colors[index].Equals(expectedColor);
        check(valid, label + " 메시 영역/유한 좌표/설정 색상");
        if (dividers.DividerCount == 0 || dividers.DividerCount > 500) return;
        bool ratiosMatch = true;
        for (int index = 0; index < dividers.DividerCount; index++)
        {
            float ratio = (index + 1f) * dividers.ProgressPerSegment / dividers.Target;
            float center = (vertices[index * 4].x + vertices[index * 4 + 2].x) * 0.5f;
            ratiosMatch &= Mathf.Approximately(dividers.GetDividerRatio(index), ratio) &&
                Mathf.Abs(center - (rect.xMin + rect.width * ratio)) < 0.001f;
        }
        check(ratiosMatch, label + " 실제 횟수 경계 비율/메시 위치");
    }

    private static void Populate(UIAchievementProgressDividers dividers, Mesh mesh)
    {
        if (_populateMesh == null) throw new MissingMethodException("구분선 메시 생성 메서드가 없습니다.");
        using (var helper = new VertexHelper())
        {
            _populateMesh.Invoke(dividers, new object[] { helper });
            mesh.Clear();
            helper.FillMesh(mesh);
        }
    }

    private static UIAchievementCatalogSO.Entry SetEntry(UIAchievementCatalogSO catalog, int target, int progressPerSegment)
    {
        using (var serialized = new SerializedObject(catalog))
        {
            SerializedProperty entries = serialized.FindProperty("_entries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_id").stringValue = "validation_only";
            entry.FindPropertyRelative("_target").intValue = target;
            entry.FindPropertyRelative("_progressPerSegment").intValue = progressPerSegment;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        return catalog.Entries[0];
    }

    private static T CreateChild<T>(string name, Transform parent) where T : Component
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        child.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
        child.transform.SetParent(parent, false);
        return child.AddComponent<T>();
    }

    private static Action<bool, string> CreateCheck(List<string> checks) => (ok, reason) =>
    {
        if (!ok) throw new InvalidOperationException("업적 진행 검사 실패 (" + checks.Count + "개 통과): " + reason);
        checks.Add(reason);
    };

    private static T Read<T>(object target, string field) => (T)FindField(target, field).GetValue(target);
    private static void Write(object target, string field, object value) => FindField(target, field).SetValue(target, value);
    private static FieldInfo FindField(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(target.GetType().Name, field);
}
