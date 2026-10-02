using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Tests
{
    /// <summary>The menu's graphics choice reaches the picture: each offered level draws with its own URP asset.</summary>
    public class GraphicsQualityTests
    {
        private static UniversalRenderPipelineAsset Level(int index) =>
            QualitySettings.GetRenderPipelineAssetAt(index) as UniversalRenderPipelineAsset;

        [Test]
        public void OfferedLevels_UseTheirOwnPipelines_CheaperDownwards()
        {
            Assert.That(QualitySettings.names.Length, Is.EqualTo(6));
            var low = Level(1);
            var medium = Level(3);
            var high = Level(5);
            Assert.That(low, Is.Not.Null, "Without its own URP asset a level changes nothing on screen");
            Assert.That(medium, Is.Not.Null);
            Assert.That(high, Is.EqualTo(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset")),
                "The top level draws as the island is set up");
            Assert.That(low, Is.Not.EqualTo(medium));
            Assert.That(medium, Is.Not.EqualTo(high));

            Assert.That(low.msaaSampleCount, Is.LessThan(medium.msaaSampleCount));
            Assert.That(medium.msaaSampleCount, Is.LessThan(high.msaaSampleCount));
            Assert.That(low.mainLightShadowmapResolution, Is.LessThan(high.mainLightShadowmapResolution));
            Assert.That(low.shadowCascadeCount, Is.LessThanOrEqualTo(medium.shadowCascadeCount));
            foreach (var level in new[] { low, medium })
            {
                Assert.That(level.renderScale, Is.EqualTo(1f), "Text over the world stays sharp");
                Assert.That(level.supportsHDR, Is.EqualTo(high.supportsHDR), "The island keeps its grading and bloom");
                Assert.That(new SerializedObject(level).FindProperty("m_RendererDataList").GetArrayElementAtIndex(0)
                    .objectReferenceValue, Is.Not.Null, "Same renderer and post-processing as the top level");
            }
        }
    }
}
