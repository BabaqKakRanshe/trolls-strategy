using System;
using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>A building the colony starts with, authored in the scene.</summary>
    public readonly struct StartingBuilding
    {
        public StartingBuilding(BuildingKind kind, Cell cell)
        {
            Kind = kind;
            Cell = cell;
        }

        public BuildingKind Kind { get; }
        public Cell Cell { get; }
    }

    public class GameSession
    {
        private readonly GameContentCatalog _catalog;
        private GameState _state;
        private int _revision;
        private float _remainderSeconds;
        private bool _debugBattleAccess;

        public event Action<GameSnapshot> OnSnapshotChanged;
        /// <summary>Every dispatched command with its result, accepted or refused, after the state committed.</summary>
        public event Action<IGameCommand, CommandResult> OnCommandResolved;

        /// <summary>
        /// Starts a colony with the given buildings. Throws when the layout breaks placement rules;
        /// StartingBuildingIds lists the created ids in layout order.
        /// </summary>
        public GameSession(GameContentCatalog catalog, IReadOnlyList<StartingBuilding> startingBuildings)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (startingBuildings == null) throw new ArgumentNullException(nameof(startingBuildings));
            _state = GameState.CreateInitialState(_catalog.Economy.StartingGold);
            var ids = new string[startingBuildings.Count];
            for (int i = 0; i < startingBuildings.Count; i++)
            {
                var start = startingBuildings[i];
                var placed = ColonySimulation.PlaceStartingBuilding(_state, start.Kind, start.Cell, _catalog, out ids[i]);
                if (!placed.Ok)
                    throw new InvalidOperationException(
                        $"Starting {start.Kind} at ({start.Cell.X}, {start.Cell.Y}): {placed.Error}");
            }
            StartingBuildingIds = ids;
            foreach (var definition in _catalog.Equipment)
            {
                if (definition == null) continue;
                for (int i = 0; i < definition.StartingQuantity; i++)
                    _state.Equipment.Add(new EquipmentState
                    {
                        Id = $"item-{_state.Equipment.Count + 1:D3}",
                        DefinitionId = definition.ItemId
                    });
            }
            _revision = 1;
        }

        public IReadOnlyList<string> StartingBuildingIds { get; }
        public GameSnapshot CurrentSnapshot => CreateSnapshot();
        public GameContentCatalog Catalog => _catalog;
        public BattleRunState ActiveBattle => _state.ActiveBattle;
        public int ActiveTimeMs => _state.ActiveTimeMs;
        public int FirstMissionWins => _state.FirstMissionWins;

#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
        public void EnableDebugBattleAccess() => _debugBattleAccess = true;
