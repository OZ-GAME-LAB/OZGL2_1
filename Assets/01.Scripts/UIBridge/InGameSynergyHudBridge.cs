using System.Collections.Generic;
using System.Text;
using OZGL2.InGame;
using OZGL2.Synergy;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 인게임 우측 시너지 트래커 ← 실제 시너지(SynergyTracker). 미리보기용 고정 문구("마법 결속" 등)를 지우고,
    /// 그리드에 배치된 마왕군의 직업 수로 계산된 값을 보여 준다. UI 씬·프리팹·스크립트는 수정하지 않는다.
    /// 표시 규칙: 배치된 직업(1명 이상) 중 발동 단계 → 배치 수 순으로 위 4개. 숫자는 "현재 배치 수 > 다음 단계 필요 수".
    /// </summary>
    public static class InGameHudBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        private static void Install()
        {
            // 실제 인게임(스테이지 부트스트랩이 있는 씬)에서만 — 미리보기 씬은 고정 문구를 그대로 둔다.
            if (Object.FindFirstObjectByType<InGamePrototypeBootstrap>() == null) return;
            var view = Object.FindFirstObjectByType<UIBattleMutedPreviewView>(FindObjectsInactive.Include);
            if (view != null && view.GetComponent<InGameSynergyHudBridge>() == null)
                view.gameObject.AddComponent<InGameSynergyHudBridge>();
        }
    }

    [DisallowMultipleComponent]
    public sealed class InGameSynergyHudBridge : MonoBehaviour
    {
        private const int Slots = 4;
        private const float TrackerScale = 1.25f; // 오른쪽 시너지 목록이 작아 잘 안 보여서 키운다
        private RectTransform _container;
        private const int ScanInterval = 5; // 프레임마다 확인하지 않아도 충분하다

        private UIBattleMutedPreviewView _view;
        private RealSynergySync _sync;
        private readonly GameObject[] _rows = new GameObject[Slots];
        private readonly Image[] _icons = new Image[Slots];
        private readonly SynergyData[] _slotDef = new SynergyData[Slots];
        private Dictionary<SynergyJob, Sprite> _iconByJob;
        private string _lastSignature;

        // 호버 설명 팝업
        private Canvas _tipCanvas;
        private RectTransform _tipBox;
        private RectTransform _tipContent;
        private string _tipSignature;
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private TMP_FontAsset _font;
        private int _hoverSlot = -1;

        private void OnEnable()
        {
            _view = GetComponent<UIBattleMutedPreviewView>();
            FindRows();
            // 실제 값이 들어오기 전에는 미리보기 문구가 보이지 않게 숨긴다.
            for (int i = 0; i < Slots; i++) if (_rows[i] != null) _rows[i].SetActive(false);
            _lastSignature = null;
        }

        private void OnDisable()
        {
            if (_tipCanvas != null) _tipCanvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (_tipCanvas != null) Destroy(_tipCanvas.gameObject);
        }

        private void Update()
        {
            if (_view == null) return;
            if (_sync == null)
            {
                _sync = FindFirstObjectByType<RealSynergySync>();
                if (_sync == null || _sync.Synergy == null) { _sync = null; return; }
            }
            if (Time.frameCount % ScanInterval == 0) Refresh(_sync.Synergy);
            UpdateTooltip(_sync.Synergy);
        }

        private void LateUpdate() => ScaleTracker();

        /// <summary>시너지 목록 전체를 키우고, 화면 오른쪽(왼쪽) 밖으로 나가면 안쪽으로 당겨 놓는다.</summary>
        private void ScaleTracker()
        {
            if (_container == null) return;
            if (!Mathf.Approximately(_container.localScale.x, TrackerScale)) _container.localScale = new Vector3(TrackerScale, TrackerScale, 1f);
            var canvas = _container.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) return; // 오버레이 캔버스에서는 월드 좌표 = 화면 픽셀
            float minX = float.MaxValue, maxX = float.MinValue;
            var corners = new Vector3[4];
            for (int i = 0; i < Slots; i++)
            {
                if (_rows[i] == null || !_rows[i].activeInHierarchy) continue;
                ((RectTransform)_rows[i].transform).GetWorldCorners(corners);
                minX = Mathf.Min(minX, corners[0].x); maxX = Mathf.Max(maxX, corners[2].x);
            }
            if (maxX < minX) return;
            float margin = 12f;
            if (maxX > Screen.width - margin) _container.position += new Vector3(Screen.width - margin - maxX, 0f, 0f);
            else if (minX < margin) _container.position += new Vector3(margin - minX, 0f, 0f);
        }

        private void FindRows()
        {
            var byName = new Dictionary<string, Transform>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.gameObject.scene.IsValid() && t.name.StartsWith("Synergy_")) byName[t.name] = t;
            for (int i = 0; i < Slots; i++)
            {
                if (!byName.TryGetValue("Synergy_" + i, out var row)) continue;
                _rows[i] = row.gameObject;
                if (_container == null) _container = row.parent as RectTransform;
                foreach (var image in row.GetComponentsInChildren<Image>(true))
                    if (image.name == "Icon") { _icons[i] = image; break; }
            }
        }

        private void BuildIcons()
        {
            if (_iconByJob != null) return;
            var sprites = new Dictionary<string, Sprite>();
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>()) if (s != null) sprites[s.name] = s;
            _iconByJob = new Dictionary<SynergyJob, Sprite>();
            void Map(SynergyJob job, string spriteName)
            {
                if (sprites.TryGetValue(spriteName, out var sprite)) _iconByJob[job] = sprite;
            }
            Map(SynergyJob.Warrior, "Icon_CrossedSwords");
            Map(SynergyJob.Shield, "Icon_BoneShield");
            Map(SynergyJob.Archer, "Icon_Bow");
            Map(SynergyJob.Mage, "Icon_Arcane");
            Map(SynergyJob.Rogue, "Icon_CursedEye");
            Map(SynergyJob.Healer, "Icon_Flame"); // 힐러 전용 아이콘이 아직 없어 임시 사용
        }

        private void Refresh(SynergyTracker tracker)
        {
            // 표시할 직업: 1명 이상 배치된 것. 발동 단계 높은 순 → 배치 수 많은 순 → 직업 순서.
            var shown = new List<SynergyData>();
            foreach (var def in tracker.Defs)
                if (def != null && tracker.CountOf(def.job) > 0) shown.Add(def);
            shown.Sort((a, b) =>
            {
                int byTier = tracker.TierOf(b.job).CompareTo(tracker.TierOf(a.job));
                if (byTier != 0) return byTier;
                int byCount = tracker.CountOf(b.job).CompareTo(tracker.CountOf(a.job));
                return byCount != 0 ? byCount : ((int)a.job).CompareTo((int)b.job);
            });

            var signature = new StringBuilder();
            for (int i = 0; i < Slots && i < shown.Count; i++)
                signature.Append((int)shown[i].job).Append(':').Append(tracker.CountOf(shown[i].job)).Append(';');
            string sig = signature.ToString();
            if (sig == _lastSignature) return;
            _lastSignature = sig;

            BuildIcons();
            for (int i = 0; i < Slots; i++)
            {
                if (i >= shown.Count)
                {
                    _slotDef[i] = null;
                    if (_rows[i] != null) _rows[i].SetActive(false);
                    continue;
                }
                var def = shown[i];
                _slotDef[i] = def;
                int count = tracker.CountOf(def.job);
                int next = count >= def.tier2Threshold ? Mathf.Max(count, def.tier2Threshold)
                         : count >= def.tier1Threshold ? def.tier2Threshold : def.tier1Threshold;
                _view.SetSynergy(i, def.displayName, count, next);
                if (_rows[i] != null) _rows[i].SetActive(true);
                if (_icons[i] != null && _iconByJob.TryGetValue(def.job, out var icon)) _icons[i].sprite = icon;
            }
        }

        // ───────────── 마우스를 올리면 시너지 효과 설명 팝업

        private void UpdateTooltip(SynergyTracker tracker)
        {
            int slot = FindHoveredSlot(out Rect rowRect);
            if (slot < 0 || _slotDef[slot] == null)
            {
                _hoverSlot = -1;
                if (_tipCanvas != null && _tipCanvas.enabled) _tipCanvas.enabled = false;
                return;
            }
            EnsureTooltip();
            if (_tipCanvas == null) return;

            // 내용이 바뀔 때만 다시 만든다(유닛을 옮기면 배치 수가 바뀐다)
            string signature = SignatureOf(tracker, _slotDef[slot], slot);
            if (signature != _tipSignature)
            {
                _tipSignature = signature;
                BuildTip(tracker, _slotDef[slot], slot);
            }
            Vector2 size = _tipBox.sizeDelta;
            float scale = Mathf.Max(0.8f, Screen.height / 1080f);

            // 시너지 줄의 왼쪽에, 줄과 위쪽을 맞춰 띄운다(화면 밖으로 나가지 않게 보정)
            float x = rowRect.xMin - size.x - 14f * scale;
            float y = rowRect.yMax - size.y;
            x = Mathf.Clamp(x, 8f, Screen.width - size.x - 8f);
            y = Mathf.Clamp(y, 8f, Screen.height - size.y - 8f);
            _tipBox.anchoredPosition = new Vector2(x, y);
            _tipCanvas.enabled = true;
            _hoverSlot = slot;
        }

        private string SignatureOf(SynergyTracker tracker, SynergyData def, int slot)
        {
            var sb = new StringBuilder();
            sb.Append(slot).Append('|').Append(def.job).Append('|').Append(tracker.CountOf(def.job)).Append('|').Append(Screen.height);
            foreach (var combo in tracker.ComboDefs)
                if (combo != null && (combo.jobA == def.job || combo.jobB == def.job)) sb.Append('|').Append(tracker.IsComboActive(combo) ? 1 : 0);
            return sb.ToString();
        }

        private int FindHoveredSlot(out Rect rowRect)
        {
            rowRect = default;
            if (Mouse.current == null) return -1;
            Vector2 pointer = Mouse.current.position.ReadValue();
            for (int i = 0; i < Slots; i++)
            {
                if (_rows[i] == null || !_rows[i].activeInHierarchy || _slotDef[i] == null) continue;
                var rect = (RectTransform)_rows[i].transform;
                var canvas = rect.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, pointer, cam)) continue;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                rowRect = Rect.MinMaxRect(Mathf.Min(bl.x, tr.x), Mathf.Min(bl.y, tr.y), Mathf.Max(bl.x, tr.x), Mathf.Max(bl.y, tr.y));
                return i;
            }
            return -1;
        }

        private static string JobName(SynergyTracker tracker, SynergyJob job)
        {
            var def = tracker.Def(job);
            return def != null ? def.displayName : job.ToString();
        }

        private struct TipRow
        {
            public Sprite pip; public Color pipTint; public string body;
            public TipRow(Sprite pip, Color pipTint, string body) { this.pip = pip; this.pipTint = pipTint; this.body = body; }
        }

        /// <summary>이름으로 이미 로드된 UI 스프라이트를 찾는다(씬의 시너지 줄·웨이브 패널이 쓰는 그림을 그대로 재사용).</summary>
        private Sprite Spr(string name)
        {
            if (_sprites.TryGetValue(name, out var cached) && cached != null) return cached;
            foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>())
                if (sp != null && sp.name == name) { _sprites[name] = sp; return sp; }
            return null;
        }

        private void BuildTip(SynergyTracker tracker, SynergyData def, int slot)
        {
            int count = tracker.CountOf(def.job);
            const string gold = "#F2DC8C", gray = "#9A968E", white = "#F5F2E8", green = "#9BE59B", sky = "#9CD2FF";
            Sprite pipOn = Spr("Frame_DiamondRed"), pipOff = Spr("Frame_DiamondNeutral");
            Color offTint = new Color(0.6f, 0.6f, 0.6f, 0.8f);

            // 제목 줄: 직업 아이콘(다이아 받침 위) + "직업 시너지" + 현재 배치 수
            string titleBody = "<size=105%><b><color=" + gold + ">" + def.displayName + " 시너지</color></b></size>\n"
                             + "<size=78%><color=" + (count > 0 ? white : gray) + ">현재 " + count + "명 배치</color></size>";

            var rows = new List<TipRow>();
            rows.Add(MakeTier(pipOn, pipOff, offTint, "1단계", sky, def.tier1Threshold, Short(def, def.tier1Desc), count >= def.tier1Threshold, gray, green));
            rows.Add(MakeTier(pipOn, pipOff, offTint, "2단계", gold, def.tier2Threshold, Short(def, def.tier2Desc), count >= def.tier2Threshold, gray, green));

            var combos = new List<TipRow>();
            foreach (var combo in tracker.ComboDefs)
            {
                if (combo == null || (combo.jobA != def.job && combo.jobB != def.job)) continue;
                bool active = tracker.IsComboActive(combo);
                string color = active ? green : gray;
                string body = "<b><color=" + color + ">" + combo.displayName + "</color></b>  <size=78%><color=" + gray + ">"
                              + JobName(tracker, combo.jobA) + "·" + JobName(tracker, combo.jobB) + " 각 " + combo.requiredCountEach + "명</color></size>\n"
                              + "<size=82%><color=" + color + ">" + ComboEffect(combo.desc) + "</color></size>";
                combos.Add(new TipRow(active ? pipOn : pipOff, active ? Color.white : offTint, body));
            }

            LayoutTip(slot, titleBody, rows, combos);
        }

        private static TipRow MakeTier(Sprite on, Sprite off, Color offTint, string label, string labelColor, int threshold, string desc,
            bool reached, string gray, string green)
        {
            string body = "<b><color=" + (reached ? green : labelColor) + ">" + label + "</color></b>  <color=" + gray + ">" + threshold + "명</color>\n"
                          + "<size=88%><color=" + (reached ? green : gray) + ">" + desc + "</color></size>";
            return new TipRow(reached ? on : off, reached ? Color.white : offTint, body);
        }

        private void LayoutTip(int slot, string titleBody, List<TipRow> rows, List<TipRow> combos)
        {
            float s = Mathf.Max(0.8f, Screen.height / 1080f);
            float padX = 46f * s, padTop = 40f * s, padBottom = 40f * s, pip = 40f * s, gap = 14f * s, rowGap = 10f * s;
            float fontSize = 27f * s, titleIcon = 66f * s;

            for (int i = _tipContent.childCount - 1; i >= 0; i--) Destroy(_tipContent.GetChild(i).gameObject);

            // 글 먼저 만들어 폭을 잰다
            var titleText = TipText(titleBody, fontSize, Color.white);
            Vector2 titlePref = titleText.GetPreferredValues();
            float maxText = titlePref.x + titleIcon - pip; // 제목은 아이콘이 더 크다
            var items = new List<(TipRow row, TMP_Text text, Vector2 size)>();
            foreach (var row in rows) { var tx = TipText(row.body, fontSize, Color.white); var sz = tx.GetPreferredValues(); maxText = Mathf.Max(maxText, sz.x); items.Add((row, tx, sz)); }
            TMP_Text headerText = null; Vector2 headerSize = default;
            var comboItems = new List<(TipRow row, TMP_Text text, Vector2 size)>();
            if (combos.Count > 0)
            {
                headerText = TipText("<b><color=#F2DC8C>조합 시너지</color></b>", fontSize * 0.85f, Color.white);
                headerSize = headerText.GetPreferredValues();
                foreach (var row in combos) { var tx = TipText(row.body, fontSize, Color.white); var sz = tx.GetPreferredValues(); maxText = Mathf.Max(maxText, sz.x); comboItems.Add((row, tx, sz)); }
            }

            float width = padX * 2f + pip + gap + maxText;
            float y = -padTop;

            // 제목 줄
            var diamond = TipImage("TitleDiamond", Spr("Frame_DiamondNeutral"), RowDiamondColor(slot), titleIcon);
            Place(diamond.rectTransform, padX - (titleIcon - pip) * 0.5f, y - titleIcon * 0.5f, 0f, 0.5f);
            var icon = _icons[slot] != null ? _icons[slot].sprite : null;
            if (icon != null)
            {
                var ic = TipImage("TitleIcon", icon, Color.white, titleIcon * 0.52f);
                Place(ic.rectTransform, padX - (titleIcon - pip) * 0.5f + titleIcon * 0.5f, y - titleIcon * 0.5f, 0.5f, 0.5f);
            }
            var tr = titleText.rectTransform;
            tr.sizeDelta = titlePref;
            Place(tr, padX + pip + gap + (titleIcon - pip) * 0.5f, y - titleIcon * 0.5f, 0f, 0.5f);
            y -= titleIcon + 14f * s;

            foreach (var (row, text, size) in items) y = PlaceRow(row, text, size, padX, y, pip, gap, rowGap);

            if (headerText != null)
            {
                y -= 6f * s;
                headerText.rectTransform.sizeDelta = headerSize;
                Place(headerText.rectTransform, padX, y, 0f, 1f);
                y -= headerSize.y + rowGap;
                foreach (var (row, text, size) in comboItems) y = PlaceRow(row, text, size, padX, y, pip, gap, rowGap);
            }

            _tipBox.sizeDelta = new Vector2(width, -y - rowGap + padBottom);
        }

        private float PlaceRow(TipRow row, TMP_Text text, Vector2 size, float padX, float y, float pip, float gap, float rowGap)
        {
            float rowH = Mathf.Max(pip, size.y);
            if (row.pip != null)
            {
                var img = TipImage("Pip", row.pip, row.pipTint, pip);
                Place(img.rectTransform, padX, y - rowH * 0.5f, 0f, 0.5f);
            }
            text.rectTransform.sizeDelta = size;
            Place(text.rectTransform, padX + pip + gap, y - rowH * 0.5f, 0f, 0.5f);
            return y - rowH - rowGap;
        }

        private static void Place(RectTransform rt, float x, float y, float pivotX, float pivotY)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(pivotX, pivotY);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private Color RowDiamondColor(int slot)
        {
            var frame = _rows[slot] != null ? _rows[slot].transform.Find("Frame") : null;
            var img = frame != null ? frame.GetComponent<Image>() : null;
            return img != null ? img.color : Color.white;
        }

        private TMP_Text TipText(string body, float size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_tipContent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) tmp.font = _font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.richText = true;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.lineSpacing = -4f;
            tmp.text = body;
            tmp.ForceMeshUpdate();
            return tmp;
        }

        private Image TipImage(string name, Sprite sprite, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_tipContent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.enabled = sprite != null;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>효과 문구에서 중복되는 직업 이름 머리말("마법사 공격력 +12%" → "공격력 +12%")을 뺀다.</summary>
        private static string Short(SynergyData def, string desc)
        {
            if (string.IsNullOrEmpty(desc)) return string.Empty;
            string prefix = def.displayName + " ";
            return desc.StartsWith(prefix) ? desc.Substring(prefix.Length) : desc;
        }

        /// <summary>조합 설명은 "조건 — 효과" 꼴이라, 조건은 윗줄에 이미 있으므로 효과만 남긴다.</summary>
        private static string ComboEffect(string desc)
        {
            if (string.IsNullOrEmpty(desc)) return string.Empty;
            int dash = desc.IndexOf('—');
            return dash >= 0 ? desc.Substring(dash + 1).Trim() : desc;
        }

        private void EnsureTooltip()
        {
            if (_tipCanvas != null) return;
            var go = new GameObject("SynergyTooltip", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _tipCanvas = go.AddComponent<Canvas>();
            _tipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _tipCanvas.sortingOrder = 30; // 손패(10~11)·드롭 존(15)보다 위, 결과/증강 팝업(100)보다 아래

            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(go.transform, false);
            _tipBox = (RectTransform)boxGo.transform;
            _tipBox.anchorMin = _tipBox.anchorMax = Vector2.zero;
            _tipBox.pivot = Vector2.zero;
            var bg = boxGo.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.03f, 0.05f, 0.95f);
            bg.raycastTarget = false;

            // "이번 웨이브" 패널과 같은 9분할 프레임(씬에 이미 올라와 있는 그림을 이름으로 찾아 쓴다)
            var frameSprite = Spr("Frame_CostPlate");
            if (frameSprite != null)
            {
                var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                frameGo.transform.SetParent(boxGo.transform, false);
                var fr = (RectTransform)frameGo.transform;
                fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
                var frame = frameGo.GetComponent<Image>();
                frame.sprite = frameSprite;
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = 2.6f; // 모서리 장식이 내용을 가리지 않게 테두리를 얇게
                frame.raycastTarget = false;
            }
            else
            {
                var outline = boxGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.92f, 0.86f, 0.6f, 0.9f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(boxGo.transform, false);
            _tipContent = (RectTransform)contentGo.transform;
            _tipContent.anchorMin = Vector2.zero; _tipContent.anchorMax = Vector2.one;
            _tipContent.offsetMin = _tipContent.offsetMax = Vector2.zero;

            _font = FindKoreanFont("시너지현재명배치단계발동중아직전달성대기조합이상각궁수전사방패병마법사도적힐러");
            _tipSignature = null;
            _tipCanvas.enabled = false;
        }

        private static TMP_FontAsset FindKoreanFont(string sample)
        {
            if (UiFontOverride.Current != null) return UiFontOverride.Current; // 씬 전체 폰트(던파 비트체)가 있으면 그것을 쓴다
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters(sample, out _, true, true)) return font;
            return null;
        }
    }
}
