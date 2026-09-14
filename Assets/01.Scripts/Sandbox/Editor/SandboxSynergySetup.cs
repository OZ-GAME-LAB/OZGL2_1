using System.IO;
using UnityEditor;
using UnityEngine;
using OZGL2.Synergy;

namespace OZGL2.Sandbox.EditorTools
{
    /// <summary>
    /// 시너지 6종(SO_Synergy) 생성 → Resources/Synergies/. SkillSandbox 자동 로드.
    /// 다시 실행하면 폴더 비우고 재생성.
    /// </summary>
    public static class SandboxSynergySetup
    {
        private const string Dir = "Assets/01.Scripts/Sandbox/Resources/Synergies";
        private const string ComboDir = "Assets/01.Scripts/Sandbox/Resources/ComboSynergies";

        [MenuItem("OZGL2/Sandbox/Create Synergy Assets (시너지 6)")]
        public static void CreateAll()
        {
            Directory.CreateDirectory(Dir);
            foreach (string f in Directory.GetFiles(Dir, "*.asset"))
                AssetDatabase.DeleteAsset(f.Replace('\\', '/'));

            int n = 0;
            n += S(SynergyJob.Warrior, "전사", SynergyEffect.WarriorAttack,
                0.10f, 0.25f, "전사 공격력 +10%", "전사 공격력 +25%", "스텁");
            n += S(SynergyJob.Shield, "방패병", SynergyEffect.ShieldHp,
                0.15f, 0.35f, "방패병 체력 +15%", "방패병 체력 +35%", "샌드박스");
            n += S(SynergyJob.Archer, "궁수", SynergyEffect.ArcherAttackSpeed,
                0.12f, 0.25f, "궁수 공격속도 +12%", "궁수 공격속도 +25%", "스텁");
            n += S(SynergyJob.Mage, "마법사", SynergyEffect.MageAttack,
                0.12f, 0.25f, "마법사 공격력 +12%", "마법사 공격력 +25%", "스텁");
            n += S(SynergyJob.Rogue, "도적", SynergyEffect.RogueAttackSpeed,
                0.15f, 0.30f, "도적 공격속도 +15%", "도적 공격속도 +30%", "스텁");
            n += S(SynergyJob.Healer, "힐러", SynergyEffect.HealerHealAmount,
                0.20f, 0.45f, "힐러 회복량 +20%", "힐러 회복량 +45%", "스텁");

            Directory.CreateDirectory(ComboDir);
            foreach (string f in Directory.GetFiles(ComboDir, "*.asset"))
                AssetDatabase.DeleteAsset(f.Replace('\\', '/'));

            int nc = 0;
            nc += C(SynergyJob.Warrior, SynergyJob.Shield, "돌격 대형", ComboSynergyEffect.FrontlineAssault,
                0.15f, "전사+방패병 각 2명 이상 — 전사 공격력 +15%, 방패병 체력 +15%", "스텁");
            nc += C(SynergyJob.Archer, SynergyJob.Mage, "원거리 포격대", ComboSynergyEffect.RangedBarrage,
                0.15f, "궁수+마법사 각 2명 이상 — 마왕 스킬 치명타 확률 +15%p", "샌드박스");
            nc += C(SynergyJob.Rogue, SynergyJob.Archer, "질풍 사냥꾼", ComboSynergyEffect.SwiftHunters,
                0.15f, "도적+궁수 각 2명 이상 — 도적·궁수 공격속도 +15%", "스텁");
            nc += C(SynergyJob.Healer, SynergyJob.Shield, "불멸의 방벽", ComboSynergyEffect.GuardiansBlessing,
                0.15f, "힐러+방패병 각 2명 이상 — 방패병 체력 +15%, 힐러 회복량 +15%", "스텁");
            nc += C(SynergyJob.Healer, SynergyJob.Mage, "현자의 결속", ComboSynergyEffect.SagesBond,
                0.10f, "힐러+마법사 각 2명 이상 — 마왕 스킬 쿨타임 -10%", "샌드박스");
            nc += C(SynergyJob.Warrior, SynergyJob.Rogue, "학살자 콤보", ComboSynergyEffect.ExecutionerCombo,
                0.20f, "전사+도적 각 2명 이상 — 처치 시 추가 데미지 +20%", "스텁");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OZGL2] 시너지 {n}개 + 조합 시너지 {nc}개 생성 → {Dir} / {ComboDir}");
        }

        private static int S(SynergyJob job, string name, SynergyEffect effect,
            float t1, float t2, string t1Desc, string t2Desc, string conn)
        {
            var d = ScriptableObject.CreateInstance<SynergyData>();
            d.job = job;
            d.displayName = name;
            d.effect = effect;
            d.tier1Threshold = 3;
            d.tier2Threshold = 5;
            d.tier1Value = t1;
            d.tier2Value = t2;
            d.tier1Desc = t1Desc;
            d.tier2Desc = t2Desc;
            d.connStatus = conn;
            AssetDatabase.CreateAsset(d, $"{Dir}/SO_Synergy_{job}.asset");
            return 1;
        }

        private static int C(SynergyJob jobA, SynergyJob jobB, string name, ComboSynergyEffect effect,
            float value, string desc, string conn)
        {
            var d = ScriptableObject.CreateInstance<ComboSynergyData>();
            d.jobA = jobA;
            d.jobB = jobB;
            d.requiredCountEach = 2;
            d.displayName = name;
            d.effect = effect;
            d.value = value;
            d.desc = desc;
            d.connStatus = conn;
            AssetDatabase.CreateAsset(d, $"{ComboDir}/SO_ComboSynergy_{jobA}_{jobB}.asset");
            return 1;
        }
    }
}
