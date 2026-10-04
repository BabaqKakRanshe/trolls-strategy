using System;

namespace TrollStrategy.Domain
{
    /// <summary>
    /// The arena's one prize fund for repeat wins. It gains a payout every period of active time and holds no more
    /// than its cap; a paid repeat win on any level takes one. The state keeps the payouts held at a moment and
    /// that moment; what gathered since is counted from the active time, so no step has to tick it.
    /// </summary>
    public static class ArenaFund
    {
        /// <summary>Payouts the fund holds now.</summary>
        public static int Available(GameState state, int cap, int periodMs)
        {
            cap = Math.Max(1, cap);
            int held = Math.Max(0, Math.Min(cap, state.ArenaFundPayouts));
            if (held >= cap) return cap;
            long gathered = Math.Max(0L, (long)state.ActiveTimeMs - state.ArenaFundSinceMs) / Math.Max(1, periodMs);
            return (int)Math.Min(cap, held + gathered);
        }

        /// <summary>Active time until the next payout comes; −1 while the fund is full.</summary>
        public static int NextInMs(GameState state, int cap, int periodMs)
        {
            periodMs = Math.Max(1, periodMs);
            int available = Available(state, cap, periodMs);
            if (available >= Math.Max(1, cap)) return -1;
            int held = Math.Max(0, state.ArenaFundPayouts);
            long next = state.ArenaFundSinceMs + (long)(available - held + 1) * periodMs;
            return (int)Math.Max(0L, next - state.ActiveTimeMs);
        }

        /// <summary>
        /// Takes one payout; false when the fund is empty. A full fund starts gathering the next payout now; a fund
        /// that was filling keeps the time it has gathered towards it.
        /// </summary>
        public static bool Take(GameState state, int cap, int periodMs)
        {
            cap = Math.Max(1, cap);
            periodMs = Math.Max(1, periodMs);
            int available = Available(state, cap, periodMs);
            if (available <= 0) return false;
            int held = Math.Max(0, Math.Min(cap, state.ArenaFundPayouts));
            state.ArenaFundSinceMs = available >= cap
                ? state.ActiveTimeMs
                : state.ArenaFundSinceMs + (available - held) * periodMs;
            state.ArenaFundPayouts = available - 1;
            return true;
        }
    }
}
