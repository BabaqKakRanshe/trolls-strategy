using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    public enum HaulPhase
    {
        ToSource,
        QueuedAtSource,
        ToDock,
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
        // The workplace of a worker; for a free creature the building it gathers at (the barracks), or null.
        public string BuildingId { get; set; }
        public string SourceId { get; set; }
        public string DestinationId { get; set; }
        public HaulPhase Phase { get; set; }
        public int Carried { get; set; }
        public ResourceKind CarriedResource { get; set; }
        public int CarryCreditPercent { get; set; }
        public float PhaseElapsedSeconds { get; set; }
        public int QueueTicket { get; set; }
        // Place held in the group in front of the door the unit waits at (a hauler queued at its source or
        // delivering to its destination, a free creature at its gathering building); -1 when it is not waiting.
        public int CrowdSlot { get; set; } = -1;
        // Goods a hauler may take from its source; empty means whatever the route can carry.
        public List<ResourceKind> Cargo { get; set; } = new();

        public bool CarriesAnything => Cargo == null || Cargo.Count == 0;
        public bool MayCarry(ResourceKind resource) => CarriesAnything || Cargo.Contains(resource);

        /// <summary>Free; with <paramref name="gatherAt"/> it walks to that building's door and waits there.</summary>
        public static Assignment Idle(string gatherAt = null) => new() { Kind = AssignmentKind.Idle, BuildingId = gatherAt };

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

        public static Assignment Haul(string sourceId, string destinationId, IEnumerable<ResourceKind> cargo = null) => new()
        {
            Kind = AssignmentKind.Haul,
            SourceId = sourceId,
            DestinationId = destinationId,
            Phase = HaulPhase.ToSource,
            Carried = 0,
            PhaseElapsedSeconds = 0f,
            Cargo = cargo != null ? new List<ResourceKind>(cargo) : new List<ResourceKind>()
        };

        public Assignment Clone() => new()
        {
            Kind = Kind,
            BuildingId = BuildingId,
            SourceId = SourceId,
            DestinationId = DestinationId,
            Phase = Phase,
            Carried = Carried,
            CarriedResource = CarriedResource,
            CarryCreditPercent = CarryCreditPercent,
            PhaseElapsedSeconds = PhaseElapsedSeconds,
            QueueTicket = QueueTicket,
            CrowdSlot = CrowdSlot,
            Cargo = Cargo != null ? new List<ResourceKind>(Cargo) : new List<ResourceKind>()
        };
    }

    [Serializable]
    public class BuildingState
    {
        public string Id { get; set; }
        public BuildingKind Kind { get; set; }
        public Cell Cell { get; set; }
        public Dictionary<ResourceKind, int> Stock { get; set; } = new();
        public float ProductionProgress { get; set; }
        public int CompletedCycles { get; set; }
        public int Level { get; set; } = 1;
        public int InvestedGold { get; set; }

        // Iron ore held by the building; a view over Stock, not a separate counter.
        public int Ore
        {
            get => GetStock(ResourceKind.IronOre);
            set => SetStock(ResourceKind.IronOre, value);
        }

        public int GetStock(ResourceKind resource) => Stock.TryGetValue(resource, out int amount) ? amount : 0;

        public void SetStock(ResourceKind resource, int amount)
        {
            if (amount > 0) Stock[resource] = amount;
            else Stock.Remove(resource);
        }

        public void AddStock(ResourceKind resource, int amount) => SetStock(resource, GetStock(resource) + amount);

        public int TotalStock
        {
            get
            {
                int total = 0;
                foreach (var amount in Stock.Values) total += amount;
                return total;
            }
        }

        public BuildingState Clone() => new()
        {
            Id = Id,
            Kind = Kind,
            Cell = Cell,
            Stock = new Dictionary<ResourceKind, int>(Stock),
            ProductionProgress = ProductionProgress,
            CompletedCycles = CompletedCycles,
            Level = Level,
            InvestedGold = InvestedGold
        };
    }

    [Serializable]
    public class UnitState
    {
        public string Id { get; set; }
        public UnitKind Kind { get; set; }
        // The creature's own name, given at hire and unique in the colony while names last.
        public string Name { get; set; }
        public WorldPosition Position { get; set; }
        public Assignment Assignment { get; set; }

        // Remaining waypoints of the current walk; valid for RouteGoal under RouteLayoutVersion.
        public List<WorldPosition> Route { get; set; } = new();
        public bool HasRoute { get; set; }
        public WorldPosition RouteGoal { get; set; }
        public int RouteLayoutVersion { get; set; }

        /// <summary>Moves the unit without walking; any planned route no longer starts here.</summary>
        public void PlaceAt(WorldPosition position)
        {
            Position = position;
            ClearRoute();
        }

        public void ClearRoute()
        {
            Route.Clear();
            HasRoute = false;
        }

        public UnitState Clone() => new()
        {
            Id = Id,
            Kind = Kind,
            Name = Name,
            Position = Position,
            Assignment = Assignment?.Clone() ?? Assignment.Idle(),
            Route = new List<WorldPosition>(Route),
            HasRoute = HasRoute,
            RouteGoal = RouteGoal,
            RouteLayoutVersion = RouteLayoutVersion
        };
    }
}
