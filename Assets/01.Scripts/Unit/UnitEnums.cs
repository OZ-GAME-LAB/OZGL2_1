/// <summary>
/// 유닛 진영 구분.
/// 기획서 용어 통일 기준: 용사 = 침입자(Hero), 마왕군 = 방어유닛(DemonArmy).
/// (SPUM 쪽 PlayerState와 이름이 겹치지 않도록 별도 enum으로 분리해둠)
/// </summary>
public enum UnitSide
{
    Hero,       // 용사 (침입자)
    DemonArmy,  // 마왕군 (방어 유닛)
}

/// <summary>
/// 피격 효과음 종류. 데미지 출처에 따라 다른 소리를 내기 위해 TakeDamage에 같이 넘긴다.
/// </summary>
public enum HitSoundKind
{
    Physical,   // 근접·화살 (Hit)
    Magic,      // 마법사 발사체·스킬 (HitMagic)
    None,       // 소리 없음 (화상 도트 등)
}

/// <summary>
/// 유닛의 현재 행동 상태. Day1은 Idle/Move만 실제로 쓰이고,
/// Attack/Dead는 Day2(전투 판정)에서 채워 넣는다.
/// </summary>
public enum UnitState
{
    Idle,
    Move,
    Attack,
    Dead,
}
