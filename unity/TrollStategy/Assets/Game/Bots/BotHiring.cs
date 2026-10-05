using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Whom a bot hires for a job: the game's own rule (<see cref="HireAdvice"/>, which the building card's "hire
    /// here" follows too), asked with the profile's share.
    /// </summary>
    public static class BotHiring
    {
        /// <summary>Work the creature adds per second at the building, its favourite-building bonus included.</summary>
        public static float Work(UnitDefinition unit, BuildingKind building) => HireAdvice.Work(unit, building);

        /// <summary>Goods a hauler lifts per trip.</summary>
        public static float Carry(UnitDefinition unit) => HireAdvice.Carry(unit);

        /// <summary>
        /// Of the candidates whose value per gold is at least <paramref name="share"/> of the best, the one with
        /// the most value; ties go to the earlier kind. Null when there are no candidates.
        /// </summary>
        public static UnitDefinition Best(IEnumerable<UnitDefinition> candidates, Func<UnitDefinition, int> price,
            Func<UnitDefinition, float> value, float share) => HireAdvice.Best(candidates, price, value, share);
    }
}
