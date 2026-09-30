using System.Linq;
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

        /// <summary>
        /// Where the old 14x14 field's cells lie in this catalog's colony: unchanged without land, moved by as much as
        /// the scene's buildings moved (the middle of the start land against the old middle, 7,7) when the colony
        /// starts on a patch of land.
        /// </summary>
        public static Cell Offset(GameContentCatalog catalog)
        {
            var economy = catalog.Economy;
            if (!economy.LandEnabled) return new Cell(0, 0);
            var center = LandRules.StartCenter(economy);
            return new Cell(center.X - 7, center.Y - 7);
        }

        /// <summary><see cref="Layout"/> (or <paramref name="layout"/>) moved onto this catalog's start land.</summary>
        public static StartingBuilding[] LayoutFor(GameContentCatalog catalog, params StartingBuilding[] layout)
        {
            var offset = Offset(catalog);
            var source = layout != null && layout.Length > 0 ? layout : Layout;
            return source.Select(b => new StartingBuilding(b.Kind, new Cell(b.Cell.X + offset.X, b.Cell.Y + offset.Y)))
                .ToArray();
        }

        public static GameSession NewSession(GameContentCatalog catalog) => new(catalog, LayoutFor(catalog));

        public static GameState NewState(GameContentCatalog catalog)
        {
            var state = GameState.CreateInitialState(catalog.Economy.StartingGold);
            state.Land = LandRules.CreateStart(catalog.Economy);
            foreach (var building in LayoutFor(catalog))
                Assert.That(ColonySimulation.PlaceStartingBuilding(state, building.Kind, building.Cell, catalog, out _).Ok,
                    Is.True, building.Kind.ToString());
            return state;
        }
    }
}
