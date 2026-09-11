using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using OZGL2.Skill;

namespace OZGL2.Sandbox.EditorTools
{
    /// <summary>
    /// SO_Skill 전체(딜·디버프·버프·궁극) 생성 + Pixel Art RPG VFX 연결.
    /// 결과물 Resources/Skills/ → SkillSandbox 자동 로드. 다시 실행하면 덮어씀.
    /// </summary>
    public static class SandboxSkillSetup
    {
        private const string Dir = "Assets/01.Scripts/Sandbox/Resources/Skills";

        private const string FireBall = "5108265b5ba89a448b4738200a60ebe2";
        private const string FireExplosion01 = "b0b17559d1ba4c0428a7204e6cdd25de";
        private const string FireExplosion02 = "2fb13dafea85206428417f5761f1a577";
        private const string FireTornado = "0e450ea5d0ad674418845ff93c54f467";
        private const string IceProjectile = "9159edd52a26f5a4ba2d1654aa1b8250";
        private const string IceSpike = "93a7ba6d9674b424ba2db0fa8d8bb1e4";
        private const string IceShield = "cae65cca59209c641b5bcf36a06bdb36";
        private const string IceRock = "c093de0f120995c4b91ffec42d2e945c";
        private const string IceSlam = "e20edd25e3970d44fb0ba3f02d897bd8";
        private const string ElectricLightning01 = "945b93ccedb48d54cb3718120dbcea6b";
        private const string HolyProjectile = "fd0a9d81491b0104bb0547b6499bf9a0";
        private const string HolyBall = "3e30882327b1d264f952e93b5ea30646";
        private const string HolyCross = "2d863d5876bc37a4a960ab6e0f04c264";
        private const string HolyBlessing = "12e4e2e032d1df642b914575a8fcc241";
        private const string HolyWing = "d6bb0c10fa54e124883f9cc34364d5e3";
        private const string EarthGrow = "08db4dda7ffaf4b4ea061f18a93f3b92";
        private const string EarthLavaBuble = "f531d6bdff5761348adce37db1ab4324";
        private const string EarthHealing = "d5b6161c1d08a6242ad6140e4b69605f";
        private const string EarthShield = "8a3afc960c8bdcf4ba3d1d25f6fc88e5";
        private const string EarthSpin = "e1bd4ab31e7203b47a0681a88693f38e";
        private const string VoidBlackHole = "7ec41a00cb4aaf140b8ed8c9dd9c33e3";
        private const string VoidPortal = "88edfecdc8db16842b747b87a473f47b";
        private const string VoidExplosion01 = "30ff0b7054183d049974c8c5bbcc73fa";

