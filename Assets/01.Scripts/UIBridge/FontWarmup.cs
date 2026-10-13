using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 동적 TMP 글꼴(빛의 계승자 등)은 처음 쓰는 글자를 그때그때 글꼴 그림판(atlas)에 그려 넣는다. 그 사이 글자가 단색 네모(파랗게 보이기도 한다)로 번쩍였다가 제대로 나왔다.
    /// 글자를 보이기 전에 필요한 글자를 미리 그려 두면 그 번쩍임이 없다.
    /// </summary>
    public static class FontWarmup
    {
        /// <summary>
        /// 글자를 넣기 직전에 부른다: 이 글자들을 target 의 글꼴(없는 글자는 대체 글꼴)에 미리 그려 두어서 처음 보이는 순간에 깨져 보이지 않게 한다.
        /// 이미 있는 글자는 건너뛰므로 자주 불러도 가볍다.
        /// </summary>
        public static void Ensure(TMP_Text target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text) || target.font == null) return;
            var font = target.font;
            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            {
                font.TryAddCharacters(text, out string missing);
                if (!string.IsNullOrEmpty(missing) && font.fallbackFontAssetTable != null)
                    foreach (var fallback in font.fallbackFontAssetTable)
                    {
                        if (fallback == null || fallback.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;
                        fallback.TryAddCharacters(missing, out missing);
                        if (string.IsNullOrEmpty(missing)) break;
                    }
            }
        }

        private static readonly Dictionary<TMP_FontAsset, TMP_FontAsset> _isolated = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        private static readonly HashSet<TMP_FontAsset> _isolatedSet = new HashSet<TMP_FontAsset>();

        /// <summary>이 연출 전용으로 만든 글꼴인가. 글꼴 통일(UiFontUnify)이 이 글꼴을 다른 글꼴로 바꾸지 않게 할 때 쓴다.</summary>
        public static bool IsIsolated(TMP_FontAsset font) => font != null && _isolatedSet.Contains(font);

        /// <summary>
        /// 같은 글꼴 파일로 이 연출 전용 글꼴 그림판을 따로 만들어 돌려준다(chars 는 미리 그려 둔다).
        /// 게임 전체가 함께 쓰는 동적 글꼴은 여기저기서 새 글자가 들어오며 그림판이 여러 장으로 늘어나는데,
        /// 외곽선·그림자처럼 재질을 따로 쓰는 큰 글자가 그 변화에 휘말려 깨져 보이는 일이 있었다. 전용 그림판은 그 영향을 받지 않는다.
        /// 원본 글꼴 파일을 못 찾으면 원래 글꼴을 그대로 쓴다.
        /// </summary>
        public static TMP_FontAsset Isolated(TMP_FontAsset font, string chars)
        {
            if (font == null) return null;
            if (_isolated.TryGetValue(font, out var cached) && cached != null) { Warm(cached, chars); return cached; }
            if (font.sourceFontFile == null) { Warm(font, chars); return font; }
            // 그림판 여백(18)을 글자 크기(72)에 비해 넉넉히 잡는다 — 두꺼운 외곽선·그림자가 옆 글자 그림까지 끌어와 조각처럼 깨져 보이지 않게
            var copy = TMP_FontAsset.CreateFontAsset(font.sourceFontFile, 72, 18, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (copy == null) { Warm(font, chars); return font; }
            copy.name = font.name + " (전용)";
            if (font.fallbackFontAssetTable != null) copy.fallbackFontAssetTable = new List<TMP_FontAsset>(font.fallbackFontAssetTable);
            Warm(copy, chars);
            _isolated[font] = copy;
            _isolatedSet.Add(copy);
            return copy;
        }

        /// <summary>font 의 그림판에 chars 의 글자를 미리 넣는다. 글꼴이 없거나 동적이 아니면 아무것도 하지 않는다.</summary>
        public static void Warm(TMP_FontAsset font, string chars)
        {
            if (font == null || string.IsNullOrEmpty(chars)) return;
            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic) return;
            font.TryAddCharacters(chars, out _);
        }
    }
}
