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
            _catalog = catalog;
            _state = GameState.CreateInitialState();
            _revision = 1;
        }

        public GameSnapshot CurrentSnapshot => CreateSnapshot();
        public GameState InternalState => _state;

        public CommandResult Dispatch(IGameCommand command)
        {
            var result = ColonySimulation.ApplyCommand(_state, command, _catalog);
            if (result.Ok)
            {
                _revision++;
                Emit();
            }
            return result;
        }

        public GameSnapshot Advance(float deltaSeconds)
        {
            _remainderSeconds += deltaSeconds;
            float step = _catalog.Economy.EconomyStepSeconds;
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

                buildingSnapshots.Add(new BuildingSnapshot
                {
                    Id = b.Id,
                    Kind = b.Kind,
                    Name = b.Kind == BuildingKind.Mine ? $"Шахта {GetIdNumber(b.Id)}" : def.DisplayName,
                    Cell = b.Cell,
                    Width = def.Width,
                    Height = def.Height,
                    Ore = b.Ore,
                    MaxOre = def.MaxOre,
                    WorkerCount = workers,
                    MaxWorkers = def.MaxWorkers,
                    ProductionPerSecond = ColonySimulation.ProductionPerSecond(_state, b.Id, _catalog)
                });
            }

            var unitSnapshots = new List<UnitSnapshot>(_state.Units.Count);
            int carriedOre = 0;

            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                var def = _catalog.GetUnit(u.Kind);
                if (u.Assignment.Kind == AssignmentKind.Haul)
                    carriedOre += u.Assignment.Carried;

                unitSnapshots.Add(new UnitSnapshot
                {
                    Id = u.Id,
                    Number = GetIdNumber(u.Id),
                    UnitKind = u.Kind,
                    Name = def.DisplayName,
                    Strength = def.Strength,
                    Speed = def.Speed,
                    CargoCapacity = def.CargoCapacity,
                    Position = u.Position,
                    Assignment = u.Assignment.Clone(),
                    Status = FormatAssignmentStatus(u.Assignment, buildingSnapshots)
                });
            }

            return new GameSnapshot
            {
                Revision = _revision,
                Gold = _state.Gold,
                SoldOre = _state.SoldOre,
                TotalOre = totalOreInBuildings + carriedOre,
                Buildings = buildingSnapshots,
                Units = unitSnapshots
            };
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
