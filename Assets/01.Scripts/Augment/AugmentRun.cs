using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL2.Augment
{
    /// <summary>
    /// 이번 런에서 뽑은 증강 상태. 순수 C#, 저장 없음(런 끝나면 New 해서 버림).
    /// 라운드 마일스톤마다 Draw3 로 3장 뽑고 Pick 으로 확정.
    /// </summary>
    public class AugmentRun
    {
        private readonly List<AugmentData> _pool;
        private readonly Dictionary<string, int> _stacks = new Dictionary<string, int>();
        private readonly List<AugmentData> _pickedOrder = new List<AugmentData>();

        /// <summary>즉시 발동형(isInstant) 증강을 뽑았을 때 1회 발생. 샌드박스가 구독해 직접 처리.</summary>
        public event Action<AugmentData> Picked;

        public event Action Changed;

        public AugmentRun(IEnumerable<AugmentData> pool)
        {
            _pool = new List<AugmentData>(pool);
        }

        public int StackOf(AugmentData d) => _stacks.TryGetValue(d.augmentId, out var n) ? n : 0;
        public bool IsMaxed(AugmentData d) => StackOf(d) >= Mathf.Max(1, d.maxStack);
        public int PickedCount => _stacks.Count;
        public int TotalCount => _pool.Count;

        /// <summary>
        /// 이번 런에 고른 증강 목록, 고른 순서대로. 인게임 HUD("현재 보유 증강" 아이콘 줄)가 이걸 그대로 씀.
        /// 즉시발동형도 포함(한 번 실행했다는 기록으로) — 지속 효과가 없다는 건 isInstant 로 UI가 구분.
        /// </summary>
        public IReadOnlyList<AugmentData> PickedList => _pickedOrder;

        /// <summary>
        /// 라운드 마일스톤 단계(1=R10, 2=R20 … 5=R50)별로 그 단계에 배정된 티어에서만 3장 뽑는다.
        ///   R10·R20 → 실버(1)만 / R30·R40 → 골드(2)만 / R50~ → 플래티넘(3)만
        /// 이미 만스택인 카드는 제외.
        /// </summary>
        public List<AugmentData> Draw3(int milestoneStage)
        {
            int tier = TierForStage(milestoneStage);
            var candidates = new List<AugmentData>();
            foreach (var d in _pool)
            {
                if (d.tier == tier && !IsMaxed(d)) candidates.Add(d);
            }

            var picks = new List<AugmentData>();
            for (int i = 0; i < 3 && candidates.Count > 0; i++)
            {
                var chosen = WeightedPick(candidates);
                picks.Add(chosen);
                candidates.Remove(chosen); // 한 draw 안에서 중복 제거
            }
            return picks;
        }

        /// <summary>R10·20 → 실버(1) / R30·40 → 골드(2) / R50~ → 플래티넘(3).</summary>
        public static int TierForStage(int milestoneStage)
        {
            if (milestoneStage <= 2) return 1;
            if (milestoneStage <= 4) return 2;
            return 3;
        }

        private static AugmentData WeightedPick(List<AugmentData> list)
        {
            float total = 0f;
            foreach (var d in list) total += Mathf.Max(0.01f, d.weight);
            float r = UnityEngine.Random.value * total;
            foreach (var d in list)
            {
                r -= Mathf.Max(0.01f, d.weight);
                if (r <= 0f) return d;
            }
            return list[list.Count - 1];
        }

        public bool Pick(AugmentData d)
        {
            if (d == null || IsMaxed(d)) return false;
            _stacks[d.augmentId] = StackOf(d) + 1;
            _pickedOrder.Add(d);

            if (d.isInstant) Picked?.Invoke(d);

            Changed?.Invoke();
            return true;
        }

        public void ResetRun()
        {
            _stacks.Clear();
            _pickedOrder.Clear();
            Changed?.Invoke();
        }

        public AugmentModifiers BuildModifiers()
        {
            var m = AugmentModifiers.Neutral;
            foreach (var d in _pool)
            {
                if (d.isInstant) continue; // 즉시형은 누적 배율 대상 아님
                int stack = StackOf(d);
                if (stack <= 0) continue;
                Apply(ref m, d.effect, d.value, stack);
            }
            return m;
        }

        private static void Apply(ref AugmentModifiers m, AugmentEffect e, float v, int stack)
        {
            float t = v * stack;
            switch (e)
            {
                case AugmentEffect.SkillDamage: m.SkillPowerMult += t; break;
                case AugmentEffect.SkillCooldown: m.SkillCooldownMult = Mathf.Max(0.4f, m.SkillCooldownMult - t); break;
                case AugmentEffect.SkillRadius: m.SkillRadiusMult += t; break;
                case AugmentEffect.SkillBuffDuration: m.SkillBuffDurationMult += t; break;
                case AugmentEffect.SkillCapstone:
                    m.SkillPowerMult += t; m.SkillCooldownMult = Mathf.Max(0.4f, m.SkillCooldownMult - t);
                    break;

                case AugmentEffect.MonDefense: m.MonsterDefenseAdd += t; break;
                case AugmentEffect.MonHp: m.MonsterHpMult += t; break;
                case AugmentEffect.MonAttack: m.MonsterAttackMult += t; break;
                case AugmentEffect.MonAttackSpeed: m.MonsterAttackSpeedMult += t; break;

                case AugmentEffect.HeroMoveSpeed: m.HeroMoveSpeedMult -= t; break;
                case AugmentEffect.HeroDefense: m.HeroDefenseAdd -= t; break;
                case AugmentEffect.HeroAttack: m.HeroAttackMult -= t; break;
                case AugmentEffect.HeroVulnerable: m.HeroIncomingSkillMult += t; break;

                case AugmentEffect.XpGain: m.XpGainMult += t; break;
                case AugmentEffect.Grid: m.GridBonus += stack; break;

                case AugmentEffect.CritChance: m.CritChance += t; break;
                case AugmentEffect.EchoRecast: m.EchoChance += t; break;
                case AugmentEffect.CooldownOnKill: m.CooldownOnKillSeconds += t; break;
                case AugmentEffect.ExplodeOnDeath: m.ExplodeOnDeathPower += t; break;
                case AugmentEffect.OnHitSlow: m.OnHitSlowAmount = v; break;
                case AugmentEffect.MonsterReviveChance: m.MonsterReviveChance += t; break;
                case AugmentEffect.MonsterShield: m.MonsterShieldActive = true; break;
                case AugmentEffect.XpPerAliveMonster: m.XpPerAliveMonster += t; break;
                case AugmentEffect.PredatorInstinct: m.MonsterAttackMult += t; m.MonsterAttackSpeedMult += t; break;
                case AugmentEffect.FrostLance: m.HeroMoveSpeedMult -= t; m.HeroDefenseAdd -= t; break;
            }
        }
    }
}
