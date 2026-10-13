using TMPro;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 동적 TMP 글꼴(빛의 계승자 등)은 처음 쓰는 글자를 그때그때 글꼴 그림판(atlas)에 그려 넣는다. 그 사이 글자가 단색 네모(파랗게 보이기도 한다)로 번쩍였다가 제대로 나왔다.
    /// 글자를 보이기 전에 필요한 글자를 미리 그려 두면 그 번쩍임이 없다.
    /// </summary>
    public static class FontWarmup
    {
        /// <summary>font 의 그림판에 chars 의 글자를 미리 넣는다. 글꼴이 없거나 동적이 아니면 아무것도 하지 않는다.</summary>
        public static void Warm(TMP_FontAsset font, string chars)
        {
            if (font == null || string.IsNullOrEmpty(chars)) return;
            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic) return;
            font.TryAddCharacters(chars, out _);
        }
    }
}
