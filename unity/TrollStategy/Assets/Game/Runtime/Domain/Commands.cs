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

        public AssignHaulCommand(IReadOnlyList<string> unitIds, string sourceId, string destinationId)
        {
            UnitIds = unitIds;
            SourceId = sourceId;
            DestinationId = destinationId;
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

    public class SendToBarracksCommand : IGameCommand
    {
        public IReadOnlyList<string> UnitIds { get; }
        public SendToBarracksCommand(IReadOnlyList<string> unitIds) => UnitIds = unitIds;
    }
}
