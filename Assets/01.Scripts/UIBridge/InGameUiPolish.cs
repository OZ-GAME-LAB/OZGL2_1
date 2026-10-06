using System.Collections.Generic;
using System.Reflection;
using OZGL2.InGame;
using OZGL2.Synergy;
using OZGL2.UIFlow;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// UI 프리팹을 수정하지 않고 실행 중에 다듬는 모음.
    /// - 손패 카드: 시너지(직업)·특기 설명 칸이 비어 있던 것을 마왕군 데이터로 채우고, 잘 읽히게 색·크기를 맞춘다.
    ///   (카드 칸은 있는데 손패 어댑터가 trait/skill 문구를 넘기지 않아 빈 상자로 보였다.)
    /// - 상단 바: 웨이브 아이콘 자리가 흰 네모로 나오던 것에 해골 아이콘을 넣고, 웨이브 펼침 버튼의 분홍색을 바 색에 맞춘다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class InGameUiPolish : MonoBehaviour
    {
        [SerializeField] private Sprite _waveIcon;
        [SerializeField] private Color _titleColor = new Color(0.95f, 0.84f, 0.5f, 1f);
        [SerializeField] private Color _bodyColor = new Color(0.96f, 0.93f, 0.86f, 1f);
        [SerializeField] private Color _toggleColor = new Color(0.42f, 0.13f, 0.16f, 1f);

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly System.Type CardType = typeof(UIBattlePreparationCardView);
        private static readonly FieldInfo FTitle = CardType.GetField("_titleText", Priv), FRank = CardType.GetField("_rankText", Priv),
            FTraitT = CardType.GetField("_traitTitleText", Priv), FTraitD = CardType.GetField("_traitDescriptionText", Priv),
            FSkillT = CardType.GetField("_skillTitleText", Priv), FSkillD = CardType.GetField("_skillDescriptionText", Priv);
        private static readonly FieldInfo FToggle = typeof(UIWavePreviewDisclosure).GetField("_toggleButton", Priv);

        private InGamePrototypeBootstrap _bootstrap;
        private RealSynergySync _sync;
        private UIUnitCatalogSO _catalog;
        private float _next;
        private bool _hudFixed;
        private float _pendingToggleAt = -1f;
        private UIWavePreviewDisclosure _pendingDisclosure;
        private bool _pendingWasExpanded;

        private void LateUpdate()
        {
            WatchToggleClick();
            if (FRank != null)
                foreach (var card in FindObjectsByType<UIBattlePreparationCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    ShowRankStars(card);
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            PolishCards();
            if (!_hudFixed) PolishHud();
        }

        // ───────────── 손패 카드 설명

        private void PolishCards()
        {
            if (FTitle == null || FRank == null || FTraitT == null || FTraitD == null || FSkillT == null || FSkillD == null) return;
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (_sync == null) _sync = FindFirstObjectByType<RealSynergySync>();
            if (_catalog == null)
                foreach (var c in Resources.FindObjectsOfTypeAll<UIUnitCatalogSO>()) { _catalog = c; break; }
            var armyCatalog = _bootstrap != null && _bootstrap.Config != null ? _bootstrap.Config.DemonArmyCatalog : null;
            if (_catalog == null || armyCatalog == null || _sync == null || _sync.Synergy == null) return;

            foreach (var card in FindObjectsByType<UIBattlePreparationCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var title = (FTitle.GetValue(card) as Text)?.text;
                string unitId = UnitIdByName(title);
                if (unitId == null) continue; // 유닛 카드가 아니면(배치 영역 확장 등) 건드리지 않는다

                int star = 1;
                int.TryParse((FRank.GetValue(card) as Text)?.text, out star);
                star = Mathf.Clamp(star, 1, 3);
                var prefab = armyCatalog.FindPrefab(unitId, star);
                var stat = prefab != null ? prefab.statData : null;
                if (stat == null) continue;

                var def = _sync.Synergy.Def(stat.job);
                string traitTitle = def != null ? def.displayName : stat.job.ToString();
                string traitDesc = def != null
                    ? def.tier1Threshold + "명  " + Short(def, def.tier1Desc) + "\n" + def.tier2Threshold + "명  " + Short(def, def.tier2Desc)
                    : string.Empty;

                bool healer = stat.healAmount > 0f;
                string skillTitle = healer ? "치유" : (stat.attackRange <= 1.6f ? "근접" : "원거리");
                string skillDesc = (healer ? "회복 " + stat.healAmount.ToString("0.#") + " · " : "") + "사거리 " + stat.attackRange.ToString("0.#") + " · 공속 " + stat.attackSpeed.ToString("0.0");
                string special = Special(stat);
                if (!string.IsNullOrEmpty(special)) skillDesc += "\n2성  " + special;

                card.SetTrait(traitTitle, traitDesc);
                card.SetSkill(skillTitle, skillDesc);
                Style(FTraitT.GetValue(card) as Text, _titleColor, 14, 30);
                Style(FTraitD.GetValue(card) as Text, _bodyColor, 12, 28);
                Style(FSkillT.GetValue(card) as Text, _titleColor, 14, 30);
                Style(FSkillD.GetValue(card) as Text, _bodyColor, 12, 28);
            }
        }

        /// <summary>
        /// 카드의 성급(1·2·3성)을 숫자가 든 별 하나 대신 "별 개수"로 보여 준다. 1성은 별 1개, 2성은 2개, 3성은 3개.
        /// 팀 카드의 별 그림(StarBadge)과 숫자는 숨기고, 같은 모서리에 작은 별 줄을 만든다(오른쪽 끝을 별 그림에 맞춤).
        /// 숫자가 없는 카드(배치 영역 확장)는 원래 별 장식을 그대로 둔다.
        /// </summary>
        private static void ShowRankStars(UIBattlePreparationCardView card)
        {
            var rank = FRank.GetValue(card) as Text;
            var star = card.transform.Find("StarBadge") as RectTransform;
            if (rank == null || star == null) return;
            var parent = star.parent as RectTransform;
            if (parent == null) return;

            // 이전 방식(별 안의 숫자)이 남아 있으면 지운다
            var oldNumber = star.Find("RankNumber");
            if (oldNumber != null) Object.Destroy(oldNumber.gameObject);

            var starImage = star.GetComponent<Image>();
            var row = parent.Find("RankStars") as RectTransform;

            int n;
            bool isUnit = int.TryParse(rank.text, out n) && n >= 1;
            if (!isUnit)
            {
                if (row != null) row.gameObject.SetActive(false);
                if (starImage != null && !starImage.enabled) starImage.enabled = true;
                return;
            }
            n = Mathf.Clamp(n, 1, 3);
            rank.enabled = false;
            if (starImage != null) starImage.enabled = false;
            if (starImage == null || starImage.sprite == null) return;

            if (row == null)
            {
                var go = new GameObject("RankStars", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                row = (RectTransform)go.transform;
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.pivot = new Vector2(1f, 0.5f);
            }
            row.gameObject.SetActive(true);
            row.SetAsLastSibling();

            // 별 줄의 오른쪽 끝·세로 가운데를 원래 별 그림의 오른쪽 끝·가운데에 둔다 (카드 안쪽 좌표)
            Vector3 right = parent.InverseTransformPoint(star.TransformPoint(new Vector3(star.rect.xMax, star.rect.center.y, 0f)));
            row.localPosition = right;
            float size = star.rect.height * 0.5f;
            float step = size * 0.86f;
            row.sizeDelta = new Vector2(size + step * (n - 1), size);

            // 별 개수만큼만 만들고, 개수가 바뀌면 다시 만든다
            if (row.childCount != n)
            {
                for (int i = row.childCount - 1; i >= 0; i--) Object.Destroy(row.GetChild(i).gameObject);
                for (int i = 0; i < n; i++)
                {
                    var sgo = new GameObject("Star" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    sgo.transform.SetParent(row, false);
                    var img = sgo.GetComponent<Image>();
                    img.sprite = starImage.sprite;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                }
            }
            for (int i = 0; i < n; i++)
            {
                var rt = (RectTransform)row.GetChild(i);
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(-(n - 1 - i) * step, 0f);
            }
        }

        private string UnitIdByName(string title)
        {
            if (string.IsNullOrEmpty(title) || _catalog == null) return null;
            foreach (var entry in _catalog.Entries)
                if (entry != null && entry.DisplayName == title && entry.Id.StartsWith("unit.M_")) return entry.Id.Substring(5);
            return null;
        }

        private static string Short(SynergyData def, string desc)
        {
            if (string.IsNullOrEmpty(desc)) return string.Empty;
            string prefix = def.displayName + " ";
            return desc.StartsWith(prefix) ? desc.Substring(prefix.Length) : desc;
        }

        private static string Special(UnitStatData stat)
        {
            if (stat.comboStunAttackInterval > 0) return stat.comboStunAttackInterval + "번째 공격 기절";
            if (stat.executeDamageBonusPerMissingHealth > 0f) return "잃은 체력만큼 추가 피해";
            if (stat.targetLowestHealthEnemy) return "체력 낮은 적 우선";
            return null;
        }

        private static void Style(Text text, Color color, int minSize, int maxSize)
        {
            if (text == null) return;
            text.color = color;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.fontStyle = FontStyle.Normal;
        }

        // ───────────── 웨이브 펼침/접힘 버튼

        /// <summary>
        /// 화살표 버튼을 눌러도 패널이 접히지 않는다는 보고가 있어, 클릭이 버튼에 닿지 않은 경우를 대비한다.
        /// 화살표(또는 웨이브 글자) 위에서 마우스를 눌렀는데 잠시 뒤에도 패널 상태가 그대로면 직접 토글한다.
        /// 원래 버튼이 정상 동작하면 상태가 이미 바뀌어 있으므로 아무것도 하지 않는다(이중 토글 없음).
        /// </summary>
        private void WatchToggleClick()
        {
            var mouse = Mouse.current;
            if (mouse == null || FToggle == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 pointer = mouse.position.ReadValue();
                foreach (var disclosure in FindObjectsByType<UIWavePreviewDisclosure>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    var button = FToggle.GetValue(disclosure) as Button;
                    var rect = button != null ? button.GetComponent<RectTransform>() : null;
                    if (rect == null || !rect.gameObject.activeInHierarchy) continue;
                    var canvas = rect.GetComponentInParent<Canvas>();
                    Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                    if (!RectTransformUtility.RectangleContainsScreenPoint(rect, pointer, cam)) continue;
                    _pendingDisclosure = disclosure;
                    _pendingWasExpanded = disclosure.IsExpanded;
                    _pendingToggleAt = Time.unscaledTime + 0.2f;
                    break;
                }
            }

            if (_pendingToggleAt > 0f && Time.unscaledTime >= _pendingToggleAt)
            {
                _pendingToggleAt = -1f;
                var d = _pendingDisclosure;
                _pendingDisclosure = null;
                if (d != null && d.IsExpanded == _pendingWasExpanded && !d.IsTransitioning)
                {
                    Debug.Log("[UI] 웨이브 펼침 버튼 클릭이 버튼에 전달되지 않아 직접 토글합니다.");
                    d.Toggle();
                }
            }
        }

        // ───────────── 상단 바

        private Sprite FindSprite(string name)
        {
            foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>())
                if (sp != null && sp.name == name) return sp;
            return null;
        }

        /// <summary>
        /// 웨이브 펼침 탭을 다듬는다: 분홍 네모 대신 다른 마름모 버튼들과 같은 장식 틀(붉은 마름모)로 바꾸고, 바 높이 안에 들어오게 크기를 맞춘다.
        /// 웨이브 글자는 새 폰트로 폭이 넓어져 탭 뒤로 잘리므로 칸 안에 맞게 줄인다.
        /// </summary>
        private void RestyleToggle(Button button, Image image)
        {
            var rt = button.GetComponent<RectTransform>();
            if (image != null && rt != null)
            {
                var frame = FindSprite("Frame_DiamondRed");
                Vector3[] before = new Vector3[4];
                rt.GetWorldCorners(before);
                Vector3 oldCenter = (before[0] + before[2]) * 0.5f;
                if (frame != null)
                {
                    image.sprite = frame;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                }
                image.color = Color.white;
                rt.sizeDelta = new Vector2(66f, 66f);
                Vector3[] after = new Vector3[4];
                rt.GetWorldCorners(after);
                rt.position += oldCenter - (after[0] + after[2]) * 0.5f; // 원래 중심 그대로
                foreach (var arrow in button.GetComponentsInChildren<Image>(true))
                    if (arrow != image) arrow.color = new Color(1f, 0.93f, 0.7f, 1f);
            }

            foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
            {
                if (!text.gameObject.scene.IsValid() || text.name != "WaveText") continue;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 20;
                text.resizeTextMaxSize = Mathf.Max(24, text.fontSize);
                text.alignment = TextAnchor.MiddleLeft;
            }
        }

        private void PolishHud()
        {
            bool icon = false, toggle = false;
            foreach (var image in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (!image.gameObject.scene.IsValid() || image.name != "WaveIcon") continue;
                if (image.sprite == null && _waveIcon != null)
                {
                    image.sprite = _waveIcon;
                    image.preserveAspect = true;
                    image.color = Color.white;
                }
                icon = true;
                break;
            }
            if (FToggle != null)
            {
                foreach (var disclosure in FindObjectsByType<UIWavePreviewDisclosure>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var button = FToggle.GetValue(disclosure) as Button;
                    var graphic = button != null ? button.targetGraphic : null;
                    if (graphic == null) continue;
                    RestyleToggle(button, graphic as Image);
                    toggle = true;
                }
            }
            _hudFixed = icon && toggle;
        }
    }
}
