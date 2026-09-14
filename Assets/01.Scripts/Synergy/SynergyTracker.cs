using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Synergy
{
    /// <summary>
    /// 시너지 판정 — 순수 C#, 저장 없음. 실제 게임에선 그리드에 배치된 몬스터 수를 매 배치 변경마다
    /// SetCount 로 넣어주면 됨(그리드는 김건·준기 파트). 지금은 샌드박스에서 숫자를 직접 입력해 검증.
    /// </summary>
    public class SynergyTracker
    {
        private readonly List<SynergyData> _defs;
        private readonly List<ComboSynergyData> _comboDefs;
        private readonly Dictionary<SynergyJob, int> _counts = new Dictionary<SynergyJob, int>();

        public event Action Changed;

        public SynergyTracker(IEnumerable<SynergyData> defs, IEnumerable<ComboSynergyData> comboDefs = null)
        {
            _defs = new List<SynergyData>(defs);
            _comboDefs = comboDefs != null ? new List<ComboSynergyData>(comboDefs) : new List<ComboSynergyData>();
        }

        public IReadOnlyList<SynergyData> Defs => _defs;
        public IReadOnlyList<ComboSynergyData> ComboDefs => _comboDefs;

        public int CountOf(SynergyJob job) => _counts.TryGetValue(job, out var n) ? n : 0;

        public void SetCount(SynergyJob job, int count)
        {
            count = Mathf.Max(0, count);
            if (CountOf(job) == count) return;
            _counts[job] = count;
            Changed?.Invoke();
        }

        public SynergyData Def(SynergyJob job) => _defs.Find(d => d.job == job);

        /// <summary>0=미발동, 1=1단계, 2=2단계.</summary>
        public int TierOf(SynergyJob job)
        {
            var d = Def(job);
            return d == null ? 0 : d.TierForCount(CountOf(job));
        }

        /// <summary>조합 시너지 발동 조건 — 두 직업 모두 각자의 필요 배치 수 이상.</summary>
        public bool IsComboActive(ComboSynergyData c) =>
            c != null && CountOf(c.jobA) >= c.requiredCountEach && CountOf(c.jobB) >= c.requiredCountEach;

        public SynergyModifiers BuildModifiers()
        {
            var m = SynergyModifiers.Neutral;
            foreach (var d in _defs)
            {
                int tier = TierOf(d.job);
                if (tier <= 0) continue;
                float v = tier >= 2 ? d.tier2Value : d.tier1Value;
                Apply(ref m, d.effect, v);
            }
            foreach (var c in _comboDefs)
            {
                if (!IsComboActive(c)) continue;
                ApplyCombo(ref m, c.effect, c.value);
            }
            return m;
        }

        private static void Apply(ref SynergyModifiers m, SynergyEffect e, float v)
        {
            switch (e)
            {
                case SynergyEffect.WarriorAttack: m.WarriorAttackMult += v; break;
                case SynergyEffect.ShieldHp: m.ShieldHpMult += v; break;
                case SynergyEffect.ArcherAttackSpeed: m.ArcherAttackSpeedMult += v; break;
                case SynergyEffect.MageAttack: m.MageAttackMult += v; break;
                case SynergyEffect.RogueAttackSpeed: m.RogueAttackSpeedMult += v; break;
                case SynergyEffect.HealerHealAmount: m.HealerHealAmountMult += v; break;
            }
        }

        private static void ApplyCombo(ref SynergyModifiers m, ComboSynergyEffect e, float v)
        {
            switch (e)
            {
                case ComboSynergyEffect.FrontlineAssault: m.WarriorAttackMult += v; m.ShieldHpMult += v; break;
                case ComboSynergyEffect.RangedBarrage: m.ComboCritChanceBonus += v; break;
                case ComboSynergyEffect.SwiftHunters: m.ArcherAttackSpeedMult += v; m.RogueAttackSpeedMult += v; break;
                case ComboSynergyEffect.GuardiansBlessing: m.ShieldHpMult += v; m.HealerHealAmountMult += v; break;
                case ComboSynergyEffect.SagesBond: m.ComboSkillCooldownMult *= (1f - v); break;
                case ComboSynergyEffect.ExecutionerCombo: m.ComboExecuteBonusMult += v; break;
            }
        }
    }
}
