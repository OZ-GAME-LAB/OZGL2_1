namespace OZGL2.Skill
{
    /// <summary>
    /// 특성 트리(영구강화)가 스킬 수치에 거는 배율/보정. 기본값 = 무보정.
    /// Skill 은 Progression 을 참조하지 않으므로, Progression(특성 트리) 쪽에서 이 객체의
    /// 필드를 채워 SkillManager 생성 시 넘긴다. 특성 랭크가 바뀌면 같은 인스턴스를 갱신하면 된다.
    /// </summary>
    public class SkillModifiers
    {
        public float PowerMult = 1f;         // 파괴의 정수
        public float CooldownMult = 1f;      // 신속한 주문 (곱, <1 이면 감소)
        public float RadiusMult = 1f;        // 넓은 지배
        public float BuffDurationMult = 1f;  // 군단의 함성
        public int ReviveBonus = 0;          // 강령술 심화

        // 증강 전용 — 특성은 안 건드림 (치명타·에코는 특성 트리엔 없는 증강만의 재미)
        public float CritChance = 0f;        // 치명의 감각류 — 시전 시 이 확률로 피해 ×CritMultiplier
        public float EchoChance = 0f;        // 메아리 주문류 — 시전 시 이 확률로 쿨탐 없이 즉시 재시전
        public float OnHitSlowAmount = 0f;   // 언 화살촉류 — AreaDamage 피격 대상 이동속도 배율 (0=없음)

        public static readonly SkillModifiers None = new SkillModifiers();
    }
}
