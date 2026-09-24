using System;
using System.Collections.Generic;
using OZGL2.Synergy;

namespace OZGL2.Contracts
{
    /// <summary>
    /// (직업, 진영) 조합별 전투 배율 조회 허브. 성민 파트(시너지·특성·증강)가 값을 채우고,
    /// 세진의 UnitBase는 이 값만 읽어서 곱한다 — 서로의 시스템을 직접 참조 안 해도 되게 분리.
    /// 진영까지 같이 키로 쓰는 이유: 용사(Hero)·마왕군(DemonArmy)이 같은 직업 enum(전사 등)을
    /// 공유해서, 진영 구분 없이 직업만으로 저장하면 마왕군 전사 강화 배율이 적 용사 전사한테도
    /// 그대로 적용되는 버그가 생긴다. 값이 없는 조합은 항상 1(무효과)을 반환한다.
    /// </summary>
    public static class CombatModifierHub
    {
        private static readonly Dictionary<(SynergyJob, UnitSide), float> _attackMult = new Dictionary<(SynergyJob, UnitSide), float>();
        private static readonly Dictionary<(SynergyJob, UnitSide), float> _attackSpeedMult = new Dictionary<(SynergyJob, UnitSide), float>();
        private static readonly Dictionary<(SynergyJob, UnitSide), float> _healMult = new Dictionary<(SynergyJob, UnitSide), float>();
        private static readonly Dictionary<(SynergyJob, UnitSide), float> _hpMult = new Dictionary<(SynergyJob, UnitSide), float>();

        public static void SetAttackMult(SynergyJob job, UnitSide side, float mult) => _attackMult[(job, side)] = mult;
        public static void SetAttackSpeedMult(SynergyJob job, UnitSide side, float mult) => _attackSpeedMult[(job, side)] = mult;
        public static void SetHealMult(SynergyJob job, UnitSide side, float mult) => _healMult[(job, side)] = mult;
        public static void SetHpMult(SynergyJob job, UnitSide side, float mult) => _hpMult[(job, side)] = mult;

        // ─────────── 증강 전용 전투 보정 (진영 단위) — 값이 없으면 항상 무효과.
        // 성민 파트(RealSynergySync)가 값을 채우고, UnitBase 는 아래 함수만 호출한다.
        private static readonly Dictionary<UnitSide, float> _damageTakenMult = new Dictionary<UnitSide, float>();
        private static readonly Dictionary<UnitSide, float> _moveSpeedMult = new Dictionary<UnitSide, float>();
        private static readonly Dictionary<UnitSide, float> _defenseAdd = new Dictionary<UnitSide, float>();
        private static readonly Dictionary<UnitSide, bool> _firstHitShield = new Dictionary<UnitSide, bool>();
        private static readonly HashSet<int> _shieldConsumed = new HashSet<int>();

        /// <summary>유닛이 공격을 실제로 수행할 때마다 발생(진영 전달). 연타 본능처럼 "공격할 때마다" 효과용.</summary>
        public static event Action<UnitSide> AttackPerformed;

        /// <summary>해당 진영이 받는 피해 배율 (0.75 = −25%). 철벽 진형.</summary>
        public static void SetDamageTakenMult(UnitSide side, float mult) => _damageTakenMult[side] = mult;

        /// <summary>해당 진영 이동속도 배율 (0.96 = −4%). 냉기 침식.</summary>
        public static void SetMoveSpeedMult(UnitSide side, float mult) => _moveSpeedMult[side] = mult;

        /// <summary>해당 진영이 공격받을 때의 방어율 가감 (−0.04 = 방어력 −4%p). 냉기 침식.</summary>
        public static void SetDefenseAdd(UnitSide side, float add) => _defenseAdd[side] = add;

        /// <summary>해당 진영 유닛이 유닛마다 첫 피격 1회를 무효화하는 보호막을 두를지. 수호의 방패.</summary>
        public static void SetFirstHitShield(UnitSide side, bool active) => _firstHitShield[side] = active;

        /// <summary>라운드 시작마다 호출 — 보호막을 다시 두른 상태로 되돌린다.</summary>
        public static void ResetShields() => _shieldConsumed.Clear();

        public static float GetMoveSpeedMult(UnitSide side) => _moveSpeedMult.TryGetValue(side, out var v) ? v : 1f;
        public static float GetDefenseAdd(UnitSide side) => _defenseAdd.TryGetValue(side, out var v) ? v : 0f;

        /// <summary>UnitBase.TakeDamage 진입점 — 보호막 소모 + 받는 피해 배율을 적용한 최종 피해를 돌려준다.</summary>
        public static int FilterIncomingDamage(int unitId, UnitSide side, int amount)
        {
            if (amount <= 0) return amount;

            if (_firstHitShield.TryGetValue(side, out bool shield) && shield && _shieldConsumed.Add(unitId))
                return 0; // 첫 피격 1회 무효화

            return _damageTakenMult.TryGetValue(side, out var m) && m != 1f
                ? Math.Max(0, (int)Math.Round(amount * m))
                : amount;
        }

        /// <summary>UnitBase 가 공격을 수행할 때 호출.</summary>
        public static void NotifyAttack(UnitSide side) => AttackPerformed?.Invoke(side);

        public static float GetAttackMult(SynergyJob job, UnitSide side) => _attackMult.TryGetValue((job, side), out var v) ? v : 1f;
        public static float GetAttackSpeedMult(SynergyJob job, UnitSide side) => _attackSpeedMult.TryGetValue((job, side), out var v) ? v : 1f;
        public static float GetHealMult(SynergyJob job, UnitSide side) => _healMult.TryGetValue((job, side), out var v) ? v : 1f;
        public static float GetHpMult(SynergyJob job, UnitSide side) => _hpMult.TryGetValue((job, side), out var v) ? v : 1f;
    }
}
