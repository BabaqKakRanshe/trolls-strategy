using System;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Content;
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
