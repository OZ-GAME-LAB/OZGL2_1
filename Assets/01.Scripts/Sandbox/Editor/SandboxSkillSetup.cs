using System.IO;
using UnityEditor;
using UnityEngine;
using OZGL2.Skill;

namespace OZGL2.Sandbox.EditorTools
{
    /// <summary>
    /// 메뉴에서 한 번 실행하면 SO_Skill_*.asset 6종을 만들고 Vefects 프리팹을 연결한다.
    /// 결과물은 Resources/Skills/ 에 들어가 SkillSandbox 가 자동으로 로드한다.
    ///
    /// Vefects 프리팹은 GUID 로 참조 → 팩만 임포트돼 있으면 경로가 달라도 찾는다.
    /// 없으면 해당 VFX 만 비워지고(코드 기본 연출로 폴백) 나머지는 정상 생성.
    /// </summary>
    public static class SandboxSkillSetup
    {
        private const string OutputDir = "Assets/01.Scripts/Sandbox/Resources/Skills";

        [MenuItem("OZGL2/Sandbox/Create Skill Assets (VFX 연결)")]
        public static void CreateSkillAssets()
        {
            Directory.CreateDirectory(OutputDir);

            Build("SO_Skill_Fireball", s =>
            {
                s.skillId = "fireball";
                s.displayName = "화염구";
                s.tier = 1;
                s.castMode = SkillCastMode.Targeted;
                s.effectType = SkillEffectType.AreaDamage;
                s.cooldown = 12f;
                s.skillPower = 120f;
                s.radius = 1.5f;
                s.projectileVfx = LoadVfx("5623c0a9e27c84749907d738fb5108bb"); // Fireball_Projectile
                s.castVfx = LoadVfx("801ad7830c8585646a774b610bae834f");       // Fireball_Impact
            });

            Build("SO_Skill_FrostField", s =>
            {
                s.skillId = "frost_field";
                s.displayName = "빙결 결계";
                s.tier = 2;
                s.castMode = SkillCastMode.Targeted;
                s.effectType = SkillEffectType.AreaStun;
                s.cooldown = 20f;
                s.radius = 2f;
                s.duration = 2f;
                s.castVfx = LoadVfx("13bf90c452825d54bb86f82e161d54b0");        // Shield (파랑 대용)
                s.scaleCastVfxToRadius = true;
            });

            Build("SO_Skill_ChainLightning", s =>
            {
                s.skillId = "chain_lightning";
                s.displayName = "연쇄 번개";
                s.tier = 2;
                s.castMode = SkillCastMode.Instant;
                s.effectType = SkillEffectType.ChainDamage;
                s.cooldown = 15f;
                s.skillPower = 80f;
                s.chainCount = 3;
                s.perTargetVfx = LoadVfx("ed43b3f4f4d6ef849acdcc7cad0fb9b4");   // Lightning_01
            });

            Build("SO_Skill_Meteor", s =>
            {
                s.skillId = "meteor";
                s.displayName = "운석 낙하";
                s.tier = 3;
                s.castMode = SkillCastMode.Instant;
                s.effectType = SkillEffectType.AreaDamage;
                s.cooldown = 30f;
                s.skillPower = 400f;
                s.radius = 99f;
                s.castVfx = LoadVfx("b37fb5fa4f572544984898286924cddc");        // Explosion_Big
            });

            Build("SO_Skill_Vampiric", s =>
            {
                s.skillId = "vampiric_rite";
                s.displayName = "흡혈 의식";
                s.tier = 3;
                s.castMode = SkillCastMode.Instant;
                s.effectType = SkillEffectType.HealAllies;
                s.cooldown = 25f;
                s.duration = 0.4f;
                s.perTargetVfx = LoadVfx("476d7e99bf5da3440a67db0596a4176e");   // Heal_01
            });

            Build("SO_Skill_TimeStop", s =>
            {
                s.skillId = "time_stop";
                s.displayName = "시간 정지";
                s.tier = 3;
                s.castMode = SkillCastMode.Instant;
                s.effectType = SkillEffectType.AreaStun;
                s.cooldown = 40f;
                s.radius = 99f;
                s.duration = 1.5f;
                s.castVfx = LoadVfx("34a940ebbf7dd54438c68003b3d7eb08");        // Magic_Impact
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OZGL2] SO_Skill 6종 생성 완료 → {OutputDir}");
        }

        private static void Build(string assetName, System.Action<SkillData> configure)
        {
            string path = $"{OutputDir}/{assetName}.asset";
            var data = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            bool isNew = data == null;
            if (isNew)
            {
                data = ScriptableObject.CreateInstance<SkillData>();
            }

            configure(data);

            if (isNew)
            {
                AssetDatabase.CreateAsset(data, path);
            }
            else
            {
                EditorUtility.SetDirty(data);
            }
        }

        private static GameObject LoadVfx(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[OZGL2] VFX 프리팹 못 찾음 (Vefects 임포트 필요?) guid={guid}");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}
