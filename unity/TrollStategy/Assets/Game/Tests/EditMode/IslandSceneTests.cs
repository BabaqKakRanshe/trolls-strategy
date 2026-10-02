using System.Linq;
using NUnit.Framework;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Island;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.WorldUi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Pivot = UnityEngine.UIElements.Pivot;

namespace TrollStrategy.Tests
{
    public class IslandSceneTests
    {
        private const string ScenePath = "Assets/Game/Scenes/MainColonyScene.unity";
        private const string RendererPath = "Assets/Settings/Colony3DRenderer.asset";

        // hand edits of the scene's island once deleted 48 blocks and moved two rows of them 10.81 m north: bought
        // land rose off its slot or not at all, and the trees of the deleted blocks lost their wind
        [Test]
        public void SavedScene_IslandIsThePrefabAsBuilt()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var island = Object.FindObjectsByType<IslandView>(FindObjectsInactive.Include).SingleOrDefault();
            Assert.That(island, Is.Not.Null);
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(island.gameObject);
            Assert.That(root, Is.Not.Null, "the island is an instance of Colony_Isle.prefab");
            const string fix = "; TrollStrategy > Isle > Reset Island In Scene To Prefab";
            Assert.That(PrefabUtility.GetRemovedGameObjects(root).Select(r => r.assetGameObject.name), Is.Empty,
                "nothing of the island is deleted in the scene" + fix);
            Assert.That(PrefabUtility.GetRemovedComponents(root), Is.Empty, "no component is removed" + fix);
            Assert.That(PrefabUtility.GetAddedGameObjects(root), Is.Empty, "nothing is added to the island" + fix);
            Assert.That(PrefabUtility.GetAddedComponents(root), Is.Empty, "no component is added" + fix);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            var foreign = PrefabUtility.GetPropertyModifications(root)
                .Where(m => !(m.target == source && m.propertyPath == "m_Name") &&
                            !(m.target == source.transform && IsPlacement(m.propertyPath)))
                .Select(m => $"{m.target.name}.{m.propertyPath}");
            Assert.That(foreign, Is.Empty, "only the island's name and placement are the scene's" + fix);
        }

        // the island shows a block where the game sells it: its lawn lies over the block's cells on the game grid
        [Test]
        public void SavedScene_EveryBlocksLawnLiesOverItsCellsOnTheGameGrid()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var island = Object.FindObjectsByType<IslandView>(FindObjectsInactive.Include).SingleOrDefault();
            var worldView = Object.FindAnyObjectByType<TilemapWorldView>();
            Assert.That(island, Is.Not.Null);
            Assert.That(worldView, Is.Not.Null);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                "Assets/Game/Content/Definitions/GameContentCatalog.asset");
            var land = LandRules.CreateStart(catalog.Economy);
            Assert.That(land, Is.Not.Null);
            Assert.That(island.BlocksPerSide, Is.EqualTo(land.BlocksPerSide));
            Assert.That(island.BrokenBlocks(), Is.Empty, "every slot has its own block at the island's origin");
            int size = land.BlockSize;
            for (int y = 0; y < land.BlocksPerSide; y++)
            for (int x = 0; x < land.BlocksPerSide; x++)
            {
                var top = island.Block(x, y).Land.Find("Top");
                Assert.That(top, Is.Not.Null, $"block {x},{y} has a lawn");
                var mesh = top.GetComponent<MeshFilter>().sharedMesh;
                var lawn = top.TransformPoint(mesh.bounds.center);
                var cells = worldView.BuildingCenterWorld(new Cell(x * size, y * size), size, size);
                Assert.That(new Vector2(lawn.x - cells.x, lawn.z - cells.z).magnitude, Is.LessThan(.05f),
                    $"the lawn of block {x},{y} stands at {lawn}, its cells at {cells}");
            }
        }

        private static bool IsPlacement(string path) =>
            path.StartsWith("m_LocalPosition") || path.StartsWith("m_LocalRotation") ||
            path.StartsWith("m_LocalEulerAnglesHint") || path.StartsWith("m_LocalScale");

        // depth of field blurred labels standing over the far clouds: they draw after post-processing now
        [Test]
        public void ColonyRendererDrawsWorldLabelsAfterPostProcessing()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Assert.That(renderer, Is.Not.Null);
            var feature = renderer.rendererFeatures.OfType<WorldUiFeature>().SingleOrDefault();
            Assert.That(feature, Is.Not.Null, "TrollStrategy > Isle > Install Island Into Colony Scene adds it");
            Assert.That(feature.isActive, Is.True);
            Assert.That(WorldUiFeature.Event, Is.EqualTo(RenderPassEvent.AfterRenderingPostProcessing));
            int layer = 1 << WorldPanel.Layer;
            Assert.That(renderer.opaqueLayerMask & layer, Is.Zero, "the opaque pass leaves world labels out");
            Assert.That(renderer.transparentLayerMask & layer, Is.Zero, "the transparent pass leaves world labels out");
        }

        [Test]
        public void WorldPanels_StandOnTheWorldLabelLayer()
        {
            var panel = WorldPanel.Create("Label", null, 25, Pivot.Center);
            try
            {
                Assert.That(panel.gameObject.layer, Is.EqualTo(WorldPanel.Layer));
            }
            finally
            {
                Object.DestroyImmediate(panel.gameObject);
            }
        }
    }
}
