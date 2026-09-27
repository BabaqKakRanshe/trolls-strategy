using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Presentation.Battle
{
    /// <summary>
    /// Lights an arena battle the way its art was made. The battle scene is additive and never active, so
    /// ambient, fog and the main light would come from the colony scene, and the colony sun would add its
    /// light to the battle sun. Everything changed here is put back by <see cref="Restore"/>.
    /// </summary>
    public sealed class BattleArenaLighting
    {
        private readonly List<Light> _hiddenSuns = new();
        private bool _applied;
        private AmbientMode _ambientMode;
        private Color _ambientSky;
        private Color _ambientEquator;
        private Color _ambientGround;
        private float _ambientIntensity;
        private bool _fog;
        private Light _sun;
        private UniversalRenderPipelineAsset _pipeline;
        private float _shadowDistance;

        public void Apply(BattleArenaSet arena, Light battleSun)
        {
            if (_applied || arena == null || battleSun == null) return;
            _applied = true;
            _ambientMode = RenderSettings.ambientMode;
            _ambientSky = RenderSettings.ambientSkyColor;
            _ambientEquator = RenderSettings.ambientEquatorColor;
            _ambientGround = RenderSettings.ambientGroundColor;
            _ambientIntensity = RenderSettings.ambientIntensity;
            _fog = RenderSettings.fog;
            _sun = RenderSettings.sun;

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light == battleSun || !light.enabled || light.type != LightType.Directional) continue;
                light.enabled = false;
                _hiddenSuns.Add(light);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = arena.AmbientSky;
            RenderSettings.ambientEquatorColor = arena.AmbientEquator;
            RenderSettings.ambientGroundColor = arena.AmbientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            RenderSettings.sun = battleSun;

            if (arena.SunDirection.sqrMagnitude > 1e-6f)
                battleSun.transform.rotation = Quaternion.LookRotation(arena.SunDirection);
            battleSun.color = arena.SunColor;
            battleSun.intensity = arena.SunIntensity;
            // shadow look of the kit's Unity setup (Tools > Vitaria > 4. Setup Lighting); URP takes the biases
            // from the pipeline asset
            battleSun.shadows = LightShadows.Soft;
            battleSun.shadowStrength = .8f;

            _pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_pipeline != null) _shadowDistance = _pipeline.shadowDistance;
        }

        /// <summary>Shadows reach <paramref name="reach"/> metres past the camera focus; never shorter than before.</summary>
        public void FitShadows(float cameraDistance, float reach)
        {
            if (!_applied || _pipeline == null) return;
            _pipeline.shadowDistance = Mathf.Max(_shadowDistance, cameraDistance + reach);
        }

        public void Restore()
        {
            if (!_applied) return;
            _applied = false;
            foreach (var light in _hiddenSuns)
                if (light != null) light.enabled = true;
            _hiddenSuns.Clear();
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientSkyColor = _ambientSky;
            RenderSettings.ambientEquatorColor = _ambientEquator;
            RenderSettings.ambientGroundColor = _ambientGround;
            RenderSettings.ambientIntensity = _ambientIntensity;
            RenderSettings.fog = _fog;
            RenderSettings.sun = _sun;
            if (_pipeline != null) _pipeline.shadowDistance = _shadowDistance;
            _pipeline = null;
        }
    }
}
