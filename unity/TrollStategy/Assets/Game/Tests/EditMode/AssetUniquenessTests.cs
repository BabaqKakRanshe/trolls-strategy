using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TrollStrategy.Content;

namespace TrollStrategy.Tests
{
    public class AssetUniquenessTests
    {
        private const string GameRoot = "Assets/Game";
        private const string DefinitionsRoot = "Assets/Game/Content/Definitions";

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
            Assert.That(buildings, Has.Length.EqualTo(4));
            Assert.That(buildings.Select(definition => definition.Kind).Distinct().Count(), Is.EqualTo(4));
            Assert.That(units, Has.Length.EqualTo(2));
            Assert.That(units.Select(definition => definition.Kind).Distinct().Count(), Is.EqualTo(2));

            Assert.That(FindPrefabPaths("BuildingPrefab"), Is.EqualTo(new[] { "Assets/Game/Prefabs/BuildingPrefab.prefab" }));
            Assert.That(FindPrefabPaths("UnitPrefab"), Is.EqualTo(new[] { "Assets/Game/Prefabs/UnitPrefab.prefab" }));
        }

        [Test]
        public void UnitDefinitions_KeepCompleteAnimationFrameSets()
        {
            var units = LoadAll<UnitDefinition>().ToDictionary(definition => definition.Kind);

            Assert.That(units[UnitKind.Goblin].IdleFrames, Has.Length.EqualTo(16));
            Assert.That(units[UnitKind.Goblin].WalkFrames, Has.Length.EqualTo(4));
            Assert.That(units[UnitKind.Troll].IdleFrames, Has.Length.EqualTo(16));
            Assert.That(units[UnitKind.Troll].WalkFrames, Has.Length.EqualTo(6));
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
