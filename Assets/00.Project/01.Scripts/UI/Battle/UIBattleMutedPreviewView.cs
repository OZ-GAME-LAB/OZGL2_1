using System;
using System.Globalization;
using OZGL2.Synergy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 전투 로직이나 저장 데이터를 소유하지 않고, 명시적으로 전달된 미리보기 값만 표시한다.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Battle Muted Preview View")]
    public sealed class UIBattleMutedPreviewView : MonoBehaviour
    {
        [Header("공통 HUD 연결")]
        [SerializeField] private Text _waveText;
        [SerializeField] private Text _levelText;
        [SerializeField] private Text _timeText;
        [SerializeField] private TMP_Text _remainingEnemyTextSdf;
        [SerializeField] private Text _costText;
        [SerializeField] private Text _rerollCostText;
        [SerializeField] private TMP_Text _costTextSdf;
        [SerializeField] private TMP_Text _rerollCostTextSdf;
        [SerializeField] private Image _experienceFill;

        [Header("적 예고 / 시너지 연결")]
        [SerializeField] private Text[] _enemyNames;
        [SerializeField] private Text[] _enemyCounts;
        [SerializeField] private Image[] _enemyIcons;
        [SerializeField] private Text[] _synergyNames;
        [SerializeField] private Text[] _synergyThresholds;

        [Header("시너지 깃발 표시 (프레임·아이콘과 분리)")]
        [SerializeField] private Image[] _synergyLeftFlags;
        [SerializeField] private Image[] _synergyRightFlags;
        [SerializeField] private SynergyFlagStyle[] _synergyFlagStyles;
        [SerializeField] private Color _neutralSynergyFlagColor = new Color32(155, 151, 143, 255);

        [Header("시너지 기준값 표시 색상 (발동 판정과 별개)")]
        [SerializeField] private Color _firstThresholdColor = new Color32(235, 220, 153, 255);
        [SerializeField] private Color _nextThresholdColor = new Color32(119, 119, 119, 255);

        [Header("실제 전투 시스템과 연결되지 않은 미리보기 값")]
        [SerializeField, Min(0)] private int _previewWave = 3;
        [SerializeField, Min(1)] private int _previewTotalWaves = 10;
        [SerializeField, Min(1)] private int _previewLevel = 20;
        [SerializeField, Range(0f, 1f)] private float _previewExperience = 0.6f;
        [SerializeField, Min(0f)] private float _previewRemainingSeconds = 42f;
        [SerializeField, Min(0)] private int _previewRemainingEnemies;
        [SerializeField, Min(0)] private int _previewCost = 100;
        [SerializeField, Min(0)] private int _previewRerollCost = 100;
        [SerializeField] private EnemyPreview[] _previewEnemies =
        {
            new EnemyPreview("근접 용사", 12),
            new EnemyPreview("원거리 용사", 6),
            new EnemyPreview("정예 용사", 2)
        };
        [SerializeField] private SynergyPreview[] _previewSynergies =
        {
            new SynergyPreview("마법 결속", 3, 5),
            new SynergyPreview("사격 대형", 3, 5),
            new SynergyPreview("불굴의 뼈", 3, 5),
            new SynergyPreview("저주 의식", 3, 5)
        };

        private bool _usesElapsedTime;

        public int EnemySlotCount => Mathf.Max(ArrayLength(_enemyNames), ArrayLength(_enemyCounts));

        // 기존 시너지 정의는 읽기만 하며, 씬별 깃발색만 별도로 보관한다.
        [Serializable]
        private sealed class SynergyFlagStyle
        {
            [SerializeField] private SynergyData _definition;
            [SerializeField] private Color _color = Color.white;

            public SynergyData Definition => _definition;
            public Color FlagColor => _color;
        }

        [Serializable]
        private sealed class EnemyPreview
        {
            [SerializeField] private string _name;
            [SerializeField, Min(0)] private int _count;

            public string Name => _name ?? string.Empty;
            public int Count => Mathf.Max(0, _count);

            public EnemyPreview(string name, int count)
            {
                _name = name ?? string.Empty;
                _count = Mathf.Max(0, count);
            }
        }

        [Serializable]
        private sealed class SynergyPreview
        {
            [SerializeField] private string _name;
            [SerializeField, Min(0)] private int _firstThreshold;
            [SerializeField, Min(0)] private int _nextThreshold;

            public string Name => _name ?? string.Empty;
            public int FirstThreshold => Mathf.Max(0, _firstThreshold);
            public int NextThreshold => Mathf.Max(FirstThreshold, _nextThreshold);

            public SynergyPreview(string name, int firstThreshold, int nextThreshold)
            {
                _name = name ?? string.Empty;
                _firstThreshold = Mathf.Max(0, firstThreshold);
                _nextThreshold = Mathf.Max(_firstThreshold, nextThreshold);
            }
        }

        public void Configure(Text waveText, Text levelText, Text timeText, Text costText,
            Text rerollCostText, Image experienceFill, Text[] enemyNames, Text[] enemyCounts,
            Text[] synergyNames, Text[] synergyThresholds)
        {
            _waveText = waveText;
            _levelText = levelText;
            _timeText = timeText;
            _costText = costText;
            _rerollCostText = rerollCostText;
            _experienceFill = experienceFill;
            _enemyNames = enemyNames;
            _enemyCounts = enemyCounts;
            _synergyNames = synergyNames;
            _synergyThresholds = synergyThresholds;
            RefreshView();
        }

        // 기존 Legacy Text 연결은 유지하고, 하단 HUD에서 사용할 SDF 표시만 선택적으로 덧붙인다.
        public void ConfigureSdfCostTexts(TMP_Text costText, TMP_Text rerollCostText)
        {
            _costTextSdf = costText;
            _rerollCostTextSdf = rerollCostText;
            RefreshView();
        }

        // 새 HUD의 남은 용사 수만 연결한다. 기존 시간 표시와 결과용 경과 시간은 유지한다.
        public void ConfigureRemainingEnemyText(TMP_Text remainingEnemyText)
        {
            _remainingEnemyTextSdf = remainingEnemyText;
            RefreshView();
        }

        public void SetRemainingEnemyCount(int count)
        {
            _previewRemainingEnemies = Mathf.Max(0, count);
            SetText(_remainingEnemyTextSdf, FormatNumber(_previewRemainingEnemies));
        }

        public void SetWave(int wave, int totalWaves)
        {
            _previewTotalWaves = Mathf.Max(1, totalWaves);
            _previewWave = Mathf.Clamp(wave, 0, _previewTotalWaves);
            RefreshView();
        }

        public void SetLevelExperience(int level, float normalizedExperience)
        {
            _previewLevel = Mathf.Max(1, level);
            _previewExperience = ClampProgress(normalizedExperience);
            RefreshView();
        }

        public void SetRemainingTime(float seconds)
        {
            _usesElapsedTime = false;
            _previewRemainingSeconds = IsFinite(seconds) ? Mathf.Max(0f, seconds) : 0f;
            RefreshView();
        }

        public void SetElapsedTime(float seconds)
        {
            _usesElapsedTime = true;
            _previewRemainingSeconds = IsFinite(seconds) ? Mathf.Max(0f, seconds) : 0f;
            RefreshView();
        }

        public void SetCost(int cost, int rerollCost)
        {
            _previewCost = Mathf.Max(0, cost);
            _previewRerollCost = Mathf.Max(0, rerollCost);
            RefreshView();
        }

        public void SetEnemy(int index, string name, int count)
        {
            if (_previewEnemies == null || index < 0 || index >= _previewEnemies.Length) return;
            _previewEnemies[index] = new EnemyPreview(name, count);
            RefreshView();
        }

        public void SetSynergy(int index, string name, int firstThreshold, int nextThreshold)
        {
            if (_previewSynergies == null || index < 0 || index >= _previewSynergies.Length) return;
            _previewSynergies[index] = new SynergyPreview(name, firstThreshold, nextThreshold);
            RefreshView();
        }

        public void RefreshView()
        {
            int totalWaves = Mathf.Max(1, _previewTotalWaves);
            SetText(_waveText, "WAVE " + FormatNumber(Mathf.Clamp(_previewWave, 0, totalWaves)) +
                " / " + FormatNumber(totalWaves));
            SetText(_levelText, "LV. " + FormatNumber(Mathf.Max(1, _previewLevel)));
            SetText(_timeText, FormatTime(_previewRemainingSeconds, _usesElapsedTime));
            SetText(_remainingEnemyTextSdf, FormatNumber(Mathf.Max(0, _previewRemainingEnemies)));
            SetText(_costText, FormatNumber(Mathf.Max(0, _previewCost)));
            SetText(_rerollCostText, FormatNumber(Mathf.Max(0, _previewRerollCost)));
            SetText(_costTextSdf, FormatNumber(Mathf.Max(0, _previewCost)));
            SetText(_rerollCostTextSdf, FormatNumber(Mathf.Max(0, _previewRerollCost)));

            if (_experienceFill != null)
            {
                _experienceFill.type = Image.Type.Filled;
                _experienceFill.fillMethod = Image.FillMethod.Horizontal;
                _experienceFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                _experienceFill.fillAmount = ClampProgress(_previewExperience);
            }

            int enemySlots = Mathf.Max(ArrayLength(_enemyNames), ArrayLength(_enemyCounts));
            for (int i = 0; i < enemySlots; i++)
            {
                EnemyPreview enemy = _previewEnemies != null && i < _previewEnemies.Length
                    ? _previewEnemies[i] : null;
                bool hasEnemy = enemy != null && !string.IsNullOrWhiteSpace(enemy.Name);
                SetTextAt(_enemyNames, i, hasEnemy ? enemy.Name : string.Empty);
                SetTextAt(_enemyCounts, i, hasEnemy ? "×" + FormatNumber(enemy.Count) : string.Empty);
                // 이번 웨이브에 없는 병종의 예고 아이콘은 표시하지 않는다.
                if (_enemyIcons != null && i < _enemyIcons.Length && _enemyIcons[i] != null)
                    _enemyIcons[i].enabled = hasEnemy && enemy.Count > 0;
            }

            int synergySlots = Mathf.Max(
                Mathf.Max(ArrayLength(_synergyNames), ArrayLength(_synergyThresholds)),
                Mathf.Max(_synergyLeftFlags != null ? _synergyLeftFlags.Length : 0,
                    _synergyRightFlags != null ? _synergyRightFlags.Length : 0));
            for (int i = 0; i < synergySlots; i++)
            {
                SynergyPreview synergy = _previewSynergies != null && i < _previewSynergies.Length
                    ? _previewSynergies[i] : null;
                SetTextAt(_synergyNames, i, synergy != null ? synergy.Name : string.Empty);
                Color flagColor = ResolveSynergyFlagColor(synergy != null ? synergy.Name : null);
                SetFlagColorAt(_synergyLeftFlags, i, flagColor);
                SetFlagColorAt(_synergyRightFlags, i, flagColor);
                if (_synergyThresholds != null && i < _synergyThresholds.Length && _synergyThresholds[i] != null)
                {
                    _synergyThresholds[i].supportRichText = true;
                    SetText(_synergyThresholds[i], synergy != null
                        ? "<color=#" + ColorUtility.ToHtmlStringRGB(_firstThresholdColor) + ">" +
                          FormatNumber(synergy.FirstThreshold) + "</color><color=#" +
                          ColorUtility.ToHtmlStringRGB(_nextThresholdColor) + "> > " +
                          FormatNumber(synergy.NextThreshold) + "</color>"
                        : string.Empty);
                }
            }
        }

        private Color ResolveSynergyFlagColor(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || _synergyFlagStyles == null)
                return _neutralSynergyFlagColor;

            SynergyFlagStyle match = null;
            foreach (SynergyFlagStyle style in _synergyFlagStyles)
            {
                if (style == null || style.Definition == null ||
                    !string.Equals(style.Definition.displayName, name, StringComparison.Ordinal)) continue;

                // 기존 전달 API는 이름만 제공하므로 중복 이름을 임의의 직업으로 판정하지 않는다.
                if (match != null) return _neutralSynergyFlagColor;
                match = style;
            }
            return match != null ? match.FlagColor : _neutralSynergyFlagColor;
        }

        private static void SetFlagColorAt(Image[] flags, int index, Color color)
        {
            if (flags == null || index < 0 || index >= flags.Length || flags[index] == null) return;
            if (flags[index].color != color) flags[index].color = color;
        }

        private void OnEnable() => RefreshView();
        private void OnValidate() => RefreshView();

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float ClampProgress(float value) => IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        private static int ArrayLength(Text[] values) => values != null ? values.Length : 0;
        private static string FormatNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string FormatTime(float seconds, bool isElapsed)
        {
            // 남은 시간은 올림, 경과 시간은 내림으로 표시한다.
            double safeSeconds = IsFinite(seconds) ? Math.Max(0d, (double)seconds) : 0d;
            double roundedSeconds = isElapsed ? Math.Floor(safeSeconds) : Math.Ceiling(safeSeconds);
            int totalSeconds = (int)Math.Min(int.MaxValue, roundedSeconds);
            return (totalSeconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                (totalSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static void SetTextAt(Text[] targets, int index, string value)
        {
            if (targets != null && index >= 0 && index < targets.Length) SetText(targets[index], value);
        }

        private static void SetText(Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }
    }
}
