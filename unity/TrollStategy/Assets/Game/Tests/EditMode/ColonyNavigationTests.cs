using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrollStrategy.Tests
{
    public class ColonyNavigationTests
    {
        private const float StepSeconds = 0.01f;
        private readonly List<ScriptableObject> _assets = new();
        private GameContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            var economy = Create<EconomyConfig>();
            economy.Init(14, 14, 1f, 1000, 20, 0.25f, 0.1f, 0.5f);
            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            var mine = Create<BuildingDefinition>();
            mine.Init(BuildingKind.Mine, "Шахта", 200, 3, 3, 100, 5, null);
            mine.SetConstructible(true);
            mine.SetRecipes(new ProductionRecipe(1f, null, new[] { new ResourceAmount(ResourceKind.IronOre, 1) }));
            var warehouse = Create<BuildingDefinition>();
            warehouse.Init(BuildingKind.Warehouse, "Склад", 0, 3, 3, 500, 0, null);
            warehouse.SetStorage(StorageRole.Stockpile, ResourceKind.IronOre);
            var market = Create<BuildingDefinition>();
            market.Init(BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, null);
            market.SetStorage(StorageRole.Market);
            var forge = Create<BuildingDefinition>();
            forge.Init(BuildingKind.Forge, "Кузница", 180, 2, 2, 0, 0, null);
            forge.SetConstructible(true);

            _catalog = Create<GameContentCatalog>();
            _catalog.Init(economy, new[] { goblin }, new[] { mine, warehouse, market, forge },
                resources: new[] { new ResourceDefinition(ResourceKind.IronOre, "Руда", 3) });
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _assets.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(_assets[i]);
            _assets.Clear();
        }

        [Test]
        public void Worker_WalksAroundTheBuildingAndEntersThroughItsDoor()
        {
            var state = TestColony.NewState(_catalog);
            var mine = Place(state, BuildingKind.Mine, new Cell(4, 4));
            // North of the mine; its door faces south, so the straight line crosses the footprint.
            var worker = AddUnit(state, new WorldPosition(5.5f, 9.5f), Assignment.ToWork(mine.Id));

            WalkUntil(state, worker, () => worker.Assignment.Kind == AssignmentKind.Work);

            Assert.That(worker.Position, Is.EqualTo(ColonySimulation.BuildingEntrancePosition(mine, _catalog)));
        }

        [Test]
        public void Hauler_LeavesAndEntersBuildingsOnlyThroughDoors()
        {
            var state = TestColony.NewState(_catalog);
            var mine = Place(state, BuildingKind.Mine, new Cell(4, 4));
            mine.AddStock(ResourceKind.IronOre, 3);
            var warehouse = state.Buildings.Find(b => b.Kind == BuildingKind.Warehouse);
            var hauler = AddUnit(state, new WorldPosition(5.5f, 9.5f), Assignment.Haul(mine.Id, warehouse.Id));

            WalkUntil(state, hauler, () => warehouse.GetStock(ResourceKind.IronOre) >= 2);
        }

        [Test]
        public void Placement_KeepsEveryDoorApproachOpen()
        {
            var state = TestColony.NewState(_catalog);
            // The market at (10, 2) opens south onto cell (11, 1).
            var coversMarketDoor = ColonySimulation.ValidateBuildingPlacement(state, BuildingKind.Forge, new Cell(11, 0), _catalog);
            Assert.That(coversMarketDoor.Ok, Is.False);
            Assert.That(coversMarketDoor.Error, Is.EqualTo("Постройка перекроет вход в другое здание"));

            var doorOffTheField = ColonySimulation.ValidateBuildingPlacement(state, BuildingKind.Forge, new Cell(0, 0), _catalog);
            Assert.That(doorOffTheField.Ok, Is.False);
            Assert.That(doorOffTheField.Error, Is.EqualTo("Вход в постройку будет перекрыт"));

            Place(state, BuildingKind.Forge, new Cell(5, 2));
            var doorIntoForge = ColonySimulation.ValidateBuildingPlacement(state, BuildingKind.Forge, new Cell(5, 4), _catalog);
            Assert.That(doorIntoForge.Ok, Is.False);
            Assert.That(doorIntoForge.Error, Is.EqualTo("Вход в постройку будет перекрыт"));

            Assert.That(ColonySimulation.ValidateBuildingPlacement(state, BuildingKind.Forge, new Cell(1, 5), _catalog).Ok, Is.True);
        }

        [Test]
        public void Deliveries_GatherAsACrowdInFrontOfTheDoor()
        {
            var state = TestColony.NewState(_catalog);
            var mine = Place(state, BuildingKind.Mine, new Cell(4, 4));
            var warehouse = state.Buildings.Find(b => b.Kind == BuildingKind.Warehouse);
            var haulers = new List<UnitState>();
            for (int i = 0; i < 6; i++)
            {
                var assignment = Assignment.Haul(mine.Id, warehouse.Id);
                assignment.Phase = HaulPhase.ToDestination;
                assignment.Carried = 1;
                assignment.CarriedResource = ResourceKind.IronOre;
                haulers.Add(AddUnit(state, new WorldPosition(1.5f + i * 0.1f, 12.5f), assignment));
            }

            for (int tick = 0; tick < 2000 && haulers.Exists(h => h.Assignment.Phase != HaulPhase.Unloading); tick++)
                ColonySimulation.TickColony(state, StepSeconds, _catalog);

            Assert.That(haulers.TrueForAll(h => h.Assignment.Phase == HaulPhase.Unloading), Is.True, "all six reach the door");
            for (int i = 0; i < haulers.Count; i++)
            {
                Assert.That(ColonySimulation.IsPointWithinBuilding(warehouse, haulers[i].Position, _catalog), Is.False,
                    $"{haulers[i].Id} waits outside the warehouse");
                for (int j = i + 1; j < haulers.Count; j++)
                    Assert.That(Distance(haulers[i].Position, haulers[j].Position),
                        Is.GreaterThanOrEqualTo(MinGap(warehouse)),
                        $"{haulers[i].Id} and {haulers[j].Id} share a place");
            }
        }

        [Test]
        public void CrowdPlaces_AreScatteredInFrontOfTheDoorButNeverTooClose()
        {
            var state = TestColony.NewState(_catalog);
            foreach (var building in state.Buildings)
            {
                var doorway = ColonyNavigation.DoorwayOf(building, _catalog);
                var places = new List<WorldPosition>();
                for (int slot = 0; slot < 24; slot++)
                    places.Add(ColonyNavigation.CrowdSlotPosition(building, slot, _catalog));

                bool anyOffLine = false;
                for (int i = 0; i < places.Count; i++)
                {
                    float outward = (places[i].X - doorway.Approach.X) * doorway.NormalX +
                                    (places[i].Y - doorway.Approach.Y) * doorway.NormalY;
                    Assert.That(outward, Is.GreaterThan(0f), $"{building.Id} place {i} is in front of the door");
                    float side = (places[i].X - doorway.Approach.X) * doorway.TangentX +
                                 (places[i].Y - doorway.Approach.Y) * doorway.TangentY;
                    anyOffLine |= Math.Abs(side) > 0.2f;
                    for (int j = i + 1; j < places.Count; j++)
                        Assert.That(Distance(places[i], places[j]), Is.GreaterThanOrEqualTo(MinGap(building)),
                            $"{building.Id} places {i} and {j}");
                }
                Assert.That(anyOffLine, Is.True, "the group spreads sideways, not in a line");
            }
        }

        [Test]
        public void CrowdSpacing_FromTheDefinitionPacksTheGroupCloserOrLooser()
        {
            var state = TestColony.NewState(_catalog);
            var warehouse = state.Buildings.Find(b => b.Kind == BuildingKind.Warehouse);
            var definition = _catalog.GetBuilding(BuildingKind.Warehouse);
            var approach = ColonyNavigation.DoorwayOf(warehouse, _catalog).Approach;

            definition.SetCrowdSpacing(0.15f);
            float tight = GroupReach(warehouse, approach);
            definition.SetCrowdSpacing(0.45f);
            float loose = GroupReach(warehouse, approach);

            Assert.That(loose, Is.EqualTo(tight * 3f).Within(0.01f), "every distance in the group scales with the spacing");
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.SetCrowdSpacing(0.05f));
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.SetCrowdSpacing(1.5f));
            Assert.That(definition.CrowdSpacingCells, Is.EqualTo(0.45f));
        }

        private float GroupReach(BuildingState building, WorldPosition approach)
        {
            float furthest = 0f;
            for (int slot = 0; slot < 8; slot++)
                furthest = Mathf.Max(furthest, Distance(approach, ColonyNavigation.CrowdSlotPosition(building, slot, _catalog)));
            return furthest;
        }

        [Test]
        public void EntranceInFrontOfTheBuilding_IsWalkedToWithoutEnteringIt()
        {
            _catalog.GetBuilding(BuildingKind.Market).SetEntrance(new Vector2(1.5f, -0.45f));
            var state = TestColony.NewState(_catalog);
            var market = state.Buildings.Find(b => b.Kind == BuildingKind.Market);
            var doorway = ColonyNavigation.DoorwayOf(market, _catalog);
            Assert.That(doorway.Approach, Is.EqualTo(doorway.Entrance), "an outside entrance is its own approach");
            Assert.That(doorway.NormalY, Is.EqualTo(-1f));

            var mine = Place(state, BuildingKind.Mine, new Cell(4, 4));
            mine.AddStock(ResourceKind.IronOre, 3);
            var warehouse = state.Buildings.Find(b => b.Kind == BuildingKind.Warehouse);
            warehouse.AddStock(ResourceKind.IronOre, 3);
            // North of the market: the walk has to go round it to the entrance on its south side.
            var hauler = AddUnit(state, new WorldPosition(11.5f, 5.5f), Assignment.Haul(warehouse.Id, market.Id));
            for (int tick = 0; tick < 20000 && state.SoldGoods < 2; tick++)
            {
                ColonySimulation.TickColony(state, StepSeconds, _catalog);
                Assert.That(ColonySimulation.IsPointWithinBuilding(market, hauler.Position, _catalog), Is.False,
                    $"hauler walked into the market at {hauler.Position}");
            }
            Assert.That(state.SoldGoods, Is.GreaterThanOrEqualTo(2));

            // The entrance cell in front of the market must stay open.
            var blocksEntrance = ColonySimulation.ValidateBuildingPlacement(state, BuildingKind.Forge, new Cell(11, 0), _catalog);
            Assert.That(blocksEntrance.Ok, Is.False);
        }

        [Test]
        public void Route_IsReplannedWhenABuildingAppearsOnIt()
        {
            var state = TestColony.NewState(_catalog);
            // The mine door at (5.5, 9.35) is straight north of the worker.
            var mine = Place(state, BuildingKind.Mine, new Cell(4, 9));
            var worker = AddUnit(state, new WorldPosition(5.5f, 1.5f), Assignment.ToWork(mine.Id));
            ColonySimulation.TickColony(state, StepSeconds, _catalog);
            Assert.That(worker.HasRoute, Is.True);

            // A forge dropped across that straight walk must be walked around, not through.
            Place(state, BuildingKind.Forge, new Cell(5, 4));
            WalkUntil(state, worker, () => worker.Assignment.Kind == AssignmentKind.Work);
        }

        // Ticks until done, asserting a unit steps into a footprint only right after passing that building's approach point.
        private void WalkUntil(GameState state, UnitState unit, Func<bool> done)
        {
            float step = ColonySimulation.UnitMovementSpeed(_catalog.GetUnit(unit.Kind), _catalog) * StepSeconds;
            BuildingState lastApproached = null;
            for (int tick = 0; tick < 20000 && !done(); tick++)
            {
                var before = unit.Position;
                ColonySimulation.TickColony(state, StepSeconds, _catalog);
                foreach (var building in state.Buildings)
                {
                    var approach = ColonyNavigation.DoorwayOf(building, _catalog).Approach;
                    if (Distance(unit.Position, approach) <= step + 0.001f) lastApproached = building;
                    if (!ColonySimulation.IsPointWithinBuilding(building, unit.Position, _catalog) ||
                        ColonySimulation.IsPointWithinBuilding(building, before, _catalog)) continue;
                    Assert.That(lastApproached, Is.SameAs(building),
                        $"{unit.Id} stepped into {building.Id} at {unit.Position} without passing its door approach {approach}");
                }
            }
            Assert.That(done(), Is.True, $"{unit.Id} never finished; stopped at {unit.Position}");
        }

        private BuildingState Place(GameState state, BuildingKind kind, Cell cell)
        {
            Assert.That(ColonySimulation.PlaceStartingBuilding(state, kind, cell, _catalog, out var id).Ok, Is.True,
                $"{kind} at ({cell.X}, {cell.Y})");
            return state.Buildings.Find(b => b.Id == id);
        }

        private static UnitState AddUnit(GameState state, WorldPosition position, Assignment assignment)
        {
            var unit = new UnitState
            {
                Id = $"unit-{state.Units.Count + 1}",
                Kind = UnitKind.Goblin,
                Position = position,
                Assignment = assignment
            };
            state.Units.Add(unit);
            return unit;
        }

        private float MinGap(BuildingState building) =>
            _catalog.GetBuilding(building.Kind).CrowdSpacingCells * ColonyNavigation.CrowdMinGapRatio - 0.001f;

        private static float Distance(WorldPosition a, WorldPosition b) =>
            Mathf.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
