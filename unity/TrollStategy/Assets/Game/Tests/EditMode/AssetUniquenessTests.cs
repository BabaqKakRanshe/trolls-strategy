using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TrollStrategy.Content;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Units;

namespace TrollStrategy.Tests
{
    public class AssetUniquenessTests
    {
        private const string GameRoot = "Assets/Game";
        private const string DefinitionsRoot = "Assets/Game/Content/Definitions";
        private const string BuildingBasePath = "Assets/Game/Prefabs/BuildingBase.prefab";
        private const string UnitBasePath = "Assets/Game/Prefabs/UnitBase.prefab";

        [Test]
        public void RuntimeAssets_HaveOneCanonicalDefinitionPerGameFact()
        {
            Assert.That(AssetDatabase.IsValidFolder("Assets/Game/Resources"), Is.False);

            var catalogs = LoadAll<GameContentCatalog>();
            var economies = LoadAll<EconomyConfig>();
            var buildings = LoadAll<BuildingDefinition>();
            var units = LoadAll<UnitDefinition>();

            Assert.That(catalogs, Has.Length.EqualTo(1));
            Assert.That(economies, Has.Length.EqualTo(1));
            int buildingKinds = System.Enum.GetValues(typeof(BuildingKind)).Length;
            Assert.That(buildings, Has.Length.EqualTo(buildingKinds));
            Assert.That(buildings.Select(definition => definition.Kind).Distinct().Count(), Is.EqualTo(buildingKinds));
            Assert.That(catalogs[0].Buildings, Is.EquivalentTo(buildings));
            Assert.That(units, Has.Length.EqualTo(2));
            Assert.That(units.Select(definition => definition.Kind).Distinct().Count(), Is.EqualTo(2));
            foreach (var unit in units)
            {
                Assert.That(unit.Names, Has.Count.GreaterThanOrEqualTo(12), unit.name);
                Assert.That(unit.Names.Concat(unit.Epithets), Is.Unique.And.All.Not.Empty, unit.name);
            }
            Assert.That(units.SelectMany(unit => unit.Names), Is.Unique, "one name belongs to one species");

            Assert.That(FindPrefabPaths("BuildingBase"), Is.EqualTo(new[] { BuildingBasePath }));
            Assert.That(FindPrefabPaths("UnitBase"), Is.EqualTo(new[] { UnitBasePath }));
        }

        // Each definition is the single entry point: it owns the numbers and names the one prefab variant that renders it.
        [Test]
        public void EveryDefinition_PointsToItsOwnVariantOfTheBasePrefab()
        {
            var buildingBase = AssetDatabase.LoadAssetAtPath<GameObject>(BuildingBasePath);
            foreach (var definition in LoadAll<BuildingDefinition>())
            {
                AssertVariant(definition.Prefab, buildingBase, $"Assets/Game/Prefabs/Buildings/{definition.Kind}.prefab");
                var view = ContentPrefabs.Building(definition);
                Assert.That(view, Is.Not.Null, definition.name);
                Assert.That(view.Model, Is.Not.Null, definition.name);
                Assert.That(view.Model.transform.parent, Is.EqualTo(view.transform), definition.name);
            }

            var unitBase = AssetDatabase.LoadAssetAtPath<GameObject>(UnitBasePath);
            foreach (var definition in LoadAll<UnitDefinition>())
            {
                AssertVariant(definition.Prefab, unitBase, $"Assets/Game/Prefabs/Units/{definition.Kind}.prefab");
                Assert.That(ContentPrefabs.Unit(definition), Is.Not.Null, definition.name);
            }

            var catalog = LoadAll<GameContentCatalog>().Single();
            Assert.That(ContentPrefabs.Validate(catalog, out var error), Is.True, error);
        }

        [Test]
        public void UnitPrefabs_KeepCompleteAnimationFrameSets()
        {
            var units = LoadAll<UnitDefinition>().ToDictionary(definition => definition.Kind, ContentPrefabs.Unit);

            Assert.That(units[UnitKind.Goblin].IdleFrameCount, Is.EqualTo(16));
            Assert.That(units[UnitKind.Goblin].WalkFrameCount, Is.EqualTo(4));
            Assert.That(units[UnitKind.Troll].IdleFrameCount, Is.EqualTo(16));
            Assert.That(units[UnitKind.Troll].WalkFrameCount, Is.EqualTo(6));
        }

        private static void AssertVariant(GameObject prefab, GameObject basePrefab, string expectedPath)
        {
            Assert.That(prefab, Is.Not.Null, expectedPath);
            Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(expectedPath));
            Assert.That(PrefabUtility.GetPrefabAssetType(prefab), Is.EqualTo(PrefabAssetType.Variant), expectedPath);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(prefab), Is.EqualTo(basePrefab), expectedPath);
        }

        private static T[] LoadAll<T>() where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DefinitionsRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
        }

        private static string[] FindPrefabPaths(string prefabName)
        {
            return AssetDatabase.FindAssets($"{prefabName} t:Prefab", new[] { GameRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .ToArray();
        }
    }
}
