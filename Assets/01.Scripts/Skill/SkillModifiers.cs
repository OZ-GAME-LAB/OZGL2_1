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

        public static readonly SkillModifiers None = new SkillModifiers();
    }
}
