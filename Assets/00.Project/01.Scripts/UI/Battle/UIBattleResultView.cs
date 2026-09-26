using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    public enum eBattleResultState
    {
        VICTORY,
        DEFEAT
    }

    // 결과 화면에 전달할 표시 전용 값이다. 전투 집계, 보상 지급, 저장은 담당하지 않는다.
    [Serializable]
    public sealed class BattleResultDisplayData
    {
        [SerializeField] private string _difficulty = "보통";
        [SerializeField, Min(0f)] private float _seconds = 318f;
        [SerializeField, Min(0)] private int _kills = 73;
        [SerializeField, Min(0)] private int _deployments = 9;
        [SerializeField, Min(0)] private int _earnedXp = 860;
        [SerializeField, Min(1)] private int _level = 20;
        [SerializeField, Range(0f, 1f)] private float _normalizedXp = 0.6f;
        [SerializeField] private bool _didLevelUp = true;

        public string Difficulty => _difficulty ?? string.Empty;
        public float Seconds => IsFinite(_seconds) ? Mathf.Max(0f, _seconds) : 0f;
        public int Kills => Mathf.Max(0, _kills);
        public int Deployments => Mathf.Max(0, _deployments);
        public int EarnedXp => Mathf.Max(0, _earnedXp);
        public int Level => Mathf.Max(1, _level);
        public float NormalizedXp => IsFinite(_normalizedXp) ? Mathf.Clamp01(_normalizedXp) : 0f;
        public bool DidLevelUp => _didLevelUp;

        public BattleResultDisplayData() { }

        public BattleResultDisplayData(string difficulty, float seconds, int kills, int deployments,
            int earnedXp, int level, float normalizedXp, bool didLevelUp)
        {
            _difficulty = difficulty;
            _seconds = seconds;
            _kills = kills;
            _deployments = deployments;
            _earnedXp = earnedXp;
            _level = level;
            _normalizedXp = normalizedXp;
            _didLevelUp = didLevelUp;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Battle Result View")]
    public sealed class UIBattleResultView : MonoBehaviour
    {
        [Header("결과 문구 / 집계 표시")]
        [SerializeField] private TMP_Text _difficultyText;
        [SerializeField] private TMP_Text _resultTitleText;
        [SerializeField] private TMP_Text _timeLabelText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private TMP_Text _killsText;
        [SerializeField] private TMP_Text _deploymentsText;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _levelUpText;
        [SerializeField] private Slider _experience;

        [Header("승리 / 패배 공통 배치")]
        [SerializeField] private Image _crown;
        [SerializeField] private Image _bannerLeft;
        [SerializeField] private Image _bannerRight;
        [SerializeField] private Image _rune;
        [SerializeField] private Image _titleAccent;

        [Header("상태별 교체 아트 / 강조색")]
        [SerializeField] private Sprite _victoryCrown;
        [SerializeField] private Sprite _defeatCrown;
        [SerializeField] private Sprite _victoryBanner;
        [SerializeField] private Sprite _defeatBanner;
        [SerializeField] private Color _victoryAccent = new Color32(200, 167, 104, 255);
        [SerializeField] private Color _defeatAccent = new Color32(153, 111, 117, 255);

        [Header("로비 이동 연결")]
        [SerializeField] private Button _lobbyButton;
        [SerializeField] private UIPopupPanel _popup;
        [SerializeField] private UISceneNavigator _navigator;
        [SerializeField] private string _lobbyScenePath;

        [Header("전투 / 보상 시스템에 연결되지 않은 미리보기 값")]
        [SerializeField] private eBattleResultState _state = eBattleResultState.DEFEAT;
        [SerializeField] private BattleResultDisplayData _previewData = new BattleResultDisplayData();

        private BattleResultDisplayData _runtimeData;
        private Button _subscribedButton;
        private bool _needsRefresh;

        public eBattleResultState State => _state;
        public UIPopupPanel Popup => _popup;
        public BattleResultDisplayData DisplayData => _runtimeData ?? _previewData;

        // 전달된 값은 런타임에서만 보유하여 Inspector 미리보기 원본을 덮어쓰지 않는다.
        public void SetData(BattleResultDisplayData data)
        {
            _runtimeData = data;
            RefreshView();
        }

        public void SetState(eBattleResultState state)
        {
            _state = state;
            RefreshView();
        }

        public void ConfigureNavigation(UISceneNavigator navigator, string lobbyScenePath)
        {
            _navigator = navigator;
            _lobbyScenePath = lobbyScenePath;
            SyncButtonSubscription();
        }

        public void RefreshView()
        {
            _needsRefresh = false;
            SyncButtonSubscription();

            bool isVictory = _state == eBattleResultState.VICTORY;
            Color accent = isVictory ? _victoryAccent : _defeatAccent;
            SetText(_resultTitleText, isVictory ? "승리" : "패배");
            SetText(_timeLabelText, isVictory ? "클리어 시간" : "플레이 시간");
            if (_resultTitleText != null) _resultTitleText.color = accent;
            SetSprite(_crown, isVictory ? _victoryCrown : _defeatCrown);
            Sprite banner = isVictory ? _victoryBanner : _defeatBanner;
            SetSprite(_bannerLeft, banner);
            SetSprite(_bannerRight, banner);
            SetColor(_rune, accent);
            SetColor(_titleAccent, accent);

            BattleResultDisplayData data = DisplayData;
            SetText(_difficultyText, FormatDifficulty(data != null ? data.Difficulty : string.Empty));
            SetText(_timeText, FormatTime(data != null ? data.Seconds : 0f));
            SetText(_killsText, FormatNumber(data != null ? data.Kills : 0) + "명");
            SetText(_deploymentsText, FormatNumber(data != null ? data.Deployments : 0) + "개");
            SetText(_rewardText, "+ " + FormatNumber(data != null ? data.EarnedXp : 0) + " EXP");
            SetText(_levelText, "LV. " + FormatNumber(data != null ? data.Level : 1));
            SetText(_levelUpText, data != null && data.DidLevelUp ? "레벨 업!" : string.Empty);

            if (_experience != null)
            {
                // 결과 표시 전용 Slider이며 값 변경 이벤트로 실제 경험치를 지급하지 않는다.
                _experience.interactable = false;
                _experience.wholeNumbers = false;
                _experience.minValue = 0f;
                _experience.maxValue = 1f;
                _experience.SetValueWithoutNotify(data != null ? data.NormalizedXp : 0f);
            }
        }

        public void ReturnToLobby()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (_popup != null && _popup.Controller != null && !_popup.Controller.IsTopPopup(_popup)) return;
            if (_navigator == null || string.IsNullOrWhiteSpace(_lobbyScenePath))
            {
                Debug.LogWarning("결과 화면의 Navigator와 로비 Scene 경로 연결을 확인하세요.", this);
                return;
            }

            // Scene 이동 실패 시 결과 화면이 사라지지 않도록 팝업을 먼저 닫지 않는다.
            _navigator.LoadScene(_lobbyScenePath);
        }

        private void SyncButtonSubscription()
        {
            Button target = isActiveAndEnabled && Application.IsPlaying(gameObject) ? _lobbyButton : null;
            if (_subscribedButton == target) return;
            if (_subscribedButton != null) _subscribedButton.onClick.RemoveListener(ReturnToLobby);
            _subscribedButton = target;
            if (_subscribedButton != null) _subscribedButton.onClick.AddListener(ReturnToLobby);
        }

        private void OnEnable()
        {
            SyncButtonSubscription();
            _needsRefresh = true;
        }

        private void OnDisable()
        {
            if (_subscribedButton != null) _subscribedButton.onClick.RemoveListener(ReturnToLobby);
            _subscribedButton = null;
        }

        // Inspector 역직렬화 중 TMP나 Slider 하위 오브젝트를 즉시 변경하지 않는다.
        private void OnValidate() => _needsRefresh = true;

        private void LateUpdate()
        {
            if (_needsRefresh) RefreshView();
        }

        private static string FormatNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string FormatDifficulty(string difficulty)
        {
            if (string.IsNullOrWhiteSpace(difficulty)) return string.Empty;
            string text = difficulty.Trim();
            return text.EndsWith("난이도", StringComparison.Ordinal) ? text : text + " 난이도";
        }

        private static string FormatTime(float seconds)
        {
            // 매우 큰 입력의 int 변환과 NaN/Infinity를 안전하게 처리한다.
            double safeSeconds = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0d : Math.Max(0d, seconds);
            int totalSeconds = (int)Math.Min(int.MaxValue, Math.Floor(safeSeconds));
            return (totalSeconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                (totalSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }

        private static void SetSprite(Image target, Sprite sprite)
        {
            if (target == null) return;
            target.sprite = sprite;
            target.enabled = sprite != null;
        }

        private static void SetColor(Image target, Color color)
        {
            if (target == null) return;
            // 룬과 제목 장식에 설정한 불투명도는 상태 전환 시에도 유지한다.
            color.a = target.color.a;
            target.color = color;
        }
    }
}
