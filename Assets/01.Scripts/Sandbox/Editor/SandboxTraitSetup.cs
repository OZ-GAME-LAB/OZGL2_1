using System.IO;
using UnityEditor;
using UnityEngine;
using OZGL2.Progression;

namespace OZGL2.Sandbox.EditorTools
{
    /// <summary>
    /// 특성 노드 40개(SO_Trait) 생성 → Resources/Traits/. SkillSandbox 가 자동 로드.
    /// 다시 실행하면 폴더 비우고 재생성.
    /// </summary>
    public static class SandboxTraitSetup
    {
        private const string Dir = "Assets/01.Scripts/Sandbox/Resources/Traits";

        private static readonly int[] Line = { 1, 1, 1 };  // 스탯 노드 — 랭크당 1 LP
        private static readonly int[] Big = { 1, 1 };       // 그리드·슬롯 (max 2)
        private static readonly int[] Cap = { 2, 2, 2 };    // 캡스톤 — 랭크당 2 LP

        [MenuItem("OZGL2/Sandbox/Create Trait Assets (특성 40)")]
        public static void CreateAll()
        {
            Directory.CreateDirectory(Dir);
            foreach (string f in Directory.GetFiles(Dir, "*.asset"))
                AssetDatabase.DeleteAsset(f.Replace('\\', '/'));

            int n = 0;
            var B = TraitBranch.Monster;

            // ───────── 몬스터
            n += N(TraitId.Mon_A1, "예리함", "몬스터 공격력 +2% / 랭크", B, TraitLine.A, TraitEffect.MonAttack, 0.02f, 3, Line, "샌드박스");
            n += N(TraitId.Mon_A2, "맹공", "몬스터 공격력 +3% / 랭크", B, TraitLine.A, TraitEffect.MonAttack, 0.03f, 3, Line, "샌드박스", TraitId.Mon_A1);
            n += N(TraitId.Mon_A3, "처형 본능", "체력 30% 이하 용사에게 주는 피해 +8% / 랭크", B, TraitLine.A, TraitEffect.MonExecute, 0.08f, 3, Line, "스텁", TraitId.Mon_A2);
            n += N(TraitId.Mon_B1, "강건함", "몬스터 체력 +3% / 랭크", B, TraitLine.B, TraitEffect.MonHp, 0.03f, 3, Line, "샌드박스");
            n += N(TraitId.Mon_B2, "불굴의 살", "몬스터 체력 +4% / 랭크", B, TraitLine.B, TraitEffect.MonHp, 0.04f, 3, Line, "샌드박스", TraitId.Mon_B1);
            n += N(TraitId.Mon_B3, "철갑", "몬스터 방어력 +2%p / 랭크", B, TraitLine.B, TraitEffect.MonDefense, 0.02f, 3, Line, "스텁", TraitId.Mon_B2);
            n += N(TraitId.Mon_C1, "민첩", "몬스터 공격속도 +2% / 랭크", B, TraitLine.C, TraitEffect.MonAttackSpeed, 0.02f, 3, Line, "스텁");
            n += N(TraitId.Mon_C2, "연격", "몬스터 공격속도 +3% / 랭크", B, TraitLine.C, TraitEffect.MonAttackSpeed, 0.03f, 3, Line, "스텁", TraitId.Mon_C1);
            n += N(TraitId.Mon_C3, "관통 사격", "몬스터 공격 사거리 +5% / 랭크", B, TraitLine.C, TraitEffect.MonRange, 0.05f, 3, Line, "스텁", TraitId.Mon_C2);
            n += N(TraitId.Mon_Cap, "마왕군의 진격", "모든 몬스터 전 스탯 +4% / 랭크", B, TraitLine.Capstone, TraitEffect.CapMonster, 0.04f, 3, Cap, "샌드박스", TraitId.Mon_A3, TraitId.Mon_B3, TraitId.Mon_C3);

            // ───────── 용사 (약화)
            B = TraitBranch.Hero;
            n += N(TraitId.Hero_A1, "쇠약", "용사 공격력 −2% / 랭크", B, TraitLine.A, TraitEffect.HeroAttack, 0.02f, 3, Line, "스텁");
            n += N(TraitId.Hero_A2, "무력화", "용사 공격력 −3% / 랭크", B, TraitLine.A, TraitEffect.HeroAttack, 0.03f, 3, Line, "스텁", TraitId.Hero_A1);
            n += N(TraitId.Hero_A3, "나태", "용사 공격속도 −3% / 랭크", B, TraitLine.A, TraitEffect.HeroAttackSpeed, 0.03f, 3, Line, "스텁", TraitId.Hero_A2);
            n += N(TraitId.Hero_B1, "무장 해제", "용사 방어력 −2%p / 랭크", B, TraitLine.B, TraitEffect.HeroDefense, 0.02f, 3, Line, "스텁");
            n += N(TraitId.Hero_B2, "파쇄", "용사 방어력 −3%p / 랭크", B, TraitLine.B, TraitEffect.HeroDefense, 0.03f, 3, Line, "스텁", TraitId.Hero_B1);
            n += N(TraitId.Hero_B3, "취약 각인", "용사가 받는 스킬 피해 +5% / 랭크", B, TraitLine.B, TraitEffect.HeroVulnerable, 0.05f, 3, Line, "샌드박스", TraitId.Hero_B2);
            n += N(TraitId.Hero_C1, "둔족", "용사 이동속도 −3% / 랭크", B, TraitLine.C, TraitEffect.HeroMoveSpeed, 0.03f, 3, Line, "스텁");
            n += N(TraitId.Hero_C2, "수렁", "용사 이동속도 −4% / 랭크", B, TraitLine.C, TraitEffect.HeroMoveSpeed, 0.04f, 3, Line, "스텁", TraitId.Hero_C1);
            n += N(TraitId.Hero_C3, "진창", "용사 이동속도 −5% / 랭크", B, TraitLine.C, TraitEffect.HeroMoveSpeed, 0.05f, 3, Line, "스텁", TraitId.Hero_C2);
            n += N(TraitId.Hero_Cap, "절망의 낙인", "모든 용사 전 스탯 −5% / 랭크", B, TraitLine.Capstone, TraitEffect.CapHero, 0.05f, 3, Cap, "샌드박스", TraitId.Hero_A3, TraitId.Hero_B3, TraitId.Hero_C3);

            // ───────── 스킬
            B = TraitBranch.Skill;
            n += N(TraitId.Sk_A1, "파괴의 정수", "스킬 피해 +5% / 랭크", B, TraitLine.A, TraitEffect.SkillDamage, 0.05f, 3, Line, "실제");
            n += N(TraitId.Sk_A2, "정밀 주문", "스킬 피해 +5% / 랭크", B, TraitLine.A, TraitEffect.SkillDamage, 0.05f, 3, Line, "실제", TraitId.Sk_A1);
            n += N(TraitId.Sk_A3, "마력 관통", "스킬이 용사 방어력 10% 무시 / 랭크", B, TraitLine.A, TraitEffect.SkillArmorPen, 0.10f, 3, Line, "스텁", TraitId.Sk_A2);
            n += N(TraitId.Sk_B1, "신속한 주문", "스킬 쿨타임 −3% / 랭크", B, TraitLine.B, TraitEffect.SkillCooldown, 0.03f, 3, Line, "실제");
            n += N(TraitId.Sk_B2, "가속", "스킬 쿨타임 −3% / 랭크", B, TraitLine.B, TraitEffect.SkillCooldown, 0.03f, 3, Line, "실제", TraitId.Sk_B1);
            n += N(TraitId.Sk_B3, "시간 왜곡", "궁극기(4티어) 쿨타임 −15% / 랭크", B, TraitLine.B, TraitEffect.SkillUltCooldown, 0.15f, 3, Line, "실제", TraitId.Sk_B2);
            n += N(TraitId.Sk_C1, "광역 지배", "스킬 반경 +5% / 랭크", B, TraitLine.C, TraitEffect.SkillRadius, 0.05f, 3, Line, "실제");
            n += N(TraitId.Sk_C2, "군단의 함성", "아군 버프·오라 지속 +12% / 랭크", B, TraitLine.C, TraitEffect.SkillBuffDuration, 0.12f, 3, Line, "실제", TraitId.Sk_C1);
            n += N(TraitId.Sk_C3, "추가 지령", "스킬 장착 슬롯 +1 / 랭크 (3→5)", B, TraitLine.C, TraitEffect.SkillSlot, 1f, 2, Big, "실제", TraitId.Sk_C2);
            n += N(TraitId.Sk_Cap, "마왕의 권능", "모든 스킬 피해 +10%, 쿨타임 −6% / 랭크", B, TraitLine.Capstone, TraitEffect.CapSkill, 1f, 3, Cap, "실제", TraitId.Sk_A3, TraitId.Sk_B3, TraitId.Sk_C3);

            // ───────── 재화·성장
            B = TraitBranch.Economy;
            n += N(TraitId.Eco_A1, "통찰", "XP 획득 +6% / 랭크", B, TraitLine.A, TraitEffect.XpGain, 0.06f, 3, Line, "실제");
            n += N(TraitId.Eco_A2, "심층 통찰", "XP 획득 +6% / 랭크", B, TraitLine.A, TraitEffect.XpGain, 0.06f, 3, Line, "실제", TraitId.Eco_A1);
            n += N(TraitId.Eco_A3, "약탈", "용사 처치 시 추가 XP +3 / 랭크", B, TraitLine.A, TraitEffect.KillXp, 3f, 3, Line, "샌드박스", TraitId.Eco_A2);
            n += N(TraitId.Eco_B1, "속성 성장", "레벨업 필요 XP −4% / 랭크", B, TraitLine.B, TraitEffect.XpNeed, 0.04f, 3, Line, "실제");
            n += N(TraitId.Eco_B2, "숙련의 결정", "레벨 5의 배수마다 즉시 XP +100 / 랭크", B, TraitLine.B, TraitEffect.LevelSurge, 100f, 3, Line, "실제", TraitId.Eco_B1);
            n += N(TraitId.Eco_B3, "가르침", "레벨 5·10·15·20 도달 시 보너스 LP +1 / 랭크", B, TraitLine.B, TraitEffect.MilestoneLp, 1f, 3, Line, "실제", TraitId.Eco_B2);
            n += N(TraitId.Eco_C1, "규모 확장", "전투 그리드 +1칸 / 랭크", B, TraitLine.C, TraitEffect.Grid, 1f, 2, Big, "스텁");
            n += N(TraitId.Eco_C2, "효율 편성", "시작 배치 코스트 +1 / 랭크", B, TraitLine.C, TraitEffect.DeployCost, 1f, 3, Line, "스텁", TraitId.Eco_C1);
            n += N(TraitId.Eco_C3, "대군세", "전투 그리드 +1칸 / 랭크", B, TraitLine.C, TraitEffect.Grid, 1f, 2, Big, "스텁", TraitId.Eco_C2);
            n += N(TraitId.Eco_Cap, "지배자의 지혜", "XP·LP 획득 +12%, 라운드 마일스톤마다 SP +1 / 랭크", B, TraitLine.Capstone, TraitEffect.CapEconomy, 0.12f, 3, Cap, "실제", TraitId.Eco_A3, TraitId.Eco_B3, TraitId.Eco_C3);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OZGL2] 특성 {n}개 생성 → {Dir}");
        }

        private static int N(TraitId id, string name, string desc, TraitBranch branch, TraitLine line,
            TraitEffect effect, float value, int maxRank, int[] costs, string conn, params TraitId[] parents)
        {
            var d = ScriptableObject.CreateInstance<TraitData>();
            d.id = id;
            d.displayName = name;
            d.description = desc;
            d.branch = branch;
            d.line = line;
            d.effect = effect;
            d.valuePerRank = value;
            d.maxRank = maxRank;
            d.rankCosts = (int[])costs.Clone();
            d.parents = parents != null && parents.Length > 0 ? (TraitId[])parents.Clone() : new TraitId[0];
            d.connStatus = conn;
            AssetDatabase.CreateAsset(d, $"{Dir}/SO_Trait_{id}.asset");
            return 1;
        }
    }
}