        [MenuItem("OZGL2/Sandbox/Create Skill Assets (전체 · VFX 연결)")]
        public static void CreateAll()
        {
            Directory.CreateDirectory(Dir);

            // 기존 SO_Skill 전부 삭제 후 재생성 (이름 바뀐 것·삭제된 것 잔존 방지)
            if (Directory.Exists(Dir))
            {
                foreach (string f in Directory.GetFiles(Dir, "*.asset"))
                {
                    AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
                }
            }

            int n = 0;

            // ── 딜
            n += B("Damage_01_Fireball", d => { Base(d, "화염구", SkillCategory.Damage, 1, SkillCastMode.Targeted, SkillEffectType.AreaDamage, 12f);
                d.skillPower = 120f; d.radius = 1.5f; d.projectileVfx = V(FireBall); d.castVfx = V(FireExplosion01); });

            n += B("Damage_02_IceShard", d => { Base(d, "얼음 가시", SkillCategory.Damage, 1, SkillCastMode.Targeted, SkillEffectType.AreaDamage, 10f);
                d.skillPower = 90f; d.radius = 1.3f; d.projectileVfx = V(IceProjectile); d.castVfx = V(IceSpike); });

            n += B("Damage_03_ChainLightning", d => { Base(d, "연쇄 번개", SkillCategory.Damage, 2, SkillCastMode.Targeted, SkillEffectType.ChainDamage, 15f);
                d.skillPower = 80f; d.chainCount = 3; d.chainInterval = 0.1f; d.perTargetVfx = V(ElectricLightning01); });

            n += B("Damage_04_HolyArrow", d => { Base(d, "신성 화살", SkillCategory.Damage, 2, SkillCastMode.Targeted, SkillEffectType.SingleDamage, 8f);
                d.skillPower = 200f; d.projectileVfx = V(HolyBall); d.castVfx = V(HolyCross); });

            n += B("Damage_05_Earthquake", d => { Base(d, "지진", SkillCategory.Damage, 2, SkillCastMode.Targeted, SkillEffectType.Knockback, 18f);
                d.skillPower = 60f; d.radius = 3f; d.force = 6f; d.castVfx = V(EarthGrow); });

            n += B("Damage_06_FireTornado", d => { Base(d, "화염 회오리", SkillCategory.Damage, 2, SkillCastMode.Targeted, SkillEffectType.MovingZone, 22f);
                d.radius = 1.6f; d.duration = 4f; d.zoneEffect = ZoneEffect.DamageOverTime; d.zoneMagnitude = 24f; d.zoneTick = 0.2f;
                d.zoneMoveSpeed = 8f; d.force = 8f; d.castVfx = V(FireTornado); });

            n += B("Damage_07_Meteor", d => { Base(d, "운석 낙하", SkillCategory.Damage, 3, SkillCastMode.Targeted, SkillEffectType.AreaDamage, 30f);
                d.skillPower = 400f; d.radius = 4f; d.castDelay = 0.8f; d.fallFromSky = true;
                d.projectileVfx = V(FireBall); d.castVfx = V(FireExplosion02); });

            n += B("Damage_08_VoidCollapse", d => { Base(d, "공허 붕괴", SkillCategory.Damage, 3, SkillCastMode.Targeted, SkillEffectType.Vacuum, 28f);
                d.skillPower = 150f; d.radius = 2.5f; d.force = 7f; d.castVfx = V(VoidBlackHole); d.finishVfx = V(VoidExplosion01); });

            // ── 디버프 (시간 정지만 즉시, 나머지 조준형)
            n += B("Debuff_01_KnockbackWave", d => { Base(d, "넉백 파동", SkillCategory.Debuff, 1, SkillCastMode.Targeted, SkillEffectType.Knockback, 10f);
                d.skillPower = 0f; d.radius = 2.5f; d.force = 8f; d.castVfx = V(EarthSpin); d.scaleCastVfxToRadius = true; });

            n += B("Debuff_02_SlowMire", d => { Base(d, "감속 늪", SkillCategory.Debuff, 1, SkillCastMode.Targeted, SkillEffectType.PersistentZone, 14f);
                d.radius = 2f; d.duration = 5f; d.zoneEffect = ZoneEffect.Slow; d.zoneMagnitude = 0.5f; d.castVfx = V(EarthLavaBuble); });

            n += B("Debuff_03_FrostField", d => { Base(d, "빙결 결계", SkillCategory.Debuff, 2, SkillCastMode.Targeted, SkillEffectType.PersistentZone, 20f);
                d.radius = 2f; d.duration = 2.5f; d.zoneEffect = ZoneEffect.Stun; d.castVfx = V(IceShield); d.scaleCastVfxToRadius = true; });

            n += B("Debuff_04_CurseMark", d => { Base(d, "저주 낙인", SkillCategory.Debuff, 2, SkillCastMode.Targeted, SkillEffectType.PersistentZone, 16f);
                d.radius = 2f; d.duration = 4f; d.zoneEffect = ZoneEffect.Vulnerable; d.zoneMagnitude = 1.3f; d.castVfx = V(VoidPortal); });

            n += B("Debuff_05_TimeStop", d => { Base(d, "시간 정지", SkillCategory.Debuff, 3, SkillCastMode.Instant, SkillEffectType.Stun, 40f);
                d.radius = 99f; d.duration = 1.5f; d.castVfx = V(VoidPortal); });

            // ── 버프 (광폭화·강철피부 = 마왕 전체 버프, 즉시)
            n += B("Buff_01_Trap", d => { Base(d, "함정 설치", SkillCategory.Buff, 1, SkillCastMode.Targeted, SkillEffectType.PersistentZone, 8f);
                d.radius = 1.2f; d.duration = 12f; d.zoneEffect = ZoneEffect.Slow; d.zoneMagnitude = 0.4f; d.castVfx = V(EarthGrow); });

            n += B("Buff_02_Berserk", d => { Base(d, "광폭화", SkillCategory.Buff, 2, SkillCastMode.Instant, SkillEffectType.AllyBuff, 25f);
                d.duration = 8f; d.buffStat = BuffStat.AttackSpeed; d.buffMultiplier = 1.35f; d.perTargetVfx = V(HolyBall); });

            n += B("Buff_03_IronSkin", d => { Base(d, "강철 피부", SkillCategory.Buff, 2, SkillCastMode.Instant, SkillEffectType.AllyBuff, 25f);
                d.duration = 8f; d.buffStat = BuffStat.Defense; d.buffMultiplier = 1.3f; d.perTargetVfx = V(EarthShield); });

            n += B("Buff_04_BlessingAura", d => { Base(d, "축복의 오라", SkillCategory.Buff, 2, SkillCastMode.Targeted, SkillEffectType.PersistentZone, 18f);
                d.radius = 2.5f; d.duration = 6f; d.zoneEffect = ZoneEffect.Heal; d.zoneTarget = ZoneTarget.Allies; d.zoneMagnitude = 3f; d.zoneTick = 0.5f; d.castVfx = V(HolyBlessing); });

            n += B("Buff_05_VampiricRite", d => { Base(d, "흡혈 의식", SkillCategory.Buff, 3, SkillCastMode.Instant, SkillEffectType.HealAllies, 25f);
                d.duration = 0.4f; d.perTargetVfx = V(EarthHealing); });

            n += B("Buff_06_RaiseDead", d => { Base(d, "망자 부활", SkillCategory.Buff, 3, SkillCastMode.Instant, SkillEffectType.Revive, 35f);
                d.reviveCount = 3; d.perTargetVfx = V(HolyWing); });

            // ── 궁극 (화려하게)
            n += B("Ult_01_MeteorShower", d => { Base(d, "유성우", SkillCategory.Ultimate, 4, SkillCastMode.Instant, SkillEffectType.AreaDamage, 60f);
                d.skillPower = 150f; d.radius = 5f; d.barrageCount = 12; d.chainInterval = 0.1f; d.castVfx = V(FireExplosion02);
                d.flourishVfx = V(FireExplosion01); d.flourishCount = 8; });

            n += B("Ult_02_AbsoluteZero", d => { Base(d, "절대 영도", SkillCategory.Ultimate, 4, SkillCastMode.Instant, SkillEffectType.Stun, 90f);
                d.radius = 99f; d.duration = 4f; d.castVfx = V(IceSlam);
                d.flourishVfx = V(IceRock); d.flourishCount = 12; });

            n += B("Ult_03_Judgment", d => { Base(d, "심판", SkillCategory.Ultimate, 4, SkillCastMode.Instant, SkillEffectType.AreaDamage, 90f);
                d.skillPower = 600f; d.radius = 99f; d.castVfx = V(HolyBall);
                d.flourishVfx = V(HolyCross); d.flourishCount = 14; });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OZGL2] SO_Skill {n}종 생성 → {Dir}");
        }

