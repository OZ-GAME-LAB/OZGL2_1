using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OZGL2.Sandbox.EditorTools
{
    /// <summary>
    /// Vefects "Pixel Craft VFX" 는 Built-in RP 셰이더(SH_Vefects_BIRP_*)만 들어있어 URP 에서 마젠타로 보인다.
    /// 이 메뉴가 팩 안의 모든 머티리얼 셰이더를 URP 파티클 Unlit 으로 바꾸고 텍스처·틴트를 옮긴다.
    ///
    /// gitignore 된 외부 에셋을 로컬에서 수정하는 것 — 커밋 안 됨, 각자 한 번씩 실행.
    /// 정식 통합 시엔 팀(희수)이 URP 대응 방식을 정한다.
    /// </summary>
    public static class VefectsUrpFix
    {
        private const string VefectsRoot = "Assets/98.ExternalAssets/00.LocalStaging/00.Packages/Vefects";

        [MenuItem("OZGL2/Sandbox/Fix Vefects Materials for URP")]
        public static void Fix()
        {
            var urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urp == null)
            {
                Debug.LogError("[OZGL2] URP Particles/Unlit 셰이더를 못 찾음. URP 패키지 확인.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { VefectsRoot });
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[OZGL2] {VefectsRoot} 에서 머티리얼을 못 찾음 (Vefects 임포트됨?).");
                return;
            }

            int changed = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == urp)
                {
                    continue;
                }

                if (mat.shader != null && !mat.shader.name.StartsWith("Vefects"))
                {
                    continue; // 팩 셰이더가 아니면 건드리지 않음
                }

                Texture tex = GetFirst(mat, "_MainTexture", "_MainTex", "_BaseMap");
                Color tint = GetFirstColor(mat, "_OverallTint", "_TintColor", "_Color", "_BaseColor");
                if (tint.a <= 0f)
                {
                    tint.a = 1f;
                }

                mat.shader = urp;
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                }

                mat.SetColor("_BaseColor", tint);

                // 투명 + 가산 블렌드
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 2f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHATEST_ON");

                EditorUtility.SetDirty(mat);
                changed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[OZGL2] Vefects 머티리얼 {changed}개 → URP 파티클 Unlit 로 변환.");
        }

        private static Texture GetFirst(Material mat, params string[] names)
        {
            foreach (string n in names)
            {
                if (mat.HasProperty(n))
                {
                    var t = mat.GetTexture(n);
                    if (t != null)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private static Color GetFirstColor(Material mat, params string[] names)
        {
            foreach (string n in names)
            {
                if (mat.HasProperty(n))
                {
                    return mat.GetColor(n);
                }
            }

            return Color.white;
        }
    }
}
