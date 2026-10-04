using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    // Why a producing building is or is not advancing its current cycle.
    public enum ProductionState
    {
        NotProducer,
        NoWorkers,
        MissingInputs,
        OutputFull,
        Working
    }

    public static class ColonySimulation
    {
        public static CommandResult ApplyCommand(GameState state, IGameCommand command, GameContentCatalog catalog)
        {
            return command switch
            {
                BuildMineCommand c => BuildMine(state, c.Cell, catalog),
                BuildBuildingCommand c => BuildBuilding(state, c.Kind, c.Cell, catalog),
                UpgradeBuildingCommand c => UpgradeBuilding(state, c.BuildingId, catalog),
                DemolishBuildingCommand c => DemolishBuilding(state, c.BuildingId, catalog),
                MoveBuildingCommand c => MoveBuilding(state, c.BuildingId, c.Cell, catalog),
                BuyUnitsCommand c => BuyUnits(state, c.UnitKind, c.Amount, c.Cell, catalog),
                AssignWorkCommand c => AssignWork(state, c.UnitIds, c.BuildingId, catalog),
                AssignHaulCommand c => AssignHaul(state, c.UnitIds, c.SourceId, c.DestinationId, c.Cargo, catalog),
                ReleaseUnitsCommand c => ReleaseUnits(state, c.UnitIds, catalog),
                SellUnitsCommand c => SellUnits(state, c.UnitIds, catalog),
                ClaimQuestRewardCommand => Progression.Claim(state, catalog),
                BuyLandCommand c => LandRules.Buy(state, c.BlockX, c.BlockY, catalog),
                ClearLandCommand c => LandRules.Clear(state, c.BlockX, c.BlockY, catalog),
                BuyUpgradeCommand c => UpgradeRules.Buy(state, c.UpgradeId, catalog),
                _ => CommandResult.Fail("Неизвестная команда")
            };
        }

        public static void TickColony(GameState state, float deltaSeconds, GameContentCatalog catalog)
        {
            LandRules.Tick(state, deltaSeconds);
            TrailRules.Tick(state, deltaSeconds, catalog);
            ProduceGoods(state, deltaSeconds, catalog);

            for (int i = 0; i < state.Units.Count; i++)
            {
                var unit = state.Units[i];
                if (unit.Assignment.Kind == AssignmentKind.ToWork)
                    TickWorkerArrival(state, unit, deltaSeconds, catalog);
                else if (unit.Assignment.Kind == AssignmentKind.Haul)
                    TickHauler(state, unit, deltaSeconds, catalog);
                else if (unit.Assignment.Kind == AssignmentKind.Idle && unit.Assignment.BuildingId != null)
                    TickGathering(state, unit, deltaSeconds, catalog);
            }
        }

        public static CommandResult ValidateBuildingPlacement(GameState state, BuildingKind kind, Cell cell, GameContentCatalog catalog, string ignoredId = null)
        {
            var definition = catalog.GetBuilding(kind);
            var economy = catalog.Economy;

            if (cell.X < 0 || cell.Y < 0 || cell.X + definition.Width > economy.GridWidth || cell.Y + definition.Height > economy.GridHeight)
                return CommandResult.Fail("Постройка выходит за границу поля");

            for (int y = cell.Y; y < cell.Y + definition.Height; y++)
            for (int x = cell.X; x < cell.X + definition.Width; x++)
                if (!LandRules.IsOpen(state, new Cell(x, y)))
                    return CommandResult.Fail(LandRequired);

            for (int i = 0; i < state.Buildings.Count; i++)
            {
                var b = state.Buildings[i];
                if (b.Id == ignoredId) continue;
                var bDef = catalog.GetBuilding(b.Kind);
                if (FootprintsOverlap(cell, definition.Width, definition.Height, b.Cell, bDef.Width, bDef.Height))
                    return CommandResult.Fail("Здесь уже стоит здание");
                if (BuildingOccupiesCell(cell, definition.Width, definition.Height,
                        ColonyNavigation.ApproachCell(b.Kind, b.Cell, catalog)))
                    return CommandResult.Fail("Постройка перекроет вход в другое здание");
            }

            // Units reach a building only through the open cell in front of its door.
            var approach = ColonyNavigation.ApproachCell(kind, cell, catalog);
            if (!ColonyNavigation.IsWalkable(state, approach, catalog, ignoredId))
                return CommandResult.Fail("Вход в постройку будет перекрыт");

            return CommandResult.Success();
        }

        private const string LandRequired = "Сначала купите и расчистите эту землю";

        public static CommandResult ValidateUnitPurchase(GameState state, UnitKind kind, int amount, Cell cell, GameContentCatalog catalog)
        {
            var economy = catalog.Economy;
            var unitDef = catalog.GetUnit(kind);

            if (!unitDef.Hireable)
                return CommandResult.Fail($"Существо «{unitDef.DisplayName}» не нанимается в поселение");
            if (!Progression.IsUnitUnlocked(state, kind))
                return CommandResult.Fail($"Существо «{unitDef.DisplayName}» ещё не открыто: выполняйте задания");

            if (amount < 1 || amount > economy.MaxUnitsPerCell)
                return CommandResult.Fail($"Количество должно быть от 1 до {economy.MaxUnitsPerCell}");

            if (cell.X < 0 || cell.Y < 0 || cell.X >= economy.GridWidth || cell.Y >= economy.GridHeight)
                return CommandResult.Fail("Клетка находится за границей поля");

            if (!LandRules.IsOpen(state, cell))
                return CommandResult.Fail(LandRequired);

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

            if (state.Gold < HirePrice(state, kind, amount, catalog))
                return CommandResult.Fail("Недостаточно золота");

            return CommandResult.Success();
        }

        /// <summary>
        /// Gold for the next building of this kind: its catalog price, times the copy growth once for every
        /// building of the kind already standing.
        /// </summary>
        public static int BuildingPrice(BuildingDefinition definition, int owned, EconomyConfig economy) =>
            RoundGold(definition.Price * Math.Pow(economy.BuildingCopyPriceGrowth, Math.Max(0, owned)));

        public static int BuildingPrice(GameState state, BuildingKind kind, GameContentCatalog catalog)
        {
            int owned = 0;
            foreach (var building in state.Buildings)
                if (building.Kind == kind) owned++;
            return BuildingPrice(catalog.GetBuilding(kind), owned, catalog.Economy);
        }

        /// <summary>
        /// Gold for hiring <paramref name="amount"/> creatures of a kind: each costs its catalog price plus the
        /// per-creature percentage for everyone already in the colony, the ones hired before it in the group included.
        /// </summary>
        public static int HirePrice(UnitDefinition definition, int population, int amount, EconomyConfig economy)
        {
            double percent = economy.HirePricePercentPerCreature / 100.0;
            int total = 0;
            for (int i = 0; i < amount; i++)
                total += RoundGold(definition.Price * (1.0 + percent * (population + i)));
            return total;
        }

        public static int HirePrice(GameState state, UnitKind kind, int amount, GameContentCatalog catalog) =>
            HirePrice(catalog.GetUnit(kind), state.Units.Count, amount, catalog.Economy);

        private static int RoundGold(double gold) => (int)Math.Round(gold, MidpointRounding.AwayFromZero);

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

        private static readonly ResourceKind[] AllResources = (ResourceKind[])Enum.GetValues(typeof(ResourceKind));

        // A route is valid when the source hands out at least one good the destination takes.
        public static bool IsValidHaulRoute(BuildingKind source, BuildingKind destination, GameContentCatalog catalog)
        {
            var sourceDef = catalog.GetBuilding(source);
            var destinationDef = catalog.GetBuilding(destination);
            foreach (var resource in AllResources)
                if (Provides(sourceDef, resource) && Accepts(destinationDef, resource, catalog))
                    return true;
            return false;
        }

        /// <summary>Goods a hauler can take from a source to a destination, in the order haulers pick them.</summary>
        public static List<ResourceKind> CarriableResources(BuildingKind source, BuildingKind destination,
            GameContentCatalog catalog)
        {
            var sourceDef = catalog.GetBuilding(source);
            var destinationDef = catalog.GetBuilding(destination);
            var goods = new List<ResourceKind>();
            foreach (var resource in AllResources)
                if (Provides(sourceDef, resource) && Accepts(destinationDef, resource, catalog)) goods.Add(resource);
            return goods;
        }

        /// <summary>Goods a building hands to haulers, whatever the destination.</summary>
        public static List<ResourceKind> ProvidedResources(BuildingKind source, GameContentCatalog catalog)
        {
            var definition = catalog.GetBuilding(source);
            var goods = new List<ResourceKind>();
            foreach (var resource in AllResources)
                if (Provides(definition, resource)) goods.Add(resource);
            return goods;
        }

        // Goods haulers may pick up: a stockpile's stored goods or a producer's outputs, never its inputs.
        public static bool Provides(BuildingDefinition definition, ResourceKind resource) =>
            definition.Stores(resource) || definition.ProducesInRecipe(resource);

        public static bool Accepts(BuildingDefinition definition, ResourceKind resource, GameContentCatalog catalog)
        {
            switch (definition.StorageRole)
            {
                case StorageRole.Market:
                    return catalog.TryGetResource(resource) != null;
                case StorageRole.Armory:
                    return catalog.TryGetResource(resource)?.IsEquipment == true;
                default:
                    return definition.Stores(resource) || definition.ConsumesInRecipe(resource);
            }
        }

        // How many units of a good the building can still take; markets and armories never fill up.
        public static int Room(BuildingState building, ResourceKind resource, GameContentCatalog catalog)
        {
            var definition = catalog.GetBuilding(building.Kind);
            if (!Accepts(definition, resource, catalog)) return 0;
            if (definition.StorageRole == StorageRole.Market || definition.StorageRole == StorageRole.Armory)
                return int.MaxValue;
            int capacity = definition.Capacity(building.Level);
            if (definition.StorageRole == StorageRole.Stockpile)
            {
                return definition.SlotStackSize > 0
                    ? StorageSlots.Room(building.Stock, resource, capacity, definition.SlotStackSize)
                    : Math.Max(0, capacity - building.TotalStock);
            }
            return Math.Max(0, capacity - building.GetStock(resource));
        }

        public static int SalePrice(GameContentCatalog catalog, ResourceKind resource, int marketLevel) =>
            catalog.GetResource(resource).SellPrice + catalog.GetBuilding(BuildingKind.Market).SaleBonus(marketLevel);

        private static CommandResult BuildMine(GameState state, Cell cell, GameContentCatalog catalog)
        {
            return BuildBuilding(state, BuildingKind.Mine, cell, catalog);
        }

        // Places a pre-built colony building: no price or constructible check, same placement rules as building.
        // Ids are numbered per kind ("market-1", "market-2") so a layout yields the same ids every run.
        public static CommandResult PlaceStartingBuilding(GameState state, BuildingKind kind, Cell cell,
            GameContentCatalog catalog, out string buildingId)
        {
            buildingId = null;
            var validation = ValidateBuildingPlacement(state, kind, cell, catalog);
            if (!validation.Ok) return validation;

            string prefix = kind.ToString().ToLowerInvariant();
            int number = 1;
            while (state.Buildings.Exists(b => b.Id == $"{prefix}-{number}")) number++;
            buildingId = $"{prefix}-{number}";
            state.LayoutVersion++;
            state.Buildings.Add(new BuildingState
            {
                Id = buildingId,
                Kind = kind,
                Cell = cell,
                Level = 1,
                ProductionProgress = 0f
            });
            return CommandResult.Success();
        }

        private static CommandResult BuildBuilding(GameState state, BuildingKind kind, Cell cell, GameContentCatalog catalog)
        {
            var definition = catalog.GetBuilding(kind);
            if (!definition.Constructible)
                return CommandResult.Fail("Эту постройку нельзя построить");
            if (!Progression.IsBuildingUnlocked(state, kind))
                return CommandResult.Fail($"Постройка «{definition.DisplayName}» ещё не открыта: выполняйте задания");
            var validation = ValidateBuildingPlacement(state, kind, cell, catalog);
            if (!validation.Ok) return validation;

            int price = BuildingPrice(state, kind, catalog);
            if (state.Gold < price) return CommandResult.Fail("Недостаточно золота");

            state.Gold -= price;
            string prefix = kind.ToString().ToLowerInvariant();
            string newId;
            do { newId = $"{prefix}-{state.NextBuildingId++}"; }
            while (state.Buildings.Exists(b => b.Id == newId));
            state.LayoutVersion++;
            state.Buildings.Add(new BuildingState
            {
                Id = newId,
                Kind = kind,
                Cell = cell,
                Level = 1,
                InvestedGold = price,
                ProductionProgress = 0f
            });

            return CommandResult.Success();
        }

        private static CommandResult BuyUnits(GameState state, UnitKind kind, int amount, Cell cell, GameContentCatalog catalog)
        {
            var validation = ValidateUnitPurchase(state, kind, amount, cell, catalog);
            if (!validation.Ok) return validation;

            state.Gold -= HirePrice(state, kind, amount, catalog);

            var economy = catalog.Economy;
            int existingInCell = CountUnitsInCell(state, cell, economy);

            var definition = catalog.GetUnit(kind);
            for (int i = 0; i < amount; i++)
            {
                int serial = state.NextUnitId++;
                string uId = $"unit-{serial}";
                var pos = CrowdPosition(cell, existingInCell + i, economy);
                state.Units.Add(new UnitState
                {
                    Id = uId,
                    Kind = kind,
                    Name = PickUnitName(state, definition, serial),
                    Position = pos,
                    Assignment = Assignment.Idle()
                });
            }

            return CommandResult.Success();
        }

        /// <summary>
        /// A new creature's name: the first of its kind's names nobody in the colony wears, starting where the
        /// hire's serial points so a group does not read down the list; when every name is worn, a name with an
        /// epithet. The same colony always names the same hire the same way.
        /// </summary>
        public static string PickUnitName(GameState state, UnitDefinition definition, int serial)
        {
            var names = definition.Names;
            if (names.Count == 0) return definition.DisplayName;

            var worn = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unit in state.Units)
                if (unit.Name != null) worn.Add(unit.Name);

            uint mix = unchecked((uint)serial * 0x9E3779B9u);
            mix ^= mix >> 16;
            mix = unchecked(mix * 0x85EBCA6Bu);
            mix ^= mix >> 13;
            int start = (int)(mix % (uint)names.Count);

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[(start + i) % names.Count];
                if (!worn.Contains(name)) return name;
            }
            var epithets = definition.Epithets;
            for (int e = 0; e < epithets.Count; e++)
            for (int i = 0; i < names.Count; i++)
            {
                string name = $"{names[(start + i) % names.Count]} {epithets[(start + e) % epithets.Count]}";
                if (!worn.Contains(name)) return name;
            }
            return names[start];
        }

        private static CommandResult AssignWork(
            GameState state,
            IReadOnlyList<string> unitIds,
            string buildingId,
            GameContentCatalog catalog)
        {
            var resolveResult = ResolveUnits(state, unitIds, out var selectedUnits);
            if (!resolveResult.Ok) return resolveResult;

            var building = state.Buildings.Find(b => b.Id == buildingId);
            if (building == null || !catalog.GetBuilding(building.Kind).IsWorkplace)
                return CommandResult.Fail("Работать можно только на производстве");

            var workplaceDef = catalog.GetBuilding(building.Kind);

            int currentAssigned = 0;
            for (int i = 0; i < state.Units.Count; i++)
            {
                var a = state.Units[i].Assignment;
                if ((a.Kind == AssignmentKind.ToWork || a.Kind == AssignmentKind.Work) && a.BuildingId == buildingId)
                    currentAssigned++;
            }

            int availableSlots = workplaceDef.WorkerCapacity(building.Level) - currentAssigned;
            int assigned = 0;
            for (int i = 0; i < selectedUnits.Count && assigned < Math.Max(0, availableSlots); i++)
            {
                var u = selectedUnits[i];

                if ((u.Assignment.Kind == AssignmentKind.ToWork || u.Assignment.Kind == AssignmentKind.Work) && u.Assignment.BuildingId == buildingId)
                    continue;

                ReturnCarriedCargo(state, u);
                u.Assignment = Assignment.ToWork(buildingId);
                assigned++;
            }

            return CommandResult.Success();
        }

        private static CommandResult AssignHaul(
            GameState state,
            IReadOnlyList<string> unitIds,
            string sourceId,
            string destinationId,
            IReadOnlyList<ResourceKind> cargo,
            GameContentCatalog catalog)
        {
            var resolveResult = ResolveUnits(state, unitIds, out var selectedUnits);
            if (!resolveResult.Ok) return resolveResult;

            var source = state.Buildings.Find(b => b.Id == sourceId);
            var destination = state.Buildings.Find(b => b.Id == destinationId);
            if (source == null || destination == null) return CommandResult.Fail("Здание не найдено");

            if (source.Id == destination.Id || !IsValidHaulRoute(source.Kind, destination.Kind, catalog))
                return CommandResult.Fail("Этот маршрут не перевозит подходящий товар");

            // a chosen good must travel this route; an empty choice means everything it can carry
            var carriable = CarriableResources(source.Kind, destination.Kind, catalog);
            var goods = new List<ResourceKind>();
            if (cargo != null)
            {
                foreach (var resource in cargo)
                {
                    if (!carriable.Contains(resource))
                        return CommandResult.Fail(
                            $"По этому маршруту не возят: {catalog.TryGetResource(resource)?.DisplayName ?? resource.ToString()}");
                    if (!goods.Contains(resource)) goods.Add(resource);
                }
            }

            for (int i = 0; i < selectedUnits.Count; i++)
            {
                var u = selectedUnits[i];

                ReturnCarriedCargo(state, u);
                u.Assignment = Assignment.Haul(sourceId, destinationId, goods);
            }

            return CommandResult.Success();
        }

        private static CommandResult ReleaseUnits(GameState state, IReadOnlyList<string> unitIds, GameContentCatalog catalog)
        {
            var resolveResult = ResolveUnits(state, unitIds, out var selectedUnits);
            if (!resolveResult.Ok) return resolveResult;

            for (int i = 0; i < selectedUnits.Count; i++)
            {
                var u = selectedUnits[i];

                ReturnCarriedCargo(state, u);
                Free(state, u, catalog);
            }

            return CommandResult.Success();
        }

        /// <summary>
        /// Takes a creature off its job: it walks to a place in the group at the nearest barracks door. Without
        /// barracks a worker steps out and waits at its workplace door; anyone else stays where it stands.
        /// </summary>
        private static void Free(GameState state, UnitState unit, GameContentCatalog catalog)
        {
            var a = unit.Assignment;
            var place = NearestBarracks(state, unit.Position, catalog);
            if (place == null && a.Kind == AssignmentKind.Work)
                place = state.Buildings.Find(b => b.Id == a.BuildingId);
            // already waiting there: it keeps its place
            if (place != null && a.Kind == AssignmentKind.Idle && a.BuildingId == place.Id && a.CrowdSlot >= 0) return;

            unit.Assignment = Assignment.Idle(place?.Id);
            unit.ClearRoute();
            if (place != null && !TryClaimCrowdSlot(state, unit, place, catalog))
                unit.Assignment = Assignment.Idle();
        }

        private static BuildingState NearestBarracks(GameState state, WorldPosition from, GameContentCatalog catalog)
        {
            BuildingState nearest = null;
            float best = float.MaxValue;
            foreach (var building in state.Buildings)
            {
                if (building.Kind != BuildingKind.Barracks) continue;
                var door = ColonyNavigation.DoorwayOf(building, catalog).Approach;
                float dx = door.X - from.X, dy = door.Y - from.Y;
                if (dx * dx + dy * dy >= best) continue;
                best = dx * dx + dy * dy;
                nearest = building;
            }
            return nearest;
        }

        private static CommandResult SellUnits(GameState state, IReadOnlyList<string> unitIds, GameContentCatalog catalog)
        {
            var resolveResult = ResolveUnits(state, unitIds, out var selectedUnits);
            if (!resolveResult.Ok) return resolveResult;

            int totalRefund = 0;
            var toRemove = new HashSet<string>();

            for (int i = 0; i < selectedUnits.Count; i++)
            {
                var u = selectedUnits[i];
                ReturnCarriedCargo(state, u);
                var def = catalog.GetUnit(u.Kind);
                int refund = UnitSaleRefund(def);
                totalRefund += refund;
                toRemove.Add(u.Id);
            }

            state.Units.RemoveAll(u => toRemove.Contains(u.Id));
            foreach (var item in state.Equipment)
                if (item.OwnerUnitId != null && toRemove.Contains(item.OwnerUnitId))
                    item.OwnerUnitId = null;
            state.Gold += totalRefund;
            return CommandResult.Success();
        }

        private static void ProduceGoods(GameState state, float deltaSeconds, GameContentCatalog catalog)
        {
            for (int i = 0; i < state.Buildings.Count; i++)
            {
                var b = state.Buildings[i];
                var def = catalog.GetBuilding(b.Kind);
                if (def.Recipes.Count == 0) continue;

                var recipe = NextRecipe(b, def, catalog);
                if (recipe == null)
                {
                    // A stalled building does not bank work while it waits.
                    b.ProductionProgress = 0f;
                    continue;
                }

                b.ProductionProgress += WorkPerSecond(state, b.Id, catalog) * deltaSeconds;
                while (recipe != null && b.ProductionProgress >= recipe.Work)
                {
                    RunCycle(state, b, recipe);
                    b.ProductionProgress -= recipe.Work;
                    recipe = NextRecipe(b, def, catalog);
                }
                if (recipe == null) b.ProductionProgress = 0f;
            }
        }

        private static ProductionRecipe NextRecipe(BuildingState building, BuildingDefinition definition, GameContentCatalog catalog)
        {
            foreach (var recipe in definition.Recipes)
                if (recipe.MinLevel <= building.Level && HasInputs(building, recipe) &&
                    HasOutputRoom(building, recipe, catalog))
                    return recipe;
            return null;
        }

        /// <summary>The recipes a building runs at its level, in priority order.</summary>
        public static List<ProductionRecipe> ActiveRecipes(BuildingState building, GameContentCatalog catalog)
        {
            var active = new List<ProductionRecipe>();
            foreach (var recipe in catalog.GetBuilding(building.Kind).Recipes)
                if (recipe.MinLevel <= building.Level) active.Add(recipe);
            return active;
        }

        private static bool HasInputs(BuildingState building, ProductionRecipe recipe)
        {
            foreach (var input in recipe.Inputs)
                if (building.GetStock(input.Resource) < input.Amount) return false;
            return true;
        }

        // Room for every outcome the cycle may have: the product, each by-product open at this level, spoilage.
        private static bool HasOutputRoom(BuildingState building, ProductionRecipe recipe, GameContentCatalog catalog)
        {
            int capacity = catalog.GetBuilding(building.Kind).Capacity(building.Level);
            foreach (var output in recipe.Outputs)
                if (building.GetStock(output.Resource) + output.Amount > capacity) return false;
            foreach (var extra in recipe.Extras)
                if (extra.MinLevel <= building.Level &&
                    building.GetStock(extra.Output.Resource) + extra.Output.Amount > capacity)
                    return false;
            if (recipe.FailChancePercent > 0)
                foreach (var output in recipe.FailOutputs)
                    if (building.GetStock(output.Resource) + output.Amount > capacity) return false;
            return true;
        }

        // Inputs are consumed and outputs released together, so a cycle never half-completes. A spoiled cycle
        // gives its fail outputs instead of the product and no by-products; chances roll on the production dice.
        private static void RunCycle(GameState state, BuildingState building, ProductionRecipe recipe)
        {
            foreach (var input in recipe.Inputs)
                building.AddStock(input.Resource, -input.Amount);
            bool spoiled = recipe.FailChancePercent > 0 && ProductionDice.Chance(state, recipe.FailChancePercent);
            if (spoiled)
            {
                foreach (var output in recipe.FailOutputs)
                    Yield(state, building, output);
            }
            else
            {
                foreach (var output in recipe.Outputs)
                    Yield(state, building, output);
                foreach (var extra in recipe.Extras)
                    if (extra.MinLevel <= building.Level && ProductionDice.Chance(state, extra.ChancePercent))
                        Yield(state, building, extra.Output);
            }
            building.CompletedCycles++;
        }

        private static void Yield(GameState state, BuildingState building, ResourceAmount output)
        {
            building.AddStock(output.Resource, output.Amount);
            state.ProducedByResource[output.Resource] = state.ProducedOf(output.Resource) + output.Amount;
        }

        // Work the building's present workers add per second; a creature in one of its favoured buildings adds more.
        private static float WorkPerSecond(GameState state, string buildingId, GameContentCatalog catalog)
        {
            float total = 0f;
            BuildingKind? kind = null;
            for (int i = 0; i < state.Units.Count; i++)
            {
                var u = state.Units[i];
                if (u.Assignment.Kind == AssignmentKind.Work && u.Assignment.BuildingId == buildingId)
                {
                    var uDef = catalog.GetUnit(u.Kind);
                    float work = uDef.Strength * catalog.Economy.WorkPerStrengthSecond;
                    kind ??= state.Buildings.Find(b => b.Id == buildingId)?.Kind;
                    if (kind.HasValue && uDef.Favors(kind.Value))
                        work = UpgradeRules.Raise(work, uDef.FavoredWorkPercent);
                    total += work;
                }
            }
            return total;
        }

        public static ProductionState DescribeProduction(GameState state, BuildingState building, GameContentCatalog catalog)
        {
            var def = catalog.GetBuilding(building.Kind);
            if (!def.IsWorkplace) return ProductionState.NotProducer;
            if (NextRecipe(building, def, catalog) == null)
            {
                foreach (var recipe in def.Recipes)
                    if (recipe.MinLevel <= building.Level && HasInputs(building, recipe)) return ProductionState.OutputFull;
                return ProductionState.MissingInputs;
            }
            return WorkPerSecond(state, building.Id, catalog) > 0f ? ProductionState.Working : ProductionState.NoWorkers;
        }

        private static void TickWorkerArrival(GameState state, UnitState unit, float deltaSeconds, GameContentCatalog catalog)
        {
            var building = state.Buildings.Find(b => b.Id == unit.Assignment.BuildingId);
            if (building == null) return;

            var targetPos = BuildingEntrancePosition(building, catalog);
            var unitDef = catalog.GetUnit(unit.Kind);
            float speedInWorldUnits = UnitMovementSpeed(state, unitDef, catalog);

            if (Travel(state, unit, targetPos, building, speedInWorldUnits, deltaSeconds, catalog))
            {
                unit.Assignment = Assignment.Work(building.Id);
            }
        }

        // A free creature walks to its place in the group at its gathering door and waits there. A building put
        // up on that place sends it to another one at the same door.
        private static void TickGathering(GameState state, UnitState unit, float deltaSeconds, GameContentCatalog catalog)
        {
            var place = state.Buildings.Find(b => b.Id == unit.Assignment.BuildingId);
            if (place == null || unit.Assignment.CrowdSlot < 0)
            {
                unit.Assignment = Assignment.Idle();
                return;
            }
            var spot = ColonyNavigation.CrowdSlotPosition(place, unit.Assignment.CrowdSlot, catalog);
            if (!ColonyNavigation.IsWalkablePoint(state, spot, catalog))
            {
                if (!TryClaimCrowdSlot(state, unit, place, catalog))
                {
                    unit.Assignment = Assignment.Idle();
                    return;
                }
                spot = ColonyNavigation.CrowdSlotPosition(place, unit.Assignment.CrowdSlot, catalog);
            }
            if (unit.Position.Equals(spot)) return;
            Travel(state, unit, spot, null, UnitMovementSpeed(state, catalog.GetUnit(unit.Kind), catalog), deltaSeconds, catalog);
        }

        private static void TickHauler(GameState state, UnitState unit, float deltaSeconds, GameContentCatalog catalog)
        {
            var source = state.Buildings.Find(b => b.Id == unit.Assignment.SourceId);
            var destination = state.Buildings.Find(b => b.Id == unit.Assignment.DestinationId);
            if (source == null || destination == null) return;

            var unitDef = catalog.GetUnit(unit.Kind);
            float speedInWorldUnits = UnitMovementSpeed(state, unitDef, catalog);
            var assignment = unit.Assignment;

            switch (assignment.Phase)
            {
                case HaulPhase.ToSource:
                {
                    var dockPos = SourceDockPosition(source, unit, catalog);
                    if (Travel(state, unit, dockPos, source, speedInWorldUnits, deltaSeconds, catalog))
                    {
                        assignment.PhaseElapsedSeconds = 0f;
                        if (FreeLoadingPlaces(state, unit, catalog) > 0 && !HasSourceQueue(state, source.Id))
                            BeginLoading(state, unit, source, destination, unitDef, catalog);
                        else
                        {
                            assignment.Phase = HaulPhase.QueuedAtSource;
                            assignment.QueueTicket = state.NextHaulQueueTicket++;
                        }
                    }
                    break;
                }
                case HaulPhase.QueuedAtSource:
                {
                    int rank = SourceQueueRank(state, unit);
                    if (rank < FreeLoadingPlaces(state, unit, catalog))
                    {
                        assignment.Phase = HaulPhase.ToDock;
                        assignment.QueueTicket = 0;
                        assignment.CrowdSlot = -1;
                        break;
                    }
                    // Tickets keep the loading order; the waiters themselves stand as a loose group.
                    if (assignment.CrowdSlot < 0 && !TryClaimCrowdSlot(state, unit, source, catalog)) break;
                    Travel(state, unit, ColonyNavigation.CrowdSlotPosition(source, assignment.CrowdSlot, catalog), null,
                        speedInWorldUnits, deltaSeconds, catalog);
                    break;
                }
                case HaulPhase.ToDock:
                {
                    if (Travel(state, unit, SourceDockPosition(source, unit, catalog), source, speedInWorldUnits, deltaSeconds, catalog))
                        BeginLoading(state, unit, source, destination, unitDef, catalog);
                    break;
                }
                case HaulPhase.Loading:
                {
                    assignment.PhaseElapsedSeconds += deltaSeconds;
                    TryLoad(state, unit, source, destination, unitDef, catalog);
                    break;
                }
                case HaulPhase.ToDestination:
                {
                    // Deliveries gather as a loose group in front of the door, one place per hauler.
                    if (assignment.CrowdSlot < 0 && !TryClaimCrowdSlot(state, unit, destination, catalog)) break;
                    var place = ColonyNavigation.CrowdSlotPosition(destination, assignment.CrowdSlot, catalog);
                    if (Travel(state, unit, place, null, speedInWorldUnits, deltaSeconds, catalog))
                    {
                        assignment.Phase = HaulPhase.Unloading;
                        assignment.PhaseElapsedSeconds = 0f;
                        // with no unloading time the goods change hands on arrival
                        if (UnloadSeconds(state, catalog) <= 0f) TryUnload(state, destination, assignment, catalog);
                    }
                    break;
                }
                case HaulPhase.Unloading:
                {
                    assignment.PhaseElapsedSeconds += deltaSeconds;
                    TryUnload(state, destination, assignment, catalog);
                    break;
                }
            }
        }

        // The hauler stands at the door; with no loading time it takes its load at once.
        private static void BeginLoading(GameState state, UnitState unit, BuildingState source,
            BuildingState destination, UnitDefinition unitDef, GameContentCatalog catalog)
        {
            unit.Assignment.Phase = HaulPhase.Loading;
            unit.Assignment.PhaseElapsedSeconds = 0f;
            if (LoadSeconds(state, catalog) <= 0f) TryLoad(state, unit, source, destination, unitDef, catalog);
        }

        // Once EconomyConfig.LoadSeconds have passed, takes as much of the first good it may carry as it can
        // lift and the destination can hold. With nothing it may take it waits at the door, unless others
        // queue for the door: then it gives up its place and joins the back of the queue.
        private static void TryLoad(GameState state, UnitState unit, BuildingState source, BuildingState destination,
            UnitDefinition unitDef, GameContentCatalog catalog)
        {
            var assignment = unit.Assignment;
            float loadTime = LoadSeconds(state, catalog);
            if (assignment.PhaseElapsedSeconds < loadTime) return;
            if (!TryPickCargo(source, destination, assignment, catalog, out var resource, out int destinationRoom))
            {
                if (HasSourceQueue(state, source.Id))
                {
                    assignment.Phase = HaulPhase.QueuedAtSource;
                    assignment.QueueTicket = state.NextHaulQueueTicket++;
                    assignment.CrowdSlot = -1;
                    assignment.PhaseElapsedSeconds = 0f;
                    return;
                }
                assignment.PhaseElapsedSeconds = loadTime;
                return;
            }
            int carryBudget = assignment.CarryCreditPercent + HaulStamina(state, unitDef, catalog);
            int capacity = TripCarryCapacity(carryBudget);
            int taken = Math.Min(source.GetStock(resource), Math.Min(capacity, destinationRoom));

            source.AddStock(resource, -taken);
            assignment.Carried = taken;
            assignment.CarriedResource = resource;
            assignment.CarryCreditPercent = carryBudget >= StaminaPercentPerOre
                ? carryBudget % StaminaPercentPerOre
                : 0;
            assignment.Phase = HaulPhase.ToDestination;
            assignment.PhaseElapsedSeconds = 0f;
        }

        // Once EconomyConfig.UnloadSeconds have passed, hands the load over (sells it at a market); what does
        // not fit stays with the hauler, who tries again next step.
        private static void TryUnload(GameState state, BuildingState destination, Assignment assignment,
            GameContentCatalog catalog)
        {
            float unloadTime = UnloadSeconds(state, catalog);
            if (assignment.PhaseElapsedSeconds < unloadTime) return;
            Unload(state, destination, assignment, catalog);
            if (assignment.Carried == 0)
            {
                assignment.CrowdSlot = -1;
                assignment.Phase = HaulPhase.ToSource;
                assignment.PhaseElapsedSeconds = 0f;
            }
            else
            {
                assignment.PhaseElapsedSeconds = unloadTime;
            }
        }

        // Of the goods the hauler may take, the source holds and the destination still has room for, the one the
        // source holds most of (ties in ResourceKind order): a mine's crystals leave as surely as its ore, so a
        // by-product never fills the building up behind the main good.
        private static bool TryPickCargo(BuildingState source, BuildingState destination, Assignment assignment,
            GameContentCatalog catalog, out ResourceKind resource, out int destinationRoom)
        {
            var sourceDef = catalog.GetBuilding(source.Kind);
            resource = default;
            destinationRoom = 0;
            int most = 0;
            foreach (var candidate in AllResources)
            {
                int held = source.GetStock(candidate);
                if (held <= most || !assignment.MayCarry(candidate) || !Provides(sourceDef, candidate))
                    continue;
                int room = Room(destination, candidate, catalog);
                if (room <= 0) continue;
                resource = candidate;
                destinationRoom = room;
                most = held;
            }
            return most > 0;
        }

        // Anything that does not fit stays with the hauler, who retries next step instead of dropping it.
        private static void Unload(GameState state, BuildingState destination, Assignment assignment, GameContentCatalog catalog)
        {
            var resource = assignment.CarriedResource;
            var role = catalog.GetBuilding(destination.Kind).StorageRole;
            if (role == StorageRole.Market)
            {
                int income = assignment.Carried * SalePrice(catalog, resource, destination.Level);
                state.SoldGoods += assignment.Carried;
                state.SoldByResource[resource] = state.SoldOf(resource) + assignment.Carried;
                state.SalesGold += income;
                state.Gold += income;
                assignment.Carried = 0;
            }
            else if (role == StorageRole.Armory)
            {
                string equipmentId = catalog.GetResource(resource).EquipmentId;
                for (int i = 0; i < assignment.Carried; i++)
                    state.Equipment.Add(new EquipmentState { Id = NextEquipmentId(state), DefinitionId = equipmentId });
                assignment.Carried = 0;
            }
            else
            {
                int deposited = Math.Min(Room(destination, resource, catalog), assignment.Carried);
                destination.AddStock(resource, deposited);
                assignment.Carried -= deposited;
            }
        }

        private static string NextEquipmentId(GameState state)
        {
            for (int n = state.Equipment.Count + 1; ; n++)
            {
                string id = $"item-{n:D3}";
                if (!state.Equipment.Exists(item => item.Id == id)) return id;
            }
        }

        // Each source building loads EconomyConfig.LoadersPerDoor haulers at a time; the rest wait as a group
        // at its door and step up as places free.
        private const int MaxCrowdSlots = 64;

        // Loaders stand at the entrance itself.
        private static WorldPosition SourceDockPosition(BuildingState source, UnitState unit, GameContentCatalog catalog)
        {
            return BuildingEntrancePosition(source, catalog);
        }

        // Lowest free place in the group at this building's door whose ground is open.
        // Queued and delivering haulers share one group, so nobody is given a place already taken.
        private static bool TryClaimCrowdSlot(GameState state, UnitState unit, BuildingState building,
            GameContentCatalog catalog)
        {
            var taken = new HashSet<int>();
            foreach (var other in state.Units)
                if (other != unit && other.Assignment.CrowdSlot >= 0 && CrowdBuildingId(other.Assignment) == building.Id)
                    taken.Add(other.Assignment.CrowdSlot);
            for (int slot = 0; slot < MaxCrowdSlots; slot++)
            {
                if (taken.Contains(slot)) continue;
                if (!ColonyNavigation.IsWalkablePoint(state, ColonyNavigation.CrowdSlotPosition(building, slot, catalog), catalog))
                    continue;
                unit.Assignment.CrowdSlot = slot;
                return true;
            }
            return false;
        }

        private static string CrowdBuildingId(Assignment assignment) =>
            assignment.Kind == AssignmentKind.Idle ? assignment.BuildingId
            : assignment.Kind != AssignmentKind.Haul ? null
            : assignment.Phase == HaulPhase.QueuedAtSource ? assignment.SourceId
            : assignment.Phase == HaulPhase.ToDestination || assignment.Phase == HaulPhase.Unloading ? assignment.DestinationId
            : null;

        // Places at the source's door still open for loading: EconomyConfig.LoadersPerDoor minus the haulers
        // already stepping up to it or loading there.
        private static int FreeLoadingPlaces(GameState state, UnitState unit, GameContentCatalog catalog)
        {
            string sourceId = unit.Assignment.SourceId;
            int busy = 0;
            for (int i = 0; i < state.Units.Count; i++)
            {
                var other = state.Units[i];
                if (other == unit) continue;
                var a = other.Assignment;
                if (a.Kind == AssignmentKind.Haul && a.SourceId == sourceId &&
                    (a.Phase == HaulPhase.ToDock || a.Phase == HaulPhase.Loading))
                    busy++;
            }
            return Math.Max(0, LoadersPerDoor(state, catalog) - busy);
        }

        /// <summary>Haulers loading at one door at once: the economy's number plus the guild's widened doors.</summary>
        public static int LoadersPerDoor(GameState state, GameContentCatalog catalog) =>
            catalog.Economy.LoadersPerDoor + UpgradeRules.Total(state, catalog, UpgradeEffect.LoadersPerDoor);

        /// <summary>Seconds a hauler takes to load at a door, after the guild's quick hands.</summary>
        public static float LoadSeconds(GameState state, GameContentCatalog catalog) =>
            UpgradeRules.Shorten(catalog.Economy.LoadSeconds,
                UpgradeRules.Total(state, catalog, UpgradeEffect.HandlingTimePercent));

        /// <summary>Seconds a hauler takes to hand its load over, after the guild's quick hands.</summary>
        public static float UnloadSeconds(GameState state, GameContentCatalog catalog) =>
            UpgradeRules.Shorten(catalog.Economy.UnloadSeconds,
                UpgradeRules.Total(state, catalog, UpgradeEffect.HandlingTimePercent));

        /// <summary>A hauler's stamina for carrying, in percent, with the guild's strong backs.</summary>
        public static int HaulStamina(GameState state, UnitDefinition definition, GameContentCatalog catalog) =>
            definition.Stamina + UpgradeRules.Total(state, catalog, UpgradeEffect.CarryPercent);

        private static bool HasSourceQueue(GameState state, string sourceId)
        {
            for (int i = 0; i < state.Units.Count; i++)
            {
                var a = state.Units[i].Assignment;
                if (a.Kind == AssignmentKind.Haul && a.SourceId == sourceId && a.Phase == HaulPhase.QueuedAtSource)
                    return true;
            }
            return false;
        }

        private static int SourceQueueRank(GameState state, UnitState unit)
        {
            var own = unit.Assignment;
            int rank = 0;
            for (int i = 0; i < state.Units.Count; i++)
            {
                var a = state.Units[i].Assignment;
                if (a.Kind == AssignmentKind.Haul && a.SourceId == own.SourceId &&
                    a.Phase == HaulPhase.QueuedAtSource && a.QueueTicket < own.QueueTicket)
                    rank++;
            }
            return rank;
        }

        /// <summary>
        /// Walks the unit toward <paramref name="goal"/> around footprints; a goal inside
        /// <paramref name="goalBuilding"/> is entered through its door. Returns true on arrival.
        /// With no open route the unit waits and replans next tick.
        /// </summary>
        private static bool Travel(GameState state, UnitState unit, WorldPosition goal, BuildingState goalBuilding,
            float speed, float deltaSeconds, GameContentCatalog catalog)
        {
            if (!unit.HasRoute || !unit.RouteGoal.Equals(goal) || unit.RouteLayoutVersion != state.LayoutVersion)
            {
                var route = ColonyNavigation.FindRoute(state, unit.Position, goal, goalBuilding, catalog);
                if (route == null)
                {
                    unit.ClearRoute();
                    return false;
                }
                unit.Route = route;
                unit.HasRoute = true;
                unit.RouteGoal = goal;
                unit.RouteLayoutVersion = state.LayoutVersion;
            }

            // a trail underfoot speeds the creature up; every cell it steps into wears (heavy creatures more)
            var economy = catalog.Economy;
            float budget = speed * deltaSeconds * TrailRules.SpeedAt(state, unit.Position, economy);
            int wear = state.Trails != null ? catalog.GetUnit(unit.Kind)?.TrailWear ?? 1 : 0;
            while (unit.Route.Count > 0)
            {
                var next = unit.Route[0];
                var from = unit.Position;
                float dx = next.X - from.X;
                float dy = next.Y - from.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > budget)
                {
                    unit.Position = new WorldPosition(from.X + dx / dist * budget, from.Y + dy / dist * budget);
                    TrailRules.Walk(state, from, unit.Position, wear, economy);
                    return false;
                }
                unit.Position = next;
                TrailRules.Walk(state, from, next, wear, economy);
                budget -= dist;
                unit.Route.RemoveAt(0);
            }

            unit.ClearRoute();
            return true;
        }

        private static void ReturnCarriedCargo(GameState state, UnitState unit)
        {
            if (unit.Assignment.Kind != AssignmentKind.Haul || unit.Assignment.Carried == 0)
                return;

            var source = state.Buildings.Find(b => b.Id == unit.Assignment.SourceId);
            if (source != null)
                source.AddStock(unit.Assignment.CarriedResource, unit.Assignment.Carried);
        }

        private static CommandResult ResolveUnits(GameState state, IReadOnlyList<string> unitIds, out List<UnitState> units)
        {
            units = new List<UnitState>();
            if (unitIds == null || unitIds.Count == 0)
                return CommandResult.Fail("Сначала выберите юнитов");

            var seen = new HashSet<string>();
            for (int i = 0; i < unitIds.Count; i++)
            {
                string id = unitIds[i];
                if (!seen.Add(id))
                    return CommandResult.Fail("Юнит выбран дважды");

                var unit = state.Units.Find(candidate => candidate.Id == id);
                if (unit == null)
                    return CommandResult.Fail("Юнит не найден");

                units.Add(unit);
            }

            return CommandResult.Success();
        }

        private static CommandResult UpgradeBuilding(GameState state, string buildingId, GameContentCatalog catalog)
        {
            var building = state.Buildings.Find(b => b.Id == buildingId);
            if (building == null) return CommandResult.Fail("Здание не найдено");
            int cost = catalog.GetBuilding(building.Kind).UpgradeCost(building.Level);
            if (cost < 0) return CommandResult.Fail("Достигнут максимальный уровень");
            if (state.Gold < cost) return CommandResult.Fail("Недостаточно золота");
            state.Gold -= cost;
            building.Level++;
            building.InvestedGold += cost;
            return CommandResult.Success();
        }

        private static CommandResult DemolishBuilding(GameState state, string buildingId, GameContentCatalog catalog)
        {
            var building = state.Buildings.Find(b => b.Id == buildingId);
            if (building == null) return CommandResult.Fail("Здание не найдено");
            // what cannot be built again (the scene's market and warehouse) cannot be torn down either
            if (!catalog.GetBuilding(building.Kind).Constructible) return CommandResult.Fail("Эту постройку нельзя снести");
            if (building.TotalStock > 0) return CommandResult.Fail("Сначала вывезите товары из постройки");
            foreach (var unit in state.Units)
            {
                var a = unit.Assignment;
                if (a.Kind == AssignmentKind.Haul && a.Carried > 0 && (a.SourceId == buildingId || a.DestinationId == buildingId))
                    return CommandResult.Fail("Сначала завершите перевозку груза");
            }
            state.Buildings.Remove(building);
            state.LayoutVersion++;
            // its workers and haulers, and the free creatures waiting at its door, are free to go to the barracks
            foreach (var unit in state.Units)
            {
                var a = unit.Assignment;
                if ((a.Kind != AssignmentKind.Haul && a.BuildingId == buildingId) ||
                    (a.Kind == AssignmentKind.Haul && (a.SourceId == buildingId || a.DestinationId == buildingId)))
                    Free(state, unit, catalog);
            }
            state.Gold += building.InvestedGold / 2;
            return CommandResult.Success();
        }

        private static CommandResult MoveBuilding(GameState state, string buildingId, Cell cell, GameContentCatalog catalog)
        {
            var building = state.Buildings.Find(b => b.Id == buildingId);
            if (building == null) return CommandResult.Fail("Здание не найдено");
            var validation = ValidateBuildingPlacement(state, building.Kind, cell, catalog, buildingId);
            if (!validation.Ok) return validation;
            var oldEntrance = BuildingEntrancePosition(building, catalog);
            building.Cell = cell;
            state.LayoutVersion++;
            foreach (var unit in state.Units)
            {
                var a = unit.Assignment;
                if (a.Kind == AssignmentKind.Work && a.BuildingId == buildingId)
                {
                    unit.PlaceAt(oldEntrance);
                    unit.Assignment = Assignment.ToWork(buildingId);
                }

            }
            return CommandResult.Success();
        }

        public static WorldPosition BuildingEntrancePosition(BuildingState building, GameContentCatalog catalog)
        {
            var def = catalog.GetBuilding(building.Kind);
            float cs = catalog.Economy.CellSize;
            return new WorldPosition(
                (building.Cell.X + def.EntranceX) * cs,
                (building.Cell.Y + def.EntranceY) * cs
            );
        }

        public static bool IsPointWithinBuilding(BuildingState building, WorldPosition point, GameContentCatalog catalog)
        {
            if (building == null) return false;
            var def = catalog.GetBuilding(building.Kind);
            float cs = catalog.Economy.CellSize;
            float minX = building.Cell.X * cs;
            float minY = building.Cell.Y * cs;
            float maxX = (building.Cell.X + def.Width) * cs;
            float maxY = (building.Cell.Y + def.Height) * cs;
            const float epsilon = 0.001f;
            return point.X >= minX - epsilon && point.X <= maxX + epsilon &&
                   point.Y >= minY - epsilon && point.Y <= maxY + epsilon;
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

        // Cycles per second of the building's first recipe at its current staffing.
        public static float ProductionPerSecond(GameState state, string buildingId, GameContentCatalog catalog)
        {
            var building = state.Buildings.Find(b => b.Id == buildingId);
            if (building == null) return 0f;
            var recipes = ActiveRecipes(building, catalog);
            return recipes.Count > 0 ? WorkPerSecond(state, buildingId, catalog) / recipes[0].Work : 0f;
        }

        // Share of the current cycle's work already done, 0..1; a stalled building has none.
        public static float CycleProgress(BuildingState building, GameContentCatalog catalog)
        {
            var definition = catalog.GetBuilding(building.Kind);
            var recipe = definition.Recipes.Count > 0 ? NextRecipe(building, definition, catalog) : null;
            return recipe != null ? Math.Min(1f, building.ProductionProgress / recipe.Work) : 0f;
        }

        // 100% stamina carries one unit per trip; the fractional remainder is banked for the next trip.
        public const int StaminaPercentPerOre = 100;

        /// <summary>Gold a sold creature returns: half its hiring price, rounded down.</summary>
        public static int UnitSaleRefund(UnitDefinition definition) =>
            definition != null ? (int)Math.Floor(definition.Price * 0.5f) : 0;

        public static float CarryCapacity(int staminaPercent)
        {
            return staminaPercent / (float)StaminaPercentPerOre;
        }

        // A hauler always lifts at least one unit, even below 100% stamina.
        private static int TripCarryCapacity(int carryBudgetPercent)
        {
            return Math.Max(1, carryBudgetPercent / StaminaPercentPerOre);
        }

        public static float UnitMovementSpeed(UnitDefinition unitDefinition, GameContentCatalog catalog)
        {
            if (unitDefinition == null || catalog == null) return 0f;
            return (120f + unitDefinition.Speed * 12f) / 48f * catalog.Economy.CellSize * catalog.Economy.WalkSpeedScale;
        }

        /// <summary>A creature's walking speed in the colony, with the guild's light step.</summary>
        public static float UnitMovementSpeed(GameState state, UnitDefinition unitDefinition, GameContentCatalog catalog) =>
            UpgradeRules.Raise(UnitMovementSpeed(unitDefinition, catalog),
                UpgradeRules.Total(state, catalog, UpgradeEffect.WalkSpeedPercent));

        public static Cell? FindFirstValidBuildingCell(GameState state, BuildingKind kind, GameContentCatalog catalog)
        {
            var economy = catalog.Economy;
            var definition = catalog.GetBuilding(kind);
            for (int y = 0; y <= economy.GridHeight - definition.Height; y++)
            {
                for (int x = 0; x <= economy.GridWidth - definition.Width; x++)
                {
                    var cell = new Cell(x, y);
                    if (ValidateBuildingPlacement(state, kind, cell, catalog).Ok)
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