        private static void Base(SkillData d, string name, SkillCategory cat, int tier,
            SkillCastMode mode, SkillEffectType fx, float cd)
        {
            d.skillId = name;
            d.displayName = name;
            d.category = cat;
            d.tier = tier;
            d.castMode = mode;
            d.effectType = fx;
            d.cooldown = cd;
            d.vfxIsUi = true;
            // reset (덮어쓰기 시 이전 값 잔존 방지)
            d.projectileVfx = null; d.castVfx = null; d.perTargetVfx = null; d.finishVfx = null; d.flourishVfx = null;
            d.flourishCount = 0; d.barrageCount = 1; d.castDelay = 0f; d.fallFromSky = false; d.scaleCastVfxToRadius = false;
        }

        private static int B(string assetName, Action<SkillData> cfg)
        {
            string path = $"{Dir}/{assetName}.asset";
            var d = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            bool isNew = d == null;
            if (isNew) d = ScriptableObject.CreateInstance<SkillData>();
            cfg(d);
            if (isNew) AssetDatabase.CreateAsset(d, path);
            else EditorUtility.SetDirty(d);
            return 1;
        }

        private static GameObject V(string guid)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(p))
            {
                Debug.LogWarning($"[OZGL2] VFX 못 찾음 (Pixel Art RPG VFX 임포트?) guid={guid}");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
    }
}
