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
        /// <summary>Share of the current production cycle done, 0..1.</summary>
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
        public UnitKind UnitKind { get; }
        /// <summary>The creature's own name; its species is its definition's DisplayName.</summary>
        public string Name { get; }
        public int Strength { get; }
        public float Speed { get; }
        public int Stamina { get; }
        public float CarryCapacity => ColonySimulation.CarryCapacity(Stamina);
        public WorldPosition Position { get; }
        public Assignment Assignment => _assignment.Clone();
        public string Status { get; }
        public float MovementSpeed { get; }

        public UnitSnapshot(string id, UnitKind unitKind, string name, int strength, float speed,
            int stamina, WorldPosition position, Assignment assignment, string status,
            float movementSpeed = 0f)
        {
            Id = id;
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
        public ProgressSnapshot Progress { get; }
        /// <summary>Gold a won battle rolled that waits to be taken; null when there is none.</summary>
        public BattleRewardSnapshot BattleReward { get; }
        /// <summary>The colony's land blocks; null when land limits nothing.</summary>
        public LandSnapshot Land { get; }
        /// <summary>Every colony upgrade of the catalog with its level and next price, in catalog order.</summary>
        public IReadOnlyList<UpgradeSnapshot> Upgrades { get; }

        public GameSnapshot(int revision, int gold, int soldGoods, int totalOre,
            IReadOnlyList<BuildingSnapshot> buildings, IReadOnlyList<UnitSnapshot> units,
            IReadOnlyList<EquipmentSnapshot> equipment = null, ProgressSnapshot progress = null,
            BattleRewardSnapshot battleReward = null, LandSnapshot land = null,
            IReadOnlyList<UpgradeSnapshot> upgrades = null)
        {
            BattleReward = battleReward;
            Land = land;
            Upgrades = Copy(upgrades);
            Revision = revision;
            Gold = gold;
            SoldGoods = soldGoods;
            TotalOre = totalOre;
            Buildings = Copy(buildings);
            Units = Copy(units);
            Equipment = Copy(equipment);
            Progress = progress ?? ProgressSnapshot.Sandbox;
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

    /// <summary>A colony upgrade as its host building's panel shows it.</summary>
    public sealed class UpgradeSnapshot
    {
        public UpgradeSnapshot(string id, string name, string description, BuildingKind host, UpgradeEffect effect,
            int amountPerLevel, int level, int maxLevel, int nextCost, int hostLevel, int nextHostLevel)
        {
            Id = id;
            Name = name;
            Description = description;
            Host = host;
            Effect = effect;
            AmountPerLevel = amountPerLevel;
            Level = level;
            MaxLevel = maxLevel;
            NextCost = nextCost;
            HostLevel = hostLevel;
            NextHostLevel = nextHostLevel;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public BuildingKind Host { get; }
        public UpgradeEffect Effect { get; }
        public int AmountPerLevel { get; }
        public int Level { get; }
        public int MaxLevel { get; }
        /// <summary>Gold for the next level; -1 at the top.</summary>
        public int NextCost { get; }
        /// <summary>The level of the colony's highest host building; 0 while it has none.</summary>
        public int HostLevel { get; }
        /// <summary>The host level the next level needs; 0 at the top.</summary>
        public int NextHostLevel { get; }
        public bool HostBuilt => HostLevel > 0;
        public bool IsMaxed => Level >= MaxLevel;
        /// <summary>The next level is for sale: the colony's host building is big enough for it.</summary>
        public bool IsOpen => !IsMaxed && HostBuilt && HostLevel >= NextHostLevel;
        /// <summary>The host stands, but the next level waits for it to grow.</summary>
        public bool WaitsForHost => !IsMaxed && HostBuilt && HostLevel < NextHostLevel;
        /// <summary>The whole effect bought so far, in the effect's own unit (percent or count).</summary>
        public int Total => Level * AmountPerLevel;
    }

    /// <summary>
    /// The colony's land as the island and the HUD show it: every block (index = y * BlocksPerSide + x), the price
    /// of the next block and what a clearing costs.
    /// </summary>
    public sealed class LandSnapshot
    {
        private readonly LandBlockSnapshot[] _blocks;

        public LandSnapshot(int blocksPerSide, int blockSize, IReadOnlyList<LandBlockSnapshot> blocks, int nextPrice,
            int clearGold, float clearSeconds)
        {
            BlocksPerSide = blocksPerSide;
            BlockSize = blockSize;
            _blocks = new LandBlockSnapshot[blocks?.Count ?? 0];
            for (int i = 0; i < _blocks.Length; i++) _blocks[i] = blocks[i];
            NextPrice = nextPrice;
            ClearGold = clearGold;
            ClearSeconds = clearSeconds;
        }

        public int BlocksPerSide { get; }
        /// <summary>Side of a block in cells.</summary>
        public int BlockSize { get; }
        public IReadOnlyList<LandBlockSnapshot> Blocks => _blocks;
        /// <summary>Gold the next block costs, whichever is bought.</summary>
        public int NextPrice { get; }
        public int ClearGold { get; }
        public float ClearSeconds { get; }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < BlocksPerSide && y < BlocksPerSide;

        public LandBlockSnapshot Block(int x, int y) => Inside(x, y) ? _blocks[y * BlocksPerSide + x] : default;

        /// <summary>Block that holds the cell; false outside the land.</summary>
        public bool BlockOf(Cell cell, out int x, out int y)
        {
            x = cell.X >= 0 ? cell.X / BlockSize : -1;
            y = cell.Y >= 0 ? cell.Y / BlockSize : -1;
            return Inside(x, y);
        }
    }

    public readonly struct LandBlockSnapshot
    {
        public LandBlockSnapshot(int x, int y, bool owned, bool cleared, bool clearing, float clearProgress,
            float clearSecondsLeft, bool canBuy)
        {
            X = x;
            Y = y;
            Owned = owned;
            Cleared = cleared;
            Clearing = clearing;
            ClearProgress = clearProgress;
            ClearSecondsLeft = clearSecondsLeft;
            CanBuy = canBuy;
        }

        public int X { get; }
        public int Y { get; }
        public bool Owned { get; }
        public bool Cleared { get; }
        /// <summary>Owned and wild, with the clearing under way.</summary>
        public bool Clearing { get; }
        /// <summary>Share of the clearing done, 0..1.</summary>
        public float ClearProgress { get; }
        public float ClearSecondsLeft { get; }
        /// <summary>Not owned and next to owned land: buyable once the treasury holds <see cref="LandSnapshot.NextPrice"/>.</summary>
        public bool CanBuy { get; }
        public bool Wild => Owned && !Cleared;
    }

    /// <summary>
    /// The player's progress as the HUD shows it: the current quest and what is open. A sandbox game has
    /// no quests and everything open.
    /// </summary>
    public sealed class ProgressSnapshot
    {
        public static readonly ProgressSnapshot Sandbox = new(false, 0, null, null, null, null, null, null, null);

        private readonly IReadOnlyCollection<BuildingKind> _buildings;
        private readonly IReadOnlyCollection<UnitKind> _units;
        private readonly IReadOnlyCollection<string> _missions;
        private readonly IReadOnlyDictionary<BuildingKind, int> _buildingLevels;
        private readonly IReadOnlyDictionary<UnitKind, int> _unitLevels;
        private readonly IReadOnlyDictionary<string, int> _missionLevels;

        public ProgressSnapshot(bool enabled, int level, QuestSnapshot quest,
            IReadOnlyCollection<BuildingKind> unlockedBuildings, IReadOnlyCollection<UnitKind> unlockedUnits,
            IReadOnlyCollection<string> unlockedMissions, IReadOnlyDictionary<BuildingKind, int> buildingUnlockLevels,
            IReadOnlyDictionary<UnitKind, int> unitUnlockLevels, IReadOnlyDictionary<string, int> missionUnlockLevels)
        {
            Enabled = enabled;
            Level = level;
            Quest = quest;
            _buildings = unlockedBuildings ?? System.Array.Empty<BuildingKind>();
            _units = unlockedUnits ?? System.Array.Empty<UnitKind>();
            _missions = unlockedMissions ?? System.Array.Empty<string>();
            _buildingLevels = buildingUnlockLevels ?? new Dictionary<BuildingKind, int>();
            _unitLevels = unitUnlockLevels ?? new Dictionary<UnitKind, int>();
            _missionLevels = missionUnlockLevels ?? new Dictionary<string, int>();
        }

        /// <summary>False in a sandbox game: no quests, nothing locked.</summary>
        public bool Enabled { get; }
        /// <summary>The number of the current quest in the whole chain, from 1.</summary>
        public int Level { get; }
        /// <summary>The current quest; null in a sandbox game or when no quest follows.</summary>
        public QuestSnapshot Quest { get; }

        public bool IsBuildingUnlocked(BuildingKind kind) => !Enabled || Contains(_buildings, kind);
        public bool IsUnitUnlocked(UnitKind kind) => !Enabled || Contains(_units, kind);
        public bool IsMissionUnlocked(string missionId) => !Enabled || Contains(_missions, missionId);

        /// <summary>The level whose quest opens a locked building; 0 when open or when no quest opens it.</summary>
        public int UnlockLevel(BuildingKind kind) =>
            IsBuildingUnlocked(kind) || !_buildingLevels.TryGetValue(kind, out int level) ? 0 : level;

        public int UnlockLevel(UnitKind kind) =>
            IsUnitUnlocked(kind) || !_unitLevels.TryGetValue(kind, out int level) ? 0 : level;

        public int MissionUnlockLevel(string missionId) =>
            missionId == null || IsMissionUnlocked(missionId) || !_missionLevels.TryGetValue(missionId, out int level)
                ? 0
                : level;

        private static bool Contains<T>(IReadOnlyCollection<T> items, T item)
        {
            foreach (var candidate in items)
                if (EqualityComparer<T>.Default.Equals(candidate, item)) return true;
            return false;
        }
    }

    public sealed class BattleRewardSnapshot
    {
        public BattleRewardSnapshot(string missionName, int gold, int minGold, int maxGold, bool firstWin)
        {
            MissionName = missionName;
            Gold = gold;
            MinGold = minGold;
            MaxGold = maxGold;
            FirstWin = firstWin;
        }

        public string MissionName { get; }
        public int Gold { get; }
        public int MinGold { get; }
        public int MaxGold { get; }
        public bool FirstWin { get; }
    }

    public sealed class QuestSnapshot
    {
        public QuestSnapshot(string id, int level, string title, string description, bool isTutorial,
            int tutorialStep, int tutorialSteps, IReadOnlyList<GoalSnapshot> goals, IReadOnlyList<RewardSnapshot> rewards,
            bool isComplete)
        {
            Id = id;
            Level = level;
            Title = title;
            Description = description;
            IsTutorial = isTutorial;
            TutorialStep = tutorialStep;
            TutorialSteps = tutorialSteps;
            Goals = goals ?? System.Array.Empty<GoalSnapshot>();
            Rewards = rewards ?? System.Array.Empty<RewardSnapshot>();
            IsComplete = isComplete;
        }

        public string Id { get; }
        public int Level { get; }
        public string Title { get; }
        /// <summary>How to do it, in the player's clicks.</summary>
        public string Description { get; }
        public bool IsTutorial { get; }
        /// <summary>Step within the tutorial, from 1; 0 outside it.</summary>
        public int TutorialStep { get; }
        public int TutorialSteps { get; }
        public IReadOnlyList<GoalSnapshot> Goals { get; }
        public IReadOnlyList<RewardSnapshot> Rewards { get; }
        public bool IsComplete { get; }

        /// <summary>The reward the reveal lands on: the first unlock, or the gold when there is none.</summary>
        public RewardSnapshot Headline
        {
            get
            {
                foreach (var reward in Rewards)
                    if (reward.Kind != QuestRewardKind.Gold) return reward;
                return Rewards.Count > 0 ? Rewards[0] : null;
            }
        }

        /// <summary>The first goal still to do; null when all are met.</summary>
        public GoalSnapshot NextGoal
        {
            get
            {
                foreach (var goal in Goals)
                    if (!goal.Done) return goal;
                return null;
            }
        }
    }

    public sealed class GoalSnapshot
    {
        public GoalSnapshot(QuestGoal goal, string text, int current, bool done)
        {
            Goal = goal;
            Text = text;
            Current = done ? System.Math.Max(current, goal.Amount) : System.Math.Min(current, goal.Amount);
            Done = done;
        }

        public QuestGoal Goal { get; }
        public QuestGoalKind Kind => Goal.Kind;
        public string Text { get; }
        public int Current { get; }
        public int Target => Goal.Amount;
        public bool Done { get; }
        public string ProgressText => $"{System.Math.Min(Current, Target)}/{Target}";
    }

    public sealed class RewardSnapshot
    {
        public RewardSnapshot(QuestReward reward, string title, string caption, string description)
        {
            Reward = reward;
            Title = title;
            Caption = caption;
            Description = description;
        }

        public QuestReward Reward { get; }
        public QuestRewardKind Kind => Reward.Kind;
        /// <summary>What it is: the building's or creature's name, the mission, or the gold amount.</summary>
        public string Title { get; }
        /// <summary>What kind of reward it is, in capitals: new building, new creature…</summary>
        public string Caption { get; }
        public string Description { get; }
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
