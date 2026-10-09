using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Whether what the HUD sent is the move the bot meant. The HUD picks creatures by where they stand and fighters
    /// and gear by kind, so a creature of the same kind, or another item of the same kind, counts as the same move;
    /// buildings, cells, land, goods and levels must be the same.
    /// </summary>
    public static class BotShowMatch
    {
        /// <param name="kindOf">The kind of a creature by id, read before the move.</param>
        public static bool Same(IGameCommand meant, IGameCommand sent, System.Func<string, UnitKind?> kindOf)
        {
            return meant switch
            {
                ClaimQuestRewardCommand => sent is ClaimQuestRewardCommand,
                AcknowledgeBattleCommand => sent is AcknowledgeBattleCommand,
                ClaimBattleRewardCommand => sent is ClaimBattleRewardCommand,
                BuildBuildingCommand a => sent is BuildBuildingCommand b && a.Kind == b.Kind && a.Cell.Equals(b.Cell),
                BuyUnitsCommand a => sent is BuyUnitsCommand b && a.UnitKind == b.UnitKind && a.Amount == b.Amount,
                AssignWorkCommand a => sent is AssignWorkCommand b && a.BuildingId == b.BuildingId &&
                                       Kinds(a.UnitIds, kindOf).SequenceEqual(Kinds(b.UnitIds, kindOf)),
                AssignHaulCommand a => sent is AssignHaulCommand b && a.SourceId == b.SourceId &&
                                       a.DestinationId == b.DestinationId && Cargo(a.Cargo).SequenceEqual(Cargo(b.Cargo)) &&
                                       Kinds(a.UnitIds, kindOf).SequenceEqual(Kinds(b.UnitIds, kindOf)),
                UpgradeBuildingCommand a => sent is UpgradeBuildingCommand b && a.BuildingId == b.BuildingId,
                BuyUpgradeCommand a => sent is BuyUpgradeCommand b && a.UpgradeId == b.UpgradeId,
                BuyLandCommand a => sent is BuyLandCommand b && a.BlockX == b.BlockX && a.BlockY == b.BlockY,
                ClearLandCommand a => sent is ClearLandCommand b && a.BlockX == b.BlockX && a.BlockY == b.BlockY,
                ReleaseUnitsCommand a => sent is ReleaseUnitsCommand b && Kinds(a.UnitIds, kindOf).SequenceEqual(Kinds(b.UnitIds, kindOf)),
                SellUnitsCommand a => sent is SellUnitsCommand b && Kinds(a.UnitIds, kindOf).SequenceEqual(Kinds(b.UnitIds, kindOf)),
                MoveBuildingCommand a => sent is MoveBuildingCommand b && a.BuildingId == b.BuildingId && a.Cell.Equals(b.Cell),
                StartBattleCommand a => sent is StartBattleCommand b && a.MissionId == b.MissionId &&
                                        Squad(a.Placements, kindOf).SequenceEqual(Squad(b.Placements, kindOf)),
                // the card's "hire here" is gone: the HUD hires in the catalog and then sends the creature to work
                HireWorkerCommand a => sent is AssignWorkCommand b && a.BuildingId == b.BuildingId && b.UnitIds.Count == 1 &&
                                       kindOf(b.UnitIds[0]) == a.UnitKind,
                _ => false
            };
        }

        /// <summary>The move is the same, creature for creature and item for item.</summary>
        public static bool Exact(IGameCommand meant, IGameCommand sent) => meant switch
        {
            AssignWorkCommand a => sent is AssignWorkCommand b && a.UnitIds.OrderBy(i => i).SequenceEqual(b.UnitIds.OrderBy(i => i)),
            AssignHaulCommand a => sent is AssignHaulCommand b && a.UnitIds.OrderBy(i => i).SequenceEqual(b.UnitIds.OrderBy(i => i)),
            StartBattleCommand a => sent is StartBattleCommand b &&
                                    a.Placements.Select(p => p.UnitId + "@" + p.Cell.X + "," + p.Cell.Y).OrderBy(s => s)
                                        .SequenceEqual(b.Placements.Select(p => p.UnitId + "@" + p.Cell.X + "," + p.Cell.Y).OrderBy(s => s)),
            BuyUnitsCommand a => sent is BuyUnitsCommand b && a.Cell.Equals(b.Cell),
            HireWorkerCommand => false,
            _ => true
        };

        private static IEnumerable<string> Kinds(IReadOnlyList<string> ids, System.Func<string, UnitKind?> kindOf) =>
            ids.Select(id => kindOf(id)?.ToString() ?? "?").OrderBy(k => k);

        private static IEnumerable<ResourceKind> Cargo(IReadOnlyList<ResourceKind> cargo) =>
            (cargo ?? new List<ResourceKind>()).Distinct().OrderBy(r => r);

        private static IEnumerable<string> Squad(IReadOnlyList<BattlePlacement> placements, System.Func<string, UnitKind?> kindOf) =>
            placements.Select(p => $"{kindOf(p.UnitId)}@{p.Cell.X},{p.Cell.Y}").OrderBy(s => s);
    }
}
