using UnityEngine;

namespace OZGL2.Skill
{
    /// <summary>스킬 발동 방식.</summary>
    public enum SkillCastMode
    {
        /// <summary>버튼 클릭 즉시 발동 (맵 전체 또는 자동 타겟). 연쇄 번개·운석 낙하·흡혈 의식·시간 정지.</summary>
        Instant,

        /// <summary>드래그앤드랍으로 위치 지정 후 발동. 화염구·빙결 결계.</summary>
        Targeted,
    }

    /// <summary>스킬 효과 유형. Execute 분기 기준.</summary>
    public enum SkillEffectType
    {
        AreaDamage,   // 지정 지점 반경 피해 — 화염구, 운석 낙하
        ChainDamage,  // 가장 가까운 대상부터 연쇄 피해 — 연쇄 번개
        AreaStun,     // 지정 지점 반경 정지 — 빙결 결계, 시간 정지
        HealAllies,   // 전 몬스터 회복 — 흡혈 의식 (아군 provider 붙으면 구현)
    }

    /// <summary>
    /// 스킬 1종의 정의. 수치는 밸런스 시트 "11.마왕·스킬" 시트와 1:1 대응.
    /// Assets → Create → OZGL2/Skill Data 로 생성.
    /// </summary>
    [CreateAssetMenu(menuName = "OZGL2/Skill Data", fileName = "SO_Skill_")]
    public class SkillData : ScriptableObject
    {
        [Header("식별")]
        public string skillId;
        public string displayName;
        [Range(1, 3)] public int tier = 1;

        [Header("발동")]
        public SkillCastMode castMode = SkillCastMode.Instant;
        public float cooldown = 12f;

        [Header("효과")]
        public SkillEffectType effectType = SkillEffectType.AreaDamage;

        [Header("수치")]
        [Tooltip("스킬 피해량 (AreaDamage·ChainDamage)")]
        public float skillPower = 100f;

        [Tooltip("효과 반경(칸). AreaDamage·AreaStun")]
        public float radius = 1.5f;

        [Tooltip("연쇄 대상 수. ChainDamage")]
        public int chainCount = 3;

        [Tooltip("정지 지속(초). AreaStun / 회복 비율(0~1). HealAllies")]
        public float duration = 2f;

        [Header("VFX (선택 — 비우면 코드 기본 연출)")]
        [Tooltip("조준형 발사체. 마왕 → 조준점으로 날아감 (화염구)")]
        public GameObject projectileVfx;

        [Tooltip("착탄·발동 지점 1회 (화염구 폭발, 운석, 빙결 장막)")]
        public GameObject castVfx;

        [Tooltip("영향받은 대상마다 1회 (연쇄 번개 감전, 흡혈 힐)")]
        public GameObject perTargetVfx;

        [Tooltip("castVfx 크기를 radius 에 맞춰 스케일")]
        public bool scaleCastVfxToRadius = false;
    }
}
