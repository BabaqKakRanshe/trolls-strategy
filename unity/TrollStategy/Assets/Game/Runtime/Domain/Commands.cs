using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    public readonly struct CommandResult
    {
        public bool Ok { get; }
        public string Error { get; }

        private CommandResult(bool ok, string error)
        {
            Ok = ok;
            Error = error;
        }

        public static CommandResult Success() => new(true, null);
        public static CommandResult Fail(string error) => new(false, error);
    }

    public interface IGameCommand
    {
    }

    public sealed class StartBattleCommand : IGameCommand
    {
        public string MissionId { get; }
        public IReadOnlyList<BattlePlacement> Placements { get; }
        public IReadOnlyList<BattleEquipmentAssignment> Equipment { get; }

        public StartBattleCommand(string missionId, IReadOnlyList<BattlePlacement> placements,
            IReadOnlyList<BattleEquipmentAssignment> equipment = null)
        {
            MissionId = missionId;
            Placements = placements;
            Equipment = equipment;
        }
    }

    public sealed class AcknowledgeBattleCommand : IGameCommand
    {
    }

    /// <summary>Takes the gold a won battle rolled into the treasury.</summary>
    public sealed class ClaimBattleRewardCommand : IGameCommand
    {
    }

    public class BuildMineCommand : IGameCommand
    {
        public Cell Cell { get; }
        public BuildMineCommand(Cell cell) => Cell = cell;
    }

    public class BuildBuildingCommand : IGameCommand
    {
        public BuildingKind Kind { get; }
        public Cell Cell { get; }
        public BuildBuildingCommand(BuildingKind kind, Cell cell) { Kind = kind; Cell = cell; }
    }

    public class UpgradeBuildingCommand : IGameCommand
    {
        public string BuildingId { get; }
        public UpgradeBuildingCommand(string buildingId) => BuildingId = buildingId;
    }

    public class DemolishBuildingCommand : IGameCommand
    {
        public string BuildingId { get; }
        public DemolishBuildingCommand(string buildingId) => BuildingId = buildingId;
    }

    public class MoveBuildingCommand : IGameCommand
    {
        public string BuildingId { get; }
        public Cell Cell { get; }
        public MoveBuildingCommand(string buildingId, Cell cell) { BuildingId = buildingId; Cell = cell; }
    }

    public class BuyUnitsCommand : IGameCommand
    {
        public UnitKind UnitKind { get; }
        public int Amount { get; }
        public Cell Cell { get; }

        public BuyUnitsCommand(UnitKind unitKind, int amount, Cell cell)
        {
            UnitKind = unitKind;
            Amount = amount;
            Cell = cell;
        }
    }

    /// <summary>Hires one creature at the cell and sends it to work at the building: a workplace card's "hire here".</summary>
    public class HireWorkerCommand : IGameCommand
    {
        public UnitKind UnitKind { get; }
        public string BuildingId { get; }
        public Cell Cell { get; }

        public HireWorkerCommand(UnitKind unitKind, string buildingId, Cell cell)
        {
            UnitKind = unitKind;
            BuildingId = buildingId;
            Cell = cell;
        }
    }

    public class AssignWorkCommand : IGameCommand
    {
        public IReadOnlyList<string> UnitIds { get; }
        public string BuildingId { get; }

        public AssignWorkCommand(IReadOnlyList<string> unitIds, string buildingId)
        {
            UnitIds = unitIds;
            BuildingId = buildingId;
        }
    }

    public class AssignHaulCommand : IGameCommand
    {
        public IReadOnlyList<string> UnitIds { get; }
        public string SourceId { get; }
        public string DestinationId { get; }
        /// <summary>Goods the haulers may take; null or empty means whatever the route can carry.</summary>
        public IReadOnlyList<ResourceKind> Cargo { get; }

        public AssignHaulCommand(IReadOnlyList<string> unitIds, string sourceId, string destinationId,
            IReadOnlyList<ResourceKind> cargo = null)
        {
            UnitIds = unitIds;
            SourceId = sourceId;
            DestinationId = destinationId;
            Cargo = cargo ?? System.Array.Empty<ResourceKind>();
        }
    }

    public class ReleaseUnitsCommand : IGameCommand
    {
        public IReadOnlyList<string> UnitIds { get; }
        public ReleaseUnitsCommand(IReadOnlyList<string> unitIds) => UnitIds = unitIds;
    }

    public class SellUnitsCommand : IGameCommand
    {
        public IReadOnlyList<string> UnitIds { get; }
        public SellUnitsCommand(IReadOnlyList<string> unitIds) => UnitIds = unitIds;
    }

    /// <summary>Buys a block of land next to the colony's own; it comes wild (LandRules).</summary>
    public sealed class BuyLandCommand : IGameCommand
    {
        public int BlockX { get; }
        public int BlockY { get; }
        public BuyLandCommand(int blockX, int blockY) { BlockX = blockX; BlockY = blockY; }
    }

    /// <summary>Starts clearing a wild block of the colony's land (LandRules).</summary>
    public sealed class ClearLandCommand : IGameCommand
    {
        public int BlockX { get; }
        public int BlockY { get; }
        public ClearLandCommand(int blockX, int blockY) { BlockX = blockX; BlockY = blockY; }
    }

    /// <summary>Raises a colony improvement one level in its host building (UpgradeRules).</summary>
    public sealed class BuyUpgradeCommand : IGameCommand
    {
        public string UpgradeId { get; }
        public BuyUpgradeCommand(string upgradeId) => UpgradeId = upgradeId;
    }

    /// <summary>Takes the rewards of the finished current quest and begins the next one.</summary>
    public sealed class ClaimQuestRewardCommand : IGameCommand
    {
    }
}
