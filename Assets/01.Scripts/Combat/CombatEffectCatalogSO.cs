using UnityEngine;

/// <summary>
/// 전투 이펙트(SPUM Ultimate Resource Bundle) 프리팹 모음. 유닛별 데이터가 아니라 공용 트리거
/// 기준(피격/힐/둔화/시전)이라 유닛 스탯 SO가 아닌 별도 카탈로그로 뺐다.
/// Resources/CombatEffectCatalog.asset로 저장해서 CombatEffects가 Resources.Load로 찾는다.
/// </summary>
[CreateAssetMenu(fileName = "CombatEffectCatalog", menuName = "MajokDefense/Combat Effect Catalog")]
public sealed class CombatEffectCatalogSO : ScriptableObject
{
    [Header("원샷 (재생 후 자동 소멸)")]
    public GameObject hitEffect;         // 피격 시(TakeDamage)
    public GameObject healEffect;        // 힐 받을 때(Heal)
    public GameObject magicCastEffect;   // 마법사 발사체 시전 순간
    public GameObject saintCastEffect;   // 힐러 시전 순간(시전자 위치)

    [Header("지속 (상태 만료 시 직접 제거)")]
    public GameObject slowEffect;        // 둔화 걸린 동안 유지

    [Header("공통 크기 배율 (원본 프리팹 대비)")]
    [Range(0.05f, 2f)]
    public float effectScale = 0.5f;     // 유닛 대비 원본 이펙트가 너무 커서 축소

    [Header("피격 이펙트 전용 조정 (타격이 잦아서 화면을 덮지 않도록 작고 흐리게)")]
    [Range(0.05f, 1f)]
    public float hitEffectScaleMultiplier = 0.5f; // effectScale에 추가로 곱함
    [Range(0.05f, 1f)]
    public float hitEffectAlpha = 0.5f;           // 파티클/스프라이트 알파에 곱함

    [Header("데미지 숫자")]
    public bool showDamageNumbers = true;
    [Min(0.5f)] public float damageNumberFontSize = 3f;
    public Color heroDamageColor = new Color(1f, 0.95f, 0.6f, 1f);      // 용사(적)가 맞을 때
    public Color demonArmyDamageColor = new Color(1f, 0.4f, 0.4f, 1f);  // 마왕군(아군)이 맞을 때
    public Color healNumberColor = new Color(0.4f, 1f, 0.45f, 1f);      // 회복 숫자(+N)
    [Range(0.2f, 1.2f)] public float damageNumberHeightRatio = 0.55f; // 시작 높이 = 스프라이트 키 x 이 값(1이면 머리 끝)
    [Min(0.1f)] public float damageNumberRiseDistance = 0.5f;
    [Min(0.1f)] public float damageNumberDuration = 0.7f;
}
