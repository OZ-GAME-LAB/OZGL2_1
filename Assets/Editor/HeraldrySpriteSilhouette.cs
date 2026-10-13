using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

/// <summary>
/// PNG는 유지하고, 불투명 본체와 떨어진 픽셀 조각이 Sprite에 그려지지 않도록 메시만 만듭니다.
/// 내부가 채워진 UI 패널 전용입니다. 분리된 장식이나 여러 본체를 가진 그림에는 적용하지 않습니다.
/// </summary>
public static class HeraldrySpriteSilhouette
{
    private const float OUTLINE_DETAIL = 0.65f;
    private const float POINT_EPSILON = 0.0001f;
    private const int MAX_VERTICES = 4096;

    private static readonly MethodInfo GENERATE_OUTLINE = typeof(UnityEditor.Sprites.SpriteUtility).GetMethod(
        "GenerateOutline", BindingFlags.Static | BindingFlags.NonPublic, null,
        new[]
        {
            typeof(Texture2D), typeof(Rect), typeof(float), typeof(byte), typeof(bool),
            typeof(Vector2[][]).MakeByRefType(), typeof(bool)
        }, null);

    /// <summary>
    /// alphaTolerance는 0~1 값입니다. 가장 큰 외곽선만 남기고 삼각분할합니다.
    /// 읽기용 PNG 사본과 Sprite가 같은 크기여야 하며, 저장은 호출한 빌더가 담당합니다.
    /// 기존 Sprite 에셋에 적용하면 Undo를 기록합니다. Image는 Simple/useSpriteMesh=true로 표시해야 합니다.
    /// </summary>
    public static void Apply(Sprite sprite, Texture2D readablePixels, float alphaTolerance)
    {
        if (sprite == null) throw new ArgumentNullException(nameof(sprite));
        if (readablePixels == null) throw new ArgumentNullException(nameof(readablePixels));
        if (!readablePixels.isReadable)
            throw new ArgumentException("원본 PNG를 읽기 가능한 Texture2D 사본으로 전달해 주세요.", nameof(readablePixels));
        if (float.IsNaN(alphaTolerance) || alphaTolerance < 0f || alphaTolerance > 1f)
            throw new ArgumentOutOfRangeException(nameof(alphaTolerance), "알파 기준은 0~1이어야 합니다.");
        if (sprite.texture == null || sprite.texture.width != readablePixels.width || sprite.texture.height != readablePixels.height)
            throw new ArgumentException("Sprite와 읽기용 PNG의 크기가 다릅니다.", nameof(readablePixels));
        if (sprite.packed)
            throw new ArgumentException("Atlas에 패킹되지 않은 개별 Sprite에 적용해 주세요.", nameof(sprite));
        if (GENERATE_OUTLINE == null)
            throw new NotSupportedException("현재 Unity에서 Sprite 외곽선 생성 API를 찾을 수 없습니다.");

        // holeDetection=false: 채워진 패널 안쪽의 작은 반투명 점은 구멍으로 만들지 않습니다.
        // zeroExtrusion=true: 외곽선을 바깥으로 늘려 떨어진 잔여 픽셀을 다시 포함하지 않습니다.
        object[] args =
        {
            readablePixels, sprite.rect, OUTLINE_DETAIL, (byte)Mathf.RoundToInt(alphaTolerance * 255f),
            false, null, true
        };
        GENERATE_OUTLINE.Invoke(null, args);
        var paths = args[5] as Vector2[][];
        List<Vector2> outline = SelectLargestOutline(paths);
        if (outline.Count < 3 || outline.Count > MAX_VERTICES)
            throw new InvalidOperationException("패널 실루엣의 꼭짓점 수가 유효하지 않습니다: " + outline.Count);

        ushort[] triangles = Triangulate(outline);
        var vertices = new Vector3[outline.Count];
        var uvs = new Vector2[outline.Count];
        Vector2 halfSize = sprite.rect.size * 0.5f;
        // 외곽선은 rect 중심 기준 픽셀입니다. 위치는 pivot 기준 Unity 단위, UV는 원본 Texture 좌표로 바꿉니다.
        for (int i = 0; i < outline.Count; i++)
        {
            Vector2 pixel = outline[i] + halfSize;
            pixel.x = Mathf.Clamp(pixel.x, 0f, sprite.rect.width);
            pixel.y = Mathf.Clamp(pixel.y, 0f, sprite.rect.height);
            vertices[i] = (pixel - sprite.pivot) / sprite.pixelsPerUnit;
            uvs[i] = new Vector2((sprite.rect.x + pixel.x) / sprite.texture.width,
                (sprite.rect.y + pixel.y) / sprite.texture.height);
        }

        // 생성·분할이 모두 성공한 뒤에만 대상 Sprite를 갱신합니다. PNG와 Texture Importer는 수정하지 않습니다.
        if (EditorUtility.IsPersistent(sprite)) Undo.RecordObject(sprite, "UI 패널 실루엣 갱신");
        // Unity 6의 Edit Mode에서는 OverrideGeometry가 경고만 남기고 무시되므로 Editor에서도 저장되는 버퍼 API를 사용합니다.
        using (var positions = new NativeArray<Vector3>(vertices, Allocator.Temp))
        using (var textureCoordinates = new NativeArray<Vector2>(uvs, Allocator.Temp))
        using (var indices = new NativeArray<ushort>(triangles, Allocator.Temp))
        {
            sprite.SetVertexCount(vertices.Length);
            sprite.SetVertexAttribute(VertexAttribute.Position, positions);
            sprite.SetVertexAttribute(VertexAttribute.TexCoord0, textureCoordinates);
            sprite.SetIndices(indices);
        }
        Vector2[] appliedVertices = sprite.vertices;
        ushort[] appliedTriangles = sprite.triangles;
        if (appliedVertices.Length != vertices.Length || appliedTriangles.Length != triangles.Length)
            throw new InvalidOperationException("패널 메시가 적용되지 않았습니다. 에셋 저장을 중단합니다.");
        Vector2 minimum = -sprite.pivot / sprite.pixelsPerUnit;
        Vector2 maximum = (sprite.rect.size - sprite.pivot) / sprite.pixelsPerUnit;
        for (int i = 0; i < appliedVertices.Length; i++)
        {
            Vector2 point = appliedVertices[i];
            if (point.x < minimum.x - POINT_EPSILON || point.y < minimum.y - POINT_EPSILON ||
                point.x > maximum.x + POINT_EPSILON || point.y > maximum.y + POINT_EPSILON ||
                (point - (Vector2)vertices[i]).sqrMagnitude > POINT_EPSILON)
                throw new InvalidOperationException("패널 메시 좌표가 원본 영역 또는 생성한 실루엣과 일치하지 않습니다.");
        }
        for (int i = 0; i < appliedTriangles.Length; i++)
            if (appliedTriangles[i] != triangles[i])
                throw new InvalidOperationException("패널 메시의 삼각형 연결이 적용되지 않았습니다.");
        EditorUtility.SetDirty(sprite);
    }

