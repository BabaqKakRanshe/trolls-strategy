using System;
using System.Collections.Generic;

namespace TrollStrategy.Domain
{
    /// <summary>How the colony's squad compares with a level's enemies, in the arena window's three words.</summary>
    public enum OddsGrade { Weaker, Even, Stronger }

    /// <summary>
    /// A quick estimate of a battle from the fighters it would start with, by Lanchester's linear law: each side's
    /// health times the damage per second it deals through the other side's armour. The ratio of the two products
    /// is how much longer the enemies would need to beat the squad than the squad needs to beat them. It ignores
    /// movement and range, so it is a hint, never the outcome: the battle decides.
    /// </summary>
    public static class BattleOdds
    {
        /// <summary>
        /// The ratio of the squad's strength to the enemies' and its grade: at least <paramref name="margin"/> is
        /// stronger, at most its inverse weaker. No squad is weaker; no enemies, stronger.
        /// </summary>
        public static (double Ratio, OddsGrade Grade) Compare(IReadOnlyList<BattleFighterInput> players,
            IReadOnlyList<BattleFighterInput> enemies, double margin)
        {
            margin = Math.Max(1.0001, margin);
            if (players == null || players.Count == 0) return (0, OddsGrade.Weaker);
            if (enemies == null || enemies.Count == 0) return (double.PositiveInfinity, OddsGrade.Stronger);
            double squad = Health(players) * DamagePerSecond(players, AverageArmor(enemies));
            double foes = Health(enemies) * DamagePerSecond(enemies, AverageArmor(players));
            double ratio = foes > 0 ? squad / foes : double.PositiveInfinity;
            var grade = ratio >= margin ? OddsGrade.Stronger : ratio <= 1 / margin ? OddsGrade.Weaker : OddsGrade.Even;
            return (ratio, grade);
        }

        private static double Health(IReadOnlyList<BattleFighterInput> side)
        {
            double sum = 0;
            foreach (var fighter in side) sum += Math.Max(0, fighter.Health);
            return sum;
        }

        private static double AverageArmor(IReadOnlyList<BattleFighterInput> side)
        {
            double sum = 0;
            foreach (var fighter in side) sum += fighter.Armor;
            return sum / side.Count;
        }

        // the battle's own damage rule (at least 1 a blow) against the other side's average armour
        private static double DamagePerSecond(IReadOnlyList<BattleFighterInput> side, double armor)
        {
            double sum = 0;
            foreach (var fighter in side)
                sum += Math.Max(1, fighter.Damage - armor) * 1000.0 / Math.Max(1, fighter.AttackIntervalMs);
            return sum;
        }
    }
}