#endif

        public CommandResult CanEnterMission(string missionId)
        {
            foreach (var mission in _catalog.Missions)
                if (mission != null && mission.MissionId == missionId)
                    return BattleApplication.ValidateAvailability(_state, mission, _debugBattleAccess);
            return CommandResult.Fail("Миссия не найдена");
        }

        /// <summary>Active colony time left before the mission opens or recovers; 0 when time does not hold it back.</summary>
        public int MissionWaitMs(string missionId)
        {
            foreach (var mission in _catalog.Missions)
                if (mission != null && mission.MissionId == missionId)
                    return BattleApplication.WaitMs(_state, mission);
            return 0;
        }

        public CommandResult Dispatch(IGameCommand command)
        {
            if (command == null)
                return CommandResult.Fail("Команда не задана");
            if (_state.ActiveBattle != null && command is not AcknowledgeBattleCommand)
                return CommandResult.Fail("Сначала завершите текущий бой");

            var candidate = _state.Clone();
            var result = command switch
            {
                StartBattleCommand start => BattleApplication.Start(candidate, start, _catalog,
                    _debugBattleAccess),
                AcknowledgeBattleCommand => BattleApplication.Acknowledge(candidate),
                _ => ColonySimulation.ApplyCommand(candidate, command, _catalog)
            };
            if (result.Ok)
            {
                _state = candidate;
                _revision++;
                Emit();
            }
            OnCommandResolved?.Invoke(command, result);
            return result;
        }

        public GameSnapshot Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
                return CurrentSnapshot;
            if (_state.ActiveBattle != null)
                return CurrentSnapshot;

            float step = _catalog.Economy.EconomyStepSeconds;
            if (step <= 0f)
                throw new InvalidOperationException("Шаг экономики должен быть больше нуля");

            _remainderSeconds += deltaSeconds;
            bool changed = false;

            while (_remainderSeconds >= step)
            {
                ColonySimulation.TickColony(_state, step, _catalog);
                _state.ActiveTimeMs += (int)Math.Round(step * 1000f);
                _remainderSeconds -= step;
                changed = true;
            }

            if (changed)
            {
                _revision++;
                Emit();
            }

            return CurrentSnapshot;
        }

        public Cell FindSpawnCell()
        {
            var barracks = _state.Buildings.Find(b => b.Kind == BuildingKind.Barracks);
            if (barracks != null)
            {
                var def = _catalog.GetBuilding(BuildingKind.Barracks);
                int x = barracks.Cell.X + def.Width / 2;
                int y = barracks.Cell.Y + def.Height;
                var candidate = new Cell(x, y);
                if (CanBuyUnits(UnitKind.Goblin, 1, candidate).Ok)
                    return candidate;
            }

            for (int r = 0; r < 6; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var candidate = new Cell(3 + dx, 3 + dy);
                        if (CanBuyUnits(UnitKind.Goblin, 1, candidate).Ok)
                            return candidate;
                    }
                }
            }

            return new Cell(3, 3);
        }

        public CommandResult CanPlaceBuilding(BuildingKind kind, Cell cell, string ignoredId = null) =>
            ColonySimulation.ValidateBuildingPlacement(_state, kind, cell, _catalog, ignoredId);

        public Cell? FindFirstBuildingCell(BuildingKind kind) =>
            ColonySimulation.FindFirstValidBuildingCell(_state, kind, _catalog);

        public CommandResult CanBuyUnits(UnitKind kind, int amount, Cell cell)
        {
            return ColonySimulation.ValidateUnitPurchase(_state, kind, amount, cell, _catalog);
        }

        public GameSnapshot CreateSnapshot()
        {
            var buildingSnapshots = new List<BuildingSnapshot>(_state.Buildings.Count);
            int totalOreInBuildings = 0;

            for (int i = 0; i < _state.Buildings.Count; i++)
            {
                var b = _state.Buildings[i];
                var def = _catalog.GetBuilding(b.Kind);
                totalOreInBuildings += b.GetStock(ResourceKind.IronOre);

                int workers = 0;
                int goblinHaulers = 0, trollHaulers = 0;
                for (int u = 0; u < _state.Units.Count; u++)
                {
                    var unit = _state.Units[u];
                    var a = unit.Assignment;
                    if ((a.Kind == AssignmentKind.ToWork || a.Kind == AssignmentKind.Work) && a.BuildingId == b.Id)
                        workers++;
                    if (a.Kind == AssignmentKind.Haul && (a.SourceId == b.Id || a.DestinationId == b.Id))
                    {
                        if (unit.Kind == UnitKind.Goblin) goblinHaulers++;
                        else if (unit.Kind == UnitKind.Troll) trollHaulers++;
                    }
                }

                buildingSnapshots.Add(new BuildingSnapshot(
                    b.Id,
                    b.Kind,
                    b.Kind == BuildingKind.Mine || GetIdNumber(b.Id) > 1 ? $"{def.DisplayName} {GetIdNumber(b.Id)}" : def.DisplayName,
                    b.Cell,
                    def.Width,
                    def.Height,
                    b.TotalStock,
                    def.Capacity(b.Level),
                    workers,
                    def.WorkerCapacity(b.Level),
                    ColonySimulation.ProductionPerSecond(_state, b.Id, _catalog),
                    b.ProductionProgress,
                    b.Level,
                    b.InvestedGold / 2,
                    def.UpgradeCost(b.Level),
                    def.SaleBonus(b.Level),
                    goblinHaulers,
                    trollHaulers,
                    def.SlotStackSize,
                    def.StorageRole == StorageRole.Stockpile
                        ? StorageSlots.Fill(b.Stock, def.Capacity(b.Level), def.SlotStackSize)
                        : null,
                    StockOf(b),
                    def.IsWorkplace,
                    ColonySimulation.DescribeProduction(_state, b, _catalog),
                    DescribeRecipes(def)));
            }

            var unitSnapshots = new List<UnitSnapshot>(_state.Units.Count);
            int carriedOre = 0;

            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                var def = _catalog.GetUnit(u.Kind);
                if (u.Assignment.Kind == AssignmentKind.Haul && u.Assignment.CarriedResource == ResourceKind.IronOre)
                    carriedOre += u.Assignment.Carried;

                unitSnapshots.Add(new UnitSnapshot(
                    u.Id,
                    GetIdNumber(u.Id),
                    u.Kind,
                    def.DisplayName,
                    def.Strength,
                    def.Speed,
                    def.Stamina,
                    u.Position,
                    u.Assignment,
                    FormatAssignmentStatus(u.Assignment, buildingSnapshots),
                    ColonySimulation.UnitMovementSpeed(def, _catalog)));
            }

            var equipmentSnapshots = new List<EquipmentSnapshot>(_state.Equipment.Count);
            foreach (var item in _state.Equipment)
                equipmentSnapshots.Add(new EquipmentSnapshot(item.Id,
                    _catalog.GetEquipment(item.DefinitionId), item.OwnerUnitId));

            return new GameSnapshot(
                _revision,
                _state.Gold,
                _state.SoldGoods,
                totalOreInBuildings + carriedOre,
                buildingSnapshots,
                unitSnapshots,
                equipmentSnapshots);
        }

        private void Emit()
        {
            OnSnapshotChanged?.Invoke(CurrentSnapshot);
        }

        public string ResourceName(ResourceKind resource) =>
            _catalog.TryGetResource(resource)?.DisplayName ?? resource.ToString();

        private List<ResourceStack> StockOf(BuildingState building)
        {
            var stock = new List<ResourceStack>();
            foreach (ResourceKind resource in Enum.GetValues(typeof(ResourceKind)))
            {
                int amount = building.GetStock(resource);
                if (amount > 0) stock.Add(new ResourceStack(resource, ResourceName(resource), amount));
            }
            return stock;
        }

        /// <summary>The building's recipes as the player reads them, one per line; empty for non-producers.</summary>
        public string DescribeRecipes(BuildingDefinition definition)
        {
            var lines = new List<string>();
            foreach (var recipe in definition.Recipes)
            {
                string line = (recipe.Inputs.Length > 0 ? FormatAmounts(recipe.Inputs) + " → " : "") +
                    FormatAmounts(recipe.Outputs);
                if (recipe.HasBonus)
                    line += $" (+{recipe.BonusOutput.Amount} {ResourceName(recipe.BonusOutput.Resource)} каждые {recipe.BonusEveryCycles} циклов)";
                lines.Add(line);
            }
            return string.Join("\n", lines);
        }

        private string FormatAmounts(ResourceAmount[] amounts)
        {
            var parts = new List<string>(amounts.Length);
            foreach (var amount in amounts)
                parts.Add($"{amount.Amount} {ResourceName(amount.Resource)}");
            return string.Join(" + ", parts);
        }

        private static int GetIdNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return 1;
            int dash = id.LastIndexOf('-');
            if (dash >= 0 && int.TryParse(id.Substring(dash + 1), out int num))
                return num;
            return 1;
        }

        private static string FormatAssignmentStatus(Assignment assignment, List<BuildingSnapshot> buildings)
        {
            if (assignment.Kind == AssignmentKind.Idle) return "Свободен";
            if (assignment.Kind == AssignmentKind.ToWork)
            {
                var target = buildings.Find(b => b.Id == assignment.BuildingId);
                return $"Идёт на работу: {target?.Name ?? "производство"}";
            }
            if (assignment.Kind == AssignmentKind.Work)
            {
                var target = buildings.Find(b => b.Id == assignment.BuildingId);
                return $"Работает: {target?.Name ?? "производство"}";
            }
            var source = buildings.Find(b => b.Id == assignment.SourceId)?.Name ?? "источник";
            var dest = buildings.Find(b => b.Id == assignment.DestinationId)?.Name ?? "цель";
            return $"Несёт: {source} -> {dest}";
        }
    }
}
