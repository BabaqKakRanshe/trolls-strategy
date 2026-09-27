using UnityEngine;

namespace TrollStrategy.Presentation.Feel
{
    /// <summary>Easing curves for presentation tweens; t runs 0..1.</summary>
    public static class Ease
    {
        public static float OutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        public static float InCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        /// <summary>Decaying oscillation: 0 at the ends, used for punches and wobbles.</summary>
        public static float Spring(float t, float oscillations = 2f)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin(t * Mathf.PI * 2f * oscillations) * (1f - t) * (1f - t);
        }

        /// <summary>0 → 1 → 0 hump for arcs and flashes.</summary>
        public static float Hump(float t)
        {
            t = Mathf.Clamp01(t);
            return 4f * t * (1f - t);
        }
    }
}
