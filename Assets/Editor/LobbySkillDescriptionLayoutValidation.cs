using System;
using System.Linq;
using System.Reflection;
using System.Text;
using OZGL2.UIBridge;
using OZGL2.UIFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 실제 Lobby의 글자 설정만 복제한다. 원본 화면, 계정, 카탈로그, Prefab은 수정하지 않는다.
public static class LobbySkillDescriptionLayoutValidation
{
    private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PRIVATE_STATIC = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("Tools/OZGL2/Lobby/Validate Skill Description Word Wrapping")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        var view = Resources.FindObjectsOfTypeAll<UISkillLoadoutPreview>().FirstOrDefault(item =>
            item.gameObject.scene.IsValid() && item.gameObject.scene.path == "Assets/00.Scenes/Builds/Lobby.unity");
        if (view == null) throw new InvalidOperationException("Builds/Lobby Scene을 열고 실행하세요. 스킬창은 닫혀 있어도 됩니다.");
        return Run(view);
    }

    public static string Run(UISkillLoadoutPreview source)
    {
        var catalog = Read<UISkillPreviewCatalogSO>(source, "_catalog");
        var original = Read<TMP_Text>(source, "_detailDescription");
        if (catalog == null || original == null || original.font == null)
            throw new InvalidOperationException("실제 스킬 카탈로그와 설명 TMP/폰트 연결이 필요합니다.");
        float maximum = original.enableAutoSizing ? original.fontSizeMax : original.fontSize;
        var formatter = typeof(UISkillLoadoutPreview).GetMethod("WrapDescriptionWords", PRIVATE_STATIC);
        var provider = typeof(LobbySkillLoadoutBinder).GetMethod("BuildDetailDescription", PRIVATE_STATIC);
        if (formatter == null || provider == null) throw new InvalidOperationException("실제 설명 표시 경로를 찾을 수 없습니다.");
        string before = EditorJsonUtility.ToJson(original);
        string catalogBefore = EditorJsonUtility.ToJson(catalog);
        var scene = EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder();
        int checks = 0;
        try
        {
            var canvas = new GameObject("Skill description validation", typeof(RectTransform), typeof(Canvas));
            canvas.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(canvas, scene);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            TMP_Text baseline = CreateLabel(original, canvas.transform);
            TMP_Text actual = CreateLabel(original, canvas.transform);
            var host = new GameObject("Description view");
            host.hideFlags = HideFlags.HideAndDontSave;
            host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, scene);
            var view = host.AddComponent<UISkillLoadoutPreview>();
            typeof(UISkillLoadoutPreview).GetField("_detailDescription", PRIVATE_INSTANCE).SetValue(view, actual);
            typeof(UISkillLoadoutPreview).GetField("_detailDescriptionMinimumFontSize", PRIVATE_INSTANCE)
                .SetValue(view, Read<float>(source, "_detailDescriptionMinimumFontSize"));
            actual.enableAutoSizing = false;
            actual.fontSize = maximum;
            report.AppendLine($"Rect {original.rectTransform.rect.width:0.##} x {original.rectTransform.rect.height:0.##}, maximum {maximum:0.##}");

            bool Validate(string name, string text, string[] protectedPhrases = null, bool includeLines = true)
            {
                baseline.enableAutoSizing = false;
                baseline.fontSize = maximum;
                baseline.textWrappingMode = TextWrappingModes.Normal;
                baseline.text = text;
                baseline.ForceMeshUpdate(true, true);
                string parsed = baseline.GetParsedText();
                int rawLines = baseline.textInfo.lineCount;
                bool rawQuantitySplit = protectedPhrases != null && protectedPhrases.Any(phrase =>
                    !IsPhraseOnOneLine(baseline, phrase));
                baseline.text = (string)formatter.Invoke(null, new object[] { text });
                baseline.ForceMeshUpdate(true, true);
                int wordLines = baseline.textInfo.lineCount;
                view.SetDetailDescription(text);
                actual.ForceMeshUpdate(true, true);
                Require(actual.GetParsedText() == parsed, name + " 원문/색상 태그 내용 유지");
                Require(!actual.isTextOverflowing && !actual.isTextTruncated, name + " overflow/잘림 없음");
                Require(actual.textBounds.size.x <= actual.rectTransform.rect.width + 0.5f &&
                    actual.textBounds.size.y <= actual.rectTransform.rect.height + 0.5f, name + " 표시 영역 내부");
                Require(actual.enableAutoSizing && actual.fontSize <= maximum + 0.01f, name + " 기존 최대 크기 유지");
                Require(actual.fontSize >= actual.fontSizeMin - 0.01f, name + " 최소 글자 크기 유지");
                Require(actual.font == original.font && actual.fontSharedMaterial == original.fontSharedMaterial &&
                    actual.color == original.color && actual.margin == original.margin &&
                    actual.rectTransform.rect.size == original.rectTransform.rect.size, name + " 폰트/색/마진/영역 유지");
                for (int i = 1; i < actual.textInfo.characterCount; i++)
                {
                    TMP_CharacterInfo previous = actual.textInfo.characterInfo[i - 1];
                    TMP_CharacterInfo current = actual.textInfo.characterInfo[i];
                    if (previous.lineNumber == current.lineNumber) continue;
                    Require(char.IsWhiteSpace(previous.character) || char.IsWhiteSpace(current.character), name + " 어절 중간 줄바꿈 없음");
                }
                // 공백 앞뒤의 줄바꿈 허용 검사만으로는 '4 인'이나 '반경 4 안의' 분리를 찾을 수 없다.
                // 원문에서 정한 표현의 모든 문자를 직접 비교해 숫자와 단위의 결합을 확인한다.
                foreach (string phrase in protectedPhrases ?? Array.Empty<string>())
                    Require(IsPhraseOnOneLine(actual, phrase), name + " 숫자 표현 유지: " + phrase);
                foreach (string phrase in new[] { "반경 4 안의", "반경 2.5 안의", "반경 1.5 안의", "Lv 1", "Lv 50" })
                    if (parsed.Contains(phrase))
                        Require(IsPhraseOnOneLine(actual, phrase), name + " 카탈로그/레벨 표현 유지: " + phrase);
                checks++;
                if (includeLines)
                {
                    report.AppendLine($"{name}: raw {rawLines}, words {wordLines}, fitted {actual.textInfo.lineCount} lines / {actual.fontSize:0.##} pt");
                    report.AppendLine("  " + DescribeLines(actual));
                }
                return rawQuantitySplit;
            }

            foreach (var entry in catalog.Entries)
            {
                if (entry == null) continue;
                Validate(entry.DisplayName + " / catalog", entry.Description);
                if (entry.EffectLabel == "피해" || entry.EffectLabel == "연쇄 피해" ||
                    entry.EffectLabel == "지속 피해" || entry.EffectLabel == "연속 피해")
                {
                    foreach (int level in new[] { 1, 50 })
                    {
                        string body = (string)provider.Invoke(null, new object[] { entry.Description, 1f + (level - 1) * 0.04f, level });
                        Validate(entry.DisplayName + " / runtime Lv " + level, body);
                    }
                }
            }
            Validate("rich text", "<color=#FFD27A>피해를 30%만큼</color> <b>추가합니다.</b>\n<nobr>한 단어</nobr> <size=90%>스킬 설명</size>",
                new[] { "30%만큼", "한 단어" });
            Validate("explicit line breaks", "숫자 4\n인 단위 0.8<br>초");
            Require(!IsPhraseOnOneLine(actual, "4\n인") && !IsPhraseOnOneLine(actual, "0.8\n초"), "원문에서 지정한 개행 유지");

            var quantities = new (string Text, string Phrase)[]
            {
                ("4인", "4인"), ("4 인", "4 인"), ("0.8초", "0.8초"), ("0.8 초", "0.8 초"),
                ("30%", "30%"), ("30 %", "30 %"), ("5체에게", "5체에게"), ("18%만큼", "18%만큼"),
                ("반경 4 안의", "반경 4 안의"), ("반경 1.5 안의", "반경 1.5 안의"),
                ("반경4 안의", "반경4 안의"), ("Lv 50", "Lv 50"),
                ("반경 <b>4</b> 안의", "반경 4 안의"),
                ("반경 <nobr>4</nobr> 안의", "반경 4 안의"),
                ("반경 <color=#FFD27A>1.5</color> 안의", "반경 1.5 안의"),
                ("<color=#FFD27A>4</color> 인", "4 인"),
                ("<color=#FFD27A>0.8</color> <size=90%>초</size>", "0.8 초"),
                ("30 <color=#FFD27A>%</color>", "30 %")
            };
            int quantityChecks = 0;
            int rawQuantitySplits = 0;
            foreach (var quantity in quantities)
            {
                // 실제 설명칸 크기를 유지한 채 앞쪽 어절 수를 바꿔 모든 표현을 줄 끝 경계에 걸친다.
                for (int prefixWords = 1; prefixWords <= 28; prefixWords++)
                {
                    string prefix = string.Join(" ", Enumerable.Repeat("가", prefixWords));
                    if (Validate("quantity boundary / " + quantity.Phrase + " / " + prefixWords,
                        prefix + " " + quantity.Text + " 효과를 적용합니다.", new[] { quantity.Phrase }, false))
                        rawQuantitySplits++;
                    quantityChecks++;
                }
            }
            Require(rawQuantitySplits > 0, "줄 끝에서 숫자 표현이 갈라지는 원본 조건 재현");
            report.AppendLine($"Quantity boundaries: {quantityChecks} passed; {rawQuantitySplits} original split positions covered.");
            Validate("empty", string.Empty);
            Validate("short after long", "스킬 설명");
            Require(Mathf.Abs(actual.fontSize - maximum) < 0.01f, "긴 설명 다음 짧은 설명의 최대 크기 복원");
            return checks + " skill description cases passed\n" + report;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Require(before == EditorJsonUtility.ToJson(original), "원본 TMP 설정 불변");
            Require(catalogBefore == EditorJsonUtility.ToJson(catalog), "원본 카탈로그 불변");
        }
    }

    private static bool IsPhraseOnOneLine(TMP_Text text, string phrase)
    {
        var characters = new StringBuilder(text.textInfo.characterCount);
        for (int i = 0; i < text.textInfo.characterCount; i++)
            characters.Append(text.textInfo.characterInfo[i].character);
        string rendered = characters.ToString();
        int start = rendered.IndexOf(phrase, StringComparison.Ordinal);
        if (start < 0) return false;
        do
        {
            int line = text.textInfo.characterInfo[start].lineNumber;
            for (int i = start + 1; i < start + phrase.Length; i++)
                if (text.textInfo.characterInfo[i].lineNumber != line) return false;
            start = rendered.IndexOf(phrase, start + phrase.Length, StringComparison.Ordinal);
        }
        while (start >= 0);
        return true;
    }

    private static string DescribeLines(TMP_Text text)
    {
        var result = new StringBuilder();
        int previousLine = -1;
        for (int i = 0; i < text.textInfo.characterCount; i++)
        {
            var character = text.textInfo.characterInfo[i];
            if (character.lineNumber != previousLine)
            {
                if (previousLine >= 0) result.Append(" | ");
                result.Append('[').Append(character.lineNumber + 1).Append("] ");
                previousLine = character.lineNumber;
            }
            if (character.character != '\n' && character.character != '\r') result.Append(character.character);
        }
        return result.ToString();
    }

    private static TMP_Text CreateLabel(TMP_Text original, Transform parent)
    {
        var go = new GameObject("Description sample", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        EditorUtility.CopySerialized(original, text);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.sizeDelta = original.rectTransform.rect.size;
        text.raycastTarget = false;
        return text;
    }

    private static T Read<T>(UISkillLoadoutPreview view, string name) =>
        (T)typeof(UISkillLoadoutPreview).GetField(name, PRIVATE_INSTANCE).GetValue(view);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Skill description 검사 실패: " + message);
    }
}
