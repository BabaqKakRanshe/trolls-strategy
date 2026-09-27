using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Tests
{
    /// <summary>The starting layout tests share: a warehouse and a market, as the scene used to spawn.</summary>
    internal static class TestColony
    {
        public static readonly StartingBuilding[] Layout =
        {
            new(BuildingKind.Warehouse, new Cell(10, 8)),
            new(BuildingKind.Market, new Cell(10, 2))
        };

        public static GameSession NewSession(GameContentCatalog catalog) => new(catalog, Layout);

        public static GameState NewState(GameContentCatalog catalog)
        {
            var state = GameState.CreateInitialState(catalog.Economy.StartingGold);
            foreach (var building in Layout)
                Assert.That(ColonySimulation.PlaceStartingBuilding(state, building.Kind, building.Cell, catalog, out _).Ok,
                    Is.True, building.Kind.ToString());
            return state;
        }
    }
}
