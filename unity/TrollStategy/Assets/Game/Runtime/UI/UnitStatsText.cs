using System;

namespace TrollStrategy.UI
{
    public static class UnitStatsText
    {
        /// <summary>
        /// Goods a creature takes per trip, as the player sees it: "1", or "1–2" when part of a unit builds up
        /// between trips and every so often it takes one more.
        /// </summary>
        public static string Load(float capacity)
        {
            int whole = (int)Math.Floor(capacity + .001f);
            return capacity - whole < .01f ? whole.ToString() : $"{whole}–{whole + 1}";
        }
    }
}
