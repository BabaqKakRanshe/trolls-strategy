using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// Haulers count the goods already on their way to a building as delivered. Without it several haulers load
    /// for its last free place at once, those who arrive to a full building stand at its door holding their load,
    /// and a workshop full of one input never gets the other: a farm full of wheat waits for straw forever.
    /// </summary>
    public class HaulingReservationTests
    {
        private readonly List<ScriptableObject> _assets = new();
        private GameContentCatalog _catalog;
        private EconomyConfig _economy;

        [SetUp]
        public void SetUp()
        {
            _economy = Create<EconomyConfig>();
            _economy.Init(20, 20, 1f, 1000, 20, .25f, .1f, .5f);
            _economy.SetHauling(.25f, .25f, 3);
            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            var field = Create<BuildingDefinition>();
            field.Init(BuildingKind.Field, "Поле", 0, 3, 3, 50, 5, null);
            field.SetRecipes(new ProductionRecipe(1f, null,
                new[] { new ResourceAmount(ResourceKind.Wheat, 10), new ResourceAmount(ResourceKind.Straw, 3) }));
            var farm = Create<BuildingDefinition>();
            farm.Init(BuildingKind.Farm, "Ферма", 0, 3, 3, 25, 4, null);
            farm.SetRecipes(new ProductionRecipe(1f,
                new[] { new ResourceAmount(ResourceKind.Wheat, 3), new ResourceAmount(ResourceKind.Straw, 1) },
                new[] { new ResourceAmount(ResourceKind.AnimalHide, 1) }));
            var market = Create<BuildingDefinition>();
            market.Init(BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, null);
            market.SetStorage(StorageRole.Market);
            _catalog = Create<GameContentCatalog>();
            _catalog.Init(_economy, new[] { goblin }, new[] { field, farm, market }, new BattleMissionDefinition[0],
                resources: new[]
                {
                    new ResourceDefinition(ResourceKind.Wheat, "Пшеница", 3),
                    new ResourceDefinition(ResourceKind.Straw, "Солома", 1),
                    new ResourceDefinition(ResourceKind.AnimalHide, "Шкура", 12)
                });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void HaulersLoadingForTheLastPlace_TakeTheOtherInput()
        {
            var (state, field, farm) = FieldAndFarm(farmWheat: 24);
            var haulers = Haulers(state, field, farm, 3);

            ColonySimulation.TickColony(state, _economy.LoadSeconds, _catalog);

            Assert.That(haulers.All(h => h.Assignment.Carried > 0), Is.True, "all three loaded");
            Assert.That(haulers.Where(h => h.Assignment.CarriedResource == ResourceKind.Wheat).Sum(h => h.Assignment.Carried),
                Is.EqualTo(1), "one place for wheat, one wheat on its way");
            Assert.That(haulers.Count(h => h.Assignment.CarriedResource == ResourceKind.Straw), Is.EqualTo(2));
        }

        [Test]
        public void AFarmFullOfWheat_StillGetsItsStraw()
        {
            var (state, field, farm) = FieldAndFarm(farmWheat: 15);
            Haulers(state, field, farm, 3);

            // the field keeps far more wheat than straw, as a real one does
            for (int i = 0; i < 400 && farm.GetStock(ResourceKind.Straw) == 0; i++)
            {
                ColonySimulation.TickColony(state, .25f, _catalog);
                if (field.GetStock(ResourceKind.Wheat) < 30) field.AddStock(ResourceKind.Wheat, 10);
            }

            Assert.That(farm.GetStock(ResourceKind.Wheat), Is.LessThanOrEqualTo(25));
            Assert.That(farm.GetStock(ResourceKind.Straw), Is.GreaterThan(0), "the straw reaches the farm");
            Assert.That(state.Units.Count(u => u.Assignment.Phase == HaulPhase.Unloading && u.Assignment.Carried > 0 &&
                                                ColonySimulation.Room(farm, u.Assignment.CarriedResource, _catalog) == 0),
                Is.Zero, "nobody stands at the farm's door with goods it has no room for");
        }

        private (GameState state, BuildingState field, BuildingState farm) FieldAndFarm(int farmWheat)
        {
            var state = GameState.CreateInitialState(_economy.StartingGold);
            state.Land = LandRules.CreateStart(_economy);
            Assert.That(ColonySimulation.PlaceStartingBuilding(state, BuildingKind.Field, new Cell(2, 8), _catalog, out _).Ok, Is.True);
            Assert.That(ColonySimulation.PlaceStartingBuilding(state, BuildingKind.Farm, new Cell(12, 8), _catalog, out _).Ok, Is.True);
            var field = state.Buildings.First(b => b.Kind == BuildingKind.Field);
            var farm = state.Buildings.First(b => b.Kind == BuildingKind.Farm);
            field.AddStock(ResourceKind.Wheat, 40);
            field.AddStock(ResourceKind.Straw, 5);
            farm.AddStock(ResourceKind.Wheat, farmWheat);
            return (state, field, farm);
        }

        private List<UnitState> Haulers(GameState state, BuildingState source, BuildingState destination, int count)
        {
            var haulers = new List<UnitState>();
            for (int i = 1; i <= count; i++)
            {
                var unit = new UnitState
                {
                    Id = $"unit-{i}",
                    Kind = UnitKind.Goblin,
                    Position = ColonySimulation.BuildingEntrancePosition(source, _catalog),
                    Assignment = Assignment.Haul(source.Id, destination.Id)
                };
                unit.Assignment.Phase = HaulPhase.Loading;
                state.Units.Add(unit);
                haulers.Add(unit);
            }
            return haulers;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
