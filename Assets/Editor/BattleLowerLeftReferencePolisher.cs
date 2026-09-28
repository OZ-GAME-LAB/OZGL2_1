using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 승인된 하단 왼쪽 영역만 보정한다. 버튼 이벤트와 동적 숫자 참조는 유지한다.
public static class BattleLowerLeftReferencePolisher
{
    private const string SCENE_PATH = "Assets/00.Scenes/UI_Flow/UI_Battle_MutedPreview.unity";
    private const string ART_ROOT = "Assets/06.UI/BattleMutedPreview/LowerLeftReference/";
    private const string FONT_PATH = "Assets/98.ExternalAssets/00.LocalStaging/01.Font/NotoSansCJKkr-Regular.otf";
    private const string UNDO_NAME = "전투 하단 왼쪽 레퍼런스 보정";
    private const float REROLL_SIZE = 168f;
    private const float PRICE_WIDTH = 180f;
    private const float PRICE_HEIGHT = 56f;
    private static readonly Color OPAQUE_BLACK = new Color(0.01f, 0.01f, 0.012f, 1f);

    [MenuItem("Tools/OZGL2/Battle/Apply Lower Left Reference")]
    public static void Apply()
    {
        Scene scene = ValidateScene();
        Transform root = null;
        foreach (GameObject candidate in scene.GetRootGameObjects())
        {
            if (candidate.name != "UI_BattleScreens") continue;
            if (root != null) throw new InvalidOperationException("UI_BattleScreens가 중복되었습니다.");
            root = candidate.transform;
        }
        if (root == null) throw new InvalidOperationException("UI_BattleScreens가 없습니다.");
        Transform prep = Need(root, "Canvas_Preparation");
        string[] paths = { "BottomNobleBackground", "Currency", "Currency/Body", "Currency/Frame", "Currency/Icon", "Currency/Value", "Reroll_Placeholder", "Reroll_Placeholder/Body", "Reroll_Placeholder/Frame", "Reroll_Placeholder/Icon", "RerollPrice", "RerollPrice/Frame", "RerollPrice/Frame/Body", "RerollPrice/Frame/Border", "RerollPrice/CurrencyIcon", "RerollCost" };
        var targets = new Dictionary<string, RectTransform>();
        foreach (string path in paths)
        {
            RectTransform rect = Need(prep, path) as RectTransform;
            if (rect == null || PrefabUtility.IsPartOfPrefabInstance(rect.gameObject))
                throw new InvalidOperationException("대상은 씬의 RectTransform이어야 합니다: " + path);
            if (rect.GetComponent<LayoutGroup>() != null || rect.GetComponent<ContentSizeFitter>() != null)
                throw new InvalidOperationException("자동 레이아웃과 충돌할 수 있어 중단합니다: " + path);
            targets.Add(path, rect);
        }
        foreach (string path in new[] { "BottomNobleBackground", "Currency/Body", "Currency/Frame", "Currency/Icon", "Reroll_Placeholder/Body", "Reroll_Placeholder/Frame", "Reroll_Placeholder/Icon", "RerollPrice/Frame/Body", "RerollPrice/Frame/Border", "RerollPrice/CurrencyIcon" })
            Require<Image>(targets[path]);
        Text costValue = Require<Text>(targets["Currency/Value"]);
        Text rerollValue = Require<Text>(targets["RerollCost"]);
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
        if (font == null) throw new InvalidOperationException("숫자 표시용 기존 Font를 찾지 못했습니다.");
        string[] names = { "Frame_Cost", "Icon_Flame", "Frame_Reroll", "Icon_Reroll", "Frame_Price", "Icon_CostGem", "Panel_Clean" };
        foreach (string name in names)
            if (!File.Exists(ART_ROOT + name + ".png")) throw new FileNotFoundException("아트가 모두 준비되지 않았습니다.", name);
        var sprites = new Dictionary<string, Sprite>();
        var importSettings = new Dictionary<string, string>();
        int group = -1;
        try
        {
            foreach (string name in names) sprites.Add(name, ImportSprite(ART_ROOT + name + ".png", importSettings));
            if (sprites["Panel_Clean"].rect.size != new Vector2(1920, 432))
                throw new InvalidOperationException("패널은 기존 좌표를 보존하는 1920×432 PNG여야 합니다.");
            ValidateScene();
            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UNDO_NAME);
            var objects = new List<UnityEngine.Object>();
            foreach (RectTransform rect in targets.Values)
            {
                objects.Add(rect);
                Image image = rect.GetComponent<Image>();
                Text text = rect.GetComponent<Text>();
                if (image != null) objects.Add(image);
                if (text != null) objects.Add(text);
            }
            Undo.RecordObjects(objects.ToArray(), UNDO_NAME);

            // 사용자가 맞춘 Cost 위치와 배경 RectTransform/색상은 덮어쓰지 않는다.
            RectTransform cost = targets["Currency"];
            RectTransform panel = targets["BottomNobleBackground"];
            Image panelImage = panel.GetComponent<Image>();
            panelImage.sprite = sprites["Panel_Clean"];
            panelImage.overrideSprite = null;
            panelImage.raycastTarget = false;
            Vector2 costCenter = ((RectTransform)prep).InverseTransformPoint(cost.TransformPoint(cost.rect.center));
            Vector2 prepTopLeft = new Vector2(((RectTransform)prep).rect.xMin, ((RectTransform)prep).rect.yMax);
            float centerY = prepTopLeft.y - costCenter.y;

            // PreserveAspect가 만드는 내부 여백도 고려해 작은 봉우리의 실제 X를 구한다.
            float renderScale = Mathf.Min(panel.rect.width / 1920f, panel.rect.height / 432f);
            float insetX = (panel.rect.width - 1920f * renderScale) * 0.5f;
            Vector3 peakWorld = panel.TransformPoint(new Vector3(panel.rect.xMin + insetX + 375f * renderScale, panel.rect.yMin, 0));
            float centerX = ((RectTransform)prep).InverseTransformPoint(peakWorld).x - prepTopLeft.x;

            SetSprite(targets["Currency/Frame"], sprites["Frame_Cost"]);
            SetSprite(targets["Currency/Icon"], sprites["Icon_Flame"]);
            Place(targets["Currency/Icon"], 94, 42, 72, 94);
            Place(targets["Currency/Value"], 54, 138, 152, 66);
            StyleNumber(costValue, font, 56, FontStyle.Bold);
            targets["Currency/Body"].GetComponent<Image>().color = OPAQUE_BLACK;

            Place(targets["Reroll_Placeholder"], centerX - REROLL_SIZE * 0.5f, centerY - REROLL_SIZE * 0.5f, REROLL_SIZE, REROLL_SIZE);
            RectTransform rerollBody = targets["Reroll_Placeholder/Body"];
            rerollBody.anchorMin = rerollBody.anchorMax = new Vector2(0, 1);
            rerollBody.pivot = new Vector2(0.5f, 0.5f);
            rerollBody.anchoredPosition = new Vector2(84, -84);
            rerollBody.sizeDelta = new Vector2(94, 94);
            rerollBody.localScale = Vector3.one;
            rerollBody.localRotation = Quaternion.Euler(0, 0, 45);
            rerollBody.GetComponent<Image>().color = OPAQUE_BLACK;
            Place(targets["Reroll_Placeholder/Frame"], 0, 0, REROLL_SIZE, REROLL_SIZE);
            SetSprite(targets["Reroll_Placeholder/Frame"], sprites["Frame_Reroll"]);
            Place(targets["Reroll_Placeholder/Icon"], 40, 47, 88, 74);
            SetSprite(targets["Reroll_Placeholder/Icon"], sprites["Icon_Reroll"]);

            float priceX = centerX - PRICE_WIDTH * 0.5f;
            float priceY = centerY + REROLL_SIZE * 0.5f + 4;
            Place(targets["RerollPrice"], priceX, priceY, PRICE_WIDTH, PRICE_HEIGHT);
            Place(targets["RerollPrice/Frame"], 0, 0, PRICE_WIDTH, PRICE_HEIGHT);
            Place(targets["RerollPrice/Frame/Body"], 5, 5, PRICE_WIDTH - 10, PRICE_HEIGHT - 10);
            targets["RerollPrice/Frame/Body"].GetComponent<Image>().color = OPAQUE_BLACK;
            Place(targets["RerollPrice/Frame/Border"], 0, 0, PRICE_WIDTH, PRICE_HEIGHT);
            SetSprite(targets["RerollPrice/Frame/Border"], sprites["Frame_Price"]);
            Place(targets["RerollPrice/CurrencyIcon"], 18, 10, 36, 36);
            SetSprite(targets["RerollPrice/CurrencyIcon"], sprites["Icon_CostGem"]);
            Place(targets["RerollCost"], priceX + 62, priceY + 4, 106, 48);
            StyleNumber(rerollValue, font, 40, FontStyle.Normal);

            Undo.FlushUndoRecordObjects();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("프리뷰 씬 저장에 실패했습니다.");
            Undo.CollapseUndoOperations(group);
            Debug.Log("하단 Cost/리롤/가격 레퍼런스 보정 및 패널 장식 3곳 제거 완료. 기존 숫자 값·버튼 연결·다른 UI는 유지했습니다.");
        }
        catch
        {
            if (group >= 0) { Undo.FlushUndoRecordObjects(); Undo.RevertAllDownToGroup(group); }
            foreach (var pair in importSettings)
            {
                var importer = AssetImporter.GetAtPath(pair.Key) as TextureImporter;
                if (importer == null) continue;
                EditorJsonUtility.FromJsonOverwrite(pair.Value, importer);
                importer.SaveAndReimport();
            }
            throw;
        }
    }

    private static Scene ValidateScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded || scene.path != SCENE_PATH || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("UI_Battle_MutedPreview의 Edit Mode에서 실행해 주세요.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬을 먼저 보존해 주세요.");
        return scene;
    }

    private static Transform Need(Transform parent, string path)
    {
        foreach (string part in path.Split('/'))
        {
            Transform found = null;
            foreach (Transform child in parent)
            {
                if (child.name != part) continue;
                if (found != null) throw new InvalidOperationException("중복 경로: " + path);
                found = child;
            }
            if (found == null) throw new InvalidOperationException("필수 대상 누락: " + path);
            parent = found;
        }
        return parent;
    }

    private static T Require<T>(Transform target) where T : Component
    {
        if (!target.TryGetComponent(out T component)) throw new InvalidOperationException("필수 컴포넌트 누락: " + target.name + "/" + typeof(T).Name);
        return component;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition3D = new Vector3(x, -y, 0);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetSprite(RectTransform rect, Sprite sprite)
    {
        Image image = rect.GetComponent<Image>();
        image.overrideSprite = null;
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void StyleNumber(Text label, Font font, int size, FontStyle style)
    {
        label.font = font;
        label.fontSize = size;
        label.fontStyle = style;
        label.resizeTextForBestFit = false;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
    }

    private static Sprite ImportSprite(string path, Dictionary<string, string> settings)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Sprite 임포터 누락: " + path);
        settings.Add(path, EditorJsonUtility.ToJson(importer));
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
        textureSettings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(textureSettings);
        importer.spriteBorder = Vector4.zero;
        importer.spritePixelsPerUnit = 100;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Sprite 로드 실패: " + path);
        return sprite;
    }
}
