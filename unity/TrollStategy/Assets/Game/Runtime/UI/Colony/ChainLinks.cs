using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Who makes a good and who takes it, read from the buildings' recipes: the quest reward and the book name
    /// the same neighbours. Takers are recipe inputs, then the armory for gear and the market for anything it
    /// buys (both come with the colony, so they count although they are not built).
    /// </summary>
    public static class ChainLinks
    {
        /// <summary>The buildings that make any of <paramref name="goods"/>, as an output or a chance by-product.</summary>
        public static List<BuildingDefinition> Makers(GameContentCatalog catalog, IEnumerable<ResourceKind> goods,
            BuildingKind? except = null)
        {
            var wanted = new HashSet<ResourceKind>(goods);
            var found = new List<BuildingDefinition>();
            foreach (var building in catalog.Buildings)
            {
                if (building == null || !building.Constructible || building.Kind == except) continue;
                bool makes = false;
                foreach (var recipe in building.Recipes)
                {
                    foreach (var output in recipe.Outputs) makes |= wanted.Contains(output.Resource);
                    foreach (var extra in recipe.Extras) makes |= wanted.Contains(extra.Output.Resource);
                }
                if (makes) found.Add(building);
            }
            return found;
        }

        /// <summary>The buildings that take any of <paramref name="goods"/>: recipe inputs, the armory, the market.</summary>
        public static List<BuildingDefinition> Takers(GameContentCatalog catalog, IEnumerable<ResourceKind> goods,
            BuildingKind? except = null)
        {
            var wanted = new HashSet<ResourceKind>(goods);
            var found = new List<BuildingDefinition>();
            BuildingDefinition market = null, armory = null;
            foreach (var building in catalog.Buildings)
            {
                if (building == null || building.Kind == except) continue;
                if (building.Kind == BuildingKind.Market) market = building;
                if (building.Kind == BuildingKind.Armory) armory = building;
                if (!building.Constructible) continue;
                bool takes = false;
                foreach (var recipe in building.Recipes)
                    foreach (var input in recipe.Inputs) takes |= wanted.Contains(input.Resource);
                if (takes) found.Add(building);
            }
            bool gear = false, sold = false;
            foreach (var kind in wanted)
            {
                var resource = catalog.TryGetResource(kind);
                gear |= resource != null && resource.IsEquipment;
                sold |= resource != null && resource.SellPrice > 0;
            }
            if (gear && armory != null && !found.Contains(armory)) found.Add(armory);
            if (sold && market != null) found.Add(market);
            return found;
        }

        public static IEnumerable<ResourceKind> Kinds(IEnumerable<ResourceAmount> amounts)
        {
            foreach (var amount in amounts) yield return amount.Resource;
        }
    }
}
