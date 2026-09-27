using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    public readonly struct ResourceStack
    {
        public ResourceKind Resource { get; }
        public string Name { get; }
        public int Amount { get; }

        public ResourceStack(ResourceKind resource, string name, int amount)
        {
            Resource = resource;
            Name = name;
            Amount = amount;
        }
    }

    public class BuildingSnapshot
    {
        public string Id { get; }
        public BuildingKind Kind { get; }
        public string Name { get; }
        public Cell Cell { get; }
        public int Width { get; }
        public int Height { get; }
        public int TotalStock { get; }
        public int Capacity { get; }
        public int WorkerCount { get; }
        public int MaxWorkers { get; }
        public float ProductionPerSecond { get; }
        public float ProductionProgress { get; }
        public int Level { get; }
        public int RefundGold { get; }
        public int UpgradeCost { get; }
        public int SaleBonus { get; }
        public int GoblinHaulers { get; }
        public int TrollHaulers { get; }
        public int SlotStackSize { get; }
        public IReadOnlyList<StorageSlot> Slots { get; }
        public IReadOnlyList<ResourceStack> Stock { get; }
        public bool IsWorkplace { get; }
        public ProductionState ProductionState { get; }
        public string RecipeText { get; }

        public BuildingSnapshot(string id, BuildingKind kind, string name, Cell cell, int width, int height,
            int totalStock, int capacity, int workerCount, int maxWorkers, float productionPerSecond,
            float productionProgress = 0f, int level = 1, int refundGold = 0, int upgradeCost = -1,
            int saleBonus = 0, int goblinHaulers = 0, int trollHaulers = 0,
            int slotStackSize = 0, IReadOnlyList<StorageSlot> slots = null,
            IReadOnlyList<ResourceStack> stock = null, bool isWorkplace = false,
            ProductionState productionState = ProductionState.NotProducer, string recipeText = "")
        {
            Id = id;
            Kind = kind;
            Name = name;
            Cell = cell;
            Width = width;
            Height = height;
            TotalStock = totalStock;
            Capacity = capacity;
            WorkerCount = workerCount;
            MaxWorkers = maxWorkers;
            ProductionPerSecond = productionPerSecond;
            ProductionProgress = productionProgress;
            Level = level;
            RefundGold = refundGold;
            UpgradeCost = upgradeCost;
            SaleBonus = saleBonus;
            GoblinHaulers = goblinHaulers;
            TrollHaulers = trollHaulers;
            SlotStackSize = slotStackSize;
            Slots = slots != null ? new List<StorageSlot>(slots) : (IReadOnlyList<StorageSlot>)System.Array.Empty<StorageSlot>();
            Stock = stock != null ? new List<ResourceStack>(stock) : (IReadOnlyList<ResourceStack>)System.Array.Empty<ResourceStack>();
            IsWorkplace = isWorkplace;
            ProductionState = productionState;
            RecipeText = recipeText ?? "";
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
        public int Stamina { get; }
        public float CarryCapacity => ColonySimulation.CarryCapacity(Stamina);
        public WorldPosition Position { get; }
        public Assignment Assignment => _assignment.Clone();
        public string Status { get; }
        public float MovementSpeed { get; }

        public UnitSnapshot(string id, int number, UnitKind unitKind, string name, int strength, float speed,
            int stamina, WorldPosition position, Assignment assignment, string status,
            float movementSpeed = 0f)
        {
            Id = id;
            Number = number;
            UnitKind = unitKind;
            Name = name;
            Strength = strength;
            Speed = speed;
            Stamina = stamina;
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
        public int SoldGoods { get; }
        public int TotalOre { get; }
        public IReadOnlyList<BuildingSnapshot> Buildings { get; }
        public IReadOnlyList<UnitSnapshot> Units { get; }
        public IReadOnlyList<EquipmentSnapshot> Equipment { get; }

        public GameSnapshot(int revision, int gold, int soldGoods, int totalOre,
            IReadOnlyList<BuildingSnapshot> buildings, IReadOnlyList<UnitSnapshot> units,
            IReadOnlyList<EquipmentSnapshot> equipment = null)
        {
            Revision = revision;
            Gold = gold;
            SoldGoods = soldGoods;
            TotalOre = totalOre;
            Buildings = Copy(buildings);
            Units = Copy(units);
            Equipment = Copy(equipment);
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

    public sealed class EquipmentSnapshot
    {
        public string Id { get; }
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public EquipmentSlot Slot { get; }
        public int DamageBonus { get; }
        public int ArmorBonus { get; }
        public string OwnerUnitId { get; }

        public EquipmentSnapshot(string id, EquipmentDefinition definition, string ownerUnitId)
        {
            Id = id;
            DefinitionId = definition.ItemId;
            DisplayName = definition.DisplayName;
            Slot = definition.Slot;
            DamageBonus = definition.DamageBonus;
            ArmorBonus = definition.ArmorBonus;
            OwnerUnitId = ownerUnitId;
        }
    }
}
