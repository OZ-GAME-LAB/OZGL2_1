using System;
using System.Collections.Generic;
using UnityEngine;
using OZGL2.Skill;

namespace OZGL2.Progression
{
    /// <summary>
    /// 특성 트리 — 계정 영구. 노드별 랭크 보유, 개방 판정(부모 만렙), LP 소비, 배율 합산.
    /// 프로토는 PlayerPrefs. Skill 은 참조하되(SkillModifiers 채우기용) Sandbox 는 안 봄.
    /// </summary>
    public class TraitTree
    {
        private const string KeyPrefix = "OZGL2.Trait.";

        private readonly List<TraitData> _defs = new List<TraitData>();
        private readonly Dictionary<TraitId, TraitData> _byId = new Dictionary<TraitId, TraitData>();
        private readonly Dictionary<TraitId, int> _ranks = new Dictionary<TraitId, int>();

        public event Action Changed;

        public TraitTree(IEnumerable<TraitData> defs)
        {
            foreach (var d in defs)
            {
                if (d == null) continue;
                _defs.Add(d);
                _byId[d.id] = d;
            }
            Load();
        }

        public IReadOnlyList<TraitData> Defs => _defs;
        public TraitData Def(TraitId id) => _byId.TryGetValue(id, out var d) ? d : null;
        public int RankOf(TraitId id) => _ranks.TryGetValue(id, out var r) ? r : 0;
        public bool IsMaxed(TraitId id) { var d = Def(id); return d != null && RankOf(id) >= d.maxRank; }

        /// <summary>부모(전제)가 전부 만렙이면 개방. 부모 없으면 항상 개방.</summary>
        public bool IsOpen(TraitId id)
        {
            var d = Def(id);
            if (d == null) return false;
            if (d.parents == null || d.parents.Length == 0) return true;
            foreach (var p in d.parents)
            {
                if (!IsMaxed(p)) return false;
            }
            return true;
        }

        /// <summary>다음 랭크 비용. 만렙이면 -1.</summary>
        public int NextCost(TraitId id)
        {
            var d = Def(id);
            if (d == null) return -1;
            int r = RankOf(id);
            return r >= d.maxRank ? -1 : d.CostForRank(r);
        }

        public bool CanRank(TraitId id, int availableLp)
        {
            int c = NextCost(id);
            return c >= 0 && IsOpen(id) && availableLp >= c;
        }

        /// <summary>랭크 +1. LP 는 mawang 에서 차감. 성공 시 true.</summary>
        public bool TryRank(TraitId id, MawangLevel mawang)
        {
            int c = NextCost(id);
            if (c < 0 || !IsOpen(id) || mawang == null || !mawang.TrySpend(c)) return false;
            _ranks[id] = RankOf(id) + 1;
            Save();
            Changed?.Invoke();
            return true;
        }

        public void ResetAll()
        {
            foreach (var d in _defs) PlayerPrefs.DeleteKey(KeyPrefix + d.id);
            PlayerPrefs.Save();
            _ranks.Clear();
            Changed?.Invoke();
        }

        // ─────────────────────────────── 합산

        public TraitModifiers BuildModifiers()
        {
            var m = TraitModifiers.Neutral;
            foreach (var d in _defs)
            {
                int r = RankOf(d.id);
                if (r > 0) ApplyEffect(ref m, d.effect, d.valuePerRank, r);
            }
            return m;
        }

        /// <summary>스킬 배율 객체(SkillManager 가 든 것)에 반영.</summary>
        public void ApplyToSkills(SkillModifiers sm)
        {
            if (sm == null) return;
            var m = BuildModifiers();
            sm.PowerMult = m.SkillPowerMult;
            sm.CooldownMult = m.SkillCooldownMult;
            sm.RadiusMult = m.SkillRadiusMult;
            sm.BuffDurationMult = m.SkillBuffDurationMult;
            sm.ReviveBonus = 0;
        }

