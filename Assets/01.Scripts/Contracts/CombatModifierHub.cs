using System.Collections.Generic;
using OZGL2.Synergy;

namespace OZGL2.Contracts
{
    /// <summary>
    /// 직업별 전투 배율 조회 허브. 성민 파트(시너지·특성·증강)가 값을 채우고,
    /// 세진의 UnitBase는 이 값만 읽어서 곱한다 — 서로 서로의 시스템을 직접 참조 안 해도 되게 분리.
    /// 값이 없는 직업은 항상 1(무효과)을 반환한다.
    /// </summary>
    public static class CombatModifierHub
    {
        private static readonly Dictionary<SynergyJob, float> _attackMult = new Dictionary<SynergyJob, float>();
        private static readonly Dictionary<SynergyJob, float> _attackSpeedMult = new Dictionary<SynergyJob, float>();
        private static readonly Dictionary<SynergyJob, float> _healMult = new Dictionary<SynergyJob, float>();
        private static readonly Dictionary<SynergyJob, float> _hpMult = new Dictionary<SynergyJob, float>();

        public static void SetAttackMult(SynergyJob job, float mult) => _attackMult[job] = mult;
        public static void SetAttackSpeedMult(SynergyJob job, float mult) => _attackSpeedMult[job] = mult;
        public static void SetHealMult(SynergyJob job, float mult) => _healMult[job] = mult;
        public static void SetHpMult(SynergyJob job, float mult) => _hpMult[job] = mult;

        public static float GetAttackMult(SynergyJob job) => _attackMult.TryGetValue(job, out var v) ? v : 1f;
        public static float GetAttackSpeedMult(SynergyJob job) => _attackSpeedMult.TryGetValue(job, out var v) ? v : 1f;
        public static float GetHealMult(SynergyJob job) => _healMult.TryGetValue(job, out var v) ? v : 1f;
        public static float GetHpMult(SynergyJob job) => _hpMult.TryGetValue(job, out var v) ? v : 1f;
    }
}
