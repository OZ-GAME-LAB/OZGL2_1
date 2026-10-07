using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 상단 경험치바는 희수가 만든 그림(붉은 화살표 칸이 오른쪽으로 갈수록 어두워지는 채움) 그대로 쓰고, 색만 더 진하게 만든다.
    /// 원본 채움 그림은 어두운 붉은색이라 어두운 바 위에서 잘 안 보였다. 실행할 때 한 번 그 그림을 읽어
    /// 밝기를 올리고 채도를 높인 복사본을 만들어 채움에 쓴다(원본 파일은 그대로, 모양·칸 구분·그라데이션 방향도 그대로).
    /// 영혼이 날아와 채우는 연출(InGameSoulAbsorb)은 같은 채움 그림을 쓰므로 그대로 동작한다.
    /// </summary>
    [DefaultExecutionOrder(1001)]
    public sealed class InGameXpBarStyle : MonoBehaviour
    {
        [SerializeField, Range(1f, 2.5f), Tooltip("밝기 배율(1 = 원본)")] private float _brightness = 1.6f;
        [SerializeField, Range(1f, 2.5f), Tooltip("채도 배율(1 = 원본)")] private float _saturation = 1.45f;

        private Sprite _source, _boosted;

        private void OnDestroy()
        {
            InGameSoulAbsorb.OverrideFillSprite = null;
            if (_boosted != null) { Destroy(_boosted.texture); Destroy(_boosted); }
        }

        private void LateUpdate()
        {
            var fill = InGameSoulAbsorb.FillImage;
            if (fill == null || fill.sprite == null) return;
            if (fill.sprite == _boosted) return;           // 이미 진한 그림을 쓰는 중
            if (_source != fill.sprite) { _source = fill.sprite; Rebuild(); }
            if (_boosted != null) InGameSoulAbsorb.OverrideFillSprite = _boosted;
        }

        private void Rebuild()
        {
            if (_boosted != null) { Destroy(_boosted.texture); Destroy(_boosted); _boosted = null; }
            var texture = ReadTexture(_source);
            if (texture == null) return;
            var pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a == 0) continue;
                float r = p.r / 255f, g = p.g / 255f, b = p.b / 255f;
                float gray = r * 0.299f + g * 0.587f + b * 0.114f;
                r = Mathf.Lerp(gray, r, _saturation) * _brightness;
                g = Mathf.Lerp(gray, g, _saturation) * _brightness;
                b = Mathf.Lerp(gray, b, _saturation) * _brightness;
                pixels[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), p.a);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.filterMode = _source.texture.filterMode;
            // 원본 스프라이트와 같은 영역·기준점·단위·테두리로 만든다
            var rect = new Rect(0, 0, texture.width, texture.height);
            _boosted = Sprite.Create(texture, rect, _source.pivot / _source.rect.size, _source.pixelsPerUnit * (_source.rect.width / texture.width), 0, SpriteMeshType.FullRect, _source.border);
        }

        /// <summary>읽기 권한이 없는 그림도 한 번 그려서 읽는다(원본 스프라이트가 차지하는 영역만).</summary>
        private static Texture2D ReadTexture(Sprite sprite)
        {
            var source = sprite.texture;
            var rect = sprite.textureRect;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var result = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(rect.x, rect.y, rect.width, rect.height), 0, 0);
                result.Apply();
                return result;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("경험치바 색 보정 실패: " + e.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
