using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrollStrategy.Content
{
    // Values are serialized by index: append new kinds, never reorder.
    public enum QuestGoalKind
    {
        /// <summary>Own at least Amount creatures of Unit (or of any kind).</summary>
        OwnUnits,
        /// <summary>Own at least Amount buildings of Building.</summary>
        OwnBuildings,
        /// <summary>Amount creatures of Unit (or any) working, or walking to work, at a Building.</summary>
        WorkAt,
        /// <summary>Amount creatures of Unit (or any) carrying from a Building to a Destination.</summary>
        HaulRoute,
        /// <summary>Hold at least Amount gold.</summary>
        HaveGold,
        /// <summary>Earn Amount gold at the market after the quest begins.</summary>
        EarnGold,
        /// <summary>Sell Amount goods of any kind after the quest begins.</summary>
        SellGoods,
        /// <summary>Sell Amount of Resource after the quest begins.</summary>
        SellResource,
        /// <summary>Win Amount battles after the quest begins.</summary>
        WinBattles,
        /// <summary>Raise a Building to level Amount.</summary>
        UpgradeBuilding
    }

    // Values are serialized by index: append new kinds, never reorder.
    public enum QuestRewardKind
    {
        Gold,
        UnlockBuilding,
        UnlockUnit,
        UnlockMission
    }

    /// <summary>One condition of a quest. Which fields matter depends on the kind.</summary>
    [Serializable]
    public struct QuestGoal
    {
        [SerializeField] private QuestGoalKind _kind;
        [Tooltip("Creature the goal counts, unless Any Unit is set.")]
        [SerializeField] private UnitKind _unit;
        [SerializeField] private bool _anyUnit;
        [Tooltip("Building to own, work at, upgrade or carry from.")]
        [SerializeField] private BuildingKind _building;
        [Tooltip("Where the haulers carry to (Haul Route only).")]
        [SerializeField] private BuildingKind _destination;
        [SerializeField] private ResourceKind _resource;
        [Tooltip("Count, gold or building level to reach.")]
        [SerializeField, Min(1)] private int _amount;

        private QuestGoal(QuestGoalKind kind, int amount, UnitKind unit = default, bool anyUnit = false,
            BuildingKind building = default, BuildingKind destination = default, ResourceKind resource = default)
        {
            _kind = kind;
            _amount = Math.Max(1, amount);
            _unit = unit;
            _anyUnit = anyUnit;
            _building = building;
            _destination = destination;
            _resource = resource;
        }

        public QuestGoalKind Kind => _kind;
        public UnitKind Unit => _unit;
        public bool AnyUnit => _anyUnit;
        public BuildingKind Building => _building;
        public BuildingKind Destination => _destination;
        public ResourceKind Resource => _resource;
        public int Amount => Math.Max(1, _amount);

        /// <summary>True for goals counted from the moment the quest begins rather than from the colony as it is.</summary>
        public bool IsCumulative => _kind == QuestGoalKind.EarnGold || _kind == QuestGoalKind.SellGoods ||
                                    _kind == QuestGoalKind.SellResource || _kind == QuestGoalKind.WinBattles;

        public bool CountsUnit(UnitKind kind) => _anyUnit || _unit == kind;

        public QuestGoal WithAmount(int amount)
        {
            var copy = this;
            copy._amount = Math.Max(1, amount);
            return copy;
        }

        public static QuestGoal OwnUnits(UnitKind unit, int amount) => new(QuestGoalKind.OwnUnits, amount, unit);
        public static QuestGoal Population(int amount) => new(QuestGoalKind.OwnUnits, amount, anyUnit: true);
        public static QuestGoal OwnBuildings(BuildingKind building, int amount = 1) =>
            new(QuestGoalKind.OwnBuildings, amount, building: building);
        public static QuestGoal WorkAt(UnitKind unit, BuildingKind building, int amount = 1) =>
            new(QuestGoalKind.WorkAt, amount, unit, building: building);
        public static QuestGoal AnyWorkAt(BuildingKind building, int amount) =>
            new(QuestGoalKind.WorkAt, amount, anyUnit: true, building: building);
        public static QuestGoal HaulRoute(UnitKind unit, BuildingKind from, BuildingKind to, int amount = 1) =>
            new(QuestGoalKind.HaulRoute, amount, unit, building: from, destination: to);
        public static QuestGoal AnyHaulRoute(BuildingKind from, BuildingKind to, int amount = 1) =>
            new(QuestGoalKind.HaulRoute, amount, anyUnit: true, building: from, destination: to);
        public static QuestGoal HaveGold(int amount) => new(QuestGoalKind.HaveGold, amount);
        public static QuestGoal EarnGold(int amount) => new(QuestGoalKind.EarnGold, amount);
        public static QuestGoal SellGoods(int amount) => new(QuestGoalKind.SellGoods, amount);
        public static QuestGoal SellResource(ResourceKind resource, int amount) =>
            new(QuestGoalKind.SellResource, amount, resource: resource);
        public static QuestGoal WinBattles(int amount = 1) => new(QuestGoalKind.WinBattles, amount);
        public static QuestGoal UpgradeBuilding(BuildingKind building, int level) =>
            new(QuestGoalKind.UpgradeBuilding, level, building: building);
    }

    /// <summary>What claiming a finished quest gives: gold or the right to build, hire or fight.</summary>
    [Serializable]
    public struct QuestReward
    {
        [SerializeField] private QuestRewardKind _kind;
        [SerializeField, Min(0)] private int _gold;
        [SerializeField] private BuildingKind _building;
        [SerializeField] private UnitKind _unit;
        [SerializeField] private string _missionId;

        private QuestReward(QuestRewardKind kind, int gold = 0, BuildingKind building = default,
            UnitKind unit = default, string missionId = null)
        {
            _kind = kind;
            _gold = Math.Max(0, gold);
            _building = building;
            _unit = unit;
            _missionId = missionId ?? string.Empty;
        }

        public QuestRewardKind Kind => _kind;
        public int Gold => Math.Max(0, _gold);
        public BuildingKind Building => _building;
        public UnitKind Unit => _unit;
        public string MissionId => _missionId ?? string.Empty;
        public bool IsUnlock => _kind != QuestRewardKind.Gold;

        public QuestReward WithGold(int gold)
        {
            var copy = this;
            copy._gold = Math.Max(0, gold);
            return copy;
        }

        public static QuestReward Coins(int gold) => new(QuestRewardKind.Gold, gold);
        public static QuestReward UnlockBuilding(BuildingKind building) =>
            new(QuestRewardKind.UnlockBuilding, building: building);
        public static QuestReward UnlockUnit(UnitKind unit) => new(QuestRewardKind.UnlockUnit, unit: unit);
        public static QuestReward UnlockMission(string missionId) =>
            new(QuestRewardKind.UnlockMission, missionId: missionId);
    }

    /// <summary>A quest: what to do, how to do it, and what claiming it gives.</summary>
    [Serializable]
    public sealed class QuestDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _title;
        [Tooltip("How to do it, in the player's clicks.")]
        [SerializeField, TextArea(2, 5)] private string _description;
        [Tooltip("Tutorial steps are counted as the tutorial in the quest tracker.")]
        [SerializeField] private bool _tutorial;
        [SerializeField] private List<QuestGoal> _goals = new();
        [SerializeField] private List<QuestReward> _rewards = new();

        // for the serializer
        private QuestDefinition() { }

        public QuestDefinition(string id, string title, string description, bool tutorial,
            IEnumerable<QuestGoal> goals, IEnumerable<QuestReward> rewards)
        {
            _id = id;
            _title = title;
            _description = description;
            _tutorial = tutorial;
            _goals = goals != null ? new List<QuestGoal>(goals) : new List<QuestGoal>();
            _rewards = rewards != null ? new List<QuestReward>(rewards) : new List<QuestReward>();
        }

        public string Id => _id ?? string.Empty;
        public string Title => _title ?? string.Empty;
        public string Description => _description ?? string.Empty;
        public bool IsTutorial => _tutorial;
        public IReadOnlyList<QuestGoal> Goals => _goals ?? (IReadOnlyList<QuestGoal>)Array.Empty<QuestGoal>();
        public IReadOnlyList<QuestReward> Rewards => _rewards ?? (IReadOnlyList<QuestReward>)Array.Empty<QuestReward>();
    }

    /// <summary>
    /// The player's path through the game: what is open at the start, the authored quest chain (tutorial first,
    /// then quests that unlock the remaining buildings) and the repeatable quests that follow the chain.
    /// </summary>
    [CreateAssetMenu(fileName = "Progression", menuName = "TrollStrategy/Content/Progression")]
    public class ProgressionDefinition : ScriptableObject
    {
        [Header("Open from the start")]
        [SerializeField] private UnitKind[] _startingUnits = { UnitKind.Goblin };
        [SerializeField] private BuildingKind[] _startingBuildings = Array.Empty<BuildingKind>();
        [SerializeField] private string[] _startingMissions = Array.Empty<string>();

        [Header("Quests")]
        [Tooltip("Played once, in order: the tutorial steps first.")]
        [SerializeField] private List<QuestDefinition> _quests = new();
        [Tooltip("Cycled after the chain ends; they give gold only and their targets grow every cycle.")]
        [SerializeField] private List<QuestDefinition> _repeatableQuests = new();
        [Tooltip("How much each full cycle of repeatable quests raises their targets and gold, in percent.")]
        [SerializeField, Range(0, 200)] private int _repeatGrowthPercent = 25;

        public IReadOnlyList<UnitKind> StartingUnits => _startingUnits ?? Array.Empty<UnitKind>();
        public IReadOnlyList<BuildingKind> StartingBuildings => _startingBuildings ?? Array.Empty<BuildingKind>();
        public IReadOnlyList<string> StartingMissions => _startingMissions ?? Array.Empty<string>();
        public IReadOnlyList<QuestDefinition> Quests => _quests ?? (IReadOnlyList<QuestDefinition>)Array.Empty<QuestDefinition>();
        public IReadOnlyList<QuestDefinition> RepeatableQuests =>
            _repeatableQuests ?? (IReadOnlyList<QuestDefinition>)Array.Empty<QuestDefinition>();
        public int RepeatGrowthPercent => _repeatGrowthPercent;

        public void Init(IEnumerable<UnitKind> startingUnits, IEnumerable<BuildingKind> startingBuildings,
            IEnumerable<string> startingMissions, IEnumerable<QuestDefinition> quests,
            IEnumerable<QuestDefinition> repeatableQuests, int repeatGrowthPercent)
        {
            _startingUnits = startingUnits != null ? new List<UnitKind>(startingUnits).ToArray() : Array.Empty<UnitKind>();
            _startingBuildings = startingBuildings != null
                ? new List<BuildingKind>(startingBuildings).ToArray()
                : Array.Empty<BuildingKind>();
            _startingMissions = startingMissions != null ? new List<string>(startingMissions).ToArray() : Array.Empty<string>();
            _quests = quests != null ? new List<QuestDefinition>(quests) : new List<QuestDefinition>();
            _repeatableQuests = repeatableQuests != null
                ? new List<QuestDefinition>(repeatableQuests)
                : new List<QuestDefinition>();
            _repeatGrowthPercent = Mathf.Clamp(repeatGrowthPercent, 0, 200);
        }
    }
}
