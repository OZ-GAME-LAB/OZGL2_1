using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.Skill
{
    /// <summary>
    /// 스킬 이펙트(VFX)의 대표 색을 뽑아 사거리 표시 색으로 쓴다.
    /// 이펙트 이미지의 눈에 띄는 색(채도 높은 부분 위주)을 평균내고, 바닥 위에서 보이도록 채도·밝기를 끌어올린다.
    /// 스킬 데이터의 색 교체(vfxRecolor)·틴트(vfxTint)가 있으면 그 색을 따른다. 결과는 스킬별로 캐시한다.
    /// 이펙트가 없는(코드로 그린) 스킬은 false를 돌려주고, 부르는 쪽이 기본색을 쓴다.
    /// </summary>
    public static class SkillVfxColor
    {
        private static readonly Dictionary<SkillData, Color?> Cache = new Dictionary<SkillData, Color?>();
        private static readonly Dictionary<Sprite, Color?> SpriteCache = new Dictionary<Sprite, Color?>();
        private static readonly Dictionary<Texture2D, Color32[]> PixelCache = new Dictionary<Texture2D, Color32[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Cache.Clear();
            SpriteCache.Clear();
            PixelCache.Clear();
        }

        public static bool TryGet(SkillData d, out Color color)
        {
            color = default;
            if (d == null) return false;
            if (!Cache.TryGetValue(d, out var cached))
            {
                cached = Compute(d);
                Cache[d] = cached;
            }
            if (!cached.HasValue) return false;
            color = cached.Value;
            return true;
        }

        private static Color? Compute(SkillData d)
        {
            // 색 교체가 지정돼 있으면 그 색조를 쓴다
            if (d.vfxRecolor.a > 0.01f) return Vivid(d.vfxRecolor);

            foreach (var prefab in new[] { d.castVfx, d.projectileVfx, d.perTargetVfx, d.flourishVfx, d.finishVfx })
            {
                if (prefab == null) continue;
                Color? c = FromPrefab(prefab);
                if (!c.HasValue) continue;
                Color v = c.Value;
                Color.RGBToHSV(v, out _, out float sat, out _);
                if (sat < 0.12f) continue;
                if (d.vfxTint != Color.white && d.vfxTint.a > 0.01f) v = new Color(v.r * d.vfxTint.r, v.g * d.vfxTint.g, v.b * d.vfxTint.b, 1f);
                return Vivid(v);
            }
            return null;
        }

        private static Color? FromPrefab(GameObject prefab)
        {
            // 1) 스프라이트(UI Image / SpriteRenderer)로 그린 이펙트 — 중간쯤 프레임들의 평균색
            var sprites = new List<Sprite>();
            foreach (var image in prefab.GetComponentsInChildren<Image>(true)) if (image.sprite != null) sprites.Add(image.sprite);
            foreach (var sr in prefab.GetComponentsInChildren<SpriteRenderer>(true)) if (sr.sprite != null) sprites.Add(sr.sprite);
            if (sprites.Count > 0)
            {
                // 너무 앞·뒤(점·잔상) 프레임은 피하고 가운데 근처 최대 3장만 쓴다
                var picks = new List<Sprite>();
                int mid = sprites.Count / 2;
                for (int i = Mathf.Max(0, mid - 1); i <= Mathf.Min(sprites.Count - 1, mid + 1); i++) picks.Add(sprites[i]);
                Vector3 sum = Vector3.zero;
                float weight = 0f;
                foreach (var sp in picks)
                {
                    var c = SpriteColor(sp);
                    if (!c.HasValue) continue;
                    sum += new Vector3(c.Value.r, c.Value.g, c.Value.b);
                    weight += 1f;
                }
                if (weight > 0f) { sum /= weight; return new Color(sum.x, sum.y, sum.z, 1f); }
            }

            // 2) 파티클 이펙트 — 시작 색
            var ps = prefab.GetComponentInChildren<ParticleSystem>(true);
            if (ps != null)
            {
                Color start = ps.main.startColor.color;
                Color.RGBToHSV(start, out _, out float s, out _);
                if (s > 0.15f) return start;
            }
            return null;
        }

        /// <summary>스프라이트 한 장의 "눈에 띄는 색" — 불투명하고 채도가 높은 픽셀일수록 비중을 크게 둔 평균.</summary>
        private static Color? SpriteColor(Sprite sp)
        {
            if (SpriteCache.TryGetValue(sp, out var cached)) return cached;
            Color? result = null;
            try
            {
                var tex = sp.texture;
                if (!PixelCache.TryGetValue(tex, out var px))
                {
                    // 읽기 불가 텍스처라 GPU로 복사해서 읽는다
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    px = readable.GetPixels32();
                    Object.Destroy(readable);
                    PixelCache[tex] = px;
                }

                Rect r = sp.textureRect;
                int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y), w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
                double sr = 0, sg = 0, sb = 0, sw = 0;
                for (int y = 0; y < h; y += 2)
                    for (int x = 0; x < w; x += 2)
                    {
                        var p = px[(y0 + y) * tex.width + (x0 + x)];
                        if (p.a < 80) continue;
                        Color.RGBToHSV(p, out _, out float s, out float v);
                        if (v < 0.25f) continue;
                        double wgt = (p.a / 255.0) * (0.1 + s * s) * v; // 채도 높은 밝은 픽셀 위주
                        sr += p.r / 255.0 * wgt; sg += p.g / 255.0 * wgt; sb += p.b / 255.0 * wgt; sw += wgt;
                    }
                if (sw > 0.0001) result = new Color((float)(sr / sw), (float)(sg / sw), (float)(sb / sw), 1f);
            }
            catch { result = null; }
            SpriteCache[sp] = result;
            return result;
        }

        /// <summary>사거리 표시로 잘 보이도록 채도와 밝기를 끌어올린다(색조는 유지).</summary>
        private static Color Vivid(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s = Mathf.Clamp01(Mathf.Max(s * 1.4f, 0.92f)); // 채도를 크게 끌어올려 바닥 위에서 선명하게
            v = 1f;
            var o = Color.HSVToRGB(h, s, v);
            o.a = 1f;
            return o;
        }
    }
}
