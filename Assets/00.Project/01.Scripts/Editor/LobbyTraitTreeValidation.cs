using System;
using System.Linq;
using System.Text;
using OZGL2.Progression;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// PlayerPrefs를 쓰지 않는 실제 모델로 검증한다. 테스트 LP는 플레이어 계정에 지급되지 않는다.
internal static class LobbyTraitTreeValidation
{
    [MenuItem("Tools/OZGL2/Lobby/Validate Trait Progression Rules")]
    public static void RunFromMenu() => Debug.Log(ValidateModels());

    public static string ValidateModels()
    {
        var definitions = Resources.LoadAll<TraitData>("Traits").OrderBy(d => (int)d.id).ToArray();
        string before = Snapshot(definitions);
        var tree = new TraitTree(definitions, false);
        var account = new MawangLevel(false);
        int assertions = 0;
        Action<bool, string> check = (condition, message) =>
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("특성 검증 실패: " + message);
        };
        check(definitions.Length == 40, "특성 40개");
        foreach (var d in definitions)
        {
            check(!tree.CanUnrank(d.id), d.id + " 최저 레벨 버튼 제한");
            check(!tree.TryUnrank(d.id, account), d.id + " 최저 레벨 요청 거절");
            check(!tree.TryRank(d.id, account), d.id + " LP 부족 요청 거절");
        }
        const int BUDGET = 10000;
        account.Refund(BUDGET);
        foreach (var d in definitions)
        {
            bool root = d.parents == null || d.parents.Length == 0;
            check(tree.IsOpen(d.id) == root, d.id + " 초기 해금");
            if (!root) check(!tree.TryRank(d.id, account), d.id + " 잠긴 특성 요청 거절");
        }
        foreach (var d in definitions)
        {
            check(tree.IsOpen(d.id), d.id + " 부모 만렙 후 해금");
            for (int rank = 0; rank < d.maxRank; rank++)
            {
                int lp = account.Points;
                check(tree.NextCost(d.id) == 1, d.id + " 모든 단계 비용 1 LP");
                check(tree.TryRank(d.id, account), d.id + " 레벨업");
                check(tree.RefundCost(d.id) == 1, d.id + " 모든 단계 환급 1 LP");
                check(tree.RankOf(d.id) == rank + 1, d.id + " 랭크 일치");
                check(account.Points == lp - d.CostForRank(rank), d.id + " 비용 차감");
                foreach (var child in definitions.Where(c => c.parents != null && c.parents.Contains(d.id)))
                    if (rank + 1 < d.maxRank) check(!tree.IsOpen(child.id), child.id + " 선행 만렙 이전 잠금");
            }
            check(!tree.CanRank(d.id, account.Points), d.id + " 상한 버튼 제한");
            check(!tree.TryRank(d.id, account), d.id + " 상한 요청 거절");
            if (d.parents == null) continue;
            foreach (var parent in d.parents)
            {
                int lp = account.Points;
                int rank = tree.RankOf(parent);
                check(!tree.CanUnrank(parent), parent + " 후속 습득 시 다운 제한");
                check(!tree.TryUnrank(parent, account), parent + " 후속 습득 시 다운 거절");
                check(account.Points == lp && tree.RankOf(parent) == rank, "거절 시 데이터 유지");
            }
        }
        foreach (var d in definitions.Reverse())
        {
            for (int rank = d.maxRank; rank > 0; rank--)
            {
                int lp = account.Points;
                check(tree.TryUnrank(d.id, account), d.id + " 레벨 다운");
                check(tree.RankOf(d.id) == rank - 1, d.id + " 다운 랭크");
                check(account.Points == lp + d.CostForRank(rank - 1), d.id + " 단계별 전액 환급");
            }
            check(!tree.TryUnrank(d.id, account), d.id + " 음수 레벨 금지");
        }
        check(account.Points == BUDGET, "전체 환급 보존");
        check(!tree.TryResetAndRefund(account), "배분 없음: 초기화/중복 환급 거절");
        foreach (var d in definitions)
            for (int rank = 0; rank < d.maxRank; rank++) tree.TryRank(d.id, account);
        int allocated = definitions.Sum(d => d.maxRank);
        check(tree.AllocatedPoints == allocated && account.Points == BUDGET - allocated, "특화 포함 투자 합계 1 LP 기준");
        check(!tree.TryResetAndRefund(null) && tree.AllocatedPoints == allocated, "계정 없음: 초기화 거절");
        int beforeLevel = account.Level, beforeXp = account.Xp;
        check(tree.TryResetAndRefund(account), "전체 초기화 성공");
        check(tree.AllocatedPoints == 0 && definitions.All(d => tree.RankOf(d.id) == 0), "초기화 후 모든 랭크 0");
        check(account.Points == BUDGET && account.Level == beforeLevel && account.Xp == beforeXp, "전체 환급, 레벨/경험치 유지");
        check(!tree.TryResetAndRefund(account) && account.Points == BUDGET, "연속 초기화 중복 환급 없음");
        check(definitions.All(d => tree.IsOpen(d.id) == (d.parents == null || d.parents.Length == 0)), "초기화 후 선행 잠금 복구");
        check(tree.TryRank(TraitId.Mon_A1, account) && tree.AllocatedPoints == 1, "초기화 후 재투자");
        check(tree.TryResetAndRefund(account) && account.Points == BUDGET, "재투자 초기화 환급");
        check(Snapshot(definitions) == before, "실제 계정 저장값 불변");
        return "Trait progression: " + assertions + " assertions passed / 40 traits / account unchanged";
    }

    private static string Snapshot(TraitData[] definitions)
    {
        var snapshot = new StringBuilder();
        foreach (string key in new[] { "OZGL2.Mawang.Level", "OZGL2.Mawang.Xp", "OZGL2.Mawang.LP" }
                     .Concat(definitions.Select(d => "OZGL2.Trait." + d.id)))
            snapshot.Append(key).Append(':').Append(PlayerPrefs.HasKey(key)).Append(':').Append(PlayerPrefs.GetInt(key, -1)).Append(';');
        return snapshot.ToString();
    }

    [MenuItem("Tools/OZGL2/Lobby/Validate Trait Effect Preview Text")]
    public static void RunEffectPreviewFromMenu() => Debug.Log(ValidateEffectPreviews());

    public static string ValidateEffectPreviews()
    {
        var definitions = Resources.LoadAll<TraitData>("Traits").OrderBy(d => (int)d.id).ToArray();
        string saved = Snapshot(definitions);
        int assertions = 0;
        Action<bool, string> check = (condition, reason) =>
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("특성 효과 표시 검증 실패: " + reason);
        };
        foreach (var d in definitions)
        {
            var isolated = ScriptableObject.CreateInstance<TraitData>();
            try
            {
                // 선행 조건은 이 테스트 대상이 아니다. 단일 효과 누적값을 실 모델과 비교한다.
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(d), isolated);
                isolated.parents = Array.Empty<TraitId>();
                var tree = new TraitTree(new[] { isolated }, false);
                var account = new MawangLevel(false);
                account.Refund(100);
                for (int rank = 0; rank <= d.maxRank; rank++)
                {
                    if (rank > 0) check(tree.TryRank(d.id, account), d.id + " 단일 효과 레벨업");
                    var preview = TraitTree.PreviewModifiers(d, rank);
                    var actual = tree.BuildModifiers();
                    foreach (var field in typeof(TraitModifiers).GetFields())
                        check(field.GetValue(preview).Equals(field.GetValue(actual)), d.id + "/" + rank + "/" + field.Name);
                    string text = UITraitEffectTextFormatter.Format(d, rank);
                    check(!string.IsNullOrWhiteSpace(text) && !text.Contains("효과 정보 없음") && !text.Contains("/랭크") && !text.Contains("%p"), d.id + " 실제 누적 효과 문구");
                    if (rank == 0) check(text == "적용된 효과 없음", d.id + " 미투자 효과");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(isolated); }
        }
        var defense = definitions.Single(d => d.id == TraitId.Hero_B1);
        check(UITraitEffectTextFormatter.Format(defense, 1) == "용사 방어력 -2%", "요청 예시 현재 1p");
        check(UITraitEffectTextFormatter.Format(defense, 2) == "용사 방어력 -4%", "요청 예시 다음 2p");
        check(UITraitEffectTextFormatter.Format(defense, 99) == UITraitEffectTextFormatter.Format(defense, defense.maxRank), "상한 초과 미리보기 제한");
        check(Snapshot(definitions) == saved, "실제 계정 저장값 불변");
        return "Trait effect previews: " + assertions + " assertions passed / all 40 traits, every rank, account unchanged";
    }

    [MenuItem("Tools/OZGL2/Lobby/Validate Trait UI In Play Mode")]
    public static void RunUIFromMenu() => Debug.Log(BeginUIValidation());

    private static IEnumerator<object> _uiRoutine;
    private static int _lastFrame;
    public static string UIResult { get; private set; }

    public static string BeginUIValidation()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Mode에서 실행하세요.");
        if (_uiRoutine != null) throw new InvalidOperationException("UI 검증 실행 중입니다.");
        UIResult = "Running";
        _lastFrame = -1;
        _uiRoutine = ValidateUI();
        EditorApplication.update += StepUI;
        return UIResult;
    }

    private static void StepUI()
    {
        if (Application.isPlaying && _lastFrame == Time.frameCount) return;
        _lastFrame = Time.frameCount;
        try
        {
            if (Application.isPlaying && _uiRoutine.MoveNext()) return;
            if (!Application.isPlaying) UIResult = "Cancelled: Play Mode ended";
        }
        catch (Exception e) { UIResult = e.ToString(); }
        _uiRoutine.Dispose();
        _uiRoutine = null;
        EditorApplication.update -= StepUI;
        Debug.Log(UIResult);
    }

    private static IEnumerator<object> ValidateUI()
    {
        var host = UnityEngine.Object.FindFirstObjectByType<UILobbyOverlayView>();
        if (host == null || EventSystem.current == null) throw new InvalidOperationException("로비/EventSystem 없음");
        host.OpenTraits();
        yield return null;
        var view = host.GetComponentInChildren<UITraitOverlayView>();
        var controller = view.GetComponent<UITraitProgressionController>();
        var originalTree = controller.Tree;
        var originalAccount = controller.Account;
        var originalScroll = view.GetComponentInChildren<ScrollRect>();
        Vector3 originalScale = originalScroll.content.localScale;
        string saved = Snapshot(originalTree.Defs.ToArray());
        int assertions = 0;
        Action<bool, string> check = (condition, reason) =>
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("특성 UI 검증 실패: " + reason);
        };
        try
        {
            var tree = new TraitTree(originalTree.Defs, false);
            var account = new MawangLevel(false);
            account.Refund(100);
            controller.Bind(tree, account);
            var nodes = view.GetComponentsInChildren<UITraitFrameView>(true).ToDictionary(n => n.Definition.id);
            var detail = view.transform.Find("SelectedTraitDetail");
            var upgrade = detail.Find("Upgrade").GetComponent<Button>();
            var downgrade = detail.Find("Downgrade").GetComponent<Button>();
            var scroll = view.GetComponentInChildren<ScrollRect>();
            var zoom = scroll.GetComponent<UITraitTreeZoom>();
            var treeMask = scroll.GetComponent<RectMask2D>();
            check(treeMask != null && treeMask.softness == new Vector2Int(48, 48), "트리 네 방향의 좁은 경계 페이드");
            foreach (string path in new[] { "Background", "SelectedTraitDetail", "Back", "ResetTraits", "Recenter" })
                check(!view.transform.Find(path).IsChildOf(scroll.transform), "트리 경계 페이드 범위에서 제외: " + path);
            foreach (var text in scroll.content.GetComponentsInChildren<TMP_Text>(true))
                check(text.maskable && text.fontSharedMaterial != null &&
                    text.fontSharedMaterial.shader.name == "OZGL2/UI/Trait Pixel TMP Outline",
                    "특성명과 글자 외곽선의 경계 페이드 지원: " + text.name);
            foreach (string path in new[] { "ResetTraits", "Recenter", "Back", "ResetConfirmation/Panel/Cancel", "ResetConfirmation/Panel/Confirm", "SelectedTraitDetail/Upgrade", "SelectedTraitDetail/Downgrade" })
            {
                var image = view.transform.Find(path).GetComponent<Image>();
                check(image.sprite != null && AssetDatabase.GetAssetPath(image.sprite).Contains("/DetailArt_v2/"), path + " 전용 새 Sprite");
                check(image.type == Image.Type.Simple && image.preserveAspect, path + " 이미지 늘리기 없음");
                check(Mathf.Abs(image.sprite.rect.width / image.sprite.rect.height - image.rectTransform.rect.width / image.rectTransform.rect.height) < 0.001f, path + " 버튼과 Sprite 비율 일치");
            }
            check(detail.Find("Divider").GetComponent<Image>().sprite != null, "현재/다음 효과 전용 구분선");
            var currentDescription = detail.Find("Description").GetComponent<TMP_Text>();
            var nextDescription = detail.Find("NextDescription").GetComponent<TMP_Text>();
            string[] headerOrder = { "Icon", "Type", "Name", "Level", "Description" };
            for (int i = 0; i < headerOrder.Length - 1; i++)
            {
                var upper = detail.Find(headerOrder[i]) as RectTransform;
                var lower = detail.Find(headerOrder[i + 1]) as RectTransform;
                check(upper.anchoredPosition.y - upper.rect.height * 0.5f > lower.anchoredPosition.y + lower.rect.height * 0.5f,
                    "설명창 상단 순서/겹침 없음: " + headerOrder[i] + " → " + headerOrder[i + 1]);
            }
            var headerIcon = detail.Find("Icon").GetComponent<Image>();
            check(headerIcon.rectTransform.sizeDelta == new Vector2(128, 128) && headerIcon.preserveAspect && !headerIcon.raycastTarget,
                "설명창 아이콘 영역 확대/비율/입력 유지");
            check(zoom != null && Mathf.Approximately(scroll.scrollSensitivity, 0), "휠 이동 대신 줌 연결");
            scroll.content.localScale = Vector3.one;
            foreach (var node in nodes.Values)
            {
                check(node.transform.Find("Rank") == null && node.transform.Find("RankShade") == null, "트리 레벨 문구 제거: " + node.name);
                var frame = node.transform.Find("Frame").GetComponent<Image>();
                check(node.transform.Find("MaxLevelBorder") == null, "기존 얇은 외곽선 제거: " + node.name);
                check(node.DefaultFrameSprite != null && node.MaxLevelFrameSprite != null && node.DefaultFrameSprite != node.MaxLevelFrameSprite, "기본/만렙 프레임 연결: " + node.name);
                Sprite iconBefore = node.Icon;
                Vector2 sizeBefore = frame.rectTransform.sizeDelta;
                node.ShowRank(node.Definition.maxRank, true);
                check(frame.sprite == node.MaxLevelFrameSprite, "최대 레벨 프레임 이미지 교체: " + node.name);
                var name = node.transform.Find("Name").GetComponent<TMP_Text>();
                check(name.color == node.MaxLevelNameColor, "만렙 계열색 이름: " + node.name);
                node.ShowRank(node.Definition.maxRank - 1, true);
                check(frame.sprite == node.DefaultFrameSprite, "레벨 다운 시 기본 프레임 복원: " + node.name);
                check(name.color == (Color)new Color32(248, 242, 235, 255), "중간 레벨 이름 원색 유지: " + node.name);
                node.ShowRank(0, false);
                check(name.color == (Color)new Color32(155, 150, 146, 255), "미해금 이름 어둡게: " + node.name);
                node.ShowRank(0, true);
                check(name.color == (Color)new Color32(248, 242, 235, 255), "해금된 0레벨 이름 원색 유지: " + node.name);
                check(node.Icon == iconBefore && frame.rectTransform.sizeDelta == sizeBefore, "아이콘/프레임 크기 유지: " + node.name);
                node.ShowRank(tree.RankOf(node.Definition.id), tree.IsOpen(node.Definition.id));
            }
            check(detail.Find("Close") == null, "닫기 버튼 제거");
            view.ResetView();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Click((RectTransform)nodes[TraitId.Eco_A1].transform);
            yield return null;
            check(view.SelectedFrame == nodes[TraitId.Eco_A1] && view.IsDetailOpen, "Raycast로 특성 선택");
            check(!downgrade.interactable && upgrade.interactable, "최저 레벨 버튼 상태");
            Click((RectTransform)detail);
            check(view.IsDetailOpen, "설명창 내부 클릭 유지");
            for (int i = 0; i < 3; i++) { Click((RectTransform)upgrade.transform); yield return null; }
            check(tree.RankOf(TraitId.Eco_A1) == 3 && account.Points == 97, "+ 단계마다 1 LP 차감");
            check(!upgrade.interactable && downgrade.interactable, "최대 레벨 버튼 상태");
            check(nodes[TraitId.Eco_A1].transform.Find("Frame").GetComponent<Image>().sprite == nodes[TraitId.Eco_A1].MaxLevelFrameSprite, "실제 + 클릭 후 만렙 프레임 교체");
            check(detail.Find("Level").GetComponent<TMP_Text>().text.Contains("3 / 3"), "설명창 현재 레벨 유지");
            Click((RectTransform)upgrade.transform);
            check(account.Points == 97 && tree.RankOf(TraitId.Eco_A1) == 3, "비활성 + 클릭 차단");
            Click((RectTransform)nodes[TraitId.Eco_A2].transform);
            yield return null;
            check(view.SelectedFrame == nodes[TraitId.Eco_A2] && view.IsDetailOpen, "다른 노드 직접 전환");
            Click((RectTransform)upgrade.transform);
            yield return null;
            check(tree.RankOf(TraitId.Eco_A2) == 1, "선행 만렙 후 후속 습득");
            Click((RectTransform)nodes[TraitId.Eco_A1].transform);
            yield return null;
            check(!downgrade.interactable, "후속 습득 시 선행 - 비활성");
            Click((RectTransform)downgrade.transform);
            check(tree.RankOf(TraitId.Eco_A1) == 3, "비활성 - 클릭 차단");
            Click((RectTransform)nodes[TraitId.Eco_A2].transform);
            yield return null;
            Click((RectTransform)downgrade.transform);
            yield return null;
            check(tree.RankOf(TraitId.Eco_A2) == 0 && account.Points == 97, "후속 다운 1 LP 환급");
            Click((RectTransform)nodes[TraitId.Eco_A1].transform);
            yield return null;
            for (int i = 0; i < 3; i++) { Click((RectTransform)downgrade.transform); yield return null; }
            check(tree.RankOf(TraitId.Eco_A1) == 0 && account.Points == 100, "원금 전액 환급");
            check(nodes[TraitId.Eco_A1].transform.Find("Frame").GetComponent<Image>().sprite == nodes[TraitId.Eco_A1].DefaultFrameSprite, "실제 - 클릭 후 기본 프레임 복원");
            Click(scroll.viewport, new Vector2(0, 70));
            check(!view.IsDetailOpen, "빈 영역 클릭 닫기");
            foreach (var node in nodes.Values)
            {
                view.ShowSelection(node);
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var label in detail.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    check(label.font != null && !label.isTextOverflowing, node.name + "/" + label.name + " 텍스트 영역");
                }
            }
            view.HideDetail();
            // 현재/다음 누적 수치와 긴 특화 설명을 모든 랭크에서 실제 TMP 영역으로 검사한다.
            var previewTree = new TraitTree(originalTree.Defs, false);
            var previewAccount = new MawangLevel(false);
            previewAccount.Refund(1000);
            controller.Bind(previewTree, previewAccount);
            foreach (var node in nodes.Values.OrderBy(n => (int)n.Definition.id))
            {
                view.ShowSelection(node);
                for (int rank = 0; rank <= node.Definition.maxRank; rank++)
                {
                    if (rank > 0) check(previewTree.TryRank(node.Definition.id, previewAccount), node.name + " 비교용 랭크 증가");
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    check(currentDescription.text == "(현재) " + rank + "p :\n" + UITraitEffectTextFormatter.Format(node.Definition, rank), node.name + " 현재 누적 효과");
                    string expectedNext = rank < node.Definition.maxRank
                        ? "(다음 레벨) " + (rank + 1) + "p :\n" + UITraitEffectTextFormatter.Format(node.Definition, rank + 1)
                        : "(다음 레벨)\n최대 레벨입니다.";
                    check(nextDescription.text == expectedNext, node.name + " 다음 누적 효과/만렙");
                    check(nextDescription.color == node.LockedNameColor, node.name + " 미해금 색상 일치");
                    foreach (var label in detail.GetComponentsInChildren<TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        check(!label.isTextOverflowing, node.name + "/" + rank + "/" + label.name + " 텍스트 넘침 없음");
                    }
                }
            }
            controller.Bind(tree, account);
            view.HideDetail();
            foreach (var delta in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                view.ResetView();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Vector2 start = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(Vector3.zero));
                var pointer = new PointerEventData(EventSystem.current) { position = start, button = PointerEventData.InputButton.Left };
                scroll.OnInitializePotentialDrag(pointer);
                scroll.OnBeginDrag(pointer);
                pointer.position = start + delta * 180;
                pointer.delta = delta * 180;
                pointer.dragging = true;
                scroll.OnDrag(pointer);
                scroll.OnEndDrag(pointer);
                check(Vector2.Dot(scroll.content.anchoredPosition, delta) > 50, "드래그 " + delta);
            }
            Click((RectTransform)view.transform.Find("Recenter"));
            check(scroll.content.anchoredPosition.sqrMagnitude < 0.01f && scroll.velocity.sqrMagnitude < 0.01f,
                "중앙으로 위치/관성 초기화");
            Vector2 cursorOffset = new Vector2(130, 20);
            Vector2 pointerScreen = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(cursorOffset));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(scroll.content, pointerScreen, null, out Vector2 focusedPoint);
            Wheel(scroll.viewport, cursorOffset, 1);
            yield return null;
            check(zoom.Zoom > 1, "휠 위로 확대");
            Vector2 focusedScreen = RectTransformUtility.WorldToScreenPoint(null, scroll.content.TransformPoint(focusedPoint));
            check(Vector2.Distance(pointerScreen, focusedScreen) < 1, "커서 지점 유지");
            Wheel(scroll.viewport, cursorOffset, -1);
            yield return null;
            check(Mathf.Abs(zoom.Zoom - 1) < 0.001f && scroll.content.anchoredPosition.magnitude < 1, "휠 역방향 축소: 수직 스크롤 없음");
            Click((RectTransform)nodes[TraitId.Eco_A1].transform);
            yield return null;
            float beforeDetailWheel = zoom.Zoom;
            Wheel((RectTransform)detail, Vector2.zero, 1);
            check(Mathf.Approximately(zoom.Zoom, beforeDetailWheel), "설명창 위 휠은 트리에 전달되지 않음");
            view.HideDetail();
            for (int i = 0; i < 20; i++) Wheel(scroll.viewport, Vector2.zero, -3);
            yield return null;
            check(Mathf.Abs(zoom.Zoom - zoom.MinZoom) < 0.001f, "최소 줌 제한");
            check(Mathf.Abs(scroll.content.anchoredPosition.x) < 1, "축소 시 작은 콘텐츠 가로 중앙 유지");
            foreach (var node in nodes.Values)
            {
                var rect = (RectTransform)node.transform;
                Vector3 low = scroll.viewport.InverseTransformPoint(rect.TransformPoint(new Vector3(0, -rect.rect.height * 0.5f - 48, 0)));
                Vector3 high = scroll.viewport.InverseTransformPoint(rect.TransformPoint(new Vector3(0, rect.rect.height * 0.5f + 15, 0)));
                check(low.y >= scroll.viewport.rect.yMin && high.y <= scroll.viewport.rect.yMax, "최소 줌에서 전체 트리 세로 범위: " + node.name);
            }
            for (int i = 0; i < 20; i++) Wheel(scroll.viewport, Vector2.zero, 3);
            yield return null;
            check(Mathf.Abs(zoom.Zoom - zoom.MaxZoom) < 0.001f, "최대 줌 제한");
            Click((RectTransform)view.transform.Find("Recenter"));
            check(scroll.content.anchoredPosition.magnitude < 1 && Mathf.Approximately(zoom.Zoom, zoom.MaxZoom), "중앙으로는 배율 유지");
            // 확대 상태에서 위쪽 경로는 뷰포트 밖으로 나가므로 현재 보이는 중앙 경로를 클릭한다.
            Click((RectTransform)nodes[TraitId.Eco_B1].transform);
            yield return null;
            check(view.SelectedFrame == nodes[TraitId.Eco_B1], "확대 후 특성 Raycast 선택");
            view.HideDetail();
            Vector2 dragStart = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(Vector3.zero));
            var zoomDrag = new PointerEventData(EventSystem.current) { position = dragStart, button = PointerEventData.InputButton.Left };
            scroll.OnInitializePotentialDrag(zoomDrag);
            scroll.OnBeginDrag(zoomDrag);
            zoomDrag.position += new Vector2(90, -90);
            zoomDrag.delta = new Vector2(90, -90);
            zoomDrag.dragging = true;
            scroll.OnDrag(zoomDrag);
            scroll.OnEndDrag(zoomDrag);
            check(scroll.content.anchoredPosition.x > 30 && scroll.content.anchoredPosition.y < -30, "확대 후 사선 드래그");
            scroll.content.localScale = Vector3.one;
            view.ResetView();
            var reset = view.transform.Find("ResetTraits").GetComponent<Button>();
            var centerRect = (RectTransform)view.transform.Find("Recenter");
            var resetRect = (RectTransform)reset.transform;
            check(Mathf.Approximately(centerRect.anchoredPosition.x, -resetRect.anchoredPosition.x) &&
                  Mathf.Approximately(centerRect.anchoredPosition.y, resetRect.anchoredPosition.y) && centerRect.sizeDelta == resetRect.sizeDelta,
                "초기화/중앙으로 하단 좌우 대칭");
            check(!reset.interactable, "배분 전 초기화 비활성");
            Click(resetRect);
            check(!view.IsResetConfirmationOpen, "비활성 초기화 클릭 차단");
            for (int i = 0; i < 3; i++) tree.TryRank(TraitId.Eco_A1, account);
            tree.TryRank(TraitId.Eco_A2, account);
            yield return null;
            check(reset.interactable, "투자 후 초기화 활성");
            Click(resetRect);
            yield return null;
            var confirmation = view.transform.Find("ResetConfirmation");
            var confirm = (RectTransform)confirmation.Find("Panel/Confirm");
            var cancel = (RectTransform)confirmation.Find("Panel/Cancel");
            check(view.IsResetConfirmationOpen && !view.IsDetailOpen, "초기화 확인창");
            check(confirmation.Find("Panel/Message").GetComponent<TMP_Text>().text.Contains("4 LP"), "실제 환급량 표시");
            foreach (var label in confirmation.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                check(label.font != null && !label.isTextOverflowing, "초기화 확인창 텍스트: " + label.name);
            }
            Click((RectTransform)nodes[TraitId.Eco_A1].transform);
            check(view.IsResetConfirmationOpen && !view.IsDetailOpen, "확인창 뒤 노드 클릭 차단");
            Click(cancel);
            check(!view.IsResetConfirmationOpen && account.Points == 96 && tree.AllocatedPoints == 4, "초기화 취소 데이터 유지");
            Click(resetRect);
            yield return null;
            host.CloseTop();
            check(!view.IsResetConfirmationOpen && view.gameObject.activeSelf && tree.AllocatedPoints == 4, "ESC 경로는 확인창만 취소");
            Click(resetRect);
            yield return null;
            Click(confirm);
            yield return null;
            check(!view.IsResetConfirmationOpen && account.Points == 100 && tree.AllocatedPoints == 0, "확정 후 전체 초기화 및 전액 환급");
            check(!reset.interactable && !tree.IsOpen(TraitId.Eco_A2), "초기화 후 버튼 비활성 및 선행 잠금");
            check(nodes[TraitId.Eco_A1].transform.Find("Frame").GetComponent<Image>().sprite == nodes[TraitId.Eco_A1].DefaultFrameSprite,
                "초기화 후 만렙 프레임 해제");
            check(nodes[TraitId.Eco_A2].transform.Find("Name").GetComponent<TMP_Text>().color == (Color)new Color32(155, 150, 146, 255),
                "초기화 후 잠금 이름 색 복귀");
            // 재진입 시 리스너가 중복되어 한 클릭에 여러 번 차감/초기화하지 않아야 한다.
            for (int i = 0; i < 2; i++)
            {
                view.gameObject.SetActive(false);
                view.gameObject.SetActive(true);
                controller.Bind(tree, account);
                yield return null;
            }
            view.ShowSelection(nodes[TraitId.Eco_A1]);
            yield return null;
            Click((RectTransform)upgrade.transform);
            check(tree.RankOf(TraitId.Eco_A1) == 1 && account.Points == 99,
                "재진입 후 이벤트 단일 실행: rank=" + tree.RankOf(TraitId.Eco_A1) + ", LP=" + account.Points);
            Click(resetRect);
            yield return null;
            Click(confirm);
            yield return null;
            check(tree.AllocatedPoints == 0 && account.Points == 100,
                "재진입 후 초기화 단일 환급: allocated=" + tree.AllocatedPoints + ", LP=" + account.Points);
            check(Snapshot(originalTree.Defs.ToArray()) == saved, "실제 계정 불변");
            UIResult = "Trait UI: " + assertions + " assertions passed / flat LP, label colors, reset, frame swaps, drag and zoom";
        }
        finally
        {
            if (controller != null) controller.Bind(originalTree, originalAccount);
            if (originalScroll != null && originalScroll.content != null) originalScroll.content.localScale = originalScale;
            if (view != null) view.ResetView();
        }
    }

    private static void Wheel(RectTransform target, Vector2 offset, float delta)
    {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(offset)),
            scrollDelta = new Vector2(0, delta)
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0) throw new InvalidOperationException("휠 대상 없음: " + target.name);
        pointer.pointerCurrentRaycast = hits[0];
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.scrollHandler);
    }

    [MenuItem("Tools/OZGL2/Lobby/Validate Trait Icon Alignment")]
    public static void RunIconValidationFromMenu() => Debug.Log(ValidateIconAlignment());

    public static string ValidateIconAlignment()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/06.UI/LobbyMutedPreview/Overlays/Prefabs/Canvas_LobbyOverlays.prefab");
        var nodes = prefab.GetComponentsInChildren<UITraitFrameView>(true);
        int count = 0;
        foreach (var node in nodes)
        {
            var icon = node.transform.Find("Icon").GetComponent<Image>();
            var texture = new Texture2D(2, 2);
            try
            {
                ImageConversion.LoadImage(texture, System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(icon.sprite)));
                var pixels = texture.GetPixels32();
                var source = icon.sprite.rect;
                var rect = icon.rectTransform;
                float scale = rect.rect.width / source.width;
                Vector2 low = Vector2.one * float.PositiveInfinity, high = Vector2.one * float.NegativeInfinity;
                float maxRadius = 0;
                float visualY = 0, visualWeight = 0;
                for (int y = (int)source.yMin; y < source.yMax; y++)
                    for (int x = (int)source.xMin; x < source.xMax; x++)
                    {
                        if (pixels[y * texture.width + x].a <= 24) continue;
                        Vector2 point = rect.anchoredPosition + (new Vector2(x + 0.5f, y + 0.5f) - source.center) * scale;
                        low = Vector2.Min(low, point); high = Vector2.Max(high, point);
                        maxRadius = Mathf.Max(maxRadius, Mathf.Abs(point.x) + Mathf.Abs(point.y));
                        Color32 pixel = pixels[y * texture.width + x];
                        if (pixel.r + pixel.g + pixel.b > 240)
                        {
                            visualY += point.y * pixel.a;
                            visualWeight += pixel.a;
                        }
                    }
                float allowed = ((RectTransform)node.transform).rect.width * (node.IsSpecialized ? 0.27f : 0.30f);
                // 하트만 밝은 실루엣의 시각 중심을 사용한다. 나머지 39개는 기존 바운딩 중앙을 유지한다.
                bool isCentered = node.Definition.id == TraitId.Mon_B2
                    ? Mathf.Abs((low.x + high.x) * 0.5f) < 0.1f && visualWeight > 0 && Mathf.Abs(visualY / visualWeight) < 0.1f
                    : (low + high).magnitude <= 0.1f;
                if (!isCentered || maxRadius > allowed + 0.1f)
                    throw new InvalidOperationException("아이콘 정렬/테두리 내부 범위: " + node.name);
                count++;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        if (count != 40) throw new InvalidOperationException("아이콘 40개 필요");
        return "Trait icons: 39 bounding centers + 1 optical heart center / 40 within inner diamond";
    }

    private static void Click(RectTransform target, Vector2 offset = default)
    {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(offset)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0) throw new InvalidOperationException("클릭 대상 없음: " + target.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
}
