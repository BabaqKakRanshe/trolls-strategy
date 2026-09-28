using System;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrollStrategy.Tests
{
    public class BuildingPresentationTests
    {
        [Test]
        public void SavedScene_UsesTheUiToolkitHudWiredToTheBootstrap()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/MainColonyScene.unity");
            Assert.That(GameObject.Find("HUDCanvas"), Is.Null, "The retired uGUI HUD must be gone");
            Assert.That(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length, Is.EqualTo(1));
            var hud = Object.FindAnyObjectByType<TrollStrategy.UI.ColonyHud>();
            Assert.That(hud, Is.Not.Null);
            var document = hud.GetComponent<UnityEngine.UIElements.UIDocument>();
            var screen = document.parentUI as UnityEngine.UIElements.UIDocument;
            Assert.That(screen, Is.Not.Null, "The colony HUD nests in the screen UI document");
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(screen.gameObject),
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(TestUi.PrefabPath)), "The scene uses the UI prefab");
            Assert.That(screen.panelSettings, Is.Not.Null);
            Assert.That(screen.panelSettings.themeStyleSheet, Is.Not.Null);
            foreach (var part in screen.GetComponentsInChildren<UnityEngine.UIElements.UIDocument>(true))
                if (part != screen) Assert.That(part.parentUI, Is.Not.Null, $"{part.name} must nest in its parent document");
            var roots = hud.CollectRoots(_ => new UnityEngine.UIElements.VisualElement());
            Assert.That(roots.Required, Has.All.Not.Null, "Every HUD part document is wired");
            var boot = Object.FindAnyObjectByType<TrollStrategy.Bootstrap.GameBootstrap>();
            Assert.That(new SerializedObject(boot).FindProperty("_hud").objectReferenceValue, Is.EqualTo(hud));
        }

        // Building prefabs placed in the scene are the starting colony; the session must accept that layout.
        [Test]
        public void SavedScene_StartingBuildingsFormValidLayoutOnGrid()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/MainColonyScene.unity");
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                "Assets/Game/Content/Definitions/GameContentCatalog.asset");
            var worldView = Object.FindAnyObjectByType<TilemapWorldView>();
            Assert.That(worldView, Is.Not.Null);

            var placements = SceneBuildingPlacements.Collect(worldView, catalog);
            Assert.That(placements.Any(p => p.Building.Kind == BuildingKind.Warehouse), Is.True, "Scene needs a warehouse");
            Assert.That(placements.Any(p => p.Building.Kind == BuildingKind.Market), Is.True, "Scene needs a market");
            foreach (var placement in placements)
            {
                var def = catalog.GetBuilding(placement.Building.Kind);
                var expected = worldView.BuildingCenterWorld(placement.Building.Cell, def.Width, def.Height);
                Assert.That(Vector3.Distance(placement.View.transform.position, expected), Is.LessThan(.05f),
                    $"{placement.View.name} is off the grid; nearest cell is ({placement.Building.Cell.X}, {placement.Building.Cell.Y})");
            }
            Assert.DoesNotThrow(() => new GameSession(catalog, placements.Select(p => p.Building).ToList()));
        }

        [Test]
        public void SelectedBuilding_ShowsThreeDimensionalFootprintRim()
        {
            var root = CreateBuildingView(out var view, out var sourceTexture);
            var snapshot = MineSnapshot(0f);

            try
            {
                view.Setup(snapshot, root.GetComponent<SpriteRenderer>().sprite, null);
                view.UpdateVisuals(snapshot, true);

                var rim = root.transform.Find("Model/SelectionRim");
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
        public void ProducerProgress_GrowsFromLeftAtAuthoredPlaceAndHidesForWarehouse()
        {
            var root = CreateBuildingView(out var view, out var sourceTexture);

            try
            {
                var bar = root.transform.Find("ProductionProgress");
                Assert.That(bar, Is.Not.Null);
                var authored = new Vector3(0.5f, 1.2f, -3f);
                bar.localPosition = authored;
                var mine = MineSnapshot(0.25f);
                view.Setup(mine, root.GetComponent<SpriteRenderer>().sprite, null);

                var fill = bar.Find("Fill");
                var track = bar.Find("Track");
                Assert.That(fill, Is.Not.Null);
                Assert.That(track, Is.Not.Null);
                Assert.That(Vector3.Distance(bar.localPosition, authored), Is.LessThan(0.0001f),
                    "The prefab places the bar; the view only fills it");
                Assert.That(bar.gameObject.activeSelf, Is.True);
                Assert.That(track.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(track.localScale.x, Is.EqualTo(2.48f).Within(0.001f));
                Assert.That(fill.localScale.x, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(fill.localPosition.x, Is.EqualTo(-0.9f).Within(0.001f));

                var warehouse = new BuildingSnapshot(
                    "warehouse-1", BuildingKind.Warehouse, "Склад", new Cell(10, 8),
                    3, 3, 0, 500, 0, 0, 0f, 0f);
                view.UpdateVisuals(warehouse, false);
                Assert.That(bar.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sourceTexture);
            }
        }

        private static readonly BuildingKind[] AllKinds =
            Enum.GetValues(typeof(BuildingKind)).Cast<BuildingKind>().ToArray();

        private static readonly BuildingKind[] VitariaKinds =
            AllKinds.Where(kind => kind > BuildingKind.Barracks).ToArray();

        [Test]
        public void Catalog_SellsMineAndVitariaBuildingsButNotStorageOrTrade()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/Game/Content/Definitions/GameContentCatalog.asset");
            foreach (var kind in AllKinds)
            {
                bool expected = kind == BuildingKind.Mine || kind > BuildingKind.Barracks;
                Assert.That(catalog.GetBuilding(kind).Constructible, Is.EqualTo(expected), kind.ToString());
            }
        }

        [TestCaseSource(nameof(AllKinds))]
        public void BuildingPrefab_RendersItsModelOnTheMap(BuildingKind kind)
        {
            var root = CreateBuildingView(out var view, out var sourceTexture, kind);
            var map = new GameObject("TestMapView").AddComponent<TilemapWorldView>();
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/Game/Content/Definitions/GameContentCatalog.asset");
            var def = catalog.GetBuilding(kind);
            var snapshot = new BuildingSnapshot("test", kind, def.DisplayName, new Cell(0, 0),
                def.Width, def.Height, 0, def.Capacity(1), 0, def.MaxWorkers, 0f);

            try
            {
                view.Setup(snapshot, null, null, map);
                var model = root.transform.Find("Model");
                Assert.That(model, Is.Not.Null);
                Assert.That(view.Model.transform, Is.EqualTo(model));
                Assert.That(Quaternion.Angle(model.localRotation, Quaternion.identity), Is.LessThan(.01f),
                    "The model lies in the map plane of its building");
                Assert.That(model.childCount, Is.GreaterThan(1));
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
                Object.DestroyImmediate(map.gameObject);
                Object.DestroyImmediate(sourceTexture);
            }
        }

        [TestCaseSource(nameof(AllKinds))]
        public void BuildingPrefab_WiresProductionProgressBar(BuildingKind kind)
        {
            var root = PrefabUtility.LoadPrefabContents($"Assets/Game/Prefabs/Buildings/{kind}.prefab");
            try
            {
                var bar = root.transform.Find("ProductionProgress")?.GetComponent<BuildingProgressBar>();
                var track = root.transform.Find("ProductionProgress/Track")?.GetComponent<SpriteRenderer>();
                var fill = root.transform.Find("ProductionProgress/Fill")?.GetComponent<SpriteRenderer>();
                Assert.That(bar, Is.Not.Null);
                Assert.That(track, Is.Not.Null);
                Assert.That(fill, Is.Not.Null);
                var view = new SerializedObject(root.GetComponent<BuildingView>());
                Assert.That(view.FindProperty("_productionProgress").objectReferenceValue, Is.EqualTo(bar));
                var barSettings = new SerializedObject(bar);
                Assert.That(barSettings.FindProperty("_track").objectReferenceValue, Is.EqualTo(track));
                Assert.That(barSettings.FindProperty("_fill").objectReferenceValue, Is.EqualTo(fill));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase(BuildingKind.Mine, "KitMine", "Bld_Mine")]
        [TestCase(BuildingKind.Warehouse, "KitWarehouse", "Bld_House")]
        [TestCase(BuildingKind.Market, "KitMarket", "Bld_Market")]
        [TestCase(BuildingKind.Barracks, "KitBarracks", "Bld_House")]
        public void BuildingPrefab_UsesVitariaModelAndPalette(BuildingKind kind, string childName, string sourceName)
        {
            string path = $"Assets/Game/Prefabs/Buildings/{kind}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var model = root.transform.Find("Model");
                Assert.That(model, Is.Not.Null);
                var visual = model.Find(childName);
                Assert.That(visual, Is.Not.Null);
                var anchor = model.Find("EntranceAnchor");
                Assert.That(anchor, Is.Not.Null);
                if (kind == BuildingKind.Warehouse || kind == BuildingKind.Barracks)
                    Assert.That(model.Find("OpenDoorway"), Is.Not.Null);
                var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                    "Assets/Game/Content/Definitions/GameContentCatalog.asset");
                var def = catalog.GetBuilding(kind);
                // The prefab anchor is the authoring handle; the definition holds the baked entrance.
                var anchorOnMap = root.transform.InverseTransformPoint(anchor.position);
                float cellSize = catalog.Economy.CellSize;
                Assert.That(anchorOnMap.x, Is.EqualTo((def.EntranceX - def.Width * .5f) * cellSize).Within(.001f),
                    "EntranceAnchor X differs from the baked BuildingDefinition entrance");
                Assert.That(anchorOnMap.y, Is.EqualTo((def.EntranceY - def.Height * .5f) * cellSize).Within(.001f),
                    "EntranceAnchor Y differs from the baked BuildingDefinition entrance");
                Assert.That(def.CrowdSpacingCells, Is.EqualTo(model.GetComponent<BuildingModel>().CrowdSpacingCells).Within(.001f),
                    "BuildingModel crowd spacing differs from the baked BuildingDefinition");
                Assert.That(model.Find("SelectionRim")?.childCount, Is.EqualTo(4));
                Assert.That(Quaternion.Angle(root.transform.localRotation, Quaternion.Euler(90f, 0f, 0f)),
                    Is.LessThan(.01f), "Saved building prefab must stand upright in Prefab Mode");
                Assert.That(Vector3.Angle(visual.TransformDirection(Vector3.up), Vector3.up),
                    Is.LessThan(.01f), "Imported model's up axis must point upward in Prefab Mode");
                Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty);
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

        [TestCaseSource(nameof(VitariaKinds))]
        public void VitariaBuildingPrefab_FitsFootprintWithEntrance(BuildingKind kind)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/Game/Content/Definitions/GameContentCatalog.asset");
            var def = catalog.GetBuilding(kind);
            var root = PrefabUtility.LoadPrefabContents($"Assets/Game/Prefabs/Buildings/{kind}.prefab");
            try
            {
                var model = root.transform.Find("Model");
                Assert.That(model, Is.Not.Null);
                var visual = model.Find($"Kit{kind}");
                Assert.That(visual, Is.Not.Null);
                Assert.That(model.Find("EntranceAnchor"), Is.Not.Null);
                Assert.That(model.Find("SelectionRim")?.childCount, Is.EqualTo(4));
                Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty);
                root.transform.localRotation = Quaternion.identity;
                var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers, Is.Not.Empty);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    Assert.That(AssetDatabase.GetAssetPath(renderer.GetComponent<MeshFilter>()?.sharedMesh),
                        Does.EndWith($"/Bld_{kind}.fbx"));
                }
                Assert.That(bounds.size.x, Is.LessThanOrEqualTo(def.Width + .01f), "Model must fit the footprint width");
                Assert.That(bounds.size.y, Is.LessThanOrEqualTo(def.Height + .01f), "Model must fit the footprint depth");
                Assert.That(Mathf.Abs(bounds.center.x), Is.LessThan(.05f));
                Assert.That(Mathf.Abs(bounds.center.y), Is.LessThan(.05f));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject CreateBuildingView(out BuildingView view, out Texture2D sourceTexture,
            BuildingKind kind = BuildingKind.Mine)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Prefabs/Buildings/{kind}.prefab");
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
