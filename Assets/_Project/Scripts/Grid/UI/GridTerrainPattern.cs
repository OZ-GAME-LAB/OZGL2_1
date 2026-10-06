using System;
using UnityEngine;

namespace OZGL2.Grid.UI
{
    /// <summary>원화의 N/E/S/W 연결 순서와 좌표별 고정 변형 선택을 공유한다.</summary>
    public static class GridTerrainPattern
    {
        public const int FULL_MASK = 15;

        public static int GetMask(Vector2Int cell, Func<Vector2Int, bool> contains)
            => (contains(cell + Vector2Int.up) ? 1 : 0) |
               (contains(cell + Vector2Int.right) ? 2 : 0) |
               (contains(cell + Vector2Int.down) ? 4 : 0) |
               (contains(cell + Vector2Int.left) ? 8 : 0);

        public static Sprite GetVariant(Sprite[] sprites, Vector2Int cell)
        {
            if (sprites == null || sprites.Length == 0) return null;
            unchecked
            {
                uint hash = (uint)(cell.x * 73856093) ^ (uint)(cell.y * 19349663);
                hash ^= hash >> 13; hash *= 1274126177u; hash ^= hash >> 16;
                return sprites[hash % sprites.Length];
            }
        }

        public static Sprite GetConnected(Sprite[] sprites, int mask)
            => sprites != null && mask >= 0 && mask < sprites.Length ? sprites[mask] : null;
    }
}
