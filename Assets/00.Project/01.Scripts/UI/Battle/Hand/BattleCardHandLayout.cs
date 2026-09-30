using System;
using UnityEngine;

namespace OZGL2.UIFlow
{
    /// <summary>Unity 오브젝트 상태와 무관하게 수평 손패 배치를 계산한다.</summary>
    public static class BattleCardHandLayout
    {
        public readonly struct Result
        {
            public Result(float[] positions, float startX, float step, float contentWidth, bool requiresScroll)
            {
                Positions = positions ?? Array.Empty<float>();
                StartX = startX;
                Step = step;
                ContentWidth = contentWidth;
                RequiresScroll = requiresScroll;
            }

            public float[] Positions { get; }
            public float StartX { get; }
            public float Step { get; }
            public float ContentWidth { get; }
            public bool RequiresScroll { get; }
        }

        public static Result Calculate(
            float viewportWidth,
            float cardWidth,
            int count,
            float comfortableSpacing,
            float minimumRevealWidth)
        {
            viewportWidth = Mathf.Max(0f, viewportWidth);
            cardWidth = Mathf.Max(0f, cardWidth);
            count = Mathf.Max(0, count);

            if (count == 0)
                return new Result(Array.Empty<float>(), 0f, 0f, viewportWidth, false);

            float[] positions = new float[count];
            if (count == 1)
            {
                float contentWidth = Mathf.Max(viewportWidth, cardWidth);
                float startX = contentWidth * 0.5f;
                positions[0] = startX;
                return new Result(positions, startX, 0f, contentWidth, cardWidth > viewportWidth);
            }

            float revealWidth = Mathf.Clamp(minimumRevealWidth, 0f, cardWidth);
            float preferredStep = Mathf.Max(revealWidth, cardWidth + comfortableSpacing);
            float preferredCardsWidth = cardWidth + preferredStep * (count - 1);

            float step;
            float start;
            float resultWidth;
            bool requiresScroll;

            if (preferredCardsWidth <= viewportWidth)
            {
                step = preferredStep;
                resultWidth = viewportWidth;
                start = (viewportWidth - preferredCardsWidth) * 0.5f + cardWidth * 0.5f;
                requiresScroll = false;
            }
            else
            {
                float fittingStep = Mathf.Max(0f, viewportWidth - cardWidth) / (count - 1);
                if (fittingStep >= revealWidth && cardWidth <= viewportWidth)
                {
                    step = fittingStep;
                    resultWidth = viewportWidth;
                    start = cardWidth * 0.5f;
                    requiresScroll = false;
                }
                else
                {
                    step = revealWidth;
                    resultWidth = Mathf.Max(viewportWidth, cardWidth + step * (count - 1));
                    start = cardWidth * 0.5f;
                    requiresScroll = resultWidth > viewportWidth;
                }
            }

            for (int i = 0; i < positions.Length; i++)
                positions[i] = start + step * i;

            return new Result(positions, start, step, resultWidth, requiresScroll);
        }
    }
}
