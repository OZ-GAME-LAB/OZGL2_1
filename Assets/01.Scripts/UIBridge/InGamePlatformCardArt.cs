using System.Collections.Generic;
using System.Reflection;
using OZGL2.UIFlow;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 「배치 발판」 카드의 그림을 확장 카드와 같은 결의 흙 타일로 바꾼다. 원래는 짙은 격자 위에 밋밋한 갈색 칸이 칠해져 있었다.
    /// 발판의 모양은 그대로 두고(1성은 1칸, 2성은 2칸, 3성은 4칸 모양), 차지한 칸마다 금테 두른 흙 타일 한 장씩을 놓는다.
    /// 격자 선은 숨기고, 모양이 카드 그림 영역(Artwork)의 정확히 한가운데에 오도록 칸을 키워서 옮긴다. 카드 프리팹과 팀 코드는 건드리지 않고 실행 중에 칸의 그림만 바꾼다.
    /// 확장·유닛 카드는 이 칸 격자를 쓰지 않으므로(그림이 따로 있다) 영향이 없다.
    /// </summary>
    public sealed class InGamePlatformCardArt : MonoBehaviour
    {
        [SerializeField, Tooltip("한 칸짜리 흙 타일(Platform_Tile)")] private Sprite _tile;
        [SerializeField, Range(1f, 3f), Tooltip("한 칸을 원래 격자 칸보다 얼마나까지 키울지")] private float _maxScale = 2.2f;

        private struct Original { public Vector2 pos, size; }

        private readonly Dictionary<RectTransform, Original> _originals = new Dictionary<RectTransform, Original>();
        private float _next;

        private void LateUpdate()
        {
            if (_tile == null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.15f;
            foreach (var card in FindObjectsByType<UIBattlePreparationCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                Style(card);
        }

        private static readonly FieldInfo FArtwork = typeof(UIBattlePreparationCardView).GetField("_artwork", BindingFlags.Instance | BindingFlags.NonPublic);

        private void Style(UIBattlePreparationCardView card)
        {
            var grid = card.transform.Find("FootprintGrid") as RectTransform;
            if (grid == null || !grid.gameObject.activeInHierarchy) return;

            var cells = new List<(RectTransform rt, Image img)>();
            foreach (Transform child in grid)
            {
                if (child.name.StartsWith("ColumnLine_") || child.name.StartsWith("RowLine_")) { if (child.gameObject.activeSelf) child.gameObject.SetActive(false); continue; }
                if (!child.name.StartsWith("Cell_")) continue;
                var rt = child as RectTransform;
                var img = child.GetComponent<Image>();
                if (rt == null || img == null || rt.anchorMin != rt.anchorMax) continue; // 점 앵커인 칸만 다룬다
                if (!_originals.ContainsKey(rt)) _originals[rt] = new Original { pos = rt.anchoredPosition, size = rt.sizeDelta };
                cells.Add((rt, img));
            }
            if (cells.Count == 0) return;

            // 먼저 모든 칸을 원래 자리·크기로 되돌린 뒤(몇 번을 맞춰도 같은 결과), 켜진 칸의 범위를 격자 좌표로 잰다
            foreach (var (rt, _) in cells) { var o = _originals[rt]; rt.anchoredPosition = o.pos; rt.sizeDelta = o.size; }
            Vector2 allMin = new Vector2(float.MaxValue, float.MaxValue), allMax = new Vector2(float.MinValue, float.MinValue);
            Vector2 onMin = allMin, onMax = allMax;
            bool any = false;
            foreach (var (rt, _) in cells)
            {
                Vector2 c = LocalCenter(grid, rt), half = rt.rect.size * 0.5f;
                allMin = Vector2.Min(allMin, c - half); allMax = Vector2.Max(allMax, c + half);
                if (!rt.gameObject.activeSelf) continue;
                any = true;
                onMin = Vector2.Min(onMin, c - half); onMax = Vector2.Max(onMax, c + half);
            }
            if (!any) return;

            // 놓을 자리: 카드의 그림 영역(Artwork) 한가운데. 없으면 격자 한가운데.
            Vector2 target = (allMin + allMax) * 0.5f, area = allMax - allMin;
            var art = FArtwork != null ? FArtwork.GetValue(card) as Image : null;
            if (art != null && art.rectTransform.rect.width > 1f)
            {
                var ar = art.rectTransform;
                target = LocalPoint(grid, ar, ar.rect.center);
                float ratio = ar.lossyScale.x / Mathf.Max(0.0001f, grid.lossyScale.x);
                area = new Vector2(ar.rect.width * ratio * 0.8f, ar.rect.height * ratio * 0.8f);   // 가장자리에서 조금 떨어뜨린다
            }
            Vector2 onCenter = (onMin + onMax) * 0.5f, onSize = onMax - onMin;
            float scale = Mathf.Min(_maxScale, area.x / Mathf.Max(1f, onSize.x), area.y / Mathf.Max(1f, onSize.y));

            foreach (var (rt, img) in cells)
            {
                if (!rt.gameObject.activeSelf) continue;
                img.sprite = _tile;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.color = Color.white;
                img.raycastTarget = false;
                Vector2 newCenter = target + (LocalCenter(grid, rt) - onCenter) * scale;
                rt.sizeDelta = _originals[rt].size * scale;
                rt.anchoredPosition += newCenter - LocalCenter(grid, rt);   // 크기를 바꾼 뒤의 가운데와 원하는 가운데의 차이만큼 옮긴다
            }
        }

        /// <summary>칸의 가운데를 격자(부모)의 로컬 좌표로.</summary>
        private static Vector2 LocalCenter(RectTransform grid, RectTransform cell) => LocalPoint(grid, cell, cell.rect.center);

        private static Vector2 LocalPoint(RectTransform grid, RectTransform from, Vector2 point) => grid.InverseTransformPoint(from.TransformPoint(point));
    }
}
