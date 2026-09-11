using System.IO;
using UnityEditor;
using UnityEngine;
using OZGL2.Augment;

namespace OZGL2.Sandbox.EditorTools
{
    /// <summary>
    /// 증강 카드 30개(SO_Augment) 생성 → Resources/Augments/. SkillSandbox 자동 로드.
    /// 전부 유니크(한 번 고르면 이번 런에서 다시 안 뜸) — 스택 없이 다양성으로 재플레이 재미.
    /// 다시 실행하면 폴더 비우고 재생성.
    /// </summary>
    public static class SandboxAugmentSetup
    {
        private const string Dir = "Assets/01.Scripts/Sandbox/Resources/Augments";

        [MenuItem("OZGL2/Sandbox/Create Augment Assets (증강 30)")]
        public static void CreateAll()
        {
            Directory.CreateDirectory(Dir);
            foreach (string f in Directory.GetFiles(Dir, "*.asset"))
                AssetDatabase.DeleteAsset(f.Replace('\\', '/'));

            int n = 0;

            // ═══ 실버(1) — R10·R20 전용. 기본기 ═══
            n += A("skill_damage", "화력 강화", "스킬 피해 +8%", 1, 12f, AugmentEffect.SkillDamage, 0.08f, "실제");
            n += A("skill_cooldown", "냉각 코일", "스킬 쿨타임 −5%", 1, 12f, AugmentEffect.SkillCooldown, 0.05f, "실제");
            n += A("shield_s", "수호의 방패", "모든 몬스터가 첫 피격 1회를 무효화하는 보호막을 두르고 시작", 1, 9f, AugmentEffect.MonsterShield, 1f, "샌드박스");
            n += A("mon_hp", "불굴의 대열", "몬스터 체력 +8%", 1, 10f, AugmentEffect.MonHp, 0.08f, "샌드박스");
            n += A("mon_speed", "재빠른 발놀림", "몬스터 공격속도 +6%", 1, 9f, AugmentEffect.MonAttackSpeed, 0.06f, "스텁");
            n += A("onhit_s", "언 화살촉", "광역 스킬에 맞은 용사 이동속도 30% 감소(2초)", 1, 9f, AugmentEffect.OnHitSlow, 0.70f, "실제");
            n += A("crit_s", "치명의 감각", "스킬 시전 시 12% 확률로 피해 ×1.5", 1, 9f, AugmentEffect.CritChance, 0.12f, "실제");
            n += A("eco_xp_s", "여신의 축복", "XP 획득 +10%", 1, 10f, AugmentEffect.XpGain, 0.10f, "실제");
            n += A("util_heal", "응급 처치", "즉시 몬스터 전체 체력 20% 회복", 1, 9f, AugmentEffect.InstantHealMonsters, 0.20f, "샌드박스", instant: true);
            n += A("util_xp_s", "즉각 강타", "즉시 XP +150", 1, 8f, AugmentEffect.InstantXp, 150f, "실제", instant: true);

            // ═══ 골드(2) — R30·R40 전용. 확실히 강함 ═══
            n += A("skill_radius", "확장 좌표", "스킬 반경 +10%", 2, 9f, AugmentEffect.SkillRadius, 0.10f, "실제");
            n += A("skill_buffdur", "이중 시전", "아군 버프·오라 지속 +15%", 2, 8f, AugmentEffect.SkillBuffDuration, 0.15f, "실제");
            n += A("skill_cooldown_g", "가속 마법진", "스킬 쿨타임 −8%", 2, 8f, AugmentEffect.SkillCooldown, 0.08f, "실제");
            n += A("echo_g", "메아리 주문", "스킬 시전 시 15% 확률로 쿨탐 없이 즉시 재시전", 2, 8f, AugmentEffect.EchoRecast, 0.15f, "실제");
            n += A("predator_g", "포식자의 본능", "몬스터 공격력 +6%, 공격속도 +6% 동시 상승", 2, 8f, AugmentEffect.PredatorInstinct, 0.06f, "스텁");
            n += A("frost_g", "빙결의 창", "용사 이동속도 −4%, 방어력 −4%p 동시 감소", 2, 8f, AugmentEffect.FrostLance, 0.04f, "스텁");
            n += A("hero_vuln", "절망 낙인", "용사가 받는 스킬 피해 +9%", 2, 8f, AugmentEffect.HeroVulnerable, 0.09f, "샌드박스");
            n += A("cdkill_g", "처형자의 축복", "용사 처치 시 모든 스킬 쿨탐 −1초", 2, 8f, AugmentEffect.CooldownOnKill, 1f, "실제");
            n += A("eco_sp", "전리품 확대", "즉시 SP +2", 2, 7f, AugmentEffect.InstantSp, 2f, "실제", instant: true);
            n += A("util_reset", "재정비", "즉시 모든 스킬 쿨타임 초기화", 2, 6f, AugmentEffect.InstantResetCooldowns, 0f, "실제", instant: true);

            // ═══ 플래티넘(3) — R50~ 전용. 제일 강함, 판을 뒤집는 임팩트 ═══
            n += A("skill_capstone", "마왕의 진노", "전 스킬 피해 +20%, 쿨타임 −12%", 3, 7f, AugmentEffect.SkillCapstone, 0.20f, "실제");
            n += A("skill_radius_p", "광역 지배 강화", "스킬 반경 +15%", 3, 6f, AugmentEffect.SkillRadius, 0.15f, "실제");
            n += A("crit_p", "치명의 폭풍", "스킬 시전 시 25% 확률로 피해 ×1.5", 3, 7f, AugmentEffect.CritChance, 0.25f, "실제");
            n += A("echo_p", "심판의 메아리", "스킬 시전 시 30% 확률로 쿨탐 없이 즉시 재시전", 3, 7f, AugmentEffect.EchoRecast, 0.30f, "실제");
            n += A("revive_p", "불사의 진영", "몬스터가 죽으면 30% 확률로 즉시 부활", 3, 6f, AugmentEffect.MonsterReviveChance, 0.30f, "실제");
            n += A("cdkill_p", "처형자의 진노", "용사 처치 시 모든 스킬 쿨탐 −2.5초", 3, 7f, AugmentEffect.CooldownOnKill, 2.5f, "실제");
            n += A("xpalive_p", "백성의 성원", "용사 처치 XP + 생존 몬스터 수 × 5", 3, 6f, AugmentEffect.XpPerAliveMonster, 5f, "실제");
            n += A("eco_xp_p", "풍요의 축복", "XP 획득 +18%", 3, 6f, AugmentEffect.XpGain, 0.18f, "실제");
            n += A("explode_p", "연쇄 폭발", "용사가 죽으면 그 자리에서 주변 용사에게 40 피해", 3, 6f, AugmentEffect.ExplodeOnDeath, 40f, "실제");
            n += A("util_xp_p", "각성의 순간", "즉시 XP +400", 3, 6f, AugmentEffect.InstantXp, 400f, "실제", instant: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OZGL2] 증강 {n}개 생성 → {Dir}");
        }

        private static int A(string id, string name, string desc, int tier, float weight,
            AugmentEffect effect, float value, string conn, bool instant = false)
        {
            var d = ScriptableObject.CreateInstance<AugmentData>();
            d.augmentId = id;
            d.displayName = name;
            d.description = desc;
            d.tier = tier;
            d.weight = weight;
            d.effect = effect;
            d.value = value;
            d.maxStack = 1; // 전부 유니크 — 한 번 고르면 이번 런에서 다시 안 뜸
            d.isInstant = instant;
            d.connStatus = conn;
            AssetDatabase.CreateAsset(d, $"{Dir}/SO_Augment_{id}.asset");
            return 1;
        }
    }
}
