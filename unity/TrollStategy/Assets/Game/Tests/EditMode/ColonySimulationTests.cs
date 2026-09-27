using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.UI;
using UnityEngine;

namespace TrollStrategy.Tests
{
    public class ColonySimulationTests
    {
        private readonly List<ScriptableObject> _assets = new();
        private GameContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            var economy = Create<EconomyConfig>();
            economy.Init(14, 14, 1f, 1234, 20, 0.25f, 0.1f, 0.5f);

            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            var troll = Create<UnitDefinition>();
            troll.Init(UnitKind.Troll, "Тролль", 170, 9, 2f, 150);

            var mine = Create<BuildingDefinition>();
            mine.Init(BuildingKind.Mine, "Шахта", 200, 3, 3, 100, 5, null);
            mine.SetUpgrades(new[] { 150, 250, 350 }, 50, 2, 0);
            mine.SetConstructible(true);
            mine.SetRecipes(new ProductionRecipe(1f, null, Amounts(ResourceKind.IronOre, 1),
                4, new ResourceAmount(ResourceKind.VioletCrystal, 1)));
            var warehouse = Create<BuildingDefinition>();
            warehouse.Init(BuildingKind.Warehouse, "Склад", 0, 3, 3, 500, 0, null);
            warehouse.SetUpgrades(new[] { 120, 200, 280 }, 250, 0, 0);
            warehouse.SetStorageSlots(50);
            warehouse.SetStorage(StorageRole.Stockpile, ResourceKind.IronOre, ResourceKind.IronIngot, ResourceKind.VioletCrystal);
            var market = Create<BuildingDefinition>();
            market.Init(BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, null);
            market.SetUpgrades(new[] { 180, 300, 420 }, 0, 0, 1);
            market.SetStorage(StorageRole.Market);
            var barracks = Create<BuildingDefinition>();
            barracks.Init(BuildingKind.Barracks, "Бараки", 0, 3, 3, 0, 0, null);
            var forge = Create<BuildingDefinition>();
            forge.Init(BuildingKind.Forge, "Кузница", 180, 2, 2, 0, 0, null);
            forge.SetConstructible(true);
            var smeltery = Create<BuildingDefinition>();
            smeltery.Init(BuildingKind.Smeltery, "Плавильня", 220, 3, 3, 10, 4, null);
            smeltery.SetConstructible(true);
            smeltery.SetRecipes(new ProductionRecipe(2f, Amounts(ResourceKind.IronOre, 2), Amounts(ResourceKind.IronIngot, 1)));
            var armory = Create<BuildingDefinition>();
            armory.Init(BuildingKind.Armory, "Склад экипировки", 160, 2, 2, 0, 0, null);
            armory.SetConstructible(true);
            armory.SetStorage(StorageRole.Armory);

            var sword = Create<EquipmentDefinition>();
            sword.Init("iron-sword", "Железный меч", EquipmentSlot.Weapon, 2, 0, 0);

            _catalog = Create<GameContentCatalog>();
            _catalog.Init(economy, new[] { goblin, troll }, new[] { mine, warehouse, market, barracks, forge, smeltery, armory },
                equipment: new[] { sword },
                resources: new[]
                {
                    new ResourceDefinition(ResourceKind.IronOre, "Руда", 3),
                    new ResourceDefinition(ResourceKind.IronIngot, "Слиток", 9),
                    new ResourceDefinition(ResourceKind.VioletCrystal, "Кристалл", 30),
                    new ResourceDefinition(ResourceKind.IronSword, "Меч", 26, null, "iron-sword")
                });
        }

