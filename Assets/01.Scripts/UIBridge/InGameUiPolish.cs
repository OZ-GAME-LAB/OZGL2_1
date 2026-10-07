using System.Collections.Generic;
using System.Reflection;
using OZGL2.InGame;
using OZGL2.UIFlow;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// UI 프리팹을 수정하지 않고 실행 중에 다듬는 모음.
    /// - 배치 카드의 용도를 채운다. 유닛 정보는 손패 어댑터·프리팹 설정을 사용한다.
    /// - 상단 바: 웨이브 아이콘 자리가 흰 네모로 나오던 것에 해골 아이콘을 넣고, 웨이브 펼침 버튼의 분홍색을 바 색에 맞춘다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class InGameUiPolish : MonoBehaviour
    {
        [SerializeField, Tooltip("배치 카드 설명을 채운다. 유닛 정보는 손패 어댑터가 관리한다.")] private bool _fillCardInfo = true;
        [SerializeField, Tooltip("카드의 성급 숫자를 별 개수로 바꾼다(희수 UI의 별 배지를 쓰면 끈다)")] private bool _starRows = false;
        [SerializeField, Tooltip("웨이브 아이콘·펼침 단추 모양을 손본다(희수 UI를 그대로 쓰면 끈다)")] private bool _polishWaveHud = false;
        [SerializeField] private Sprite _waveIcon;
        [SerializeField] private Color _titleColor = new Color(0.95f, 0.84f, 0.5f, 1f);
        [SerializeField] private Color _bodyColor = new Color(0.96f, 0.93f, 0.86f, 1f);
        [SerializeField] private Color _toggleColor = new Color(0.42f, 0.13f, 0.16f, 1f);

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly System.Type CardType = typeof(UIBattlePreparationCardView);
        private static readonly FieldInfo FTitle = CardType.GetField("_titleText", Priv), FRank = CardType.GetField("_rankText", Priv);
        private static readonly FieldInfo FAreaT = CardType.GetField("_areaTitleText", Priv), FAreaD = CardType.GetField("_areaDescriptionText", Priv);
        private static readonly FieldInfo FToggle = typeof(UIWavePreviewDisclosure).GetField("_toggleButton", Priv);

        private InGamePrototypeBootstrap _bootstrap;
        private float _next;
        private bool _hudFixed;
        private float _pendingToggleAt = -1f;
        private UIWavePreviewDisclosure _pendingDisclosure;
        private bool _pendingWasExpanded;

        private void LateUpdate()
        {
            if (_polishWaveHud) WatchToggleClick();
            if (_starRows && FRank != null)
                foreach (var card in FindObjectsByType<UIBattlePreparationCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    ShowRankStars(card);
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.05f;
            if (_fillCardInfo) PolishCards();
            if (_polishWaveHud && !_hudFixed) PolishHud();
        }

        // ───────────── 손패 카드 설명

        private void PolishCards()
        {
            if (FTitle == null || FAreaT == null || FAreaD == null) return;
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();

            foreach (var card in FindObjectsByType<UIBattlePreparationCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // 이름 매칭 대신 영역 설명 참조로 판별해 유닛/도감 문구를 덮어쓰지 않는다.
                if (!IsCardText(FAreaT.GetValue(card)) || !IsCardText(FAreaD.GetValue(card))) continue;
                PolishLandCard(card, ReadCardText(FTitle.GetValue(card)));
            }
        }

        private const string ExpansionTitle = "배치 영역 확장";

        /// <summary>카드의 영역 제목에서 이번에 늘어나는 칸 수를 읽는다. 없으면 기본 조각의 칸 수.</summary>
        private static int ExpansionCells(UIBattlePreparationCardView card, OZGL2.Grid.GridManager grid)
        {
            var text = ReadCardText(FAreaT?.GetValue(card));
            if (!string.IsNullOrEmpty(text))
            {
                var m = System.Text.RegularExpressions.Regex.Match(text, "(\\d+)칸");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int n) && n > 0) return n;
            }
            return grid.Definition.Expansion.Cells.Count;
        }

        /// <summary>
        /// 배치 카드 두 종류의 역할을 카드 안에서 분명히 알려 준다.
        /// - 「배치 영역 확장」(웨이브 보상 3택 중 하나): 배치 영역의 증가 칸수와 사용 방법을 안내한다.
        /// - 발판 카드(유닛을 고르면 함께 오는 것): 그 유닛이 설 바닥 조각 → 칸 위에 놓은 뒤 유닛을 올리는 것.
        /// </summary>
        private void PolishLandCard(UIBattlePreparationCardView card, string title)
        {
            if (FAreaT == null || FAreaD == null) return;
            var grid = _bootstrap != null && _bootstrap.GridSession != null ? _bootstrap.GridSession.Grid : null;
            string areaTitle, body;
            bool isExpansion = title == ExpansionTitle || title == "발판 확장";
            if (isExpansion)
            {
                if (grid == null) return;
                int add = ExpansionCells(card, grid);
                areaTitle = "배치 영역 +" + add + "칸";
                body = "확장할 위치에 배치해\n유닛을 놓을 공간을 넓힙니다.";
            }
            else
            {
                if (title != "배치 발판") card.SetTitle("배치 발판");
                areaTitle = "유닛이 설 자리";
                body = "이 모양의 바닥 조각이에요.\n영역 위에 먼저 놓고, 그 위에 유닛을 올리세요.\n(이미 놓은 발판과 이어서 놓아야 해요)";
            }
            card.SetAreaDescription(areaTitle, body);
            if (isExpansion) return;
            Style(FAreaT.GetValue(card) as Graphic, _titleColor, TitleSize - 2, TextAnchor.MiddleCenter);
            Style(FAreaD.GetValue(card) as Graphic, _bodyColor, BodySize, TextAnchor.MiddleLeft);
        }

        /// <summary>
        /// 카드의 성급(1·2·3성)을 숫자가 든 별 하나 대신 "별 개수"로 보여 준다. 1성은 별 1개, 2성은 2개, 3성은 3개.
        /// 팀 카드의 별 그림(StarBadge)과 숫자는 숨기고, 같은 모서리에 작은 별 줄을 만든다(오른쪽 끝을 별 그림에 맞춤).
        /// 숫자가 없는 카드(배치 영역 확장)는 원래 별 장식을 그대로 둔다.
        /// </summary>
        private static void ShowRankStars(UIBattlePreparationCardView card)
        {
            var rank = FRank.GetValue(card) as Graphic;
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
            bool isUnit = int.TryParse(ReadCardText(rank), out n) && n >= 1;
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

        // 카드 설계 좌표(폭 640 기준)의 글자 크기
        private const int TitleSize = 26, BodySize = 21;

        private static bool IsCardText(object target) => target is Text || target is TMP_Text;

        private static string ReadCardText(object target)
        {
            if (target is TMP_Text tmp) return tmp.text;
            return (target as Text)?.text;
        }

        private static void Style(Graphic target, Color color, int size, TextAnchor align)
        {
            if (target == null) return;
            target.color = color;
            if (target is TMP_Text tmp)
            {
                tmp.richText = false;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.overflowMode = TextOverflowModes.Truncate;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = Mathf.RoundToInt(size * 0.6f);
                tmp.fontSizeMax = size;
                tmp.alignment = align == TextAnchor.MiddleCenter ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
                tmp.fontStyle = FontStyles.Normal;
                return;
            }
            if (!(target is Text text)) return;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.RoundToInt(size * 0.6f);
            text.resizeTextMaxSize = size;
            text.alignment = align;
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
