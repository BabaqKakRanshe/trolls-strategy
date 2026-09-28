using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.Tests
{
    /// <summary>Haul cargo choice, hauling times, loading places, who can be selected, and the battle prize.</summary>
    public class HaulingAndRewardTests
    {
        private readonly List<ScriptableObject> _assets = new();
        private GameContentCatalog _catalog;
        private EconomyConfig _economy;
        private BattleMissionDefinition _mission;

        [SetUp]
        public void SetUp()
        {
            _economy = Create<EconomyConfig>();
            _economy.Init(14, 14, 1f, 1000, 20, .25f, .1f, .5f);
            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            goblin.SetCombatStats(20, 2, 1, 2000, 3);
            var troll = Create<UnitDefinition>();
            troll.Init(UnitKind.Troll, "Тролль", 170, 9, 2f, 150);
            troll.SetCombatStats(55, 7, 3, 2600, 1);
            var mine = Create<BuildingDefinition>();
            mine.Init(BuildingKind.Mine, "Шахта", 200, 3, 3, 100, 5, null);
            mine.SetConstructible(true);
            mine.SetRecipes(new ProductionRecipe(1f, null, new[] { new ResourceAmount(ResourceKind.IronOre, 1) }));
            var warehouse = Create<BuildingDefinition>();
            warehouse.Init(BuildingKind.Warehouse, "Склад", 0, 3, 3, 500, 0, null);
            warehouse.SetStorage(StorageRole.Stockpile, ResourceKind.IronOre, ResourceKind.IronIngot);
            var market = Create<BuildingDefinition>();
            market.Init(BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, null);
            market.SetStorage(StorageRole.Market);
            _mission = Create<BattleMissionDefinition>();
            _mission.SetDesign("mission-1", "Первый бой", 3, 3, 1,
                new[] { new Cell(0, 1) }, new Cell[0],
                new[] { new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(2, 1) } });
            _mission.SetTimingAndRewards(0f, 0f, 200, 50);
            _mission.SetRewardRanges(300, 90);
            _catalog = Create<GameContentCatalog>();
            _catalog.Init(_economy, new[] { goblin, troll }, new[] { mine, warehouse, market }, new[] { _mission },
                resources: new[]
                {
                    new ResourceDefinition(ResourceKind.IronOre, "Руда", 3),
                    new ResourceDefinition(ResourceKind.IronIngot, "Слиток", 9)
                });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void ChosenCargo_IsAllTheHaulerTakes()
        {
            var state = TestColony.NewState(_catalog);
            var warehouse = state.Buildings.First(b => b.Kind == BuildingKind.Warehouse);
            warehouse.AddStock(ResourceKind.IronOre, 5);
            warehouse.AddStock(ResourceKind.IronIngot, 5);
            var ingotOnly = Hauler(state, "unit-1", warehouse, Assignment.Haul("warehouse-1", "market-1",
                new[] { ResourceKind.IronIngot }));
            var anything = Hauler(state, "unit-2", warehouse, Assignment.Haul("warehouse-1", "market-1"));
            ingotOnly.Assignment.Phase = anything.Assignment.Phase = HaulPhase.Loading;

            ColonySimulation.TickColony(state, _economy.LoadSeconds, _catalog);

            Assert.That(ingotOnly.Assignment.CarriedResource, Is.EqualTo(ResourceKind.IronIngot));
            Assert.That(anything.Assignment.CarriedResource, Is.EqualTo(ResourceKind.IronOre),
                "Without a choice the first good in order goes");
        }

        [Test]
        public void HaulOrder_RefusesCargoTheRouteCannotCarry_AndKeepsTheChoice()
        {
            var state = TestColony.NewState(_catalog);
            state.Units.Add(new UnitState { Id = "unit-1", Kind = UnitKind.Goblin, Position = new WorldPosition(3f, 3f), Assignment = Assignment.Idle() });

            var wheat = ColonySimulation.ApplyCommand(state, new AssignHaulCommand(new[] { "unit-1" }, "warehouse-1", "market-1",
                new[] { ResourceKind.Wheat }), _catalog);
            Assert.That(wheat.Ok, Is.False);
            Assert.That(wheat.Error, Does.StartWith("По этому маршруту не возят"));

            Assert.That(ColonySimulation.ApplyCommand(state, new AssignHaulCommand(new[] { "unit-1" }, "warehouse-1", "market-1",
                new[] { ResourceKind.IronIngot, ResourceKind.IronIngot }), _catalog).Ok, Is.True);
            Assert.That(state.Units[0].Assignment.Cargo, Is.EqualTo(new[] { ResourceKind.IronIngot }));
            Assert.That(state.Clone().Units[0].Assignment.Cargo, Is.EqualTo(new[] { ResourceKind.IronIngot }));
        }

        [Test]
        public void NoLoadingOrUnloadingTime_HandsGoodsOverOnArrival()
        {
            _economy.SetHauling(0f, 0f, 1);
            var state = TestColony.NewState(_catalog);
            var warehouse = state.Buildings.First(b => b.Kind == BuildingKind.Warehouse);
            warehouse.AddStock(ResourceKind.IronOre, 5);
            var hauler = Hauler(state, "unit-1", warehouse, Assignment.Haul("warehouse-1", "market-1"));
            hauler.Assignment.Phase = HaulPhase.ToDock;

            ColonySimulation.TickColony(state, _economy.StepTimeSeconds, _catalog);

            Assert.That(hauler.Assignment.Phase, Is.EqualTo(HaulPhase.ToDestination), "Loaded in the step it arrived");
            Assert.That(hauler.Assignment.Carried, Is.EqualTo(1));
            int gold = state.Gold;
            for (int i = 0; i < 200 && state.SoldGoods == 0; i++) ColonySimulation.TickColony(state, _economy.StepTimeSeconds, _catalog);
            Assert.That(state.SoldGoods, Is.EqualTo(1));
            Assert.That(state.Gold, Is.EqualTo(gold + 3));
        }

        [Test]
        public void LoadingTime_HoldsTheHaulerAtTheDoor()
        {
            _economy.SetHauling(.75f, 0f, 1);
            var state = TestColony.NewState(_catalog);
            var warehouse = state.Buildings.First(b => b.Kind == BuildingKind.Warehouse);
            warehouse.AddStock(ResourceKind.IronOre, 5);
            var hauler = Hauler(state, "unit-1", warehouse, Assignment.Haul("warehouse-1", "market-1"));
            hauler.Assignment.Phase = HaulPhase.Loading;

            ColonySimulation.TickColony(state, .25f, _catalog);
            ColonySimulation.TickColony(state, .25f, _catalog);
            Assert.That(hauler.Assignment.Carried, Is.Zero);
            ColonySimulation.TickColony(state, .25f, _catalog);
            Assert.That(hauler.Assignment.Carried, Is.EqualTo(1));
        }

        [Test]
        public void SeveralLoadingPlaces_LetHaulersLoadSideBySide()
        {
            _economy.SetHauling(.5f, .5f, 2);
            var state = TestColony.NewState(_catalog);
            var warehouse = state.Buildings.First(b => b.Kind == BuildingKind.Warehouse);
            warehouse.AddStock(ResourceKind.IronOre, 30);
            var haulers = new[] { "unit-1", "unit-2", "unit-3" }
                .Select(id => Hauler(state, id, warehouse, Assignment.Haul("warehouse-1", "market-1"))).ToList();

            ColonySimulation.TickColony(state, .25f, _catalog);

            Assert.That(haulers.Count(u => u.Assignment.Phase == HaulPhase.Loading), Is.EqualTo(2));
            Assert.That(haulers.Count(u => u.Assignment.Phase == HaulPhase.QueuedAtSource), Is.EqualTo(1));
        }

        [Test]
        public void LoaderWithNothingToTake_GivesTheDoorToTheQueue()
        {
            _economy.SetHauling(.25f, .25f, 1);
            var state = TestColony.NewState(_catalog);
            var warehouse = state.Buildings.First(b => b.Kind == BuildingKind.Warehouse);
            warehouse.AddStock(ResourceKind.IronOre, 10);
            var picky = Hauler(state, "unit-1", warehouse, Assignment.Haul("warehouse-1", "market-1",
                new[] { ResourceKind.IronIngot }));
            var anything = Hauler(state, "unit-2", warehouse, Assignment.Haul("warehouse-1", "market-1"));

            for (int i = 0; i < 8 && anything.Assignment.Carried == 0; i++)
                ColonySimulation.TickColony(state, .25f, _catalog);

            Assert.That(anything.Assignment.CarriedResource, Is.EqualTo(ResourceKind.IronOre),
                "The ore must not wait behind a hauler that only takes ingots");
            Assert.That(anything.Assignment.Carried, Is.GreaterThan(0));
            Assert.That(picky.Assignment.Carried, Is.Zero);
        }

        [Test]
        public void CreaturesInsideABuilding_CannotBeSelected_AndLeaveTheSelection()
        {
            var session = TestColony.NewSession(_catalog);
            var interaction = new InteractionController(session);
            Assert.That(session.Dispatch(new BuildBuildingCommand(BuildingKind.Mine, new Cell(2, 2))).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(6, 3))).Ok, Is.True);
            string goblin = session.CurrentSnapshot.Units[0].Id;
            string mine = session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Mine).Id;
            interaction.ClickUnit(goblin, false);
            Assert.That(interaction.SelectedIds, Does.Contain(goblin));

            Assert.That(session.Dispatch(new AssignWorkCommand(new[] { goblin }, mine)).Ok, Is.True);
            for (int i = 0; i < 400 && session.CurrentSnapshot.Units[0].Assignment.Kind != AssignmentKind.Work; i++)
                session.Advance(_economy.StepTimeSeconds);
            Assert.That(session.CurrentSnapshot.Units[0].Assignment.Kind, Is.EqualTo(AssignmentKind.Work));

            Assert.That(interaction.SelectedIds, Is.Empty, "A creature that went inside drops out of the selection");
            Assert.That(interaction.IsSelectable(goblin), Is.False);
            interaction.ClickUnit(goblin, false);
            interaction.SelectUnits(new[] { goblin });
            Assert.That(interaction.SelectedIds, Is.Empty);

            interaction.ReleaseUnits(new[] { goblin });
            Assert.That(interaction.IsSelectable(goblin), Is.True, "Released, it comes out and can be picked again");
        }

        [Test]
        public void BattlePrize_RollsWithinTheRange_WaitsAndIsTakenOnce()
        {
            var session = Victory(out int before);
            var prize = session.CurrentSnapshot.BattleReward;

            Assert.That(prize, Is.Not.Null);
            Assert.That(prize.Gold, Is.InRange(200, 300));
            Assert.That(prize.MinGold, Is.EqualTo(200));
            Assert.That(prize.MaxGold, Is.EqualTo(300));
            Assert.That(prize.FirstWin, Is.True);
            Assert.That(session.ActiveBattle.AwardedGold, Is.EqualTo(prize.Gold));
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(before));

            Assert.That(session.Dispatch(new AcknowledgeBattleCommand()).Ok, Is.True);
            Assert.That(session.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.True);
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(before + prize.Gold));
            Assert.That(session.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.False);
        }

        [Test]
        public void BattlePrize_IsTheSameForTheSameGame()
        {
            int first = Victory(out _).CurrentSnapshot.BattleReward.Gold;
            int second = Victory(out _).CurrentSnapshot.BattleReward.Gold;

            Assert.That(second, Is.EqualTo(first), "The dice live in the game state, not in the clock");
        }

        [Test]
        public void RewardDice_CoverTheWholeRangeAndNothingElse()
        {
            var state = GameState.CreateInitialState();
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                int roll = RewardDice.Roll(state, 10, 14);
                Assert.That(roll, Is.InRange(10, 14));
                seen.Add(roll);
            }
            Assert.That(seen.Count, Is.EqualTo(5));
            Assert.That(RewardDice.Roll(state, 7, 7), Is.EqualTo(7));
        }

        private GameSession Victory(out int goldBefore)
        {
            var session = TestColony.NewSession(_catalog);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, new Cell(3, 3))).Ok, Is.True);
            goldBefore = session.CurrentSnapshot.Gold;
            string troll = session.CurrentSnapshot.Units[0].Id;
            Assert.That(session.Dispatch(new StartBattleCommand("mission-1",
                new[] { new BattlePlacement(troll, new Cell(0, 1)) })).Ok, Is.True);
            Assert.That(session.ActiveBattle.Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            return session;
        }

        private UnitState Hauler(GameState state, string id, BuildingState source, Assignment assignment)
        {
            var unit = new UnitState
            {
                Id = id,
                Kind = UnitKind.Goblin,
                Position = ColonySimulation.BuildingEntrancePosition(source, _catalog),
                Assignment = assignment
            };
            state.Units.Add(unit);
            return unit;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
