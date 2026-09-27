using System.Text;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Units;
using UnityEngine;

namespace TrollStrategy.Presentation
{
    // Resolves the view prefab each content definition points to.
    public static class ContentPrefabs
    {
        public static BuildingView Building(BuildingDefinition definition) =>
            definition != null && definition.Prefab != null ? definition.Prefab.GetComponent<BuildingView>() : null;

        public static UnitView Unit(UnitDefinition definition) =>
            definition != null && definition.Prefab != null ? definition.Prefab.GetComponent<UnitView>() : null;

        public static bool Validate(GameContentCatalog catalog, out string error)
        {
            var problems = new StringBuilder();
            foreach (var definition in catalog.Buildings)
            {
                if (definition == null) problems.AppendLine("catalog has an empty building entry");
                else if (Building(definition) == null) problems.AppendLine($"{definition.name}: prefab with BuildingView is missing");
                else if (Building(definition).Model == null) problems.AppendLine($"{definition.name}: prefab has no BuildingModel");
                else if (Building(definition).Kind != definition.Kind)
                    problems.AppendLine($"{definition.name}: prefab BuildingView kind {Building(definition).Kind} != {definition.Kind}");
            }
            foreach (var definition in catalog.Units)
            {
                if (definition == null) problems.AppendLine("catalog has an empty unit entry");
                else if (Unit(definition) == null) problems.AppendLine($"{definition.name}: prefab with UnitView is missing");
            }
            error = problems.ToString();
            return error.Length == 0;
        }
    }
}
