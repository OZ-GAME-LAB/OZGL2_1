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

        public static float GetAttackMult(SynergyJob job, UnitSide side) => _attackMult.TryGetValue((job, side), out var v) ? v : 1f;
        public static float GetAttackSpeedMult(SynergyJob job, UnitSide side) => _attackSpeedMult.TryGetValue((job, side), out var v) ? v : 1f;
        public static float GetHealMult(SynergyJob job, UnitSide side) => _healMult.TryGetValue((job, side), out var v) ? v : 1f;
        public static float GetHpMult(SynergyJob job, UnitSide side) => _hpMult.TryGetValue((job, side), out var v) ? v : 1f;
    }
}
