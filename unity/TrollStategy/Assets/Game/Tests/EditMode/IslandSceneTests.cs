using System.Linq;
using NUnit.Framework;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Island;
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

        // every block the game can sell must have land on the island to raise; blocks once deleted from the scene's
        // island left their slots empty and bought land never showed
        [Test]
        public void SavedScene_IslandHasAViewForEveryLandBlock()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var island = Object.FindObjectsByType<IslandView>(FindObjectsInactive.Include).SingleOrDefault();
            Assert.That(island, Is.Not.Null);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                "Assets/Game/Content/Definitions/GameContentCatalog.asset");
            var land = LandRules.CreateStart(catalog.Economy);
            Assert.That(land, Is.Not.Null);
            Assert.That(island.BlocksPerSide, Is.EqualTo(land.BlocksPerSide));
            Assert.That(island.MissingBlocks(), Is.Empty, "every slot needs its block");
            var removed = PrefabUtility.GetRemovedGameObjects(PrefabUtility.GetOutermostPrefabInstanceRoot(island.gameObject));
            Assert.That(removed.Select(r => r.assetGameObject.name), Is.Empty,
                "nothing of the island prefab is deleted in the scene: blocks and cover puffs are all needed");
        }

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
