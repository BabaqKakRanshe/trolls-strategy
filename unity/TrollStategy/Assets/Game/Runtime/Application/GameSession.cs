using System;
using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    public class GameSession
    {
        private readonly GameContentCatalog _catalog;
        private GameState _state;
        private int _revision;
        private float _remainderSeconds;

        public event Action<GameSnapshot> OnSnapshotChanged;

        public GameSession(GameContentCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _state = GameState.CreateInitialState(_catalog.Economy.StartingGold);
            _revision = 1;
        }

        public GameSnapshot CurrentSnapshot => CreateSnapshot();
        public GameContentCatalog Catalog => _catalog;

        public CommandResult Dispatch(IGameCommand command)
        {
            if (command == null)
                return CommandResult.Fail("Команда не задана");

            var candidate = _state.Clone();
            var result = ColonySimulation.ApplyCommand(candidate, command, _catalog);
            if (result.Ok)
            {
                _state = candidate;
                _revision++;
                Emit();
            }
            return result;
        }

        public GameSnapshot Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
                return CurrentSnapshot;

            float step = _catalog.Economy.EconomyStepSeconds;
            if (step <= 0f)
                throw new InvalidOperationException("Шаг экономики должен быть больше нуля");

            _remainderSeconds += deltaSeconds;
            bool changed = false;

            while (_remainderSeconds >= step)
            {
                ColonySimulation.TickColony(_state, step, _catalog);
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

        public Cell? FindFirstMineCell()
        {
            return ColonySimulation.FindFirstValidMineCell(_state, _catalog);
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

        public CommandResult CanBuildMine(Cell cell)
        {
            return ColonySimulation.ValidateMinePlacement(_state, cell, _catalog);
        }

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
                totalOreInBuildings += b.Ore;

                int workers = 0;
                for (int u = 0; u < _state.Units.Count; u++)
                {
                    var a = _state.Units[u].Assignment;
                    if ((a.Kind == AssignmentKind.ToWork || a.Kind == AssignmentKind.Work) && a.BuildingId == b.Id)
                        workers++;
                }

                buildingSnapshots.Add(new BuildingSnapshot(
                    b.Id,
                    b.Kind,
                    b.Kind == BuildingKind.Mine ? $"Шахта {GetIdNumber(b.Id)}" : def.DisplayName,
                    b.Cell,
                    def.Width,
                    def.Height,
                    b.Ore,
                    def.MaxOre,
                    workers,
                    def.MaxWorkers,
                    ColonySimulation.ProductionPerSecond(_state, b.Id, _catalog),
                    b.ProductionProgress));
            }

            var unitSnapshots = new List<UnitSnapshot>(_state.Units.Count);
            int carriedOre = 0;

            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                var def = _catalog.GetUnit(u.Kind);
                if (u.Assignment.Kind == AssignmentKind.Haul)
                    carriedOre += u.Assignment.Carried;

                unitSnapshots.Add(new UnitSnapshot(
                    u.Id,
                    GetIdNumber(u.Id),
                    u.Kind,
                    def.DisplayName,
                    def.Strength,
                    def.Speed,
                    def.CargoCapacity,
                    u.Position,
                    u.Assignment,
                    FormatAssignmentStatus(u.Assignment, buildingSnapshots),
                    ColonySimulation.UnitMovementSpeed(def, _catalog)));
            }

            return new GameSnapshot(
                _revision,
                _state.Gold,
                _state.SoldOre,
                totalOreInBuildings + carriedOre,
                buildingSnapshots,
                unitSnapshots);
        }

        private void Emit()
        {
            OnSnapshotChanged?.Invoke(CurrentSnapshot);
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
                return $"Идёт в {target?.Name ?? "шахту"}";
            }
            if (assignment.Kind == AssignmentKind.Work)
            {
                var target = buildings.Find(b => b.Id == assignment.BuildingId);
                return $"В шахте: {target?.Name ?? "шахта"}";
            }
            var source = buildings.Find(b => b.Id == assignment.SourceId)?.Name ?? "источник";
            var dest = buildings.Find(b => b.Id == assignment.DestinationId)?.Name ?? "цель";
            return $"Несёт: {source} -> {dest}";
        }
    }
}
