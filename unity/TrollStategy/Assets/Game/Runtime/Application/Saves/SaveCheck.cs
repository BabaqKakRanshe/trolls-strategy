using System;
using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>
    /// The colony-wide rules a read save must keep before a session plays it: one owner per id, every reference
    /// pointing at something the save has, gear in free slots, the layout the placement rules allow, this build's
    /// land grid and a quest this build's chain has. A broken rule refuses the save; nothing is mended.
    /// </summary>
    internal static class SaveCheck
    {
        public static void Run(GameState state, GameContentCatalog catalog)
        {
            CheckLand(state, catalog);

            var buildings = new Dictionary<string, BuildingState>(StringComparer.Ordinal);
            for (int i = 0; i < state.Buildings.Count; i++)
            {
                var building = state.Buildings[i];
                if (!buildings.TryAdd(building.Id, building))
                    Refuse(SaveError.DuplicateId, $"buildings[{i}].id: \"{building.Id}\" twice");
            }

            var units = new Dictionary<string, UnitState>(StringComparer.Ordinal);
            for (int i = 0; i < state.Units.Count; i++)
            {
                var unit = state.Units[i];
                string path = $"units[{i}]";
                if (!units.TryAdd(unit.Id, unit)) Refuse(SaveError.DuplicateId, $"{path}.id: \"{unit.Id}\" twice");
                // hires take "unit-N" from the counter without looking: it must run ahead of every serial
                if (unit.Id.StartsWith("unit-", StringComparison.Ordinal) &&
                    int.TryParse(unit.Id.Substring(5), out int serial) && serial >= state.NextUnitId)
                    Refuse(SaveError.InvalidValue, $"nextUnitId: {state.NextUnitId}, not past {unit.Id}");
                CheckAssignment(unit.Assignment, path + ".assignment", buildings);
            }

            var items = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < state.Equipment.Count; i++)
            {
                var item = state.Equipment[i];
                string path = $"equipment[{i}]";
                if (string.IsNullOrEmpty(item.Id)) Refuse(SaveError.InvalidValue, $"{path}.id: empty");
                if (!items.Add(item.Id)) Refuse(SaveError.DuplicateId, $"{path}.id: \"{item.Id}\" twice");
                if (item.OwnerUnitId == null) continue;
                if (!units.ContainsKey(item.OwnerUnitId))
                    Refuse(SaveError.DanglingReference, $"{path}.owner: no creature \"{item.OwnerUnitId}\"");
                var slot = catalog.GetEquipment(item.DefinitionId).Slot;
                if (!slots.Add($"{item.OwnerUnitId}:{slot}"))
                    Refuse(SaveError.InvalidOwnership, $"{path}: {item.OwnerUnitId} already wears a {slot}");
            }

            // every footprint as the placement rules see it: on the grid, on cleared land, apart, doors open
            for (int i = 0; i < state.Buildings.Count; i++)
            {
                var building = state.Buildings[i];
                var placed = ColonySimulation.ValidateBuildingPlacement(state, building.Kind, building.Cell, catalog,
                    building.Id);
                if (!placed.Ok)
                    Refuse(SaveError.InvalidLayout,
                        $"buildings[{i}] {building.Id} at {building.Cell}: {placed.Error}");
            }

            var progress = state.Progress;
            if (progress != null && (progress.QuestStartMs < 0 || progress.QuestStartMs > state.ActiveTimeMs))
                Refuse(SaveError.InvalidValue,
                    $"progress.questStartMs: {progress.QuestStartMs} with {state.ActiveTimeMs} ms played");
            if (state.ArenaFundSinceMs > state.ActiveTimeMs)
                Refuse(SaveError.InvalidValue,
                    $"arenaFundSinceMs: {state.ArenaFundSinceMs} with {state.ActiveTimeMs} ms played");
        }

        /// <summary>
        /// Finds the saved quest in this build's chain: at its saved place when the id is still there, else by its
        /// id (a chain that grew in front of it moves it), a repeatable one by its template and cycle. Its goals must
        /// match the saved ones in number.
        /// </summary>
        public static void PlaceQuest(GameState state, string questId, GameContentCatalog catalog)
        {
            var progress = state.Progress;
            var definition = catalog.Progression;
            if (progress.ChainLength > 0 && progress.QuestIndex > progress.ChainLength)
                Refuse(SaveError.InvalidValue,
                    $"progress: quest {progress.QuestIndex + 1} of a game that ends after {progress.ChainLength}");
            var quest = Progression.CurrentQuest(state, catalog);
            if (quest?.Id != questId)
            {
                int index = questId != null ? IndexOf(definition, questId) : -1;
                if (index < 0)
                    Refuse(SaveError.QuestMismatch, questId == null
                        ? $"progress: the saved game had no quest left, this build has \"{quest?.Id}\" at {progress.QuestIndex + 1}"
                        : $"progress.questId: no quest \"{questId}\" in this build's chain");
                progress.QuestIndex = index;
                quest = Progression.CurrentQuest(state, catalog);
                if (quest?.Id != questId)
                    Refuse(SaveError.QuestMismatch, $"progress.questId: \"{questId}\" does not stand at {index + 1}");
            }
            int goals = quest?.Goals.Count ?? 0;
            if (progress.GoalDone.Count != goals || progress.GoalBaseline.Count != goals)
                Refuse(SaveError.QuestMismatch,
                    $"progress: \"{questId}\" has {goals} goals in this build, the save {progress.GoalDone.Count}");
        }

        private static int IndexOf(ProgressionDefinition definition, string questId)
        {
            var chain = definition.Quests;
            for (int i = 0; i < chain.Count; i++)
                if (chain[i] != null && chain[i].Id == questId) return i;
            // a repeatable quest is "<template>#<cycle from 1>"
            int hash = questId.LastIndexOf('#');
            if (hash <= 0 || !int.TryParse(questId.Substring(hash + 1), out int cycle) || cycle < 1) return -1;
            string template = questId.Substring(0, hash);
            var repeatable = definition.RepeatableQuests;
            for (int r = 0; r < repeatable.Count; r++)
                if (repeatable[r] != null && repeatable[r].Id == template)
                {
                    long index = chain.Count + (long)(cycle - 1) * repeatable.Count + r;
                    return index <= int.MaxValue ? (int)index : -1;
                }
            return -1;
        }

        private static void CheckAssignment(Assignment a, string path, Dictionary<string, BuildingState> buildings)
        {
            switch (a.Kind)
            {
                case AssignmentKind.ToWork:
                case AssignmentKind.Work:
                    if (a.BuildingId == null) Refuse(SaveError.DanglingReference, $"{path}.building: a worker without a workplace");
                    break;
                case AssignmentKind.Haul:
                    if (a.SourceId == null || a.DestinationId == null)
                        Refuse(SaveError.DanglingReference, $"{path}: a hauler without its route");
                    break;
            }
            // the colony leaves no id behind when a building goes (DemolishBuilding frees its creatures)
            Require(a.BuildingId, path + ".building", buildings);
            Require(a.SourceId, path + ".source", buildings);
            Require(a.DestinationId, path + ".destination", buildings);
            if (a.Carried < 0 || a.CrowdSlot < -1 || a.QueueTicket < 0)
                Refuse(SaveError.InvalidValue, $"{path}: carried {a.Carried}, crowd slot {a.CrowdSlot}, ticket {a.QueueTicket}");
        }

        private static void Require(string buildingId, string path, Dictionary<string, BuildingState> buildings)
        {
            if (buildingId != null && !buildings.ContainsKey(buildingId))
                Refuse(SaveError.DanglingReference, $"{path}: no building \"{buildingId}\"");
        }

        private static void CheckLand(GameState state, GameContentCatalog catalog)
        {
            var expected = LandRules.CreateStart(catalog.Economy);
            var land = state.Land;
            if ((expected == null) != (land == null))
                Refuse(SaveError.LandMismatch, land == null
                    ? "the save has no land, this build sells it"
                    : "the save has land, this build has none");
            if (land == null) return;
            if (land.BlocksPerSide != expected.BlocksPerSide || land.BlockSize != expected.BlockSize)
                Refuse(SaveError.LandMismatch,
                    $"land: {land.BlocksPerSide}² blocks of {land.BlockSize}, this build {expected.BlocksPerSide}² of {expected.BlockSize}");
        }

        private static void Refuse(SaveError error, string detail) => throw new SaveRejection(error, detail);
    }
}
