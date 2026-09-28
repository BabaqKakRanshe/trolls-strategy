using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.UI
{
    /// <summary>
    /// What the HUD points at for the next goal of the current quest: the card to buy, the command to give,
    /// the building to pick, the battle button. Worked out from the snapshot on every refresh; it holds no state.
    /// </summary>
    public sealed class QuestFocus
    {
        public static readonly QuestFocus None = new();

        public UnitKind? Hire { get; private set; }
        public BuildingKind? Build { get; private set; }
        public BuildingKind? WorkTarget { get; private set; }
        public BuildingKind? HaulFrom { get; private set; }
        public BuildingKind? HaulTo { get; private set; }
        public bool Battle { get; private set; }
        /// <summary>The goal a work or haul order is for; tells which selected creatures it concerns.</summary>
        public QuestGoal Order { get; private set; }

        public bool UsesCatalog => Hire != null || Build != null;

        /// <summary>True when the order the focus points at would count for this creature.</summary>
        public bool Orders(UnitKind kind) => (WorkTarget != null || HaulFrom != null) && Order.CountsUnit(kind);

        public static QuestFocus From(GameSnapshot snapshot)
        {
            var quest = snapshot.Progress.Quest;
            var goal = quest == null || quest.IsComplete ? null : quest.NextGoal;
            if (goal == null) return None;

            var g = goal.Goal;
            var focus = new QuestFocus();
            switch (g.Kind)
            {
                case QuestGoalKind.OwnUnits:
                    if (!g.AnyUnit) focus.Hire = g.Unit;
                    break;
                case QuestGoalKind.OwnBuildings:
                    focus.Build = g.Building;
                    break;
                case QuestGoalKind.WorkAt:
                    focus.Order = g;
                    focus.WorkTarget = g.Building;
                    if (!g.AnyUnit && !HasFree(snapshot, g)) focus.Hire = g.Unit;
                    break;
                case QuestGoalKind.HaulRoute:
                    focus.Order = g;
                    focus.HaulFrom = g.Building;
                    focus.HaulTo = g.Destination;
                    if (!g.AnyUnit && !HasFree(snapshot, g)) focus.Hire = g.Unit;
                    break;
                case QuestGoalKind.WinBattles:
                    focus.Battle = true;
                    break;
            }
            return focus;
        }

        // A creature the order could go to: one of the right kind not already doing what the goal asks.
        private static bool HasFree(GameSnapshot snapshot, QuestGoal goal)
        {
            foreach (var unit in snapshot.Units)
            {
                if (!goal.CountsUnit(unit.UnitKind)) continue;
                var a = unit.Assignment;
                bool counted = goal.Kind == QuestGoalKind.WorkAt
                    ? (a.Kind == AssignmentKind.Work || a.Kind == AssignmentKind.ToWork) &&
                      KindOf(snapshot.Buildings, a.BuildingId) == goal.Building
                    : a.Kind == AssignmentKind.Haul && KindOf(snapshot.Buildings, a.SourceId) == goal.Building &&
                      KindOf(snapshot.Buildings, a.DestinationId) == goal.Destination;
                if (!counted) return true;
            }
            return false;
        }

        private static BuildingKind? KindOf(IReadOnlyList<BuildingSnapshot> buildings, string id)
        {
            foreach (var building in buildings)
                if (building.Id == id) return building.Kind;
            return null;
        }
    }
}
