using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Buildings;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Tests
{
    public class BuildingPresentationTests
    {
        [Test]
        public void SelectedBuilding_ShowsThreeDimensionalFootprintRim()
        {
            var root = CreateBuildingView(out var view, out var sourceTexture);
            var snapshot = MineSnapshot(0f);

            try
            {
                view.Setup(snapshot, root.GetComponent<SpriteRenderer>().sprite, null);
                view.UpdateVisuals(snapshot, true);

                var rim = root.transform.Find("PrimitiveModel/SelectionRim");
                Assert.That(rim, Is.Not.Null);
                Assert.That(rim.gameObject.activeSelf, Is.True);
                Assert.That(rim.childCount, Is.EqualTo(4));
                Assert.That(root.GetComponent<SpriteRenderer>().enabled, Is.False);
                view.UpdateVisuals(snapshot, false);
                Assert.That(rim.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sourceTexture);
            }
        }

        [Test]
        public void ProducerProgress_GrowsFromLeftBelowBuildingAndHidesForWarehouse()
        {
            var root = CreateBuildingView(out var view, out var sourceTexture);

            try
            {
                var mine = MineSnapshot(0.25f);
                view.Setup(mine, root.GetComponent<SpriteRenderer>().sprite, null);

                var fill = root.transform.Find("ProductionProgressFill");
                var track = root.transform.Find("ProductionProgressTrack");
                Assert.That(fill, Is.Not.Null);
                Assert.That(track, Is.Not.Null);
                Assert.That(fill.gameObject.activeSelf, Is.True);
                Assert.That(track.gameObject.activeSelf, Is.True);
                Assert.That(fill.localScale.x, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(fill.localPosition.x, Is.EqualTo(-0.9f).Within(0.001f));
                Assert.That(fill.localPosition.y, Is.LessThan(-1.5f));

                var warehouse = new BuildingSnapshot(
                    "warehouse-1", BuildingKind.Warehouse, "Склад", new Cell(10, 8),
                    3, 3, 0, 500, 0, 0, 0f, 0f);
                view.UpdateVisuals(warehouse, false);
                Assert.That(fill.gameObject.activeSelf, Is.False);
                Assert.That(track.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sourceTexture);
            }
        }

        [TestCase(BuildingKind.Mine)]
        [TestCase(BuildingKind.Warehouse)]
        [TestCase(BuildingKind.Market)]
        [TestCase(BuildingKind.Barracks)]
        public void BuildingView_UsesMatchingModelPrefab(BuildingKind kind)
        {
            var root = CreateBuildingView(out var view, out var sourceTexture);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/Game/Content/Definitions/GameContentCatalog.asset");
            var def = catalog.GetBuilding(kind);
            var snapshot = new BuildingSnapshot("test", kind, def.DisplayName, new Cell(0, 0),
                def.Width, def.Height, 0, def.MaxOre, 0, def.MaxWorkers, 0f);

            try
            {
                view.Setup(snapshot, null, null);
                var model = root.transform.Find("PrimitiveModel");
                Assert.That(model, Is.Not.Null);
                Assert.That(model.GetComponent<PrimitiveBuilding>().Kind, Is.EqualTo(kind));
                Assert.That(model.childCount, Is.GreaterThan(1));
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model.gameObject),
                    Is.EqualTo($"Assets/Game/Prefabs/Buildings/{kind}Model.prefab"));
                var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers, Is.Not.Empty);
                foreach (var meshRenderer in renderers)
                {
                    Assert.That(meshRenderer.GetComponent<MeshFilter>()?.sharedMesh, Is.Not.Null,
                        $"{kind}/{meshRenderer.name} has no mesh");
                    Assert.That(meshRenderer.sharedMaterial?.shader, Is.Not.Null,
                        $"{kind}/{meshRenderer.name} has no material shader");
                    Assert.That(AssetDatabase.Contains(meshRenderer.sharedMaterial), Is.True,
                        $"{kind}/{meshRenderer.name} must reference a saved material");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sourceTexture);
            }
        }

        [TestCase(BuildingKind.Mine, "KitMine", "Bld_Mine")]
        [TestCase(BuildingKind.Warehouse, "KitWarehouse", "Bld_House")]
        [TestCase(BuildingKind.Market, "KitMarket", "Bld_Market")]
        [TestCase(BuildingKind.Barracks, "KitBarracks", "Bld_House")]
        public void BuildingPrefab_UsesVitariaModelAndPalette(BuildingKind kind, string childName, string sourceName)
        {
            string path = $"Assets/Game/Prefabs/Buildings/{kind}Model.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.transform.Find(childName);
                Assert.That(visual, Is.Not.Null);
                Assert.That(root.transform.Find("SelectionRim")?.childCount, Is.EqualTo(4));
                Assert.That(root.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers, Is.Not.Empty);
                foreach (var renderer in renderers)
                {
                    string meshPath = AssetDatabase.GetAssetPath(renderer.GetComponent<MeshFilter>()?.sharedMesh);
                    Assert.That(meshPath, Does.EndWith($"/{sourceName}.fbx"));
                    Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial),
                        Is.EqualTo("Assets/Vitaria/Materials/Vitaria_Palette.mat"));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject CreateBuildingView(out BuildingView view, out Texture2D sourceTexture)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/BuildingPrefab.prefab");
            Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab);
            root.name = "BuildingUnderTest";
            var renderer = root.GetComponent<SpriteRenderer>();
            sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sourceTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            sourceTexture.Apply();
            renderer.sprite = Sprite.Create(sourceTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 1f);
            view = root.GetComponent<BuildingView>();
            return root;
        }

        private static BuildingSnapshot MineSnapshot(float progress)
        {
            return new BuildingSnapshot(
                "mine-1", BuildingKind.Mine, "Шахта 1", new Cell(3, 3),
                3, 3, 10, 100, 1, 5, 0.3f, progress);
        }
    }
}