    private static List<Vector2> SelectLargestOutline(Vector2[][] paths)
    {
        Vector2[] largest = null;
        double largestArea = 0d;
        if (paths != null)
        {
            foreach (Vector2[] path in paths)
            {
                if (path == null || path.Length < 3) continue;
                double area = Math.Abs(SignedArea(path));
                if (area <= largestArea) continue;
                largest = path;
                largestArea = area;
            }
        }
        if (largest == null || largestArea <= POINT_EPSILON)
            throw new InvalidOperationException("알파 기준을 만족하는 패널 본체 외곽선을 찾지 못했습니다.");

        var points = new List<Vector2>(largest.Length);
        foreach (Vector2 point in largest)
        {
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))
                throw new InvalidOperationException("패널 외곽선에 잘못된 좌표가 있습니다.");
            if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > POINT_EPSILON)
                points.Add(point);
        }
        if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude <= POINT_EPSILON)
            points.RemoveAt(points.Count - 1);

        // 같은 직선 위의 점을 제거해 가늘거나 면적이 0인 삼각형을 피합니다.
        bool removed = true;
        while (removed && points.Count > 3)
        {
            removed = false;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector2 previous = points[(i + points.Count - 1) % points.Count];
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                if (Mathf.Abs(Cross(previous, current, next)) > POINT_EPSILON ||
                    Vector2.Dot(current - previous, next - current) < 0f) continue;
                points.RemoveAt(i);
                removed = true;
                if (points.Count <= 3) break;
            }
        }
        if (SignedArea(points) < 0d) points.Reverse();
        return points;
    }

    private static ushort[] Triangulate(List<Vector2> polygon)
    {
        var remaining = new List<int>(polygon.Count);
        for (int i = 0; i < polygon.Count; i++) remaining.Add(i);
        var triangles = new List<ushort>((polygon.Count - 2) * 3);

        // 오목한 문양도 바깥 영역을 채우지 않도록 볼록한 귀를 하나씩 분리합니다.
        while (remaining.Count > 3)
        {
            bool clipped = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                int b = remaining[i];
                int c = remaining[(i + 1) % remaining.Count];
                if (Cross(polygon[a], polygon[b], polygon[c]) <= POINT_EPSILON) continue;
                bool containsPoint = false;
                foreach (int point in remaining)
                {
                    if (point == a || point == b || point == c) continue;
                    if (!IsInsideTriangle(polygon[point], polygon[a], polygon[b], polygon[c])) continue;
                    containsPoint = true;
                    break;
                }
                if (containsPoint) continue;
                triangles.Add((ushort)a);
                triangles.Add((ushort)b);
                triangles.Add((ushort)c);
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped)
                throw new InvalidOperationException("패널 외곽선을 안전하게 삼각분할하지 못했습니다. Sprite는 변경하지 않았습니다.");
        }
        if (remaining.Count == 3)
        {
            triangles.Add((ushort)remaining[0]);
            triangles.Add((ushort)remaining[1]);
            triangles.Add((ushort)remaining[2]);
        }
        return triangles.ToArray();
    }

    private static bool IsInsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        return Cross(a, b, point) >= -POINT_EPSILON &&
               Cross(b, c, point) >= -POINT_EPSILON &&
               Cross(c, a, point) >= -POINT_EPSILON;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static double SignedArea(IList<Vector2> points)
    {
        double area = 0d;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];
            area += (double)a.x * b.y - (double)b.x * a.y;
        }
        return area * 0.5d;
    }
}
