using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>
    /// Whom to hire for a job. Places at a workshop and haulers on a route are few, gold is not: of the creatures
    /// that give at least a share of the best value per gold, the one worth most per head. The building card's
    /// "hire here" asks it with <see cref="CardShare"/>; the campaign bots ask it with their profile's share.
    /// </summary>
    public static class HireAdvice
    {
        /// <summary>The share of the best work per gold the building card's hire accepts.</summary>
        public const float CardShare = 0.9f;

        /// <summary>Work the creature adds per second at the building, its favourite-building bonus included.</summary>
        public static float Work(UnitDefinition unit, BuildingKind building) =>
            unit.Favors(building) ? UpgradeRules.Raise(unit.Strength, unit.FavoredWorkPercent) : unit.Strength;

        /// <summary>
        /// Goods a hauler lifts per trip. Its pace counts for little: haulers spend much of a trip queuing and
        /// loading at doors.
        /// </summary>
        public static float Carry(UnitDefinition unit) => ColonySimulation.CarryCapacity(unit.Stamina);

        /// <summary>
        /// Of the candidates whose value per gold is at least <paramref name="share"/> of the best, the one with
        /// the most value; ties go to the earlier kind. Null when there are no candidates.
        /// </summary>
        public static UnitDefinition Best(IEnumerable<UnitDefinition> candidates, Func<UnitDefinition, int> price,
            Func<UnitDefinition, float> value, float share)
        {
            var list = candidates.Where(u => u != null).ToList();
            if (list.Count == 0) return null;
            float PerGold(UnitDefinition unit) => value(unit) / Math.Max(1, price(unit));
            float best = list.Max(PerGold);
            return list.Where(u => PerGold(u) >= best * share)
                .OrderByDescending(value).ThenBy(u => u.Kind).First();
        }
    }
}
