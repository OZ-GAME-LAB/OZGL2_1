using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OZGL2.Sandbox.EditorTools
{
    /// <summary>
    /// Pixel Art RPG VFX 프리팹을 어두운 무대에 격자 배치 + 전용 카메라.
    /// 스킬에 뭘 쓸지 훑어보는 용도. 다 보면 "VFX_Gallery" 삭제.
    /// </summary>
    public static class VfxGallery
    {
        private const string Root = "Assets/98.ExternalAssets/00.LocalStaging/00.Packages/Pixel Art";

        private static readonly string[] Prefabs =
        {
            "FireBall", "FireFlamme", "FireExplosion01", "FireExplosion02", "FireTornado", "FireSlash", "FireShield",
            "IceBall", "IceSpike", "IceProjectile", "IceSlash", "IceShield", "IceSlam",
            "ElectricBall", "ElectricLightning01", "ElectricLightning02", "ElectricExplosion", "ElectricTornado",
            "HolyBall", "HolyProjectile", "HolyCross", "HolyBlessing", "HolyWing", "HolySlash",
            "EarthBall", "EarthRock", "EarthGrow", "EarthLava", "EarthHealing", "EarthShield", "EarthSpin",
            "VoidBall", "VoidBlackHole", "VoidPortal", "VoidExplosion01", "VoidSlash",
            "WindGust", "WaterWave", "Explosion_003", "Explosion_005", "Explosion_008",
            "Attack_Slash_001", "Attack_Slash_004", "Attack_Slash_007",
        };

        [MenuItem("OZGL2/Sandbox/Spawn VFX Gallery (Pixel Art)")]
        public static void Spawn()
        {
            var old = GameObject.Find("VFX_Gallery");
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject("VFX_Gallery");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            const int perRow = 7;
            const float sp = 3.2f;
            int col = 0, row = 0, found = 0;

            foreach (var name in Prefabs)
            {
                string path = AssetDatabase.FindAssets($"{name} t:Prefab", new[] { Root })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => p.EndsWith($"/{name}.prefab") && !p.Contains("/Demo/"));
                if (path == null) { Debug.LogWarning($"[OZGL2] 갤러리: {name} 못 찾음"); continue; }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var pos = new Vector3((col - (perRow - 1) * 0.5f) * sp, -row * sp, 0f);

                var canvasGo = new GameObject($"vfx_{name}");
                canvasGo.transform.SetParent(root.transform);
                canvasGo.transform.position = pos;
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvasGo.transform.localScale = Vector3.one * 0.015f;
                var child = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
                child.transform.localPosition = Vector3.zero;

                foreach (var ps in canvasGo.GetComponentsInChildren<ParticleSystem>())
                {
                    var m = ps.main; m.loop = true; m.playOnAwake = true;
                }

                var lbl = new GameObject($"label_{name}");
                lbl.transform.SetParent(root.transform);
                lbl.transform.position = pos + new Vector3(0f, -1.4f, 0f);
                var tm = lbl.AddComponent<TextMesh>();
                tm.text = name;
                tm.fontSize = 40; tm.characterSize = 0.055f;
                tm.anchor = TextAnchor.UpperCenter; tm.color = Color.white; tm.font = font;
                lbl.GetComponent<MeshRenderer>().sharedMaterial = font.material;

                found++;
                if (++col >= perRow) { col = 0; row++; }
            }

            int rows = row + (col > 0 ? 1 : 0);
            float cy = -(rows - 1) * sp * 0.5f;

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "DarkBackdrop";
            bg.transform.SetParent(root.transform);
            bg.transform.position = new Vector3(0f, cy, 2f);
            bg.transform.localScale = new Vector3(perRow * sp + 6f, rows * sp + 6f, 1f);
            Object.DestroyImmediate(bg.GetComponent<Collider>());
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(unlit);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.07f, 0.07f, 0.09f));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.07f, 0.07f, 0.09f));
            bg.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var camGo = new GameObject("GalleryCamera");
            camGo.transform.SetParent(root.transform);
            camGo.transform.position = new Vector3(0f, cy, -12f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = rows * sp * 0.6f + 3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.09f);
            cam.depth = 10;

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"[OZGL2] VFX 갤러리 {found}개 (Pixel Art). Play → GalleryCamera. 다 보면 VFX_Gallery 삭제.");
        }
    }
}
