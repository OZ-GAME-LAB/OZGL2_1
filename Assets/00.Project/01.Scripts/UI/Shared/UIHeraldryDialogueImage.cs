using UnityEngine;
using UnityEngine.UI;

namespace OZGL2.UIFlow
{
    /// <summary>일체형 말풍선의 모서리와 가운데 꼬리를 늘리지 않고 빈 면만 늘립니다.</summary>
    public sealed class UIHeraldryDialogueImage : Image
    {
        private bool _isTailRight;
        // 원본 그림의 가운데 꼬리 구역입니다. 위아래의 빈 직선 구간만 늘립니다.
        private static readonly Vector2 TAIL_BAND = new Vector2(0.22f, 0.74f);

        public void SetDirection(bool isTailRight)
        {
            if (_isTailRight == isTailRight) return;
            _isTailRight = isTailRight;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            Sprite art = overrideSprite;
            if (art == null)
            {
                base.OnPopulateMesh(vertexHelper);
                return;
            }

            vertexHelper.Clear();
            Rect target = GetPixelAdjustedRect();
            Rect source = art.rect;
            float pixelScale = Mathf.Max(0.01f, pixelsPerUnit * pixelsPerUnitMultiplier);
            Vector4 border = art.border;
            float left = (_isTailRight ? border.z : border.x) / pixelScale;
            float right = (_isTailRight ? border.x : border.z) / pixelScale;
            float horizontalFit = Mathf.Min(1f, target.width / Mathf.Max(1f, left + right));
            left *= horizontalFit;
            right *= horizontalFit;

            float bottom = border.y / pixelScale;
            float top = border.w / pixelScale;
            float middle = source.height * (TAIL_BAND.y - TAIL_BAND.x) / pixelScale;
            float verticalFit = Mathf.Min(1f, target.height / Mathf.Max(1f, bottom + top + middle));
            bottom *= verticalFit;
            top *= verticalFit;
            middle *= verticalFit;
            float middleBottom = Mathf.Clamp(target.height * 0.5f - middle * 0.5f, bottom, target.height - top - middle);

            float[] x = { target.xMin, target.xMin + left, target.xMax - right, target.xMax };
            float[] y = { target.yMin, target.yMin + bottom, target.yMin + middleBottom,
                target.yMin + middleBottom + middle, target.yMax - top, target.yMax };
            float[] u = _isTailRight
                ? new[] { 1f, 1f - border.z / source.width, border.x / source.width, 0f }
                : new[] { 0f, border.x / source.width, 1f - border.z / source.width, 1f };
            float[] v = { 0f, border.y / source.height, TAIL_BAND.x, TAIL_BAND.y,
                1f - border.w / source.height, 1f };
            Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(art);
            for (int row = 0; row < 6; row++)
                for (int column = 0; column < 4; column++)
                    vertexHelper.AddVert(new Vector3(x[column], y[row]), color,
                        new Vector2(Mathf.Lerp(uv.x, uv.z, u[column]), Mathf.Lerp(uv.y, uv.w, v[row])));
            for (int row = 0; row < 5; row++)
                for (int column = 0; column < 3; column++)
                {
                    int index = row * 4 + column;
                    vertexHelper.AddTriangle(index, index + 4, index + 5);
                    vertexHelper.AddTriangle(index, index + 5, index + 1);
                }
        }
    }
}
