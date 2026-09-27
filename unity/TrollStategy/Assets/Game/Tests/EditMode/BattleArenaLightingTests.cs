using NUnit.Framework;
using TrollStrategy.Presentation.Battle;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Tests
{
    public class BattleArenaLightingTests
    {
        [Test]
        public void ArenaLightsTheBattleAloneAndGivesTheColonyItsLightBack()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float shadowDistance = pipeline != null ? pipeline.shadowDistance : 0f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientSkyColor = Color.gray;
            RenderSettings.fog = true;
            var colonySun = new GameObject("ColonySun").AddComponent<Light>();
            colonySun.type = LightType.Directional;
            var battleSun = new GameObject("BattleSun").AddComponent<Light>();
            battleSun.type = LightType.Directional;
            var arena = new GameObject("Arena").AddComponent<BattleArenaSet>();
            var sky = new Color(.64f, .72f, .82f);
            arena.ConfigureLighting(new Vector3(0f, -1f, 1f), new Color(1f, .95f, .86f), 1.3f,
                sky, new Color(.52f, .58f, .5f), new Color(.28f, .26f, .24f));
            var sunBefore = RenderSettings.sun;
            var lighting = new BattleArenaLighting();
            try
            {
                lighting.Apply(arena, battleSun);
                lighting.FitShadows(36f, arena.ShadowReach);

                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
                Assert.That(Vector4.Distance(RenderSettings.ambientSkyColor, sky), Is.LessThan(1e-4f));
                Assert.That(RenderSettings.fog, Is.False);
                Assert.That(RenderSettings.sun, Is.SameAs(battleSun));
                Assert.That(colonySun.enabled, Is.False, "the colony sun would add its light to the battle");
                Assert.That(battleSun.intensity, Is.EqualTo(1.3f));
                Assert.That(battleSun.shadows, Is.EqualTo(LightShadows.Soft));
                Assert.That(Vector3.Angle(battleSun.transform.forward, new Vector3(0f, -1f, 1f)), Is.LessThan(.01f));
                if (pipeline != null)
                    Assert.That(pipeline.shadowDistance,
                        Is.EqualTo(Mathf.Max(shadowDistance, 36f + arena.ShadowReach)));
            }
            finally
            {
                lighting.Restore();
            }

            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
            Assert.That(Vector4.Distance(RenderSettings.ambientSkyColor, Color.gray), Is.LessThan(1e-4f));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.sun, Is.SameAs(sunBefore));
            Assert.That(colonySun.enabled, Is.True);
            if (pipeline != null) Assert.That(pipeline.shadowDistance, Is.EqualTo(shadowDistance));
        }
    }
}
