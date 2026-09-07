using System;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    public enum HaulPhase
    {
        ToSource,
        Loading,
        ToDestination,
        Unloading
    }

    public enum AssignmentKind
    {
        Idle,
        ToWork,
        Work,
        Haul
    }

    [Serializable]
    public class Assignment
    {
        public AssignmentKind Kind { get; set; }
        public string BuildingId { get; set; }
        public string SourceId { get; set; }
        public string DestinationId { get; set; }
        public HaulPhase Phase { get; set; }
        public int Carried { get; set; }
        public float PhaseElapsedSeconds { get; set; }

        public static Assignment Idle() => new() { Kind = AssignmentKind.Idle };

        public static Assignment ToWork(string buildingId) => new()
        {
            Kind = AssignmentKind.ToWork,
            BuildingId = buildingId
        };

        public static Assignment Work(string buildingId) => new()
        {
            Kind = AssignmentKind.Work,
            BuildingId = buildingId
        };

        public static Assignment Haul(string sourceId, string destinationId) => new()
        {
            Kind = AssignmentKind.Haul,
            SourceId = sourceId,
            DestinationId = destinationId,
            Phase = HaulPhase.ToSource,
            Carried = 0,
            PhaseElapsedSeconds = 0f
        };

        public Assignment Clone() => new()
        {
            Kind = Kind,
            BuildingId = BuildingId,
            SourceId = SourceId,
            DestinationId = DestinationId,
            Phase = Phase,
            Carried = Carried,
            PhaseElapsedSeconds = PhaseElapsedSeconds
        };
    }

    [Serializable]
    public class BuildingState
    {
        public string Id { get; set; }
        public BuildingKind Kind { get; set; }
        public Cell Cell { get; set; }
        public int Ore { get; set; }
        public float ProductionProgress { get; set; }

        public BuildingState Clone() => new()
        {
            Id = Id,
            Kind = Kind,
            Cell = Cell,
            Ore = Ore,
            ProductionProgress = ProductionProgress
        };
    }

    [Serializable]
    public class UnitState
    {
        public string Id { get; set; }
        public UnitKind Kind { get; set; }
        public WorldPosition Position { get; set; }
        public Assignment Assignment { get; set; }

        public UnitState Clone() => new()
        {
            Id = Id,
            Kind = Kind,
            Position = Position,
            Assignment = Assignment?.Clone() ?? Assignment.Idle()
        };
    }
}
