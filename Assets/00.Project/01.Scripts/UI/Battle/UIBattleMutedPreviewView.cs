using System;
using System.Globalization;
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
        [SerializeField] private Text _costText;
        [SerializeField] private Text _rerollCostText;
        [SerializeField] private Image _experienceFill;

        [Header("적 예고 / 시너지 연결")]
        [SerializeField] private Text[] _enemyNames;
        [SerializeField] private Text[] _enemyCounts;
        [SerializeField] private Text[] _synergyNames;
        [SerializeField] private Text[] _synergyThresholds;

        [Header("시너지 기준값 표시 색상 (발동 판정과 별개)")]
        [SerializeField] private Color _firstThresholdColor = new Color32(235, 220, 153, 255);
        [SerializeField] private Color _nextThresholdColor = new Color32(119, 119, 119, 255);

        [Header("실제 전투 시스템과 연결되지 않은 미리보기 값")]
        [SerializeField, Min(0)] private int _previewWave = 3;
        [SerializeField, Min(1)] private int _previewTotalWaves = 10;
        [SerializeField, Min(1)] private int _previewLevel = 20;
        [SerializeField, Range(0f, 1f)] private float _previewExperience = 0.6f;
        [SerializeField, Min(0f)] private float _previewRemainingSeconds = 42f;
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
            SetText(_timeText, FormatTime(_previewRemainingSeconds));
            SetText(_costText, FormatNumber(Mathf.Max(0, _previewCost)));
            SetText(_rerollCostText, FormatNumber(Mathf.Max(0, _previewRerollCost)));

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
                SetTextAt(_enemyNames, i, enemy != null ? enemy.Name : string.Empty);
                SetTextAt(_enemyCounts, i, enemy != null ? "×" + FormatNumber(enemy.Count) : string.Empty);
            }

            int synergySlots = Mathf.Max(ArrayLength(_synergyNames), ArrayLength(_synergyThresholds));
            for (int i = 0; i < synergySlots; i++)
            {
                SynergyPreview synergy = _previewSynergies != null && i < _previewSynergies.Length
                    ? _previewSynergies[i] : null;
                SetTextAt(_synergyNames, i, synergy != null ? synergy.Name : string.Empty);
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

        private void OnEnable() => RefreshView();
        private void OnValidate() => RefreshView();

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float ClampProgress(float value) => IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        private static int ArrayLength(Text[] values) => values != null ? values.Length : 0;
        private static string FormatNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string FormatTime(float seconds)
        {
            // 올림으로 남은 1초 미만도 표시하며, 큰 입력의 정수 변환 오버플로를 방지한다.
            double safeSeconds = IsFinite(seconds) ? Math.Max(0d, (double)seconds) : 0d;
            int totalSeconds = (int)Math.Min(int.MaxValue, Math.Ceiling(safeSeconds));
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
    }
}
