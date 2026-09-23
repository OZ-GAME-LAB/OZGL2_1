using System;
using System.IO;
using System.Linq;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 승인된 복제 씬만 다시 정렬한다. 기존 이벤트와 표시 참조는 교체하지 않는다.
public static class BattleMutedReferenceLayout
{
    public const string PREVIEW_SCENE = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    public const string ART_DIRECTORY = "Assets/06.UI/BattleMutedPreview/Reference_v2/";
    private const string KOREAN_FONT = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/NotoSansCJKkr-Regular.otf";
    private const string NUMBER_FONT = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/DOSGothic.ttf";
    private const string EXPERIENCE_WHITE = ART_DIRECTORY + "Experience_White.png";
    private static readonly Color INK = new Color(0.018f, 0.016f, 0.019f, 0.94f);
    private static readonly Color PAPER = new Color(0.96f, 0.95f, 0.91f, 1f);

    [MenuItem("Tools/OZGL2/Battle/Apply Reference V2 Layout")]
    public static void ApplyReferenceLayout()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != PREVIEW_SCENE)
            throw new InvalidOperationException("UI_Battle_MutedPreview 씬의 Edit Mode에서만 실행할 수 있습니다.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("미저장 씬 변경을 먼저 저장하거나 보존해 주세요. 정렬을 적용하지 않았습니다.");

        Transform screens = FindRoot(scene, "UI_BattleScreens");
        Transform ready = Need(screens, "Canvas_GetReady");
        Transform prep = Need(screens, "Canvas_Preparation");
        UIBattleMutedPreviewView view = screens.GetComponent<UIBattleMutedPreviewView>();
        if (view == null) throw new InvalidOperationException("V1 프리뷰의 표시 컴포넌트가 필요합니다.");
        Font korean = Load<Font>(KOREAN_FONT);
        Font numbers = Load<Font>(NUMBER_FONT);
        // 필수 아트가 없으면 검정 단색 등으로 대체하지 않고 변경 전에 중단한다.
        Sprite background = Load<Sprite>(ART_DIRECTORY + "Background_Obsidian.png");
        Sprite wave = Load<Sprite>(ART_DIRECTORY + "Frame_Wave.png");
        Sprite action = Load<Sprite>(ART_DIRECTORY + "Frame_DiamondAction.png");
        Sprite synergy = Load<Sprite>(ART_DIRECTORY + "Frame_DiamondSynergy.png");
        foreach (string path in new[] { "Background", "BattleHUD", "SettingsButton", "WavePreview", "SynergyTrackers" }) Need(ready, path);
        foreach (string path in new[] { "Deployment", "ChoiceCards", "Currency", "StartCombatButton", "Reroll_Placeholder", "RerollPrice", "RerollCost", "SynergyButton", "ProbabilitiesButton" }) Need(prep, path);
        Sprite white = EnsureWhiteDataSprite();

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("전투 프리뷰 레퍼런스 V2 정렬");
        Undo.RegisterFullObjectHierarchyUndo(screens.gameObject, "전투 프리뷰 레퍼런스 V2 정렬");
        try
        {
            Image backdrop = ImageAt(ready, "Background");
            backdrop.sprite = background;
            backdrop.type = Image.Type.Simple;
            backdrop.preserveAspect = false;
            backdrop.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            backdrop.raycastTarget = false;
            ApplyHud(ready, numbers, white);
            ApplyEnemies(ready, wave, korean, numbers);
            ApplySynergies(ready, synergy, korean, numbers);
            ApplyActions(prep, action, korean, numbers);

            // 제외 대상의 아트/데이터는 그대로 두고, 승인받은 화면 자리만 비례 조정한다.
            ResizePlaceholder(NeedRect(prep, "Deployment"), 286, 324, 1230, 373);
            ResizePlaceholder(NeedRect(prep, "ChoiceCards"), 516, 723, 890, 319);
            Need(prep, "SynergyButton").gameObject.SetActive(false);
            Need(prep, "ProbabilitiesButton").gameObject.SetActive(false);
            view.RefreshView();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("복제 전투 씬 저장 실패");
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Battle Reference V2: HUD·적 예고·시너지·Cost·리롤·전투 시작 정렬 및 저장 완료. 원본 씬과 기존 버튼 이벤트 보존.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    private static void ApplyHud(Transform ready, Font font, Sprite white)
    {
        Transform hud = Need(ready, "BattleHUD");
        Place(hud, 53, 32, 1815, 79);
        Place(Need(hud, "Body"), 0, 0, 1815, 79);
        Place(Need(hud, "Body/Body"), 5, 5, 1805, 69);
        Place(Need(hud, "Body/Border"), 0, 0, 1815, 79);
        ImageAt(hud, "Body/Border").color = PAPER;
        Place(Need(hud, "WaveIcon"), 44, 18, 48, 46);
        StyleText(hud, "WaveText", font, 42, 136, 10, 360, 60, TextAnchor.MiddleLeft);
        Place(Need(hud, "Divider_Wave"), 493, 20, 5, 40);
        StyleText(hud, "LevelText", font, 40, 588, 10, 170, 60, TextAnchor.MiddleLeft);

        Image track = ImageAt(hud, "ExperienceTrack");
        Place(track.transform, 746, 28, 315, 25);
        track.sprite = null;
        track.type = Image.Type.Simple;
        track.color = new Color(0.12f, 0.12f, 0.125f, 1f);
        Image fill = ImageAt(hud, "ExperienceFill");
        Place(fill.transform, 746, 28, 315, 25);
        fill.sprite = white;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.preserveAspect = false;
        fill.color = new Color(0.56f, 0.19f, 0.22f, 1f);
        Transform ticks = Need(hud, "ExperienceTicks");
        Place(ticks, 746, 28, 315, 25);
        for (int i = 1; i <= ticks.childCount; i++)
        {
            Transform tick = ticks.Find("Tick_" + i);
            if (tick == null) continue;
            tick.gameObject.SetActive(i < 8);
            if (i < 8)
            {
                Place(tick, i * 315f / 8f - 2, 0, 4, 25);
                tick.GetComponent<Image>().color = new Color(0.022f, 0.02f, 0.023f, 1f);
            }
        }
        Place(Need(hud, "Divider_Level"), 1186, 20, 5, 40);
        Place(Need(hud, "TimerIcon"), 1325, 16, 45, 48);
        StyleText(hud, "RemainingTimeText", font, 42, 1407, 10, 235, 60, TextAnchor.MiddleLeft);
        Place(Need(hud, "Divider_Menu"), 1672, 20, 5, 40);
        Transform menu = Need(ready, "SettingsButton");
        Place(menu, 1750, 32, 112, 79);
        Place(Need(menu, "Icon"), 28, 23, 42, 34);
        ImageAt(menu, "Icon").color = PAPER;
    }

    private static void ApplyEnemies(Transform ready, Sprite frame, Font korean, Font numbers)
    {
        Transform wave = Need(ready, "WavePreview");
        Place(wave, 46, 122, 635, 183);
        Place(Need(wave, "Frame"), 0, 0, 635, 183);
        Place(Need(wave, "Frame/Body"), 14, 12, 607, 157);
        Image border = ImageAt(wave, "Frame/Border");
        Place(border.transform, 0, 0, 635, 183);
        border.sprite = frame;
        border.type = Image.Type.Simple;
        border.preserveAspect = false;
        border.color = Color.white;
        Need(wave, "HeaderShade").gameObject.SetActive(false);
        Need(wave, "HeaderLine").gameObject.SetActive(false);
        StyleText(wave, "Title", korean, 28, 196, 7, 243, 40, TextAnchor.MiddleCenter);
        for (int i = 0; i < 3; i++)
        {
            Transform enemy = Need(wave, "Enemy_" + i);
            Place(enemy, 13 + i * 204, 55, 201, 117);
            Place(Need(enemy, "Icon"), 30, 0, 77, 78);
            ImageAt(enemy, "Icon").color = new Color(0.78f, 0.75f, 0.71f, 1f);
            StyleText(enemy, "Count", numbers, 29, 111, 39, 78, 36, TextAnchor.MiddleLeft);
            StyleText(enemy, "Name", korean, 23, 0, 79, 201, 34, TextAnchor.MiddleCenter);
            Transform separator = enemy.Find("Separator");
            if (separator != null) separator.gameObject.SetActive(false);
        }
    }

    private static void ApplySynergies(Transform ready, Sprite frame, Font korean, Font numbers)
    {
        Transform group = Need(ready, "SynergyTrackers");
        Place(group, 1544, 194, 334, 532);
        Color[] colors = { new Color(0.68f, 0.40f, 0.77f), new Color(0.82f, 0.65f, 0.35f), new Color(0.87f, 0.82f, 0.75f), new Color(0.69f, 0.26f, 0.49f) };
        for (int i = 0; i < 4; i++)
        {
            Transform row = Need(group, "Synergy_" + i);
            Place(row, 0, 136 * i, 334, 124);
            Transform plate = Need(row, "Nameplate");
            Place(plate, 63, 12, 271, 110);
            Place(Need(plate, "Body"), 0, 0, 271, 110);
            ImageAt(plate, "Body").color = INK;
            Need(plate, "Border").gameObject.SetActive(false);
            // 이름 판은 얇은 직선 네 개로 구성하여 장식 프레임과 중복되지 않게 한다.
            PlainLine(plate, "ReferenceTop", 0, 0, 271, 1.4f);
            PlainLine(plate, "ReferenceBottom", 0, 108.6f, 271, 1.4f);
            PlainLine(plate, "ReferenceRight", 269.6f, 0, 1.4f, 110);
            PlainLine(plate, "ReferenceLeft", 0, 0, 1.4f, 110);
            PlaceDiamondBody(Need(row, "Body"), 126);
            Image diamond = ImageAt(row, "Frame");
            Place(diamond.transform, 0, 0, 126, 126);
            diamond.sprite = frame;
            diamond.preserveAspect = true;
            diamond.type = Image.Type.Simple;
            diamond.color = colors[i];
            Place(Need(row, "Icon"), 34, 29, 58, 68);
            ImageAt(row, "Icon").color = PAPER;
            // 기존 실루엣의 1px 선이 축소 표시에서 사라지지 않도록 표시 두께만 보강한다.
            Image symbol = ImageAt(row, "Icon");
            Outline outline = symbol.GetComponent<Outline>();
            if (outline == null) outline = Undo.AddComponent<Outline>(symbol.gameObject);
            outline.effectColor = PAPER;
            outline.effectDistance = new Vector2(0.7f, -0.7f);
            outline.useGraphicAlpha = true;
            StyleText(row, "Name", korean, 25, 136, 22, 185, 36, TextAnchor.MiddleLeft);
            Text thresholds = StyleText(row, "Thresholds", numbers, 37, 139, 68, 179, 43, TextAnchor.MiddleCenter);
            thresholds.supportRichText = true;
        }
    }

    private static void ApplyActions(Transform prep, Sprite frame, Font korean, Font numbers)
    {
        Transform cost = Need(prep, "Currency");
        Transform start = Need(prep, "StartCombatButton");
        Place(cost, 29, 766, 260, 260);
        Place(start, 1627, 766, 260, 260);
        foreach (Transform owner in new[] { cost, start })
        {
            PlaceDiamondBody(Need(owner, "Body"), 260);
            Image border = ImageAt(owner, "Frame");
            Place(border.transform, 0, 0, 260, 260);
            border.sprite = frame;
            border.color = Color.white;
            border.type = Image.Type.Simple;
            border.preserveAspect = true;
        }
        Place(Need(cost, "Icon"), 91, 54, 78, 93);
        ImageAt(cost, "Icon").color = PAPER;
        StyleText(cost, "Value", numbers, 58, 54, 138, 152, 66, TextAnchor.MiddleCenter);
        Place(Need(start, "Icon"), 77, 67, 106, 84);
        ImageAt(start, "Icon").color = PAPER;
        StyleText(start, "Title", korean, 30, 44, 142, 172, 44, TextAnchor.MiddleCenter);

        Transform reroll = Need(prep, "Reroll_Placeholder");
        Place(reroll, 306, 838, 138, 138);
        PlaceDiamondBody(Need(reroll, "Body"), 138);
        Place(Need(reroll, "Frame"), 0, 0, 138, 138);
        ImageAt(reroll, "Frame").color = PAPER;
        Place(Need(reroll, "Icon"), 36, 36, 66, 66);
        ImageAt(reroll, "Icon").color = PAPER;
        Transform price = Need(prep, "RerollPrice");
        Place(price, 279, 973, 184, 59);
        Place(Need(price, "Frame"), 0, 0, 184, 59);
        Place(Need(price, "Frame/Body"), 6, 6, 172, 47);
        Place(Need(price, "Frame/Border"), 0, 0, 184, 59);
        Place(Need(price, "CurrencyIcon"), 21, 11, 36, 36);
        StyleText(prep, "RerollCost", numbers, 36, 345, 981, 103, 43, TextAnchor.MiddleCenter);
        Need(prep, "RerollCost").SetAsLastSibling();
    }

    private static void ResizePlaceholder(RectTransform target, float x, float y, float width, float height)
    {
        Vector2 oldSize = target.rect.size;
        if (oldSize.x <= 0 || oldSize.y <= 0) throw new InvalidOperationException(target.name + "의 기존 크기가 유효하지 않습니다.");
        Vector2 ratio = new Vector2(width / oldSize.x, height / oldSize.y);
        foreach (RectTransform child in target.GetComponentsInChildren<RectTransform>(true))
        {
            if (child == target) continue;
            child.anchoredPosition = Vector2.Scale(child.anchoredPosition, ratio);
            child.sizeDelta = Vector2.Scale(child.sizeDelta, ratio);
        }
        Place(target, x, y, width, height);
    }

    private static Sprite EnsureWhiteDataSprite()
    {
        // Filled 게이지에 필요한 단색 데이터 텍스처다. 아트 원본은 생성/덮어쓰지 않는다.
        if (!File.Exists(EXPERIENCE_WHITE))
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
                File.WriteAllBytes(EXPERIENCE_WHITE, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        AssetDatabase.ImportAsset(EXPERIENCE_WHITE, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(EXPERIENCE_WHITE) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("단색 경험치 데이터 텍스처를 불러올 수 없습니다.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return Load<Sprite>(EXPERIENCE_WHITE);
    }

    private static Text StyleText(Transform owner, string path, Font font, int size, float x, float y, float width, float height, TextAnchor anchor)
    {
        Text label = Need(owner, path).GetComponent<Text>();
        if (label == null) throw new InvalidOperationException(path + "에 Unity Text가 없습니다.");
        Place(label.transform, x, y, width, height);
        label.font = font;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.color = PAPER;
        label.alignment = anchor;
        label.resizeTextForBestFit = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static void PlainLine(Transform owner, string name, float x, float y, float width, float height)
    {
        Transform target = owner.Find(name);
        if (target == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "시너지 이름 판 직선 생성");
            go.layer = owner.gameObject.layer;
            go.transform.SetParent(owner, false);
            target = go.transform;
        }
        Place(target, x, y, width, height);
        Image image = target.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = new Color(0.69f, 0.65f, 0.58f, 0.82f);
        image.raycastTarget = false;
    }

    private static void PlaceDiamondBody(Transform target, float size)
    {
        RectTransform rect = target as RectTransform;
        Place(rect, 0, 0, size * 0.55f, size * 0.55f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(size / 2, -size / 2);
        rect.localRotation = Quaternion.Euler(0, 0, 45);
        rect.GetComponent<Image>().color = INK;
    }

    private static void Place(Transform target, float x, float y, float width, float height)
    {
        RectTransform rect = target as RectTransform;
        if (rect == null) throw new InvalidOperationException(target.name + "에 RectTransform이 없습니다.");
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static Image ImageAt(Transform owner, string path)
    {
        Image image = Need(owner, path).GetComponent<Image>();
        if (image == null) throw new InvalidOperationException(path + "에 Image가 없습니다.");
        return image;
    }

    private static RectTransform NeedRect(Transform owner, string path)
    {
        RectTransform result = Need(owner, path) as RectTransform;
        if (result == null) throw new InvalidOperationException(path + "에 RectTransform이 없습니다.");
        return result;
    }

    private static Transform Need(Transform owner, string path)
    {
        Transform result = owner.Find(path);
        if (result == null) throw new InvalidOperationException("필수 UI 오브젝트 없음: " + owner.name + "/" + path);
        return result;
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);
        if (root == null) throw new InvalidOperationException("필수 씬 루트 없음: " + name);
        return root.transform;
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("필수 리소스 없음. 정렬을 적용하지 않았습니다: " + path);
        return asset;
    }
}
