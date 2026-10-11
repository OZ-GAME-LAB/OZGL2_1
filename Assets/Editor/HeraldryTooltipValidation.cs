using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OZGL2.Synergy;
using OZGL2.UI;
using OZGL2.UIBridge;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>게임 초기화 없이 실제 표시 메서드만 임시 씬에서 검사합니다. 계정/보상 처리는 호출하지 않습니다.</summary>
public static class HeraldryTooltipValidation
{
    private const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string OUTPUT = "Temp/HeraldryTooltips_20261012";

    [MenuItem("Tools/OZGL2/Battle Tooltips/Validate In Temporary UI Scene")]
    public static void ValidateFromMenu() => Debug.Log(Run());

    [MenuItem("Tools/OZGL2/Battle Tooltips/Validate Wave Reward Attachment")]
    public static void ValidateWaveAttachmentFromMenu() => Debug.Log(RunWaveAttachment());

    /// <summary>실제 웨이브 표시를 복제해 게임 처리 없이 보상창 연결 위치를 검사합니다.</summary>
    public static string RunWaveAttachment()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 UI 위치를 검증해 주세요.");
        Scene original = SceneManager.GetActiveScene();
        var rects = original.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RectTransform>(true)).ToArray();
        var sourceViewport = rects.Single(r => r.name == "WavePreviewViewport");
        var sourceHud = original.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InGameCurrencyHud>(true)).Single();
        string before = SceneFingerprint(original);
        bool wasDirty = original.isDirty;
        Scene test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var results = new List<object>();
        try
        {
            SceneManager.SetActiveScene(test);
            var camera = new GameObject("WaveAttachmentCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(30,28,34,255);
            camera.orthographic = true;
            new GameObject("WaveAttachmentLight", typeof(Light)).GetComponent<Light>().type = LightType.Directional;
            var canvas = new GameObject("WaveAttachmentCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            var viewport = Object.Instantiate(sourceViewport, canvas.transform, false);
            viewport.localScale = Vector3.one * 0.72f;
            // 실제 전투 데이터 조회/구독은 비활성화하고 기존 프레임·미리보기만 사용합니다.
            foreach (var carousel in viewport.GetComponentsInChildren<UIWavePreviewCarousel>(true)) carousel.enabled = false;
            var wave = (RectTransform)viewport.Find("WavePreview");
            var group = wave.GetComponent<CanvasGroup>() ?? wave.gameObject.AddComponent<CanvasGroup>();
            var disclosure = new GameObject("TestDisclosure", typeof(UIWavePreviewDisclosure)).GetComponent<UIWavePreviewDisclosure>();
            disclosure.enabled = false;
            Set(disclosure, "_viewport", viewport);
            Set(disclosure, "_panel", wave);
            Set(disclosure, "_panelCanvasGroup", group);
            var header = rects.FirstOrDefault(r => r.name == "BattleHUD");
            if (header != null) Object.Instantiate(header, canvas.transform, false);

            var owner = new GameObject("RewardAttachmentTest");
            var hud = owner.AddComponent<InGameCurrencyHud>();
            hud.enabled = false;
            Set(hud, "_summaryPanelPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(HeraldryTooltipPrefabBuilder.REWARD_PREFAB).GetComponent<UIAdaptiveTooltipPanel>());
            Set(hud, "_wavePanel", viewport);
            Invoke(hud, "EnsureSummary");
            var panel = Get<UIAdaptiveTooltipPanel>(hud, "_summaryPanel");
            ClearPreview(panel);
            Type rowType = typeof(InGameCurrencyHud).GetNestedType("RewardRow", BindingFlags.NonPublic);
            var rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType));
            rows.Add(Activator.CreateInstance(rowType, new object[]{Get<Sprite>(sourceHud,"_gemIcon"),Color.white,"재화","최대 +13","용사 3명 처치 9 + 클리어 4","#F2DC8C"}));
            rows.Add(Activator.CreateInstance(rowType, new object[]{Get<Sprite>(sourceHud,"_spIcon"),new Color(0.6f,0.82f,1f),"SP","이번엔 없음","다음 SP: 라운드 4","#C9A8FF"}));
            Invoke(hud, "LayoutSummary", rows);

            Func<string,bool,float?,Vector2> check = (name, expanded, progress) =>
            {
                disclosure.SetExpanded(expanded, false);
                if (progress.HasValue) Invoke(disclosure, "ApplyProgress", progress.Value);
                Canvas.ForceUpdateCanvases();
                Invoke(hud, "PlaceSummaryUnderWavePanel");
                Vector2 position = panel.RectTransform.anchoredPosition;
                var corners = new Vector3[4]; viewport.GetWorldCorners(corners);
                float left = RectTransformUtility.WorldToScreenPoint(camera,corners[0]).x;
                float top = RectTransformUtility.WorldToScreenPoint(camera,corners[2]).y;
                float scale = Mathf.Max(0.8f, Screen.height/1080f);
                if (Mathf.Abs(position.x-left) > 0.1f)
                    throw new InvalidOperationException(name+": 보상창의 가로 위치가 바뀌었습니다.");
                if (!expanded && !progress.HasValue && Mathf.Abs(position.y-(top-28f*scale)) > 0.1f)
                    throw new InvalidOperationException(name+": 접힌 창이 Viewport 상단에 붙지 않았습니다.");
                results.Add(new{name,x=position.x,y=position.y,width=panel.RectTransform.sizeDelta.x,summaryBottom=InGameCurrencyHud.SummaryBottom});
                if (name == "Expanded" || name == "Collapsed")
                    Capture(Get<Canvas>(hud,"_summaryCanvas"), panel, camera, "WaveReward_"+name, false);
                return position;
            };
            check("StartsCollapsed",false,null);
            Vector2 open = check("Expanded",true,null);
            Vector2 midway = check("HalfCollapsed",false,0.5f);
            Vector2 closed = check("Collapsed",false,null);
            Vector2 reopened = check("Reexpanded",true,null);
            if (!(open.y < midway.y && midway.y < closed.y) || Mathf.Abs(open.y-reopened.y)>0.1f)
                throw new InvalidOperationException("펼침/접힘 위치 추종 또는 재펼침 복원이 실패했습니다.");
            Set(hud,"_wavePanel",wave); // 패널이 부모 마스크를 찾아야 하는 구조도 검사합니다.
            check("ChildPanelCollapsed",false,null);
            check("ChildPanelExpanded",true,null);
            for (int i=0;i<8;i++) check("Repeat"+i,i%2==0,null);
            return Newtonsoft.Json.JsonConvert.SerializeObject(results);
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(test, true);
            // 테스트 전후 직렬화 상태가 같을 때만 다시 열어 불필요한 미저장 표시를 없앱니다.
            if (!wasDirty && original.isDirty && before == SceneFingerprint(original) && !string.IsNullOrEmpty(original.path))
                EditorSceneManager.OpenScene(original.path, OpenSceneMode.Single);
        }
    }

    private static string SceneFingerprint(Scene scene) => string.Join("\n",scene.GetRootGameObjects()
        .SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null)
        .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)
            +(c is Transform ? ":"+EditorJsonUtility.ToJson(c.gameObject) : string.Empty)));

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("게임 초기화를 실행하지 않는 Edit Mode UI 검증입니다.");
        var results = new List<object>();
        float normalSynergyHeight = 0f;
        Scene original = SceneManager.GetActiveScene();
        var sourceHud = original.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InGameCurrencyHud>(true)).SingleOrDefault();
        Scene test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(test);
            Camera camera = new GameObject("UIValidationCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(30, 28, 34, 255);
            camera.orthographic = true;
            new GameObject("UIValidationLight", typeof(Light)).GetComponent<Light>().type = LightType.Directional;
            var defs = AssetDatabase.FindAssets("t:SynergyData", new[]{"Assets/01.Scripts/Sandbox/Resources/Synergies"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<SynergyData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            var combos = AssetDatabase.FindAssets("t:ComboSynergyData", new[]{"Assets/01.Scripts/Sandbox/Resources/ComboSynergies"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<ComboSynergyData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            UIAdaptiveTooltipPanel synergyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeraldryTooltipPrefabBuilder.SYNERGY_PREFAB).GetComponent<UIAdaptiveTooltipPanel>();
            foreach (var def in defs)
            {
                var owner = new GameObject("SynergyTest");
                var bridge = owner.AddComponent<InGameSynergyHudBridge>();
                bridge.enabled = false;
                Set(bridge, "_tooltipPanelPrefab", synergyPrefab);
                SetSynergyIcons(bridge, owner, def.job);
                Invoke(bridge, "EnsureTooltip");
                var panel = Get<UIAdaptiveTooltipPanel>(bridge, "_tooltipPanel");
                ClearPreview(panel);
                var tracker = new SynergyTracker(defs, combos);
                tracker.SetCount(def.job, 1);
                Invoke(bridge, "BuildTip", tracker, def, 0);
                if (def.job == SynergyJob.Shield) normalSynergyHeight = panel.RectTransform.sizeDelta.y;
                results.Add(Report(def.displayName, panel));
                if (def.job == SynergyJob.Shield || def.job == SynergyJob.Archer)
                    Capture(Get<Canvas>(bridge, "_tipCanvas"), panel, camera, "Synergy_" + def.job);
                Object.DestroyImmediate(owner);
            }

            // 원본 SO를 수정하지 않고 복제 데이터의 긴 설명으로 줄바꿈과 높이 증가를 확인합니다.
            var shield = defs.First(d => d.job == SynergyJob.Shield);
            var sourceCombo = combos.First(c => c.jobA == shield.job || c.jobB == shield.job);
            var extended = Object.Instantiate(sourceCombo);
            try
            {
                extended.desc = "긴 설명 검증 — 방패병의 체력이 증가하고 힐러의 회복량이 증가합니다. "
                    + "전선에서 아군을 보호하며 함께 배치한 직업에 적용되는 효과를 여러 줄로 안내합니다. "
                    + "이 문장은 실제 게임 데이터가 아닌 줄바꿈 검증용입니다.";
                var owner = new GameObject("LongSynergyTest");
                var bridge = owner.AddComponent<InGameSynergyHudBridge>();
                bridge.enabled = false;
                Set(bridge, "_tooltipPanelPrefab", synergyPrefab);
                SetSynergyIcons(bridge, owner, shield.job);
                Invoke(bridge, "EnsureTooltip");
                var panel = Get<UIAdaptiveTooltipPanel>(bridge, "_tooltipPanel");
                ClearPreview(panel);
                var testCombos = combos.Select(c => c == sourceCombo ? extended : c).ToArray();
                var tracker = new SynergyTracker(defs, testCombos);
                tracker.SetCount(shield.job, 5);
                tracker.SetCount(extended.jobA, 5);
                tracker.SetCount(extended.jobB, 5);
                Invoke(bridge, "BuildTip", tracker, shield, 0);
                if (panel.RectTransform.sizeDelta.y <= normalSynergyHeight)
                    throw new InvalidOperationException("긴 설명의 높이 증가가 확인되지 않았습니다.");
                results.Add(Report("SynergyLong", panel));
                Capture(Get<Canvas>(bridge, "_tipCanvas"), panel, camera, "Synergy_Long");
                Object.DestroyImmediate(owner);
            }
            finally { Object.DestroyImmediate(extended); }

            UIAdaptiveTooltipPanel rewardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeraldryTooltipPrefabBuilder.REWARD_PREFAB).GetComponent<UIAdaptiveTooltipPanel>();
            Type rowType = typeof(InGameCurrencyHud).GetNestedType("RewardRow", BindingFlags.NonPublic);
            foreach (int count in new[]{2, 3, 4})
            {
                var owner = new GameObject("RewardTest");
                var hud = owner.AddComponent<InGameCurrencyHud>();
                hud.enabled = false;
                Set(hud, "_summaryPanelPrefab", rewardPrefab);
                Invoke(hud, "EnsureSummary");
                var panel = Get<UIAdaptiveTooltipPanel>(hud, "_summaryPanel");
                ClearPreview(panel);
                var rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType));
                Sprite gem = sourceHud != null ? Get<Sprite>(sourceHud, "_gemIcon") : null;
                Sprite sp = sourceHud != null ? Get<Sprite>(sourceHud, "_spIcon") : null;
                Sprite augment = sourceHud != null ? Get<Sprite>(sourceHud, "_augmentIcon") : null;
                rows.Add(Activator.CreateInstance(rowType, new object[]{gem,Color.white,"재화","최대 +33","용사 9명 처치 27 + 클리어 6","#F2DC8C"}));
                rows.Add(Activator.CreateInstance(rowType, new object[]{sp,new Color(0.6f,0.9f,0.6f),"SP","+1","2라운드마다 +1 · 스킬 해금","#9BE59B"}));
                if (count >= 3)
                {
                    var unlock = Activator.CreateInstance(rowType, new object[]{null,Color.white,"마왕군 해금","마법사","클리어하면 영구 해금되는 마왕군 유닛입니다. 다음 전투부터 소환할 수 있습니다.","#FFD27A"});
                    // 초상화 렌더러와 계정 로직을 실행하지 않고 기존 아이콘 텍스처로 72px 열을 검사합니다.
                    rowType.GetField("portrait").SetValue(unlock, augment != null ? augment.texture : Texture2D.whiteTexture);
                    rows.Add(unlock);
                    if (count == 4)
                        rows.Add(Activator.CreateInstance(rowType, new object[]{augment,Color.white,"증강","1개 선택","보스 처치 보상","#C9A8FF"}));
                }
                Invoke(hud, "LayoutSummary", rows);
                results.Add(Report("Reward" + count, panel));
                Capture(Get<Canvas>(hud, "_summaryCanvas"), panel, camera, "Reward" + count);
                Object.DestroyImmediate(owner);
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject(results);
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(test, true);
        }
    }

    private static object Report(string name, UIAdaptiveTooltipPanel panel)
    {
        Canvas.ForceUpdateCanvases();
        var labels = panel.ContentRoot.GetComponentsInChildren<TMPro.TMP_Text>();
        foreach (var label in labels) label.ForceMeshUpdate();
        if (labels.Any(t=>t.isTextOverflowing))
            throw new InvalidOperationException(name + ": 글자 넘침이 확인되었습니다.");
        return new { name, width=panel.RectTransform.sizeDelta.x, height=panel.RectTransform.sizeDelta.y,
            overflow=labels.Any(t=>t.isTextOverflowing), wrappedLines=labels.Sum(t=>t.textInfo.lineCount),
            fixedOrnament=panel.transform.Find("HeaderOrnament") != null,
            icons=panel.ContentRoot.GetComponentsInChildren<Image>().Count(i=>i.sprite != null),
            portraits=panel.ContentRoot.GetComponentsInChildren<RawImage>().Length,
            frameType=panel.transform.Find("Frame").GetComponent<Image>().type.ToString(),
            crestSize=((RectTransform)panel.transform.Find("HeaderOrnament")).sizeDelta.ToString() };
    }

    private static void SetSynergyIcons(InGameSynergyHudBridge bridge, GameObject owner, SynergyJob job)
    {
        string name = job == SynergyJob.Shield ? "Icon_BoneShield" : job == SynergyJob.Archer ? "Icon_Bow"
            : job == SynergyJob.Mage ? "Icon_Arcane" : job == SynergyJob.Rogue ? "Icon_CursedEye"
            : job == SynergyJob.Healer ? "Icon_Flame" : "Icon_CrossedSwords";
        var image = new GameObject("TestIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(owner.transform, false);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/06.UI/BattleMutedPreview/Sprites/" + name + ".png");
        Get<Image[]>(bridge, "_icons")[0] = image;
        var sprites = Get<Dictionary<string,Sprite>>(bridge, "_sprites");
        foreach (string pip in new[]{"Frame_DiamondRed", "Frame_DiamondNeutral"})
            sprites[pip] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/06.UI/BattleMutedPreview/Sprites/" + pip + ".png");
    }

    private static void Capture(Canvas canvas, UIAdaptiveTooltipPanel panel, Camera camera, string name, bool center = true)
    {
        Directory.CreateDirectory(OUTPUT);
        RenderTexture previous = RenderTexture.active;
        var target = new RenderTexture(1920, 1080, 24);
        var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = target;
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1f;
            if (center)
            {
                panel.RectTransform.anchorMin = panel.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                panel.RectTransform.pivot = new Vector2(0.5f, 0.5f);
                panel.RectTransform.anchoredPosition = Vector2.zero;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
            File.WriteAllBytes(OUTPUT + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
        }
    }

    private static void ClearPreview(UIAdaptiveTooltipPanel panel)
    {
        for (int i=panel.ContentRoot.childCount-1;i>=0;i--) Object.DestroyImmediate(panel.ContentRoot.GetChild(i).gameObject);
    }
    private static void Set(object obj,string name,object value) => obj.GetType().GetField(name,PRIVATE).SetValue(obj,value);
    private static T Get<T>(object obj,string name) => (T)obj.GetType().GetField(name,PRIVATE).GetValue(obj);
    private static void Invoke(object obj,string name,params object[] args) => obj.GetType().GetMethod(name,PRIVATE).Invoke(obj,args);
}
