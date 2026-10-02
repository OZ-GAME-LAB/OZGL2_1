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
        private TMP_Text _tipText;
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

        private void FindRows()
        {
            var byName = new Dictionary<string, Transform>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.gameObject.scene.IsValid() && t.name.StartsWith("Synergy_")) byName[t.name] = t;
            for (int i = 0; i < Slots; i++)
            {
                if (!byName.TryGetValue("Synergy_" + i, out var row)) continue;
                _rows[i] = row.gameObject;
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
            // 내용은 매 프레임 다시 쓴다(유닛을 옮기면 배치 수가 바뀌므로)
            _tipText.text = BuildTooltipText(tracker, _slotDef[slot]);
            _tipText.ForceMeshUpdate();
            Vector2 text = _tipText.GetPreferredValues();
            const float pad = 22f;
            float scale = Mathf.Max(0.8f, Screen.height / 1080f);
            Vector2 size = new Vector2(text.x + pad * 2f, text.y + pad * 2f);
            _tipBox.sizeDelta = size;
            var textRect = (RectTransform)_tipText.transform;
            textRect.sizeDelta = new Vector2(size.x - pad * 2f, size.y - pad * 2f);

            // 시너지 줄의 왼쪽에, 줄과 위쪽을 맞춰 띄운다(화면 밖으로 나가지 않게 보정)
            float x = rowRect.xMin - size.x - 14f * scale;
            float y = rowRect.yMax - size.y;
            x = Mathf.Clamp(x, 8f, Screen.width - size.x - 8f);
            y = Mathf.Clamp(y, 8f, Screen.height - size.y - 8f);
            _tipBox.anchoredPosition = new Vector2(x, y);
            _tipCanvas.enabled = true;
            _hoverSlot = slot;
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

        private string BuildTooltipText(SynergyTracker tracker, SynergyData def)
        {
            int count = tracker.CountOf(def.job);
            int tier = tracker.TierOf(def.job);
            const string gold = "#F2DC8C", gray = "#8C8C8C", white = "#F5F2E8", green = "#9BE59B", sky = "#9CD2FF";
            var sb = new StringBuilder();

            // 제목 + 현재 상태
            sb.Append("<size=130%><b><color=").Append(gold).Append('>').Append(def.displayName).Append(" 시너지</color></b></size>\n");
            sb.Append("<color=").Append(white).Append(">현재 ").Append(count).Append("명 배치 · ")
              .Append(tier >= 2 ? "2단계 발동 중" : tier == 1 ? "1단계 발동 중" : "아직 발동 전").Append("</color>\n\n");

            // 1단계 / 2단계를 줄 머리말과 색으로 분명히 나눈다
            AppendTier(sb, "1단계", sky, def.tier1Threshold, def.tier1Desc, count >= def.tier1Threshold, tier == 1, gray, green);
            sb.Append('\n');
            AppendTier(sb, "2단계", gold, def.tier2Threshold, def.tier2Desc, count >= def.tier2Threshold, tier >= 2, gray, green);

            bool header = false;
            foreach (var combo in tracker.ComboDefs)
            {
                if (combo == null || (combo.jobA != def.job && combo.jobB != def.job)) continue;
                if (!header) { sb.Append("\n\n<color=").Append(gold).Append("><b>조합 시너지</b></color>\n"); header = true; }
                bool active = tracker.IsComboActive(combo);
                sb.Append("<color=").Append(active ? green : gray).Append('>')
                  .Append(active ? "[발동] " : "[대기] ").Append(combo.displayName)
                  .Append(" (").Append(JobName(tracker, combo.jobA)).Append("+").Append(JobName(tracker, combo.jobB))
                  .Append(" 각 ").Append(combo.requiredCountEach).Append("명)\n   ").Append(combo.desc).Append("</color>\n");
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>한 단계 블록: "1단계 · 3명" 머리말 한 줄 + 효과 한 줄. 달성한 단계는 밝게, 아직인 단계는 회색으로 표시한다.</summary>
        private static void AppendTier(StringBuilder sb, string label, string labelColor, int threshold, string desc,
            bool reached, bool currentTier, string gray, string green)
        {
            sb.Append("<color=").Append(labelColor).Append("><b>[").Append(label).Append("] ").Append(threshold).Append("명 이상</b></color>");
            sb.Append("  <color=").Append(reached ? green : gray).Append('>').Append(reached ? (currentTier ? "발동 중" : "달성") : "대기").Append("</color>\n");
            sb.Append("<color=").Append(reached ? green : gray).Append('>').Append("   ").Append(desc).Append("</color>");
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
            bg.color = new Color(0.05f, 0.04f, 0.06f, 0.94f);
            bg.raycastTarget = false;
            var outline = boxGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.92f, 0.86f, 0.6f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(boxGo.transform, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            _font = FindKoreanFont("시너지현재명배치단계발동중아직전달성대기조합이상각궁수전사방패병마법사도적힐러");
            _tipText = textGo.AddComponent<TextMeshProUGUI>();
            if (_font != null) _tipText.font = _font;
            _tipText.fontSize = 24f * Mathf.Max(0.8f, Screen.height / 1080f);
            _tipText.alignment = TextAlignmentOptions.TopLeft;
            _tipText.richText = true;
            _tipText.raycastTarget = false;
            _tipText.textWrappingMode = TextWrappingModes.NoWrap;
            _tipCanvas.enabled = false;
        }

        private static TMP_FontAsset FindKoreanFont(string sample)
        {
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters(sample, out _, true, true)) return font;
            return null;
        }
    }
}
