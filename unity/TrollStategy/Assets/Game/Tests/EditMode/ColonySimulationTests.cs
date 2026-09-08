using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
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
            economy.Init(14, 14, 1f, 1234, 20, 0.25f, 3, 0.1f, 0.5f);

            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 10, null, null, null);
            var troll = Create<UnitDefinition>();
            troll.Init(UnitKind.Troll, "Тролль", 170, 9, 2f, 30, null, null, null);

            var mine = Create<BuildingDefinition>();
            mine.Init(BuildingKind.Mine, "Шахта", 200, 3, 3, 100, 5, null);
            var warehouse = Create<BuildingDefinition>();
            warehouse.Init(BuildingKind.Warehouse, "Склад", 0, 3, 3, 500, 0, null);
            var market = Create<BuildingDefinition>();
            market.Init(BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, null);
            var barracks = Create<BuildingDefinition>();
            barracks.Init(BuildingKind.Barracks, "Бараки", 0, 3, 3, 0, 0, null);

            _catalog = Create<GameContentCatalog>();
            _catalog.Init(economy, new[] { goblin, troll }, new[] { mine, warehouse, market, barracks });
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _assets.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(_assets[i]);
            _assets.Clear();
        }

        [Test]
        public void Session_UsesCatalogStartingGoldAndCanonicalPrices()
        {
            var session = new GameSession(_catalog);

            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(1234));
            Assert.That(session.Dispatch(new BuildMineCommand(new Cell(3, 3))).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 2, new Cell(7, 7))).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, new Cell(8, 7))).Ok, Is.True);

            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(784));
        }

        [Test]
        public void RejectedCommand_DoesNotChangeStateOrRevision()
        {
            var session = new GameSession(_catalog);
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
            var session = new GameSession(_catalog);
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
            var session = new GameSession(_catalog);
            Assert.That(session.Dispatch(new BuildMineCommand(new Cell(3, 3))).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 6, new Cell(7, 7))).Ok, Is.True);

            var result = session.Dispatch(new AssignWorkCommand(
                new[] { "unit-1", "unit-2", "unit-3", "unit-4", "unit-5", "unit-6" }, "mine-1"));

            Assert.That(result.Ok, Is.True);
            Assert.That(session.CurrentSnapshot.Units.Count(u => u.Assignment.Kind == AssignmentKind.ToWork), Is.EqualTo(5));
            Assert.That(session.CurrentSnapshot.Units.Single(u => u.Id == "unit-6").Assignment.Kind, Is.EqualTo(AssignmentKind.Idle));
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
            var session = new GameSession(_catalog);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(7, 7))).Ok, Is.True);
            var exposedAssignment = session.CurrentSnapshot.Units.Single().Assignment;

            exposedAssignment.Kind = AssignmentKind.Haul;
            exposedAssignment.Carried = 999;

            Assert.That(session.CurrentSnapshot.Units.Single().Assignment.Kind, Is.EqualTo(AssignmentKind.Idle));
            Assert.That(session.CurrentSnapshot.TotalOre, Is.Zero);
        }

        private GameState StateWithMineAndHauler(HaulPhase phase, int carried)
        {
            var state = GameState.CreateInitialState(_catalog.Economy.StartingGold);
            state.Buildings.Add(new BuildingState
            {
                Id = "mine-1",
                Kind = BuildingKind.Mine,
                Cell = new Cell(3, 3),
                Ore = 0
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
