using System;
using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    public class BuildingSnapshot
    {
        public string Id { get; set; }
        public BuildingKind Kind { get; set; }
        public string Name { get; set; }
        public Cell Cell { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Ore { get; set; }
        public int MaxOre { get; set; }
        public int WorkerCount { get; set; }
        public int MaxWorkers { get; set; }
        public float ProductionPerSecond { get; set; }
    }

    public class UnitSnapshot
    {
        public string Id { get; set; }
        public int Number { get; set; }
        public UnitKind UnitKind { get; set; }
        public string Name { get; set; }
        public int Strength { get; set; }
        public float Speed { get; set; }
        public int CargoCapacity { get; set; }
        public WorldPosition Position { get; set; }
        public Assignment Assignment { get; set; }
        public string Status { get; set; }
    }

    public class GameSnapshot
    {
        public int Revision { get; set; }
        public int Gold { get; set; }
        public int SoldOre { get; set; }
        public int TotalOre { get; set; }
        public IReadOnlyList<BuildingSnapshot> Buildings { get; set; }
        public IReadOnlyList<UnitSnapshot> Units { get; set; }
    }
}
