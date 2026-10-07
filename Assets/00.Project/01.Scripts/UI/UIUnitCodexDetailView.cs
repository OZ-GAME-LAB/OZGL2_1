using System.Collections.Generic;
using System.Globalization;
using OZGL2.Synergy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    // 전투 카드의 표시 기능만 재사용한다. 유닛 생성, 전투 보정, 해금 및 저장은 수행하지 않는다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIPopupPanel))]
    public sealed class UIUnitCodexDetailView : MonoBehaviour
    {
        [SerializeField] private UIBattlePreparationCardView _cardView;
        [SerializeField] private Image _starBadge;
        [SerializeField] private TMP_Text _contextText;
        [SerializeField] private Sprite[] _factionIcons = System.Array.Empty<Sprite>();
        private UIPopupPanel _panel;

        public string DisplayedEntryId { get; private set; } = string.Empty;
        public UIPopupPanel Panel
        {
            get
            {
                if (_panel == null) TryGetComponent(out _panel);
                return _panel;
            }
        }

        // 선택한 목록 카드만 읽는다. 실제 유닛이나 원본 SO는 변경하지 않는다.
        public bool ShowUnit(UIUnitCodexCardView source)
        {
            if (source == null || !source.IsUnlocked || source.Entry == null || _cardView == null) return false;
            UIUnitCatalogSO.Entry entry = source.Entry;
            UnitStatData stats = entry.BaseStats;
            bool isDemon = entry.Faction == eUnitCodexFaction.DEMON;
            if (stats == null || entry.Id != "unit." + stats.unitId ||
                stats.side != (isDemon ? UnitSide.DemonArmy : UnitSide.Hero)) return false;

            int star = isDemon ? Mathf.Clamp(source.AppearanceIndex + 1, 1, 3) : 1;
            float multiplier = isDemon ? UnitStatData.GetStarMultiplier(star) : 1f;
            // UnitBase.ApplyStarLevel과 같은 기본 계산만 수행하며 전투 보정은 호출하지 않는다.
            float attack = stats.attackPower * multiplier;
            int health = Mathf.RoundToInt(stats.maxHealth * multiplier);
            float healing = stats.healAmount * multiplier;
            float range = stats.attackRange + (isDemon ? stats.rangeBonusPerStar * (star - 1) : 0f);

            DisplayedEntryId = entry.Id;
            _cardView.SetTitle(entry.DisplayName);
            _cardView.SetRankText(isDemon ? star.ToString(CultureInfo.InvariantCulture) : string.Empty);
            _cardView.SetArtwork(entry.GetPortrait(source.AppearanceIndex));
            int factionIndex = (int)entry.Faction;
            _cardView.SetTypeIcon(_factionIcons != null && factionIndex < _factionIcons.Length
                ? _factionIcons[factionIndex] : null);
            _cardView.SetFootprint(null);
            _cardView.SetFootprintVisible(false);
            _cardView.SetStats(Format(attack), Format(stats.defensePercent * 100f) + "%",
                health.ToString(CultureInfo.InvariantCulture));
            _cardView.SetTrait("분류", (isDemon ? "마왕군" : "인간군") + " · " + JobName(stats.job));
            _cardView.SetSkill("전투 정보", "사거리 " + Format(range) + " · 공속 " +
                Format(stats.attackSpeed) + "회/초\n" + BuildEffects(stats, star, healing));

            if (_starBadge != null) _starBadge.gameObject.SetActive(isDemon);
            if (_contextText != null)
                _contextText.text = (isDemon ? star + "성" : $"외형 {source.AppearanceIndex + 1} / {source.AppearanceCount}") +
                    " · 기본 능력치 (특성·시너지·난이도 보정 제외)";
            return true;
        }

        public void Close()
        {
            UIPopupController controller = Panel != null ? Panel.Controller : null;
            if (controller != null && controller.IsTopPopup(Panel)) controller.CloseTopPopup();
        }

        private void OnDisable() => DisplayedEntryId = string.Empty;

        private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string JobName(SynergyJob job)
        {
            switch (job)
            {
                case SynergyJob.Warrior: return "전사";
                case SynergyJob.Shield: return "방패병";
                case SynergyJob.Archer: return "궁수";
                case SynergyJob.Mage: return "마법사";
                case SynergyJob.Rogue: return "도적";
                case SynergyJob.Healer: return "힐러";
                default: return job.ToString();
            }
        }

        private static string BuildEffects(UnitStatData stats, int star, float healing)
        {
            var effects = new List<string>();
            if (healing > 0f) effects.Add("체력 비율 낮은 아군 " + Mathf.Clamp(star, 1, 3) + "명 회복 " + Format(healing));
            if (stats.targetLowestHealthEnemy) effects.Add("체력이 낮은 적 우선 공격");
            if (stats.splashRadius > 0f)
                effects.Add("반경 " + Format(stats.splashRadius) + " · 추가 " +
                    Format(stats.splashSecondaryDamagePercent * 100f) + "%" +
                    (stats.splashMaxTargets > 0 ? " · 최대 " + stats.splashMaxTargets + "명" : string.Empty));
            if (star >= 2 && stats.executeDamageBonusPerMissingHealth > 0f)
                effects.Add("잃은 체력 비례 추가 피해 (최대 " +
                    Format(stats.executeDamageBonusPerMissingHealth * 100f) + "%)");
            if (star >= 2 && stats.comboStunAttackInterval > 0)
                effects.Add(stats.comboStunAttackInterval + "번째 공격마다 " + Format(stats.comboStunDuration) + "초 기절");
            if (stats.burnDamagePerSecond > 0f && stats.burnDuration > 0f)
                effects.Add(Format(stats.burnDuration) + "초간 초당 " + Format(stats.burnDamagePerSecond) + " 화상 피해");
            if (stats.side == UnitSide.DemonArmy && stats.attackRange <= 1f)
                effects.Add("사거리 안의 용사를 자신에게 유인");
            return effects.Count > 0 ? string.Join("\n", effects) : "별도의 고유 효과 없음";
        }
    }
}
