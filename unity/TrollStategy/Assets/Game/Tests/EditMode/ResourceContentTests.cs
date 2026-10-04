using System;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Tests
{
    /// <summary>Every good of the real catalog has its icon and a model of one piece from the Vitaria kit.</summary>
    public class ResourceContentTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
        private const string KitModels = "Assets/Vitaria/Models/Resources/";
        private static readonly string[] KitMaterials = { "Vitaria_Palette", "Vitaria_FX" };

        private GameContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
        }

        [Test]
        public void Catalog_DefinesEveryGoodOnce()
        {
            var kinds = _catalog.Resources.Select(resource => resource.Kind).ToList();
            Assert.That(kinds, Is.Unique);
            Assert.That(kinds, Is.EquivalentTo(Enum.GetValues(typeof(ResourceKind)).Cast<ResourceKind>()));
        }

        [Test]
        public void EveryGood_HasAnIconAndAKitModelOfOnePiece()
        {
            foreach (var resource in _catalog.Resources)
            {
                string kind = resource.Kind.ToString();
                Assert.That(resource.Icon, Is.Not.Null, kind + " has no icon (TrollStrategy/Dev/Import Icon Atlases)");
                Assert.That(resource.Model, Is.Not.Null, kind + " has no model (TrollStrategy/Dev/Setup Production Content)");
                AssertKitModel(resource.Model, kind);
                if (resource.PileModel != null) AssertKitModel(resource.PileModel, kind + " pile");
            }
        }

        [Test]
        public void EveryEquipment_HasItsIconAndAGoodThatBringsIt()
        {
            var goods = _catalog.Resources.Where(r => r.IsEquipment).Select(r => r.EquipmentId).ToList();
            Assert.That(goods, Is.Unique);
            foreach (var equipment in _catalog.Equipment)
            {
                Assert.That(equipment.Icon, Is.Not.Null, equipment.ItemId + " has no icon (TrollStrategy/Dev/Import Icon Atlases)");
                Assert.That(goods, Does.Contain(equipment.ItemId), equipment.ItemId + " has no good to bring it to an armory");
                Assert.That(equipment.Enchanted, Is.EqualTo(equipment.ItemId.StartsWith("enchanted-")), equipment.ItemId);
            }
            foreach (var id in goods)
                Assert.That(_catalog.Equipment.Any(e => e.ItemId == id), Is.True, id + " is a good without an EquipmentDefinition");
        }

        [Test]
        public void EveryGood_IsMadeSomewhereAndTakenSomewhere()
        {
            foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
            {
                bool made = _catalog.Buildings.Any(b => b != null && b.Recipes.Any(r => r.CanYield(kind))) ||
                            _catalog.Missions.Any(m => m != null && m.WinGoods.Any(g => g.Resource == kind));
                Assert.That(made, Is.True, kind + " is made by no recipe and won in no battle");
                Assert.That(_catalog.Buildings.Any(b => b != null && ColonySimulation.Accepts(b, kind, _catalog)), Is.True,
                    kind + " is taken by no building");
            }
        }

        [Test]
        public void ArenaTrophies_AreGearTheBarracksKeep()
        {
            var barracks = _catalog.GetBuilding(BuildingKind.Barracks);
            var trophies = _catalog.Missions.Where(m => m != null).SelectMany(m => m.WinGoods).ToList();
            Assert.That(trophies, Is.Not.Empty, "The first arena level gives a rusty set for every win");
            foreach (var trophy in trophies)
            {
                Assert.That(_catalog.GetResource(trophy.Resource).IsEquipment, Is.True, trophy.Resource + " is no gear");
                Assert.That(barracks.Stores(trophy.Resource), Is.True, "The barracks do not keep " + trophy.Resource);
            }
            Assert.That(barracks.Capacity(1), Is.GreaterThan(0));
        }

        // The FBX itself, so a kit export updates it; and only the kit's palette materials, never a pink one.
        private static void AssertKitModel(GameObject model, string what)
        {
            string path = AssetDatabase.GetAssetPath(model);
            Assert.That(path, Does.StartWith(KitModels).And.EndWith(".fbx"), what + " must use the kit's FBX, not a copy");
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers, Is.Not.Empty, path);
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
                Assert.That(material != null && KitMaterials.Contains(material.name), Is.True,
                    $"{path}/{renderer.name}: {(material != null ? material.name : "no material")} instead of the kit palette");
        }
    }
}
