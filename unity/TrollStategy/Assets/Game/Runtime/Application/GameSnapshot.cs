using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    public class BuildingSnapshot
    {
        public string Id { get; }
        public BuildingKind Kind { get; }
        public string Name { get; }
        public Cell Cell { get; }
        public int Width { get; }
        public int Height { get; }
        public int Ore { get; }
        public int MaxOre { get; }
        public int WorkerCount { get; }
        public int MaxWorkers { get; }
        public float ProductionPerSecond { get; }
        public float ProductionProgress { get; }

        public BuildingSnapshot(string id, BuildingKind kind, string name, Cell cell, int width, int height,
            int ore, int maxOre, int workerCount, int maxWorkers, float productionPerSecond,
            float productionProgress = 0f)
        {
            Id = id;
            Kind = kind;
            Name = name;
            Cell = cell;
            Width = width;
            Height = height;
            Ore = ore;
            MaxOre = maxOre;
            WorkerCount = workerCount;
            MaxWorkers = maxWorkers;
            ProductionPerSecond = productionPerSecond;
            ProductionProgress = productionProgress;
        }
    }

    public class UnitSnapshot
    {
        private readonly Assignment _assignment;

        public string Id { get; }
        public int Number { get; }
        public UnitKind UnitKind { get; }
        public string Name { get; }
        public int Strength { get; }
        public float Speed { get; }
        public int CargoCapacity { get; }
        public WorldPosition Position { get; }
        public Assignment Assignment => _assignment.Clone();
        public string Status { get; }
        public float MovementSpeed { get; }

        public UnitSnapshot(string id, int number, UnitKind unitKind, string name, int strength, float speed,
            int cargoCapacity, WorldPosition position, Assignment assignment, string status,
            float movementSpeed = 0f)
        {
            Id = id;
            Number = number;
            UnitKind = unitKind;
            Name = name;
            Strength = strength;
            Speed = speed;
            CargoCapacity = cargoCapacity;
            Position = position;
            _assignment = assignment?.Clone() ?? Domain.Assignment.Idle();
            Status = status;
            MovementSpeed = movementSpeed;
        }
    }

    public class GameSnapshot
    {
        public int Revision { get; }
        public int Gold { get; }
        public int SoldOre { get; }
        public int TotalOre { get; }
        public IReadOnlyList<BuildingSnapshot> Buildings { get; }
        public IReadOnlyList<UnitSnapshot> Units { get; }

        public GameSnapshot(int revision, int gold, int soldOre, int totalOre,
            IReadOnlyList<BuildingSnapshot> buildings, IReadOnlyList<UnitSnapshot> units)
        {
            Revision = revision;
            Gold = gold;
            SoldOre = soldOre;
            TotalOre = totalOre;
            Buildings = Copy(buildings);
            Units = Copy(units);
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
                return System.Array.Empty<T>();

            var copy = new T[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }
    }
}
