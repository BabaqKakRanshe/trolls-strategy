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

    public class BuildMineCommand : IGameCommand
    {
        public Cell Cell { get; }
        public BuildMineCommand(Cell cell) => Cell = cell;
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
        public WorldPosition? AccessPoint { get; }

        public AssignWorkCommand(IReadOnlyList<string> unitIds, string buildingId)
            : this(unitIds, buildingId, null)
        {
        }

        public AssignWorkCommand(IReadOnlyList<string> unitIds, string buildingId, WorldPosition? accessPoint)
        {
            UnitIds = unitIds;
            BuildingId = buildingId;
            AccessPoint = accessPoint;
        }
    }

    public class AssignHaulCommand : IGameCommand
    {
        public IReadOnlyList<string> UnitIds { get; }
        public string SourceId { get; }
        public string DestinationId { get; }
        public WorldPosition? SourceAccessPoint { get; }
        public WorldPosition? DestinationAccessPoint { get; }

        public AssignHaulCommand(IReadOnlyList<string> unitIds, string sourceId, string destinationId)
            : this(unitIds, sourceId, destinationId, null, null)
        {
        }

        public AssignHaulCommand(
            IReadOnlyList<string> unitIds,
            string sourceId,
            string destinationId,
            WorldPosition? sourceAccessPoint,
            WorldPosition? destinationAccessPoint)
        {
            UnitIds = unitIds;
            SourceId = sourceId;
            DestinationId = destinationId;
            SourceAccessPoint = sourceAccessPoint;
            DestinationAccessPoint = destinationAccessPoint;
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
