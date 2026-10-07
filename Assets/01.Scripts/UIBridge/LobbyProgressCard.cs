using OZGL2.Progression;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비 왼쪽 위(레벨 막대 아래)에 「도전 현황」 카드를 띄운다. 지금 고른 난이도의 진행을 보여 준다.
    /// - 마왕 레벨·경험치바·LP/SP (패배해서 1웨이브부터 다시 시작해도 레벨은 그대로 쌓인다)
    /// - 도전 중인 판이 있으면 웨이브 진행바 + 재화·유닛·증강, 없으면 「새 도전」 안내, 잠긴 난이도면 열리는 조건
    /// 난이도 카드를 넘기면 그 난이도의 진행으로 바뀐다. 희수의 어두운 프레임·이름표를 쓰고, 팝업(정렬 100)보다 아래, 로비 본 화면보다 위에 그려진다.
    /// 난이도 선택 화면이 보이지 않을 때(다른 탭을 열었을 때)는 숨는다. 눌림은 받지 않는다.
    /// </summary>
    public sealed class LobbyProgressCard : MonoBehaviour
    {
        [Header("희수 UI 조각(없으면 단색 상자로 대신)")]
        [SerializeField, Tooltip("창 프레임(Frame_WavePreview_Flat, 9분할)")] private Sprite _panelFrame;
        [SerializeField, Tooltip("이름표(Frame_SynergyNameplate_Flat, 9분할)")] private Sprite _namePlate;
        [SerializeField, Tooltip("모서리 마름모(Ornament_Diamond_Flat)")] private Sprite _diamond;
        [SerializeField, Tooltip("카드 왼쪽 위 모서리의 화면 위치(1920×1080 기준)")] private Vector2 _topLeft = new Vector2(44f, 156f);

        private static readonly Color Cream = new Color(0.93f, 0.87f, 0.75f), Gold = new Color(1f, 0.84f, 0.48f), Orange = new Color(1f, 0.62f, 0.28f);

        private UILobbyDifficultySelector _selector;
        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _root, _panel;
        private string _signature = string.Empty;
        private float _nextPoll, _alpha;

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void Update()
        {
            if (_selector == null)
            {
                if (Time.unscaledTime < _nextPoll) return;
                _nextPoll = Time.unscaledTime + 0.5f;
                _selector = FindFirstObjectByType<UILobbyDifficultySelector>(FindObjectsInactive.Include);
                if (_selector == null) return;
            }

            bool visible = IsSelectorVisible();
            _alpha = Mathf.MoveTowards(_alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            if (_canvas != null) { _group.alpha = _alpha; _canvas.enabled = _alpha > 0.01f; }
            if (!visible && _canvas == null) return;

            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.3f;
            if (!visible) return;
            string sig = Signature();
            if (sig == _signature && _panel != null) return;
            _signature = sig;
            Rebuild();
        }

        /// <summary>난이도 선택 화면이 켜져 있고 보이는 상태인가(탭을 바꿔 가려졌으면 숨긴다).</summary>
        private bool IsSelectorVisible()
        {
            if (_selector == null || !_selector.isActiveAndEnabled) return false;
            foreach (var group in _selector.GetComponentsInParent<CanvasGroup>(false))
                if (group.alpha < 0.01f) return false;
            return true;
        }

        private string Signature()
        {
            int index = _selector.SelectedIndex;
            string id = StageClearStore.StageIdOf(index);
            var mawang = MawangXpBridge.Mawang;
            return index + "|" + RunSaveStore.SavedAt(id) + "|" + (RunSaveStore.Has(id) ? 1 : 0) + "|" + StageClearStore.IsCleared(id) + "|" + StageClearStore.IsDifficultyUnlocked(index)
                   + "|" + (mawang != null ? mawang.Level + ":" + mawang.Xp + ":" + mawang.Points : "-") + "|" + SkillTreeStore.SkillPoints + "|" + Screen.height;
        }

        /// <summary>스테이지 ID 끝의 숫자(stage_normal_30 → 30)가 그 스테이지의 전체 웨이브 수다.</summary>
        private static int WaveTotalOf(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return 0;
            int u = stageId.LastIndexOf('_');
            return u >= 0 && int.TryParse(stageId.Substring(u + 1), out int n) ? n : 0;
        }

        // ───────────── 그리기

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            var go = new GameObject("LobbyProgressCard", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _group = go.AddComponent<CanvasGroup>();
            _group.interactable = false; _group.blocksRaycasts = false;
            _root = (RectTransform)go.transform;
        }

        private void Rebuild()
        {
            EnsureCanvas();
            if (_panel != null) Destroy(_panel.gameObject);

            int index = Mathf.Max(0, _selector.SelectedIndex);
            string id = StageClearStore.StageIdOf(index);
            string difficulty = index < StageClearStore.DifficultyNames.Length ? StageClearStore.DifficultyNames[index] : string.Empty;
            bool unlocked = StageClearStore.IsDifficultyUnlocked(index);
            int total = WaveTotalOf(id);
            var save = RunSaveStore.Load(id);
            bool hasRun = unlocked && save != null && save.clearedRounds >= 1 && (total <= 0 || save.clearedRounds < total);

            var mawang = MawangXpBridge.Mawang ?? new MawangLevel();
            TraitMawangSettings.ApplySaved(mawang);

            const float w = 400f, pad = 28f, rowH = 70f;
            float h = 90f + 60f + 14f + (hasRun ? rowH : 40f) + 18f;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.sizeDelta = new Vector2(w, h);
            _panel.anchoredPosition = new Vector2(_topLeft.x, -_topLeft.y);
            var frame = panelGo.GetComponent<Image>();
            frame.raycastTarget = false;
            if (_panelFrame != null) { frame.sprite = _panelFrame; frame.type = Image.Type.Sliced; frame.color = Color.white; }
            else frame.color = new Color(0.08f, 0.04f, 0.06f, 0.94f);

            // 네 모서리 마름모
            if (_diamond != null)
                foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
                {
                    var d = NewImage("Corner", _panel, _diamond, Color.white);
                    var dr = d.rectTransform;
                    dr.anchorMin = dr.anchorMax = corner; dr.pivot = new Vector2(0.5f, 0.5f);
                    dr.sizeDelta = new Vector2(30f, 30f); dr.anchoredPosition = Vector2.zero;
                }

            // 위 가장자리에 걸친 이름표
            var plate = NewImage("Plate", _panel, _namePlate, _namePlate != null ? Color.white : new Color(0.3f, 0.1f, 0.1f, 1f));
            if (_namePlate != null) plate.type = Image.Type.Sliced;
            var pr = plate.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(230f, 64f); pr.anchoredPosition = Vector2.zero;
            var title = NewText("PlateText", pr, 34f, Gold, TextAlignmentOptions.Center);
            title.text = "도전 현황";
            title.enableAutoSizing = true; title.fontSizeMin = 20f; title.fontSizeMax = 34f;
            Stretch(title.rectTransform, 14f, 6f);

            TMP_Text Put(string text, float size, Color color, TextAlignmentOptions align, float cy, float boxW)
            {
                var t = NewText("Text", _panel, size, color, align);
                t.text = text;
                t.enableAutoSizing = true; t.fontSizeMin = size * 0.6f; t.fontSizeMax = size;
                t.overflowMode = TextOverflowModes.Ellipsis;
                var shadow = t.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.75f); shadow.effectDistance = new Vector2(2f, -2f);
                bool left = align == TextAlignmentOptions.Left, right = align == TextAlignmentOptions.Right;
                var r = t.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(left ? 0f : right ? 1f : 0.5f, 0.5f);
                r.sizeDelta = new Vector2(boxW, size * 1.3f);
                r.anchoredPosition = new Vector2(left ? -w * 0.5f + pad : right ? w * 0.5f - pad : 0f, cy);
                return t;
            }

            void Bar(float cy, float ratio)
            {
                var back = NewImage("BarBack", _panel, null, new Color(0f, 0f, 0f, 0.55f));
                var br = back.rectTransform;
                br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
                br.sizeDelta = new Vector2(w - pad * 2f, 13f); br.anchoredPosition = new Vector2(0f, cy);
                var edge = back.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(Cream.r, Cream.g, Cream.b, 0.35f); edge.effectDistance = new Vector2(1f, -1f);
                var fill = NewImage("BarFill", br, null, Orange);
                var fr = fill.rectTransform;
                fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
                fr.offsetMin = new Vector2(2f, 2f); fr.offsetMax = new Vector2(-2f, -2f);
                fill.enabled = ratio > 0.001f;
            }

            void Hairline(float cy)
            {
                var line = NewImage("Hairline", _panel, null, new Color(Cream.r, Cream.g, Cream.b, 0.22f));
                var lr = line.rectTransform;
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(w - pad * 2f, 2f); lr.anchoredPosition = new Vector2(0f, cy);
            }

            float y = h * 0.5f - 90f;

            // 마왕 레벨 · LP/SP · 경험치바
            Put("마왕 Lv " + mawang.Level, 36f, Gold, TextAlignmentOptions.Left, y - 18f, 220f);
            Put("LP " + mawang.Points + "  ·  SP " + SkillTreeStore.SkillPoints, 24f, Cream, TextAlignmentOptions.Right, y - 18f, 190f);
            Bar(y - 50f, mawang.Xp / (float)Mathf.Max(1, mawang.XpToNext));
            y -= 60f;
            Hairline(y - 7f);
            y -= 14f;

            // 지금 고른 난이도의 진행
            Color dim = new Color(Cream.r, Cream.g, Cream.b, 0.75f);
            if (!unlocked)
            {
                Put(StageClearStore.LockedHint(index), 22f, dim, TextAlignmentOptions.Center, y - 20f, w - pad * 2f);
            }
            else if (!hasRun)
            {
                bool cleared = StageClearStore.IsCleared(id);
                Put(difficulty + "  ·  새 도전은 1웨이브부터" + (cleared ? "  (클리어 완료)" : string.Empty), 22f, dim, TextAlignmentOptions.Center, y - 20f, w - pad * 2f);
            }
            else
            {
                int placed = 0;
                foreach (var unit in save.units) if (unit.placed) placed++;
                Put(difficulty, 32f, Gold, TextAlignmentOptions.Left, y - 16f, 140f);
                Put(total > 0 ? save.clearedRounds + " / " + total + " 웨이브" : save.clearedRounds + " 웨이브", 28f, Cream, TextAlignmentOptions.Right, y - 16f, 220f);
                Bar(y - 44f, total > 0 ? save.clearedRounds / (float)total : 1f);
                Put("재화 " + save.currency + "  ·  유닛 " + save.units.Count + "(배치 " + placed + ")  ·  증강 " + save.augments.Count
                    + (save.rewardStage > 0 ? "  ·  보상 대기" : string.Empty), 20f, dim, TextAlignmentOptions.Left, y - 58f, w - pad * 2f);
            }
        }

        // ───────────── 도우미

        private static TMP_Text NewText(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (UiFontOverride.Current != null) text.font = UiFontOverride.Current;
            text.fontSize = size; text.color = color; text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rt, float x, float y)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(x, y); rt.offsetMax = new Vector2(-x, -y);
        }
    }
}
