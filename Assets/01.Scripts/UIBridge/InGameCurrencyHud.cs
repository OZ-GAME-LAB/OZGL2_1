using System.Collections.Generic;
using System.Reflection;
using OZGL2.Grid;
using OZGL2.InGame;
using OZGL2.UIFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 인게임 재화(불꽃)와 리롤.
    /// - 용사를 처치할 때마다 처치 보상(UnitBase.OnHeroKilled의 보상 값 × _rewardScale)만큼 재화가 쌓인다.
    /// - 왼쪽 아래 재화 표시가 바로 갱신되고, 얻을 때마다 아이콘이 살짝 커졌다 돌아오며, 쓰러진 자리에서 "+N"이 떠오른다.
    /// - 리롤 버튼: 라운드 보상(유닛 3택 1) 카드를 고르는 중에 재화를 내고 후보 카드를 다시 뽑는다. 재화가 모자라거나 보상을 고르는 중이 아니면
    ///   안내 문구가 뜨고 아무 일도 일어나지 않는다. 새 판(재도전 포함)이 시작되면 재화는 시작값으로 돌아간다.
    /// UI 스크립트와 보상 코드(StageGridRewards)는 수정하지 않고 리플렉션과 공개 API로만 다룬다.
    /// </summary>
    public sealed class InGameCurrencyHud : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SerializeField, Min(0)] private int _startAmount = 0;
        [SerializeField, Min(0f)] private float _rewardScale = 1f;
        [SerializeField, Min(0.1f)] private float _floatSeconds = 0.9f;
        [Header("리롤")]
        [SerializeField, Min(0)] private int _rerollCost = 100;
        [Tooltip("같은 보상에서 리롤할 때마다 비용이 이만큼 늘어난다. 0이면 항상 같은 비용.")]
        [SerializeField, Min(0)] private int _rerollCostStep = 0;

        private sealed class Floating
        {
            public RectTransform Rect;
            public TMP_Text Text;
            public Vector2 Start;
            public float Age;
        }

        private UIBattleMutedPreviewView _view;
        private InGamePrototypeBootstrap _bootstrap;
        private UIGridStorageHandAdapter _handAdapter;
        private object _stage;
        private RectTransform _pulseTarget;
        private Button _rerollButton;
        private TMP_Text _rerollCostLabel;
        private float _pulse;
        private int _balance;
        private int _rerollCount;
        private string _rerollRequestId;
        private bool _pushed;
        private bool _buttonHooked;
        private Canvas _canvas;
        private TMP_FontAsset _font;
        private readonly List<Floating> _floats = new List<Floating>();

        public int Balance => _balance;
        private int CurrentRerollCost => _rerollCost + _rerollCostStep * _rerollCount;

        private void OnEnable()
        {
            _balance = _startAmount;
            _pushed = false;
            UnitBase.OnHeroKilled += OnHeroKilled;
        }

        private void OnDisable()
        {
            UnitBase.OnHeroKilled -= OnHeroKilled;
            if (_rerollButton != null && _buttonHooked) _rerollButton.onClick.RemoveListener(OnRerollClicked);
            _buttonHooked = false;
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            _floats.Clear();
        }

        private void Update()
        {
            if (_view == null) _view = FindFirstObjectByType<UIBattleMutedPreviewView>(FindObjectsInactive.Include);
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            if (_view == null) return;
            HookRerollButton();

            // 새 판(재도전 포함)이 시작되면 재화를 시작값으로 되돌린다.
            var stage = _bootstrap != null ? _bootstrap.Stage : null;
            if (stage != null && !ReferenceEquals(stage, _stage))
            {
                _stage = stage;
                _balance = _startAmount;
                _pushed = false;
            }
            TrackRewardRequest();
            if (!_pushed) Push();
            RefreshRerollState();
            UpdatePulse();
            UpdateFloats();
        }

        private void Push()
        {
            if (_view == null) return;
            _view.SetCost(_balance, CurrentRerollCost);
            _pushed = true;
        }

        private void OnHeroKilled(UnitBase hero, int reward)
        {
            int gain = Mathf.Max(1, Mathf.RoundToInt(reward * _rewardScale));
            _balance += gain;
            Push();
            _pulse = 0.3f;
            if (hero != null) SpawnFloating(WorldToScreen(hero.transform.position + Vector3.up * 0.4f), "+" + gain, new Color(1f, 0.86f, 0.35f));
        }

        // ───────────── 리롤

        private void HookRerollButton()
        {
            if (_buttonHooked && _rerollButton != null) return;
            foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (!button.gameObject.scene.IsValid() || button.gameObject.name != "Button_Reroll") continue;
                _rerollButton = button;
                _rerollButton.onClick.RemoveListener(OnRerollClicked);
                _rerollButton.onClick.AddListener(OnRerollClicked);
                _buttonHooked = true;
                break;
            }
            if (_rerollCostLabel == null)
                foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    if (text.gameObject.scene.IsValid() && text.gameObject.name == "RerollValue_Text") { _rerollCostLabel = text; break; }
        }

        /// <summary>같은 보상(요청)에서만 리롤 횟수를 세고, 새 보상이 나오면 비용을 처음으로 되돌린다.</summary>
        private void TrackRewardRequest()
        {
            string id = _bootstrap?.Rewards?.Pending?.RequestId;
            if (id == _rerollRequestId) return;
            _rerollRequestId = id;
            _rerollCount = 0;
            _pushed = false;
        }

        private bool IsChoosingReward(out GridRunSession run)
        {
            run = _bootstrap != null ? _bootstrap.GridSession : null;
            var rewards = _bootstrap != null ? _bootstrap.Rewards : null;
            return run != null && rewards?.Pending != null && run.PendingRewardId == rewards.Pending.RequestId &&
                run.Grid != null && run.Grid.Phase == eGridPhase.REWARD && !run.Grid.HasPendingStorage;
        }

        private void RefreshRerollState()
        {
            bool choosing = IsChoosingReward(out _);
            if (_rerollButton != null && _rerollButton.interactable != choosing) _rerollButton.interactable = choosing;
            if (_rerollCostLabel != null)
            {
                // 보상을 고르는 중인데 재화가 모자라면 비용을 빨갛게 보여 준다.
                Color want = choosing && _balance < CurrentRerollCost ? new Color(1f, 0.45f, 0.4f) : Color.white;
                if (_rerollCostLabel.color != want) _rerollCostLabel.color = want;
            }
        }

        private void OnRerollClicked()
        {
            Vector2 at = _rerollButton != null ? ScreenCenter(_rerollButton.transform as RectTransform) : new Vector2(Screen.width * 0.2f, Screen.height * 0.12f);
            if (!IsChoosingReward(out var run))
            {
                SpawnFloating(at, "보상을 고를 때만 쓸 수 있어요", new Color(0.8f, 0.85f, 1f));
                return;
            }
            int cost = CurrentRerollCost;
            if (_balance < cost)
            {
                SpawnFloating(at, "재화가 모자라요 (" + cost + " 필요)", new Color(1f, 0.5f, 0.45f));
                return;
            }
            if (!RedrawCandidates(run))
            {
                SpawnFloating(at, "다시 뽑지 못했어요", new Color(1f, 0.5f, 0.45f));
                return;
            }
            _balance -= cost;
            _rerollCount++;
            Push();
            RefreshHandCards();
            SpawnFloating(at, "-" + cost + "  다시 뽑았어요", new Color(0.7f, 0.95f, 1f));
        }

        /// <summary>보상 후보(Candidates)를 같은 규칙(GeneralRewardSource.Draw)으로 새로 뽑아 바꿔 끼운다.</summary>
        private bool RedrawCandidates(GridRunSession run)
        {
            var rewards = _bootstrap.Rewards;
            var source = rewards.GetType().GetField("_source", Private)?.GetValue(rewards) as GeneralRewardSource;
            var setter = rewards.GetType().GetProperty("Candidates")?.GetSetMethod(true);
            if (source == null || setter == null)
            {
                Debug.LogWarning("리롤 실패: 보상 후보를 바꿀 방법을 찾지 못했습니다(StageGridRewards 구조가 바뀌었을 수 있음).", this);
                return false;
            }
            setter.Invoke(rewards, new object[] { source.Draw(run.Grid.CanExpand) });
            return true;
        }

        /// <summary>손패(카드 UI)가 새 후보를 바로 그리도록 갱신을 요청한다.</summary>
        private void RefreshHandCards()
        {
            if (_handAdapter == null) _handAdapter = FindFirstObjectByType<UIGridStorageHandAdapter>(FindObjectsInactive.Include);
            _handAdapter?.GetType().GetMethod("RefreshItems", Private)?.Invoke(_handAdapter, null);
        }

        // ───────────── 얻을 때 재화 아이콘이 톡 커졌다 돌아온다

        private void UpdatePulse()
        {
            if (_pulseTarget == null)
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    if (t.gameObject.scene.IsValid() && t.name == "Currency" && t.GetComponentInParent<Canvas>(true) != null)
                    { _pulseTarget = t as RectTransform; break; }
                if (_pulseTarget == null) return;
            }
            if (_pulse > 0f) _pulse = Mathf.Max(0f, _pulse - Time.unscaledDeltaTime);
            float k = _pulse > 0f ? 1f + 0.18f * Mathf.Sin((_pulse / 0.3f) * Mathf.PI) : 1f;
            _pulseTarget.localScale = new Vector3(k, k, 1f);
        }

        // ───────────── 떠오르는 문구 ("+N", 안내)

        private static Vector2 WorldToScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector3 s = cam.WorldToScreenPoint(world);
            return new Vector2(s.x, s.y);
        }

        private static Vector2 ScreenCenter(RectTransform rect)
        {
            if (rect == null) return Vector2.zero;
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return new Vector2((bl.x + tr.x) * 0.5f, tr.y + 6f); // 버튼 바로 위
        }

        private void SpawnFloating(Vector2 screen, string message, Color color)
        {
            EnsureCanvas();
            var go = new GameObject("Float", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(700f, 60f);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.text = message;
            text.fontSize = 32f * Mathf.Max(0.8f, Screen.height / 1080f);
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(30, 18, 0, 255);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            _floats.Add(new Floating { Rect = rect, Text = text, Start = screen, Age = 0f });
        }

        private void UpdateFloats()
        {
            float rise = 70f * Mathf.Max(0.8f, Screen.height / 1080f);
            for (int i = _floats.Count - 1; i >= 0; i--)
            {
                var f = _floats[i];
                f.Age += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(f.Age / _floatSeconds);
                if (f.Rect == null || t >= 1f)
                {
                    if (f.Rect != null) Destroy(f.Rect.gameObject);
                    _floats.RemoveAt(i);
                    continue;
                }
                f.Rect.anchoredPosition = f.Start + new Vector2(0f, rise * Mathf.SmoothStep(0f, 1f, t));
                var c = f.Text.color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                f.Text.color = c;
            }
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            var go = new GameObject("CurrencyFloats", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 25; // 손패·드롭 존보다 위, 시너지 팝업(30)·결과 팝업(100)보다 아래
            const string sample = "+-0123456789보상을고를때만쓸수있어요재화가모자라요필요다시뽑았지못했";
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.HasCharacters(sample, out _, true, true)) { _font = font; break; }
        }
    }
}