        [Test]
        public void BuildBuilding_AllowsOnlyConstructibleKinds()
        {
            var session = TestColony.NewSession(_catalog);
            int revision = session.CurrentSnapshot.Revision;

            foreach (var kind in new[] { BuildingKind.Warehouse, BuildingKind.Market, BuildingKind.Barracks })
            {
                var rejected = session.Dispatch(new BuildBuildingCommand(kind, new Cell(1, 1)));
                Assert.That(rejected.Ok, Is.False, kind.ToString());
                Assert.That(rejected.Error, Is.EqualTo("Эту постройку нельзя построить"));
            }
            Assert.That(session.CurrentSnapshot.Revision, Is.EqualTo(revision));

            Assert.That(session.Dispatch(new BuildBuildingCommand(BuildingKind.Forge, new Cell(1, 1))).Ok, Is.True);
            var forge = session.CurrentSnapshot.Buildings.Single(b => b.Kind == BuildingKind.Forge);
            Assert.That(forge.Width, Is.EqualTo(2));
            Assert.That(forge.MaxWorkers, Is.EqualTo(0));
            Assert.That(forge.UpgradeCost, Is.LessThan(0));
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(1234 - 180));
            Assert.That(session.Dispatch(new BuildBuildingCommand(BuildingKind.Forge, new Cell(2, 2))).Ok, Is.False,
                "Footprints must not overlap");
        }

        [Test]
        public void InteractionPlacement_BuildsTheChosenKindAndReturnsToNeutral()
        {
            var session = TestColony.NewSession(_catalog);
            var interaction = new InteractionController(session);

            interaction.BeginBuildingPlacement(BuildingKind.Forge);
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.PlacingBuilding));
            Assert.That(interaction.Mode.BuildingKind, Is.EqualTo(BuildingKind.Forge));

            interaction.PlaceBuilding(new Cell(10, 8));
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.PlacingBuilding),
                "An occupied cell keeps the player in placement mode");

            interaction.PlaceBuildingAutomatically();
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral));
            var forge = session.CurrentSnapshot.Buildings.Single(b => b.Kind == BuildingKind.Forge);
            Assert.That(session.CanPlaceBuilding(BuildingKind.Forge, forge.Cell).Ok, Is.False);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _assets.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(_assets[i]);
            _assets.Clear();
        }

        [Test]
        public void BuildingManagement_UpgradesMovesAndRefundsHalfOfInvestment()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuildMineCommand(new Cell(1, 1))).Ok, Is.True);
            var mine = session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Mine);
            Assert.That(session.Dispatch(new UpgradeBuildingCommand(mine.Id)).Ok, Is.True);
            mine = session.CurrentSnapshot.Buildings.First(b => b.Id == mine.Id);
            Assert.That(mine.Level, Is.EqualTo(2));
            Assert.That(mine.Capacity, Is.EqualTo(150));
            Assert.That(mine.MaxWorkers, Is.EqualTo(7));
            Assert.That(mine.RefundGold, Is.EqualTo(175));

            int goldBeforeInvalidMove = session.CurrentSnapshot.Gold;
            Assert.That(session.Dispatch(new MoveBuildingCommand(mine.Id, new Cell(10, 8))).Ok, Is.False);
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(goldBeforeInvalidMove));
            Assert.That(session.Dispatch(new MoveBuildingCommand(mine.Id, new Cell(4, 1))).Ok, Is.True);
            Assert.That(session.CurrentSnapshot.Buildings.First(b => b.Id == mine.Id).Cell, Is.EqualTo(new Cell(4, 1)));
            Assert.That(session.Dispatch(new DemolishBuildingCommand(mine.Id)).Ok, Is.True);
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(goldBeforeInvalidMove + 175));
        }

        [Test]
        public void InteractionMove_RelocatesInspectedBuildingOnMapClick()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuildMineCommand(new Cell(1, 1))).Ok, Is.True);
            var mine = session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Mine);
            var interaction = new InteractionController(session);

            interaction.SelectBuilding(mine.Id);
            interaction.BeginMoveInspectedBuilding();
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.MovingBuilding));
            Assert.That(interaction.Mode.BuildingId, Is.EqualTo(mine.Id));
            Assert.That(session.CanPlaceBuilding(BuildingKind.Mine, new Cell(2, 1), mine.Id).Ok, Is.True,
                "The preview ignores the building's own footprint");

            interaction.MoveBuilding(new Cell(10, 8));
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.MovingBuilding),
                "An invalid cell keeps the player in move mode");
            Assert.That(session.CurrentSnapshot.Buildings.First(b => b.Id == mine.Id).Cell, Is.EqualTo(new Cell(1, 1)));

            interaction.MoveBuilding(new Cell(4, 1));
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral));
            Assert.That(interaction.InspectedBuildingId, Is.EqualTo(mine.Id));
            Assert.That(session.CurrentSnapshot.Buildings.First(b => b.Id == mine.Id).Cell, Is.EqualTo(new Cell(4, 1)));
        }

        [Test]
        public void BuildingManagement_TracksWarehouseAndMarketLevels()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new UpgradeBuildingCommand("warehouse-1")).Ok, Is.True);
            Assert.That(session.Dispatch(new UpgradeBuildingCommand("market-1")).Ok, Is.True);
            Assert.That(session.CurrentSnapshot.Buildings.First(b => b.Id == "warehouse-1").Capacity, Is.EqualTo(750));
            Assert.That(session.CurrentSnapshot.Buildings.First(b => b.Id == "market-1").SaleBonus, Is.EqualTo(1));
            Assert.That(session.Dispatch(new UpgradeBuildingCommand("market-1")).Ok, Is.True);
            Assert.That(session.Dispatch(new UpgradeBuildingCommand("market-1")).Ok, Is.True);
            var market = session.CurrentSnapshot.Buildings.First(b => b.Id == "market-1");
            Assert.That(market.Level, Is.EqualTo(4));
            Assert.That(market.SaleBonus, Is.EqualTo(3));
            Assert.That(ColonySimulation.SalePrice(_catalog, ResourceKind.IronOre, market.Level), Is.EqualTo(6));
            Assert.That(session.Dispatch(new UpgradeBuildingCommand("market-1")).Ok, Is.False);
        }

        [Test]
        public void BuildingEntrance_IsOnVisibleSouthFacade()
        {
            foreach (var kind in new[] { BuildingKind.Mine, BuildingKind.Warehouse, BuildingKind.Market, BuildingKind.Barracks })
            {
                var building = new BuildingState { Id = kind.ToString(), Kind = kind, Cell = new Cell(4, 5) };
                var entrance = ColonySimulation.BuildingEntrancePosition(building, _catalog);
                Assert.That(entrance.X, Is.EqualTo(5.5f).Within(.001f), kind.ToString());
                Assert.That(entrance.Y, Is.EqualTo(5.35f).Within(.001f), kind.ToString());
            }
        }

        [Test]
        public void StartingLayout_CreatesBuildingsWithPerKindIdsInLayoutOrder()
        {
            var session = new GameSession(_catalog, new[]
            {
                new StartingBuilding(BuildingKind.Market, new Cell(0, 1)),
                new StartingBuilding(BuildingKind.Warehouse, new Cell(5, 5)),
                new StartingBuilding(BuildingKind.Market, new Cell(0, 4))
            });

            Assert.That(session.StartingBuildingIds, Is.EqualTo(new[] { "market-1", "warehouse-1", "market-2" }));
            var buildings = session.CurrentSnapshot.Buildings;
            Assert.That(buildings.Select(b => b.Id), Is.EqualTo(session.StartingBuildingIds));
            Assert.That(buildings[1].Cell, Is.EqualTo(new Cell(5, 5)));
            Assert.That(buildings.All(b => b.Level == 1), Is.True);
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(_catalog.Economy.StartingGold));
        }

        [Test]
        public void StartingLayout_UsesPlacementRules()
        {
            Assert.Throws<System.InvalidOperationException>(() => new GameSession(_catalog, new[]
            {
                new StartingBuilding(BuildingKind.Warehouse, new Cell(4, 4)),
                new StartingBuilding(BuildingKind.Market, new Cell(5, 5))
            }), "overlapping footprints");
            Assert.Throws<System.InvalidOperationException>(() => new GameSession(_catalog, new[]
            {
                new StartingBuilding(BuildingKind.Warehouse, new Cell(12, 0))
            }), "footprint outside the grid");
        }

        [Test]
        public void StartingLayout_CanBeEmpty()
        {
            var session = new GameSession(_catalog, new StartingBuilding[0]);
            Assert.That(session.CurrentSnapshot.Buildings, Is.Empty);
            Assert.That(session.StartingBuildingIds, Is.Empty);
        }

        [Test]
        public void BuildingEntrance_UsesDefinitionEntrance()
        {
            var forge = _catalog.GetBuilding(BuildingKind.Forge);
            forge.SetEntrance(new Vector2(2f, 1.25f));
            var building = new BuildingState { Id = "forge", Kind = BuildingKind.Forge, Cell = new Cell(4, 5) };

            var entrance = ColonySimulation.BuildingEntrancePosition(building, _catalog);
            Assert.That(entrance.X, Is.EqualTo(6f).Within(.001f));
            Assert.That(entrance.Y, Is.EqualTo(6.25f).Within(.001f));
        }

        [Test]
        public void BuildingEntrance_MayStandInFrontButNotFarFromTheBuilding()
        {
            var forge = _catalog.GetBuilding(BuildingKind.Forge);
            forge.SetEntrance(new Vector2(1f, -0.45f));
            Assert.That(forge.EntranceY, Is.EqualTo(-0.45f));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => forge.SetEntrance(new Vector2(3.5f, 1f)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => forge.SetEntrance(new Vector2(1f, -1.1f)));
            Assert.That(forge.EntranceY, Is.EqualTo(-0.45f), "a rejected entrance leaves the previous one");
        }

        [Test]
        public void Session_UsesCatalogStartingGoldAndCanonicalPrices()
        {
            var session = TestColony.NewSession(_catalog);

            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(1234));
            Assert.That(session.Dispatch(new BuildMineCommand(new Cell(3, 3))).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 2, new Cell(7, 7))).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, new Cell(8, 7))).Ok, Is.True);

            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(784));
        }

        [Test]
        public void RejectedCommand_DoesNotChangeStateOrRevision()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(7, 7))).Ok, Is.True);
            var before = session.CurrentSnapshot;

            var result = session.Dispatch(new AssignWorkCommand(new[] { "unit-1", "missing-unit" }, "mine-1"));
            var after = session.CurrentSnapshot;

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo("Юнит не найден"));
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.Gold, Is.EqualTo(before.Gold));
            Assert.That(after.Units.Single().Assignment.Kind, Is.EqualTo(AssignmentKind.Idle));
        }

        [Test]
        public void DuplicateUnitIds_AreRejectedWithoutMutation()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(7, 7))).Ok, Is.True);
            var before = session.CurrentSnapshot;

            var result = session.Dispatch(new ReleaseUnitsCommand(new[] { "unit-1", "unit-1" }));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo("Юнит выбран дважды"));
            Assert.That(session.CurrentSnapshot.Revision, Is.EqualTo(before.Revision));
        }

        [Test]
        public void AssignWork_RespectsMineCapacityAsACompleteCommand()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuildMineCommand(new Cell(3, 3))).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 6, new Cell(7, 7))).Ok, Is.True);

            var result = session.Dispatch(new AssignWorkCommand(
                new[] { "unit-1", "unit-2", "unit-3", "unit-4", "unit-5", "unit-6" }, "mine-1"));

            Assert.That(result.Ok, Is.True);
            Assert.That(session.CurrentSnapshot.Units.Count(u => u.Assignment.Kind == AssignmentKind.ToWork), Is.EqualTo(5));
            Assert.That(session.CurrentSnapshot.Units.Single(u => u.Id == "unit-6").Assignment.Kind, Is.EqualTo(AssignmentKind.Idle));
        }

        [Test]
        public void AssignWork_WorkerStopsAtTheEntranceAnchorNotInsideTheBuilding()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuildMineCommand(new Cell(3, 3))).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(7, 7))).Ok, Is.True);

            Assert.That(session.Dispatch(new AssignWorkCommand(new[] { "unit-1" }, "mine-1")).Ok, Is.True);
            session.Advance(10f);

            var unit = session.CurrentSnapshot.Units.Single();
            var mine = session.CurrentSnapshot.Buildings.Single(b => b.Id == "mine-1");
            var entrance = ColonySimulation.BuildingEntrancePosition(
                new BuildingState { Id = mine.Id, Kind = mine.Kind, Cell = mine.Cell }, _catalog);
            Assert.That(unit.Assignment.Kind, Is.EqualTo(AssignmentKind.Work));
            Assert.That(unit.Position, Is.EqualTo(entrance));
        }

        [Test]
        public void SquadOfHaulers_LoadsOneAtATimeAndWaitsAsAGroup()
        {
            var state = TestColony.NewState(_catalog);
            state.Buildings.Add(new BuildingState { Id = "mine-1", Kind = BuildingKind.Mine, Cell = new Cell(3, 3), Ore = 50 });
            var ids = new List<string>();
            for (int i = 1; i <= 5; i++)
            {
                ids.Add($"unit-{i}");
                state.Units.Add(new UnitState
                {
                    Id = $"unit-{i}",
                    Kind = UnitKind.Goblin,
                    Position = new WorldPosition(7.5f, 7.5f),
                    Assignment = Assignment.Idle()
                });
            }
            Assert.That(ColonySimulation.ApplyCommand(
                state, new AssignHaulCommand(ids, "mine-1", "warehouse-1"), _catalog).Ok, Is.True);

            var departures = new Dictionary<string, int>();
            float step = _catalog.Economy.StepTimeSeconds;
            for (int tick = 0; tick < 80 && departures.Count < ids.Count; tick++)
            {
                ColonySimulation.TickColony(state, step, _catalog);

                int atDock = state.Units.Count(u =>
                    u.Assignment.Phase == HaulPhase.Loading || u.Assignment.Phase == HaulPhase.ToDock);
                Assert.That(atDock, Is.LessThanOrEqualTo(1), $"tick {tick}: only one hauler may use the dock");

                var queued = state.Units.Where(u => u.Assignment.Phase == HaulPhase.QueuedAtSource).ToList();
                Assert.That(queued.Select(u => u.Assignment.QueueTicket).Distinct().Count(), Is.EqualTo(queued.Count));

                foreach (var u in state.Units)
                    if (!departures.ContainsKey(u.Id) && u.Assignment.Phase == HaulPhase.ToDestination)
                        departures[u.Id] = tick;
            }

            Assert.That(departures.Count, Is.EqualTo(5), "every hauler should get a turn at the dock");
            var ticks = departures.Values.OrderBy(t => t).ToArray();
            for (int i = 1; i < ticks.Length; i++)
                Assert.That(ticks[i] - ticks[i - 1], Is.GreaterThanOrEqualTo((int)(_catalog.Economy.TransferTimeSeconds / step)),
                    "haulers must leave the source staggered by a full load");

            var positions = state.Units.Select(u => u.Position).ToList();
            for (int i = 0; i < positions.Count; i++)
            for (int j = i + 1; j < positions.Count; j++)
            {
                float dx = positions[i].X - positions[j].X;
                float dy = positions[i].Y - positions[j].Y;
                Assert.That(dx * dx + dy * dy, Is.GreaterThan(0.01f), $"{state.Units[i].Id} overlaps {state.Units[j].Id}");
            }
        }

        [Test]
        public void InteractionSelection_ReplacesTogglesAndAddsOnlyExistingUnits()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 3, new Cell(7, 7))).Ok, Is.True);
            var interaction = new InteractionController(session);

            interaction.ClickUnit("unit-1", false);
            interaction.ClickUnit("unit-2", true);
            interaction.ClickUnit("unit-1", true);
            interaction.SelectUnits(new[] { "unit-3", "missing-unit" }, true);

            Assert.That(interaction.SelectedIds, Is.EquivalentTo(new[] { "unit-2", "unit-3" }));
        }

        [Test]
        public void InteractionSelection_OpensUnitCardOnlyOnSquadInspect()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 3, new Cell(7, 7))).Ok, Is.True);
            var interaction = new InteractionController(session);

            interaction.ClickUnit("unit-1", false);
            Assert.That(interaction.InspectedUnitId, Is.Null);

            interaction.SelectUnits(new[] { "unit-1" });
            Assert.That(interaction.InspectedUnitId, Is.Null);

            interaction.InspectSquad("unit-2", new[] { "unit-1", "unit-2", "unit-3", "missing-unit" });
            Assert.That(interaction.InspectedUnitId, Is.EqualTo("unit-2"));
            Assert.That(interaction.SelectedIds, Is.EquivalentTo(new[] { "unit-1", "unit-2", "unit-3" }));
        }

        [Test]
        public void Hauler_WithFullStamina_CarriesOneOrePerTrip()
        {
            var state = StateWithMineAndHauler(HaulPhase.Loading, carried: 0);
            state.Buildings.Single(b => b.Id == "mine-1").Ore = 20;

            Assert.That(LoadTrips(state, 4), Is.EqualTo(new[] { 1, 1, 1, 1 }));
            Assert.That(state.Buildings.Single(b => b.Id == "mine-1").Ore, Is.EqualTo(16));
        }

        [Test]
        public void Hauler_WithOneAndAHalfStamina_AveragesOneAndAHalfOrePerTrip()
        {
            var state = StateWithMineAndHauler(HaulPhase.Loading, carried: 0);
            state.Units.Single().Kind = UnitKind.Troll;
            state.Buildings.Single(b => b.Id == "mine-1").Ore = 20;

            Assert.That(LoadTrips(state, 4), Is.EqualTo(new[] { 1, 2, 1, 2 }));
            Assert.That(state.Buildings.Single(b => b.Id == "mine-1").Ore, Is.EqualTo(14));
        }

        [Test]
        public void MineProduction_ScalesWithWorkerStrength()
        {
            var state = StateWithMineAndHauler(HaulPhase.Loading, carried: 0);
            state.Units.Single().Assignment = Assignment.Work("mine-1");
            Assert.That(ColonySimulation.ProductionPerSecond(state, "mine-1", _catalog), Is.EqualTo(0.3f).Within(0.0001f));

            state.Units.Single().Kind = UnitKind.Troll;
            Assert.That(ColonySimulation.ProductionPerSecond(state, "mine-1", _catalog), Is.EqualTo(0.9f).Within(0.0001f));
        }

        [Test]
        public void UnitStats_ShowStaminaAndDerivedCarryCapacity()
        {
            string stats = UnitStatsText.Compact(_catalog.GetUnit(UnitKind.Troll));

            Assert.That(stats, Does.Contain("Сила 9"));
            Assert.That(stats, Does.Contain("Выносливость 150%"));
            Assert.That(stats, Does.Contain("груз 1.5"));
        }

        [Test]
        public void FullWarehouse_PreventsPickupAtSource()
        {
            var state = StateWithMineAndHauler(HaulPhase.Loading, carried: 0);
            state.Buildings.Single(b => b.Id == "mine-1").Ore = 25;
            state.Buildings.Single(b => b.Id == "warehouse-1").Ore = 500;

            ColonySimulation.TickColony(state, 0.5f, _catalog);

            var unit = state.Units.Single();
            Assert.That(state.Buildings.Single(b => b.Id == "mine-1").Ore, Is.EqualTo(25));
            Assert.That(unit.Assignment.Carried, Is.Zero);
            Assert.That(unit.Assignment.Phase, Is.EqualTo(HaulPhase.Loading));
        }

        [Test]
        public void WarehouseBecomingFull_DoesNotDiscardCarriedOre()
        {
            var state = StateWithMineAndHauler(HaulPhase.Unloading, carried: 10);
            state.Buildings.Single(b => b.Id == "warehouse-1").Ore = 500;

            ColonySimulation.TickColony(state, 0.5f, _catalog);

            var assignment = state.Units.Single().Assignment;
            Assert.That(assignment.Carried, Is.EqualTo(10));
            Assert.That(assignment.Phase, Is.EqualTo(HaulPhase.Unloading));
        }

        [Test]
        public void ReassigningHauler_ReturnsCargoToItsSource()
        {
            var state = StateWithMineAndHauler(HaulPhase.ToDestination, carried: 10);
            state.Buildings.Single(b => b.Id == "mine-1").Ore = 40;

            var result = ColonySimulation.ApplyCommand(
                state, new ReleaseUnitsCommand(new[] { "unit-1" }), _catalog);

            Assert.That(result.Ok, Is.True);
            Assert.That(state.Buildings.Single(b => b.Id == "mine-1").Ore, Is.EqualTo(50));
            Assert.That(state.Buildings.Single(b => b.Id == "warehouse-1").Ore, Is.Zero);
            Assert.That(state.Units.Single().Assignment.Kind, Is.EqualTo(AssignmentKind.Idle));
        }

        [Test]
        public void SnapshotAssignment_CannotMutateSessionState()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(7, 7))).Ok, Is.True);
            var exposedAssignment = session.CurrentSnapshot.Units.Single().Assignment;

            exposedAssignment.Kind = AssignmentKind.Haul;
            exposedAssignment.Carried = 999;

            Assert.That(session.CurrentSnapshot.Units.Single().Assignment.Kind, Is.EqualTo(AssignmentKind.Idle));
            Assert.That(session.CurrentSnapshot.TotalOre, Is.Zero);
        }

        private int[] LoadTrips(GameState state, int trips)
        {
            var assignment = state.Units.Single().Assignment;
            var carried = new int[trips];
            for (int i = 0; i < trips; i++)
            {
                assignment.Phase = HaulPhase.Loading;
                assignment.PhaseElapsedSeconds = 0f;
                assignment.Carried = 0;
                ColonySimulation.TickColony(state, _catalog.Economy.TransferTimeSeconds, _catalog);
                Assert.That(assignment.Phase, Is.EqualTo(HaulPhase.ToDestination));
                carried[i] = assignment.Carried;
            }
            return carried;
        }

        [Test]
        public void StorageSlots_StartANewSlotPerGoodAndFillFrontToBack()
        {
            var stock = new Dictionary<ResourceKind, int> { [ResourceKind.IronIngot] = 30, [ResourceKind.IronOre] = 75 };
            var slots = StorageSlots.Fill(stock, 200, 50);

            Assert.That(slots.Select(slot => slot.Amount), Is.EqualTo(new[] { 50, 25, 30, 0 }));
            Assert.That(slots.Take(3).Select(slot => slot.Resource),
                Is.EqualTo(new[] { ResourceKind.IronOre, ResourceKind.IronOre, ResourceKind.IronIngot }));
            Assert.That(StorageSlots.Room(stock, ResourceKind.IronOre, 200, 50), Is.EqualTo(25 + 50));
            Assert.That(StorageSlots.Room(stock, ResourceKind.IronIngot, 200, 50), Is.EqualTo(20 + 50));
            Assert.That(StorageSlots.Room(stock, ResourceKind.VioletCrystal, 200, 50), Is.EqualTo(50));
            Assert.That(StorageSlots.Fill(stock, 120, 50).Select(slot => slot.Amount), Is.EqualTo(new[] { 50, 25, 20 }),
                "The last slot holds only the remaining capacity");
            Assert.That(StorageSlots.Fill(stock, 100, 0), Is.Empty, "Stack size 0 disables the slot inventory");
        }

        [Test]
        public void WarehouseSnapshot_ExposesInventorySlotsThatGrowWithLevel()
        {
            var session = TestColony.NewSession(_catalog);
            var warehouse = session.CurrentSnapshot.Buildings.Single(b => b.Kind == BuildingKind.Warehouse);
            Assert.That(warehouse.SlotStackSize, Is.EqualTo(50));
            Assert.That(warehouse.Slots.Count, Is.EqualTo(10));
            Assert.That(warehouse.Slots.All(slot => slot.IsEmpty), Is.True);
            Assert.That(session.CurrentSnapshot.Buildings.Single(b => b.Kind == BuildingKind.Market).Slots,
                Is.Empty);

            Assert.That(session.Dispatch(new UpgradeBuildingCommand(warehouse.Id)).Ok, Is.True);
            Assert.That(session.CurrentSnapshot.Buildings.Single(b => b.Id == warehouse.Id).Slots.Count,
                Is.EqualTo(15));
        }

        [Test]
        public void HaulRoutes_FollowGoodsEachBuildingHandsOutAndTakes()
        {
            Assert.That(ColonySimulation.IsValidHaulRoute(BuildingKind.Mine, BuildingKind.Warehouse, _catalog), Is.True);
            Assert.That(ColonySimulation.IsValidHaulRoute(BuildingKind.Mine, BuildingKind.Smeltery, _catalog), Is.True);
            Assert.That(ColonySimulation.IsValidHaulRoute(BuildingKind.Warehouse, BuildingKind.Smeltery, _catalog), Is.True);
            Assert.That(ColonySimulation.IsValidHaulRoute(BuildingKind.Smeltery, BuildingKind.Market, _catalog), Is.True);
            Assert.That(ColonySimulation.IsValidHaulRoute(BuildingKind.Smeltery, BuildingKind.Mine, _catalog), Is.False,
                "A mine takes no goods");
            Assert.That(ColonySimulation.IsValidHaulRoute(BuildingKind.Mine, BuildingKind.Armory, _catalog), Is.False,
                "Ore is not equipment");
            Assert.That(ColonySimulation.IsValidHaulRoute(BuildingKind.Market, BuildingKind.Warehouse, _catalog), Is.False);
        }

        [Test]
        public void Smeltery_ConsumesOreAndReleasesIngotsAtomically()
        {
            var state = TestColony.NewState(_catalog);
            var smeltery = new BuildingState { Id = "smeltery-1", Kind = BuildingKind.Smeltery, Cell = new Cell(1, 1) };
            smeltery.SetStock(ResourceKind.IronOre, 5);
            state.Buildings.Add(smeltery);
            state.Units.Add(new UnitState
            {
                Id = "unit-1", Kind = UnitKind.Troll, Position = new WorldPosition(2.5f, 1.35f),
                Assignment = Assignment.Work("smeltery-1")
            });
            Assert.That(ColonySimulation.DescribeProduction(state, smeltery, _catalog), Is.EqualTo(ProductionState.Working));

            // A troll adds 0.9 work/s; a 2-work cycle completes after ~2.2 s.
            ColonySimulation.TickColony(state, 2f, _catalog);
            Assert.That(smeltery.GetStock(ResourceKind.IronIngot), Is.Zero);
            Assert.That(smeltery.GetStock(ResourceKind.IronOre), Is.EqualTo(5));
            ColonySimulation.TickColony(state, 0.5f, _catalog);
            Assert.That(smeltery.GetStock(ResourceKind.IronIngot), Is.EqualTo(1));
            Assert.That(smeltery.GetStock(ResourceKind.IronOre), Is.EqualTo(3));

            ColonySimulation.TickColony(state, 10f, _catalog);
            Assert.That(smeltery.GetStock(ResourceKind.IronIngot), Is.EqualTo(2));
            Assert.That(smeltery.GetStock(ResourceKind.IronOre), Is.EqualTo(1), "One ore is not enough for a cycle");
            Assert.That(smeltery.ProductionProgress, Is.Zero, "A starved building banks no work");
            Assert.That(ColonySimulation.DescribeProduction(state, smeltery, _catalog), Is.EqualTo(ProductionState.MissingInputs));
        }

        [Test]
        public void FullOutput_StopsProductionWithoutLosingGoods()
        {
            var state = TestColony.NewState(_catalog);
            var smeltery = new BuildingState { Id = "smeltery-1", Kind = BuildingKind.Smeltery, Cell = new Cell(1, 1) };
            smeltery.SetStock(ResourceKind.IronOre, 4);
            smeltery.SetStock(ResourceKind.IronIngot, 10);
            state.Buildings.Add(smeltery);
            state.Units.Add(new UnitState { Id = "unit-1", Kind = UnitKind.Troll, Assignment = Assignment.Work("smeltery-1") });

            ColonySimulation.TickColony(state, 10f, _catalog);

            Assert.That(smeltery.GetStock(ResourceKind.IronOre), Is.EqualTo(4));
            Assert.That(smeltery.GetStock(ResourceKind.IronIngot), Is.EqualTo(10));
            Assert.That(ColonySimulation.DescribeProduction(state, smeltery, _catalog), Is.EqualTo(ProductionState.OutputFull));
        }

        [Test]
        public void Mine_AddsCrystalOnEveryFourthCycle()
        {
            var state = StateWithMineAndHauler(HaulPhase.ToSource, carried: 0);
            state.Units.Single().Assignment = Assignment.Work("mine-1");
            var mine = state.Buildings.Single(b => b.Id == "mine-1");

            // A goblin adds 0.3 ore/s: eight cycles need a little under 27 s.
            for (int i = 0; i < 108; i++)
                ColonySimulation.TickColony(state, 0.25f, _catalog);

            Assert.That(mine.Ore, Is.EqualTo(8));
            Assert.That(mine.GetStock(ResourceKind.VioletCrystal), Is.EqualTo(2));
        }

        [Test]
        public void Hauler_CarriesIngotsToMarketAtTheIngotPrice()
        {
            var state = TestColony.NewState(_catalog);
            var smeltery = new BuildingState { Id = "smeltery-1", Kind = BuildingKind.Smeltery, Cell = new Cell(1, 1) };
            smeltery.SetStock(ResourceKind.IronOre, 6);
            smeltery.SetStock(ResourceKind.IronIngot, 3);
            state.Buildings.Add(smeltery);
            state.Units.Add(new UnitState
            {
                Id = "unit-1", Kind = UnitKind.Goblin, Position = new WorldPosition(2.5f, 1.35f),
                Assignment = Assignment.Haul("smeltery-1", "market-1")
            });
            state.Units[0].Assignment.Phase = HaulPhase.Loading;

            ColonySimulation.TickColony(state, _catalog.Economy.TransferTimeSeconds, _catalog);
            var assignment = state.Units[0].Assignment;
            Assert.That(assignment.CarriedResource, Is.EqualTo(ResourceKind.IronIngot), "Inputs are never picked up");
            Assert.That(smeltery.GetStock(ResourceKind.IronOre), Is.EqualTo(6));

            int gold = state.Gold;
            assignment.Phase = HaulPhase.Unloading;
            assignment.PhaseElapsedSeconds = 0f;
            ColonySimulation.TickColony(state, _catalog.Economy.TransferTimeSeconds, _catalog);
            Assert.That(state.Gold, Is.EqualTo(gold + 9));
            Assert.That(state.SoldGoods, Is.EqualTo(1));
        }

        [Test]
        public void Armory_TurnsDeliveredSwordsIntoInventoryItems()
        {
            var state = TestColony.NewState(_catalog);
            state.Buildings.Add(new BuildingState { Id = "armory-1", Kind = BuildingKind.Armory, Cell = new Cell(1, 1) });
            state.Units.Add(new UnitState
            {
                Id = "unit-1", Kind = UnitKind.Troll, Position = new WorldPosition(2f, 1.35f),
                Assignment = Assignment.Haul("warehouse-1", "armory-1")
            });
            var assignment = state.Units[0].Assignment;
            assignment.Phase = HaulPhase.Unloading;
            assignment.Carried = 2;
            assignment.CarriedResource = ResourceKind.IronSword;

            ColonySimulation.TickColony(state, _catalog.Economy.TransferTimeSeconds, _catalog);

            Assert.That(assignment.Carried, Is.Zero);
            Assert.That(state.Equipment.Select(item => item.DefinitionId), Is.EqualTo(new[] { "iron-sword", "iron-sword" }));
            Assert.That(state.Equipment.Select(item => item.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(state.Equipment.All(item => item.OwnerUnitId == null), Is.True);
        }

        [Test]
        public void AssignWork_AcceptsAnyStaffedProducerButNotStorage()
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuildBuildingCommand(BuildingKind.Smeltery, new Cell(1, 1))).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(7, 7))).Ok, Is.True);
            var smeltery = session.CurrentSnapshot.Buildings.Single(b => b.Kind == BuildingKind.Smeltery);
            Assert.That(smeltery.IsWorkplace, Is.True);

            var rejected = session.Dispatch(new AssignWorkCommand(new[] { "unit-1" }, "warehouse-1"));
            Assert.That(rejected.Ok, Is.False);
            Assert.That(session.Dispatch(new AssignWorkCommand(new[] { "unit-1" }, smeltery.Id)).Ok, Is.True);
        }

        private static ResourceAmount[] Amounts(ResourceKind resource, int amount) =>
            new[] { new ResourceAmount(resource, amount) };

        private GameState StateWithMineAndHauler(HaulPhase phase, int carried)
        {
            var state = TestColony.NewState(_catalog);
            state.Buildings.Add(new BuildingState
            {
                Id = "mine-1",
                Kind = BuildingKind.Mine,
                Cell = new Cell(3, 3)
            });
            state.Units.Add(new UnitState
            {
                Id = "unit-1",
                Kind = UnitKind.Goblin,
                Position = new WorldPosition(4.5f, 5.65f),
                Assignment = Assignment.Haul("mine-1", "warehouse-1")
            });
            state.Units[0].Assignment.Phase = phase;
            state.Units[0].Assignment.Carried = carried;
            return state;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
