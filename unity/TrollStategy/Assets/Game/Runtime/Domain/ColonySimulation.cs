using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    public static class ColonySimulation
    {
        public static CommandResult ApplyCommand(GameState state, IGameCommand command, GameContentCatalog catalog)
        {
            return command switch
            {
                BuildMineCommand c => BuildMine(state, c.Cell, catalog),
                BuyUnitsCommand c => BuyUnits(state, c.UnitKind, c.Amount, c.Cell, catalog),
                AssignWorkCommand c => AssignWork(state, c.UnitIds, c.BuildingId, catalog),
                AssignHaulCommand c => AssignHaul(state, c.UnitIds, c.SourceId, c.DestinationId, catalog),
                ReleaseUnitsCommand c => ReleaseUnits(state, c.UnitIds, catalog),
                SellUnitsCommand c => SellUnits(state, c.UnitIds, catalog),
                SendToBarracksCommand c => SendToBarracks(state, c.UnitIds, catalog),
                _ => CommandResult.Fail("Неизвестная команда")
            };
        }

        public static void TickColony(GameState state, float deltaSeconds, GameContentCatalog catalog)
        {
            ProduceOre(state, deltaSeconds, catalog);

            for (int i = 0; i < state.Units.Count; i++)
            {
                var unit = state.Units[i];
                if (unit.Assignment.Kind == AssignmentKind.ToWork)
                    TickWorkerArrival(state, unit, deltaSeconds, catalog);
                else if (unit.Assignment.Kind == AssignmentKind.Haul)
                    TickHauler(state, unit, deltaSeconds, catalog);
            }
        }

        public static CommandResult ValidateMinePlacement(GameState state, Cell cell, GameContentCatalog catalog)
        {
            var mine = catalog.GetBuilding(BuildingKind.Mine);
            var economy = catalog.Economy;

            if (cell.X < 0 || cell.Y < 0 || cell.X + mine.Width > economy.GridWidth || cell.Y + mine.Height > economy.GridHeight)
                return CommandResult.Fail("Шахта выходит за границу поля");

            for (int i = 0; i < state.Buildings.Count; i++)
            {
                var b = state.Buildings[i];
                var bDef = catalog.GetBuilding(b.Kind);
                if (FootprintsOverlap(cell, mine.Width, mine.Height, b.Cell, bDef.Width, bDef.Height))
                    return CommandResult.Fail("Здесь уже стоит здание");
            }

            return CommandResult.Success();
        }

        public static CommandResult ValidateUnitPurchase(GameState state, UnitKind kind, int amount, Cell cell, GameContentCatalog catalog)
        {
            var economy = catalog.Economy;
            var unitDef = catalog.GetUnit(kind);

            if (amount < 1 || amount > economy.MaxUnitsPerCell)
                return CommandResult.Fail($"Количество должно быть от 1 до {economy.MaxUnitsPerCell}");

            if (cell.X < 0 || cell.Y < 0 || cell.X >= economy.GridWidth || cell.Y >= economy.GridHeight)
                return CommandResult.Fail("Клетка находится за границей поля");

            for (int i = 0; i < state.Buildings.Count; i++)
            {
                var b = state.Buildings[i];
                var bDef = catalog.GetBuilding(b.Kind);
                if (BuildingOccupiesCell(b.Cell, bDef.Width, bDef.Height, cell))
                    return CommandResult.Fail("Эта клетка занята постройкой");
            }

            int existingUnitsInCell = CountUnitsInCell(state, cell, economy);
            if (existingUnitsInCell + amount > economy.MaxUnitsPerCell)
                return CommandResult.Fail($"В одной клетке помещается не больше {economy.MaxUnitsPerCell} существ");

            int totalPrice = unitDef.Price * amount;
            if (state.Gold < totalPrice)
                return CommandResult.Fail("Недостаточно золота");

            return CommandResult.Success();
        }

        public static int CountUnitsInCell(GameState state, Cell cell, EconomyConfig economy)
        {
            float cs = economy.CellSize;
            int count = 0;
            for (int i = 0; i < state.Units.Count; i++)
            {
                var pos = state.Units[i].Position;
                int cx = (int)Math.Floor(pos.X / cs);
                int cy = (int)Math.Floor(pos.Y / cs);
                if (cx == cell.X && cy == cell.Y) count++;
            }
            return count;
        }

        public static bool FootprintsOverlap(Cell c1, int w1, int h1, Cell c2, int w2, int h2)
        {
            return c1.X < c2.X + w2 &&
                   c1.X + w1 > c2.X &&
                   c1.Y < c2.Y + h2 &&
                   c1.Y + h1 > c2.Y;
        }

        public static bool BuildingOccupiesCell(Cell bCell, int w, int h, Cell testCell)
        {
            return testCell.X >= bCell.X && testCell.X < bCell.X + w &&
                   testCell.Y >= bCell.Y && testCell.Y < bCell.Y + h;
        }

        public static bool IsValidHaulRoute(BuildingKind source, BuildingKind destination)
        {
            return (source == BuildingKind.Mine && (destination == BuildingKind.Warehouse || destination == BuildingKind.Market)) ||
                   (source == BuildingKind.Warehouse && destination == BuildingKind.Market);
        }

        private static CommandResult BuildMine(GameState state, Cell cell, GameContentCatalog catalog)
        {
            var validation = ValidateMinePlacement(state, cell, catalog);
            if (!validation.Ok) return validation;

            var mineDef = catalog.GetBuilding(BuildingKind.Mine);
            if (state.Gold < mineDef.Price) return CommandResult.Fail("Недостаточно золота");

            state.Gold -= mineDef.Price;
            string newId = $"mine-{state.NextBuildingId++}";
            state.Buildings.Add(new BuildingState
            {
                Id = newId,
                Kind = BuildingKind.Mine,
                Cell = cell,
                Ore = 0,
                ProductionProgress = 0f
            });

            return CommandResult.Success();
        }

        private static CommandResult BuyUnits(GameState state, UnitKind kind, int amount, Cell cell, GameContentCatalog catalog)
        {
            var validation = ValidateUnitPurchase(state, kind, amount, cell, catalog);
            if (!validation.Ok) return validation;

            var unitDef = catalog.GetUnit(kind);
            int totalPrice = unitDef.Price * amount;
            state.Gold -= totalPrice;

            var economy = catalog.Economy;
            int existingInCell = CountUnitsInCell(state, cell, economy);

            for (int i = 0; i < amount; i++)
            {
                string uId = $"unit-{state.NextUnitId++}";
                var pos = CrowdPosition(cell, existingInCell + i, economy);
                state.Units.Add(new UnitState
                {
                    Id = uId,
                    Kind = kind,
                    Position = pos,
                    Assignment = Assignment.Idle()
                });
            }

            return CommandResult.Success();
        }

        private static CommandResult AssignWork(GameState state, IReadOnlyList<string> unitIds, string buildingId, GameContentCatalog catalog)
        {
            if (unitIds == null || unitIds.Count == 0) return CommandResult.Fail("Сначала выберите юнитов");

            var building = state.Buildings.Find(b => b.Id == buildingId);
            if (building == null || building.Kind != BuildingKind.Mine)
                return CommandResult.Fail("Работать можно только в шахте");

            var mineDef = catalog.GetBuilding(BuildingKind.Mine);

            int currentAssigned = 0;
            for (int i = 0; i < state.Units.Count; i++)
            {
                var a = state.Units[i].Assignment;
                if ((a.Kind == AssignmentKind.ToWork || a.Kind == AssignmentKind.Work) && a.BuildingId == buildingId)
                    currentAssigned++;
            }

            int availableSlots = mineDef.MaxWorkers - currentAssigned;
            if (availableSlots <= 0)
                return CommandResult.Fail("В шахте уже максимальное число рабочих");

            int assigned = 0;
            for (int i = 0; i < unitIds.Count && assigned < availableSlots; i++)
            {
                var u = state.Units.Find(un => un.Id == unitIds[i]);
                if (u == null) continue;

                if ((u.Assignment.Kind == AssignmentKind.ToWork || u.Assignment.Kind == AssignmentKind.Work) && u.Assignment.BuildingId == buildingId)
                    continue;

                ReturnCarriedOre(state, u);
                u.Assignment = Assignment.ToWork(buildingId);
                assigned++;
            }

            return CommandResult.Success();
        }

        private static CommandResult AssignHaul(GameState state, IReadOnlyList<string> unitIds, string sourceId, string destinationId, GameContentCatalog catalog)
        {
            if (unitIds == null || unitIds.Count == 0) return CommandResult.Fail("Сначала выберите юнитов");

            var source = state.Buildings.Find(b => b.Id == sourceId);
            var destination = state.Buildings.Find(b => b.Id == destinationId);
            if (source == null || destination == null) return CommandResult.Fail("Здание не найдено");

            if (!IsValidHaulRoute(source.Kind, destination.Kind))
                return CommandResult.Fail("Этот маршрут не перевозит руду");

            for (int i = 0; i < unitIds.Count; i++)
            {
                var u = state.Units.Find(un => un.Id == unitIds[i]);
                if (u == null) continue;

                ReturnCarriedOre(state, u);
                u.Assignment = Assignment.Haul(sourceId, destinationId);
            }

            return CommandResult.Success();
        }

        private static CommandResult ReleaseUnits(GameState state, IReadOnlyList<string> unitIds, GameContentCatalog catalog)
        {
            if (unitIds == null || unitIds.Count == 0) return CommandResult.Fail("Сначала выберите юнитов");

            for (int i = 0; i < unitIds.Count; i++)
            {
                var u = state.Units.Find(un => un.Id == unitIds[i]);
                if (u == null) continue;

                ReturnCarriedOre(state, u);
                u.Assignment = Assignment.Idle();
                u.Position = IdlePosition(GetUnitNumber(u.Id), catalog.Economy);
            }

            return CommandResult.Success();
        }

        private static CommandResult SellUnits(GameState state, IReadOnlyList<string> unitIds, GameContentCatalog catalog)
        {
            if (unitIds == null || unitIds.Count == 0) return CommandResult.Fail("Сначала выберите юнитов");

            int totalRefund = 0;
            var toRemove = new HashSet<string>();

            for (int i = 0; i < unitIds.Count; i++)
            {
                var id = unitIds[i];
                var u = state.Units.Find(un => un.Id == id);
                if (u != null)
                {
                    ReturnCarriedOre(state, u);
                    var def = catalog.GetUnit(u.Kind);
                    int refund = (int)Math.Floor(def.Price * 0.5f);
                    totalRefund += refund;
                    toRemove.Add(id);
                }
            }

            state.Units.RemoveAll(u => toRemove.Contains(u.Id));
            state.Gold += totalRefund;
            return CommandResult.Success();
        }

        private static CommandResult SendToBarracks(GameState state, IReadOnlyList<string> unitIds, GameContentCatalog catalog)
        {
            if (unitIds == null || unitIds.Count == 0) return CommandResult.Fail("Сначала выберите юнитов");

            var barracks = state.Buildings.Find(b => b.Kind == BuildingKind.Barracks);
            var targetPos = barracks != null 
                ? BuildingEntrancePosition(barracks, catalog) 
                : new WorldPosition(2.5f * catalog.Economy.CellSize, 2.5f * catalog.Economy.CellSize);

            for (int i = 0; i < unitIds.Count; i++)
            {
                var id = unitIds[i];
                var u = state.Units.Find(un => un.Id == id);
                if (u != null)
                {
                    ReturnCarriedOre(state, u);
                    u.Assignment = Assignment.Idle();
                    u.Position = targetPos;
                }
            }

            return CommandResult.Success();
        }

        private static void ProduceOre(GameState state, float deltaSeconds, GameContentCatalog catalog)
        {
            var mineDef = catalog.GetBuilding(BuildingKind.Mine);

            for (int i = 0; i < state.Buildings.Count; i++)
            {
                var b = state.Buildings[i];
                if (b.Kind != BuildingKind.Mine) continue;

                float prodPerSec = CalculateMineProductionPerSecond(state, b.Id, catalog);
                b.ProductionProgress += prodPerSec * deltaSeconds;

                int produced = (int)Math.Floor(b.ProductionProgress);
                if (produced > 0)
                {
                    int room = mineDef.MaxOre - b.Ore;
                    int added = Math.Min(room, produced);
                    b.Ore += added;
                    b.ProductionProgress = b.Ore >= mineDef.MaxOre ? 0f : b.ProductionProgress - produced;
                }
            }
        }

        private static float CalculateMineProductionPerSecond(GameState state, string buildingId, GameContentCatalog catalog)
        {
            float total = 0f;
            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u.Assignment.Kind == AssignmentKind.Work && u.Assignment.BuildingId == buildingId)
                {
                    var uDef = catalog.GetUnit(u.Kind);
                    total += uDef.Strength * catalog.Economy.OrePerStrengthSecond;
                }
            }
            return total;
        }

        private static void TickWorkerArrival(GameState state, UnitState unit, float deltaSeconds, GameContentCatalog catalog)
        {
            var building = state.Buildings.Find(b => b.Id == unit.Assignment.BuildingId);
            if (building == null) return;

            var targetPos = BuildingEntrancePosition(building, catalog);
            var unitDef = catalog.GetUnit(unit.Kind);
            float speedInWorldUnits = (120f + unitDef.Speed * 12f) / 48f * catalog.Economy.CellSize;

            if (MoveToward(unit, targetPos, speedInWorldUnits, deltaSeconds))
            {
                unit.Assignment = Assignment.Work(building.Id);
            }
        }

        private static void TickHauler(GameState state, UnitState unit, float deltaSeconds, GameContentCatalog catalog)
        {
            var source = state.Buildings.Find(b => b.Id == unit.Assignment.SourceId);
            var destination = state.Buildings.Find(b => b.Id == unit.Assignment.DestinationId);
            if (source == null || destination == null) return;

            var unitDef = catalog.GetUnit(unit.Kind);
            float speedInWorldUnits = (120f + unitDef.Speed * 12f) / 48f * catalog.Economy.CellSize;
            var assignment = unit.Assignment;

            switch (assignment.Phase)
            {
                case HaulPhase.ToSource:
                {
                    var accessPos = BuildingAccessPosition(source, unit.Id, catalog);
                    if (MoveToward(unit, accessPos, speedInWorldUnits, deltaSeconds))
                    {
                        assignment.Phase = HaulPhase.Loading;
                        assignment.PhaseElapsedSeconds = 0f;
                    }
                    break;
                }
                case HaulPhase.Loading:
                {
                    assignment.PhaseElapsedSeconds += deltaSeconds;
                    float transferTime = catalog.Economy.TransferTimeSeconds;
                    if (assignment.PhaseElapsedSeconds >= transferTime)
                    {
                        int taken = Math.Min(source.Ore, unitDef.CargoCapacity);
                        source.Ore -= taken;
                        assignment.Carried = taken;
                        assignment.Phase = HaulPhase.ToDestination;
                        assignment.PhaseElapsedSeconds = 0f;
                    }
                    break;
                }
                case HaulPhase.ToDestination:
                {
                    var accessPos = BuildingAccessPosition(destination, unit.Id, catalog);
                    if (MoveToward(unit, accessPos, speedInWorldUnits, deltaSeconds))
                    {
                        assignment.Phase = HaulPhase.Unloading;
                        assignment.PhaseElapsedSeconds = 0f;
                    }
                    break;
                }
                case HaulPhase.Unloading:
                {
                    assignment.PhaseElapsedSeconds += deltaSeconds;
                    float transferTime = catalog.Economy.TransferTimeSeconds;
                    if (assignment.PhaseElapsedSeconds >= transferTime)
                    {
                        if (destination.Kind == BuildingKind.Warehouse)
                        {
                            var whDef = catalog.GetBuilding(BuildingKind.Warehouse);
                            int room = whDef.MaxOre - destination.Ore;
                            int deposited = Math.Min(room, assignment.Carried);
                            destination.Ore += deposited;
                            assignment.Carried -= deposited;
                        }
                        else if (destination.Kind == BuildingKind.Market)
                        {
                            int sold = assignment.Carried;
                            state.SoldOre += sold;
                            state.Gold += sold * catalog.Economy.OreSellPrice;
                            assignment.Carried = 0;
                        }

                        assignment.Phase = HaulPhase.ToSource;
                        assignment.PhaseElapsedSeconds = 0f;
                    }
                    break;
                }
            }
        }

        private static bool MoveToward(UnitState unit, WorldPosition target, float speed, float deltaSeconds)
        {
            float dx = target.X - unit.Position.X;
            float dy = target.Y - unit.Position.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            float step = speed * deltaSeconds;
            if (dist <= step || dist < 0.001f)
            {
                unit.Position = target;
                return true;
            }

            unit.Position = new WorldPosition(
                unit.Position.X + (dx / dist) * step,
                unit.Position.Y + (dy / dist) * step
            );
            return false;
        }

        private static void ReturnCarriedOre(GameState state, UnitState unit)
        {
            if (unit.Assignment.Carried > 0)
            {
                var warehouse = state.Buildings.Find(b => b.Kind == BuildingKind.Warehouse);
                if (warehouse != null)
                {
                    warehouse.Ore += unit.Assignment.Carried;
                }
                unit.Assignment.Carried = 0;
            }
        }

        public static WorldPosition BuildingEntrancePosition(BuildingState building, GameContentCatalog catalog)
        {
            var def = catalog.GetBuilding(building.Kind);
            float cs = catalog.Economy.CellSize;
            return new WorldPosition(
                (building.Cell.X + def.Width * 0.5f) * cs,
                (building.Cell.Y + def.Height - 0.35f) * cs
            );
        }

        public static WorldPosition BuildingAccessPosition(BuildingState building, string unitId, GameContentCatalog catalog)
        {
            var def = catalog.GetBuilding(building.Kind);
            var entrance = BuildingEntrancePosition(building, catalog);
            int unitNum = GetUnitNumber(unitId);
            float offset = ((unitNum % 5) - 2) * 0.18f * catalog.Economy.CellSize;
            return new WorldPosition(entrance.X + offset, entrance.Y);
        }

        public static WorldPosition IdlePosition(int unitNumber, EconomyConfig economy)
        {
            int slot = unitNumber - 1;
            int crowd = slot / economy.MaxUnitsPerCell;
            var cell = new Cell(7 + (crowd % 3), 5 + (crowd / 3));
            return CrowdPosition(cell, slot % economy.MaxUnitsPerCell, economy);
        }

        public static WorldPosition CrowdPosition(Cell cell, int slot, EconomyConfig economy)
        {
            double goldenAngle = Math.PI * (3 - Math.Sqrt(5));
            double cellRotation = (cell.X * 17 + cell.Y * 31) * 0.19;
            int scatteredSlot = (slot * 7) % economy.MaxUnitsPerCell;
            double radius = Math.Sqrt((scatteredSlot + 0.5) / economy.MaxUnitsPerCell) * 0.36;
            double angle = cellRotation + scatteredSlot * goldenAngle;

            float cs = economy.CellSize;
            return new WorldPosition(
                (float)(cell.X + 0.5 + Math.Cos(angle) * radius) * cs,
                (float)(cell.Y + 0.5 + Math.Sin(angle) * radius) * cs
            );
        }

                public static float ProductionPerSecond(GameState state, string buildingId, GameContentCatalog catalog)
        {
            return CalculateMineProductionPerSecond(state, buildingId, catalog);
        }

        public static Cell? FindFirstValidMineCell(GameState state, GameContentCatalog catalog)
        {
            var economy = catalog.Economy;
            var mineDef = catalog.GetBuilding(BuildingKind.Mine);
            for (int y = 0; y <= economy.GridHeight - mineDef.Height; y++)
            {
                for (int x = 0; x <= economy.GridWidth - mineDef.Width; x++)
                {
                    var cell = new Cell(x, y);
                    if (ValidateMinePlacement(state, cell, catalog).Ok)
                        return cell;
                }
            }
            return null;
        }
public static int GetUnitNumber(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return 1;
            int dash = unitId.LastIndexOf('-');
            if (dash >= 0 && int.TryParse(unitId.Substring(dash + 1), out int num))
                return num;
            return 1;
        }
    }
}
