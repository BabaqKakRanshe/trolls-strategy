using System.Linq;
using NUnit.Framework;
using TrollStrategy.Presentation.Island;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Tests
{
    public class IslandHazeTests
    {
        private const string RendererPath = "Assets/Settings/Colony3DRenderer.asset";

        [Test]
        public void HazeStaysOffUntilAVolumeTurnsItOn()
        {
            var haze = ScriptableObject.CreateInstance<IslandHaze>();
            try
            {
                Assert.That(haze.IsActive(), Is.False,
                    "cameras without the colony volume, like the arena's, get no haze");
                haze.fogOpacity.Override(.9f);
                Assert.That(haze.IsActive(), Is.True);
                haze.fogOpacity.Override(0f);
                haze.edgeIntensity.Override(.5f);
                Assert.That(haze.IsActive(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(haze);
            }
        }

        [Test]
        public void ColonyRendererCarriesTheHazePassWithACompilingShader()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Assert.That(renderer, Is.Not.Null);
            var feature = renderer.rendererFeatures.OfType<IslandHazeFeature>().SingleOrDefault();
            Assert.That(feature, Is.Not.Null, "TrollStrategy > Isle > Install Island Into Colony Scene adds it");
            Assert.That(feature.isActive, Is.True);
            var shader = Resources.Load<Shader>(IslandHazeFeature.ShaderResource);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }
    }
}
