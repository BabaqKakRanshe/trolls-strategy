using System;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Map;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// Every building has three levels, one for each kit model: the content gives each a level 1 and two upgrades,
    /// the prefab carries the three models in one place, and the view shows the model of the building's level with
    /// the production bar over that roof. The levels of the guild, the barracks and the armory open their upgrades.
    /// </summary>
    public class BuildingLevelTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
        private const int Levels = 3;

        private static readonly BuildingKind[] AllKinds =
            Enum.GetValues(typeof(BuildingKind)).Cast<BuildingKind>().ToArray();

        private static GameContentCatalog Catalog() => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);

        [TestCaseSource(nameof(AllKinds))]
        public void Building_HasADefaultLevelAndTwoUpgrades(BuildingKind kind)
        {
            var building = Catalog().GetBuilding(kind);
            Assert.That(building.MaxLevel, Is.EqualTo(Levels));
            Assert.That(building.UpgradeCost(1), Is.GreaterThan(0));
            Assert.That(building.UpgradeCost(2), Is.GreaterThan(building.UpgradeCost(1)), "The second upgrade costs more");
            Assert.That(building.UpgradeCost(Levels), Is.EqualTo(-1), "Nothing above level 3");
        }

        [Test]
        public void HostedUpgrades_OpenWithTheLevelOfTheirBuilding()
        {
            var catalog = Catalog();
            Assert.That(catalog.Upgrades, Is.Not.Empty);
            foreach (var upgrade in catalog.Upgrades)
            {
                var host = catalog.GetBuilding(upgrade.Host);
                Assert.That(upgrade.HostLevelFrom(0), Is.EqualTo(1), $"{upgrade.Id}: the first level opens with the building");
                Assert.That(upgrade.OpenLevels(1), Is.LessThan(upgrade.MaxLevel), $"{upgrade.Id}: a building of level 1 opens only a part");
                Assert.That(upgrade.OpenLevels(host.MaxLevel), Is.EqualTo(upgrade.MaxLevel), $"{upgrade.Id}: the top host opens all");
                for (int level = 1; level < upgrade.MaxLevel; level++)
                    Assert.That(upgrade.HostLevelFrom(level), Is.GreaterThanOrEqualTo(upgrade.HostLevelFrom(level - 1)), upgrade.Id);
                Assert.That(upgrade.HostLevelFrom(upgrade.MaxLevel), Is.Zero, "Nothing to open at the top");
            }
            Assert.That(catalog.Upgrades.Single(u => u.Effect == UpgradeEffect.FighterDamage).Host, Is.EqualTo(BuildingKind.Armory),
                "The weapon drill is bought in the armory");
            foreach (var host in new[] { BuildingKind.HaulersGuild, BuildingKind.Barracks, BuildingKind.Armory })
                Assert.That(catalog.Upgrades.Any(u => u.Host == host), Is.True, $"{host} hosts upgrades its level opens");
        }

        [TestCaseSource(nameof(AllKinds))]
        public void BuildingPrefab_HoldsAKitModelForEachLevelInOnePlace(BuildingKind kind)
        {
            var root = PrefabUtility.LoadPrefabContents($"Assets/Game/Prefabs/Buildings/{kind}.prefab");
            try
            {
                var model = root.GetComponentInChildren<BuildingModel>(true);
                Assert.That(model, Is.Not.Null);
                Assert.That(model.LevelCount, Is.EqualTo(Levels));
                var first = model.LevelModel(1).transform;
                Assert.That(first, Is.EqualTo(model.transform.Find($"Kit{kind}")), "Level 1 is the building's kit model");
                string source = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(first.gameObject));
                Assert.That(source, Does.EndWith(".fbx"));
                Assert.That(first.gameObject.activeSelf, Is.True, "The prefab shows level 1");
                for (int level = 1; level <= Levels; level++)
                {
                    var levelModel = model.LevelModel(level).transform;
                    Assert.That(levelModel.parent, Is.EqualTo(first.parent), $"L{level}");
                    if (level > 1)
                    {
                        Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(levelModel.gameObject)),
                            Is.EqualTo(source.Replace(".fbx", $"_L{level}.fbx")), $"L{level} comes from its kit FBX");
                        Assert.That(levelModel.gameObject.activeSelf, Is.False, $"L{level} waits hidden");
                    }
                    Assert.That(Vector3.Distance(levelModel.localPosition, first.localPosition), Is.LessThan(1e-4f), $"L{level} position");
                    Assert.That(Quaternion.Angle(levelModel.localRotation, first.localRotation), Is.LessThan(.01f), $"L{level} rotation");
                    Assert.That(Vector3.Distance(levelModel.localScale, first.localScale), Is.LessThan(1e-5f), $"L{level} scale");
                    Assert.That(model.LevelTop(level), Is.EqualTo(Top(root.transform, levelModel)).Within(.005f),
                        $"L{level}: the stored roof height is the model's");
                    foreach (var renderer in levelModel.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var materials = renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray();
                        Assert.That(materials[0], Is.EqualTo("Assets/Vitaria/Materials/Vitaria_Palette.mat"), $"L{level}/{renderer.name}");
                        Assert.That(materials.Skip(1), Has.All.EqualTo("Assets/Vitaria/Materials/Vitaria_FX.mat"), $"L{level}/{renderer.name}");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCaseSource(nameof(AllKinds))]
        public void BuildingView_ShowsTheModelOfItsLevel_WithTheBarOverThatRoof(BuildingKind kind)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Prefabs/Buildings/{kind}.prefab");
            var root = Object.Instantiate(prefab);
            var map = new GameObject("TestMapView").AddComponent<TilemapWorldView>();
            var def = Catalog().GetBuilding(kind);
            try
            {
                var view = root.GetComponent<BuildingView>();
                var model = view.Model;
                var bar = root.transform.Find("ProductionProgress");
                Assert.That(bar, Is.Not.Null);
                float placed = -bar.localPosition.z;
                view.Setup(Snapshot(kind, def, 1), null, null, map);
                float previous = 0f;
                for (int level = 1; level <= Levels + 1; level++)
                {
                    view.UpdateVisuals(Snapshot(kind, def, level), false);
                    int shown = Math.Min(level, Levels);
                    for (int i = 1; i <= Levels; i++)
                        Assert.That(model.LevelModel(i).activeSelf, Is.EqualTo(i == shown),
                            $"Level {level} shows L{shown} alone, not L{i}");
                    float expected = Mathf.Max(placed, model.LevelTop(shown) + .3f);
                    Assert.That(-bar.localPosition.z, Is.EqualTo(expected).Within(1e-4f),
                        $"Level {level}: the bar floats over the shown roof, never under the prefab's place");
                    Assert.That(-bar.localPosition.z, Is.GreaterThan(model.LevelTop(shown)), $"Level {level}: the roof cuts the bar");
                    Assert.That(-bar.localPosition.z, Is.GreaterThanOrEqualTo(previous - 1e-4f), "The bar never sinks as the building grows");
                    previous = -bar.localPosition.z;
                }
                Assert.That(model.LevelTop(Levels), Is.GreaterThan(model.LevelTop(1)), "Level 3 stands taller than level 1");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(map.gameObject);
            }
        }

        private static BuildingSnapshot Snapshot(BuildingKind kind, BuildingDefinition def, int level) =>
            new("test", kind, def.DisplayName, new Cell(0, 0), def.Width, def.Height, 0, def.Capacity(level), 0,
                def.WorkerCapacity(level), 0f, level: level);

        // the highest point of a model above the ground: -z of the building root points up
        private static float Top(Transform root, Transform model)
        {
            float top = 0f;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var bounds = filter.sharedMesh.bounds;
                var toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                    top = Mathf.Max(top, -toRoot.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))).z);
            }
            return top;
        }
    }
}
