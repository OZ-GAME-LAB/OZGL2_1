using System.Reflection;
using OZGL2.Grid;
using OZGL2.Grid.Prototype;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 전장에 배치한 유닛(또는 발판)을 보관함으로 끌어 넣는 동작을 눈에 보이게 한다.
    /// - 드래그 중에만 손패 영역 위에 "보관함" 드롭 존을 표시한다. 포인터가 들어가면 색이 바뀌고 문구가 "놓으면 보관"으로 바뀐다.
    /// - 지금까지 전장→보관함 드롭 판정은 눈에 안 보이는 옛 UI Toolkit 보관함(storage-tray)의 위치로 이뤄져서 손패 위치와 어긋날 수 있었다.
    ///   그 옛 보관함 영역을 손패의 드롭 영역과 같은 위치·크기로 맞춰, 표시된 곳에 놓으면 실제로 보관되게 한다.
    /// 팀 파일(GridPrototypeRunner, UIBattleCardHandView)은 수정하지 않고 리플렉션으로만 읽는다.
    /// </summary>
    public sealed class InGameStorageDropHint : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string TrayName = "storage-tray";

        private GridPrototypeRunner _runner;
        private UIBattleCardHandView _hand;
        private RectTransform _dropRect;
        private Canvas _handCanvas;
        private VisualElement _tray;
        private bool _traySyncFailed;
        private float _nextTraySync;

        private Canvas _overlay;
        private RectTransform _box;
        private Image _fill;
        private Outline _outline;
        private TMP_Text _label;
        private TMP_Text _subLabel;

        private void Update()
        {
            Resolve();
            if (_dropRect == null) return;
            SyncLegacyTray();

            bool dragging = IsDraggingPlacedItem();
            if (!dragging)
            {
                if (_overlay != null && _overlay.enabled) _overlay.enabled = false;
                return;
            }
            EnsureOverlay();
            if (_overlay == null) return;
            _overlay.enabled = true;
            UpdateOverlay();
        }

        private void OnDestroy()
        {
            if (_overlay != null) Destroy(_overlay.gameObject);
        }

        private void Resolve()
        {
            if (_runner == null) _runner = FindFirstObjectByType<GridPrototypeRunner>(FindObjectsInactive.Include);
            if (_hand == null) _hand = FindFirstObjectByType<UIBattleCardHandView>(FindObjectsInactive.Include);
            if (_hand != null && _dropRect == null)
            {
                _dropRect = typeof(UIBattleCardHandView).GetField("_dropZone", Private)?.GetValue(_hand) as RectTransform
                            ?? typeof(UIBattleCardHandView).GetField("_viewport", Private)?.GetValue(_hand) as RectTransform;
                if (_dropRect != null) _handCanvas = _dropRect.GetComponentInParent<Canvas>();
            }
        }

        /// <summary>전장에 놓여 있던 유닛/발판을 끌고 있는 중인가 (보관함에서 꺼낸 것을 끄는 중이면 표시하지 않는다).</summary>
        private bool IsDraggingPlacedItem()
        {
            var manager = _runner != null ? _runner.Manager : null;
            if (manager == null || !manager.HasSelection || manager.Phase != eGridPhase.PREPARATION) return false;
            string id = manager.SelectedId;
            if (string.IsNullOrEmpty(id)) return false;
            if (manager.DragKind == eGridDragKind.UNIT)
            {
                foreach (var unit in manager.Units) if (unit.InstanceId == id) return unit.IsPlaced;
            }
            else if (manager.DragKind == eGridDragKind.BLOCK)
            {
                foreach (var block in manager.Blocks) if (block.InstanceId == id) return block.IsPlaced;
            }
            return false;
        }

        // ───────────── 손패 드롭 영역(화면 좌표)

        private bool TryGetScreenRect(out Rect rect)
        {
            rect = default;
            if (_dropRect == null || !_dropRect.gameObject.activeInHierarchy) return false;
            var corners = new Vector3[4];
            _dropRect.GetWorldCorners(corners);
            Camera cam = _handCanvas != null && _handCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _handCanvas.worldCamera : null;
            Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            rect = Rect.MinMaxRect(Mathf.Min(bl.x, tr.x), Mathf.Min(bl.y, tr.y), Mathf.Max(bl.x, tr.x), Mathf.Max(bl.y, tr.y));
            return rect.width > 1f && rect.height > 1f;
        }

        // ───────────── 옛 보관함(드롭 판정) 위치를 손패와 맞춘다

        private void SyncLegacyTray()
        {
            if (_traySyncFailed || _runner == null || Time.unscaledTime < _nextTraySync) return;
            _nextTraySync = Time.unscaledTime + 0.25f;
            if (!TryGetScreenRect(out var rect)) return;
            if (_tray == null || _tray.panel == null)
            {
                var root = _runner.GetType().GetField("_root", Private)?.GetValue(_runner) as VisualElement;
                _tray = root?.Q<VisualElement>(TrayName);
                if (_tray == null || _tray.panel == null) return;
            }
            var panel = _tray.panel;
            Vector2 tl = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(rect.xMin, rect.yMax));
            Vector2 br = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(rect.xMax, rect.yMin));
            var parent = _tray.parent;
            if (parent == null) return;
            Vector2 ltl = parent.WorldToLocal(tl), lbr = parent.WorldToLocal(br);

            // 이전 동기화 결과 검증: 트레이의 실제 위치(worldBound)가 목표 중심과 크게 다르면 좌표 변환이 맞지 않는 것이므로 되돌리고 중단한다.
            Rect wb = _tray.worldBound;
            if (_traySyncApplied && float.IsFinite(wb.x) && wb.width > 0f &&
                Vector2.Distance(wb.center, (tl + br) * 0.5f) > 12f)
            {
                _tray.style.position = Position.Relative;
                _tray.style.left = _tray.style.top = _tray.style.width = _tray.style.height = StyleKeyword.Null;
                _traySyncFailed = true;
                Debug.LogWarning("보관함 드롭 영역 맞춤 실패: 좌표 변환이 어긋나 원래 위치로 되돌렸습니다.", this);
                return;
            }
            _tray.style.position = Position.Absolute;
            _tray.style.left = ltl.x;
            _tray.style.top = ltl.y;
            _tray.style.width = lbr.x - ltl.x;
            _tray.style.height = lbr.y - ltl.y;
            _traySyncApplied = true;
        }

        private bool _traySyncApplied;

        // ───────────── 드롭 존 표시 (uGUI 오버레이)

        private void EnsureOverlay()
        {
            if (_overlay != null) return;
            var go = new GameObject("StorageDropHint", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _overlay = go.AddComponent<Canvas>();
            _overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlay.sortingOrder = 15; // 손패(10~11)보다 위, 팝업(100)보다 아래
            go.AddComponent<GraphicRaycaster>().enabled = false;

            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(go.transform, false);
            _box = (RectTransform)boxGo.transform;
            _box.anchorMin = _box.anchorMax = Vector2.zero;
            _box.pivot = Vector2.zero;
            _fill = boxGo.GetComponent<Image>();
            _fill.raycastTarget = false;
            _outline = boxGo.AddComponent<Outline>();
            _outline.effectDistance = new Vector2(4f, -4f);

            var font = FindKoreanFont("보관함에놓으면보관돼요여기로드래그");
            _label = CreateText(boxGo.transform, "Label", font, 30f, new Vector2(0f, 8f));
            _subLabel = CreateText(boxGo.transform, "SubLabel", font, 20f, new Vector2(0f, -26f));
            _overlay.enabled = false;
        }

        private static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, float size, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 50f);
            rt.anchoredPosition = offset;
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static TMP_FontAsset FindKoreanFont(string sample)
        {
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters(sample, out _, true, true)) return font;
            return null;
        }

        private void UpdateOverlay()
        {
            if (!TryGetScreenRect(out var rect)) { _overlay.enabled = false; return; }
            _box.anchoredPosition = rect.position;
            _box.sizeDelta = rect.size;

            Vector2 pointer = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
            bool hot = rect.Contains(pointer);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (hot ? 9f : 4f));

            Color idleFill = new Color(0.35f, 0.55f, 0.95f, Mathf.Lerp(0.16f, 0.30f, pulse));
            Color idleLine = new Color(0.75f, 0.88f, 1f, Mathf.Lerp(0.55f, 1f, pulse));
            Color hotFill = new Color(1f, 0.82f, 0.25f, Mathf.Lerp(0.38f, 0.52f, pulse));
            Color hotLine = new Color(1f, 0.95f, 0.6f, 1f);
            _fill.color = hot ? hotFill : idleFill;
            _outline.effectColor = hot ? hotLine : idleLine;
            _label.text = hot ? "놓으면 보관돼요" : "보관함";
            _subLabel.text = hot ? "마우스를 놓으세요" : "여기로 드래그해서 보관";
            _label.color = hot ? new Color(1f, 0.97f, 0.8f) : new Color(0.9f, 0.96f, 1f);
            _subLabel.color = _label.color;
        }
    }
}