        private static void ApplyEffect(ref TraitModifiers m, TraitEffect e, float v, int rank)
        {
            float t = v * rank;
            switch (e)
            {
                case TraitEffect.MonAttack: m.MonsterAttackMult += t; break;
                case TraitEffect.MonAttackSpeed: m.MonsterAttackSpeedMult += t; break;
                case TraitEffect.MonRange: m.MonsterRangeMult += t; break;
                case TraitEffect.MonHp: m.MonsterHpMult += t; break;
                case TraitEffect.MonDefense: m.MonsterDefenseAdd += t; break;
                case TraitEffect.MonExecute: m.MonsterExecuteBonus += t; break;

                case TraitEffect.HeroAttack: m.HeroAttackMult -= t; break;
                case TraitEffect.HeroAttackSpeed: m.HeroAttackSpeedMult -= t; break;
                case TraitEffect.HeroRange: m.HeroRangeMult -= t; break;
                case TraitEffect.HeroDefense: m.HeroDefenseAdd -= t; break;
                case TraitEffect.HeroMoveSpeed: m.HeroMoveSpeedMult -= t; break;
                case TraitEffect.HeroHp: m.HeroHpMult -= t; break;
                case TraitEffect.HeroVulnerable: m.HeroIncomingSkillMult += t; break;

                case TraitEffect.SkillDamage: m.SkillPowerMult += t; break;
                case TraitEffect.SkillCooldown: m.SkillCooldownMult = Mathf.Max(0.4f, m.SkillCooldownMult - t); break;
                case TraitEffect.SkillRadius: m.SkillRadiusMult += t; break;
                case TraitEffect.SkillUltCooldown: m.SkillUltCooldownMult = Mathf.Max(0.4f, m.SkillUltCooldownMult - t); break;
                case TraitEffect.SkillArmorPen: m.SkillArmorPen += t; break;
                case TraitEffect.SkillBuffDuration: m.SkillBuffDurationMult += t; break;
                case TraitEffect.SkillSlot: m.ExtraSkillSlots += rank; break;

                case TraitEffect.XpGain: m.XpGainMult += t; break;
                case TraitEffect.XpNeed: m.XpNeedMult = Mathf.Max(0.3f, m.XpNeedMult - t); break;
                case TraitEffect.KillXp: m.KillXpBonus += Mathf.RoundToInt(v) * rank; break;
                case TraitEffect.LevelSurge: m.SurgeXpPer5Level += Mathf.RoundToInt(v) * rank; break;
                case TraitEffect.MilestoneLp: m.MilestoneBonusLp += rank; break;
                case TraitEffect.Grid: m.GridBonus += rank; break;
                case TraitEffect.DeployCost: m.DeployCostBonus += rank; break;

                case TraitEffect.CapMonster:
                    m.MonsterAttackMult += t; m.MonsterHpMult += t;
                    m.MonsterAttackSpeedMult += t; m.MonsterRangeMult += t;
                    break;
                case TraitEffect.CapHero:
                    m.HeroAttackMult -= t; m.HeroHpMult -= t;
                    m.HeroAttackSpeedMult -= t; m.HeroMoveSpeedMult -= t; m.HeroRangeMult -= t;
                    break;
                case TraitEffect.CapSkill:
                    m.SkillPowerMult += 0.10f * rank;
                    m.SkillCooldownMult = Mathf.Max(0.4f, m.SkillCooldownMult - 0.06f * rank);
                    break;
                case TraitEffect.CapEconomy:
                    m.XpGainMult += 0.12f * rank;
                    m.MilestoneBonusLp += rank;
                    m.MilestoneSpBonus += rank;
                    break;
            }
        }

        // ─────────────────────────────── 저장

        private void Save()
        {
            foreach (var kv in _ranks) PlayerPrefs.SetInt(KeyPrefix + kv.Key, kv.Value);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            _ranks.Clear();
            foreach (var d in _defs)
            {
                int r = PlayerPrefs.GetInt(KeyPrefix + d.id, 0);
                if (r > 0) _ranks[d.id] = r;
            }
        }
    }
}
