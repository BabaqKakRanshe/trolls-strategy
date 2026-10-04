using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    /// <summary>Where the player stands in the quest chain and what they may build, hire and fight.</summary>
    [Serializable]
    public class ProgressState
    {
        public int QuestIndex { get; set; }
        // One entry per goal of the current quest. A goal once met stays met, so selling the goblin that
        // finished "hire a goblin" does not take the quest back.
        public List<bool> GoalDone { get; set; } = new();
        // Counter values when the quest began, for goals counted from then on (sales, earnings, wins).
        public List<int> GoalBaseline { get; set; } = new();
        public HashSet<BuildingKind> UnlockedBuildings { get; set; } = new();
        public HashSet<UnitKind> UnlockedUnits { get; set; } = new();
        public HashSet<string> UnlockedMissions { get; set; } = new(StringComparer.Ordinal);

        public ProgressState Clone() => new()
        {
            QuestIndex = QuestIndex,
            GoalDone = new List<bool>(GoalDone),
            GoalBaseline = new List<int>(GoalBaseline),
            UnlockedBuildings = new HashSet<BuildingKind>(UnlockedBuildings),
            UnlockedUnits = new HashSet<UnitKind>(UnlockedUnits),
            UnlockedMissions = new HashSet<string>(UnlockedMissions, StringComparer.Ordinal)
        };
    }

    /// <summary>
    /// Quest and unlock rules. A game without progress state (sandbox) has everything open and no quests.
    /// Goals are checked after every committed command and simulation step; a met goal stays met. Claiming a
    /// finished quest applies all its rewards and begins the next quest in one step.
    /// </summary>
    public static class Progression
    {
        public static void Start(GameState state, GameContentCatalog catalog)
        {
            var definition = catalog?.Progression ??
                throw new InvalidOperationException("Content catalog has no progression");
            var progress = new ProgressState();
            foreach (var unit in definition.StartingUnits) progress.UnlockedUnits.Add(unit);
            foreach (var building in definition.StartingBuildings) progress.UnlockedBuildings.Add(building);
            foreach (var mission in definition.StartingMissions)
                if (!string.IsNullOrEmpty(mission)) progress.UnlockedMissions.Add(mission);
            state.Progress = progress;
            Activate(state, catalog);
        }

        public static bool IsBuildingUnlocked(GameState state, BuildingKind kind) =>
            state.Progress == null || state.Progress.UnlockedBuildings.Contains(kind);

        public static bool IsUnitUnlocked(GameState state, UnitKind kind) =>
            state.Progress == null || state.Progress.UnlockedUnits.Contains(kind);

        public static bool IsMissionUnlocked(GameState state, string missionId) =>
            state.Progress == null || (missionId != null && state.Progress.UnlockedMissions.Contains(missionId));

        /// <summary>The quest the player works on now; null in a sandbox game or after the last quest.</summary>
        public static QuestDefinition CurrentQuest(GameState state, GameContentCatalog catalog) =>
            state.Progress == null || catalog?.Progression == null
                ? null
                : QuestAt(catalog.Progression, state.Progress.QuestIndex);

        /// <summary>
        /// The quest at a place in the chain. After the authored chain the repeatable quests follow in a cycle,
        /// each cycle raising their targets and gold by the definition's growth (an arena level by the ladder's step
        /// of milestones instead); null when nothing follows.
        /// </summary>
        public static QuestDefinition QuestAt(ProgressionDefinition definition, int index)
        {
            if (definition == null || index < 0) return null;
            var chain = definition.Quests;
            if (index < chain.Count) return chain[index];
            var repeatable = definition.RepeatableQuests;
            if (repeatable.Count == 0) return null;
            int offset = index - chain.Count;
            return Repeat(repeatable[offset % repeatable.Count], offset / repeatable.Count,
                definition.RepeatGrowthPercent, definition.RepeatArenaLevelStep, definition.RepeatArenaLevelCap);
        }

        /// <summary>How far a goal of the current quest has come: counted from the quest's start for sales and wins.</summary>
        public static int GoalProgress(GameState state, int goalIndex, QuestGoal goal)
        {
            int value = Measure(state, goal);
            if (goal.IsCumulative && state.Progress != null && goalIndex < state.Progress.GoalBaseline.Count)
                value -= state.Progress.GoalBaseline[goalIndex];
            return Math.Max(0, value);
        }

        /// <summary>The colony's current count for a goal, before any quest baseline.</summary>
        public static int Measure(GameState state, QuestGoal goal)
        {
            switch (goal.Kind)
            {
                case QuestGoalKind.OwnUnits:
                {
                    int count = 0;
                    foreach (var unit in state.Units)
                        if (goal.CountsUnit(unit.Kind)) count++;
                    return count;
                }
                case QuestGoalKind.OwnBuildings:
                {
                    int count = 0;
                    foreach (var building in state.Buildings)
                        if (building.Kind == goal.Building) count++;
                    return count;
                }
                case QuestGoalKind.WorkAt:
                {
                    int count = 0;
                    foreach (var unit in state.Units)
                    {
                        var a = unit.Assignment;
                        if (!goal.CountsUnit(unit.Kind) ||
                            (a.Kind != AssignmentKind.Work && a.Kind != AssignmentKind.ToWork)) continue;
                        if (KindOf(state, a.BuildingId) == goal.Building) count++;
                    }
                    return count;
                }
                case QuestGoalKind.HaulRoute:
                {
                    int count = 0;
                    foreach (var unit in state.Units)
                    {
                        var a = unit.Assignment;
                        if (!goal.CountsUnit(unit.Kind) || a.Kind != AssignmentKind.Haul) continue;
                        if (KindOf(state, a.SourceId) == goal.Building && KindOf(state, a.DestinationId) == goal.Destination)
                            count++;
                    }
                    return count;
                }
                case QuestGoalKind.HaveGold:
                    return state.Gold;
                case QuestGoalKind.EarnGold:
                    return state.SalesGold;
                case QuestGoalKind.SellGoods:
                    return state.SoldGoods;
                case QuestGoalKind.SellResource:
                    return state.SoldOf(goal.Resource);
                case QuestGoalKind.WinBattles:
                    return state.BattlesWon;
                case QuestGoalKind.ReachArenaLevel:
                    return state.HighestMissionLevel;
                case QuestGoalKind.OwnLand:
                    return state.Land?.Purchases ?? 0;
                case QuestGoalKind.ProduceResource:
                    return state.ProducedOf(goal.Resource);
                case QuestGoalKind.BuyUpgrades:
                {
                    int levels = 0;
                    foreach (var level in state.Upgrades.Values) levels += level;
                    return levels;
                }
                case QuestGoalKind.EquipFighters:
                {
                    var owners = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var item in state.Equipment)
                        if (item.OwnerUnitId != null) owners.Add(item.OwnerUnitId);
                    return owners.Count;
                }
                case QuestGoalKind.OwnEquipment:
                    return state.Equipment.Count;
                case QuestGoalKind.UpgradeBuilding:
                {
                    int level = 0;
                    foreach (var building in state.Buildings)
                        if (building.Kind == goal.Building) level = Math.Max(level, building.Level);
                    return level;
                }
                default:
                    return 0;
            }
        }

        public static bool IsQuestComplete(GameState state, GameContentCatalog catalog)
        {
            var quest = CurrentQuest(state, catalog);
            if (quest == null || state.Progress.GoalDone.Count != quest.Goals.Count) return false;
            foreach (bool done in state.Progress.GoalDone)
                if (!done) return false;
            return true;
        }

        /// <summary>Marks every goal of the current quest the colony has now met. Met goals stay met.</summary>
        public static void Update(GameState state, GameContentCatalog catalog)
        {
            var quest = CurrentQuest(state, catalog);
            if (quest == null) return;
            var progress = state.Progress;
            if (progress.GoalDone.Count != quest.Goals.Count || progress.GoalBaseline.Count != quest.Goals.Count)
            {
                Activate(state, catalog);
                return;
            }
            for (int i = 0; i < quest.Goals.Count; i++)
            {
                if (progress.GoalDone[i]) continue;
                var goal = quest.Goals[i];
                if (GoalProgress(state, i, goal) >= goal.Amount) progress.GoalDone[i] = true;
            }
        }

        /// <summary>Gives every reward of the finished current quest and begins the next one.</summary>
        public static CommandResult Claim(GameState state, GameContentCatalog catalog)
        {
            if (state.Progress == null) return CommandResult.Fail("В этой игре нет заданий");
            var quest = CurrentQuest(state, catalog);
            if (quest == null) return CommandResult.Fail("Все задания выполнены");
            Update(state, catalog);
            if (!IsQuestComplete(state, catalog)) return CommandResult.Fail("Задание ещё не выполнено");

            var progress = state.Progress;
            foreach (var reward in quest.Rewards)
            {
                switch (reward.Kind)
                {
                    case QuestRewardKind.Gold:
                        state.Gold += reward.Gold;
                        break;
                    case QuestRewardKind.UnlockBuilding:
                        progress.UnlockedBuildings.Add(reward.Building);
                        break;
                    case QuestRewardKind.UnlockUnit:
                        progress.UnlockedUnits.Add(reward.Unit);
                        break;
                    case QuestRewardKind.UnlockMission:
                        if (!string.IsNullOrEmpty(reward.MissionId)) progress.UnlockedMissions.Add(reward.MissionId);
                        break;
                }
            }
            progress.QuestIndex++;
            Activate(state, catalog);
            return CommandResult.Success();
        }

        /// <summary>Development shortcut: marks every goal of the current quest as met.</summary>
        public static bool CompleteCurrentQuest(GameState state, GameContentCatalog catalog)
        {
            var quest = CurrentQuest(state, catalog);
            if (quest == null) return false;
            Update(state, catalog);
            for (int i = 0; i < state.Progress.GoalDone.Count; i++) state.Progress.GoalDone[i] = true;
            return true;
        }

        // Starts the current quest: nothing met yet, sales and wins counted from now on.
        private static void Activate(GameState state, GameContentCatalog catalog)
        {
            var progress = state.Progress;
            progress.GoalDone.Clear();
            progress.GoalBaseline.Clear();
            var quest = CurrentQuest(state, catalog);
            if (quest == null) return;
            foreach (var goal in quest.Goals)
            {
                progress.GoalDone.Add(false);
                progress.GoalBaseline.Add(goal.IsCumulative ? Measure(state, goal) : 0);
            }
            Update(state, catalog);
        }

        private static QuestDefinition Repeat(QuestDefinition template, int cycle, int growthPercent, int arenaStep,
            int arenaCap)
        {
            int percent = 100 + Math.Max(0, growthPercent) * cycle;
            var goals = new List<QuestGoal>(template.Goals.Count);
            foreach (var goal in template.Goals)
                goals.Add(goal.Kind == QuestGoalKind.UpgradeBuilding ? goal
                    // the arena climbs to the next milestone of the ladder each cycle, never past its top
                    : goal.Kind == QuestGoalKind.ReachArenaLevel
                        ? goal.WithAmount(Math.Min(Math.Max(goal.Amount, arenaCap), goal.Amount + cycle * Math.Max(1, arenaStep)))
                        : goal.WithAmount(Scale(goal.Amount, percent)));
            var rewards = new List<QuestReward>(template.Rewards.Count);
            foreach (var reward in template.Rewards)
                rewards.Add(reward.Kind == QuestRewardKind.Gold ? reward.WithGold(Scale(reward.Gold, percent)) : reward);
            return new QuestDefinition($"{template.Id}#{cycle + 1}", template.Title, template.Description, false,
                goals, rewards);
        }

        private static int Scale(int value, int percent) => (int)Math.Min(int.MaxValue, (long)value * percent / 100);

        private static BuildingKind? KindOf(GameState state, string buildingId)
        {
            if (buildingId == null) return null;
            foreach (var building in state.Buildings)
                if (building.Id == buildingId) return building.Kind;
            return null;
        }
    }
}
